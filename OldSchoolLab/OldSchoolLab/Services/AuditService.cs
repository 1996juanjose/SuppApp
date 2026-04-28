using OldSchoolLab.Data;
using OldSchoolLab.Models;
using System.Text.Json;

namespace OldSchoolLab.Services;

public interface IAuditService
{
    Task LogAsync(string tableName, int recordId, string action, string userId, string userName, object? details = null);
}

public class AuditService(ApplicationDbContext db) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public async Task LogAsync(string tableName, int recordId, string action, string userId, string userName, object? details = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
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
