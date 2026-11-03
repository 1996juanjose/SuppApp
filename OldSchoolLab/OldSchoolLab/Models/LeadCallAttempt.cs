using System.ComponentModel.DataAnnotations;
namespace OldSchoolLab.Models;
public class LeadCallAttempt
{
    public Guid Id { get; set; }
    public int CompanyId { get; set; }
    public int RecordId { get; set; }
    public DateTime CreatedAt { get; set; }
    [MaxLength(40)] public string Outcome { get; set; } = "";
    [MaxLength(250)] public string Notes { get; set; } = "";
    [MaxLength(450)] public string CalledByUserId { get; set; } = "";
    [MaxLength(120)] public string CalledByUserName { get; set; } = "";
    public DateTime? NextCallAt { get; set; }
}
