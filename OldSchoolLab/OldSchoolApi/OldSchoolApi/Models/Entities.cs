using System.ComponentModel.DataAnnotations;

namespace OldSchoolApi.Models;

public class CustomerRecord
{
    public int Id { get; set; }
    public int StatusCatalogId { get; set; }
    public DateTime RecordDate { get; set; }

    [MaxLength(20)]
    public string Cellphone { get; set; } = string.Empty;

    [MaxLength(120)]
    public string NameOrReference { get; set; } = string.Empty;

    [MaxLength(300)]
    public string CallActivity { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Dni { get; set; } = string.Empty;

    public int? ProductId { get; set; }
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

public class StatusCatalog
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class Product
{
    public int Id { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public ICollection<ProductPrice> Prices { get; set; } = new List<ProductPrice>();
}

public class ProductPrice
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
