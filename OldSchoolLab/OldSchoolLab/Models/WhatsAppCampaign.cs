using System.ComponentModel.DataAnnotations;
namespace OldSchoolLab.Models;

public class WhatsAppContact
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(15)] public string Phone { get; set; } = "";
    [MaxLength(20)] public string ConsentStatus { get; set; } = "Unknown";
    public DateTime? ConsentExpiresAt { get; set; }
    public DateTime ChangedAt { get; set; }
    [MaxLength(500)] public string Evidence { get; set; } = "";
    [MaxLength(450)] public string ChangedByUserId { get; set; } = "";
    [MaxLength(120)] public string ChangedByUserName { get; set; } = "";
}
public class WhatsAppConsentChange
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int CompanyId { get; set; }
    public int RecordId { get; set; }
    [MaxLength(15)] public string Phone { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "Unknown";
    public DateTime? ExpiresAt { get; set; }
    public DateTime ChangedAt { get; set; }
    [MaxLength(500)] public string Evidence { get; set; } = "";
    [MaxLength(450)] public string ActorId { get; set; } = "";
    [MaxLength(120)] public string ActorName { get; set; } = "";
}
public class WhatsAppMessageSetting
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(450)] public string UserId { get; set; } = "";
    [MaxLength(2000)] public string Message { get; set; } = "";
}
public class WhatsAppCampaign
{
    public Guid Id { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(450)] public string UserId { get; set; } = "";
    [MaxLength(120)] public string UserName { get; set; } = "";
    [MaxLength(2000)] public string Message { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
}
public class WhatsAppCampaignRecipient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public WhatsAppCampaign Campaign { get; set; } = default!;
    public int CompanyId { get; set; }
    // Keep original client ID and snapshots even after the source record is deleted.
    public int RecordId { get; set; }
    public int Position { get; set; }
    [MaxLength(15)] public string Phone { get; set; } = "";
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(2000)] public string Message { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "Pending";
    public DateTime? OpenedAt { get; set; }
    public DateTime? DeclaredSentAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    [MaxLength(450)] public string ActorId { get; set; } = "";
    [MaxLength(120)] public string ActorName { get; set; } = "";
    [MaxLength(200)] public string Reason { get; set; } = "";
}
