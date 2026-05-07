using System.Security.Claims;

namespace OldSchoolLab.Services;

public static class ClaimsPrincipalCompanyExtensions
{
    public static int? GetCompanyId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypesHelper.CompanyId);
        return int.TryParse(value, out var companyId) ? companyId : null;
    }

    public static bool IsGlobalAdmin(this ClaimsPrincipal user)
    {
        return string.Equals(user.FindFirstValue(ClaimTypesHelper.IsGlobalAdmin), bool.TrueString, StringComparison.OrdinalIgnoreCase)
            || user.IsInRole("SuperAdmin");
    }

    public static string GetCompanyName(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypesHelper.CompanyName) ?? string.Empty;
    }

    public static string GetCompanyLogoPath(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypesHelper.CompanyLogoPath) ?? string.Empty;
    }
}
