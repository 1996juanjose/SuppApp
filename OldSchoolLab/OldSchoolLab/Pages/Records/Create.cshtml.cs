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

        var productAmount = await ResolveProductAmountAsync(Input.ProductId, Input.Quantity, companyId.Value);
        if (Input.ProductId.HasValue && productAmount is null)
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

        var total = productAmount ?? 0m;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var userName = User.Identity?.Name ?? string.Empty;

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
            PaidAmount = 0m,
            BalanceDue = total,
            FolderPath = Input.FolderPath?.Trim() ?? string.Empty,
            CreatedByUserId = userId,
            CreatedByUserName = userName
        };

        db.CustomerRecords.Add(record);
        await db.SaveChangesAsync();

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
    }

    private async Task<decimal?> ResolveProductAmountAsync(int? productId, int quantity, int companyId)
    {
        if (!productId.HasValue)
        {
            return null;
        }

        var productPrice = await db.ProductPrices
            .AsNoTracking()
            .Where(x => x.ProductId == productId.Value && x.Quantity == quantity)
            .Where(x => x.Product.CompanyId == companyId)
            .FirstOrDefaultAsync();

        return productPrice?.Price;
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
}
