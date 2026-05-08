using System.ComponentModel.DataAnnotations;

namespace OldSchoolLab.Models;

public class CustomerRecordPayment
{
    public int Id { get; set; }

    public int CustomerRecordId { get; set; }

    public CustomerRecord CustomerRecord { get; set; } = default!;

    [Range(0.01, 100000)]
    public decimal Amount { get; set; }

    [DataType(DataType.Date)]
    public DateTime PaymentDate { get; set; } = DateTime.Today;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

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
