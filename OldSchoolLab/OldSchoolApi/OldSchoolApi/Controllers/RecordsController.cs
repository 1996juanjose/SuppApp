using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OldSchoolApi.Data;
using OldSchoolApi.Models;

namespace OldSchoolApi.Controllers;

[ApiController]
[Route("api/records")]
[Authorize]
public class RecordsController(ApiDbContext db) : ControllerBase
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
        if (string.IsNullOrWhiteSpace(request.Celular))
            return BadRequest(new { error = "El campo Celular es obligatorio." });

        var celular = request.Celular.Trim();

        // Regla principal: si ya existe el celular ? no modificar
        var existe = await db.CustomerRecords
            .AsNoTracking()
            .AnyAsync(x => x.Cellphone == celular);

        if (existe)
        {
            return Ok(new
            {
                skipped = true,
                message = $"El celular {celular} ya está registrado. No se realizaron cambios."
            });
        }

        // Resolver estado por nombre
        var estadoNombre = !string.IsNullOrWhiteSpace(request.Estado)
            ? request.Estado.Trim()
            : "Prospecto";

        var status = await db.Statuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Name == estadoNombre && x.IsActive)
            ?? await db.Statuses
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .FirstOrDefaultAsync();

        if (status is null)
            return BadRequest(new { error = "No se encontró un estado válido en el sistema." });

        // Resolver fecha
        var fecha = DateTime.Today;
        if (!string.IsNullOrWhiteSpace(request.AutoCont)
            && DateTime.TryParse(request.AutoCont, out var fechaParsed))
        {
            fecha = fechaParsed.Date;
        }

        // Resolver producto y precio
        int? productId = null;
        decimal productAmount = 0m;

        if (!string.IsNullOrWhiteSpace(request.Producto))
        {
            var producto = await db.Products
                .AsNoTracking()
                .Include(x => x.Prices)
                .FirstOrDefaultAsync(x => x.Name == request.Producto.Trim() && x.IsActive);

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
            CreatedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "api",
            CreatedByUserName = User.Identity?.Name ?? "api"
        };

        db.CustomerRecords.Add(record);
        await db.SaveChangesAsync();

        return StatusCode(201, new
        {
            skipped = false,
            id = record.Id,
            celular = record.Cellphone,
            estado = estadoNombre,
            fecha = record.RecordDate.ToString("yyyy-MM-dd"),
            nombre = record.NameOrReference
        });
    }
}
