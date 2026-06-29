using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OldSchoolApi.Data;
using OldSchoolApi.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OldSchoolApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(ApiDbContext db, IConfiguration config) : ControllerBase
{
    public class LoginRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int? CompanyId { get; set; }
    }

    public class CompanyLookupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies()
    {
        var companies = await db.Set<CompanyLookupDto>()
            .FromSqlRaw("select \"Id\", \"Name\" from \"Companies\" where coalesce(\"IsActive\", true) = true order by \"Name\"")
            .ToListAsync();

        return Ok(companies);
    }

    /// <summary>
    /// Obtiene un JWT token. Usa las mismas credenciales del sistema web.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Usuario y contraseña son obligatorios." });

        var normalizedName = request.UserName.ToUpperInvariant();

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedUserName == normalizedName);

        if (user is null)
            return Unauthorized(new { error = "Credenciales inválidas." });

        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<object>();
        var result = hasher.VerifyHashedPassword(new object(), user.PasswordHash ?? string.Empty, request.Password);

        if (result == Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "Credenciales inválidas." });

        var roles = await (
            from ur in db.UserRoles
            join r in db.Roles on ur.RoleId equals r.Id
            where ur.UserId == user.Id
            select r.Name
        ).ToListAsync();

        var selectedCompany = request.CompanyId.HasValue
            ? await db.Set<CompanyLookupDto>()
                .FromSqlRaw("select \"Id\", \"Name\" from \"Companies\" where \"Id\" = {0} and coalesce(\"IsActive\", true) = true", request.CompanyId.Value)
                .FirstOrDefaultAsync()
            : null;

        var token = GenerateJwt(user.Id, user.UserName ?? string.Empty, roles);

        return Ok(new
        {
            token,
            userName = user.UserName,
            roles,
            companyId = selectedCompany?.Id,
            companyName = selectedCompany?.Name,
            expiresIn = $"{config["Jwt:ExpiresInHours"]}h"
        });
    }

    private string GenerateJwt(string userId, string userName, List<string?> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKeyProvider.GetKey(config)));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.UniqueName, userName),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles.Where(r => r is not null))
        {
            claims.Add(new Claim(ClaimTypes.Role, role!));
        }

        var hours = int.TryParse(config["Jwt:ExpiresInHours"], out var h) ? h : 24;

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(hours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
