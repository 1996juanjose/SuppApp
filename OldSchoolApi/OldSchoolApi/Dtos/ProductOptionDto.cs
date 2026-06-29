namespace OldSchoolApi.Dtos
{
    public class RecordProductsOption
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal PurchaseUnitCost { get; set; }

        public List<RecordProductPriceOption> Prices { get; set; } = [];

        public List<RecordProductCommissionTierOption> CommissionTiers { get; set; } = [];

        public List<RecordProductStockMovementOption> StockMovements { get; set; } = [];
    }

    public class RecordProductPriceOption
    {
        public int Id { get; set; }

        public int Quantity { get; set; }

        public decimal Price { get; set; }
    }

    public class RecordProductCommissionTierOption
    {
        public int Id { get; set; }

        public int Quantity { get; set; }

        public decimal CommissionRate { get; set; }
    }

    public class RecordProductStockMovementOption
    {
        public int Id { get; set; }

        public int Quantity { get; set; }

        public decimal UnitCost { get; set; }

        public string MovementType { get; set; } = string.Empty;

        public DateTime MovementDate { get; set; }

        public decimal TotalCost { get; set; }
    }
}
