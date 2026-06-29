using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    public IList<CallReminderViewModel> DueCallReminders { get; private set; } = new List<CallReminderViewModel>();
    public IList<CallReminderViewModel> UpcomingCallReminders { get; private set; } = new List<CallReminderViewModel>();
    public IList<CollectionAlertViewModel> CollectionAlerts { get; private set; } = new List<CollectionAlertViewModel>();
    public IList<CollectionAlertViewModel> ReversalAlerts { get; private set; } = new List<CollectionAlertViewModel>();
    public IList<CollectionAlertViewModel> EarlyCollectionAlerts { get; private set; } = new List<CollectionAlertViewModel>();
    public DateTime? NextCallScheduledAt { get; private set; }
    public int PrintDayCount { get; private set; }

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
        var printDaySessionKey = $"records-print-day:{User.GetCompanyId()?.ToString() ?? "global"}:{DateTime.Today:yyyyMMdd}";
        var printDayRaw = HttpContext.Session.GetString(printDaySessionKey);
        PrintDayCount = string.IsNullOrWhiteSpace(printDayRaw)
            ? 0
            : printDayRaw.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var id) ? id : 0).Count(x => x > 0);
        var today = DateTime.Today;
        var companyId = User.GetCompanyId();
        var query = BuildFilteredRecordsQuery();
        var now = DateTime.Now;

        NextCallScheduledAt = await db.CustomerRecords
            .AsNoTracking()
            .Where(x => !x.IsCallConcrete)
            .Where(x => x.CallScheduledAt.HasValue)
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .Where(x => x.CallScheduledAt > now)
            .OrderBy(x => x.CallScheduledAt)
            .Select(x => x.CallScheduledAt)
            .FirstOrDefaultAsync();

        DueCallReminders = await db.CustomerRecords
            .AsNoTracking()
            .Include(x => x.StatusCatalog)
            .Where(x => !x.IsCallConcrete)
            .Where(x => x.CallScheduledAt.HasValue)
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .Where(x => x.CallScheduledAt >= now.AddDays(-21) && x.CallScheduledAt <= now)
            .OrderBy(x => x.CallScheduledAt)
            .ThenBy(x => x.Id)
            .Select(x => new CallReminderViewModel
            {
                Id = x.Id,
                Cellphone = x.Cellphone,
                NameOrReference = x.NameOrReference,
                CallActivity = x.CallActivity,
                CallScheduledAt = x.CallScheduledAt,
                Status = x.StatusCatalog.Name
            })
            .Take(10)
            .ToListAsync();

        UpcomingCallReminders = await db.CustomerRecords
            .AsNoTracking()
            .Include(x => x.StatusCatalog)
            .Where(x => !x.IsCallConcrete)
            .Where(x => x.CallScheduledAt.HasValue)
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .Where(x => x.CallScheduledAt > now && x.CallScheduledAt <= now.AddMinutes(5))
            .OrderBy(x => x.CallScheduledAt)
            .ThenBy(x => x.Id)
            .Select(x => new CallReminderViewModel
            {
                Id = x.Id,
                Cellphone = x.Cellphone,
                NameOrReference = x.NameOrReference,
                CallActivity = x.CallActivity,
                CallScheduledAt = x.CallScheduledAt,
                Status = x.StatusCatalog.Name
            })
            .Take(10)
            .ToListAsync();

        var collectionBaseQuery = db.CustomerRecords
            .AsNoTracking()
            .Where(x => !companyId.HasValue || x.CompanyId == companyId.Value)
            .Where(x => x.PaidAmount > 0m)
            .Where(x => x.BalanceDue > 0m)
            .Where(x => x.StatusCatalog.Name == "Por Pagar" || x.StatusCatalog.Name == "Cliente" || x.StatusCatalog.Name == "Clientes");

        var collectionRecords = await collectionBaseQuery
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.Cellphone,
                x.NameOrReference,
                Status = x.StatusCatalog.Name,
                x.BalanceDue,
                LastPaymentAt = x.Payments.Where(p => !p.IsReversed).OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt).Select(p => (DateTime?)p.PaymentDate).FirstOrDefault()
            })
            .ToListAsync();

        foreach (var record in collectionRecords)
        {
            var daysSinceLastPayment = record.LastPaymentAt.HasValue
                ? (int)(today - record.LastPaymentAt.Value.Date).TotalDays
                : int.MaxValue;

            if (daysSinceLastPayment >= 7 && (record.Status is "Por Pagar" || record.Status is not "Cliente" and not "Clientes"))
            {
                CollectionAlerts = CollectionAlerts.Append(new CollectionAlertViewModel
                {
                    Id = record.Id,
                    Cellphone = record.Cellphone,
                    NameOrReference = record.NameOrReference,
                    Status = record.Status,
                    BalanceDue = record.BalanceDue,
                    LastPaymentAt = record.LastPaymentAt,
                    DaysSinceLastPayment = daysSinceLastPayment,
                    AlertType = "Cobranza fuerte / extorno"
                }).ToList();
            }
            else if (record.Status is "Por Pagar" && daysSinceLastPayment >= 3)
            {
                EarlyCollectionAlerts = EarlyCollectionAlerts.Append(new CollectionAlertViewModel
                {
                    Id = record.Id,
                    Cellphone = record.Cellphone,
                    NameOrReference = record.NameOrReference,
                    Status = record.Status,
                    BalanceDue = record.BalanceDue,
                    LastPaymentAt = record.LastPaymentAt,
                    DaysSinceLastPayment = daysSinceLastPayment,
                    AlertType = "Empezar a cobrar lo restante"
                }).ToList();
            }
        }

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

    public async Task<IActionResult> OnPostAddToPrintDayAsync(int id, string? returnUrl)
    {
        if (!CanEdit && !User.IsInRole("SuperAdmin"))
            return Forbid();

        var companyId = User.GetCompanyId();
        var record = await db.CustomerRecords
            .AsNoTracking()
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id && (!companyId.HasValue || x.CompanyId == companyId.Value));

        if (record is null)
            return NotFound();

        var printDaySessionKey = $"records-print-day:{User.GetCompanyId()?.ToString() ?? "global"}:{DateTime.Today:yyyyMMdd}";
        var raw = HttpContext.Session.GetString(printDaySessionKey);
        var ids = string.IsNullOrWhiteSpace(raw)
            ? new List<int>()
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x, out var value) ? value : 0)
                .Where(x => x > 0)
                .Distinct()
                .ToList();

        if (!ids.Contains(record.Id))
        {
            ids.Add(record.Id);
            HttpContext.Session.SetString(printDaySessionKey, string.Join(',', ids.Distinct()));
        }

        TempData["StatusMessage"] = "Ficha agregada al documento del día.";
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToPage();
    }

    public IActionResult OnPostClearPrintDay(string? returnUrl)
    {
        var printDaySessionKey = $"records-print-day:{User.GetCompanyId()?.ToString() ?? "global"}:{DateTime.Today:yyyyMMdd}";
        HttpContext.Session.Remove(printDaySessionKey);
        TempData["StatusMessage"] = "Documento del día eliminado.";
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

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

    private string PrintDaySessionKey => $"records-print-day:{User.GetCompanyId()?.ToString() ?? "global"}:{DateTime.Today:yyyyMMdd}";

    private List<int> GetPrintDayRecordIds()
    {
        var raw = HttpContext.Session.GetString(PrintDaySessionKey);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<int>();
        }

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
    }

    private void SavePrintDayRecordIds(List<int> ids)
    {
        HttpContext.Session.SetString(PrintDaySessionKey, string.Join(',', ids.Distinct()));
    }

    private void ClearPrintDayRecordIds()
    {
        HttpContext.Session.Remove(PrintDaySessionKey);
    }

    private IActionResult RedirectBack()
    {
        var referer = Request.Headers.Referer.ToString();
        if (!string.IsNullOrWhiteSpace(referer) && Url.IsLocalUrl(referer))
        {
            return Redirect(referer);
        }

        return RedirectToPage();
    }
}

