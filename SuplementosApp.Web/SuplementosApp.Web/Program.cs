using SuplementosApp.Web.Components;
using Microsoft.EntityFrameworkCore;
using SuplementosApp.Web.Data;
using SuplementosApp.Web.Services;
using SuplementosApp.Web.Services.Interfaces;
using SuplementosApp.Web.Models;
using System.Text.Json;

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

// Evolution API (WhatsApp)
builder.Services.AddSingleton<EvolutionApiService>();

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

// ?? Endpoint para Make + Z-API ?????????????????????????????????????????????
// Make llama a este endpoint cuando llega un mensaje nuevo en Z-API
// Body esperado: { "numero": "51999...", "nombre": "Juan", "mensaje": "Hola" }

app.MapPost("/api/prospecto", async (
    HttpRequest request,
    IServiceScopeFactory scopeFactory,
    IConfiguration config) =>
{
    // Seguridad: token en header X-Api-Key
    var apiKey = config["WhatsApp:ApiKey"];
    if (!string.IsNullOrEmpty(apiKey))
    {
        var headerKey = request.Headers["X-Api-Key"].ToString();
        if (headerKey != apiKey)
            return Results.Unauthorized();
    }

    NuevoProspectoRequest? body;
    try { body = await request.ReadFromJsonAsync<NuevoProspectoRequest>(); }
    catch { return Results.BadRequest("JSON inválido"); }

    if (body == null || string.IsNullOrWhiteSpace(body.Numero))
        return Results.BadRequest("Campo 'numero' es requerido");

    // Normalizar número (quitar espacios, guiones)
    var numero = body.Numero.Replace(" ", "").Replace("-", "");
    if (!numero.StartsWith("+")) numero = "+" + numero;

    using var scope = scopeFactory.CreateScope();
    var service = scope.ServiceProvider.GetRequiredService<ISeguimientoClienteService>();

    // No duplicar si ya existe
    if (await service.ExisteNumeroAsync(numero))
        return Results.Ok(new { creado = false, mensaje = "Número ya registrado" });

    var nombre = string.IsNullOrWhiteSpace(body.Nombre) ? "Prospecto" : body.Nombre;
    var obs = string.IsNullOrWhiteSpace(body.Mensaje) ? null : $"Primer mensaje: {body.Mensaje}";

    await service.CreateAsync(new SeguimientoCliente
    {
        Nombre           = nombre,
        Numero           = numero,
        ProductoInteres  = "Prospecto",
        LlamadaRealizada = false,
        Rechazado        = false,
        Observacion      = obs
    });

    return Results.Ok(new { creado = true, numero, nombre });
});

// ?? Webhook Meta (ya existente) ????????????????????????????????????????????
app.MapGet("/webhook/whatsapp", (HttpRequest request, IConfiguration config) =>
{
    var verifyToken = config["WhatsApp:VerifyToken"];
    var mode        = request.Query["hub.mode"].ToString();
    var token       = request.Query["hub.verify_token"].ToString();
    var challenge   = request.Query["hub.challenge"].ToString();

    if (mode == "subscribe" && token == verifyToken)
        return Results.Ok(int.Parse(challenge));

    return Results.Forbid();
});

