namespace OldSchool.AuthService.Models;

public class AppUser
{
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? NormalizedUserName { get; set; }
    public string? PasswordHash { get; set; }
}