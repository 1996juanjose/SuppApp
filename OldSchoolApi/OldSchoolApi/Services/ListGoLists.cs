using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OldSchoolApi.Data;

namespace OldSchoolApi.Services;

public static class ListGoLists
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapListGoLists(this WebApplication app)
    {
        var routes = app.MapGroup("/api/listgo/lists");
        routes.MapGet("/", ReadAsync);
        routes.MapPut("/", WriteAsync);
    }

    private static async Task<Guid?> AccountAsync(HttpRequest request, ApiDbContext db, CancellationToken ct)
    {
        var auth = request.Headers.Authorization.ToString();
        if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;
        var token = auth[7..];
        if (token.Length != 64 || !token.All(char.IsAsciiHexDigit))
            return null;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var now = DateTime.UtcNow;
        return await db.Database.SqlQuery<Guid>(
            $"SELECT account_id AS \"Value\" FROM listgo_sessions WHERE token_hash = {hash} AND expires_at > {now}")
            .Select(id => (Guid?)id).SingleOrDefaultAsync(ct);
    }

    private static async Task<IResult> ReadAsync(HttpRequest request, ApiDbContext db, CancellationToken ct)
    {
        var account = await AccountAsync(request, db, ct);
        if (account is null) return Results.Unauthorized();
        var stored = await db.Database.SqlQuery<Stored>(
            $"SELECT revision AS \"Revision\", payload::text AS \"Payload\" FROM listgo_list_stores WHERE account_id = {account.Value}")
            .SingleOrDefaultAsync(ct);
        return Results.Ok(new
        {
            revision = stored?.Revision ?? 0,
            lists = stored is null ? [] : JsonSerializer.Deserialize<SavedList[]>(stored.Payload, Json) ?? []
        });
    }

    private static async Task<IResult> WriteAsync(SaveRequest input, HttpRequest request, ApiDbContext db, CancellationToken ct)
    {
        var account = await AccountAsync(request, db, ct);
        if (account is null) return Results.Unauthorized();
        if (input.Revision < 0 || input.Revision == int.MaxValue || input.Lists is null || input.Lists.Length > 500)
            return Results.BadRequest();
        var total = 0;
        foreach (var list in input.Lists)
        {
            if (list is null || string.IsNullOrWhiteSpace(list.Name) || list.Name.Length > 255 || list.Contacts is null)
                return Results.BadRequest();
            total += list.Contacts.Length;
            if (total > 50000) return Results.BadRequest();
            foreach (var c in list.Contacts)
                if (c is null || string.IsNullOrEmpty(c.Phone) || c.Phone.Length > 32 || c.Name is null || c.Name.Length > 500
                    || c.Notes?.Length > 2000
                    || c.Result is not (null or "answered" or "no_answer" or "competitor" or "not_interested" or "skipped")
                    || c.Status is not ("prospect" or "interested" or "pending_payment" or "customer" or "rejected"))
                    return Results.BadRequest();
        }
        var payload = JsonSerializer.Serialize(input.Lists, Json);
        if (Encoding.UTF8.GetByteCount(payload) > 5_000_000) return Results.StatusCode(413);
        int changed;
        if (input.Revision == 0)
        {
            changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO listgo_list_stores (account_id, revision, payload)
                VALUES ({account.Value}, 1, CAST({payload} AS jsonb))
                ON CONFLICT (account_id) DO NOTHING
                """, ct);
        }
        else
        {
            changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE listgo_list_stores SET revision = revision + 1,
                    payload = CAST({payload} AS jsonb), updated_at = now()
                WHERE account_id = {account.Value} AND revision = {input.Revision}
                """, ct);
        }
        return changed == 1 ? Results.Ok(new { revision = input.Revision + 1 }) : Results.Conflict();
    }

    public sealed record Stored(int Revision, string Payload);
    public sealed record SaveRequest(int Revision, SavedList[]? Lists);
    public sealed record SavedList(string Name, SavedContact[] Contacts);
    public sealed record SavedContact(string Phone, string Name, string? Result = null, string? Notes = null, string Status = "prospect");
}
