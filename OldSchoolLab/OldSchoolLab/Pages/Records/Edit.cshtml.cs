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

[Authorize(Roles = "Gerencia,Gestor")]
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
    public IReadOnlyList<CustomerRecordPayment> Payments { get; private set; } = [];

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
            FolderPath = record.FolderPath
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

        var productAmount = await ResolveProductAmountAsync(Input.ProductId, Input.Quantity);
        if (Input.ProductId.HasValue && productAmount is null)
        {
            ModelState.AddModelError("Input.Quantity", "No existe un precio configurado para ese producto y cantidad.");
            return Page();
        }

        var total = productAmount ?? 0m;
        var paidAmount = GetActivePaidAmount(record.Payments);

        var cambios = new Dictionary<string, string>();
        if (record.StatusCatalogId != Input.StatusCatalogId)
            cambios["Estado"] = $"{record.StatusCatalogId} ? {Input.StatusCatalogId}";
        if (record.RecordDate.Date != Input.RecordDate.Date)
            cambios["Fecha"] = $"{record.RecordDate:yyyy-MM-dd} ? {Input.RecordDate:yyyy-MM-dd}";
        if (record.Cellphone != Input.Cellphone.Trim())
            cambios["Celular"] = $"{record.Cellphone} ? {Input.Cellphone.Trim()}";
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

        record.StatusCatalogId = Input.StatusCatalogId;
        record.RecordDate = Input.RecordDate;
        record.Cellphone = Input.Cellphone.Trim();
        record.NameOrReference = Input.NameOrReference?.Trim() ?? string.Empty;
        record.CallActivity = Input.CallActivity?.Trim() ?? string.Empty;
        record.Dni = Input.Dni?.Trim() ?? string.Empty;
        record.ProductId = Input.ProductId;
        record.Quantity = Input.ProductId.HasValue ? Input.Quantity : 1;
        record.ProductAmount = total;
        record.PaidAmount = paidAmount;
        record.BalanceDue = Math.Max(0m, total - paidAmount);
        record.FolderPath = Input.FolderPath?.Trim() ?? string.Empty;

        await db.SaveChangesAsync();

        if (cambios.Count > 0)
        {
            await audit.LogAsync("Registro", record.Id, "Actualizado",
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                User.Identity?.Name ?? string.Empty,
                cambios);
        }

        TempData["StatusMessage"] = "Registro actualizado correctamente.";
        return RedirectToPage("/Records/Index");
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

        await db.SaveChangesAsync();

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
        return RedirectToPage(new { id });
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

        await db.SaveChangesAsync();

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
        return RedirectToPage(new { id });
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

    private void NormalizeInputWithoutProduct()
    {
        if (Input.ProductId.HasValue)
        {
            return;
        }

        Input.Quantity = 1;
        ModelState.Remove($"{nameof(Input)}.{nameof(Input.Quantity)}");
    }

    private async Task<decimal?> ResolveProductAmountAsync(int? productId, int quantity)
    {
        if (!productId.HasValue)
        {
            return null;
        }

        var productPrice = await db.ProductPrices
            .AsNoTracking()
            .Where(x => x.ProductId == productId.Value && x.Quantity == quantity)
            .Where(x => !User.GetCompanyId().HasValue || x.Product.CompanyId == User.GetCompanyId().Value)
            .FirstOrDefaultAsync();

        return productPrice?.Price;
    }
}
