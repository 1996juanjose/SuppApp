using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using OldSchoolLab.Services;

namespace OldSchoolLab.Pages.Admin.Products;

[Authorize(Roles = "Gerencia")]
public class IndexModel(ApplicationDbContext db) : PageModel
{
    public IList<Product> Products { get; private set; } = new List<Product>();

    public async Task OnGetAsync()
    {
        var companyId = User.GetCompanyId();

        Products = await db.Products
            .AsNoTracking()
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .Include(x => x.Prices.OrderBy(p => p.Quantity))
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var companyId = User.GetCompanyId();
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));
        if (product is null) return NotFound();

        product.IsActive = !product.IsActive;
        await db.SaveChangesAsync();

        TempData["StatusMessage"] = "Producto actualizado.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetAuditAsync(int id)
    {
        var logs = await db.AuditLogs
            .AsNoTracking()
            .Where(x => x.TableName == "Producto" && x.RecordId == id)
            .Where(x => !User.GetCompanyId().HasValue || x.CompanyId == User.GetCompanyId().Value)
            .OrderByDescending(x => x.ChangedAt)
            .ToListAsync();

        return new JsonResult(logs.Select(x => new
        {
            x.Action,
            x.ChangedByUserName,
            ChangedAt = x.ChangedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            x.Details
        }));
    }
}
