namespace OldSchoolLab.Services;

public sealed class CallReminderViewModel
{
    public int Id { get; set; }
    public string Cellphone { get; set; } = string.Empty;
    public string NameOrReference { get; set; } = string.Empty;
    public string? CallActivity { get; set; }
    public DateTime? CallScheduledAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
