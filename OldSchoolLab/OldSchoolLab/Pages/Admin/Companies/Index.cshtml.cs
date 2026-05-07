using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;

namespace OldSchoolLab.Pages.Admin.Companies;

[Authorize(Roles = "SuperAdmin")]
public class IndexModel(ApplicationDbContext db) : PageModel
{
    public IList<Company> Companies { get; private set; } = new List<Company>();

    public async Task OnGetAsync()
    {
        Companies = await db.Companies
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync();
    }
}
