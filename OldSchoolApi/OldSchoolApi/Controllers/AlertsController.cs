using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OldSchoolApi.Data;

namespace OldSchoolApi.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize]
public class AlertsController(ApiDbContext db) : ControllerBase
{
    [HttpGet("calls")]
    public async Task<IActionResult> GetCallAlerts([FromQuery] int? companyId, [FromQuery] int minutesAhead = 5, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var minWindow = now.AddDays(-21);
        var maxWindow = now.AddMinutes(Math.Max(1, minutesAhead));

        var query = db.CustomerRecords
            .AsNoTracking()
            .Where(x => !x.IsCallConcrete)
            .Where(x => x.CallScheduledAt.HasValue)
            .Where(x => x.CallScheduledAt >= minWindow && x.CallScheduledAt <= maxWindow)
            .AsQueryable();

        if (companyId.HasValue)
        {
            query = query.Where(x => x.CompanyId == companyId.Value);
        }

        var alerts = await query
            .OrderBy(x => x.CallScheduledAt)
            .Select(x => new
            {
                x.Id,
                x.CompanyId,
                x.Cellphone,
                x.NameOrReference,
                x.CallActivity,
                x.CallScheduledAt,
                IsDue = x.CallScheduledAt <= now
            })
            .ToListAsync(cancellationToken);

        var nextCall = await db.CustomerRecords
            .AsNoTracking()
            .Where(x => !x.IsCallConcrete)
            .Where(x => x.CallScheduledAt.HasValue)
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .Where(x => x.CallScheduledAt > now)
            .OrderBy(x => x.CallScheduledAt)
            .Select(x => x.CallScheduledAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            now,
            nextCallScheduledAt = nextCall,
            alerts
        });
    }
}