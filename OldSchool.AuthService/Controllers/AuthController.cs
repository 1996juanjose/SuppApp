using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OldSchool.AuthService.Data;
using OldSchool.AuthService.Models;
using OldSchool.AuthService.Services;

namespace OldSchool.AuthService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(JwtTokenService jwtTokenService, AuthDbContext db) : ControllerBase
{
    public sealed class CompanyLookupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int? CompanyId { get; set; }
    }

    [HttpGet("companies")]
    public async Task<IActionResult> GetCompanies(CancellationToken cancellationToken)
    {
        var companies = new List<CompanyLookupDto>();

        try
        {
            await using var connection = CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "select \"Id\", \"Name\" from \"Companies\" where coalesce(\"IsActive\", true) = true order by \"Name\"";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                companies.Add(new CompanyLookupDto
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
        }
        catch
        {
            return Ok(companies);
        }

        return Ok(companies);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedUserName = request.Username.Trim();
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedUserName == normalizedUserName.ToUpperInvariant(), cancellationToken);

        if (user is null)
        {
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        if (user is null || user.PasswordHash is null)
        {
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        var verifier = new PasswordHasher<AppUser>();
        var appUser = new AppUser
        {
            Id = user.Id,
            UserName = user.UserName,
            NormalizedUserName = user.NormalizedUserName,
            PasswordHash = user.PasswordHash
        };

        var result = verifier.VerifyHashedPassword(appUser, user.PasswordHash, request.Password);

        if (result is not PasswordVerificationResult.Success and not PasswordVerificationResult.SuccessRehashNeeded)
        {
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        var selectedCompanyId = request.CompanyId;
        if (selectedCompanyId.HasValue)
        {
            var selectedCompany = await LoadCompanyAsync(selectedCompanyId.Value, cancellationToken);
            if (selectedCompany is null)
            {
                return BadRequest(new { message = "La empresa seleccionada no existe o está inactiva." });
            }
        }

        var roles = await db.Database.SqlQuery<string>($"""
            select coalesce(r."Name", 'Admin')
            from "AspNetUserRoles" ur
            inner join "AspNetRoles" r on r."Id" = ur."RoleId"
            where ur."UserId" = {user.Id}
            """).ToListAsync(cancellationToken);

        if (roles.Count == 0)
        {
            roles = new List<string> { "Admin" };
        }

        int? companyId = null;
        string? companyName = null;

        if (selectedCompanyId.HasValue)
        {
            var selectedCompany = await LoadCompanyAsync(selectedCompanyId.Value, cancellationToken);
            companyId = selectedCompany?.Id;
            companyName = selectedCompany?.Name;
        }
        else
        {
            await using var connection = CreateConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "select \"CompanyId\", \"CompanyName\" from \"AspNetUsers\" where \"Id\" = @userId limit 1";
                command.Parameters.AddWithValue("userId", user.Id);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    if (!await reader.IsDBNullAsync(0, cancellationToken))
                    {
                        companyId = reader.GetInt32(0);
                    }

                    if (!await reader.IsDBNullAsync(1, cancellationToken))
                    {
                        companyName = reader.GetString(1);
                    }
                }
            }
            catch
            {
                // Si el esquema todavía no tiene estas columnas, el login sigue funcionando.
            }
        }

        var token = jwtTokenService.CreateToken(user.UserName ?? normalizedUserName, roles);
        return Ok(new
        {
            token,
            tokenType = "Bearer",
            username = user.UserName ?? normalizedUserName,
            roles,
            companyId,
            companyName
        });
    }

    private async Task<CompanyLookupDto?> LoadCompanyAsync(int companyId, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "select \"Id\", \"Name\" from \"Companies\" where \"Id\" = @companyId and coalesce(\"IsActive\", true) = true limit 1";
            command.Parameters.AddWithValue("companyId", companyId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new CompanyLookupDto
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            };
        }
        catch
        {
            return null;
        }
    }

    private NpgsqlConnection CreateConnection()
    {
        var connectionString = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("No se configuró la cadena de conexión de AuthService.");
        }

        return new NpgsqlConnection(connectionString);
    }
}