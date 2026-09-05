using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OldSchoolApi.Data;
using OldSchoolApi.Dtos;
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
    public class UpdateRecordRequest
    {
        public int StatusCatalogId { get; set; }
        public DateTime RecordDate { get; set; }
        public string Cellphone { get; set; } = string.Empty;
        public string? NameOrReference { get; set; }
        public string? CallActivity { get; set; }
        public DateTime? CallScheduledAt { get; set; }
        public bool IsCallConcrete { get; set; }
        public string? Dni { get; set; }
        public int? ProductId { get; set; }
        public int Quantity { get; set; } = 1;
        public string? FolderPath { get; set; }
        public string? Destino { get; set; }
        public string? Clave { get; set; }
        public string? Guia { get; set; }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetRecord(int id, [FromQuery] int? companyId, CancellationToken cancellationToken)
    {
        var query = db.CustomerRecords
            .AsNoTracking()
            .Where(x => x.Id == id);

        if (companyId.HasValue)
        {
            query = query.Where(x => x.CompanyId == companyId.Value);
        }

        var record = await query.Select(x => new
        {
            x.Id,
            x.StatusCatalogId,
            x.RecordDate,
            x.CreatedAt,
            cellphone = x.Cellphone,
            nameOrReference = x.NameOrReference,
            callActivity = x.CallActivity,
            x.Dni,
            x.CompanyId,
            x.ProductId,
            x.Quantity,
            x.ProductAmount,
            x.PaidAmount,
            x.BalanceDue,
            x.FolderPath,
            x.Destino,
            x.Clave,
            x.Guia,
            x.CreatedByUserId,
            x.CreatedByUserName,
            x.CallScheduledAt,
            x.IsCallConcrete,
            StatusName = x.StatusCatalog.Name,
            BadgeClass = x.StatusCatalog.Name == "Clientes" ? "success"
                : x.StatusCatalog.Name == "Rechazo" ? "danger"
                : x.StatusCatalog.Name == "Interesado" ? "warning"
                : x.StatusCatalog.Name == "Por Pagar" ? "secondary"
                : "primary",
            ProductName = x.ProductId.HasValue
                ? db.Products.Where(p => p.Id == x.ProductId.Value).Select(p => p.Name).FirstOrDefault()
                : null,
            activePaidAmount = x.PaidAmount,
            calculatedBalanceDue = x.BalanceDue
        }).FirstOrDefaultAsync(cancellationToken);

        return record is null ? NotFound() : Ok(record);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateRecord(int id, [FromBody] UpdateRecordRequest request, CancellationToken cancellationToken)
    {
        var companyId = GetCompanyId(User);

        var record = await db.CustomerRecords
            .FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value), cancellationToken);

        if (record is null)
        {
            return NotFound();
        }

        var status = await db.Statuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.StatusCatalogId && x.IsActive && (!companyId.HasValue || x.CompanyId == companyId.Value), cancellationToken);

        if (status is null)
        {
            return BadRequest(new { error = "Estado inv�lido." });
        }

        var productDetails = await ResolveProductDetailsForUpdateAsync(request.ProductId, request.Quantity, companyId, cancellationToken);
        if (request.ProductId.HasValue && productDetails is null)
        {
            return BadRequest(new { error = "No existe un precio configurado para ese producto y cantidad." });
        }

        var normalizedCellphone = NormalizeCellphone(request.Cellphone);
        if (string.IsNullOrWhiteSpace(normalizedCellphone))
        {
            return BadRequest(new { error = "El campo Celular no tiene un formato v�lido." });
        }

        var paidAmount = record.PaidAmount;
        var total = productDetails?.SaleAmount ?? 0m;

        record.StatusCatalogId = request.StatusCatalogId;
        record.RecordDate = request.RecordDate.Date;
        record.Cellphone = normalizedCellphone;
        record.NameOrReference = request.NameOrReference?.Trim() ?? string.Empty;
        record.CallActivity = request.CallActivity?.Trim() ?? string.Empty;
        record.CallScheduledAt = request.CallScheduledAt;
        record.IsCallConcrete = request.IsCallConcrete;
        record.Dni = request.Dni?.Trim() ?? string.Empty;
        record.ProductId = request.ProductId;
        record.Quantity = request.ProductId.HasValue ? Math.Max(1, request.Quantity) : 1;
        record.ProductAmount = total;
        record.PaidAmount = paidAmount;
        record.BalanceDue = Math.Max(0m, total - paidAmount);
        record.FolderPath = request.FolderPath?.Trim() ?? string.Empty;
        record.Destino = request.Destino?.Trim() ?? string.Empty;
        record.Clave = request.Clave?.Trim() ?? string.Empty;
        record.Guia = request.Guia?.Trim() ?? string.Empty;

        await db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            id = record.Id,
            statusCatalogId = record.StatusCatalogId,
            statusName = status.Name,
            recordDate = record.RecordDate,
            createdAt = record.CreatedAt,
            cellphone = record.Cellphone,
            nameOrReference = record.NameOrReference,
            callActivity = record.CallActivity,
            dni = record.Dni,
            companyId = record.CompanyId,
            productId = record.ProductId,
            productName = productDetails?.Name,
            quantity = record.Quantity,
            productAmount = record.ProductAmount,
            paidAmount = record.PaidAmount,
            balanceDue = record.BalanceDue,
            folderPath = record.FolderPath,
            destino = record.Destino,
            clave = record.Clave,
            guia = record.Guia,
            createdByUserId = record.CreatedByUserId,
            createdByUserName = record.CreatedByUserName,
            callScheduledAt = record.CallScheduledAt,
            isCallConcrete = record.IsCallConcrete,
            activePaidAmount = paidAmount,
            calculatedBalanceDue = record.BalanceDue
        });
    }

    public class CreateRecordRequest
    {
        /// <summary>N�mero de celular. Si ya existe en el sistema, no se modifica.</summary>
        public string Celular { get; set; } = string.Empty;

        /// <summary>Nombre del estado. Si no se env�a, se usa 'Prospecto'.</summary>
        public string? Estado { get; set; }

        /// <summary>Fecha (yyyy-MM-dd). Si no se env�a, se usa la fecha de hoy.</summary>
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
            return Unauthorized(new { error = "ApiKey inv�lida." });
        }

        return await CreateInternalAsync(request, "n8n", "n8n");
    }

    [HttpGet]
    public async Task<IActionResult> GetRecords([FromQuery] string? search, [FromQuery] string? fromDate, [FromQuery] string? toDate, [FromQuery] int? companyId, [FromQuery] List<int> statusIds, CancellationToken cancellationToken)
    {
        var query = db.CustomerRecords
            .AsNoTracking()
            .Include(x => x.StatusCatalog)
            .AsQueryable();

        if (companyId.HasValue)
        {
            query = query.Where(x => x.CompanyId == companyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Cellphone.Contains(term) ||
                x.NameOrReference.Contains(term) ||
                x.Dni.Contains(term));
        }

        if (statusIds.Count > 0)
        {
            query = query.Where(x => statusIds.Contains(x.StatusCatalogId));
        }

        if (DateTime.TryParse(fromDate, out var from))
        {
            query = query.Where(x => x.CreatedAt >= from.Date);
        }

        if (DateTime.TryParse(toDate, out var to))
        {
            query = query.Where(x => x.CreatedAt < to.Date.AddDays(1));
        }

        var records = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.StatusCatalogId,
                x.RecordDate,
                x.CreatedAt,
                cellphone = x.Cellphone,
                nameOrReference = x.NameOrReference,
                callActivity = x.CallActivity,
                x.Dni,
                x.CompanyId,
                x.ProductId,
                x.Quantity,
                x.ProductAmount,
                x.PaidAmount,
                x.BalanceDue,
                x.FolderPath,
                x.Destino,
                x.Clave,
                x.Guia,
                x.CreatedByUserId,
                x.CreatedByUserName,
                x.CallScheduledAt,
                x.IsCallConcrete,
                ActivePaidAmount = db.CustomerRecordPayments
                    .Where(p => p.CustomerRecordId == x.Id && !p.IsReversed)
                    .Sum(p => (decimal?)p.Amount) ?? 0m,
                CalculatedBalanceDue = Math.Max(0m,
                    x.ProductAmount - (db.CustomerRecordPayments
                        .Where(p => p.CustomerRecordId == x.Id && !p.IsReversed)
                        .Sum(p => (decimal?)p.Amount) ?? 0m)),
                StatusName = x.StatusCatalog.Name
            })
            .ToListAsync(cancellationToken);

        return Ok(records);
    }

    [HttpGet("statuses")]
    public async Task<IActionResult> GetStatuses([FromQuery] int? companyId, CancellationToken cancellationToken)
    {
        var statuses = await db.Statuses
            .AsNoTracking()
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.SortOrder,
                BadgeClass = x.Name == "Clientes" ? "success"
                    : x.Name == "Rechazo" ? "danger"
                    : x.Name == "Interesado" ? "warning"
                    : x.Name == "Por Pagar" ? "secondary"
                    : "primary"
            })
            .ToListAsync(cancellationToken);

        return Ok(statuses);
    }

    [HttpGet("current-summary")]
    public async Task<IActionResult> GetCurrentSummary([FromQuery] int? companyId, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var query = db.CustomerRecords
            .AsNoTracking()
            .Where(x => !x.IsCallConcrete)
            .Where(x => x.CallScheduledAt.HasValue)
            .Where(x => x.CallScheduledAt >= now.AddDays(-21) && x.CallScheduledAt <= now.AddMinutes(5));

        if (companyId.HasValue)
        {
            query = query.Where(x => x.CompanyId == companyId.Value);
        }

        var alerts = await query
            .OrderBy(x => x.CallScheduledAt)
            .Select(x => new
            {
                x.Id,
                x.CompanyId,
                cellphone = x.Cellphone,
                nameOrReference = x.NameOrReference,
                callActivity = x.CallActivity,
                x.CallScheduledAt,
                IsDue = x.CallScheduledAt <= now
            })
            .ToListAsync(cancellationToken);

        var nextCall = await db.CustomerRecords
            .AsNoTracking()
            .Where(x => !x.IsCallConcrete)
            .Where(x => x.CallScheduledAt.HasValue)
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .Where(x => x.CallScheduledAt > now)
            .OrderBy(x => x.CallScheduledAt)
            .Select(x => x.CallScheduledAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            now,
            nextCallScheduledAt = nextCall,
            alerts
        });
    }
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(
     [FromQuery] int? companyId,
     CancellationToken cancellationToken)
    {
        var products = await db.Products
            .AsNoTracking()
            .Where(x => x.IsActive && x.CompanyId == companyId)
            .OrderBy(x => x.Name)
            .Select(x => new RecordProductsOption
            {
                Id = x.Id,
                Name = x.Name,
                PurchaseUnitCost = x.PurchaseUnitCost,

                Prices = x.Prices
                    .Select(p => new RecordProductPriceOption
                    {
                        Id = p.Id,
                        Quantity = p.Quantity,
                        Price = p.Price
                    })
                    .ToList(),

                CommissionTiers = x.CommissionTiers
                    .Select(c => new RecordProductCommissionTierOption
                    {
                        Id = c.Id,
                        Quantity = c.Quantity,
                        CommissionRate = c.CommissionRate
                    })
                    .ToList(),

                StockMovements = x.StockMovements
                    .Select(s => new RecordProductStockMovementOption
                    {
                        Id = s.Id,
                        Quantity = s.Quantity,
                        UnitCost = s.UnitCost,
                        MovementType = s.MovementType,
                        MovementDate = s.MovementDate,
                        TotalCost = s.Quantity * s.UnitCost
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return Ok(products);
    }
    private async Task<IActionResult> CreateInternalAsync(CreateRecordRequest request, string createdByUserId, string createdByUserName)
    {
        if (string.IsNullOrWhiteSpace(request.Celular))
            return BadRequest(new { error = "El campo Celular es obligatorio." });

        var celular = NormalizeCellphone(request.Celular);

        if (string.IsNullOrWhiteSpace(celular))
            return BadRequest(new { error = "El campo Celular no tiene un formato v�lido." });

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
                message = $"El celular {celular} ya est� registrado. No se realizaron cambios."
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
            return BadRequest(new { error = "No se encontr� un estado v�lido en el sistema." });

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
            CreatedAt = AppClock.Now(config),
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
            createdAt = record.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            nombre = record.NameOrReference
        });
    }

    public class ProcessPaymentRequest
    {
        /// <summary>Celular del remitente (enviado por n8n desde WhatsApp).</summary>
        public string Celular { get; set; } = string.Empty;

        /// <summary>URL p�blica de la imagen del comprobante (Yape/Plin).</summary>
        public string? ImageUrl { get; set; }

        /// <summary>Imagen en base64 (alternativa a ImageUrl).</summary>
        public string? ImageBase64 { get; set; }
    }

    public class ProcessShipmentRequest
    {
        /// <summary>Celular del destinatario o remitente para localizar el registro.</summary>
        public string Celular { get; set; } = string.Empty;

        /// <summary>Imagen en base64.</summary>
        public string? ImageBase64 { get; set; }

        /// <summary>Extensi�n de la imagen (jpg, png, webp...).</summary>
        public string? ImageExtension { get; set; }
    }

    public class ProcessTextRequest
    {
        /// <summary>Mensaje de texto libre a interpretar con IA.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Celular enviado como campo aparte.</summary>
        public string? Cellphone { get; set; }
    }

    private class PaymentImageResult
    {
        [JsonPropertyName("amount")] public decimal Amount { get; set; }
        [JsonPropertyName("date")] public string Date { get; set; } = string.Empty;
        [JsonPropertyName("paymentType")] public string PaymentType { get; set; } = string.Empty;
        [JsonPropertyName("valid")] public bool Valid { get; set; }
    }

    private class ShipmentImageResult
    {
        [JsonPropertyName("valid")] public bool Valid { get; set; }
        [JsonPropertyName("orderNumber")] public string OrderNumber { get; set; } = string.Empty;
        [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
        [JsonPropertyName("destination")] public string Destination { get; set; } = string.Empty;
        [JsonPropertyName("recipientName")] public string RecipientName { get; set; } = string.Empty;
        [JsonPropertyName("dni")] public string Dni { get; set; } = string.Empty;
    }

    private class TextMessageResult
    {
        [JsonPropertyName("valid")] public bool Valid { get; set; }
        [JsonPropertyName("cellphone")] public string Cellphone { get; set; } = string.Empty;
        [JsonPropertyName("clientName")] public string ClientName { get; set; } = string.Empty;
        [JsonPropertyName("dni")] public string Dni { get; set; } = string.Empty;
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
            return Unauthorized(new { error = "ApiKey inv�lida." });

        if (string.IsNullOrWhiteSpace(request.Celular))
            return BadRequest(new { error = "El campo Celular es obligatorio." });

        if (string.IsNullOrWhiteSpace(request.ImageBase64))
            return BadRequest(new { error = "Se requiere ImageBase64." });

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
            return NotFound(new { error = $"No se encontr� un registro para el celular {celular}." });

        // Extraer datos del comprobante con IA
        PaymentImageResult? paymentData;

        try
        {
            paymentData = await ExtractPaymentDataFromImageAsync(request.ImageUrl, request.ImageBase64);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }

        if (paymentData is null || !paymentData.Valid || paymentData.Amount <= 0)
            return BadRequest(new { error = "No se pudo detectar un comprobante de pago v�lido en la imagen." });

        var statusBaseQuery = db.Statuses
            .AsNoTracking()
            .Where(x => x.IsActive && x.CompanyId == record.CompanyId);

        var currentStatus = await statusBaseQuery.FirstOrDefaultAsync(x => x.Id == record.StatusCatalogId);
        var clienteStatus = await statusBaseQuery.FirstOrDefaultAsync(x => x.Name == "Cliente" || x.Name == "Clientes");
        var porPagarStatus = await statusBaseQuery.FirstOrDefaultAsync(x => x.Name == "Por Pagar");

        if (currentStatus is not null && (currentStatus.Name == "Cliente" || currentStatus.Name == "Clientes"))
            return Conflict(new { error = "El registro ya est� marcado como cliente y no se pueden registrar m�s pagos." });

        // Determinar fecha del pago
        var paymentDate = DateTime.TryParse(paymentData.Date, out var parsedDate)
            ? parsedDate
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

        var statusToApply = record.ProductAmount > 0m && record.PaidAmount >= record.ProductAmount
            ? clienteStatus
            : porPagarStatus;

        if (statusToApply is not null)
            record.StatusCatalogId = statusToApply.Id;

        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            recordId = record.Id,
            celular = record.Cellphone,
            monto = paymentData.Amount,
            tipoPago = paymentData.PaymentType,
            fecha = paymentDate.ToString("yyyy-MM-dd"),
            estadoActualizado = statusToApply?.Name ?? "sin cambio",
            nuevoPagado = record.PaidAmount,
            nuevoDebe = record.BalanceDue
        });
    }

    /// <summary>
    /// Procesa una imagen de gu�a/orden de env�o desde n8n.
    /// Extrae N� de orden, c�digo y destino, y actualiza el registro localizado por celular.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("process-shipment")]
    public async Task<IActionResult> ProcessShipmentFromImage([FromBody] ProcessShipmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Celular))
            return BadRequest(new { error = "El campo Celular es obligatorio." });

        if (string.IsNullOrWhiteSpace(request.ImageBase64))
            return BadRequest(new { error = "Se requiere ImageBase64." });

        var celular = NormalizeCellphone(request.Celular);
        var record = await db.CustomerRecords
            .FirstOrDefaultAsync(x => x.Cellphone
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace("+", string.Empty) == celular);

        if (record is null)
            return NotFound(new { error = $"No se encontr� un registro para el celular {celular}." });

        var storedImage = await SaveShipmentProofImageAsync(request.ImageBase64, request.ImageExtension);

        ShipmentImageResult? shipmentData;

        try
        {
            shipmentData = await ExtractShipmentDataFromImageAsync(request.ImageBase64, request.ImageExtension);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = "Error al procesar la imagen de env�o.",
                detail = ex.Message
            });
        }

        if (shipmentData is null || !shipmentData.Valid)
            return BadRequest(new { error = "No se pudo leer la orden de env�o en la imagen." });

        var orderNumber = shipmentData.OrderNumber.Trim();
        var code = shipmentData.Code.Trim();
        var destination = shipmentData.Destination.Trim();

        if (string.IsNullOrWhiteSpace(orderNumber) && string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(destination))
            return BadRequest(new { error = "No se encontraron datos v�lidos en la imagen." });

        var resolvedDestination = await ResolveRegisteredDestinationAsync(destination) ?? destination;

        var changes = new Dictionary<string, string>();

        if (!string.Equals(record.FolderPath, orderNumber, StringComparison.Ordinal))
            changes["Ruta carpeta"] = $"{record.FolderPath} -> {orderNumber}";

        if (!string.Equals(record.Guia, code, StringComparison.Ordinal))
            changes["Gu�a"] = $"{record.Guia} -> {code}";

        if (!string.Equals(record.Destino, resolvedDestination, StringComparison.OrdinalIgnoreCase))
            changes["Destino"] = $"{record.Destino} -> {resolvedDestination}";

        record.FolderPath = orderNumber;
        record.Guia = code;
        record.Destino = resolvedDestination;
        if (!string.IsNullOrWhiteSpace(shipmentData.RecipientName))
            record.NameOrReference = shipmentData.RecipientName.Trim();

        if (!string.IsNullOrWhiteSpace(shipmentData.Dni))
        {
            record.Dni = shipmentData.Dni.Trim();
            var generatedClave = GenerateClaveFromDni(record.Dni);
            if (!string.IsNullOrWhiteSpace(generatedClave))
                record.Clave = generatedClave;
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            recordId = record.Id,
            celular = record.Cellphone,
            imagePath = storedImage.PublicPath,
            rutaCarpeta = record.FolderPath,
            guia = record.Guia,
            destino = record.Destino,
            recipientName = record.NameOrReference,
            dni = record.Dni,
            destinoDetectado = destination,
            destinoResuelto = resolvedDestination,
            changes
        });
    }

    /// <summary>
    /// Procesa un mensaje de texto con IA y actualiza solo Cliente y DNI por celular.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("process-text")]
    public async Task<IActionResult> ProcessTextMessage([FromBody] ProcessTextRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "El campo Message es obligatorio." });

        var normalizedProvidedCellphone = NormalizeCellphone(request.Cellphone ?? string.Empty);

        TextMessageResult? textData;

        try
        {
            textData = await ExtractTextDataFromMessageAsync(request.Message);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }

        if (textData is null || !textData.Valid)
            return BadRequest(new { error = "No se pudo interpretar el mensaje." });

        var celular = !string.IsNullOrWhiteSpace(normalizedProvidedCellphone)
            ? normalizedProvidedCellphone
            : NormalizeCellphone(textData.Cellphone);
        if (string.IsNullOrWhiteSpace(celular))
            return BadRequest(new { error = "No se detect� un celular v�lido en el mensaje." });

        var record = await db.CustomerRecords
            .FirstOrDefaultAsync(x => x.Cellphone
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("(", string.Empty)
                .Replace(")", string.Empty)
                .Replace("+", string.Empty) == celular);

        if (record is null)
            return NotFound(new { error = $"No se encontr� un registro para el celular {celular}." });

        var changes = new Dictionary<string, string>();

        var clientName = textData.ClientName.Trim();
        if (!string.IsNullOrWhiteSpace(clientName) && !string.Equals(record.NameOrReference, clientName, StringComparison.Ordinal))
            changes["Cliente"] = $"{record.NameOrReference} -> {clientName}";

        var dni = textData.Dni.Trim();
        if (!string.IsNullOrWhiteSpace(dni) && !string.Equals(record.Dni, dni, StringComparison.Ordinal))
            changes["DNI"] = $"{record.Dni} -> {dni}";

        var clave = string.IsNullOrWhiteSpace(dni) ? record.Clave : GenerateClaveFromDni(dni);
        if (!string.IsNullOrWhiteSpace(clave) && !string.Equals(record.Clave, clave, StringComparison.Ordinal))
            changes["Clave"] = $"{record.Clave} -> {clave}";

        record.NameOrReference = clientName;
        record.Dni = dni;
        if (!string.IsNullOrWhiteSpace(clave))
            record.Clave = clave;

        await db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            recordId = record.Id,
            celular = record.Cellphone,
            clientName = record.NameOrReference,
            dni = record.Dni,
            clave = record.Clave,
            changes
        });
    }

    private async Task<PaymentImageResult?> ExtractPaymentDataFromImageAsync(string? imageUrl, string? imageBase64)
    {
        var openAiKey = config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(openAiKey))
            throw new InvalidOperationException("No hay ApiKey de OpenAI configurada.");

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
                        text = "Analiza este comprobante de pago (Yape, Plin u otro). Responde SOLO con JSON: {\"valid\": true/false, \"amount\": n�mero, \"date\": \"yyyy-MM-dd HH:mm:ss\", \"paymentType\": \"Yape|Plin|Otro\"}. Si no es un comprobante v�lido, devuelve {\"valid\": false, \"amount\": 0, \"date\": \"\", \"paymentType\": \"\"}."
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
        {
            if ((int)response.StatusCode is 401 or 403)
                throw new InvalidOperationException("No hay ApiKey de OpenAI configurada o es inv�lida.");

            throw new InvalidOperationException($"OpenAI respondi� con error {(int)response.StatusCode}.");
        }

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

    private async Task<ShipmentImageResult?> ExtractShipmentDataFromImageAsync(string? imageBase64, string? imageExtension)
    {
        var openAiKey = config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(openAiKey))
            throw new InvalidOperationException("No hay ApiKey de OpenAI configurada.");

        var normalizedExtension = NormalizeImageExtension(imageExtension);
        var imageContent = (object)new { type = "image_url", image_url = new { url = $"data:image/{normalizedExtension};base64,{imageBase64}" } };

        var body = new
        {
            model = "gpt-4o",
            max_tokens = 250,
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
                            text = "Analiza esta imagen de r?tulo/gu?a de env?o. Extrae SOLO JSON con: {\"valid\": true/false, \"orderNumber\": \"texto exacto del N? de Orden\", \"code\": \"texto exacto del C?digo\", \"destination\": \"texto exacto del Destino\", \"recipientName\": \"texto exacto del Destinatario\", \"dni\": \"texto exacto del N? Doc o DNI\"}. Para destination, prioriza la l?nea del destino que est? resaltada, en negrita o m?s grande. Si ves una l?nea general como ciudad/provincia/distrito (por ejemplo: JULI, JULIACA, PUNO) y debajo otra l?nea m?s espec?fica como avenida, calle, jir?n o referencia (por ejemplo: Av. Lampa), devuelve la l?nea m?s espec?fica y NO la general. Si el texto tiene varias l?neas debajo de 'Destino', elige la que parezca el destino real de entrega, aunque est? m?s abajo. Si no puedes identificar con claridad esos campos, devuelve {\"valid\": false, \"orderNumber\": \"\", \"code\": \"\", \"destination\": \"\", \"recipientName\": \"\", \"dni\": \"\"}. No inventes datos."
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
        {
            if ((int)response.StatusCode is 401 or 403)
                throw new InvalidOperationException("No hay ApiKey de OpenAI configurada o es inv�lida.");

            throw new InvalidOperationException($"OpenAI respondi� con error {(int)response.StatusCode}.");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        content = content.Trim();
        if (content.StartsWith("```"))
        {
            content = content.Split('\n').Skip(1).ToArray() is var lines
                ? string.Join('\n', lines).TrimEnd('`').Trim()
                : content;
        }

        return JsonSerializer.Deserialize<ShipmentImageResult>(content);
    }

    private async Task<TextMessageResult?> ExtractTextDataFromMessageAsync(string message)
    {
        var openAiKey = config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(openAiKey))
            throw new InvalidOperationException("No hay ApiKey de OpenAI configurada.");

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
                            text = "Analiza este mensaje y responde SOLO JSON con: {\"valid\": true/false, \"cellphone\": \"numero de celular\", \"clientName\": \"nombre completo del cliente\", \"dni\": \"numero de DNI\"}. Si falta un dato, deja la cadena vac�a. No inventes informaci�n. Mensaje: " + message
                        }
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
        {
            if ((int)response.StatusCode is 401 or 403)
                throw new InvalidOperationException("No hay ApiKey de OpenAI configurada o es inv�lida.");

            throw new InvalidOperationException($"OpenAI respondi� con error {(int)response.StatusCode}.");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        content = content.Trim();
        if (content.StartsWith("```"))
        {
            content = content.Split('\n').Skip(1).ToArray() is var lines
                ? string.Join('\n', lines).TrimEnd('`').Trim()
                : content;
        }

        return JsonSerializer.Deserialize<TextMessageResult>(content);
    }

    private async Task<(string FilePath, string PublicPath)> SaveShipmentProofImageAsync(string imageBase64, string? imageExtension)
    {
        var configuredShipmentProofsPath = config["Storage:ShipmentProofsPath"]?.Trim();
        var shipmentProofsPath = !string.IsNullOrWhiteSpace(configuredShipmentProofsPath) && Path.IsPathRooted(configuredShipmentProofsPath)
            ? configuredShipmentProofsPath
            : Path.Combine(AppContext.BaseDirectory, "storage", "shipment-proofs");

        Directory.CreateDirectory(shipmentProofsPath);

        var extension = NormalizeImageExtension(imageExtension);
        var fileName = $"shipment-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.{extension}";
        var filePath = Path.Combine(shipmentProofsPath, fileName);

        var bytes = Convert.FromBase64String(SanitizeBase64Content(imageBase64));
        await global::System.IO.File.WriteAllBytesAsync(filePath, bytes);

        return (filePath, $"/shipment-proofs/{fileName}");
    }

    private static string SanitizeBase64Content(string imageBase64)
    {
        var commaIndex = imageBase64.IndexOf(',');
        return commaIndex >= 0 ? imageBase64[(commaIndex + 1)..] : imageBase64;
    }

    private static string NormalizeImageExtension(string? imageExtension)
    {
        var value = (imageExtension ?? "jpg").Trim().TrimStart('.').ToLowerInvariant();
        return value switch
        {
            "jpeg" => "jpg",
            "jpg" => "jpg",
            "png" => "png",
            "webp" => "webp",
            "gif" => "gif",
            _ => "jpg"
        };
    }

    private static string NormalizeCellphone(string celular)
    {
        return new string(celular.Where(char.IsDigit).ToArray());
    }

    private static string GenerateClaveFromDni(string dni)
    {
        var digits = new string(dni.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
            return string.Empty;

        return digits.Length >= 4
            ? digits[^4..]
            : digits.PadLeft(4, '0');
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

    private async Task<string?> ResolveRegisteredDestinationAsync(string destination)
    {
        var normalized = NormalizeDestination(destination);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var registeredDestinations = OldSchoolApi.Services.DestinosCatalog.Destinos;

        var exact = registeredDestinations.FirstOrDefault(x => string.Equals(NormalizeDestination(x), normalized, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(exact))
            return exact;

        var contains = registeredDestinations.FirstOrDefault(x =>
            NormalizeDestination(x).Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(NormalizeDestination(x), StringComparison.OrdinalIgnoreCase));

        return contains;
    }

    private static string NormalizeDestination(string destination)
    {
        var value = destination.Trim().ToUpperInvariant();
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != System.Globalization.UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        value = builder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        value = value.Replace('Ñ', 'N');
        return string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private async Task<ProductUpdateSnapshot?> ResolveProductDetailsForUpdateAsync(int? productId, int quantity, int? companyId, CancellationToken cancellationToken)
    {
        if (!productId.HasValue)
        {
            return null;
        }

        var product = await db.Products
            .AsNoTracking()
            .Include(x => x.Prices)
            .FirstOrDefaultAsync(x => x.Id == productId.Value && x.IsActive && (!companyId.HasValue || x.CompanyId == companyId.Value), cancellationToken);

        if (product is null)
        {
            return null;
        }

        var saleAmount = product.Prices.FirstOrDefault(x => x.Quantity == quantity)?.Price;
        if (saleAmount is null)
        {
            return null;
        }

        return new ProductUpdateSnapshot(product.Name, saleAmount.Value);
    }

    private static int? GetCompanyId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("company_id");
        return int.TryParse(value, out var companyId) ? companyId : null;
    }

    private sealed record ProductUpdateSnapshot(string Name, decimal SaleAmount);
}

