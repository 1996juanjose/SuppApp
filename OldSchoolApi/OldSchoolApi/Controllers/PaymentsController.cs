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
public class PaymentsController(ApiDbContext db, IConfiguration config, IHttpClientFactory httpClientFactory, IWebHostEnvironment env, ILogger<PaymentsController> logger, OldSchool.Storage.RustFsPaymentProofs rustFs) : ControllerBase
{
    public class ProcessVoucherRequest
    {
        public int? CompanyId { get; set; }

        /// <summary>N�mero de celular del remitente (quien envi� el voucher por WhatsApp).</summary>
        public string Celular { get; set; } = string.Empty;

        /// <summary>Imagen del voucher en Base64 (sin prefijo data:image/...).</summary>
        public string ImageBase64 { get; set; } = string.Empty;

        /// <summary>Extensi�n de la imagen: jpg, png, etc.</summary>
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
            return BadRequest(new ProcessVoucherResponse { Message = "Celular no v�lido." });

        // Buscar el registro del cliente por celular
        var lookupCompanyId = int.TryParse(User.FindFirst("company_id")?.Value, out var userCompanyId) ? userCompanyId : request.CompanyId;
        var record = await db.CustomerRecords
            .Where(x => x.Cellphone
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace("+", string.Empty) == celular)
            .Where(x => !lookupCompanyId.HasValue || x.CompanyId == lookupCompanyId.Value)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        if (record is null)
            return NotFound(new ProcessVoucherResponse { Message = $"No se encontr� un registro con el celular {celular}." });

        // Extraer monto e info del voucher via OpenAI
        decimal montoDetectado;
        string tipoVoucher;
        string numeroOperacion;
        DateTime? yapePaymentDate;

        try
        {
            (montoDetectado, tipoVoucher, numeroOperacion, yapePaymentDate) = await ExtractVoucherDataAsync(request.ImageBase64, request.ImageExtension);
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
                Message = "No se pudo detectar el monto en la imagen. Verifica que sea un voucher Yape o Plin v�lido.",
                TipoVoucher = tipoVoucher
            });

        // Validar duplicado por n�mero de operaci�n
        if (!string.IsNullOrWhiteSpace(numeroOperacion))
        {
            var duplicado = await db.CustomerRecordPayments
                .AsNoTracking()
                .AnyAsync(x => x.OperationNumber == numeroOperacion && !x.IsReversed);

            if (duplicado)
                return Ok(new ProcessVoucherResponse
                {
                    Success = false,
                    Message = $"El voucher con n�mero de operaci�n {numeroOperacion} ya fue registrado anteriormente.",
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
        string proofPath;
        string proofFileName;
        try
        {
            if (rustFs.Enabled)
            {
                proofPath = await rustFs.SaveAsync(Convert.FromBase64String(NormalizeBase64Image(request.ImageBase64)), request.ImageExtension, HttpContext.RequestAborted);
                proofFileName = Path.GetFileName(proofPath);
            }
            else
            {
                (proofPath, proofFileName) = SaveProofImage(request.ImageBase64, request.ImageExtension, celular, publicBaseUrl);
            }
        }
        catch (FormatException)
        {
            return BadRequest(new ProcessVoucherResponse { Message = "La imagen Base64 no es valida." });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new ProcessVoucherResponse { Message = ex.Message });
        }

        // Registrar el pago
        var payment = new CustomerRecordPayment
        {
            CustomerRecordId = record.Id,
            Amount = montoDetectado,
            PaymentDate = yapePaymentDate ?? AppClock.Now(config),
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

        var paymentFullyCovered = record.ProductAmount > 0m && record.PaidAmount >= record.ProductAmount;
        var statusToApply = paymentFullyCovered
            ? clienteStatus
            : porPagarStatus;

        if (statusToApply is not null)
        {
            record.StatusCatalogId = statusToApply.Id;

            if (paymentFullyCovered)
            {
                record.CallActivity = "Conforme recibio pedido";
            }
        }

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

    private async Task<(decimal Monto, string TipoVoucher, string NumeroOperacion, DateTime? PaymentDateTime)> ExtractVoucherDataAsync(string imageBase64, string extension)
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
                                text = PaymentReceiptValidation.Instructions +
                                       " Responde solo JSON, sin markdown. Incluye monto (numero en soles), " +
                                       "tipo (Yape|Plin|BCP|Transferencia|Banco), nro_operacion (igual a operationNumber) " +
                                       "y fecha_hora (yyyy-MM-dd HH:mm:ss)."
                            },
                            new
                            {
                                type = "image_url",
                                image_url = new { url = $"data:image/{normalizedExtension};base64,{normalizedBase64}" }
                            }
                        }
                    }
                },
                max_tokens = 500
            };

            var json = JsonSerializer.Serialize(body);
            using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", httpContent);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode is 401 or 403)
                    throw new InvalidOperationException("No hay ApiKey de OpenAI configurada o es inv�lida.");

                throw new InvalidOperationException($"OpenAI respondi� con error {(int)response.StatusCode}.");
            }

            using var doc = JsonDocument.Parse(responseJson);
            var messageContent = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            var resultJson = ExtractJsonObject(messageContent);
            using var result = JsonDocument.Parse(resultJson);
            var monto = result.RootElement.GetProperty("monto").GetDecimal();
            var tipo = result.RootElement.GetProperty("tipo").GetString() ?? "Desconocido";
            var nroOp = PaymentReceiptValidation.Text(result.RootElement, "operationNumber");
            var receiptDate = PaymentReceiptValidation.Text(result.RootElement, "fecha_hora");
            if (!PaymentReceiptValidation.IsConfirmed(result.RootElement, monto, tipo, receiptDate))
                return (0m, "Desconocido", string.Empty, null);
            DateTime? paymentDateTime = null;

            if (result.RootElement.TryGetProperty("fecha_hora", out var fechaHoraElement))
            {
                var fechaHora = fechaHoraElement.GetString();
                if (!string.IsNullOrWhiteSpace(fechaHora) && DateTime.TryParse(fechaHora, out var parsedPaymentDateTime))
                {
                    paymentDateTime = parsedPaymentDateTime;
                }
            }

            return (monto, tipo, nroOp, paymentDateTime);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            return (0m, "Desconocido", string.Empty, null);
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

    private static bool IsAcceptedVoucherType(string tipoVoucher)
    {
        var normalized = tipoVoucher.Trim().ToUpperInvariant();
        return normalized is "YAPE" or "PLIN" or "BCP" or "TRANSFERENCIA" or "BANCO" or "TRANSFERENCIA BANCARIA" or "DEPOSITO" or "DEP�SITO" or "TRANSFERENCIA BCP" or "PAGO MOVIL";
    }
}
