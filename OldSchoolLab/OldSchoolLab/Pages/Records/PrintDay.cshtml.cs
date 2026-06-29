using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using System.Security.Claims;

namespace OldSchoolLab.Pages.Records;

[Authorize]
public class PrintDayModel(ApplicationDbContext db) : PageModel
{
    public IList<CustomerRecord> Records { get; private set; } = new List<CustomerRecord>();
    public DateTime PrintedAt { get; private set; } = DateTime.Now;
    public int TotalCount => Records.Count;
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var companyIdValue = User.FindFirstValue(OldSchoolLab.Services.ClaimTypesHelper.CompanyId);
        var companyId = int.TryParse(companyIdValue, out var parsedCompanyId) ? parsedCompanyId : (int?)null;
        var sessionKey = $"records-print-day:{companyId?.ToString() ?? "global"}:{DateTime.Today:yyyyMMdd}";
        var raw = HttpContext.Session.GetString(sessionKey);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return RedirectToPage("/Records/Index");
        }

        var ids = raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0)
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return RedirectToPage("/Records/Index");
        }

        Records = await db.CustomerRecords
            .AsNoTracking()
            .Include(x => x.StatusCatalog)
            .Include(x => x.Product)
            .Where(x => ids.Contains(x.Id))
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .OrderBy(x => x.NameOrReference)
            .ThenBy(x => x.Id)
            .ToListAsync();

        if (Records.Count == 0)
        {
            return RedirectToPage("/Records/Index");
        }

        return Page();
    }

    public IActionResult OnPostBack(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return Redirect(ReturnUrl);
        }

        return RedirectToPage("/Records/Index");
    }
}
