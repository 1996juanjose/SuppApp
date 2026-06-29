using OldSchool.ApiGateway.Security;
using Yarp.ReverseProxy;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddGatewayJwtAuthentication(builder.Configuration);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { service = "OldSchool.ApiGateway", status = "ok" }));
app.MapReverseProxy().RequireAuthorization();

app.Run();