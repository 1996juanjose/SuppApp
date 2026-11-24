using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using OldSchoolLab.Pages.MassMessages;
using OldSchoolLab.Services;

if (args.Length != 1) throw new Exception("Usage: MassMessageChecks <configuration-file>. Uses an isolated temporary PostgreSQL schema and fake Evolution HTTP only.");
var configuration = new ConfigurationManager(); configuration.AddJsonFile(Path.GetFullPath(args[0]));
var source = configuration.GetConnectionString("DefaultConnection") ?? throw new Exception("Missing connection");
var schema = "mass_message_test_" + Guid.NewGuid().ToString("N");
await using var admin = new NpgsqlConnection(source); await admin.OpenAsync();
await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
try
{
    var isolated = new NpgsqlConnectionStringBuilder(source) { SearchPath = schema, Pooling = false };
    var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.ConnectionString, n => n.EnableRetryOnFailure()).Options;
    await using var db = new ApplicationDbContext(options);
    await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
    foreach (var table in new[] { "MassMessageRecipients", "MassMessageCampaigns", "MassMessageSenders" })
        await db.Database.ExecuteSqlRawAsync($"DROP TABLE \"{table}\"");
    await MassMessageSchema.EnsureAsync(db); await MassMessageSchema.EnsureAsync(db);
    Check(true, "additive schema is repeatable against existing PostgreSQL tables");
    var config = new ConfigurationManager();
    config["Evolution:Companies:1:BaseUrl"] = "https://evolution.invalid";
    config["Evolution:Companies:1:Instance"] = "Fixture";
    config["Evolution:Companies:1:ApiKey"] = "fake-test-key";
    var fake = new FakeEvolution();
    var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(config)
        .AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(isolated.ConnectionString, n => n.EnableRetryOnFailure()))
        .AddScoped<WhatsAppCampaignService>().AddSingleton<IHttpClientFactory>(fake).AddSingleton<EvolutionSender>();
    await using var provider = services.BuildServiceProvider();
    var time = new ManualTime();
    var worker = new MassMessageWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<MassMessageWorker>.Instance, time);
    var user = Principal(1); var clock = new WhatsAppCampaignService(db, config);
    db.Companies.AddRange(new Company { Id = 1, Name = "Fixture one" }, new Company { Id = 2, Name = "Fixture two" });
    db.Statuses.AddRange(new StatusCatalog { Id = 1, CompanyId = 1, Name = "Prospecto" }, new StatusCatalog { Id = 2, CompanyId = 2, Name = "Prospecto" });
    db.CustomerRecords.AddRange(Record(1, 1, "900000001"), Record(2, 1, "+51 900 000 001"), Record(3, 1, "900000003"), Record(4, 2, "900000004"));
    db.WhatsAppContacts.Add(new WhatsAppContact { CompanyId = 1, Phone = "51900000003", ConsentStatus = "DoNotContact" });
    await db.SaveChangesAsync();
    var page = Page(user);
    await page.OnGetAsync();
    Check(page.Prospects.Count == 3, "prospect query is company scoped");
    page.Input = new() { Name = "Test campaign", Message = "Hello 🧪", IntervalSeconds = 2,
        RecordIds = [1, 2, 3], Numbers = "+51 900 000 001\n900000002\n123\n900000003" };
    var result = await page.OnPostCreateAsync(); var id = page.Input.Id;
    Check(result is RedirectToPageResult && await db.MassMessageRecipients.CountAsync(x => x.CampaignId == id) == 2,
        "manual and database numbers deduplicate and exclude invalid and opted-out numbers");
    Check(fake.Count == 0 && (await db.MassMessageCampaigns.FindAsync(id))!.Status == "Draft", "review creates a draft and performs no HTTP send");
    await page.OnPostCreateAsync();
    Check(await db.MassMessageCampaigns.CountAsync() == 1, "double submit cannot duplicate a campaign");
    var hostile = Page(user); hostile.Input = new() { Name = "Cross-company", Message = "test", RecordIds = [4] };
    Check(await hostile.OnPostCreateAsync() is PageResult && !hostile.ModelState.IsValid, "cross-company selections rejected");
    Check(await Page(Principal(2)).OnPostControlAsync(id, "start") is NotFoundResult, "another company cannot start a campaign");
    Check(await Page(Principal(1, "other")).OnPostControlAsync(id, "start") is NotFoundResult, "another seller cannot start a campaign");
    await page.OnPostControlAsync(id, "start");
    fake.Block = new(TaskCreationOptions.RunContinuationsAsynchronously);
    var first = worker.ProcessAsync(id, default);
    await fake.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
    await worker.ProcessAsync(id, default);
    Check(fake.Count == 1, "two workers cannot send the same recipient concurrently");
    fake.Block.SetResult(); await first; fake.Block = null;
    await worker.ProcessAsync(id, default);
    Check(fake.Count == 1, "configured interval prevents an immediate second send");
    time.Current = time.Current.AddSeconds(2);
    await worker.ProcessAsync(id, default);
    Check(fake.Count == 2 && await db.MassMessageRecipients.AsNoTracking().CountAsync(x => x.CampaignId == id && x.Status == "Accepted") == 2,
        "accepted results and message IDs persist after the interval");
    await page.OnPostControlAsync(id, "pause");
    await worker.ProcessAsync(id, default); Check(fake.Count == 2, "paused campaign sends nothing");

    async Task<Guid> Campaign(string phone, string status = "Pending")
    {
        var c = new MassMessageCampaign { Id = Guid.NewGuid(), CompanyId = 1, UserId = "operator", Name = "Worker test",
            Message = "Fake transport only", IntervalSeconds = 2, SenderKey = EvolutionConnection.For(config, 1).Key, Status = "Running" };
        db.MassMessageCampaigns.Add(c);
        db.MassMessageRecipients.Add(new() { CampaignId = c.Id, Phone = phone, Status = status });
        await db.SaveChangesAsync(); return c.Id;
    }
    async Task Due() => await db.MassMessageSenders.ExecuteUpdateAsync(s => s.SetProperty(x => x.NextSendAt, time.Current.UtcDateTime.AddMinutes(-1)));
    var shared = await Campaign("51900000005");
    await worker.ProcessAsync(shared, default); Check(fake.Count == 2, "campaigns sharing an instance cannot bypass the interval");
    await Due(); fake.Mode = "failure"; await worker.ProcessAsync(shared, default);
    Check((await db.MassMessageCampaigns.AsNoTracking().SingleAsync(x => x.Id == shared)).Status == "Paused", "API rejection pauses the campaign");
    var before = fake.Count; await page.OnPostControlAsync(shared, "start"); await worker.ProcessAsync(shared, default);
    Check(fake.Count == before, "continuing does not retry a rejected recipient");
    var uncertain = await Campaign("51900000006"); await Due(); fake.Mode = "timeout";
    await worker.ProcessAsync(uncertain, default);
    Check((await db.MassMessageRecipients.AsNoTracking().SingleAsync(x => x.CampaignId == uncertain)).Status == "Unknown",
        "lost response is recorded as uncertain instead of failed or delivered");
    before = fake.Count; var crashed = await Campaign("51900000007", "Sending");
    await worker.ProcessAsync(crashed, default);
    Check(fake.Count == before && (await db.MassMessageRecipients.AsNoTracking().SingleAsync(x => x.CampaignId == crashed)).Status == "Unknown",
        "restart recovers an interrupted send without automatically repeating it");
    var revoked = await Campaign("51900000003"); await Due(); await worker.ProcessAsync(revoked, default);
    Check(fake.Count == before && (await db.MassMessageRecipients.AsNoTracking().SingleAsync(x => x.CampaignId == revoked)).Status == "Skipped",
        "opt-outs are checked again immediately before sending");
    Check(!MassMessageRules.Allowed(new WhatsAppContact { ConsentStatus = "Authorized", ConsentExpiresAt = clock.Now.AddDays(-1) }, clock.Now), "expired authorization excluded");
    Console.WriteLine("All mass-message checks passed. No real WhatsApp requests were made.");

    IndexModel Page(ClaimsPrincipal principal) => new(db, config, clock)
    { PageContext = new PageContext { HttpContext = new DefaultHttpContext { User = principal } } };
}
finally
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(schema, "^mass_message_test_[a-f0-9]{32}$")) throw new Exception("Unsafe schema cleanup");
    await using var cleanup = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin); await cleanup.ExecuteNonQueryAsync();
}

