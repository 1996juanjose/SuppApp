using System.ComponentModel.DataAnnotations;

namespace OldSchoolApi.Models;

public class AppUser
{
    public string Id { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? UserName { get; set; }

    [MaxLength(256)]
    public string? NormalizedUserName { get; set; }

    public string? PasswordHash { get; set; }
}

public class AppRole
{
    public string Id { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Name { get; set; }

    [MaxLength(256)]
    public string? NormalizedName { get; set; }
}

public class AppUserRole
{
    public string UserId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
}
