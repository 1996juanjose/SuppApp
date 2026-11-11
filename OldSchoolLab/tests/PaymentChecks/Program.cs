using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using OldSchoolApi.Controllers;
using OldSchoolApi.Data;
using OldSchoolApi.Models;

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine("PASS " + name);
}

static Dictionary<string, object?> Receipt(string provider = "Yape") => new()
{
    ["valid"] = true,
    ["amount"] = 30m,
    ["monto"] = 30m,
    ["paymentType"] = provider,
    ["tipo"] = provider,
    ["date"] = "2026-11-08 22:58:00",
    ["fecha_hora"] = "2026-11-08 22:58:00",
    ["documentType"] = "payment_receipt",
    ["transactionStatus"] = "completed",
    ["operationNumber"] = "123456789",
    ["nro_operacion"] = "123456789",
    ["recipient"] = "Destinatario de prueba",
    ["confirmationText"] = "Operacion exitosa"
};

var invalid = new List<(string Name, Dictionary<string, object?> Data)>();
var legacy = Receipt();
foreach (var field in new[] { "documentType", "transactionStatus", "operationNumber", "recipient", "confirmationText" })
    legacy.Remove(field);
invalid.Add(("legacy amount-only extraction", legacy));
foreach (var (field, value) in new (string, object?)[]
{
    ("documentType", "chat"), ("documentType", "quote"), ("transactionStatus", "pending"),
    ("transactionStatus", "failed"), ("valid", false), ("operationNumber", ""),
    ("operationNumber", "ABC123"), ("recipient", ""), ("confirmationText", "")
})
{
    var data = Receipt();
    data[field] = value;
    invalid.Add(($"invalid {field}={value}", data));
}
var missingDate = Receipt();
missingDate["date"] = missingDate["fecha_hora"] = "";
invalid.Add(("missing operation date", missingDate));
var unknown = Receipt("Desconocido");
invalid.Add(("unknown institution", unknown));
var noAmount = Receipt();
noAmount["amount"] = noAmount["monto"] = 0m;
invalid.Add(("zero amount", noAmount));

string? liveKey = null;
if (args.Length >= 3 && args[0] == "--live-negative")
{
    using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));
    liveKey = settings.RootElement.GetProperty("OpenAI").GetProperty("ApiKey").GetString();
    foreach (var endpoint in new[] { "process-payment", "process-voucher" })
        foreach (var image in args.Skip(2))
            await RunAsync(endpoint, "supplied conversation screenshot", "", false, image);
    Console.WriteLine("Live image rejection checks passed using isolated in-memory data.");
    return;
}

foreach (var endpoint in new[] { "process-payment", "process-voucher" })
{
    foreach (var (name, data) in invalid)
        await RunAsync(endpoint, name, JsonSerializer.Serialize(data), false);
    await RunAsync(endpoint, "malformed extraction", "not json", false);
    foreach (var provider in new[] { "Yape", "Plin", "BCP", "Banco", "Transferencia" })
        await RunAsync(endpoint, provider, JsonSerializer.Serialize(Receipt(provider)), true);
}
Console.WriteLine("Payment regression checks passed using isolated in-memory data and simulated OCR.");

async Task RunAsync(string endpoint, string name, string content, bool accepted, string? liveImage = null)
{
    var folder = Path.Combine(Path.GetTempPath(), "oldschool-payment-checks", Guid.NewGuid().ToString("N"));
    var config = new ConfigurationManager();
    config["OpenAI:ApiKey"] = liveKey ?? "test-only";
    config["N8n:ApiKey"] = "test-only";
    config["App:TimeZoneId"] = "America/Lima";
    config["Storage:PaymentProofsPath"] = folder;
    await using var db = new ApiDbContext(new DbContextOptionsBuilder<ApiDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    db.Statuses.AddRange(
        new StatusCatalog { Id = 1, CompanyId = 1, Name = "Prospecto" },
        new StatusCatalog { Id = 2, CompanyId = 1, Name = "Por Pagar" });
    var oldRecord = new CustomerRecord { Id = 1, CompanyId = 1, StatusCatalogId = 1,
        Cellphone = "900000001", CreatedAt = new DateTime(2026, 1, 1), ProductAmount = 189, BalanceDue = 189 };
    var current = new CustomerRecord { Id = 2, CompanyId = 1, StatusCatalogId = 1,
        Cellphone = "900000001", CreatedAt = new DateTime(2026, 11, 1), ProductAmount = 189, BalanceDue = 189 };
    db.CustomerRecords.AddRange(oldRecord, current);
    await db.SaveChangesAsync();
    using var storage = new OldSchool.Storage.RustFsPaymentProofs(config,
        NullLogger<OldSchool.Storage.RustFsPaymentProofs>.Instance);
    IHttpClientFactory factory = liveImage is null ? new OcrFactory(content) : new LiveFactory();
    var imageBase64 = liveImage is null ? "AA==" : Convert.ToBase64String(await File.ReadAllBytesAsync(liveImage));
    var context = new DefaultHttpContext();
    context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("company_id", "1")], "test"));
    context.Request.Headers["X-Api-Key"] = "test-only";
    IActionResult result;
    if (endpoint == "process-payment")
    {
        var controller = new RecordsController(db, config, factory, storage)
        { ControllerContext = new ControllerContext { HttpContext = context } };
        result = await controller.ProcessPaymentFromImage(new() { Celular = "900000001", ImageBase64 = imageBase64 });
    }
    else
    {
        var controller = new PaymentsController(db, config, factory, new TestEnvironment(),
            NullLogger<PaymentsController>.Instance, storage)
        { ControllerContext = new ControllerContext { HttpContext = context } };
        result = await controller.ProcessVoucher(new() { Celular = "900000001", ImageBase64 = imageBase64, ImageExtension = "png" });
    }
    if (accepted)
    {
        Check(result is OkObjectResult && await db.CustomerRecordPayments.CountAsync() == 1
            && current.PaidAmount == 30 && current.BalanceDue == 159 && current.StatusCatalogId == 2,
            $"{endpoint} accepts {name}, updates latest order and balance");
        var payment = await db.CustomerRecordPayments.SingleAsync();
        Check(payment.OperationNumber == "123456789" && payment.PaymentDate == new DateTime(2026, 11, 8, 22, 58, 0)
            && oldRecord.PaidAmount == 0, $"{endpoint} preserves operation, date and older order");
    }
    else
    {
        Check(result is ObjectResult { StatusCode: 400 or 422 }
            && !await db.CustomerRecordPayments.AnyAsync() && current.PaidAmount == 0
            && current.BalanceDue == 189 && current.StatusCatalogId == 1 && !Directory.Exists(folder),
            $"{endpoint} rejects {name} before payment, status or proof writes");
    }
}

sealed class OcrFactory(string content) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(new OcrHandler(content));
}
sealed class LiveFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new();
}
sealed class OcrHandler(string content) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                choices = new[] { new { message = new { content } } }
            }), Encoding.UTF8, "application/json")
        });
}
sealed class TestEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Test";
    public string ApplicationName { get; set; } = "PaymentChecks";
    public string WebRootPath { get; set; } = Path.GetTempPath();
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
