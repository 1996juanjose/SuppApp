using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using OldSchoolLab.Services;

// Local-only UI test host. No production configuration, credentials, services or startup seeding.
internal static class FixtureHost
{
    public static async Task RunAsync()
    {
        var contentRoot = Path.GetFullPath(Directory.Exists("OldSchoolLab") ? "OldSchoolLab" : "../../OldSchoolLab");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = contentRoot, WebRootPath = Path.Combine(contentRoot, "wwwroot"), ApplicationName = typeof(ApplicationDbContext).Assembly.GetName().Name });
        builder.Logging.ClearProviders(); builder.Logging.AddConsole(); builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.PostConfigure<KeyManagementOptions>(options => { options.XmlRepository = new FixtureXmlRepository(); options.XmlEncryptor = null; });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Configuration["App:TimeZoneId"] = "America/Lima";
        builder.WebHost.UseUrls("http://127.0.0.1:5187");
        builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase("whatsapp-ui-fixture"));
        builder.Services.AddScoped<CashRegisterService>(); builder.Services.AddScoped<WhatsAppCampaignService>();
        builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<IAuditService, AuditService>();
        builder.Services.AddScoped<IPaymentProofStorage, FixtureProofStorage>();
        builder.Services.AddRazorPages().AddApplicationPart(typeof(ApplicationDbContext).Assembly);
        builder.Services.AddDistributedMemoryCache(); builder.Services.AddSession();
        builder.Services.AddAuthentication("Fixture").AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("Fixture", _ => {});
        builder.Services.AddAuthorization();
        var app = builder.Build();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Companies.AddRange(new Company { Id = 1, Name = "Empresa de prueba" }, new Company { Id = 2, Name = "Otra empresa" });
            db.Statuses.AddRange(
                new StatusCatalog { Id = 1, CompanyId = 1, Name = "Prospecto", BadgeClass = "primary" },
                new StatusCatalog { Id = 2, CompanyId = 1, Name = "Rechazo", BadgeClass = "danger" },
                new StatusCatalog { Id = 3, CompanyId = 1, Name = "Interesado", BadgeClass = "warning" },
                new StatusCatalog { Id = 4, CompanyId = 2, Name = "Prospecto", BadgeClass = "primary" });
            db.CustomerRecords.AddRange(
                new CustomerRecord { Id = 1, CompanyId = 1, StatusCatalogId = 1, Cellphone = "900000001", NameOrReference = "Cliente de prueba A", CreatedAt = DateTime.Today },
                new CustomerRecord { Id = 2, CompanyId = 1, StatusCatalogId = 1, Cellphone = "+51 900 000 001", NameOrReference = "Mismo número anterior", CreatedAt = DateTime.Today },
                new CustomerRecord { Id = 3, CompanyId = 1, StatusCatalogId = 1, Cellphone = "900000003", NameOrReference = "Cliente de prueba B", CreatedAt = DateTime.Today },
                new CustomerRecord { Id = 4, CompanyId = 1, StatusCatalogId = 1, Cellphone = "900000004", NameOrReference = "No contactar", CreatedAt = DateTime.Today },
                new CustomerRecord { Id = 5, CompanyId = 1, StatusCatalogId = 1, Cellphone = "900000005", NameOrReference = "Contacto antiguo", CreatedAt = DateTime.Today.AddDays(-5) },
                new CustomerRecord { Id = 6, CompanyId = 1, StatusCatalogId = 3, Cellphone = "900000006", NameOrReference = "Interesado de prueba", CreatedAt = DateTime.Today },
                new CustomerRecord { Id = 7, CompanyId = 2, StatusCatalogId = 4, Cellphone = "900000007", NameOrReference = "Contacto otra empresa", CreatedAt = DateTime.Today });
            await db.SaveChangesAsync();
            var service = scope.ServiceProvider.GetRequiredService<WhatsAppCampaignService>();
            await service.SetConsentAsync(FixtureAuthentication.Principal(), 4, "DoNotContact", "Prueba de exclusión", null);
        }
        app.UseStaticFiles(); app.UseRouting(); app.UseSession(); app.UseAuthentication(); app.UseAuthorization(); app.MapRazorPages();
        await app.RunAsync();
    }
}
internal sealed class FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    internal static ClaimsPrincipal Principal() => new(new ClaimsIdentity([
        new Claim(ClaimTypes.NameIdentifier, "ui-fixture"), new Claim(ClaimTypes.Name, "UI PRUEBA"),
        new Claim(ClaimTypes.Role, "Vendedor"), new Claim("company_id", "1")], "Fixture"));
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(Principal(), "Fixture")));
}

internal sealed class FixtureProofStorage : IPaymentProofStorage
{
    public long MaxFileSizeBytes => 1024 * 1024;
    public Task<StoredPaymentProof?> SaveAsync(IFormFile? file, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Uploads disabled in UI fixture.");
}

internal sealed class FixtureXmlRepository : IXmlRepository
{
    private readonly List<XElement> elements = [];
    public IReadOnlyCollection<XElement> GetAllElements() => elements.Select(x => new XElement(x)).ToArray();
    public void StoreElement(XElement element, string friendlyName) => elements.Add(new XElement(element));
}
