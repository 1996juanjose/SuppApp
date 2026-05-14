using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using OldSchoolLab.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace OldSchoolLab.Pages.Records;

[Authorize(Roles = "Gerencia,Gestor")]
public class CreateModel(ApplicationDbContext db, IAuditService audit, IPaymentProofStorage paymentProofStorage) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? InitialPaymentProofFile { get; set; }

    public List<SelectListItem> StatusOptions { get; private set; } = new();
    public List<SelectListItem> ProductOptions { get; private set; } = new();
    public Dictionary<int, Dictionary<int, decimal>> ProductPriceLookup { get; private set; } = new();
    public Dictionary<int, Dictionary<int, decimal>> ProductCommissionLookup { get; private set; } = new();
    public Dictionary<int, decimal> ProductPurchaseCostLookup { get; private set; } = new();
    public Dictionary<int, int> ProductStockLookup { get; private set; } = new();

    public class InputModel
    {
        [Required]
        [Display(Name = "Estado")]
        public int StatusCatalogId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha")]
        public DateTime RecordDate { get; set; } = DateTime.Today;

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
        [Display(Name = "Pago inicial")]
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

    public async Task<IActionResult> OnGetAsync()
    {
        var companyId = User.GetCompanyId()
            ?? (await db.Companies.Where(x => x.IsActive).OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync());

        if (!companyId.HasValue)
        {
            return RedirectToPage("/Index");
        }

        await LoadLookupsAsync(companyId.Value);

        var prospectoOption = StatusOptions.FirstOrDefault(x => x.Text == "Prospecto");
        if (prospectoOption is not null && int.TryParse(prospectoOption.Value, out var prospectoId))
        {
            Input.StatusCatalogId = prospectoId;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var companyId = User.GetCompanyId()
            ?? (await db.Companies.Where(x => x.IsActive).OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync());
        if (!companyId.HasValue)
        {
            return Forbid();
        }

        await LoadLookupsAsync(companyId.Value);

        NormalizeInputWithoutProduct();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var productDetails = await ResolveProductDetailsAsync(Input.ProductId, Input.Quantity, companyId.Value);
        if (Input.ProductId.HasValue && productDetails is null)
        {
            ModelState.AddModelError("Input.Quantity", "No existe un precio configurado para ese producto y cantidad.");
            return Page();
        }

        var initialPaymentAmount = Math.Max(0m, Input.PaidAmount);
        if (InitialPaymentProofFile is not null && initialPaymentAmount <= 0)
        {
            ModelState.AddModelError(nameof(InitialPaymentProofFile), "Para adjuntar un comprobante primero se debe registrar un pago inicial mayor a 0.");
            return Page();
        }

        StoredPaymentProof? storedProof;
        try
        {
            storedProof = await paymentProofStorage.SaveAsync(InitialPaymentProofFile, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(InitialPaymentProofFile), ex.Message);
            return Page();
        }

        var total = productDetails?.SaleAmount ?? 0m;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var userName = User.Identity?.Name ?? string.Empty;
        var isClientStatus = IsClientStatus(Input.StatusCatalogId);

        var record = new CustomerRecord
        {
            CompanyId = companyId.Value,
            StatusCatalogId = Input.StatusCatalogId,
            RecordDate = Input.RecordDate,
            Cellphone = Input.Cellphone.Trim(),
            NameOrReference = Input.NameOrReference?.Trim() ?? string.Empty,
            CallActivity = Input.CallActivity?.Trim() ?? string.Empty,
            Dni = Input.Dni?.Trim() ?? string.Empty,
            ProductId = Input.ProductId,
            Quantity = Input.ProductId.HasValue ? Input.Quantity : 1,
            ProductAmount = total,
            PurchaseUnitCost = productDetails?.PurchaseUnitCost ?? 0m,
            CostAmount = productDetails?.CostAmount ?? 0m,
            CommissionRate = productDetails?.CommissionRate ?? 0m,
            CommissionAmount = productDetails?.CommissionAmount ?? 0m,
            PaidAmount = 0m,
            BalanceDue = total,
            FolderPath = Input.FolderPath?.Trim() ?? string.Empty,
            Destino = Input.Destino?.Trim() ?? string.Empty,
            Clave = Input.Clave?.Trim() ?? string.Empty,
            Guia = Input.Guia?.Trim() ?? string.Empty,
            CreatedByUserId = userId,
            CreatedByUserName = userName
        };

        db.CustomerRecords.Add(record);
        await db.SaveChangesAsync();

        if (isClientStatus && productDetails is not null && Input.ProductId.HasValue)
        {
            db.ProductStockMovements.Add(new ProductStockMovement
            {
                ProductId = Input.ProductId.Value,
                CustomerRecordId = record.Id,
                Quantity = Input.Quantity,
                UnitCost = productDetails.PurchaseUnitCost,
                MovementType = "Egreso",
                MovementDate = record.RecordDate,
                CreatedAt = DateTime.Now,
                CreatedByUserId = userId,
                CreatedByUserName = userName,
                Notes = $"Salida por registro #{record.Id}"
            });

            await db.SaveChangesAsync();
        }

        if (productDetails is not null && isClientStatus)
        {
            var availableStock = ProductStockLookup.TryGetValue(Input.ProductId ?? 0, out var stock) ? stock : 0;
            if (availableStock < Input.Quantity)
            {
                TempData["WarningMessage"] = $"Aviso: el producto seleccionando tiene stock insuficiente ({availableStock} disponible, se registró {Input.Quantity}).";
            }
        }

        if (initialPaymentAmount > 0)
        {
            db.CustomerRecordPayments.Add(new CustomerRecordPayment
            {
                CustomerRecordId = record.Id,
                Amount = initialPaymentAmount,
                PaymentDate = DateTime.Today,
                CreatedAt = DateTime.Now,
                ProofImagePath = storedProof?.PublicPath ?? string.Empty,
                ProofFileName = storedProof?.OriginalFileName ?? string.Empty,
                CreatedByUserId = userId,
                CreatedByUserName = userName
            });

            record.PaidAmount = initialPaymentAmount;
            record.BalanceDue = Math.Max(0m, total - initialPaymentAmount);

            await db.SaveChangesAsync();
        }

        await audit.LogAsync("Registro", record.Id, "Creado",
            userId,
            userName,
            new
            {
                Celular = record.Cellphone,
                Fecha = record.RecordDate.ToString("yyyy-MM-dd"),
                EstadoId = record.StatusCatalogId,
                ProductoId = record.ProductId,
                Cantidad = record.Quantity,
                Pagado = record.PaidAmount,
                Debe = record.BalanceDue,
                TieneComprobante = storedProof is not null
            });

        TempData["StatusMessage"] = "Registro creado correctamente.";
        return RedirectToPage("/Records/Index");
    }

    private async Task LoadLookupsAsync(int companyId)
    {
        var statuses = await db.Statuses
            .AsNoTracking()
            .Where(x => x.IsActive && x.CompanyId == companyId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var products = await db.Products
            .AsNoTracking()
            .Where(x => x.IsActive && x.CompanyId == companyId)
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

    private async Task<ProductSnapshot?> ResolveProductDetailsAsync(int? productId, int quantity, int companyId)
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
            .FirstOrDefaultAsync(x => x.Id == productId.Value && x.CompanyId == companyId);

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

    private bool IsClientStatus(int statusCatalogId)
    {
        var status = StatusOptions.FirstOrDefault(x => int.TryParse(x.Value, out var id) && id == statusCatalogId);
        return status?.Text is "Cliente" or "Clientes";
    }

    private sealed record ProductSnapshot(
        decimal SaleAmount,
        decimal PurchaseUnitCost,
        decimal CommissionRate,
        decimal CommissionAmount,
        decimal CostAmount);

    private void NormalizeInputWithoutProduct()
    {
        if (Input.ProductId.HasValue)
        {
            return;
        }

        Input.Quantity = 1;
        ModelState.Remove($"{nameof(Input)}.{nameof(Input.Quantity)}");
    }
}
