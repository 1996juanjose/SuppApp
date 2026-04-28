using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;

namespace OldSchoolLab.Pages.Records;

[Authorize]
public class IndexModel(ApplicationDbContext db) : PageModel
{
    public IList<CustomerRecord> Records { get; private set; } = new List<CustomerRecord>();
    public IList<StatusCatalog> Statuses { get; private set; } = new List<StatusCatalog>();

    [BindProperty(SupportsGet = true)]
    public int? StatusId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public bool CanEdit => User.IsInRole("Gerencia") || User.IsInRole("Gestor");

    public async Task OnGetAsync()
    {
        Statuses = await db.Statuses
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var query = db.CustomerRecords
            .AsNoTracking()
            .Include(x => x.StatusCatalog)
            .Include(x => x.Product)
            .OrderByDescending(x => x.RecordDate)
            .ThenByDescending(x => x.Id)
            .AsQueryable();

        if (StatusId.HasValue)
        {
            query = query.Where(x => x.StatusCatalogId == StatusId.Value);
        }

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim();
            query = query.Where(x =>
                x.Cellphone.Contains(term) ||
                x.NameOrReference.Contains(term) ||
                x.Dni.Contains(term) ||
                x.CallActivity.Contains(term));
        }

        Records = await query.ToListAsync();
    }
}
