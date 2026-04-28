using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;

namespace OldSchoolLab.Pages.Admin.Products;

[Authorize(Roles = "Gerencia")]
public class IndexModel(ApplicationDbContext db) : PageModel
{
    public IList<Product> Products { get; private set; } = new List<Product>();

    public async Task OnGetAsync()
    {
        Products = await db.Products
            .AsNoTracking()
            .Include(x => x.Prices.OrderBy(p => p.Quantity))
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();

        product.IsActive = !product.IsActive;
        await db.SaveChangesAsync();

        TempData["StatusMessage"] = "Producto actualizado.";
        return RedirectToPage();
    }
}
