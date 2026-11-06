using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;

namespace OldSchoolLab.Services;

public class WhatsAppCampaignService(ApplicationDbContext db, IConfiguration config)
{
    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById(config["App:TimeZoneId"] ?? "America/Lima"));
    public static string Phone(string? raw)
    {
        var value = new string((raw ?? "").Where(c => c is >= '0' and <= '9').ToArray());
        if (value.Length == 9 && value.StartsWith('9')) value = "51" + value;
        return value;
    }
    public static bool ValidPhone(string phone) => phone.Length is >= 10 and <= 15 && !phone.StartsWith('0');
    public static bool CanUse(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        && user.GetCompanyId().HasValue && (user.IsInRole("Gerencia") || user.IsInRole("Gestor") || user.IsInRole("Vendedor"));
    private static string Actor(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Inicia sesión de nuevo.");
    private static int Company(ClaimsPrincipal user) => CanUse(user) ? user.GetCompanyId()!.Value : throw new UnauthorizedAccessException();
    // Historical contacts are selectable by default. Preserve only explicit opt-outs.
    public static bool Allowed(WhatsAppContact? contact, DateTime now) => contact?.ConsentStatus != "DoNotContact";

    private Task<T> WriteAsync<T>(int companyId, Func<Task<T>> action) => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        try
        {
            if (db.Database.IsNpgsql())
            {
                var key = $"whatsapp-assisted:{companyId}";
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))");
            }
            var result = await action();
            await db.SaveChangesAsync();
            if (tx is not null) await tx.CommitAsync();
            return result;
        }
        catch { db.ChangeTracker.Clear(); throw; }
    });

    public async Task<WhatsAppContact?> ConsentAsync(int companyId, string phone) =>
        await db.WhatsAppContacts.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == companyId && x.Phone == Phone(phone));

    public Task<bool> SetConsentAsync(ClaimsPrincipal user, int recordId, string status, string? evidence, DateTime? expires) =>
        WriteAsync(Company(user), async () =>
        {
            if (status is not ("Unknown" or "Authorized" or "DoNotContact")) throw new InvalidOperationException("Estado comercial inválido.");
            if ((evidence?.Length ?? 0) > 500 || (status == "Authorized" && string.IsNullOrWhiteSpace(evidence)))
                throw new InvalidOperationException("Indica la fecha y referencia de la autorización comercial (máximo 500 caracteres).");
            if (expires.HasValue && (expires.Value.Kind != DateTimeKind.Unspecified || expires <= Now))
                throw new InvalidOperationException("La vigencia debe ser una fecha futura en hora de Perú.");
            var companyId = Company(user);
            var record = await db.CustomerRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == recordId && x.CompanyId == companyId)
                ?? throw new InvalidOperationException("Registro no disponible.");
            var phone = Phone(record.Cellphone);
            if (!ValidPhone(phone)) throw new InvalidOperationException("El número no es válido. Corrígelo antes de registrar la autorización.");
            var contact = await db.WhatsAppContacts.SingleOrDefaultAsync(x => x.CompanyId == companyId && x.Phone == phone);
            if (contact is null) { contact = new() { CompanyId = companyId, Phone = phone }; db.WhatsAppContacts.Add(contact); }
            contact.ConsentStatus = status; contact.ConsentExpiresAt = status == "Authorized" ? expires : null;
            contact.Evidence = evidence?.Trim() ?? ""; contact.ChangedAt = Now;
            contact.ChangedByUserId = Actor(user); contact.ChangedByUserName = user.Identity!.Name ?? "";
            db.WhatsAppConsentChanges.Add(new() { CompanyId = companyId, RecordId = recordId, Phone = phone,
                Status = status, ExpiresAt = contact.ConsentExpiresAt, ChangedAt = Now, Evidence = contact.Evidence,
                ActorId = Actor(user), ActorName = contact.ChangedByUserName });
            return true;
        });

    public Task<bool> SaveMessageAsync(ClaimsPrincipal user, string message) => WriteAsync(Company(user), async () =>
    {
        ValidateMessage(message);
        var companyId = Company(user); var actor = Actor(user);
        var setting = await db.WhatsAppMessageSettings.SingleOrDefaultAsync(x => x.CompanyId == companyId && x.UserId == actor);
        if (setting is null) { setting = new() { CompanyId = companyId, UserId = actor }; db.WhatsAppMessageSettings.Add(setting); }
        setting.Message = message.Trim(); return true;
    });
    private static void ValidateMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 2000) throw new InvalidOperationException("Escribe un mensaje de 1 a 2000 caracteres.");
    }
    public sealed record Preview(int Selected, int Unique, int Duplicates, int Invalid, int Blocked, int PreviouslyContacted, int Eligible);
    private async Task<(List<CustomerRecord> Records, Preview Preview)> EligibleAsync(int companyId, int[] ids, bool excludePrevious)
    {
        if (ids is null || ids.Length is < 1 or > 1000) throw new InvalidOperationException("Selecciona entre 1 y 1000 registros.");
        var records = await db.CustomerRecords.AsNoTracking().Where(x => ids.Contains(x.Id) && x.CompanyId == companyId).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync();
        if (records.Count != ids.Distinct().Count()) throw new InvalidOperationException("Algún registro ya no existe o no pertenece a tu empresa. Actualiza el listado.");
        var selectionOrder = ids.Distinct().Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        var unique = records.OrderBy(x => selectionOrder[x.Id]).GroupBy(x => Phone(x.Cellphone)).Select(x => x.First()).ToList();
        var phones = unique.Select(x => Phone(x.Cellphone)).ToArray();
        var consents = await db.WhatsAppContacts.AsNoTracking().Where(x => x.CompanyId == companyId && phones.Contains(x.Phone)).ToDictionaryAsync(x => x.Phone);
        var previous = await db.WhatsAppCampaignRecipients.AsNoTracking().Where(x => x.CompanyId == companyId && phones.Contains(x.Phone) && x.Status == "Sent").Select(x => x.Phone).Distinct().ToListAsync();
        var invalid = unique.Count(x => !ValidPhone(Phone(x.Cellphone)));
        var noConsent = unique.Count(x => ValidPhone(Phone(x.Cellphone)) && !Allowed(consents.GetValueOrDefault(Phone(x.Cellphone)), Now));
        var eligible = unique.Where(x => ValidPhone(Phone(x.Cellphone)) && Allowed(consents.GetValueOrDefault(Phone(x.Cellphone)), Now)
            && (!excludePrevious || !previous.Contains(Phone(x.Cellphone)))).ToList();
        return (eligible, new(records.Count, unique.Count, records.Count - unique.Count, invalid, noConsent, previous.Count, eligible.Count));
    }
    public async Task<Preview> PreviewAsync(ClaimsPrincipal user, int[] ids, bool excludePrevious) => (await EligibleAsync(Company(user), ids, excludePrevious)).Preview;

    public Task<Guid> CreateAsync(ClaimsPrincipal user, Guid requestId, int[] ids, string message, bool excludePrevious) => WriteAsync(Company(user), async () =>
    {
        if (requestId == Guid.Empty) throw new InvalidOperationException("Identificador de campaña inválido.");
        ValidateMessage(message); var companyId = Company(user); var actor = Actor(user);
        var existing = await db.WhatsAppCampaigns.SingleOrDefaultAsync(x => x.Id == requestId);
        if (existing is not null)
        {
            if (existing.CompanyId != companyId || existing.UserId != actor) throw new UnauthorizedAccessException();
            return existing.Id;
        }
        if (await db.WhatsAppCampaigns.AnyAsync(x => x.CompanyId == companyId && x.UserId == actor && x.Status != "Completed"))
            throw new InvalidOperationException("Tienes una selección pendiente. Continúala o termina sus contactos antes de iniciar otra.");
        var eligible = await EligibleAsync(companyId, ids, excludePrevious);
        if (eligible.Records.Count == 0) throw new InvalidOperationException("No hay números válidos disponibles en la selección.");
        db.WhatsAppCampaigns.Add(new() { Id = requestId, CompanyId = companyId, UserId = actor, UserName = user.Identity!.Name ?? "", Message = message.Trim(), CreatedAt = Now });
        var position = 0;
        foreach (var r in eligible.Records)
            db.WhatsAppCampaignRecipients.Add(new() { CampaignId = requestId, CompanyId = companyId, RecordId = r.Id,
                Position = ++position, Phone = Phone(r.Cellphone), Name = r.NameOrReference, Message = message.Trim() });
        return requestId;
    });

    private async Task<WhatsAppCampaign> OwnedAsync(ClaimsPrincipal user, Guid campaignId)
    {
        var company = Company(user); var actor = Actor(user);
        return await db.WhatsAppCampaigns.SingleOrDefaultAsync(x => x.Id == campaignId && x.CompanyId == company && x.UserId == actor)
            ?? throw new UnauthorizedAccessException();
    }
    public Task<bool> ActionAsync(ClaimsPrincipal user, Guid campaignId, Guid? recipientId, string action) => WriteAsync(Company(user), async () =>
    {
        var campaign = await OwnedAsync(user, campaignId);
        if (action is "pause" or "continue")
        {
            if (campaign.Status != "Completed") campaign.Status = action == "pause" ? "Paused" : "Active";
            return true;
        }
        if (action is not ("open" or "sent" or "skip")) throw new InvalidOperationException("Acción inválida.");
        var recipient = await db.WhatsAppCampaignRecipients.SingleOrDefaultAsync(x => x.Id == recipientId && x.CampaignId == campaignId && x.CompanyId == campaign.CompanyId)
            ?? throw new InvalidOperationException("Contacto no disponible.");
        // Replaying the same command never affects the next recipient or changes a terminal status.
        if (recipient.Status != "Pending")
        {
            if (action == "open") throw new InvalidOperationException("Este contacto ya fue procesado. Actualiza el progreso antes de generar otro enlace.");
            return true;
        }
        if (campaign.Status != "Active") throw new InvalidOperationException("Continúa la selección antes de registrar la acción.");
        var firstId = await db.WhatsAppCampaignRecipients.Where(x => x.CampaignId == campaignId && x.Status == "Pending").OrderBy(x => x.Position).Select(x => x.Id).FirstOrDefaultAsync();
        if (firstId != recipient.Id) throw new InvalidOperationException("El contacto actual cambió. Actualiza el progreso.");
        if (action != "skip")
        {
            var consent = await db.WhatsAppContacts.SingleOrDefaultAsync(x => x.CompanyId == campaign.CompanyId && x.Phone == recipient.Phone);
            if (!Allowed(consent, Now)) throw new InvalidOperationException("Este número tiene una solicitud de no contacto registrada. Omítelo para continuar.");
            var source = await db.CustomerRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == recipient.RecordId && x.CompanyId == campaign.CompanyId);
            if (source is null || Phone(source.Cellphone) != recipient.Phone) throw new InvalidOperationException("El registro cambió o fue eliminado. Omítelo para continuar.");
        }
        if (action == "sent" && !recipient.OpenedAt.HasValue) throw new InvalidOperationException("Genera el enlace antes de declarar el envío.");
        recipient.ActorId = Actor(user); recipient.ActorName = user.Identity!.Name ?? ""; recipient.UpdatedAt = Now;
        if (action == "open") recipient.OpenedAt ??= Now;
        else
        {
            recipient.Status = action == "sent" ? "Sent" : "Skipped";
            if (action == "sent") recipient.DeclaredSentAt = Now;
            else recipient.Reason = "Omitido por el usuario";
            var remaining = await db.WhatsAppCampaignRecipients.CountAsync(x => x.CampaignId == campaignId && x.Status == "Pending");
            if (remaining == 1) campaign.Status = "Completed";
        }
        return true;
    });

    public sealed record RecipientView(Guid Id, int RecordId, string Name, string Phone, string Message, string Status, DateTime? OpenedAt, DateTime? DeclaredSentAt, string ActorName, bool Allowed, Guid CampaignId = default);
    public sealed record QueueContact(Guid Id, int RecordId, string Name, string Phone, string Status);
    public sealed record CampaignView(Guid Id, string Status, DateTime CreatedAt, string Message, int Total, int Sent, int Pending, int Skipped, RecipientView? Current, IReadOnlyList<QueueContact> Contacts);
    public sealed record CampaignState(string Message, CampaignView? Campaign, int TotalClients, int Contacted, int Generated);
    public async Task<CampaignState> StateAsync(ClaimsPrincipal user)
    {
        var companyId = Company(user); var actor = Actor(user);
        var message = await db.WhatsAppMessageSettings.AsNoTracking().Where(x => x.CompanyId == companyId && x.UserId == actor).Select(x => x.Message).SingleOrDefaultAsync() ?? "";
        var campaign = await db.WhatsAppCampaigns.AsNoTracking().Where(x => x.CompanyId == companyId && x.UserId == actor)
            .OrderBy(x => x.Status == "Completed").ThenByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        CampaignView? view = null;
        if (campaign is not null)
        {
            var items = await db.WhatsAppCampaignRecipients.AsNoTracking().Where(x => x.CampaignId == campaign.Id).OrderBy(x => x.Position).ToListAsync();
            var current = items.FirstOrDefault(x => x.Status == "Pending");
            var allowed = current is not null && Allowed(await ConsentAsync(companyId, current.Phone), Now);
            view = new(campaign.Id, campaign.Status, campaign.CreatedAt, campaign.Message, items.Count, items.Count(x => x.Status == "Sent"), items.Count(x => x.Status == "Pending"), items.Count(x => x.Status == "Skipped"),
                current is null ? null : new(current.Id, current.RecordId, current.Name, current.Phone, current.Message, current.Status, current.OpenedAt, current.DeclaredSentAt, current.ActorName, allowed, campaign.Id),
                items.Select(x => new QueueContact(x.Id, x.RecordId, x.Name, x.Phone, x.Status)).ToList());
        }
        var total = await db.CustomerRecords.AsNoTracking().Where(x => x.CompanyId == companyId).Select(x => x.Cellphone).ToListAsync();
        var phones = total.Select(Phone).Distinct().ToHashSet();
        var sent = await db.WhatsAppCampaignRecipients.AsNoTracking().Where(x => x.CompanyId == companyId && x.Status == "Sent").Select(x => x.Phone).Distinct().ToListAsync();
        var generated = await db.WhatsAppCampaignRecipients.AsNoTracking().Where(x => x.CompanyId == companyId && x.OpenedAt != null).Select(x => x.Phone).Distinct().ToListAsync();
        return new(message, view, phones.Count, sent.Count(phones.Contains), generated.Count(phones.Contains));
    }

    // Use only SQL-translatable normalization here; mobile Peruvian numbers receive country code 51.
    public IQueryable<CustomerRecord> Filter(IQueryable<CustomerRecord> query, string? filter, string? consentFilter, ClaimsPrincipal user)
    {
        var now = Now; var day = now.Date; var tomorrow = day.AddDays(1); var actor = Actor(user);
        var projected = query.Select(x => new { Record = x, Clean = x.Cellphone.Replace(" ", "").Replace("+", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace(".", "") })
            .Select(x => new { x.Record, Phone = x.Clean.Length == 9 && x.Clean.StartsWith("9") ? "51" + x.Clean : x.Clean });
        if (filter is "no" or "unsent") projected = projected.Where(x => !db.WhatsAppCampaignRecipients.Any(r => r.CompanyId == x.Record.CompanyId && r.Phone == x.Phone && r.Status == "Sent"));
        if (filter is "yes" or "sent") projected = projected.Where(x => db.WhatsAppCampaignRecipients.Any(r => r.CompanyId == x.Record.CompanyId && r.Phone == x.Phone && r.Status == "Sent"));
        if (filter == "generated" || filter == "reactivation") projected = projected.Where(x => db.WhatsAppCampaignRecipients.Any(r => r.CompanyId == x.Record.CompanyId && r.Phone == x.Phone && r.OpenedAt != null));
        if (filter == "today") projected = projected.Where(x => db.WhatsAppCampaignRecipients.Any(r => r.CompanyId == x.Record.CompanyId && r.Phone == x.Phone && r.Status == "Sent" && r.DeclaredSentAt >= day && r.DeclaredSentAt < tomorrow));
        if (filter == "pending") projected = projected.Where(x => db.WhatsAppCampaignRecipients.Any(r => r.CompanyId == x.Record.CompanyId && r.Phone == x.Phone && r.Status == "Pending" && r.Campaign.UserId == actor && r.Campaign.Status != "Completed"));
        return projected.Select(x => x.Record);
    }
    public async Task<List<RecipientView>> HistoryAsync(ClaimsPrincipal user, int recordId)
    {
        var companyId = Company(user);
        var record = await db.CustomerRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == recordId && x.CompanyId == companyId) ?? throw new UnauthorizedAccessException();
        var phone = Phone(record.Cellphone);
        return await db.WhatsAppCampaignRecipients.AsNoTracking().Where(x => x.CompanyId == companyId && (x.Phone == phone || x.RecordId == recordId))
            .OrderByDescending(x => x.UpdatedAt).Take(100).Select(x => new RecipientView(x.Id, x.RecordId, x.Name, x.Phone, x.Message, x.Status, x.OpenedAt, x.DeclaredSentAt, x.ActorName, false, x.CampaignId)).ToListAsync();
    }
}
