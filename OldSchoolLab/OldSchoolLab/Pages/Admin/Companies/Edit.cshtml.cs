using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using OldSchoolLab.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace OldSchoolLab.Pages.Admin.Companies;

[Authorize(Roles = "SuperAdmin")]
public class EditModel(ApplicationDbContext db, ICompanyLogoStorage companyLogoStorage, IAuditService audit) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public IFormFile? LogoFile { get; set; }

    public class InputModel
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(120)]
        [Display(Name = "Empresa")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Activa")]
        public bool IsActive { get; set; } = true;

        public string LogoPath { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (!id.HasValue)
        {
            return Page();
        }

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id.Value);
        if (company is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = company.Id,
            Name = company.Name,
            IsActive = company.IsActive,
            LogoPath = company.LogoPath
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        StoredCompanyLogo? storedLogo;
        try
        {
            storedLogo = await companyLogoStorage.SaveAsync(LogoFile, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(LogoFile), ex.Message);
            return Page();
        }

        Company company;
        var isNew = Input.Id == 0;
        if (isNew)
        {
            company = new Company();
            db.Companies.Add(company);
        }
        else
        {
            company = await db.Companies.FirstOrDefaultAsync(x => x.Id == Input.Id) ?? throw new InvalidOperationException("Empresa no encontrada.");
        }

        company.Name = Input.Name.Trim();
        company.IsActive = Input.IsActive;
        if (storedLogo is not null)
        {
            company.LogoPath = storedLogo.PublicPath;
            company.LogoFileName = storedLogo.OriginalFileName;
        }

        await db.SaveChangesAsync();

        if (isNew)
        {
            await EnsureDefaultsAsync(company.Id);
        }

        await audit.LogAsync("Empresa", company.Id, isNew ? "Creada" : "Actualizada",
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            User.Identity?.Name ?? string.Empty,
            new { company.Name, company.IsActive, TieneLogo = !string.IsNullOrWhiteSpace(company.LogoPath) },
            company.Id);

        TempData["StatusMessage"] = "Empresa guardada correctamente.";
        return RedirectToPage("Index");
    }

    private async Task EnsureDefaultsAsync(int companyId)
    {
        var defaultStatuses = new[]
        {
            new { Name = "Clientes", BadgeClass = "success", SortOrder = 1 },
            new { Name = "Rechazo", BadgeClass = "danger", SortOrder = 2 },
            new { Name = "Interesado", BadgeClass = "info", SortOrder = 3 },
            new { Name = "Por Pagar", BadgeClass = "warning", SortOrder = 4 },
            new { Name = "Prospecto", BadgeClass = "primary", SortOrder = 5 }
        };

        foreach (var item in defaultStatuses)
        {
            db.Statuses.Add(new StatusCatalog
            {
                CompanyId = companyId,
                Name = item.Name,
                BadgeClass = item.BadgeClass,
                SortOrder = item.SortOrder,
                IsActive = true
            });
        }

        db.Products.Add(new Product
        {
            CompanyId = companyId,
            Name = "Creatina",
            IsActive = true,
            Prices = new List<ProductPrice>
            {
                new() { Quantity = 1, Price = 89m },
                new() { Quantity = 2, Price = 149m },
                new() { Quantity = 3, Price = 189m }
            }
        });

        await db.SaveChangesAsync();
    }
}
