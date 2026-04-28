using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;

namespace OldSchoolLab.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecordsController(ApplicationDbContext db, IConfiguration config) : ControllerBase
{
    public class CreateRecordRequest
    {
        public string Celular { get; set; } = string.Empty;
        public string? Estado { get; set; }
        public string? AutoCont { get; set; }
        public string? Nombre { get; set; }
        public string? ActividadLlamada { get; set; }
        public string? Dni { get; set; }
        public string? Producto { get; set; }
        public decimal Pagado { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRecordRequest request)
    {
        // Validar API Key
        var expectedKey = config["ApiSettings:ApiKey"];
        if (!Request.Headers.TryGetValue("X-Api-Key", out var receivedKey) || receivedKey != expectedKey)
        {
            return Unauthorized(new { error = "API Key invalida." });
        }

        if (string.IsNullOrWhiteSpace(request.Celular))
        {
            return BadRequest(new { error = "El campo Celular es obligatorio." });
        }

        var celularNormalizado = request.Celular.Trim();

        // Si ya existe ese celular, no se modifica
        var existe = await db.CustomerRecords
            .AsNoTracking()
            .AnyAsync(x => x.Cellphone == celularNormalizado);

        if (existe)
        {
            return Ok(new { skipped = true, message = $"El celular {celularNormalizado} ya está registrado. No se realizaron cambios." });
        }

        // Resolver estado por nombre (default: Prospecto)
        var estadoNombre = !string.IsNullOrWhiteSpace(request.Estado) ? request.Estado.Trim() : "Prospecto";
        var status = await db.Statuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Name == estadoNombre && x.IsActive);

        if (status is null)
        {
            status = await db.Statuses
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .FirstOrDefaultAsync();
        }

        if (status is null)
        {
            return BadRequest(new { error = "No se encontró un estado válido en el sistema." });
        }

        // Resolver fecha
        var fecha = DateTime.Today;
        if (!string.IsNullOrWhiteSpace(request.AutoCont) && DateTime.TryParse(request.AutoCont, out var fechaParsed))
        {
            fecha = fechaParsed.Date;
        }

        // Resolver producto y precio
        int? productId = null;
        int quantity = 1;
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
                var precio = producto.Prices
                    .Where(p => p.Quantity == quantity)
                    .FirstOrDefault();
                productAmount = precio?.Price ?? 0m;
            }
        }

        var paidAmount = Math.Max(0m, request.Pagado);

        var record = new CustomerRecord
        {
            StatusCatalogId = status.Id,
            RecordDate = fecha,
            Cellphone = celularNormalizado,
            NameOrReference = request.Nombre?.Trim() ?? string.Empty,
            CallActivity = request.ActividadLlamada?.Trim() ?? string.Empty,
            Dni = request.Dni?.Trim() ?? string.Empty,
            ProductId = productId,
            Quantity = quantity,
            ProductAmount = productAmount,
            PaidAmount = paidAmount,
            BalanceDue = Math.Max(0m, productAmount - paidAmount),
            FolderPath = string.Empty,
            CreatedByUserId = "n8n",
            CreatedByUserName = "N8N"
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
