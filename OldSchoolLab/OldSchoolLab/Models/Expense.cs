using System.ComponentModel.DataAnnotations;

namespace OldSchoolLab.Models;

public class Expense
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = default!;

    [DataType(DataType.Date)]
    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    [MaxLength(250)]
    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string CreatedByUserName { get; set; } = string.Empty;
}
