using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using OldSchoolLab.Services;

namespace OldSchoolLab.Pages.Admin.Profit;

[Authorize(Roles = "Gerencia")]
public class IndexModel(ApplicationDbContext db) : PageModel
{
    public IList<ProfitRecordRow> Records { get; private set; } = new List<ProfitRecordRow>();
    public IList<ExpenseRow> Expenses { get; private set; } = new List<ExpenseRow>();

    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    [BindProperty]
    [DataType(DataType.Date)]
    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [BindProperty]
    [Required]
    [MaxLength(120)]
    public string ExpenseName { get; set; } = string.Empty;

    [BindProperty]
    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal ExpenseAmount { get; set; }

    [BindProperty]
    [MaxLength(250)]
    public string? ExpenseNotes { get; set; }

    public decimal TotalPaidAmount => Records.Sum(x => x.PaidAmount);

    public decimal TotalCommissionAmount => Records.Sum(x => x.CommissionAmount);

    public decimal TotalProductCost => Records.Sum(x => x.ProductCost);

    public decimal GrossProfit => TotalPaidAmount - TotalCommissionAmount - TotalProductCost;

    public decimal TotalOtherExpenses => Expenses.Sum(x => x.Amount);

    public decimal NetProfit => GrossProfit - TotalOtherExpenses;

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAddExpenseAsync()
    {
        if (!User.GetCompanyId().HasValue)
        {
            return Forbid();
        }

        await LoadAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var companyId = User.GetCompanyId();
        var expense = new Expense
        {
            CompanyId = companyId!.Value,
            ExpenseDate = ExpenseDate.Date,
            Name = ExpenseName.Trim(),
            Amount = ExpenseAmount,
            Notes = ExpenseNotes?.Trim() ?? string.Empty,
            CreatedAt = DateTime.Now,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            CreatedByUserName = User.Identity?.Name ?? string.Empty
        };

        db.Expenses.Add(expense);
        await db.SaveChangesAsync();

        TempData["StatusMessage"] = "Gasto agregado correctamente.";
        return RedirectToPage(new
        {
            fromDate = FromDate?.ToString("yyyy-MM-dd"),
            toDate = ToDate?.ToString("yyyy-MM-dd")
        });
    }

    private async Task LoadAsync()
    {
        var companyId = User.GetCompanyId();
        if (!companyId.HasValue)
        {
            Records = new List<ProfitRecordRow>();
            Expenses = new List<ExpenseRow>();
            return;
        }

        var from = FromDate?.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var to = ToDate?.Date ?? DateTime.Today;

        if (to < from)
        {
            (from, to) = (to, from);
        }

        FromDate = from;
        ToDate = to;

        var records = await db.CustomerRecords
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Payments)
            .Where(x => x.CompanyId == companyId.Value)
            .Where(x => x.RecordDate >= from && x.RecordDate <= to)
            .OrderByDescending(x => x.RecordDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        Records = records.Select(record => new ProfitRecordRow
        {
            RecordId = record.Id,
            RecordDate = record.RecordDate,
            Cellphone = record.Cellphone,
            ProductName = record.Product?.Name ?? string.Empty,
            PaidAmount = record.ActivePaidAmount,
            CommissionAmount = record.CommissionAmount,
            ProductCost = record.CostAmount,
            Profit = record.ActivePaidAmount - record.CommissionAmount - record.CostAmount,
            BalanceDue = record.CalculatedBalanceDue
        }).ToList();

        Expenses = await db.Expenses
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value)
            .Where(x => x.ExpenseDate >= from && x.ExpenseDate <= to)
            .OrderByDescending(x => x.ExpenseDate)
            .ThenByDescending(x => x.Id)
            .Select(x => new ExpenseRow
            {
                ExpenseId = x.Id,
                ExpenseDate = x.ExpenseDate,
                Name = x.Name,
                Amount = x.Amount,
                Notes = x.Notes
            })
            .ToListAsync();
    }

    public sealed class ProfitRecordRow
    {
        public int RecordId { get; init; }

        public DateTime RecordDate { get; init; }

        public string Cellphone { get; init; } = string.Empty;

        public string ProductName { get; init; } = string.Empty;

        public decimal PaidAmount { get; init; }

        public decimal CommissionAmount { get; init; }

        public decimal ProductCost { get; init; }

        public decimal Profit { get; init; }

        public decimal BalanceDue { get; init; }
    }

    public sealed class ExpenseRow
    {
        public int ExpenseId { get; init; }

        public DateTime ExpenseDate { get; init; }

        public string Name { get; init; } = string.Empty;

        public decimal Amount { get; init; }

        public string Notes { get; init; } = string.Empty;
    }
}
