using System.ComponentModel.DataAnnotations;

namespace OldSchoolLab.Models;

public class ProductPrice
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public Product Product { get; set; } = default!;

    [Range(1, 999)]
    public int Quantity { get; set; }

    public decimal Price { get; set; }
}
