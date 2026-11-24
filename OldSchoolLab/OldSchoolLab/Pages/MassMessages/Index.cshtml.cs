using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Data;
using OldSchoolLab.Models;
using OldSchoolLab.Services;

namespace OldSchoolLab.Pages.MassMessages;

public class IndexModel(ApplicationDbContext db, IConfiguration config, WhatsAppCampaignService clock) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? CampaignId { get; set; }
    public List<CustomerRecord> Prospects { get; set; } = [];
    public List<MassMessageCampaign> Campaigns { get; set; } = [];
    public MassMessageCampaign? Campaign { get; set; }
    public List<MassMessageRecipient> Recipients { get; set; } = [];
    public bool Ready { get; set; }
    public int CompanyId => User.GetCompanyId()!.Value;
    private string Actor => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public class InputModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required, StringLength(120)] public string Name { get; set; } = "";
        [Required, StringLength(4000)] public string Message { get; set; } = "";
        [Range(1, 86400)] public int IntervalSeconds { get; set; } = 30;
        [StringLength(20000)] public string? Numbers { get; set; }
        public int[] RecordIds { get; set; } = [];
    }

    private IQueryable<MassMessageCampaign> OwnCampaigns => db.MassMessageCampaigns
        .Where(x => x.CompanyId == CompanyId && x.UserId == Actor);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!WhatsAppCampaignService.CanUse(User)) return Forbid();
        await LoadAsync(); return Page();
    }

    public async Task<IActionResult> OnPostFilterAsync()
    {
        if (!WhatsAppCampaignService.CanUse(User)) return Forbid();
        ModelState.Clear();
        await LoadAsync(); return Page();
    }

    private async Task LoadAsync()
    {
        Ready = EvolutionConnection.For(config, CompanyId).Ready;
        var query = db.CustomerRecords.AsNoTracking().Where(x => x.CompanyId == CompanyId && x.StatusCatalog.Name == "Prospecto");
        if (!string.IsNullOrWhiteSpace(Search)) query = query.Where(x => x.Cellphone.Contains(Search) || (x.NameOrReference != null && x.NameOrReference.Contains(Search)));
        if (FromDate.HasValue) query = query.Where(x => x.CreatedAt >= DateTime.SpecifyKind(FromDate.Value.Date, DateTimeKind.Unspecified));
        if (ToDate.HasValue)
        {
            var end = DateTime.SpecifyKind(ToDate.Value.Date.AddDays(1), DateTimeKind.Unspecified);
            query = query.Where(x => x.CreatedAt < end);
        }
        Prospects = await query.OrderByDescending(x => x.CreatedAt).Take(500).ToListAsync();
        Campaigns = await OwnCampaigns.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(30).ToListAsync();
        if (CampaignId.HasValue)
        {
            Campaign = await OwnCampaigns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == CampaignId);
            if (Campaign != null) Recipients = await db.MassMessageRecipients.AsNoTracking()
                .Where(x => x.CampaignId == Campaign.Id).OrderBy(x => x.Position).ToListAsync();
        }
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!WhatsAppCampaignService.CanUse(User)) return Forbid();
        var connection = EvolutionConnection.For(config, CompanyId);
        if (!connection.Ready) ModelState.AddModelError("", "Falta configurar Evolution para esta empresa en el servidor.");
        if (Input.Id == Guid.Empty) ModelState.AddModelError("", "Vuelve a abrir el módulo e inténtalo de nuevo.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        if (await OwnCampaigns.AnyAsync(x => x.Id == Input.Id)) return RedirectToPage(new { CampaignId = Input.Id });
        var ids = Input.RecordIds.Distinct().ToArray();
        if (ids.Length > 1000) { ModelState.AddModelError("", "Selecciona como máximo 1000 prospectos."); await LoadAsync(); return Page(); }
        var records = await db.CustomerRecords.AsNoTracking().Where(x => ids.Contains(x.Id)
            && x.CompanyId == CompanyId && x.StatusCatalog.Name == "Prospecto").OrderBy(x => x.Id).ToListAsync();
        if (records.Count != ids.Length) { ModelState.AddModelError("", "Algunos prospectos ya no están disponibles. Actualiza la selección."); await LoadAsync(); return Page(); }
        var candidates = records.Select(x => new MassMessageRecipient { RecordId = x.Id,
            Name = x.NameOrReference ?? "", Phone = WhatsAppCampaignService.Phone(x.Cellphone) }).ToList();
        candidates.AddRange((Input.Numbers ?? "").Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => new MassMessageRecipient { Phone = WhatsAppCampaignService.Phone(x) }));
        var contacts = await db.WhatsAppContacts.AsNoTracking().Where(x => x.CompanyId == CompanyId).ToListAsync();
        var blocked = contacts.Where(x => !MassMessageRules.Allowed(x, clock.Now)).Select(x => x.Phone).ToHashSet();
        var recipients = candidates.Where(x => WhatsAppCampaignService.ValidPhone(x.Phone) && !blocked.Contains(x.Phone))
            .DistinctBy(x => x.Phone).ToList();
        if (recipients.Count is 0 or > 1000)
        {
            ModelState.AddModelError("", "Debes incluir entre 1 y 1000 números válidos, únicos y habilitados para contacto.");
            await LoadAsync(); return Page();
        }
        var campaign = new MassMessageCampaign { Id = Input.Id, CompanyId = CompanyId, UserId = Actor,
            UserName = (User.Identity?.Name ?? "")[..Math.Min(User.Identity?.Name?.Length ?? 0, 120)],
            Name = Input.Name.Trim(), Message = Input.Message.Trim(), IntervalSeconds = Input.IntervalSeconds,
            SenderKey = connection.Key, Notice = $"{candidates.Count - recipients.Count} números excluidos: duplicados, inválidos o sin autorización vigente." };
        foreach (var (recipient, position) in recipients.Select((x, i) => (x, i)))
        { recipient.CampaignId = campaign.Id; recipient.Position = position; }
        db.MassMessageCampaigns.Add(campaign); db.MassMessageRecipients.AddRange(recipients);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            if (!await OwnCampaigns.AsNoTracking().AnyAsync(x => x.Id == Input.Id)) throw;
        }
        return RedirectToPage(new { CampaignId = campaign.Id });
    }

    public async Task<IActionResult> OnPostControlAsync(Guid id, string action)
    {
        if (!WhatsAppCampaignService.CanUse(User)) return Forbid();
        var campaign = await OwnCampaigns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (campaign == null) return NotFound();
        var next = action switch
        {
            "start" when campaign.Status is "Draft" or "Paused" => "Running",
            "pause" when campaign.Status == "Running" => "Paused",
            "cancel" when campaign.Status is "Draft" or "Paused" or "Running" => "Cancelled",
            _ => null
        };
        if (next == "Running")
        {
            var connection = EvolutionConnection.For(config, CompanyId);
            if (!connection.Ready || connection.Key != campaign.SenderKey) next = null;
        }
        if (next != null) await OwnCampaigns.Where(x => x.Id == id && x.Status == campaign.Status)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, next));
        return RedirectToPage(new { CampaignId = id });
    }
}
