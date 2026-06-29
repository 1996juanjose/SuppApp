using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OldSchoolApi.Data;

namespace OldSchoolApi.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize]
public class AuditController(ApiDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAudit([FromQuery] int? companyId, [FromQuery] string? tableName, [FromQuery] string? userName, CancellationToken cancellationToken)
    {
        var query = db.AuditLogs.AsNoTracking().AsQueryable();

        if (companyId.HasValue)
        {
            query = query.Where(x => x.CompanyId == companyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            query = query.Where(x => x.TableName == tableName);
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            query = query.Where(x => x.ChangedByUserName.Contains(userName));
        }

        var logs = await query
            .OrderByDescending(x => x.ChangedAt)
            .Take(300)
            .Select(x => new
            {
                x.Id,
                x.CompanyId,
                x.TableName,
                x.RecordId,
                x.Action,
                x.ChangedByUserId,
                x.ChangedByUserName,
                x.ChangedAt,
                x.Details
            })
            .ToListAsync(cancellationToken);

        return Ok(logs);
    }
}