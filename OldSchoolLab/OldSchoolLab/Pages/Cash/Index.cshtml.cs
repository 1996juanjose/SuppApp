using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using OldSchoolLab.Services;

namespace OldSchoolLab.Pages.Cash;

public class IndexModel(ApplicationDbContext db, CashRegisterService cash) : PageModel
{
    public CashRegister? Current { get; private set; }
    public CashRegister? Selected { get; private set; }
    public CashTotals Totals { get; private set; } = new(0, 0, 0, 0);
    public List<CashRegister> History { get; private set; } = [];
    public List<Expense> Expenses { get; private set; } = [];
    public bool CanManage => CashRegisterService.CanManage(User);
    public bool CanAddExpense => CashRegisterService.CanAddExpense(User);
    [BindProperty] public ExpenseInput Input { get; set; } = new();
    public class ExpenseInput
    {
        public int BoxId { get; set; }
        [Required, StringLength(120)] public string Name { get; set; } = "";
        [Range(typeof(decimal), "0.01", "99999999.99")] public decimal Amount { get; set; }
        [StringLength(250)] public string? Notes { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        var companyId = User.GetCompanyId();
        if (!companyId.HasValue) return Forbid();
        Current = await cash.GetOpenAsync(companyId.Value);
        History = await db.CashRegisters.AsNoTracking().Where(x => x.CompanyId == companyId.Value)
            .OrderByDescending(x => x.OpenedAt).ThenByDescending(x => x.Id).Take(30).ToListAsync();
        Selected = id.HasValue ? await db.CashRegisters.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId.Value)
            : Current ?? History.FirstOrDefault();
        if (id.HasValue && Selected is null) return NotFound();
        if (Selected is not null)
        {
            Totals = await cash.GetTotalsAsync(Selected);
            var end = Selected.ClosedAt ?? cash.Now;
            Expenses = await db.Expenses.AsNoTracking().Where(x => x.CompanyId == companyId.Value && x.CreatedAt >= Selected.OpenedAt && x.CreatedAt < end)
                .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostOpenAsync()
    {
        if (!CanManage || !User.GetCompanyId().HasValue) return Forbid();
        try { var box = await cash.OpenAsync(User.GetCompanyId()!.Value, User); TempData["CashMessage"] = "Caja abierta correctamente."; return RedirectToPage(new { id = box.Id }); }
        catch (InvalidOperationException ex) { TempData["CashError"] = ex.Message; return RedirectToPage(); }
    }

    public async Task<IActionResult> OnPostCloseAsync(int id)
    {
        if (!CanManage || !User.GetCompanyId().HasValue) return Forbid();
        try { await cash.CloseAsync(User.GetCompanyId()!.Value, id, User); TempData["CashMessage"] = "Caja cerrada correctamente."; }
        catch (InvalidOperationException ex) { TempData["CashError"] = ex.Message; }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostExpenseAsync()
    {
        if (!CanAddExpense || !User.GetCompanyId().HasValue) return Forbid();
        if (!ModelState.IsValid)
        {
            var result = await OnGetAsync(Input.BoxId);
            return result;
        }
        try { await cash.AddExpenseAsync(User.GetCompanyId()!.Value, Input.BoxId, Input.Name, Input.Amount, Input.Notes, User); TempData["CashMessage"] = "Gasto registrado."; }
        catch (InvalidOperationException ex) { TempData["CashError"] = ex.Message; }
        return RedirectToPage(new { id = Input.BoxId });
    }
}
