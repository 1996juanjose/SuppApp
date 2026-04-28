using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace OldSchoolLab.Pages.Records;

[Authorize(Roles = "Gerencia,Gestor")]
public class CreateModel(ApplicationDbContext db) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

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
        [Display(Name = "Pagado")]
        public decimal PaidAmount { get; set; }

        [Display(Name = "Ruta carpeta")]
        public string? FolderPath { get; set; }
    }

    public async Task OnGetAsync()
    {
        await LoadLookupsAsync();

        var prospectoOption = StatusOptions.FirstOrDefault(x => x.Text == "Prospecto");
        if (prospectoOption is not null && int.TryParse(prospectoOption.Value, out var prospectoId))
        {
            Input.StatusCatalogId = prospectoId;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadLookupsAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var productAmount = await ResolveProductAmountAsync(Input.ProductId, Input.Quantity);
        if (Input.ProductId.HasValue && productAmount is null)
        {
            ModelState.AddModelError("Input.Quantity", "No existe un precio configurado para ese producto y cantidad.");
            return Page();
        }

        var paidAmount = Math.Max(0m, Input.PaidAmount);
        var total = productAmount ?? 0m;

        var record = new CustomerRecord
        {
            StatusCatalogId = Input.StatusCatalogId,
            RecordDate = Input.RecordDate,
            Cellphone = Input.Cellphone.Trim(),
            NameOrReference = Input.NameOrReference?.Trim() ?? string.Empty,
            CallActivity = Input.CallActivity?.Trim() ?? string.Empty,
            Dni = Input.Dni?.Trim() ?? string.Empty,
            ProductId = Input.ProductId,
            Quantity = Input.ProductId.HasValue ? Input.Quantity : 1,
            ProductAmount = total,
            PaidAmount = paidAmount,
            BalanceDue = Math.Max(0m, total - paidAmount),
            FolderPath = Input.FolderPath?.Trim() ?? string.Empty,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            CreatedByUserName = User.Identity?.Name ?? string.Empty
        };

        db.CustomerRecords.Add(record);
        await db.SaveChangesAsync();

        TempData["StatusMessage"] = "Registro creado correctamente.";
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
