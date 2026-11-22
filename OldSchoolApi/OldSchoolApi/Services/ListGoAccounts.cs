using Google.Apis.Auth;
using System.Security.Cryptography;
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
        accounts.MapPost("/google", SignInWithGoogleAsync);
        app.MapListGoLists();
    }

    private static async Task<IResult> SignInWithGoogleAsync(
        GoogleCredentials input, ApiDbContext db, IConfiguration config, CancellationToken ct)
    {
        var clientId = config["Authentication:Google:ClientId"];
        if (!config.GetValue<bool>("Authentication:Google:Enabled") || string.IsNullOrWhiteSpace(clientId))
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        if (string.IsNullOrWhiteSpace(input.IdToken) || input.IdToken.Length > 8192)
            return Results.BadRequest();

        GoogleJsonWebSignature.Payload google;
        try
        {
            google = await GoogleJsonWebSignature.ValidateAsync(input.IdToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
        }
        catch (InvalidJwtException)
        {
            return Results.Unauthorized();
        }
        if (string.IsNullOrWhiteSpace(google.Subject) || !google.EmailVerified)
            return Results.Unauthorized();

        var id = Guid.NewGuid();
        var stored = await db.Database.SqlQuery<Guid>($"""
            INSERT INTO listgo_google_accounts (id, google_subject)
            VALUES ({id}, {google.Subject})
            ON CONFLICT (google_subject) DO UPDATE SET google_subject = EXCLUDED.google_subject
            RETURNING id AS "Value"
            """).ToListAsync(ct);
        return await CreateSessionAsync(stored.Single(), db, ct);
    }

    public sealed record GoogleCredentials(string? IdToken);
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
            return inserted == 1 ? await CreateSessionAsync(id, db, ct) : Results.Conflict();
        }

        var account = await db.Database.SqlQuery<Account>(
            $"SELECT id AS \"Id\", password_hash AS \"PasswordHash\" FROM listgo_accounts WHERE email = {email}")
            .SingleOrDefaultAsync(ct);
        if (account is null || Hasher.VerifyHashedPassword(email, account.PasswordHash, password) == PasswordVerificationResult.Failed)
            return Results.Unauthorized();
        return await CreateSessionAsync(account.Id, db, ct);
    }

    private static async Task<IResult> CreateSessionAsync(Guid accountId, ApiDbContext db, CancellationToken ct)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
        var expires = DateTime.UtcNow.AddDays(7);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO listgo_sessions (token_hash, account_id, expires_at) VALUES ({hash}, {accountId}, {expires})", ct);
        return Results.Ok(new { accountId, accessToken = token });
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
            );
            CREATE TABLE IF NOT EXISTS listgo_google_accounts (
                id uuid PRIMARY KEY,
                google_subject text NOT NULL UNIQUE
            );
            CREATE TABLE IF NOT EXISTS listgo_sessions (
                token_hash varchar(64) PRIMARY KEY,
                account_id uuid NOT NULL,
                expires_at timestamptz NOT NULL
            );
            CREATE TABLE IF NOT EXISTS listgo_list_stores (
                account_id uuid PRIMARY KEY,
                revision integer NOT NULL,
                payload jsonb NOT NULL,
                updated_at timestamptz NOT NULL DEFAULT now()
            )
            """);
    }

    public sealed record Credentials(string? Email, string? Password);
    public sealed record Account(Guid Id, string PasswordHash);
}
