using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OldSchoolApi.Data;
using OldSchoolApi.Models;
using OldSchoolApi.Services;
using System.Text;
using System.Text.Json;

namespace OldSchoolApi.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController(ApiDbContext db, IConfiguration config, IHttpClientFactory httpClientFactory, IWebHostEnvironment env, ILogger<PaymentsController> logger) : ControllerBase
{
    public class ProcessVoucherRequest
    {
        /// <summary>Número de celular del remitente (quien envió el voucher por WhatsApp).</summary>
        public string Celular { get; set; } = string.Empty;

        /// <summary>Imagen del voucher en Base64 (sin prefijo data:image/...).</summary>
        public string ImageBase64 { get; set; } = string.Empty;

        /// <summary>Extensión de la imagen: jpg, png, etc.</summary>
        public string ImageExtension { get; set; } = "jpg";
    }

    public class ProcessVoucherResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal? MontoDetectado { get; set; }
        public string? TipoVoucher { get; set; }
        public int? RecordId { get; set; }
        public int? PaymentId { get; set; }
    }

    /// <summary>
    /// Procesa un voucher de Yape/Plin: extrae el monto via OpenAI Vision, registra el pago
    /// y cambia el estado del registro a "Clientes".
    /// Requiere JWT Bearer token (igual que /api/records).
    /// </summary>
    [HttpPost("process-voucher")]
    public async Task<IActionResult> ProcessVoucher([FromBody] ProcessVoucherRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Celular))
            return BadRequest(new ProcessVoucherResponse { Message = "El campo Celular es obligatorio." });

        if (string.IsNullOrWhiteSpace(request.ImageBase64))
            return BadRequest(new ProcessVoucherResponse { Message = "El campo ImageBase64 es obligatorio." });

        // Normalizar celular
        var celular = new string(request.Celular.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(celular))
            return BadRequest(new ProcessVoucherResponse { Message = "Celular no válido." });

        // Buscar el registro del cliente por celular
        var record = await db.CustomerRecords
            .FirstOrDefaultAsync(x => x.Cellphone
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace("+", string.Empty) == celular);

        if (record is null)
            return NotFound(new ProcessVoucherResponse { Message = $"No se encontró un registro con el celular {celular}." });

        // Extraer monto e info del voucher via OpenAI
        decimal montoDetectado;
        string tipoVoucher;
        string numeroOperacion;

        try
        {
            (montoDetectado, tipoVoucher, numeroOperacion) = await ExtractVoucherDataAsync(request.ImageBase64, request.ImageExtension);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new ProcessVoucherResponse
            {
                Success = false,
                Message = ex.Message
            });
        }

        if (montoDetectado <= 0)
            return UnprocessableEntity(new ProcessVoucherResponse
            {
                Message = "No se pudo detectar el monto en la imagen. Verifica que sea un voucher Yape o Plin válido.",
                TipoVoucher = tipoVoucher
            });

        // Validar duplicado por número de operación
        if (!string.IsNullOrWhiteSpace(numeroOperacion))
        {
            var duplicado = await db.CustomerRecordPayments
                .AsNoTracking()
                .AnyAsync(x => x.OperationNumber == numeroOperacion && !x.IsReversed);

            if (duplicado)
                return Ok(new ProcessVoucherResponse
                {
                    Success = false,
                    Message = $"El voucher con número de operación {numeroOperacion} ya fue registrado anteriormente.",
                    MontoDetectado = montoDetectado,
                    TipoVoucher = tipoVoucher
                });
        }

        var statusBaseQuery = db.Statuses
            .AsNoTracking()
            .Where(x => x.IsActive && (!record.CompanyId.HasValue || x.CompanyId == record.CompanyId.Value));

        var clienteStatus = await statusBaseQuery.FirstOrDefaultAsync(x => x.Name == "Cliente" || x.Name == "Clientes");
        var porPagarStatus = await statusBaseQuery.FirstOrDefaultAsync(x => x.Name == "Por Pagar");

        // Guardar imagen como comprobante
        var publicBaseUrl = $"{Request.Scheme}://{Request.Host}";
        var (proofPath, proofFileName) = SaveProofImage(request.ImageBase64, request.ImageExtension, celular, publicBaseUrl);

        // Registrar el pago
        var payment = new CustomerRecordPayment
        {
            CustomerRecordId = record.Id,
            Amount = montoDetectado,
            PaymentDate = AppClock.Today(config),
            CreatedAt = AppClock.Now(config),
            ProofImagePath = proofPath,
            ProofFileName = proofFileName,
            OperationNumber = numeroOperacion,
            CreatedByUserId = "n8n",
            CreatedByUserName = "n8n"
        };

        db.CustomerRecordPayments.Add(payment);

        // Actualizar el registro: sumar pago, cambiar estado a Clientes
        record.PaidAmount += montoDetectado;
        record.BalanceDue = Math.Max(0m, record.ProductAmount - record.PaidAmount);

        var statusToApply = record.ProductAmount > 0m && record.PaidAmount >= record.ProductAmount
            ? clienteStatus
            : porPagarStatus;

        if (statusToApply is not null)
            record.StatusCatalogId = statusToApply.Id;

        await db.SaveChangesAsync();

        return Ok(new ProcessVoucherResponse
        {
            Success = true,
            Message = $"Pago de S/{montoDetectado:0.00} registrado correctamente para el celular {celular}.",
            MontoDetectado = montoDetectado,
            TipoVoucher = tipoVoucher,
            RecordId = record.Id,
            PaymentId = payment.Id
        });
    }

    private async Task<(decimal Monto, string TipoVoucher, string NumeroOperacion)> ExtractVoucherDataAsync(string imageBase64, string extension)
    {
        try
        {
            var openAiKey = config["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(openAiKey))
                throw new InvalidOperationException("No hay ApiKey de OpenAI configurada.");

            var normalizedBase64 = NormalizeBase64Image(imageBase64);
            var normalizedExtension = NormalizeImageExtension(extension);

            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiKey}");

            var body = new
            {
                model = "gpt-4o-mini",
                temperature = 0,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = "Analiza esta imagen de voucher de pago peruano (Yape o Plin). " +
                                       "Debes detectar cualquier monto visible, pequeño o grande, por ejemplo S/ 3.00, S/ 30.00, S/ 69.00 o S/ 129.00. " +
                                       "Responde SOLO con un JSON exacto, sin markdown, sin texto extra: " +
                                       "{\"monto\": 3.00, \"tipo\": \"Yape\", \"nro_operacion\": \"10113704\"}. " +
                                       "'monto' debe ser un número en soles con 2 decimales si aplica, 'tipo' debe ser 'Yape', 'Plin' o 'Desconocido', " +
                                       "'nro_operacion' debe ser solo dígitos, sin espacios ni símbolos. " +
                                       "Si no puedes leer el voucher con seguridad, devuelve {\"monto\": 0, \"tipo\": \"Desconocido\", \"nro_operacion\": \"\"}."
                            },
                            new
                            {
                                type = "image_url",
                                image_url = new { url = $"data:image/{normalizedExtension};base64,{normalizedBase64}" }
                            }
                        }
                    }
                },
                max_tokens = 150
            };

            var json = JsonSerializer.Serialize(body);
            using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", httpContent);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode is 401 or 403)
                    throw new InvalidOperationException("No hay ApiKey de OpenAI configurada o es inválida.");

                throw new InvalidOperationException($"OpenAI respondió con error {(int)response.StatusCode}.");
            }

            var doc = JsonDocument.Parse(responseJson);
            var messageContent = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            var resultJson = ExtractJsonObject(messageContent);
            var result = JsonDocument.Parse(resultJson);
            var monto = result.RootElement.GetProperty("monto").GetDecimal();
            var tipo = result.RootElement.GetProperty("tipo").GetString() ?? "Desconocido";
            var nroOp = result.RootElement.GetProperty("nro_operacion").GetString() ?? string.Empty;

            return (monto, tipo, nroOp);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            return (0m, "Desconocido", string.Empty);
        }
    }

    private (string path, string fileName) SaveProofImage(string base64, string extension, string celular, string publicBaseUrl)
    {
        try
        {
            var bytes = Convert.FromBase64String(NormalizeBase64Image(base64));
            var configuredFolder = config["Storage:PaymentProofsPath"]?.Trim();
            var folder = !string.IsNullOrWhiteSpace(configuredFolder) && Path.IsPathRooted(configuredFolder)
                ? configuredFolder
                : Path.Combine(env.ContentRootPath, "storage", "payment-proofs");
            Directory.CreateDirectory(folder);

            var ext = NormalizeImageExtension(extension);
            var fileName = $"voucher-{celular}-{AppClock.Now(config):yyyyMMddHHmmss}.{ext}";
            var fullPath = Path.Combine(folder, fileName);
            System.IO.File.WriteAllBytes(fullPath, bytes);

            logger.LogInformation("Imagen guardada en: {Path}", fullPath);
            var publicPath = string.IsNullOrWhiteSpace(publicBaseUrl)
                ? $"/payment-proofs/{fileName}"
                : $"{publicBaseUrl.TrimEnd('/')}/payment-proofs/{fileName}";

            return (publicPath, fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al guardar imagen del voucher para celular {Celular}", celular);
            return (string.Empty, string.Empty);
        }
    }

    private static string NormalizeBase64Image(string base64)
    {
        const string prefixMarker = ";base64,";
        var value = base64.Trim();

        var prefixIndex = value.IndexOf(prefixMarker, StringComparison.OrdinalIgnoreCase);
        if (prefixIndex >= 0)
        {
            return value[(prefixIndex + prefixMarker.Length)..];
        }

        return value;
    }

    private static string NormalizeImageExtension(string extension)
    {
        var value = extension.Trim().ToLowerInvariant();

        if (value.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            value = value[6..];
        }

        value = value.TrimStart('.');

        return value switch
        {
            "jpeg" => "jpeg",
            "jpg" => "jpeg",
            "png" => "png",
            "webp" => "webp",
            _ => "jpeg"
        };
    }

    private static string ExtractJsonObject(string content)
    {
        var trimmed = content.Trim();

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');

        if (start >= 0 && end > start)
        {
            return trimmed[start..(end + 1)];
        }

        return trimmed;
    }
}
