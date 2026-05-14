using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using OldSchoolLab.Services;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;

var builder = WebApplication.CreateBuilder(args);

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
});
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 5;
        options.User.RequireUniqueEmail = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(4);
    options.SlidingExpiration = true;
});

builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IPaymentProofStorage, PaymentProofStorage>();
builder.Services.AddScoped<ICompanyLogoStorage, CompanyLogoStorage>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<HtmlEncoder>(
    HtmlEncoder.Create(UnicodeRanges.All));



builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.AuthorizeFolder("/Catalogs", "");
    options.Conventions.AuthorizeFolder("/Admin", "");
});

var app = builder.Build();

// Forzar todas las respuestas a usar UTF-8
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Type"] = "text/html; charset=utf-8";
    await next();
});
var paymentProofsPath = Path.Combine(app.Environment.ContentRootPath, "storage", "payment-proofs");
Directory.CreateDirectory(paymentProofsPath);
var companyLogosPath = Path.Combine(app.Environment.ContentRootPath, "storage", "company-logos");
Directory.CreateDirectory(companyLogosPath);

await SeedData.InitializeAsync(app.Services);

app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        if (ctx.Response.ContentType?.StartsWith("text/html") == true
            && !ctx.Response.ContentType.Contains("charset"))
        {
            ctx.Response.ContentType += "; charset=utf-8";
        }
        return Task.CompletedTask;
    });
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(paymentProofsPath),
    RequestPath = "/payment-proofs"
});
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(companyLogosPath),
    RequestPath = "/company-logos"
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
