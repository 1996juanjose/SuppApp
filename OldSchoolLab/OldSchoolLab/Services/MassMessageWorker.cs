using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;

namespace OldSchoolLab.Services;

// A PostgreSQL session lock serializes sends for the same Evolution instance,
// including when several web replicas are running.
public sealed class MassMessageWorker(IServiceScopeFactory scopes, ILogger<MassMessageWorker> logger, TimeProvider time) : BackgroundService
{
    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var ids = await db.MassMessageCampaigns.AsNoTracking().Where(x => x.Status == "Running")
                    .OrderBy(x => x.CreatedAt).Select(x => x.Id).ToListAsync(stoppingToken);
                foreach (var id in ids) await ProcessAsync(id, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Mass message worker unavailable: {ErrorType}", ex.GetType().Name); }
            try { await Task.Delay(1000, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    public async Task ProcessAsync(Guid id, CancellationToken cancellation)
    {
        using var scope = scopes.CreateScope();
        var registeredDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Do not reconnect transparently while holding a session advisory lock.
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(registeredDb.Database.GetConnectionString()).Options);
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var campaign = await db.MassMessageCampaigns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellation);
        if (campaign?.Status != "Running") return;
        var connection = EvolutionConnection.For(config, campaign.CompanyId);
        if (!connection.Ready || connection.Key != campaign.SenderKey)
        {
            await PauseAsync(db, id, "Configura la misma instancia de Evolution antes de continuar.", cancellation);
            return;
        }
        await db.Database.OpenConnectionAsync(cancellation);
        var sql = db.Database.GetDbConnection();
        await using var command = sql.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(hashtextextended(@key, 0))";
        var parameter = command.CreateParameter(); parameter.ParameterName = "key"; parameter.Value = connection.Key;
        command.Parameters.Add(parameter);
        if (!Equals(await command.ExecuteScalarAsync(cancellation), true)) return;
        try
        {
            if (!await db.MassMessageCampaigns.AnyAsync(x => x.Id == id && x.Status == "Running", cancellation)) return;
            // A previous process may have stopped after Evolution accepted a request.
            // Never retry an uncertain send automatically.
            if (await db.MassMessageRecipients.AnyAsync(x => x.CampaignId == id && x.Status == "Sending", cancellation))
            {
                await db.MassMessageRecipients.Where(x => x.CampaignId == id && x.Status == "Sending")
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Unknown")
                        .SetProperty(x => x.Detail, "Envío interrumpido. Verifica en WhatsApp; no se reenviará automáticamente."), cancellation);
                await PauseAsync(db, id, "Hay un envío sin confirmar. Revísalo antes de continuar con los demás.", cancellation);
                return;
            }
            var sender = await db.MassMessageSenders.FindAsync([connection.Key], cancellation);
            if (sender == null)
            {
                sender = new MassMessageSender { Key = connection.Key, NextSendAt = UtcNow };
                db.MassMessageSenders.Add(sender);
            }
            var recipient = await db.MassMessageRecipients.Where(x => x.CampaignId == id && x.Status == "Pending")
                .OrderBy(x => x.Position).FirstOrDefaultAsync(cancellation);
            if (recipient == null)
            {
                await db.MassMessageCampaigns.Where(x => x.Id == id && x.Status == "Running")
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Completed"), cancellation);
                return;
            }
            if (sender.NextSendAt > UtcNow) return;
            var contact = await db.WhatsAppContacts.AsNoTracking().SingleOrDefaultAsync(
                x => x.CompanyId == campaign.CompanyId && x.Phone == recipient.Phone, cancellation);
            var localNow = scope.ServiceProvider.GetRequiredService<WhatsAppCampaignService>().Now;
            if (!MassMessageRules.Allowed(contact, localNow))
            {
                recipient.Status = "Skipped"; recipient.Detail = "Contacto excluido por su autorización comercial.";
                await db.SaveChangesAsync(cancellation); return;
            }
            recipient.Status = "Sending"; recipient.AttemptedAt = UtcNow;
            sender.NextSendAt = UtcNow.AddSeconds(campaign.IntervalSeconds);
            await db.SaveChangesAsync(cancellation);
            (string Status, string Id, string Detail) result;
            try
            {
                result = await scope.ServiceProvider.GetRequiredService<EvolutionSender>()
                    .SendAsync(connection, recipient.Phone, campaign.Message, cancellation);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                if (cancellation.IsCancellationRequested) return; // recovered as Unknown on next startup
                result = ("Unknown", "", "No se confirmó el envío. Verifica en WhatsApp antes de continuar.");
            }
            recipient.Status = result.Status; recipient.MessageId = result.Id; recipient.Detail = result.Detail;
            sender.NextSendAt = UtcNow.AddSeconds(campaign.IntervalSeconds);
            await db.SaveChangesAsync(cancellation);
            if (result.Status != "Accepted") await PauseAsync(db, id, result.Detail, cancellation);
            else if (!await db.MassMessageRecipients.AnyAsync(x => x.CampaignId == id && (x.Status == "Pending" || x.Status == "Sending"), cancellation))
                await db.MassMessageCampaigns.Where(x => x.Id == id && x.Status == "Running")
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Completed"), cancellation);
        }
        finally
        {
            command.CommandText = "SELECT pg_advisory_unlock(hashtextextended(@key, 0))";
            await command.ExecuteScalarAsync(CancellationToken.None);
        }
    }

    private static Task<int> PauseAsync(ApplicationDbContext db, Guid id, string notice, CancellationToken ct) =>
        db.MassMessageCampaigns.Where(x => x.Id == id && x.Status == "Running")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Paused").SetProperty(x => x.Notice, notice), ct);
}

public static class MassMessageRules
{
    public static bool Allowed(WhatsAppContact? contact, DateTime now) => contact?.ConsentStatus != "DoNotContact"
        && !(contact?.ConsentStatus == "Authorized" && contact.ConsentExpiresAt.HasValue && contact.ConsentExpiresAt <= now);

    public static string Label(string status) => status switch
    {
        "Draft" => "Borrador", "Running" => "En curso", "Paused" => "Pausada", "Cancelled" => "Cancelada",
        "Completed" => "Finalizada", "Pending" => "Pendiente", "Sending" => "Enviando",
        "Accepted" => "Aceptado por Evolution", "Failed" => "Rechazado", "Unknown" => "Sin confirmar",
        "Skipped" => "Omitido", _ => status
    };
}
