using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using OldSchoolLab.Services;

static class Checks
{
    static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static async Task Reject(Func<Task> operation, string label)
    {
        try { await operation(); }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException) { Console.WriteLine("PASS " + label); return; }
        throw new Exception(label);
    }
    static ClaimsPrincipal User(int company, string actor = "operator") => new(new ClaimsIdentity([
        new Claim("company_id", company.ToString()), new Claim(ClaimTypes.NameIdentifier, actor),
        new Claim(ClaimTypes.Name, actor), new Claim(ClaimTypes.Role, "Vendedor")], "test"));
    static IConfiguration Config() { var c = new ConfigurationManager(); c["App:TimeZoneId"] = "America/Lima"; return c; }
    public static async Task Run(DbContextOptions<ApplicationDbContext> options)
    {
        var actor = User(1); var other = User(2); var config = Config();
        await using var db = new ApplicationDbContext(options);
        if (db.Database.IsRelational()) await db.Database.OpenConnectionAsync();
        db.Companies.AddRange(new Company { Id = 1, Name = "Isolated WhatsApp company" }, new Company { Id = 2, Name = "Other company" });
        db.Statuses.AddRange(new StatusCatalog { Id = 1, CompanyId = 1, Name = "Prospecto" }, new StatusCatalog { Id = 2, CompanyId = 2, Name = "Prospecto" });
        db.CustomerRecords.AddRange(
            new CustomerRecord { Id = 1, CompanyId = 1, StatusCatalogId = 1, Cellphone = "953342737", CreatedAt = new DateTime(2026, 1, 1) },
            new CustomerRecord { Id = 2, CompanyId = 1, StatusCatalogId = 1, Cellphone = "+51 953 342 737", CreatedAt = new DateTime(2026, 1, 2) },
            new CustomerRecord { Id = 3, CompanyId = 1, StatusCatalogId = 1, Cellphone = "900000003", CreatedAt = new DateTime(2025, 1, 1) },
            new CustomerRecord { Id = 4, CompanyId = 1, StatusCatalogId = 1, Cellphone = "900000004" },
            new CustomerRecord { Id = 5, CompanyId = 2, StatusCatalogId = 2, Cellphone = "953342737" });
        await db.SaveChangesAsync();
        var service = new WhatsAppCampaignService(db, config);
        Check(WhatsAppCampaignService.Phone("+51 (953) 342-737") == WhatsAppCampaignService.Phone("953342737"), "Peruvian phone aliases normalize to one contact");
        Check((await service.PreviewAsync(actor, [1, 2, 3], false)).Eligible == 2, "existing numbers without commercial metadata are selectable by default");
        await service.SetConsentAsync(actor, 4, "DoNotContact", "Opt out", null);
        await Reject(() => service.SetConsentAsync(actor, 5, "Authorized", "test", null), "cannot authorize another company's record");
        var preview = await service.PreviewAsync(actor, [1, 2, 3, 4], true);
        Check(preview.Unique == 3 && preview.Duplicates == 1 && preview.Blocked == 1 && preview.Eligible == 2, "preview deduplicates and excludes blocked contacts");
        await service.SaveMessageAsync(actor, "Hola & prueba 🧪\n¿Información?");
        var campaignId = Guid.NewGuid();
        await service.CreateAsync(actor, campaignId, [1, 2, 3, 4], "Hola & prueba 🧪\n¿Información?", true);
        await service.CreateAsync(actor, campaignId, [1, 2, 3, 4], "Hola & prueba 🧪\n¿Información?", true);
        Check(await db.WhatsAppCampaigns.CountAsync() == 1 && await db.WhatsAppCampaignRecipients.CountAsync() == 2, "campaign creation is idempotent and snapshots distinct numbers");
        await Reject(() => service.CreateAsync(actor, Guid.NewGuid(), [1], "Hola", true), "existing pending campaign must be resumed");
        var state = await service.StateAsync(actor); var first = state.Campaign!.Current!;
        Check(first.RecordId == 1, "campaign starts with the first selected distinct number");
        Check(state.Campaign.Contacts.Count == 2 && state.Campaign.Contacts.All(x => x.Status == "Pending"), "modal receives the complete persisted selection");
        await Reject(() => service.ActionAsync(actor, campaignId, first.Id, "sent"), "cannot declare sent before generating link");
        await service.ActionAsync(actor, campaignId, first.Id, "open");
        state = await service.StateAsync(actor);
        Check(state.Campaign!.Sent == 0 && state.Campaign.Current!.OpenedAt != null, "generating a link does not declare a message sent");
        Check(await service.Filter(db.CustomerRecords.Where(x => x.CompanyId == 1), "generated", null, actor).CountAsync() == 2, "generated/reactivation filter includes historical records for same number");
        await service.ActionAsync(actor, campaignId, first.Id, "sent");
        await service.ActionAsync(actor, campaignId, first.Id, "sent");
        await Reject(() => service.ActionAsync(actor, campaignId, first.Id, "open"), "stale browser tab cannot reopen a contact already processed");
        Check(await db.WhatsAppCampaignRecipients.CountAsync(x => x.Status == "Sent") == 1, "replayed sent confirmation never duplicates or advances another contact");
        Check(await service.Filter(db.CustomerRecords.Where(x => x.CompanyId == 1), "yes", null, actor).CountAsync() == 2, "reactivated yes filter matches both original and repeated purchase record");
        Check(await service.Filter(db.CustomerRecords.Where(x => x.CompanyId == 1), "no", null, actor).CountAsync() == 2, "reactivated no filter survives persistent history");
        Check(await service.Filter(db.CustomerRecords.Where(x => x.CompanyId == 1), "today", null, actor).CountAsync() == 2, "contacted-today uses Peru day");
        Check(await service.Filter(db.CustomerRecords.Where(x => x.CompanyId == 2), "sent", null, other).CountAsync() == 0, "same phone in another company has separate history");
        Check((await service.StateAsync(other)).Campaign is null, "company cannot see another company's campaign");
        await Reject(() => service.ActionAsync(other, campaignId, first.Id, "pause"), "another company cannot mutate a campaign");
        await Reject(() => service.ActionAsync(User(1, "another-operator"), campaignId, first.Id, "pause"), "another operator cannot mutate a campaign");
        await Reject(() => service.UpdatePendingMessageAsync(other, campaignId, "Otra empresa"), "cannot edit another company's campaign message");
        await service.UpdatePendingMessageAsync(actor, campaignId, "Mensaje nuevo 🧪");
        var edited = await service.StateAsync(actor);
        Check(edited.Campaign!.Current!.Message == "Mensaje nuevo 🧪" && edited.Campaign.Current.OpenedAt is null, "pending message is editable and requires a fresh link");
        Check((await service.HistoryAsync(actor, 1))[0].Message == "Hola & prueba 🧪\n¿Información?", "editing pending messages preserves sent history");
        await service.ActionAsync(actor, campaignId, null, "pause");
        await using (var reopened = new ApplicationDbContext(options))
        {
            if (reopened.Database.IsRelational()) await reopened.Database.OpenConnectionAsync();
            var restored = new WhatsAppCampaignService(reopened, config);
            var recovered = await restored.StateAsync(actor);
            Check(recovered.Campaign!.Status == "Paused" && recovered.Campaign.Sent == 1 && recovered.Campaign.Pending == 1 && recovered.Message.Contains("🧪"), "new context recovers saved message, sent history and pending campaign");
            await Reject(() => restored.ActionAsync(actor, campaignId, recovered.Campaign.Current!.Id, "open"), "paused campaign cannot open next link");
            await restored.ActionAsync(actor, campaignId, null, "continue");
            await restored.SetConsentAsync(actor, 3, "DoNotContact", "Revoked during campaign", null);
            await Reject(() => restored.ActionAsync(actor, campaignId, recovered.Campaign.Current!.Id, "open"), "consent is checked again after revocation");
            await restored.ActionAsync(actor, campaignId, recovered.Campaign.Current!.Id, "skip");
            var done = (await restored.StateAsync(actor)).Campaign!;
            Check(done.Status == "Completed" && done.Sent == 1 && done.Skipped == 1 && done.Pending == 0, "skip closes campaign without a false sent declaration");
        }
        db.ChangeTracker.Clear();
        Check((await service.PreviewAsync(actor, [1, 2], true)).Eligible == 0, "exclude-previous detects sent phone across records");
        Check((await service.PreviewAsync(actor, [1, 2], false)).Eligible == 1, "explicit repeat selection remains deduplicated");
        var history = await service.HistoryAsync(actor, 1);
        Check(history.Count == 1 && history[0].ActorName == "operator" && history[0].DeclaredSentAt != null && history[0].Message.Contains("🧪"), "history keeps original number, message, actor and declared timestamp");
        await service.SetConsentAsync(actor, 1, "Authorized", "Renewed", service.Now.AddDays(1));
        var contact = await db.WhatsAppContacts.SingleAsync(x => x.CompanyId == 1 && x.Phone == "51953342737");
        contact.ConsentExpiresAt = service.Now.AddSeconds(-1); await db.SaveChangesAsync();
        Check((await service.PreviewAsync(actor, [1], false)).Eligible == 1, "legacy authorization dates do not prevent manual selection");
    }
    private static async Task VerifyLargeFilteredCampaignAsync()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Companies.Add(new Company { Id = 1, Name = "Large filtered list" });
        db.Statuses.Add(new StatusCatalog { Id = 1, CompanyId = 1, Name = "Prospecto" });
        var records = Enumerable.Range(1, 1001).Select(id => new CustomerRecord
        {
            Id = id, CompanyId = 1, StatusCatalogId = 1, Cellphone = (900000000 + id).ToString()
        }).ToArray();
        db.CustomerRecords.AddRange(records);
        await db.SaveChangesAsync();
        var service = new WhatsAppCampaignService(db, Config());
        var campaignId = Guid.NewGuid();
        await service.CreateAsync(User(1), campaignId, records.Select(x => x.Id).ToArray(), "Prueba de lista filtrada", false);
        Check(await db.WhatsAppCampaignRecipients.CountAsync(x => x.CampaignId == campaignId) == 1001,
            "filtered lists above 1000 contacts are not silently truncated or rejected");
    }
    private static async Task VerifyCreatineDefaultsAsync()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Companies.AddRange(new Company { Id = 1, Name = "Price test" }, new Company { Id = 2, Name = "Other company" });
        var seed = typeof(SeedData).GetMethod("EnsureCreatinaAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        await (Task)seed.Invoke(null, [db, 1])!;
        await db.SaveChangesAsync();
        var creatine = await db.Products.Include(x => x.Prices).SingleAsync();
        Check(creatine.Prices.Single(x => x.Quantity == 1).Price == 89m
            && creatine.Prices.Single(x => x.Quantity == 2).Price == 139m
            && creatine.Prices.Single(x => x.Quantity == 3).Price == 169m, "new creatine uses 89, 139 and 169 soles");
        creatine.Prices.Single(x => x.Quantity == 2).Price = 149m;
        creatine.Prices.Single(x => x.Quantity == 3).Price = 189m;
        db.Products.Add(new Product { CompanyId = 2, Name = "Creatina", Prices = [new ProductPrice { Quantity = 2, Price = 199m }] });
        await db.SaveChangesAsync();
        for (var deployment = 0; deployment < 2; deployment++)
        {
            await (Task)seed.Invoke(null, [db, 1])!;
            await db.SaveChangesAsync();
            Check(creatine.Prices.Single(x => x.Quantity == 2).Price == 139m
                && creatine.Prices.Single(x => x.Quantity == 3).Price == 169m, "repeated startup corrects existing creatine prices");
        }
        Check((await db.Products.Include(x => x.Prices).SingleAsync(x => x.CompanyId == 2)).Prices.Single().Price == 199m,
            "default-price initialization stays within its company");
    }
    public static async Task Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--serve-fixture")
        {
            await FixtureHost.RunAsync(); return;
        }
        if (args.Length == 2 && args[0] == "--postgres-isolated")
        {
            var config = new ConfigurationManager(); config.AddJsonFile(Path.GetFullPath(args[1]));
            var connection = config.GetConnectionString("DefaultConnection") ?? throw new Exception("Missing connection");
            var schema = "oldschool_wa_test_" + Guid.NewGuid().ToString("N");
            await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
            await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
            try
            {
                var isolated = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema, Pooling = false };
                var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.ConnectionString, n => n.EnableRetryOnFailure()).Options;
                await using (var db = new ApplicationDbContext(options))
                {
                    await db.Database.OpenConnectionAsync();
                    var sql = db.Database.GenerateCreateScript();
                    if (sql.Contains("\"public\".")) throw new Exception("Isolation failure");
                    await db.Database.ExecuteSqlRawAsync(sql);
                    // Verify additive startup creation against an existing application database.
                    foreach (var table in new[] { "WhatsAppCampaignRecipients", "WhatsAppCampaigns", "WhatsAppConsentChanges", "WhatsAppContacts", "WhatsAppMessageSettings" })
                        await db.Database.ExecuteSqlRawAsync($"DROP TABLE \"{table}\"");
                    await WhatsAppCampaignSchema.EnsureAsync(db); await WhatsAppCampaignSchema.EnsureAsync(db);
                }
                await Run(options);
                Console.WriteLine("PASS real PostgreSQL additive schema, retries, persistence and translated filters");
            }
            finally
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(schema, "^oldschool_wa_test_[a-f0-9]{32}$")) throw new Exception("Unsafe schema cleanup");
                await using var cleanup = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin); await cleanup.ExecuteNonQueryAsync();
            }
        }
        else
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            await Run(options);
            await VerifyCreatineDefaultsAsync();
            await VerifyLargeFilteredCampaignAsync();
        }
    }
}
