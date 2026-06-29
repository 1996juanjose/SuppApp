using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OldSchoolApi.Data;
using System.Text;
using OldSchoolApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = JwtKeyProvider.GetKey(builder.Configuration);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "OldSchoolApi", Version = "v1" });

    c.AddSecurityDefinition("X-Api-Key", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Description = "API Key para endpoints de N8N y vouchers"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Token para endpoints autenticados"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            []
        },
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "X-Api-Key" } },
            []
        }
    });
});

var app = builder.Build();

var configuredPaymentProofsPath = builder.Configuration["Storage:PaymentProofsPath"]?.Trim();
var paymentProofsPath = !string.IsNullOrWhiteSpace(configuredPaymentProofsPath) && Path.IsPathRooted(configuredPaymentProofsPath)
    ? configuredPaymentProofsPath
    : Path.Combine(builder.Environment.ContentRootPath, "storage", "payment-proofs");
Directory.CreateDirectory(paymentProofsPath);

var configuredShipmentProofsPath = builder.Configuration["Storage:ShipmentProofsPath"]?.Trim();
var shipmentProofsPath = !string.IsNullOrWhiteSpace(configuredShipmentProofsPath) && Path.IsPathRooted(configuredShipmentProofsPath)
    ? configuredShipmentProofsPath
    : Path.Combine(builder.Environment.ContentRootPath, "storage", "shipment-proofs");
Directory.CreateDirectory(shipmentProofsPath);

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OldSchoolApi v1"));

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(paymentProofsPath),
    RequestPath = "/payment-proofs"
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(shipmentProofsPath),
    RequestPath = "/shipment-proofs"
});

app.UseHttpsRedirection();
app.UseCors("AngularDev");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    name = "OldSchoolApi",
    status = "ok",
    endpoints = new[]
    {
        "/api/auth/login",
        "/api/records",
        "/api/records/n8n"
    }
}));

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/time", (IConfiguration config) =>
{
    var timeZone = AppClock.GetTimeZone(config);
    return Results.Ok(new
    {
        timeZone.Id,
        utcNow = DateTime.UtcNow,
        localNow = AppClock.Now(config)
    });
});

app.MapControllers();

app.MapGet("/api/records/calls/summary", async (ApiDbContext db, int? companyId, CancellationToken cancellationToken) =>
{
    var now = DateTime.Now;
    var summary = await db.CustomerRecords
        .AsNoTracking()
        .Where(x => !x.IsCallConcrete)
        .Where(x => x.CallScheduledAt.HasValue)
        .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
        .Select(x => new
        {
            x.Id,
            x.CompanyId,
            x.Cellphone,
            x.NameOrReference,
            x.CallActivity,
            x.CallScheduledAt,
            x.StatusCatalogId
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(new
    {
        now,
        upcoming = summary.Where(x => x.CallScheduledAt > now && x.CallScheduledAt <= now.AddMinutes(5)).OrderBy(x => x.CallScheduledAt),
        due = summary.Where(x => x.CallScheduledAt <= now).OrderBy(x => x.CallScheduledAt)
    });
});

app.MapGet("/api/bootstrap", (IConfiguration config) => Results.Ok(new
{
    auth = new
    {
        issuer = config["Jwt:Issuer"],
        audience = config["Jwt:Audience"]
    },
    modules = new[] { "records", "products", "audit", "alerts" }
}));

app.Run();
