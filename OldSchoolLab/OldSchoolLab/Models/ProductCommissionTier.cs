using System.ComponentModel.DataAnnotations;

namespace OldSchoolLab.Models;

public class ProductCommissionTier
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product Product { get; set; } = default!;

    [Range(1, 999)]
    public int Quantity { get; set; }

    [Range(0, 100)]
    public decimal CommissionRate { get; set; }
}