static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); }
static ClaimsPrincipal Principal(int company, string actor = "operator") => new(new ClaimsIdentity([
    new Claim("company_id", company.ToString()), new Claim(ClaimTypes.NameIdentifier, actor), new Claim(ClaimTypes.Name, actor), new Claim(ClaimTypes.Role, "Vendedor")], "fixture"));
static CustomerRecord Record(int id, int company, string phone) => new() { Id = id, CompanyId = company, StatusCatalogId = company,
    Cellphone = phone, NameOrReference = "Fixture", CreatedAt = new DateTime(2026, 11, 1) };

sealed class FakeEvolution : HttpMessageHandler, IHttpClientFactory
{
    public int Count;
    public string Mode = "accepted";
    public TaskCompletionSource? Block;
    public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public HttpClient CreateClient(string name) => new(this, false);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
    {
        if (request.RequestUri!.Host != "evolution.invalid") throw new Exception("Real HTTP prohibited in tests");
        Interlocked.Increment(ref Count); Entered.TrySetResult();
        if (Block != null) await Block.Task;
        if (Mode == "timeout") throw new TaskCanceledException("Simulated lost response");
        return new HttpResponseMessage(Mode == "failure" ? HttpStatusCode.BadRequest : HttpStatusCode.Created)
        { Content = new StringContent("{\"key\":{\"id\":\"fake-" + Count + "\"}}") };
    }
}

sealed class ManualTime : TimeProvider
{
    public DateTimeOffset Current = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Current;
}
