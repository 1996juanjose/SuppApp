using OldSchool.ChatApi;
using OldSchool.ChatApi.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddChatApiServices(builder.Configuration);
builder.Services.AddChatJwtAuthentication(builder.Configuration);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "OldSchool.ChatApi", status = "ok" }));
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapControllers();

app.Run();