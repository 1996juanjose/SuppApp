using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Services;

namespace OldSchoolLab.Pages.Admin.Addresses;

[Authorize(Roles = "Gerencia")]
public class IndexModel(ApplicationDbContext db) : PageModel
{
    public IList<AddressStatsRow> AddressStats { get; private set; } = new List<AddressStatsRow>();

    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    public int TotalRecords => AddressStats.Sum(x => x.Total);

    public async Task OnGetAsync()
    {
        var companyId = User.GetCompanyId();
        if (!companyId.HasValue)
        {
            AddressStats = new List<AddressStatsRow>();
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

        var normalizedStats = await db.CustomerRecords
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId.Value)
            .Where(x => x.RecordDate >= from && x.RecordDate <= to)
            .Where(x => !string.IsNullOrWhiteSpace(x.Destino))
            .Select(x => new
            {
                x.Id,
                x.Destino
            })
            .ToListAsync();

        AddressStats = normalizedStats
            .GroupBy(x => NormalizeAddress(x.Destino))
            .Select(group => new AddressStatsRow
            {
                Destination = group.OrderByDescending(x => x.Id).Select(x => x.Destino.Trim()).FirstOrDefault() ?? string.Empty,
                Total = group.Count()
            })
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.Destination)
            .ToList();
    }

    private static string NormalizeAddress(string value)
    {
        var normalized = value.Trim().ToUpperInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        var cleaned = builder.ToString().Normalize(System.Text.NormalizationForm.FormC);
        cleaned = cleaned.Replace('Ñ', 'N');
        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public sealed class AddressStatsRow
    {
        public string Destination { get; init; } = string.Empty;
        public int Total { get; init; }
    }
}