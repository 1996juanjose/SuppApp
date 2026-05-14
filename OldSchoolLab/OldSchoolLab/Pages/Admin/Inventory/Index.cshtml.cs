using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using OldSchoolLab.Services;

namespace OldSchoolLab.Pages.Admin.Inventory;

[Authorize(Roles = "Gerencia")]
public class IndexModel(ApplicationDbContext db) : PageModel
{
    public IList<InventoryItem> Items { get; private set; } = new List<InventoryItem>();
    public IList<MovementItem> RecentMovements { get; private set; } = new List<MovementItem>();
    public IList<ProductOption> Products { get; private set; } = new List<ProductOption>();

    public int TotalProducts => Items.Count;

    public int TotalUnits => Items.Sum(x => x.Stock);

    public decimal TotalValue => Items.Sum(x => x.TotalValue);

    [BindProperty]
    public int MovementProductId { get; set; }

    [BindProperty]
    [Range(1, 999999)]
    public int MovementQuantity { get; set; } = 1;

    [BindProperty]
    public string MovementType { get; set; } = "Ingreso";

    [BindProperty]
    [Range(typeof(decimal), "0", "999999999")]
    public decimal MovementUnitCost { get; set; }

    [BindProperty]
    public DateTime MovementDate { get; set; } = DateTime.Today;

    [BindProperty]
    [MaxLength(250)]
    public string? MovementNotes { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAddMovementAsync()
    {
        if (MovementProductId <= 0)
        {
            ModelState.AddModelError(nameof(MovementProductId), "Selecciona un producto.");
        }

        if (!string.Equals(MovementType, "Ingreso", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(MovementType, "Egreso", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(MovementType, "Salida", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(MovementType), "Selecciona un tipo de movimiento válido.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var companyId = User.GetCompanyId();
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == MovementProductId && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (product is null)
        {
            return NotFound();
        }

        db.ProductStockMovements.Add(new ProductStockMovement
        {
            ProductId = product.Id,
            Quantity = MovementQuantity,
            UnitCost = MovementUnitCost,
            MovementType = MovementType,
            MovementDate = MovementDate.Date,
            CreatedAt = DateTime.Now,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            CreatedByUserName = User.Identity?.Name ?? string.Empty,
            Notes = MovementNotes?.Trim() ?? string.Empty
        });

        await db.SaveChangesAsync();

        TempData["StatusMessage"] = "Movimiento registrado correctamente.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var companyId = User.GetCompanyId();

        Products = await db.Products
            .AsNoTracking()
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .OrderBy(x => x.Name)
            .Select(x => new ProductOption
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync();

        var products = await db.Products
            .AsNoTracking()
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .Include(x => x.StockMovements)
            .OrderBy(x => x.Name)
            .ToListAsync();

        Items = products.Select(product =>
        {
            var stock = product.StockMovements.Sum(m =>
                string.Equals(m.MovementType, "Salida", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.MovementType, "Egreso", StringComparison.OrdinalIgnoreCase)
                    ? -m.Quantity
                    : m.Quantity);

            var lastMovement = product.StockMovements
                .OrderByDescending(m => m.MovementDate)
                .ThenByDescending(m => m.Id)
                .FirstOrDefault();

            return new InventoryItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                IsActive = product.IsActive,
                Stock = stock,
                PurchaseUnitCost = product.PurchaseUnitCost,
                TotalValue = stock * product.PurchaseUnitCost,
                MovementCount = product.StockMovements.Count,
                LastMovementDate = lastMovement?.MovementDate,
                LastMovementType = lastMovement?.MovementType ?? string.Empty
            };
        }).ToList();

        RecentMovements = await db.ProductStockMovements
            .AsNoTracking()
            .Where(x => !companyId.HasValue || x.Product.CompanyId == companyId.Value)
            .Include(x => x.Product)
            .OrderByDescending(x => x.MovementDate)
            .ThenByDescending(x => x.Id)
            .Take(20)
            .Select(x => new MovementItem
            {
                ProductName = x.Product.Name,
                MovementType = x.MovementType,
                Quantity = x.Quantity,
                UnitCost = x.UnitCost,
                TotalCost = x.Quantity * x.UnitCost,
                MovementDate = x.MovementDate,
                Notes = x.Notes
            })
            .ToListAsync();
    }

    public sealed class InventoryItem
    {
        public int ProductId { get; init; }

        public string ProductName { get; init; } = string.Empty;

        public bool IsActive { get; init; }

        public int Stock { get; init; }

        public decimal PurchaseUnitCost { get; init; }

        public decimal TotalValue { get; init; }

        public int MovementCount { get; init; }

        public DateTime? LastMovementDate { get; init; }

        public string LastMovementType { get; init; } = string.Empty;
    }

    public sealed class MovementItem
    {
        public string ProductName { get; init; } = string.Empty;

        public string MovementType { get; init; } = string.Empty;

        public int Quantity { get; init; }

        public decimal UnitCost { get; init; }

        public decimal TotalCost { get; init; }

        public DateTime MovementDate { get; init; }

        public string Notes { get; init; } = string.Empty;
    }

    public sealed class ProductOption
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }
}
