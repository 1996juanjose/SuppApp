using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OldSchoolApi.Data;
using OldSchoolApi.Models;
using OldSchoolApi.Services;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OldSchoolApi.Controllers;

[ApiController]
[Route("api/records")]
[Authorize]
public class RecordsController(ApiDbContext db, IConfiguration config, IHttpClientFactory httpClientFactory) : ControllerBase
{
    public class CreateRecordRequest
    {
        /// <summary>Número de celular. Si ya existe en el sistema, no se modifica.</summary>
        public string Celular { get; set; } = string.Empty;

        /// <summary>Nombre del estado. Si no se envía, se usa 'Prospecto'.</summary>
        public string? Estado { get; set; }

        /// <summary>Fecha (yyyy-MM-dd). Si no se envía, se usa la fecha de hoy.</summary>
        public string? AutoCont { get; set; }

        /// <summary>Nombre o referencia WhatsApp.</summary>
        public string? Nombre { get; set; }

        /// <summary>Actividad de la llamada.</summary>
        public string? ActividadLlamada { get; set; }

        /// <summary>DNI del contacto.</summary>
        public string? Dni { get; set; }

        /// <summary>Nombre del producto (debe existir en el sistema).</summary>
        public string? Producto { get; set; }

        /// <summary>Monto pagado adelantado.</summary>
        public decimal Pagado { get; set; }
    }

    /// <summary>
    /// Crea un nuevo registro. Si el celular ya existe, no lo modifica y retorna skipped=true.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRecordRequest request)
    {
        var createdByUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "api";
        var createdByUserName = User.Identity?.Name ?? "api";

        return await CreateInternalAsync(request, createdByUserId, createdByUserName);
    }

