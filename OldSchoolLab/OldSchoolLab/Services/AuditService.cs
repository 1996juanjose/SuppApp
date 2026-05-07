using Microsoft.AspNetCore.Http;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using System.Text.Json;

namespace OldSchoolLab.Services;

public interface IAuditService
{
    Task LogAsync(string tableName, int recordId, string action, string userId, string userName, object? details = null, int? companyId = null);
}

public class AuditService(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public async Task LogAsync(string tableName, int recordId, string action, string userId, string userName, object? details = null, int? companyId = null)
    {
        companyId ??= httpContextAccessor.HttpContext?.User.GetCompanyId();

        db.AuditLogs.Add(new AuditLog
        {
            CompanyId = companyId,
            TableName = tableName,
            RecordId = recordId,
            Action = action,
            ChangedByUserId = userId,
            ChangedByUserName = userName,
            ChangedAt = DateTime.Now,
            Details = details is not null ? JsonSerializer.Serialize(details, JsonOptions) : null
        });

        await db.SaveChangesAsync();
    }
}
