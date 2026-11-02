using System.ComponentModel.DataAnnotations;

namespace OldSchoolLab.Models;

public class CashRegister
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    [MaxLength(450)] public string OpenedByUserId { get; set; } = string.Empty;
    [MaxLength(120)] public string OpenedByUserName { get; set; } = string.Empty;
    [MaxLength(450)] public string ClosedByUserId { get; set; } = string.Empty;
    [MaxLength(120)] public string ClosedByUserName { get; set; } = string.Empty;
}
