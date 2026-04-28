using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OldSchoolApi.Data;
using OldSchoolApi.Models;
using System.Security.Claims;

namespace OldSchoolApi.Controllers;

[ApiController]
[Route("api/records")]
[Authorize]
public class RecordsController(ApiDbContext db, IConfiguration config) : ControllerBase
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

        var fecha = DateTime.Today;
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
