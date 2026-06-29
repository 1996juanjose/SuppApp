using Microsoft.EntityFrameworkCore;
using OldSchool.AuthService.Data;
using OldSchool.AuthService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AngularDev");

app.MapGet("/", () => Results.Ok(new { service = "OldSchool.AuthService", status = "ok" }));
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapHealthChecks("/health-check");
app.MapControllers();

app.Run();