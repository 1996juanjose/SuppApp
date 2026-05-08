using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OldSchoolApi.Data;
using OldSchoolApi.Models;
using System.Text;
using System.Text.Json;

namespace OldSchoolApi.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentsController(ApiDbContext db, IConfiguration config, IHttpClientFactory httpClientFactory) : ControllerBase
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

        // Extraer monto e info del voucher via OCR
        var ocrResult = await ExtractVoucherDataAsync(request.ImageBase64, request.ImageExtension);
        if (ocrResult.Monto <= 0)
            return UnprocessableEntity(new ProcessVoucherResponse
            {
                Message = "No se pudo detectar el monto en la imagen. Verifica que sea un voucher Yape o Plin válido.",
                TipoVoucher = ocrResult.TipoVoucher
            });

        // Buscar el status "Clientes"
        var clientesStatus = await db.Statuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsActive && (x.Name == "Clientes" || x.Name == "Cliente"))
            ?? await db.Statuses
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .FirstOrDefaultAsync();

        // Guardar imagen como comprobante
        var (proofPath, proofFileName) = SaveProofImage(request.ImageBase64, request.ImageExtension, celular);

        // Registrar el pago
        var payment = new CustomerRecordPayment
        {
            CustomerRecordId = record.Id,
            Amount = ocrResult.Monto,
            PaymentDate = DateTime.Today,
            CreatedAt = DateTime.Now,
            ProofImagePath = proofPath,
            ProofFileName = proofFileName,
            CreatedByUserId = "n8n",
            CreatedByUserName = "n8n"
        };

        db.CustomerRecordPayments.Add(payment);

        // Actualizar el registro: sumar pago, cambiar estado a Clientes
        record.PaidAmount += ocrResult.Monto;
        record.BalanceDue = Math.Max(0m, record.ProductAmount - record.PaidAmount);

        if (clientesStatus is not null)
            record.StatusCatalogId = clientesStatus.Id;

        await db.SaveChangesAsync();

        return Ok(new ProcessVoucherResponse
        {
            Success = true,
            Message = $"Pago de S/{ocrResult.Monto:0.00} registrado correctamente para el celular {celular}.",
            MontoDetectado = ocrResult.Monto,
            TipoVoucher = ocrResult.TipoVoucher,
            RecordId = record.Id,
            PaymentId = payment.Id
        });
    }

    private async Task<(decimal Monto, string TipoVoucher)> ExtractVoucherDataAsync(string imageBase64, string extension)
    {
        try
        {
            var openAiKey = config["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("Falta configurar OpenAI:ApiKey en appsettings.");

            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiKey}");

            var body = new
            {
                model = "gpt-4o-mini",
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
                                       "Responde SOLO con un JSON con este formato exacto, sin markdown: " +
                                       "{\"monto\": 20.00, \"tipo\": \"Yape\"} " +
                                       "Donde 'monto' es el monto en soles como número decimal y 'tipo' es 'Yape', 'Plin' o 'Desconocido'. " +
                                       "Si no puedes detectar el monto, responde: {\"monto\": 0, \"tipo\": \"Desconocido\"}"
                            },
                            new
                            {
                                type = "image_url",
                                image_url = new { url = $"data:image/{extension};base64,{imageBase64}" }
                            }
                        }
                    }
                },
                max_tokens = 100
            };

            var json = JsonSerializer.Serialize(body);
            using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", httpContent);
            var responseJson = await response.Content.ReadAsStringAsync();

            var doc = JsonDocument.Parse(responseJson);
            var messageContent = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            var result = JsonDocument.Parse(messageContent.Trim());
            var monto = result.RootElement.GetProperty("monto").GetDecimal();
            var tipo = result.RootElement.GetProperty("tipo").GetString() ?? "Desconocido";

            return (monto, tipo);
        }
        catch
        {
            return (0m, "Desconocido");
        }
    }

    private static (string path, string fileName) SaveProofImage(string base64, string extension, string celular)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            var folder = Path.Combine(AppContext.BaseDirectory, "storage", "payment-proofs");
            Directory.CreateDirectory(folder);

            var fileName = $"voucher-{celular}-{DateTime.Now:yyyyMMddHHmmss}.{extension}";
            var fullPath = Path.Combine(folder, fileName);
            System.IO.File.WriteAllBytes(fullPath, bytes);

            return ($"/payment-proofs/{fileName}", fileName);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }
}
