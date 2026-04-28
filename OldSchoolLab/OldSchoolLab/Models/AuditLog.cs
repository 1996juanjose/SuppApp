using System.ComponentModel.DataAnnotations;

namespace OldSchoolLab.Models;

public class AuditLog
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string TableName { get; set; } = string.Empty;

    public int RecordId { get; set; }

    [MaxLength(20)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(450)]
    public string ChangedByUserId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ChangedByUserName { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; } = DateTime.Now;

    public string? Details { get; set; }
}
