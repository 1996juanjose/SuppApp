using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using OldSchoolLab.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace OldSchoolLab.Pages.Records;

[Authorize(Roles = "Gerencia,Gestor,Vendedor")]
public class EditModel(ApplicationDbContext db, IAuditService audit, IPaymentProofStorage paymentProofStorage) : PageModel
{
    [BindProperty]
    [ValidateNever]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public AddPaymentInputModel PaymentInput { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public IFormFile? PaymentProofFile { get; set; }

    public List<SelectListItem> StatusOptions { get; private set; } = new();
    public List<SelectListItem> ProductOptions { get; private set; } = new();
    public Dictionary<int, Dictionary<int, decimal>> ProductPriceLookup { get; private set; } = new();
    public Dictionary<int, Dictionary<int, decimal>> ProductCommissionLookup { get; private set; } = new();
    public Dictionary<int, decimal> ProductPurchaseCostLookup { get; private set; } = new();
    public Dictionary<int, int> ProductStockLookup { get; private set; } = new();
    public IReadOnlyList<CustomerRecordPayment> Payments { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Estado")]
        public int StatusCatalogId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha")]
        public DateTime RecordDate { get; set; }

        [Required]
        [Display(Name = "Celular")]
        public string Cellphone { get; set; } = string.Empty;

        [Display(Name = "Nombre / Ref WA")]
        public string? NameOrReference { get; set; }

        [Display(Name = "Actividad de la llamada")]
        public string? CallActivity { get; set; }

        [Display(Name = "DNI")]
        public string? Dni { get; set; }

        [Display(Name = "Producto")]
        public int? ProductId { get; set; }

        [Range(1, 999)]
        [Display(Name = "Cantidad")]    
        public int Quantity { get; set; } = 1;

        [Range(0, 100000)]
        [Display(Name = "Pagado")]
        public decimal PaidAmount { get; set; }

        [Display(Name = "Ruta carpeta")]
        public string? FolderPath { get; set; }

        [Display(Name = "Destino")]
        public string? Destino { get; set; }

        [Display(Name = "Clave")]
        public string? Clave { get; set; }

        [Display(Name = "Guía")]
        public string? Guia { get; set; }
    }

    public class AddPaymentInputModel
    {
        [Range(0.01, 100000)]
        [Display(Name = "Monto del pago")]
        public decimal Amount { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        await LoadLookupsAsync();

        var companyId = User.GetCompanyId();

        var record = await db.CustomerRecords
            .AsNoTracking()
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (record is null)
        {
            return NotFound();
        }

        Payments = record.Payments
            .OrderByDescending(x => x.PaymentDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();

        Input = new InputModel
        {
            Id = record.Id,
            StatusCatalogId = record.StatusCatalogId,
            RecordDate = record.RecordDate,
            Cellphone = record.Cellphone,
            NameOrReference = record.NameOrReference,
            CallActivity = record.CallActivity,
            Dni = record.Dni,
            ProductId = record.ProductId,
            Quantity = record.Quantity,
            PaidAmount = GetActivePaidAmount(record.Payments),
            FolderPath = record.FolderPath,
            Destino = record.Destino,
            Clave = record.Clave,
            Guia = record.Guia
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadLookupsAsync();

        NormalizeInputWithoutProduct();

        ModelState.Clear();
        if (!TryValidateModel(Input, nameof(Input)))
        {
            await LoadPaymentsAsync(Input.Id);
            return Page();
        }

        var record = await db.CustomerRecords
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == Input.Id && (!User.GetCompanyId().HasValue || x.CompanyId == User.GetCompanyId().Value));
        if (record is null)
        {
            return NotFound();
        }

        var productDetails = await ResolveProductDetailsAsync(Input.ProductId, Input.Quantity);
        if (Input.ProductId.HasValue && productDetails is null)
        {
            ModelState.AddModelError("Input.Quantity", "No existe un precio configurado para ese producto y cantidad.");
            return Page();
        }

        var total = productDetails?.SaleAmount ?? 0m;
        var paidAmount = GetActivePaidAmount(record.Payments);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var userName = User.Identity?.Name ?? string.Empty;
        var wasClient = IsClientStatus(record.StatusCatalogId);
        var willBeClient = IsClientStatus(Input.StatusCatalogId);
        var oldProductId = record.ProductId;
        var oldQuantity = record.Quantity;
        var oldPurchaseUnitCost = record.PurchaseUnitCost;

        var cambios = new Dictionary<string, string>();
        if (record.StatusCatalogId != Input.StatusCatalogId)
            cambios["Estado"] = $"{record.StatusCatalogId} ? {Input.StatusCatalogId}";
        if (record.RecordDate.Date != Input.RecordDate.Date)
            cambios["Fecha"] = $"{record.RecordDate:yyyy-MM-dd} ? {Input.RecordDate:yyyy-MM-dd}";
        var normalizedCellphone = NormalizeCellphone(Input.Cellphone);
        if (record.Cellphone != normalizedCellphone)
            cambios["Celular"] = $"{record.Cellphone} ? {normalizedCellphone}";
        if (record.Dni != (Input.Dni?.Trim() ?? string.Empty))
            cambios["DNI"] = $"{record.Dni} ? {Input.Dni?.Trim()}";
        if (record.NameOrReference != (Input.NameOrReference?.Trim() ?? string.Empty))
            cambios["Nombre/Ref"] = $"{record.NameOrReference} ? {Input.NameOrReference?.Trim()}";
        if (record.CallActivity != (Input.CallActivity?.Trim() ?? string.Empty))
            cambios["Actividad"] = $"{record.CallActivity} ? {Input.CallActivity?.Trim()}";
        if (record.ProductId != Input.ProductId)
            cambios["Producto"] = $"{record.ProductId} ? {Input.ProductId}";
        if (record.Quantity != Input.Quantity)
            cambios["Cantidad"] = $"{record.Quantity} ? {Input.Quantity}";
        if (record.FolderPath != (Input.FolderPath?.Trim() ?? string.Empty))
            cambios["Ruta"] = $"{record.FolderPath} ? {Input.FolderPath?.Trim()}";
        if (record.Destino != (Input.Destino?.Trim() ?? string.Empty))
            cambios["Destino"] = $"{record.Destino} ? {Input.Destino?.Trim()}";
        if (record.Clave != (Input.Clave?.Trim() ?? string.Empty))
            cambios["Clave"] = $"{record.Clave} ? {Input.Clave?.Trim()}";
        if (record.Guia != (Input.Guia?.Trim() ?? string.Empty))
            cambios["Guia"] = $"{record.Guia} ? {Input.Guia?.Trim()}";

        record.StatusCatalogId = Input.StatusCatalogId;
        record.RecordDate = Input.RecordDate;
        record.Cellphone = normalizedCellphone;
        record.NameOrReference = Input.NameOrReference?.Trim() ?? string.Empty;
        record.CallActivity = Input.CallActivity?.Trim() ?? string.Empty;
        record.Dni = Input.Dni?.Trim() ?? string.Empty;
        record.ProductId = Input.ProductId;
        record.Quantity = Input.ProductId.HasValue ? Input.Quantity : 1;
        record.ProductAmount = total;
        record.PurchaseUnitCost = productDetails?.PurchaseUnitCost ?? 0m;
        record.CostAmount = productDetails?.CostAmount ?? 0m;
        record.CommissionRate = productDetails?.CommissionRate ?? 0m;
        record.CommissionAmount = productDetails?.CommissionAmount ?? 0m;
        record.PaidAmount = paidAmount;
        record.BalanceDue = Math.Max(0m, total - paidAmount);
        record.FolderPath = Input.FolderPath?.Trim() ?? string.Empty;
        record.Destino = Input.Destino?.Trim() ?? string.Empty;
        record.Clave = Input.Clave?.Trim() ?? string.Empty;
        record.Guia = Input.Guia?.Trim() ?? string.Empty;

        await ApplyStockChangesAsync(record.Id, oldProductId, oldQuantity, oldPurchaseUnitCost, wasClient, Input.ProductId, Input.Quantity, record.PurchaseUnitCost, willBeClient, userId, userName, record.RecordDate);

        await db.SaveChangesAsync();

        if (cambios.Count > 0)
        {
            await audit.LogAsync("Registro", record.Id, "Actualizado",
                userId,
                userName,
                cambios);
        }

        TempData["StatusMessage"] = "Registro actualizado correctamente.";
        return RedirectToReturnUrl();
    }

    public async Task<IActionResult> OnPostAddPaymentAsync(int id)
    {
        await LoadLookupsAsync();

        var record = await db.CustomerRecords
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id && (!User.GetCompanyId().HasValue || x.CompanyId == User.GetCompanyId().Value));

        if (record is null)
        {
            return NotFound();
        }

        if (PaymentInput.Amount <= 0)
        {
            ModelState.AddModelError($"{nameof(PaymentInput)}.{nameof(PaymentInput.Amount)}", "Ingresa un monto mayor a 0.");
            await LoadRecordAsync(record);
            return Page();
        }

        StoredPaymentProof? storedProof;
        try
        {
            storedProof = await paymentProofStorage.SaveAsync(PaymentProofFile, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(PaymentProofFile), ex.Message);
            await LoadRecordAsync(record);
            return Page();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var userName = User.Identity?.Name ?? string.Empty;

        var payment = new CustomerRecordPayment
        {
            CustomerRecordId = record.Id,
            Amount = PaymentInput.Amount,
            PaymentDate = DateTime.Today,
            CreatedAt = DateTime.Now,
            ProofImagePath = storedProof?.PublicPath ?? string.Empty,
            ProofFileName = storedProof?.OriginalFileName ?? string.Empty,
            CreatedByUserId = userId,
            CreatedByUserName = userName
        };

        db.CustomerRecordPayments.Add(payment);
        record.PaidAmount = GetActivePaidAmount(record.Payments) + payment.Amount;
        record.BalanceDue = Math.Max(0m, record.ProductAmount - record.PaidAmount);

        await ApplyPaymentStatusAsync(record);

        await db.SaveChangesAsync();
        await SyncAutomaticSaleMovementAsync(record, userId, userName);

        await audit.LogAsync("Registro", record.Id, "Pago agregado",
            userId,
            userName,
            new
            {
                PagoId = payment.Id,
                Monto = payment.Amount,
                FechaPago = payment.PaymentDate.ToString("yyyy-MM-dd"),
                TieneComprobante = !string.IsNullOrWhiteSpace(payment.ProofImagePath)
            });

        TempData["StatusMessage"] = "Pago agregado correctamente.";
        return RedirectToCurrentRecord();
    }

    public async Task<IActionResult> OnPostReversePaymentAsync(int id, int paymentId)
    {
        var record = await db.CustomerRecords
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id && (!User.GetCompanyId().HasValue || x.CompanyId == User.GetCompanyId().Value));

        if (record is null)
        {
            return NotFound();
        }

        var payment = record.Payments.FirstOrDefault(x => x.Id == paymentId);
        if (payment is null)
        {
            return NotFound();
        }

        if (payment.IsReversed)
        {
            TempData["StatusMessage"] = "El pago ya fue extornado.";
            return RedirectToPage(new { id });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var userName = User.Identity?.Name ?? string.Empty;

        payment.IsReversed = true;
        payment.ReversedAt = DateTime.Now;
        payment.ReversedByUserId = userId;
        payment.ReversedByUserName = userName;

        record.PaidAmount = GetActivePaidAmount(record.Payments);
        record.BalanceDue = Math.Max(0m, record.ProductAmount - record.PaidAmount);

        await ApplyPaymentStatusAsync(record);

        await db.SaveChangesAsync();
        await SyncAutomaticSaleMovementAsync(record, userId, userName);

        await audit.LogAsync("Registro", record.Id, "Pago extornado",
            userId,
            userName,
            new
            {
                PagoId = payment.Id,
                Monto = payment.Amount,
                FechaPago = payment.PaymentDate.ToString("yyyy-MM-dd"),
                ExtornadoPor = userName,
                ExtornadoEn = payment.ReversedAt?.ToString("yyyy-MM-dd HH:mm:ss")
            });

        TempData["StatusMessage"] = "Pago extornado correctamente.";
        return RedirectToCurrentRecord();
    }

    private async Task LoadLookupsAsync()
    {
        var statuses = await db.Statuses
            .AsNoTracking()
            .Where(x => x.IsActive && (!User.GetCompanyId().HasValue || x.CompanyId == User.GetCompanyId().Value))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var products = await db.Products
            .AsNoTracking()
            .Where(x => x.IsActive && (!User.GetCompanyId().HasValue || x.CompanyId == User.GetCompanyId().Value))
            .Include(x => x.Prices)
            .Include(x => x.CommissionTiers)
            .Include(x => x.StockMovements)
            .OrderBy(x => x.Name)
            .ToListAsync();

        StatusOptions = statuses
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToList();

        ProductOptions = products
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToList();

        ProductPriceLookup = products.ToDictionary(
            x => x.Id,
            x => x.Prices.ToDictionary(p => p.Quantity, p => p.Price));

        ProductCommissionLookup = products.ToDictionary(
            x => x.Id,
            x => x.CommissionTiers.ToDictionary(p => p.Quantity, p => p.CommissionRate));

        ProductPurchaseCostLookup = products.ToDictionary(x => x.Id, x => x.PurchaseUnitCost);

        ProductStockLookup = products.ToDictionary(
            x => x.Id,
            x => x.StockMovements.Sum(m => m.MovementType == "Egreso" ? -m.Quantity : m.Quantity));
    }

    private async Task LoadPaymentsAsync(int recordId)
    {
        Payments = await db.CustomerRecordPayments
            .AsNoTracking()
            .Where(x => x.CustomerRecordId == recordId)
            .Where(x => !User.GetCompanyId().HasValue || x.CustomerRecord.CompanyId == User.GetCompanyId().Value)
            .OrderByDescending(x => x.PaymentDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    private async Task LoadRecordAsync(CustomerRecord record)
    {
        await LoadPaymentsAsync(record.Id);

        Input = new InputModel
        {
            Id = record.Id,
            StatusCatalogId = record.StatusCatalogId,
            RecordDate = record.RecordDate,
            Cellphone = record.Cellphone,
            NameOrReference = record.NameOrReference,
            CallActivity = record.CallActivity,
            Dni = record.Dni,
            ProductId = record.ProductId,
            Quantity = record.Quantity,
            PaidAmount = GetActivePaidAmount(record.Payments),
            FolderPath = record.FolderPath
        };
    }

    private static decimal GetActivePaidAmount(IEnumerable<CustomerRecordPayment> payments)
    {
        return payments.Where(x => !x.IsReversed).Sum(x => x.Amount);
    }

    private async Task ApplyPaymentStatusAsync(CustomerRecord record)
    {
        var statusBaseQuery = db.Statuses
            .AsNoTracking()
            .Where(x => x.IsActive && x.CompanyId == record.CompanyId);

        var clienteStatus = await statusBaseQuery.FirstOrDefaultAsync(x => x.Name == "Cliente" || x.Name == "Clientes");
        var porPagarStatus = await statusBaseQuery.FirstOrDefaultAsync(x => x.Name == "Por Pagar");

        var statusToApply = record.ProductAmount > 0m && record.PaidAmount >= record.ProductAmount
            ? clienteStatus
            : porPagarStatus;

        if (statusToApply is not null)
            record.StatusCatalogId = statusToApply.Id;
    }

    private async Task SyncAutomaticSaleMovementAsync(CustomerRecord record, string userId, string userName)
    {
        if (!record.ProductId.HasValue)
        {
            return;
        }

        var saleMovements = await db.ProductStockMovements
            .Where(x => x.CustomerRecordId == record.Id && (x.MovementType == "Egreso" || x.MovementType == "Salida"))
            .ToListAsync();

        if (record.BalanceDue <= 0m)
        {
            if (saleMovements.Count > 0)
            {
                return;
            }

            db.ProductStockMovements.Add(new ProductStockMovement
            {
                ProductId = record.ProductId.Value,
                CustomerRecordId = record.Id,
                Quantity = record.Quantity,
                UnitCost = record.PurchaseUnitCost,
                MovementType = "Egreso",
                MovementDate = DateTime.Today,
                CreatedAt = DateTime.Now,
                CreatedByUserId = userId,
                CreatedByUserName = userName,
                Notes = $"Salida por pago completo del registro {record.Cellphone} - Fecha {record.RecordDate:yyyy-MM-dd}"
            });

            await db.SaveChangesAsync();
            return;
        }

        if (saleMovements.Count == 0)
        {
            return;
        }

        db.ProductStockMovements.RemoveRange(saleMovements);
        await db.SaveChangesAsync();
    }

    private void NormalizeInputWithoutProduct()
    {
        if (Input.ProductId.HasValue)
        {
            return;
        }

        Input.Quantity = 1;
        ModelState.Remove($"{nameof(Input)}.{nameof(Input.Quantity)}");
    }

    private async Task<ProductSnapshot?> ResolveProductDetailsAsync(int? productId, int quantity)
    {
        if (!productId.HasValue)
        {
            return null;
        }

        var product = await db.Products
            .AsNoTracking()
            .Include(x => x.Prices)
            .Include(x => x.CommissionTiers)
            .Include(x => x.StockMovements)
            .FirstOrDefaultAsync(x => x.Id == productId.Value && (!User.GetCompanyId().HasValue || x.CompanyId == User.GetCompanyId().Value));

        if (product is null)
        {
            return null;
        }

        var saleAmount = product.Prices.FirstOrDefault(x => x.Quantity == quantity)?.Price;
        if (saleAmount is null)
        {
            return null;
        }

        var commissionRate = product.CommissionTiers.FirstOrDefault(x => x.Quantity == quantity)?.CommissionRate ?? 0m;

        return new ProductSnapshot(
            saleAmount.Value,
            product.PurchaseUnitCost,
            commissionRate,
            Math.Round(saleAmount.Value * commissionRate / 100m, 2),
            Math.Round(product.PurchaseUnitCost * quantity, 2));
    }

    private async Task ApplyStockChangesAsync(
        int recordId,
        int? oldProductId,
        int oldQuantity,
        decimal oldPurchaseUnitCost,
        bool wasClient,
        int? newProductId,
        int newQuantity,
        decimal newPurchaseUnitCost,
        bool willBeClient,
        string userId,
        string userName,
        DateTime movementDate)
    {
        if (wasClient && willBeClient
            && oldProductId == newProductId
            && oldQuantity == newQuantity)
        {
            return;
        }

        if (wasClient && oldProductId.HasValue)
        {
            db.ProductStockMovements.Add(new ProductStockMovement
            {
                ProductId = oldProductId.Value,
                CustomerRecordId = recordId,
                Quantity = oldQuantity,
                UnitCost = oldPurchaseUnitCost,
                MovementType = "Ingreso",
                MovementDate = movementDate,
                CreatedAt = DateTime.Now,
                CreatedByUserId = userId,
                CreatedByUserName = userName,
                Notes = $"Reverso por actualización de registro #{recordId}"
            });
        }

        if (willBeClient && newProductId.HasValue)
        {
            db.ProductStockMovements.Add(new ProductStockMovement
            {
                ProductId = newProductId.Value,
                CustomerRecordId = recordId,
                Quantity = newQuantity,
                UnitCost = newPurchaseUnitCost,
                MovementType = "Egreso",
                MovementDate = movementDate,
                CreatedAt = DateTime.Now,
                CreatedByUserId = userId,
                CreatedByUserName = userName,
                Notes = $"Salida por actualización de registro #{recordId}"
            });
        }

        if ((wasClient && oldProductId.HasValue) || (willBeClient && newProductId.HasValue))
        {
            var selectedProductId = newProductId ?? oldProductId;
            if (selectedProductId.HasValue)
            {
                var availableStock = ProductStockLookup.TryGetValue(selectedProductId.Value, out var stock) ? stock : 0;
                var nextStock = availableStock;

                if (wasClient && oldProductId.HasValue)
                {
                    nextStock += oldQuantity;
                }

                if (willBeClient && newProductId.HasValue)
                {
                    nextStock -= newQuantity;
                }

                if (nextStock < 0)
                {
                    TempData["WarningMessage"] = $"Aviso: el stock quedará en {nextStock} para el producto seleccionado.";
                }
            }
        }

        await db.SaveChangesAsync();
    }

    private bool IsClientStatus(int statusCatalogId)
    {
        var status = StatusOptions.FirstOrDefault(x => int.TryParse(x.Value, out var id) && id == statusCatalogId);
        return status?.Text is "Clientes";
    }

    private static string NormalizeCellphone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Replace(" ", string.Empty);
        if (normalized.StartsWith("+51"))
        {
            normalized = normalized[3..];
        }

        return normalized;
    }

    private IActionResult RedirectToReturnUrl()
    {
        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return Redirect(ReturnUrl);
        }

        return RedirectToPage("/Records/Index");
    }

    private IActionResult RedirectToCurrentRecord()
    {
        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return Redirect(ReturnUrl);
        }

        return RedirectToPage(new { id = Input.Id });
    }

    private sealed record ProductSnapshot(
        decimal SaleAmount,
        decimal PurchaseUnitCost,
        decimal CommissionRate,
        decimal CommissionAmount,
        decimal CostAmount);
}
