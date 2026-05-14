using System.ComponentModel.DataAnnotations;

namespace OldSchoolLab.Models;

public class ProductStockMovement
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product Product { get; set; } = default!;

    public int? CustomerRecordId { get; set; }

    public CustomerRecord? CustomerRecord { get; set; }

    [Range(1, 999999)]
    public int Quantity { get; set; }

    public decimal UnitCost { get; set; }

    [MaxLength(20)]
    public string MovementType { get; set; } = "Ingreso";

    [DataType(DataType.Date)]
    public DateTime MovementDate { get; set; } = DateTime.Today;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string CreatedByUserName { get; set; } = string.Empty;

    [MaxLength(250)]
    public string Notes { get; set; } = string.Empty;

    public decimal TotalCost => Quantity * UnitCost;
}
