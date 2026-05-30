using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using OldSchoolLab.Services;
using System.Text;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace OldSchoolLab.Pages.Records;

[Authorize]
public class IndexModel(ApplicationDbContext db) : PageModel
{
    public IList<CustomerRecord> Records { get; private set; } = new List<CustomerRecord>();
    public IList<StatusCatalog> Statuses { get; private set; } = new List<StatusCatalog>();

    [BindProperty(SupportsGet = true)]
    public List<int> StatusIds { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    public decimal TotalPaidAmount { get; private set; }
    public decimal TotalBalanceDue { get; private set; }
    public int TotalFilteredRecords { get; private set; }

    public bool CanEdit => User.IsInRole("Gerencia") || User.IsInRole("Gestor") || User.IsInRole("Vendedor");
    public bool CanDelete => User.IsInRole("Gerencia");
    public bool CanViewAudit => User.IsInRole("Gerencia");

    public async Task OnGetAsync()
    {
        await LoadStatusesAsync();
        var today = DateTime.Today;
        var query = BuildFilteredRecordsQuery();

        if (!FromDate.HasValue && !ToDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt.Date == today);
        }

        Records = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        TotalFilteredRecords = Records.Count;
        TotalPaidAmount = Records.Sum(x => x.ActivePaidAmount);
        TotalBalanceDue = Records
            .Where(x => x.StatusCatalog.Name == "Clientes" || x.StatusCatalog.Name == "Por Pagar")
            .Sum(x => x.CalculatedBalanceDue);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!User.IsInRole("Gerencia"))
            return Forbid();

        var companyId = User.GetCompanyId();
        var record = await db.CustomerRecords
            .FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));

        if (record is null)
            return NotFound();

        db.CustomerRecords.Remove(record);
        await db.SaveChangesAsync();

        TempData["StatusMessage"] = $"Registro de {record.Cellphone} eliminado correctamente.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetExportFilteredAsync()
    {
        var records = await BuildFilteredRecordsQuery()
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        return BuildExcelResult(records,
            $"registros-filtrados-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            "Registros filtrados");
    }

    public async Task<IActionResult> OnGetExportProspectsAsync()
    {
        var companyId = User.GetCompanyId();

        var query = db.CustomerRecords
            .AsNoTracking()
            .Include(x => x.StatusCatalog)
            .Include(x => x.Product)
            .Include(x => x.Payments)
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .Where(x => x.StatusCatalog.Name == "Prospecto");

        if (FromDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= FromDate.Value.Date);
        }

        if (ToDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt < ToDate.Value.Date.AddDays(1));
        }

        var records = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        return BuildExcelResult(records,
            $"prospectos-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            "Prospectos por rango de fechas");
    }

    public async Task<IActionResult> OnGetAuditAsync(int id)
    {
        if (!User.IsInRole("Gerencia"))
            return Forbid();

        var logs = await db.AuditLogs
            .AsNoTracking()
            .Where(x => x.TableName == "Registro" && x.RecordId == id)
            .Where(x => !User.GetCompanyId().HasValue || x.CompanyId == User.GetCompanyId().Value)
            .OrderByDescending(x => x.ChangedAt)
            .ToListAsync();

        return new JsonResult(logs.Select(x => new
        {
            x.Action,
            x.ChangedByUserName,
            ChangedAt = x.ChangedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            x.Details
        }));
    }

    public async Task<IActionResult> OnGetPaymentsAsync(int id)
    {
        var payments = await db.CustomerRecordPayments
            .AsNoTracking()
            .Where(x => x.CustomerRecordId == id)
            .Where(x => !User.GetCompanyId().HasValue || x.CustomerRecord.CompanyId == User.GetCompanyId().Value)
            .OrderByDescending(x => x.PaymentDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync();

        return new JsonResult(payments.Select(x => new
        {
            x.Id,
            PaymentDate = x.PaymentDate.ToString("yyyy-MM-dd HH:mm:ss"),
            CreatedAt = x.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            amount = x.Amount,
            x.CreatedByUserName,
            x.ProofImagePath,
            x.OperationNumber,
            x.IsReversed,
            ReversedAt = x.ReversedAt?.ToString("yyyy-MM-dd HH:mm:ss"),
            x.ReversedByUserName
        }));
    }

    private async Task LoadStatusesAsync()
    {
        var companyId = User.GetCompanyId();

        Statuses = await db.Statuses
            .AsNoTracking()
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    private IQueryable<CustomerRecord> BuildFilteredRecordsQuery()
    {
        var companyId = User.GetCompanyId();

        var query = db.CustomerRecords
            .AsNoTracking()
            .Include(x => x.StatusCatalog)
            .Include(x => x.Product)
            .Include(x => x.Payments)
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .AsQueryable();

        if (StatusIds.Count > 0)
        {
            query = query.Where(x => StatusIds.Contains(x.StatusCatalogId));
        }

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim();
            query = query.Where(x =>
                x.Cellphone.Contains(term) ||
                x.NameOrReference.Contains(term) ||
                x.Dni.Contains(term) ||
                x.CallActivity.Contains(term));
        }

        if (FromDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= FromDate.Value.Date);
        }

        if (ToDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt < ToDate.Value.Date.AddDays(1));
        }

        return query;
    }

    private FileContentResult BuildExcelResult(IReadOnlyCollection<CustomerRecord> records, string fileName, string title)
    {
        var sb = new StringBuilder();

        sb.AppendLine(CsvRow("Estado", "Fecha", "Celular", "Nombre / Ref WA", "Actividad",
            "DNI", "Producto", "Cantidad", "Valor", "Pagado", "Debe",
            "Pagos activos", "\u00daltimo pago", "Ruta carpeta", "Usuario"));

        foreach (var r in records)
        {
            var lastPayment = r.Payments.Any(x => !x.IsReversed)
                ? r.Payments.Where(x => !x.IsReversed).Max(x => x.PaymentDate).ToString("yyyy-MM-dd")
                : string.Empty;

            sb.AppendLine(CsvRow(
                r.StatusCatalog.Name,
                r.RecordDate.ToString("yyyy-MM-dd"),
                r.Cellphone,
                r.NameOrReference,
                r.CallActivity,
                r.Dni,
                r.Product?.Name ?? string.Empty,
                r.Quantity.ToString(),
                r.ProductAmount.ToString("0.00"),
                r.ActivePaidAmount.ToString("0.00"),
                r.CalculatedBalanceDue.ToString("0.00"),
                r.Payments.Count(x => !x.IsReversed).ToString(),
                lastPayment,
                r.FolderPath,
                r.CreatedByUserName));
        }

        var csvFileName = System.IO.Path.ChangeExtension(fileName, ".csv");
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", csvFileName);
    }

    private static string CsvRow(params string[] fields)
    {
        return string.Join(",", fields.Select(f =>
        {
            var v = (f ?? string.Empty).Replace("\"", "\"\"");
            return v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? $"\"{ v}\"" : v;
        }));
    }
}

