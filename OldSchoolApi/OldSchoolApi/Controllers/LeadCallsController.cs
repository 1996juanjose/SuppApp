using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OldSchoolApi.Data;
using OldSchoolApi.Models;
using OldSchoolApi.Services;

namespace OldSchoolApi.Controllers;

[ApiController, Authorize, Route("api/records")]
public class LeadCallsController(ApiDbContext db, IConfiguration config) : ControllerBase
{
    private int? CompanyId => int.TryParse(User.FindFirstValue("company_id"), out var id) ? id : null;
    private string ActorId => User.FindFirstValue("user_id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "";
    public static readonly string[] Outcomes = ["Contestó", "No contestó", "Competencia", "No interesado"];

    [HttpGet("prospect-calls/queue")]
    public async Task<IActionResult> Queue(CancellationToken cancellationToken, [FromQuery] string callFilter = "all",
        [FromQuery] string? search = null, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null,
        [FromQuery] int? minAttempts = null, [FromQuery] int? maxAttempts = null, [FromQuery] int afterId = 0, [FromQuery] int take = 100)
    {
        if (!CompanyId.HasValue) return Forbid();
        if (callFilter is not ("all" or "uncalled" or "called" or "callback") || take is < 1 or > 500 || afterId < 0
            || minAttempts < 0 || maxAttempts < 0 || maxAttempts < minAttempts || fromDate > toDate)
            return BadRequest(new { error = "Filtros de llamadas invalidos." });
        var now = AppClock.Now(config);
        var query = db.CustomerRecords.AsNoTracking().Where(x => x.CompanyId == CompanyId && x.StatusCatalog.Name == "Prospecto");
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Cellphone.Contains(term) || x.NameOrReference.Contains(term));
        }
        if (fromDate.HasValue) query = query.Where(x => x.CreatedAt >= fromDate.Value.Date);
        if (toDate.HasValue) { var end = toDate.Value.Date.AddDays(1); query = query.Where(x => x.CreatedAt < end); }
        var leads = query.Select(x => new {
            x.Id, x.Cellphone, x.NameOrReference, x.CreatedAt, x.CallActivity, x.CallScheduledAt, x.IsCallConcrete,
            NoAnswerCount = db.LeadCallAttempts.Count(a => a.RecordId == x.Id && a.CompanyId == CompanyId && a.Outcome == "No contestó"),
            CallCount = db.LeadCallAttempts.Count(a => a.RecordId == x.Id && a.CompanyId == CompanyId),
            LastCallAt = db.LeadCallAttempts.Where(a => a.RecordId == x.Id && a.CompanyId == CompanyId).Max(a => (DateTime?)a.CreatedAt),
            LastOutcome = db.LeadCallAttempts.Where(a => a.RecordId == x.Id && a.CompanyId == CompanyId).OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).Select(a => a.Outcome).FirstOrDefault()
        });
        if (callFilter == "uncalled") leads = leads.Where(x => x.CallCount == 0);
        if (callFilter == "called") leads = leads.Where(x => x.CallCount > 0);
        if (callFilter == "callback") leads = leads.Where(x => !x.IsCallConcrete && x.CallScheduledAt.HasValue && x.CallScheduledAt <= now);
        if (minAttempts.HasValue) leads = leads.Where(x => x.CallCount >= minAttempts.Value);
        if (maxAttempts.HasValue) leads = leads.Where(x => x.CallCount <= maxAttempts.Value);
        var matchingPhones = await leads.Select(x => new { x.Id, x.Cellphone }).ToListAsync(cancellationToken);
        var validIds = matchingPhones.Where(x => ValidPhone(x.Cellphone)).Select(x => x.Id).ToArray();
        Response.Headers["X-Total-Count"] = validIds.Length.ToString();
        var page = await leads.Where(x => validIds.Contains(x.Id)).Where(x => x.Id > afterId).OrderBy(x => x.Id).Take(take).ToListAsync(cancellationToken);
        Response.Headers["X-Next-Cursor"] = page.Count == 0 ? afterId.ToString() : page[^1].Id.ToString();
        return Ok(page.Where(x => ValidPhone(x.Cellphone)).Select(x => new { x.Id,
            cellphone = NormalizePhone(x.Cellphone), x.NameOrReference, x.CreatedAt, x.CallActivity,
            x.CallCount, x.NoAnswerCount, x.LastCallAt, x.LastOutcome, nextCallAt = x.IsCallConcrete ? null : x.CallScheduledAt,
            callLabel = x.CallCount == 0 ? "Sin llamar" : $"Se llamo {x.CallCount} {(x.CallCount == 1 ? "vez" : "veces")}" }));
    }

    public sealed class ResultRequest
    {
        public Guid? RequestId { get; set; }
        [Required, MaxLength(40)] public string Outcome { get; set; } = "";
        [MaxLength(250)] public string? Notes { get; set; }
        public DateTime? NextCallAt { get; set; }
    }

    [HttpPost("{id:int}/prospect-call-result")]
    public async Task<IActionResult> Save(int id, ResultRequest request, CancellationToken cancellationToken)
    {
        if (!CompanyId.HasValue) return Forbid();
        var outcome = request.Outcome?.Trim() ?? "";
        if (!Outcomes.Contains(outcome) || (request.Notes?.Length ?? 0) > 250 || request.RequestId == Guid.Empty)
            return BadRequest(new { error = "Selecciona un resultado valido y notas de hasta 250 caracteres." });
        var record = await db.CustomerRecords.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == CompanyId, cancellationToken);
        if (record is null) return NotFound();
        var requestId = request.RequestId ?? Guid.NewGuid();
        var existing = await db.LeadCallAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
        if (existing is not null)
        {
            if (existing.RecordId != id || existing.CompanyId != CompanyId || existing.CalledByUserId != ActorId)
                return Conflict(new { error = "Este identificador ya pertenece a otro intento." });
            return await SavedAsync(record, existing, cancellationToken);
        }
        if (!await db.Statuses.AnyAsync(x => x.Id == record.StatusCatalogId && x.Name == "Prospecto", cancellationToken))
            return Conflict(new { error = "El registro ya no es prospecto. Recarga la cola." });
        var now = AppClock.Now(config);
        var next = outcome == "Volver a llamar" ? request.NextCallAt ?? now.AddDays(1) : (DateTime?)null;
        if (next.HasValue && (next.Value.Kind != DateTimeKind.Unspecified || next <= now || next > now.AddYears(1)))
            return BadRequest(new { error = "La proxima llamada debe ser futura, en hora local de Peru y sin zona horaria." });
        var attempt = new LeadCallAttempt { Id = requestId, CompanyId = CompanyId.Value, RecordId = id,
            CreatedAt = now, Outcome = outcome, Notes = request.Notes?.Trim() ?? "", NextCallAt = next,
            CalledByUserId = ActorId, CalledByUserName = User.Identity?.Name ?? "" };
        db.LeadCallAttempts.Add(attempt);
        record.CallScheduledAt = next;
        record.IsCallConcrete = !next.HasValue;
        var noAnswerCount = await db.LeadCallAttempts.CountAsync(x => x.RecordId == id && x.CompanyId == CompanyId && x.Outcome == "No contestó", cancellationToken);
        var activityOutcome = outcome == "No contestó" ? $"No contestó ({noAnswerCount + 1} {(noAnswerCount == 0 ? "vez" : "veces")})" : outcome;
        var line = $"{activityOutcome}{(attempt.Notes.Length > 0 ? ": " + attempt.Notes : "")}";
        var priorActivity = System.Text.RegularExpressions.Regex.Replace(record.CallActivity ?? "", @"(?m)^\d{4}-\d{2}-\d{2} \d{2}:\d{2} - ", "");
        record.CallActivity = $"{priorActivity}\n{line}".Trim();
        if (record.CallActivity.Length > 300) record.CallActivity = record.CallActivity[^300..];
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            db.ChangeTracker.Clear();
            var duplicate = await db.LeadCallAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
            if (duplicate is null || duplicate.RecordId != id || duplicate.CompanyId != CompanyId || duplicate.CalledByUserId != ActorId) throw;
            attempt = duplicate;
        }
        return await SavedAsync(record, attempt, cancellationToken);
    }

    [HttpGet("{id:int}/prospect-calls")]
    public async Task<IActionResult> History(int id, CancellationToken cancellationToken)
    {
        if (!CompanyId.HasValue) return Forbid();
        if (!await db.CustomerRecords.AnyAsync(x => x.Id == id && x.CompanyId == CompanyId, cancellationToken)) return NotFound();
        return Ok(await db.LeadCallAttempts.AsNoTracking().Where(x => x.RecordId == id && x.CompanyId == CompanyId)
            .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken));
    }

    private async Task<IActionResult> SavedAsync(CustomerRecord record, LeadCallAttempt attempt, CancellationToken ct)
    {
        var count = await db.LeadCallAttempts.CountAsync(x => x.RecordId == record.Id && x.CompanyId == CompanyId, ct);
        var noAnswerCount = await db.LeadCallAttempts.CountAsync(x => x.RecordId == record.Id && x.CompanyId == CompanyId && x.Outcome == "No contestó", ct);
        return Ok(new { noAnswerCount, id = record.Id, attemptId = attempt.Id, callCount = count, attempt.Outcome, attempt.NextCallAt,
            callLabel = $"Se llamo {count} {(count == 1 ? "vez" : "veces")}" });
    }
    private static string NormalizePhone(string phone) => (phone.TrimStart().StartsWith('+') ? "+" : "") + new string(phone.Where(char.IsDigit).ToArray());
    private static bool ValidPhone(string phone) => phone.Where(char.IsDigit).Count() is >= 9 and <= 15;
}
