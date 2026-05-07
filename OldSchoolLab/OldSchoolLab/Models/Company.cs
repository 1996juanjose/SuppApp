using System.ComponentModel.DataAnnotations;

namespace OldSchoolLab.Models;

public class Company
{
    public int Id { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string LogoPath { get; set; } = string.Empty;

    [MaxLength(120)]
    public string LogoFileName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<StatusCatalog> Statuses { get; set; } = new List<StatusCatalog>();
    public ICollection<CustomerRecord> Records { get; set; } = new List<CustomerRecord>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
