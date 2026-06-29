using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OldSchool.AuthService.Services;

public sealed class JwtTokenService(IConfiguration configuration)
{
    public string CreateToken(string username, IEnumerable<string> roles)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "OldSchool.AuthService";
        var audience = configuration["Jwt:Audience"] ?? "OldSchool.Microservices";
        var key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("No se configuró Jwt:Key.");
        var expiryMinutes = int.TryParse(configuration["Jwt:ExpiryMinutes"], out var minutes) ? minutes : 120;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, username),
            new(ClaimTypes.Name, username)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.Add(new Claim(JwtRegisteredClaimNames.UniqueName, username));

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