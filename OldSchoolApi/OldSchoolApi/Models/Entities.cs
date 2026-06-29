using System.ComponentModel.DataAnnotations;

namespace OldSchoolApi.Models;

public class CustomerRecord
{
    public int Id { get; set; }
    public int StatusCatalogId { get; set; }
    public StatusCatalog StatusCatalog { get; set; } = default!;
    public DateTime RecordDate { get; set; }
    public DateTime CreatedAt { get; set; }

    public DateTime? CallScheduledAt { get; set; }
    public bool IsCallConcrete { get; set; }

    [MaxLength(20)]
    public string Cellphone { get; set; } = string.Empty;

    [MaxLength(120)]
    public string NameOrReference { get; set; } = string.Empty;

    [MaxLength(300)]
    public string CallActivity { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Dni { get; set; } = string.Empty;

    public int? CompanyId { get; set; }
    public int? ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal ProductAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceDue { get; set; }

    [MaxLength(250)]
    public string FolderPath { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Destino { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Clave { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Guia { get; set; } = string.Empty;

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

    public int? CompanyId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<CustomerRecord> CustomerRecords { get; set; } = new List<CustomerRecord>();
}

public class Product
  {
    public int Id { get; set; }

    public int? CompanyId { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    public decimal PurchaseUnitCost { get; set; }

    public bool IsActive { get; set; } = true;
    public ICollection<ProductPrice> Prices { get; set; } = new List<ProductPrice>();

    public ICollection<ProductCommissionTier> CommissionTiers { get; set; } = new List<ProductCommissionTier>();

    public ICollection<ProductStockMovement> StockMovements { get; set; } = new List<ProductStockMovement>();
}
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

public class ProductPrice
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class CustomerRecordPayment
{
    public int Id { get; set; }
    public int CustomerRecordId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public DateTime CreatedAt { get; set; }

    [MaxLength(260)]
    public string ProofImagePath { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ProofFileName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string OperationNumber { get; set; } = string.Empty;

    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string CreatedByUserName { get; set; } = string.Empty;

    public bool IsReversed { get; set; }
    public DateTime? ReversedAt { get; set; }

    [MaxLength(450)]
    public string ReversedByUserId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ReversedByUserName { get; set; } = string.Empty;
}

public class AuditLog
{
    public int Id { get; set; }
    public int? CompanyId { get; set; }

    [MaxLength(50)]
    public string TableName { get; set; } = string.Empty;

    public int RecordId { get; set; }

    [MaxLength(20)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(450)]
    public string ChangedByUserId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ChangedByUserName { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; }

    public string? Details { get; set; }
}
