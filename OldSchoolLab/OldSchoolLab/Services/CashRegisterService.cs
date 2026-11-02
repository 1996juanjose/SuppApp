using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;

namespace OldSchoolLab.Services;

public sealed record CashTotals(decimal Income, decimal Reversals, decimal Expenses, int PaymentCount)
{
    public decimal Net => Income - Reversals - Expenses;
}

public class CashRegisterService(ApplicationDbContext db, IConfiguration config)
{
    public static bool CanManage(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        && string.Equals(user.Identity.Name, "ADMIN", StringComparison.Ordinal);
    public static bool CanAddExpense(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true && (CanManage(user) || user.IsInRole("Gerencia"));
    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById(config["App:TimeZoneId"] ?? "America/Lima"));

    private async Task LockAsync(int companyId)
    {
        if (db.Database.IsNpgsql())
        {
            var key = $"cash-register:{companyId}";
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))");
        }
    }

    public Task<CashRegister?> GetOpenAsync(int companyId) => db.CashRegisters.AsNoTracking()
        .SingleOrDefaultAsync(x => x.CompanyId == companyId && x.ClosedAt == null);

    public async Task<CashRegister> OpenAsync(int companyId, ClaimsPrincipal user)
    {
        if (!CanManage(user)) throw new UnauthorizedAccessException("Solo ADMIN puede abrir la caja.");
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await LockAsync(companyId);
        if (!await db.Companies.AnyAsync(x => x.Id == companyId && x.IsActive))
            throw new InvalidOperationException("Selecciona una empresa activa.");
        if (await db.CashRegisters.AnyAsync(x => x.CompanyId == companyId && x.ClosedAt == null))
            throw new InvalidOperationException("Ya hay una caja abierta en esta empresa.");
        var box = new CashRegister { CompanyId = companyId, OpenedAt = Now,
            OpenedByUserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", OpenedByUserName = user.Identity!.Name! };
        db.CashRegisters.Add(box);
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return box;
    }

    public async Task CloseAsync(int companyId, int id, ClaimsPrincipal user)
    {
        if (!CanManage(user)) throw new UnauthorizedAccessException("Solo ADMIN puede cerrar la caja.");
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await LockAsync(companyId);
        var box = await db.CashRegisters.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId);
        if (box is null || box.ClosedAt.HasValue) throw new InvalidOperationException("La caja no existe o ya esta cerrada.");
        box.ClosedAt = Now;
        box.ClosedByUserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        box.ClosedByUserName = user.Identity!.Name!;
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
    }

    public async Task AddExpenseAsync(int companyId, int boxId, string name, decimal amount, string? notes, ClaimsPrincipal user)
    {
        if (!CanAddExpense(user)) throw new UnauthorizedAccessException("No tienes permiso para registrar gastos.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 || amount <= 0 || amount > 99999999.99m
            || decimal.Round(amount, 2) != amount || (notes?.Length ?? 0) > 250)
            throw new InvalidOperationException("Ingresa un concepto y un gasto positivo con hasta dos decimales.");
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await LockAsync(companyId);
        var box = await db.CashRegisters.SingleOrDefaultAsync(x => x.Id == boxId && x.CompanyId == companyId && x.ClosedAt == null);
        if (box is null) throw new InvalidOperationException("Solo se pueden registrar gastos en la caja abierta.");
        var now = Now;
        db.Expenses.Add(new Expense { CompanyId = companyId, CashRegisterId = box.Id, Name = name.Trim(), Amount = amount,
            Notes = notes?.Trim() ?? "", CreatedAt = now, ExpenseDate = now.Date,
            CreatedByUserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", CreatedByUserName = user.Identity?.Name ?? "" });
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
    }

    public async Task<CashTotals> GetTotalsAsync(CashRegister box)
    {
        var end = box.ClosedAt ?? Now;
        // Count registration time, not the date printed on an old voucher.
        var payments = db.CustomerRecordPayments.AsNoTracking()
            .Where(x => x.CustomerRecord.CompanyId == box.CompanyId && x.CreatedAt >= box.OpenedAt && x.CreatedAt < end);
        var income = await payments.SumAsync(x => (decimal?)x.Amount) ?? 0m;
        var refunds = await db.CustomerRecordPayments.AsNoTracking()
            .Where(x => x.CustomerRecord.CompanyId == box.CompanyId && x.IsReversed && x.ReversedAt >= box.OpenedAt && x.ReversedAt < end)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;
        var expenses = await db.Expenses.AsNoTracking()
            .Where(x => x.CompanyId == box.CompanyId && x.CreatedAt >= box.OpenedAt && x.CreatedAt < end)
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;
        return new CashTotals(income, refunds, expenses, await payments.CountAsync());
    }
}
