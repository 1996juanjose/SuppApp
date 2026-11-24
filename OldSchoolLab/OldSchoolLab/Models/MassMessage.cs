using System.ComponentModel.DataAnnotations;

namespace OldSchoolLab.Models;

public class MassMessageCampaign
{
    public Guid Id { get; set; }
    public int CompanyId { get; set; }
    [MaxLength(450)] public string UserId { get; set; } = "";
    [MaxLength(120)] public string UserName { get; set; } = "";
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(4000)] public string Message { get; set; } = "";
    public int IntervalSeconds { get; set; } = 30;
    [MaxLength(20)] public string Status { get; set; } = "Draft";
    [MaxLength(600)] public string SenderKey { get; set; } = "";
    [MaxLength(250)] public string Notice { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class MassMessageRecipient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public int Position { get; set; }
    public int? RecordId { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(15)] public string Phone { get; set; } = "";
    [MaxLength(20)] public string Status { get; set; } = "Pending";
    [MaxLength(150)] public string MessageId { get; set; } = "";
    [MaxLength(250)] public string Detail { get; set; } = "";
    public DateTime? AttemptedAt { get; set; }
}

public class MassMessageSender
{
    [Key, MaxLength(600)] public string Key { get; set; } = "";
    public DateTime NextSendAt { get; set; }
}
