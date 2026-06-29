namespace OldSchoolLab.Services;

public sealed class CollectionAlertViewModel
{
    public int Id { get; set; }
    public string Cellphone { get; set; } = string.Empty;
    public string NameOrReference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal BalanceDue { get; set; }
    public DateTime? LastPaymentAt { get; set; }
    public int DaysSinceLastPayment { get; set; }
    public string AlertType { get; set; } = string.Empty;
}