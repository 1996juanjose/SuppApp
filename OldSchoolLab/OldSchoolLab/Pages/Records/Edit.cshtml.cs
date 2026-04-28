using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using System.ComponentModel.DataAnnotations;

namespace OldSchoolLab.Pages.Records;

[Authorize(Roles = "Gerencia,Gestor")]
public class EditModel(ApplicationDbContext db) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> StatusOptions { get; private set; } = new();
    public List<SelectListItem> ProductOptions { get; private set; } = new();
    public Dictionary<int, Dictionary<int, decimal>> ProductPriceLookup { get; private set; } = new();

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

    public async Task<IActionResult> OnGetAsync(int id)
    {
        await LoadLookupsAsync();

        var record = await db.CustomerRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (record is null)
        {
            return NotFound();
        }

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
            PaidAmount = record.PaidAmount,
            FolderPath = record.FolderPath
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadLookupsAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var record = await db.CustomerRecords.FirstOrDefaultAsync(x => x.Id == Input.Id);
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
        var paidAmount = Math.Max(0m, Input.PaidAmount);

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

        TempData["StatusMessage"] = "Registro actualizado correctamente.";
        return RedirectToPage("/Records/Index");
    }

    private async Task LoadLookupsAsync()
    {
        var statuses = await db.Statuses
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var products = await db.Products
            .AsNoTracking()
            .Where(x => x.IsActive)
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

    private async Task<decimal?> ResolveProductAmountAsync(int? productId, int quantity)
    {
        if (!productId.HasValue)
        {
            return null;
        }

        var productPrice = await db.ProductPrices
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProductId == productId.Value && x.Quantity == quantity);

        return productPrice?.Price;
    }
}
