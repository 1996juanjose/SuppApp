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
using Microsoft.AspNetCore.Http;

namespace OldSchoolLab.Pages.Account;

[AllowAnonymous]
public class LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ApplicationDbContext db, ChatApiClient chatApiClient, IHttpContextAccessor httpContextAccessor) : PageModel
{
    private const string GlobalAdminValue = "__global__";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }
    public List<SelectListItem> CompanyOptions { get; private set; } = new();

    public class InputModel
    {
        [Required]
        [Display(Name = "Empresa")]
        public string CompanyKey { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Usuario")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Contrasena")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Recordarme")]
        public bool RememberMe { get; set; }
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        ReturnUrl = returnUrl;
        LoadCompanies();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        LoadCompanies();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        ApplicationUser? user;
        List<Claim> additionalClaims;

        if (Input.CompanyKey == GlobalAdminValue)
        {
            user = await userManager.FindByNameAsync(Input.UserName.Trim());
            if (user is null || !await userManager.IsInRoleAsync(user, "SuperAdmin"))
            {
                ModelState.AddModelError(string.Empty, "Usuario, empresa o contrasena invalidos.");
                return Page();
            }

            additionalClaims =
            [
                new Claim(ClaimTypesHelper.IsGlobalAdmin, bool.TrueString),
                new Claim(ClaimTypesHelper.CompanyName, "Administrador global")
            ];
        }
        else if (int.TryParse(Input.CompanyKey, out var companyId))
        {
            user = await db.Users
                .Include(x => x.Company)
                .FirstOrDefaultAsync(x => x.UserName == Input.UserName.Trim() && x.CompanyId == companyId);

            if (user is null || user.Company is null || !user.Company.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Usuario, empresa o contrasena invalidos.");
                return Page();
            }

            additionalClaims =
            [
                new Claim(ClaimTypesHelper.CompanyId, companyId.ToString()),
                new Claim(ClaimTypesHelper.CompanyName, user.Company.Name),
                new Claim(ClaimTypesHelper.CompanyLogoPath, user.Company.LogoPath ?? string.Empty),
                new Claim(ClaimTypesHelper.IsGlobalAdmin, bool.FalseString)
            ];
        }
        else
        {
            ModelState.AddModelError(nameof(Input.CompanyKey), "Selecciona una empresa valida.");
            return Page();
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, Input.Password, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            try
            {
                var token = await chatApiClient.LoginAsync(Input.UserName.Trim(), Input.Password);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    httpContextAccessor.HttpContext?.Session.SetString("ChatApiJwt", token);
                }
            }
            catch
            {
                httpContextAccessor.HttpContext?.Session.Remove("ChatApiJwt");
            }

            await signInManager.SignOutAsync();
            await signInManager.SignInWithClaimsAsync(user, Input.RememberMe, additionalClaims);
            return LocalRedirect(returnUrl ?? Url.Page("/Index")!);
        }

        ModelState.AddModelError(string.Empty, "Usuario, empresa o contrasena invalidos.");
        return Page();
    }

    private void LoadCompanies()
    {
        CompanyOptions = db.Companies
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToList();

        CompanyOptions.Insert(0, new SelectListItem("Administrador global", GlobalAdminValue));
    }
}