    /// <summary>
    /// Endpoint para automatizaciones de N8N. Requiere el header X-Api-Key.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("n8n")]
    public async Task<IActionResult> CreateFromN8n([FromBody] CreateRecordRequest request)
    {
        var configuredApiKey = config["N8n:ApiKey"];

        if (string.IsNullOrWhiteSpace(configuredApiKey))
            return StatusCode(500, new { error = "La API no tiene configurada la ApiKey de N8N." });

        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKey)
            || !string.Equals(apiKey.ToString(), configuredApiKey, StringComparison.Ordinal))
        {
            return Unauthorized(new { error = "ApiKey inválida." });
        }

        return await CreateInternalAsync(request, "n8n", "n8n");
    }

    private async Task<IActionResult> CreateInternalAsync(CreateRecordRequest request, string createdByUserId, string createdByUserName)
    {
        if (string.IsNullOrWhiteSpace(request.Celular))
            return BadRequest(new { error = "El campo Celular es obligatorio." });

        var celular = NormalizeCellphone(request.Celular);

        if (string.IsNullOrWhiteSpace(celular))
            return BadRequest(new { error = "El campo Celular no tiene un formato válido." });

        var existe = await db.CustomerRecords
            .AsNoTracking()
            .AnyAsync(x => x.Cellphone
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace("+", string.Empty) == celular);

        if (existe)
        {
            return Ok(new
            {
                skipped = true,
                message = $"El celular {celular} ya está registrado. No se realizaron cambios."
            });
        }

        var estadoNombre = NormalizeStatusName(request.Estado);
        var estadoNombreNormalizado = estadoNombre.ToUpperInvariant();

        var status = await db.Statuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsActive && x.Name.ToUpper() == estadoNombreNormalizado)
            ?? await db.Statuses
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .FirstOrDefaultAsync();

        if (status is null)
            return BadRequest(new { error = "No se encontró un estado válido en el sistema." });

        var fecha = AppClock.Today(config);
        if (!string.IsNullOrWhiteSpace(request.AutoCont)
            && DateTime.TryParse(request.AutoCont, out var fechaParsed))
        {
            fecha = fechaParsed.Date;
        }

        int? productId = null;
        decimal productAmount = 0m;

        if (!string.IsNullOrWhiteSpace(request.Producto))
        {
            var productoNombre = request.Producto.Trim();
            var productoNombreNormalizado = productoNombre.ToUpperInvariant();

            var producto = await db.Products
                .AsNoTracking()
                .Include(x => x.Prices)
                .FirstOrDefaultAsync(x => x.IsActive && x.Name.ToUpper() == productoNombreNormalizado);

            if (producto is not null)
            {
                productId = producto.Id;
                productAmount = producto.Prices
                    .FirstOrDefault(p => p.Quantity == 1)?.Price ?? 0m;
            }
        }

        var paidAmount = Math.Max(0m, request.Pagado);

        var record = new CustomerRecord
        {
            StatusCatalogId = status.Id,
            CompanyId = status.CompanyId,
            RecordDate = fecha,
            Cellphone = celular,
            NameOrReference = request.Nombre?.Trim() ?? string.Empty,
            CallActivity = request.ActividadLlamada?.Trim() ?? string.Empty,
            Dni = request.Dni?.Trim() ?? string.Empty,
            ProductId = productId,
            Quantity = 1,
            ProductAmount = productAmount,
            PaidAmount = paidAmount,
            BalanceDue = Math.Max(0m, productAmount - paidAmount),
            FolderPath = string.Empty,
            CreatedByUserId = createdByUserId,
            CreatedByUserName = createdByUserName
        };

        db.CustomerRecords.Add(record);
        await db.SaveChangesAsync();

        return StatusCode(201, new
        {
            skipped = false,
            id = record.Id,
            celular = record.Cellphone,
            estado = status.Name,
            fecha = record.RecordDate.ToString("yyyy-MM-dd"),
            nombre = record.NameOrReference
        });
    }

    public class ProcessPaymentRequest
    {
        /// <summary>Celular del remitente (enviado por n8n desde WhatsApp).</summary>
        public string Celular { get; set; } = string.Empty;

        /// <summary>URL pública de la imagen del comprobante (Yape/Plin).</summary>
        public string? ImageUrl { get; set; }

        /// <summary>Imagen en base64 (alternativa a ImageUrl).</summary>
        public string? ImageBase64 { get; set; }
    }

    private class PaymentImageResult
    {
        [JsonPropertyName("amount")] public decimal Amount { get; set; }
        [JsonPropertyName("date")] public string Date { get; set; } = string.Empty;
        [JsonPropertyName("paymentType")] public string PaymentType { get; set; } = string.Empty;
        [JsonPropertyName("valid")] public bool Valid { get; set; }
    }

    /// <summary>
    /// Procesa un comprobante de pago (Yape/Plin) desde una imagen.
    /// Extrae el monto con IA, registra el pago y cambia el estado a Clientes.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("process-payment")]
    public async Task<IActionResult> ProcessPaymentFromImage([FromBody] ProcessPaymentRequest request)
    {
        var configuredApiKey = config["N8n:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredApiKey))
            return StatusCode(500, new { error = "La API no tiene configurada la ApiKey de N8N." });

        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKey)
            || !string.Equals(apiKey.ToString(), configuredApiKey, StringComparison.Ordinal))
            return Unauthorized(new { error = "ApiKey inválida." });

        if (string.IsNullOrWhiteSpace(request.Celular))
            return BadRequest(new { error = "El campo Celular es obligatorio." });

        if (string.IsNullOrWhiteSpace(request.ImageUrl) && string.IsNullOrWhiteSpace(request.ImageBase64))
            return BadRequest(new { error = "Se requiere ImageUrl o ImageBase64." });

        var celular = NormalizeCellphone(request.Celular);

        // Buscar el registro por celular
        var record = await db.CustomerRecords
            .FirstOrDefaultAsync(x => x.Cellphone
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace("+", string.Empty) == celular);

        if (record is null)
            return NotFound(new { error = $"No se encontró un registro para el celular {celular}." });

        // Extraer datos del comprobante con IA
        var paymentData = await ExtractPaymentDataFromImageAsync(request.ImageUrl, request.ImageBase64);

        if (paymentData is null || !paymentData.Valid || paymentData.Amount <= 0)
            return BadRequest(new { error = "No se pudo detectar un comprobante de pago válido en la imagen." });

        // Buscar estado "Clientes"
        var clientesStatus = await db.Statuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsActive && x.CompanyId == record.CompanyId
                && (x.Name == "Clientes" || x.Name == "Cliente"));

        // Determinar fecha del pago
        var paymentDate = DateTime.TryParse(paymentData.Date, out var parsedDate)
            ? parsedDate.Date
            : AppClock.Today(config);

        // Registrar el pago
        db.CustomerRecordPayments.Add(new CustomerRecordPayment
        {
            CustomerRecordId = record.Id,
            Amount = paymentData.Amount,
            PaymentDate = paymentDate,
            CreatedAt = AppClock.Now(config),
            ProofImagePath = request.ImageUrl ?? string.Empty,
            ProofFileName = string.Empty,
            CreatedByUserId = "n8n",
            CreatedByUserName = "n8n"
        });

        // Actualizar el registro
        record.PaidAmount += paymentData.Amount;
        record.BalanceDue = Math.Max(0m, record.ProductAmount - record.PaidAmount);

        if (clientesStatus is not null)
            record.StatusCatalogId = clientesStatus.Id;

        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            recordId = record.Id,
            celular = record.Cellphone,
            monto = paymentData.Amount,
            tipoPago = paymentData.PaymentType,
            fecha = paymentDate.ToString("yyyy-MM-dd"),
            estadoActualizado = clientesStatus?.Name ?? "sin cambio",
            nuevoPagado = record.PaidAmount,
            nuevoDebe = record.BalanceDue
        });
    }

    private async Task<PaymentImageResult?> ExtractPaymentDataFromImageAsync(string? imageUrl, string? imageBase64)
    {
        var openAiKey = config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(openAiKey))
            return null;

        var imageContent = imageUrl is not null
            ? (object)new { type = "image_url", image_url = new { url = imageUrl } }
            : (object)new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{imageBase64}" } };

        var body = new
        {
            model = "gpt-4o",
            max_tokens = 200,
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
                            text = "Analiza este comprobante de pago (Yape, Plin u otro). Responde SOLO con JSON: {\"valid\": true/false, \"amount\": número, \"date\": \"yyyy-MM-dd\", \"paymentType\": \"Yape|Plin|Otro\"}. Si no es un comprobante válido, devuelve {\"valid\": false, \"amount\": 0, \"date\": \"\", \"paymentType\": \"\"}."
                        },
                        imageContent
                    }
                }
            }
        };

        using var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiKey}");

        var json = JsonSerializer.Serialize(body);
        var response = await client.PostAsync(
            "https://api.openai.com/v1/chat/completions",
            new StringContent(json, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
            return null;

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        // Limpiar markdown si GPT devuelve ```json ... ```
        content = content.Trim();
        if (content.StartsWith("```"))
        {
            content = content.Split('\n').Skip(1).ToArray() is var lines
                ? string.Join('\n', lines).TrimEnd('`').Trim()
                : content;
        }

        return JsonSerializer.Deserialize<PaymentImageResult>(content);
    }

    private static string NormalizeCellphone(string celular)
    {
        return new string(celular.Where(char.IsDigit).ToArray());
    }

    private static string NormalizeStatusName(string? estado)
    {
        if (string.IsNullOrWhiteSpace(estado))
            return "Prospecto";

        return estado.Trim().ToUpperInvariant() switch
        {
            "CLIENTE" => "Clientes",
            _ => estado.Trim()
        };
    }
}
