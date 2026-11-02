using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;

namespace OldSchoolLab.Services;

public sealed record CashTotals(decimal Income, decimal Reversals, decimal Expenses, int PaymentCount)
{
    public decimal Net => Income - Expenses;
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

    private Task<T> InTransactionAsync<T>(Func<Task<T>> operation) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
            try
            {
                var result = await operation();
                if (transaction is not null) await transaction.CommitAsync();
                return result;
            }
            catch (Exception ex) when (ex is Npgsql.NpgsqlException or DbUpdateException or TimeoutException)
            {
                // Retry with fresh tracked state after a failed database operation.
                db.ChangeTracker.Clear();
                throw;
            }
        });

    public Task<CashRegister?> GetOpenAsync(int companyId) => db.CashRegisters.AsNoTracking()
        .SingleOrDefaultAsync(x => x.CompanyId == companyId && x.ClosedAt == null);

    public async Task<CashRegister> OpenAsync(int companyId, ClaimsPrincipal user)
    {
        if (!CanManage(user)) throw new UnauthorizedAccessException("Solo ADMIN puede abrir la caja.");
        var box = new CashRegister { CompanyId = companyId, OpenedAt = Now,
            OpenedByUserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", OpenedByUserName = user.Identity!.Name! };
        return await InTransactionAsync(async () =>
        {
            await LockAsync(companyId);
            // A lost commit acknowledgement must not open a second cash register.
            if (box.Id != 0 && await db.CashRegisters.AsNoTracking().AnyAsync(x => x.Id == box.Id)) return box;
            if (box.Id != 0) { db.Entry(box).State = EntityState.Detached; box.Id = 0; }
            if (!await db.Companies.AnyAsync(x => x.Id == companyId && x.IsActive))
                throw new InvalidOperationException("Selecciona una empresa activa.");
            if (await db.CashRegisters.AnyAsync(x => x.CompanyId == companyId && x.ClosedAt == null))
                throw new InvalidOperationException("Ya hay una caja abierta en esta empresa.");
            db.CashRegisters.Add(box);
            await db.SaveChangesAsync();
            return box;
        });
    }

    public async Task CloseAsync(int companyId, int id, ClaimsPrincipal user)
    {
        if (!CanManage(user)) throw new UnauthorizedAccessException("Solo ADMIN puede cerrar la caja.");
        var closedAt = Now;
        closedAt = new DateTime(closedAt.Ticks - closedAt.Ticks % 10, DateTimeKind.Unspecified);
        var actorId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        await InTransactionAsync(async () =>
        {
            await LockAsync(companyId);
            var box = await db.CashRegisters.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId);
            if (box?.ClosedAt == closedAt && box.ClosedByUserId == actorId) return true;
            if (box is null || box.ClosedAt.HasValue) throw new InvalidOperationException("La caja no existe o ya esta cerrada.");
            box.ClosedAt = closedAt;
            box.ClosedByUserId = actorId;
            box.ClosedByUserName = user.Identity!.Name!;
            await db.SaveChangesAsync();
            return true;
        });
    }

    public async Task AddExpenseAsync(int companyId, int boxId, string name, decimal amount, string? notes, ClaimsPrincipal user)
    {
        if (!CanAddExpense(user)) throw new UnauthorizedAccessException("No tienes permiso para registrar gastos.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 || amount <= 0 || amount > 99999999.99m
            || decimal.Round(amount, 2) != amount || (notes?.Length ?? 0) > 250)
            throw new InvalidOperationException("Ingresa un concepto y un gasto positivo con hasta dos decimales.");
        var now = Now;
        var expense = new Expense { CompanyId = companyId, CashRegisterId = boxId, Name = name.Trim(), Amount = amount,
            Notes = notes?.Trim() ?? "", CreatedAt = now, ExpenseDate = now.Date,
            CreatedByUserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", CreatedByUserName = user.Identity?.Name ?? "" };
        await InTransactionAsync(async () =>
        {
            await LockAsync(companyId);
            // Reuse a committed expense if the connection failed during commit acknowledgement.
            if (expense.Id != 0 && await db.Expenses.AsNoTracking().AnyAsync(x => x.Id == expense.Id)) return true;
            if (expense.Id != 0) { db.Entry(expense).State = EntityState.Detached; expense.Id = 0; }
            var box = await db.CashRegisters.SingleOrDefaultAsync(x => x.Id == boxId && x.CompanyId == companyId && x.ClosedAt == null);
            if (box is null) throw new InvalidOperationException("Solo se pueden registrar gastos en la caja abierta.");
            db.Expenses.Add(expense);
            await db.SaveChangesAsync();
            return true;
        });
    }

    public async Task<CashTotals> GetTotalsAsync(CashRegister box)
    {
        var end = box.ClosedAt ?? Now;
        // Count registration time, not the date printed on an old voucher.
        var payments = db.CustomerRecordPayments.AsNoTracking()
            .Where(x => x.CustomerRecord.CompanyId == box.CompanyId && !x.IsReversed && x.CreatedAt >= box.OpenedAt && x.CreatedAt < end);
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
