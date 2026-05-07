using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using OldSchoolLab.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace OldSchoolLab.Pages.Admin.Companies.Users;

[Authorize(Roles = "SuperAdmin")]
public class EditModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IAuditService audit) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Company Company { get; private set; } = default!;
    public List<SelectListItem> RoleOptions { get; private set; } = new();

    public class InputModel
    {
        public string Id { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Usuario")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "Nueva contraseña")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Rol")]
        public string RoleName { get; set; } = string.Empty;

        [Display(Name = "Activo")]
        public bool IsActive { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync(int companyId, string? id = null)
    {
        await LoadCompanyAsync(companyId);
        await LoadRolesAsync();

        if (string.IsNullOrWhiteSpace(id))
        {
            return Page();
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await userManager.GetRolesAsync(user);
        Input = new InputModel
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            RoleName = roles.FirstOrDefault() ?? string.Empty,
            IsActive = user.LockoutEnd is null || user.LockoutEnd <= DateTimeOffset.UtcNow
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int companyId)
    {
        await LoadCompanyAsync(companyId);
        await LoadRolesAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var existingByUserName = await db.Users.FirstOrDefaultAsync(x => x.NormalizedUserName == Input.UserName.Trim().ToUpper() && x.Id != Input.Id);
        if (existingByUserName is not null)
        {
            ModelState.AddModelError(nameof(Input.UserName), "Ya existe un usuario con ese nombre.");
            return Page();
        }

        ApplicationUser user;
        var isNew = string.IsNullOrWhiteSpace(Input.Id);
        if (isNew)
        {
            if (string.IsNullOrWhiteSpace(Input.Password))
            {
                ModelState.AddModelError(nameof(Input.Password), "La contraseña es obligatoria para un usuario nuevo.");
                return Page();
            }

            user = new ApplicationUser
            {
                UserName = Input.UserName.Trim(),
                Email = $"{Input.UserName.Trim()}.{companyId}@local.test",
                EmailConfirmed = true,
                CompanyId = companyId,
                LockoutEnabled = true,
                LockoutEnd = Input.IsActive ? null : DateTimeOffset.MaxValue
            };

            var createResult = await userManager.CreateAsync(user, Input.Password);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return Page();
            }
        }
        else
        {
            user = await db.Users.FirstOrDefaultAsync(x => x.Id == Input.Id && x.CompanyId == companyId) ?? throw new InvalidOperationException("Usuario no encontrado.");
            user.UserName = Input.UserName.Trim();
            user.NormalizedUserName = Input.UserName.Trim().ToUpperInvariant();
            user.LockoutEnabled = true;
            user.LockoutEnd = Input.IsActive ? null : DateTimeOffset.MaxValue;

            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return Page();
            }

            if (!string.IsNullOrWhiteSpace(Input.Password))
            {
                var removeResult = await userManager.RemovePasswordAsync(user);
                if (!removeResult.Succeeded)
                {
                    foreach (var error in removeResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    return Page();
                }

                var addPasswordResult = await userManager.AddPasswordAsync(user, Input.Password);
                if (!addPasswordResult.Succeeded)
                {
                    foreach (var error in addPasswordResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    return Page();
                }
            }
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            await userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        await userManager.AddToRoleAsync(user, Input.RoleName);

        await audit.LogAsync("UsuarioEmpresa", 0, isNew ? "Creado" : "Actualizado",
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            User.Identity?.Name ?? string.Empty,
            new
            {
                Empresa = Company.Name,
                Usuario = user.UserName,
                Rol = Input.RoleName,
                Activo = Input.IsActive
            },
            companyId);

        TempData["StatusMessage"] = "Usuario guardado correctamente.";
        return RedirectToPage("Index", new { companyId });
    }

    private async Task LoadCompanyAsync(int companyId)
    {
        Company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == companyId) ?? throw new InvalidOperationException("Empresa no encontrada.");
    }

    private async Task LoadRolesAsync()
    {
        var allowedRoles = new[] { "AdminEmpresa", "Gerencia", "Gestor", "Monitoreo" };
        RoleOptions = await roleManager.Roles
            .Where(x => allowedRoles.Contains(x.Name!))
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name!, x.Name!))
            .ToListAsync();
    }
}
