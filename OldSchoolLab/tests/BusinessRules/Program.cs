extern alias Web;
extern alias Api;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.FileProviders;
using Policy = Web::OldSchool.Sales.RepeatPurchasePolicy;
using Entry = Web::OldSchool.Sales.PurchaseHistoryEntry;
using WebDb = Web::OldSchoolLab.Data.ApplicationDbContext;
using Cash = Web::OldSchoolLab.Services.CashRegisterService;
using ApiDb = Api::OldSchoolApi.Data.ApiDbContext;
using Controller = Api::OldSchoolApi.Controllers.RecordsController;

if (args.Length == 2 && args[0] == "--postgres-isolated")
{
    await PostgreSqlCashRegression.RunAsync(args[1]);
    return;
}

static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
static ClaimsPrincipal User(string name, int company = 1, string role = "Gerencia") => new(new ClaimsIdentity(new[] {
    new Claim(ClaimTypes.Name, name), new Claim(ClaimTypes.NameIdentifier, name), new Claim("company_id", company.ToString()), new Claim(ClaimTypes.Role, role)
}, "test"));
static async Task Reject(Func<Task> action, string name) { try { await action(); } catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException) { Check(true, name); return; } throw new Exception("Expected rejection: " + name); }
foreach (var (input, expected) in new[] { ("image/jpeg", "jpg"), (" IMAGE/PNG ", "png"), ("image/webp", "webp"), (".JPG", "jpg"), ("jpeg", "jpg") })
{
    Check(Api::OldSchool.Storage.RustFsPaymentProofs.NormalizeExtension(input) == expected, $"API RustFS accepts {input}");
    Check(Web::OldSchool.Storage.RustFsPaymentProofs.NormalizeExtension(input) == expected, $"Web RustFS accepts {input}");
}
foreach (var input in new string?[] { "image/gif", "application/pdf", "", null })
{
    await Reject(() => { Api::OldSchool.Storage.RustFsPaymentProofs.NormalizeExtension(input); return Task.CompletedTask; }, $"API RustFS rejects {input ?? "null"}");
    await Reject(() => { Web::OldSchool.Storage.RustFsPaymentProofs.NormalizeExtension(input); return Task.CompletedTask; }, $"Web RustFS rejects {input ?? "null"}");
}
var day = new DateTime(2026, 1, 1, 12, 0, 0);
Check(!Policy.CanCreateNext(day, day.AddDays(30)) && Policy.CanCreateNext(day, day.AddDays(30).AddTicks(1)), "strict 30-day boundary");
var numbers = Policy.GetPurchaseNumbers(new[] { new Entry(1,1,"953 342 737",day),new Entry(2,1,"953342737",day.AddDays(2)),new Entry(3,1,"953342737",day.AddDays(34)),new Entry(4,1,"953342737",day.AddDays(70)),new Entry(5,2,"953342737",day.AddDays(70)) });
Check(numbers[1] == 1 && numbers[2] == 1 && numbers[3] == 2 && numbers[4] == 3 && numbers[5] == 1, "purchase numbering, normalized phones and company isolation");
var config = new ConfigurationManager(); config["App:TimeZoneId"]="America/Lima"; config["OpenAI:ApiKey"]="fake-test-key";config["N8n:ApiKey"]="test";
using (var sqlDb = new WebDb(new DbContextOptionsBuilder<WebDb>().UseNpgsql("Host=unused;Database=unused").Options))
{
    var schema=sqlDb.Database.GenerateCreateScript();
    Check(schema.Contains("CREATE TABLE \"CashRegisters\"") && schema.Contains("WHERE \"ClosedAt\" IS NULL") && schema.Contains("\"CashRegisterId\""),"PostgreSQL schema supports cash periods and expense relation");
}
using (var sqlDb = new ApiDb(new DbContextOptionsBuilder<ApiDb>().UseNpgsql("Host=unused;Database=unused").Options))
{
    var sql=sqlDb.CustomerRecords.Select(x=>new { x.Id, purchaseNumber=numbers.GetValueOrDefault(x.Id,1) }).ToQueryString();
    Check(sql.Contains("SELECT"),"purchase-number projection translates for PostgreSQL without connecting");
}
await using (var db = new WebDb(new DbContextOptionsBuilder<WebDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options))
{
    db.Companies.AddRange(new Web::OldSchoolLab.Models.Company { Id=1, Name="Test" },new Web::OldSchoolLab.Models.Company { Id=2, Name="Other" }); await db.SaveChangesAsync();
    var cash = new Cash(db, config);
    await Reject(async () => { await cash.OpenAsync(1,User("gerencia")); }, "role Gerencia cannot open cash");
    await Reject(async () => { await cash.OpenAsync(1,User("admin")); }, "ADMIN name is case-sensitive");
    var box = await cash.OpenAsync(1,User("ADMIN")); box.OpenedAt=cash.Now.AddHours(-1);await db.SaveChangesAsync();
    await Reject(async () => { await cash.OpenAsync(1,User("ADMIN")); }, "one open cash per company");
    var record = new Web::OldSchoolLab.Models.CustomerRecord { Id=1, CompanyId=1, Cellphone="TEST" };
    var other = new Web::OldSchoolLab.Models.CustomerRecord { Id=2, CompanyId=2, Cellphone="OTHER" };
    db.CustomerRecords.AddRange(record,other);
    db.CustomerRecordPayments.AddRange(
        new Web::OldSchoolLab.Models.CustomerRecordPayment { CustomerRecord=record,Amount=100,CreatedAt=box.OpenedAt.AddMinutes(10),PaymentDate=day },
        new Web::OldSchoolLab.Models.CustomerRecordPayment { CustomerRecord=record,Amount=50,CreatedAt=box.OpenedAt.AddDays(-1),IsReversed=true,ReversedAt=box.OpenedAt.AddMinutes(15) },
        new Web::OldSchoolLab.Models.CustomerRecordPayment { CustomerRecord=record,Amount=20,CreatedAt=box.OpenedAt.AddMinutes(20),IsReversed=true,ReversedAt=box.OpenedAt.AddMinutes(25) },
        new Web::OldSchoolLab.Models.CustomerRecordPayment { CustomerRecord=other,Amount=999,CreatedAt=box.OpenedAt.AddMinutes(10) });
    await db.SaveChangesAsync();
    await cash.AddExpenseAsync(1,box.Id,"Movilidad",10,"Prueba",User("ADMIN"));
    var totals=await cash.GetTotalsAsync(box);
    Check(totals.Income==120 && totals.Reversals==70 && totals.Expenses==10 && totals.Net==40 && totals.PaymentCount==2,"cash income/refunds/expenses, registration date and company isolation");
    Check((await db.Expenses.SingleAsync()).CreatedByUserName=="ADMIN" && box.OpenedByUserName=="ADMIN","cash and expense author recorded");
    await Reject(() => cash.AddExpenseAsync(1,box.Id,"Bad",-1,null,User("ADMIN")),"negative expenses rejected");
    await Reject(() => cash.CloseAsync(1,box.Id,User("gerencia")),"non-ADMIN cannot close cash");
    await cash.CloseAsync(1,box.Id,User("ADMIN"));
    Check(box.ClosedAt.HasValue && box.ClosedByUserName=="ADMIN","close timestamp and actor recorded");
    db.CustomerRecordPayments.Add(new Web::OldSchoolLab.Models.CustomerRecordPayment { CustomerRecord=record,Amount=500,CreatedAt=box.ClosedAt!.Value }); await db.SaveChangesAsync();
    Check((await cash.GetTotalsAsync(box)).Net==40,"closed cash excludes movements at/after closing boundary");
    await Reject(() => cash.AddExpenseAsync(1,box.Id,"Late",10,null,User("ADMIN")),"closed cash rejects expenses");
    await Reject(() => cash.CloseAsync(1,box.Id,User("ADMIN")),"duplicate close rejected");
    Check((await cash.OpenAsync(1,User("ADMIN"))).Id!=box.Id,"new cash can open after closing");
}
await using (var db = new ApiDb(new DbContextOptionsBuilder<ApiDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options))
{
    var now=Api::OldSchoolApi.Services.AppClock.Now(config);
    db.Statuses.AddRange(new Api::OldSchoolApi.Models.StatusCatalog { Id=1,CompanyId=1,Name="Prospecto" },new Api::OldSchoolApi.Models.StatusCatalog { Id=2,CompanyId=1,Name="Por Pagar" },new Api::OldSchoolApi.Models.StatusCatalog { Id=3,CompanyId=2,Name="Prospecto" });
    db.CustomerRecords.Add(new Api::OldSchoolApi.Models.CustomerRecord { Id=1,CompanyId=1,StatusCatalogId=1,Cellphone="953 342 737",CreatedAt=now.AddDays(-31),RecordDate=day,NameOrReference="Old",Guia="OLD" }); await db.SaveChangesAsync();
    using var storage=new Api::OldSchool.Storage.RustFsPaymentProofs(config,NullLogger<Api::OldSchool.Storage.RustFsPaymentProofs>.Instance);
    var factory=new FakeFactory();
    var controller=new Controller(db,config,factory,storage) { ControllerContext=new ControllerContext { HttpContext=new DefaultHttpContext { User=User("ADMIN") } } };
    controller.Request.Headers["X-Api-Key"]="test";
    var created=(ObjectResult)await controller.Create(new Controller.CreateRecordRequest { Celular="953342737",CompanyId=1,Pagado=12 });
    var response=JsonSerializer.SerializeToElement(created.Value);
    Check(created.StatusCode==201 && response.GetProperty("purchaseNumber").GetInt32()==2,"API creates second order after 30 days");
    var newId=response.GetProperty("id").GetInt32();
    Check((await db.CustomerRecordPayments.SingleAsync()).Amount==12,"API initial advance creates payment ledger entry for cash totals");
    var skipped=(ObjectResult)await controller.Create(new Controller.CreateRecordRequest { Celular="953342737",CompanyId=1 });
    Check(JsonSerializer.SerializeToElement(skipped.Value).GetProperty("skipped").GetBoolean() && await db.CustomerRecords.CountAsync()==2,"repeat webhook within window does not duplicate");
    factory.Content="{\"amount\":5,\"date\":\"2026-11-02\",\"paymentType\":\"Yape\",\"valid\":true}";
    var paid=(ObjectResult)await controller.ProcessPaymentFromImage(new Controller.ProcessPaymentRequest { Celular="953342737",CompanyId=1,ImageBase64="AQID" });
    Check(paid.StatusCode==200 && (await db.CustomerRecordPayments.OrderByDescending(x=>x.Id).FirstAsync()).CustomerRecordId==newId,"payment API routes to most recent order");
    factory.Content="{\"valid\":true,\"orderNumber\":\"NEW-ORDER\",\"code\":\"NEW-GUIDE\",\"destination\":\"TEST\",\"recipientName\":\"Test\",\"dni\":\"\"}";
    var folder=Path.Combine(Path.GetTempPath(),"oldschool-shipment-test-"+Guid.NewGuid().ToString("N"));config["Storage:ShipmentProofsPath"]=folder;
    try
    {
        var shipped=(ObjectResult)await controller.ProcessShipmentFromImage(new Controller.ProcessShipmentRequest { Celular="953342737",CompanyId=1,ImageBase64="AQID",ImageExtension="jpg" });
        Check(shipped.StatusCode==200 && (await db.CustomerRecords.FindAsync(newId))!.Guia=="NEW-GUIDE" && (await db.CustomerRecords.FindAsync(1))!.Guia=="OLD","shipment API updates latest order and preserves history");
    }
    finally { if(Directory.Exists(folder)) foreach(var file in Directory.GetFiles(folder)) File.Delete(file); if(Directory.Exists(folder)) Directory.Delete(folder); }
    var listed=(ObjectResult)await controller.GetRecords(null,now.ToString("yyyy-MM-dd"),null,1,[],default);
    Check(JsonSerializer.SerializeToElement(listed.Value).EnumerateArray().Single().GetProperty("purchaseNumber").GetInt32()==2,"API filters preserve historical purchase number");
    var voucherFolder=Path.Combine(Path.GetTempPath(),"oldschool-voucher-test-"+Guid.NewGuid().ToString("N"));
    config["Storage:PaymentProofsPath"]=voucherFolder;
    factory.Content="{\"monto\":7,\"tipo\":\"Yape\",\"nro_operacion\":\"987654321\",\"fecha_hora\":\"2026-11-02 12:20:00\"}";
    try
    {
        var vouchers=new Api::OldSchoolApi.Controllers.PaymentsController(db,config,factory,new TestEnvironment(),NullLogger<Api::OldSchoolApi.Controllers.PaymentsController>.Instance,storage)
            { ControllerContext=new ControllerContext { HttpContext=new DefaultHttpContext { User=User("ADMIN") } } };
        var voucher=(ObjectResult)await vouchers.ProcessVoucher(new Api::OldSchoolApi.Controllers.PaymentsController.ProcessVoucherRequest { Celular="953342737",CompanyId=1,ImageBase64="AQID",ImageExtension="jpg" });
        Check(voucher.StatusCode==200 && (await db.CustomerRecordPayments.SingleAsync(x=>x.OperationNumber=="987654321")).CustomerRecordId==newId,"voucher API also routes to newest order");
    }
    finally { if(Directory.Exists(voucherFolder)) foreach(var file in Directory.GetFiles(voucherFolder)) File.Delete(file); if(Directory.Exists(voucherFolder)) Directory.Delete(voucherFolder); }
    controller.HttpContext.User=User("ADMIN",2);
    var otherCompany=(ObjectResult)await controller.Create(new Controller.CreateRecordRequest { Celular="953342737",CompanyId=2 });
    Check(otherCompany.StatusCode==201 && JsonSerializer.SerializeToElement(otherCompany.Value).GetProperty("purchaseNumber").GetInt32()==1,"same phone in another company starts its own history");
}
Console.WriteLine("All business-rule checks passed. No production database or external OCR used.");

sealed class FakeFactory : IHttpClientFactory
{
    public string Content { get; set; }="{}";
    public HttpClient CreateClient(string name) => new(new FakeHandler(this));
    sealed class FakeHandler(FakeFactory owner) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent(JsonSerializer.Serialize(new { choices=new[] { new { message=new { content=owner.Content } } } }),Encoding.UTF8,"application/json") });
    }
}
sealed class TestEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; }="Test";
    public string ApplicationName { get; set; }="BusinessRules";
    public string WebRootPath { get; set; }=Path.GetTempPath();
    public string ContentRootPath { get; set; }=Path.GetTempPath();
    public IFileProvider WebRootFileProvider { get; set; }=new NullFileProvider();
    public IFileProvider ContentRootFileProvider { get; set; }=new NullFileProvider();
}
