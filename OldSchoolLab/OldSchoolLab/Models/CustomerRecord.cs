using System.ComponentModel.DataAnnotations;

namespace OldSchoolLab.Models;

public class CustomerRecord
{
    public int Id { get; set; }

    public int StatusCatalogId { get; set; }

    public StatusCatalog StatusCatalog { get; set; } = default!;

    [DataType(DataType.Date)]
    public DateTime RecordDate { get; set; } = DateTime.Today;

    [Required]
    [MaxLength(20)]
    public string Cellphone { get; set; } = string.Empty;

    [MaxLength(120)]
    public string NameOrReference { get; set; } = string.Empty;

    [MaxLength(300)]
    public string CallActivity { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Dni { get; set; } = string.Empty;

    public int? ProductId { get; set; }

    public Product? Product { get; set; }

    [Range(1, 999)]
    public int Quantity { get; set; } = 1;

    public decimal ProductAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal BalanceDue { get; set; }

    [MaxLength(250)]
    public string FolderPath { get; set; } = string.Empty;

    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string CreatedByUserName { get; set; } = string.Empty;
}
