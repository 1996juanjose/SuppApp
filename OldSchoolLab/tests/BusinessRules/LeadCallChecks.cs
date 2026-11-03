extern alias Api;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OldSchoolDialer;
using Db = Api::OldSchoolApi.Data.ApiDbContext;
using Calls = Api::OldSchoolApi.Controllers.LeadCallsController;
using Attempt = Api::OldSchoolApi.Models.LeadCallAttempt;

static class LeadCallChecks
{
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static JsonElement Body(IActionResult result) => JsonSerializer.SerializeToElement(((ObjectResult)result).Value);
    public static async Task RunAsync()
    {
        var session = new DialingSession();
        session.Start(); session.CallStarted(); var key = session.PendingAttemptId; session.CallStarted();
        Check(key == session.PendingAttemptId && !session.ResultSaved(), "unlocked campaign never calls next automatically; retry keeps attempt id");
        session.AutoAdvance = true; session.CallStarted(); Check(session.ResultSaved(), "automatic lock advances only after saved result");
        session.CallStarted(); session.Pause(); Check(!session.ResultSaved(), "pause overrides automatic lock");
        session.Start(); session.CallStarted(); session.AutoAdvance = false; Check(!session.ResultSaved(), "unlock during call stops automatic advance");
        Check(!session.ResultSaved(), "duplicate save callback does not advance twice");
        session.Start(); session.CallStarted(); session.CancelAttempt(); Check(!session.AwaitingResult && !session.Running && !session.ResultSaved(), "cancelled phone launch is not counted and pauses the campaign");
        await using var db = new Db(new DbContextOptionsBuilder<Db>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var config = new ConfigurationManager(); config["App:TimeZoneId"] = "America/Lima";
        var now = Api::OldSchoolApi.Services.AppClock.Now(config);
        db.Statuses.AddRange(new Api::OldSchoolApi.Models.StatusCatalog { Id = 1, CompanyId = 1, Name = "Prospecto" },new Api::OldSchoolApi.Models.StatusCatalog { Id = 2, CompanyId = 2, Name = "Prospecto" });
        db.CustomerRecords.AddRange(new Api::OldSchoolApi.Models.CustomerRecord { Id = 1, CompanyId = 1, StatusCatalogId = 1, Cellphone = "953342737", CreatedAt = now },
            new Api::OldSchoolApi.Models.CustomerRecord { Id = 2, CompanyId = 1, StatusCatalogId = 1, Cellphone = "913717808", CreatedAt = now.AddDays(-40) },
            new Api::OldSchoolApi.Models.CustomerRecord { Id = 3, CompanyId = 2, StatusCatalogId = 2, Cellphone = "922837031", CreatedAt = now });
        await db.SaveChangesAsync();
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("company_id","1"), new Claim("user_id","operator-1"), new Claim(ClaimTypes.Name,"OPERADOR") },"test")) };
        var controller = new Calls(db, config) { ControllerContext = new ControllerContext { HttpContext = context } };
        var queue = Body(await controller.Queue(default, "uncalled"));
        Check(queue.GetArrayLength() == 2 && queue.EnumerateArray().All(x => x.GetProperty("Id").GetInt32() != 3), "dialer queue isolates authenticated company");
        var request = new Calls.ResultRequest { RequestId = Guid.NewGuid(), Outcome = "No contestó", Notes = "Prueba" };
        var saved = Body(await controller.Save(1, request, default));
        Check(saved.GetProperty("callCount").GetInt32() == 1 && (await db.LeadCallAttempts.SingleAsync()).CalledByUserId == "operator-1", "call result stores actor and first attempt");
        await controller.Save(1, request, default);
        Check(await db.LeadCallAttempts.CountAsync() == 1, "network retry with same request id is idempotent");
        Check(await controller.Save(3, new Calls.ResultRequest { Outcome = "Contestó" }, default) is NotFoundResult, "cannot update another company's lead");
        Check(await controller.Save(2, request, default) is ConflictObjectResult, "request id cannot be reused on another lead");
        queue = Body(await controller.Queue(default, "uncalled")); Check(queue.GetArrayLength() == 1 && queue[0].GetProperty("Id").GetInt32() == 2, "uncalled filter excludes saved attempts");
        queue = Body(await controller.Queue(default, "called", minAttempts: 1, maxAttempts: 1)); Check(queue.GetArrayLength() == 1, "called and attempt-count filters");
        await controller.Save(1, new Calls.ResultRequest { RequestId = Guid.NewGuid(), Outcome = "Volver a llamar", NextCallAt = now.AddDays(1) }, default);
        var record = await db.CustomerRecords.FindAsync(1);
        Check(await db.LeadCallAttempts.CountAsync() == 2 && record!.CallScheduledAt.HasValue && !record.IsCallConcrete, "second call schedules a pending callback");
        Check(Body(await controller.Queue(default, "callback")).GetArrayLength() == 0, "future callbacks are not called prematurely");
        record!.CallScheduledAt = now.AddMinutes(-1); await db.SaveChangesAsync();
        Check(Body(await controller.Queue(default, "callback")).GetArrayLength() == 1, "due callback enters queue");
        Check(Body(await controller.History(1, default)).GetArrayLength() == 2, "persistent per-lead call history");
        queue = Body(await controller.Queue(default, "all", fromDate: now.Date, take: 1));
        Check(queue.GetArrayLength() == 1 && queue[0].GetProperty("Id").GetInt32() == 1, "date filters and bounded queue batches");
        Check(Body(await controller.Queue(default, "all", afterId: 1)).GetArrayLength() == 1, "cursor pagination does not repeat a processed lead");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name,"NO_COMPANY") },"test"));
        Check(await controller.Queue(default) is ForbidResult, "missing company claim cannot see global leads");
    }
}
