using System.ComponentModel.DataAnnotations;

namespace OldSchoolLab.Models;

public class Product
{
    public int Id { get; set; }

    [Required]
    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<ProductPrice> Prices { get; set; } = new List<ProductPrice>();

    public ICollection<CustomerRecord> CustomerRecords { get; set; } = new List<CustomerRecord>();
}
