using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OldSchool.AuthService.Services;

public sealed class JwtTokenService(IConfiguration configuration)
{
    public string CreateToken(string username, IEnumerable<string> roles, int? companyId = null, string? userId = null, bool rememberSession = false)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "OldSchool.AuthService";
        var audience = configuration["Jwt:Audience"] ?? "OldSchool.Microservices";
        var key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("No se configuró Jwt:Key.");
        var expiryMinutes = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var minutes) ? minutes : 120;

        if (rememberSession)
            expiryMinutes = Math.Clamp(int.TryParse(configuration["Jwt:MobileExpiryMinutes"], out var mobileMinutes) ? mobileMinutes : 10080, 60, 10080);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, username),
            new(ClaimTypes.Name, username)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.Add(new Claim(JwtRegisteredClaimNames.UniqueName, username));
        if (companyId.HasValue)
        {
            claims.Add(new Claim("company_id", companyId.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            claims.Add(new Claim("user_id", userId));
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}