using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OldSchoolApi.Data;

namespace OldSchoolApi.Services;

public static class ListGoAccounts
{
    private static readonly PasswordHasher<string> Hasher = new();

    public static void MapListGoAccounts(this WebApplication app)
    {
        var accounts = app.MapGroup("/api/listgo/accounts").RequireRateLimiting("listgo-accounts");
        accounts.MapPost("/register", (Credentials input, ApiDbContext db, CancellationToken ct) =>
            Handle(input, db, true, ct));
        accounts.MapPost("/login", (Credentials input, ApiDbContext db, CancellationToken ct) =>
            Handle(input, db, false, ct));
    }

    private static async Task<IResult> Handle(Credentials input, ApiDbContext db, bool register, CancellationToken ct)
    {
        var email = input.Email?.Trim().ToLowerInvariant();
        var password = input.Password;
        if (email is null || email.Length > 254 || !System.Net.Mail.MailAddress.TryCreate(email, out var address)
            || address.Address != email || password is null || password.Length > 128
            || password.Length < (register ? 10 : 1))
            return Results.BadRequest();

        if (register)
        {
            var id = Guid.NewGuid();
            var hash = Hasher.HashPassword(email, password);
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO listgo_accounts (id, email, password_hash) VALUES ({id}, {email}, {hash}) ON CONFLICT (email) DO NOTHING", ct);
            return inserted == 1 ? Results.Ok(new { accountId = id }) : Results.Conflict();
        }

        var account = await db.Database.SqlQuery<Account>(
            $"SELECT id AS \"Id\", password_hash AS \"PasswordHash\" FROM listgo_accounts WHERE email = {email}")
            .SingleOrDefaultAsync(ct);
        if (account is null || Hasher.VerifyHashedPassword(email, account.PasswordHash, password) == PasswordVerificationResult.Failed)
            return Results.Unauthorized();
        return Results.Ok(new { accountId = account.Id });
    }

    public static async Task EnsureAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS listgo_accounts (
                id uuid PRIMARY KEY,
                email varchar(254) NOT NULL UNIQUE,
                password_hash text NOT NULL
            )
            """);
    }

    public sealed record Credentials(string? Email, string? Password);
    public sealed record Account(Guid Id, string PasswordHash);
}
