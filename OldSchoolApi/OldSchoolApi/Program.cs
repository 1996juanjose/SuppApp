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

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OldSchoolApi v1"));

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(paymentProofsPath),
    RequestPath = "/payment-proofs"
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();
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

app.Run();