// POST: mensajes entrantes desde Meta
app.MapPost("/webhook/whatsapp", async (HttpRequest request, IServiceScopeFactory scopeFactory) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();

    WhatsAppWebhookPayload? payload;
    try
    {
        payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(body);
    }
    catch
    {
        return Results.Ok(); // siempre responder 200 a Meta
    }

    if (payload?.Entry == null) return Results.Ok();

    foreach (var entry in payload.Entry)
    foreach (var change in entry.Changes.Where(c => c.Field == "messages"))
    {
        var messages = change.Value?.Messages ?? [];
        var contacts = change.Value?.Contacts ?? [];

        foreach (var msg in messages)
        {
            var numero = "+" + msg.From; // Meta envía sin el +
            var nombreContacto = contacts
                .FirstOrDefault(c => c.WaId == msg.From)
                ?.Profile?.Name;

            var primerMensaje = msg.Text?.Body ?? "";

            using var scope = scopeFactory.CreateScope();
            var seguimientoService = scope.ServiceProvider
                .GetRequiredService<ISeguimientoClienteService>();

            // No duplicar si el número ya existe
            if (await seguimientoService.ExisteNumeroAsync(numero))
                continue;

            await seguimientoService.CreateAsync(new SeguimientoCliente
            {
                Nombre           = string.IsNullOrWhiteSpace(nombreContacto) ? "Prospecto" : nombreContacto,
                Numero           = numero,
                ProductoInteres  = "Prospecto",
                LlamadaRealizada = false,
                Rechazado        = false,
                Observacion      = string.IsNullOrWhiteSpace(primerMensaje)
                                   ? null
                                   : $"Primer mensaje: {primerMensaje}"
            });
        }
    }

    return Results.Ok();
});

// ?? Endpoint para Evolution API (gratis, directo sin Make) ????????????????
// Configurar en Evolution API: webhook URL ? https://TU-DOMINIO/api/prospecto/evolution
// Events a activar: MESSAGES_UPSERT

app.MapPost("/api/prospecto/evolution", async (
    HttpRequest request,
    IServiceScopeFactory scopeFactory,
    IConfiguration config) =>
{
    // Seguridad: API key en header (configurable en Evolution API webhook headers)
    var apiKey = config["WhatsApp:ApiKey"];
    if (!string.IsNullOrEmpty(apiKey))
    {
        var headerKey = request.Headers["apikey"].ToString();
        if (headerKey != apiKey)
            return Results.Unauthorized();
    }

    EvolutionWebhookPayload? payload;
    try { payload = await request.ReadFromJsonAsync<EvolutionWebhookPayload>(); }
    catch { return Results.Ok(); }

    if (payload?.Data == null) return Results.Ok();

    // Solo procesar mensajes recibidos (no los enviados por nosotros)
    if (payload.Event != "messages.upsert") return Results.Ok();
    if (payload.Data.Key?.FromMe == true) return Results.Ok();

    // Extraer número: "5511999999999@s.whatsapp.net" ? "+5511999999999"
    var remoteJid = payload.Data.Key?.RemoteJid ?? "";
    if (remoteJid.Contains("@g.us")) return Results.Ok(); // ignorar grupos

    var numero = "+" + remoteJid.Replace("@s.whatsapp.net", "").Replace("@c.us", "");
    var nombre = string.IsNullOrWhiteSpace(payload.Data.PushName) ? "Prospecto" : payload.Data.PushName;
    var mensaje = payload.Data.Message?.Conversation
               ?? payload.Data.Message?.ExtendedTextMessage?.Text
               ?? "";

    // Convertir Unix timestamp a DateTime (Evolution API lo envía en segundos)
    DateTime? fechaContacto = payload.Data.MessageTimestamp.HasValue
        ? DateTimeOffset.FromUnixTimeSeconds(payload.Data.MessageTimestamp.Value).LocalDateTime
        : DateTime.Now;

    using var scope = scopeFactory.CreateScope();
    var service = scope.ServiceProvider.GetRequiredService<ISeguimientoClienteService>();

    if (await service.ExisteNumeroAsync(numero))
        return Results.Ok(new { creado = false, mensaje = "Número ya registrado" });

    await service.CreateAsync(new SeguimientoCliente
    {
        Nombre           = nombre,
        Numero           = numero,
        ProductoInteres  = "Prospecto",
        LlamadaRealizada = false,
        Rechazado        = false,
        FechaContacto    = fechaContacto,
        Observacion      = string.IsNullOrWhiteSpace(mensaje) ? null : $"Primer mensaje: {mensaje}"
    });

    return Results.Ok(new { creado = true, numero, nombre });
});

// ??????????????????????????????????????????????????????????????????????????

app.Run();

// Modelo para el endpoint /api/prospecto
record NuevoProspectoRequest(string Numero, string? Nombre, string? Mensaje);
