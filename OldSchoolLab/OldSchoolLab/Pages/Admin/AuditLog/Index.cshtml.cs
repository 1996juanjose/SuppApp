using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Services;

namespace OldSchoolLab.Pages.Admin.AuditLog;

[Authorize(Roles = "Gerencia")]
public class IndexModel(ApplicationDbContext db) : PageModel
{
    public IList<OldSchoolLab.Models.AuditLog> Logs { get; private set; } = new List<OldSchoolLab.Models.AuditLog>();

    public string? FilterTable { get; set; }
    public string? FilterUser { get; set; }

    public async Task OnGetAsync(string? table, string? user)
    {
        FilterTable = table;
        FilterUser = user;

        var companyId = User.GetCompanyId();

        var query = db.AuditLogs.AsNoTracking().AsQueryable();

        if (companyId.HasValue)
            query = query.Where(x => x.CompanyId == companyId.Value);

        if (!string.IsNullOrWhiteSpace(table))
            query = query.Where(x => x.TableName == table);

        if (!string.IsNullOrWhiteSpace(user))
            query = query.Where(x => x.ChangedByUserName.Contains(user));

        Logs = await query
            .OrderByDescending(x => x.ChangedAt)
            .Take(300)
            .ToListAsync();
    }
}
