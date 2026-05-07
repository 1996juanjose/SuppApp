using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;

namespace OldSchoolLab.Pages.Admin.Companies.Users;

[Authorize(Roles = "SuperAdmin")]
public class IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : PageModel
{
    public Company Company { get; private set; } = default!;
    public List<UserRow> Users { get; private set; } = new();

    public class UserRow
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int companyId)
    {
        Company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == companyId) ?? throw new InvalidOperationException("Empresa no encontrada.");

        var users = await db.Users
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.UserName)
            .ToListAsync();

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            Users.Add(new UserRow
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                RoleName = roles.FirstOrDefault() ?? string.Empty,
                IsActive = user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow
            });
        }

        return Page();
    }
}
