using SuplementosApp.Web.Components;
using Microsoft.EntityFrameworkCore;
using SuplementosApp.Web.Data;
using SuplementosApp.Web.Services;
using SuplementosApp.Web.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// DbContext usando SQL Server en DESKTOP-R5QRD73 (autenticación Windows)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Server=DESKTOP-R5QRD73;Database=SuplementosDb;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// Servicios de dominio
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<IReporteDiarioService, ReporteDiarioService>();
builder.Services.AddScoped<IVendedorService, VendedorService>();
builder.Services.AddScoped<IGastoService, GastoService>();
builder.Services.AddScoped<ISeguimientoClienteService, SeguimientoClienteService>();

// Servicios de catálogos
builder.Services.AddScoped<IEstadosVentaService, EstadosVentaService>();
builder.Services.AddScoped<IMetodosEnvioService, MetodosEnvioService>();
builder.Services.AddScoped<IMetodosPagoService, MetodosPagoService>();
builder.Services.AddScoped<ICanalesVentaService, CanalesVentaService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
