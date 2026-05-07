using System.ComponentModel.DataAnnotations;

namespace OldSchoolLab.Models;

public class StatusCatalog
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string BadgeClass { get; set; } = "secondary";

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public ICollection<CustomerRecord> CustomerRecords { get; set; } = new List<CustomerRecord>();
}
