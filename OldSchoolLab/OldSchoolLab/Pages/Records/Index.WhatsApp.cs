using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OldSchoolLab.Models;
using OldSchoolLab.Services;

namespace OldSchoolLab.Pages.Records;
public partial class IndexModel
{
    [BindProperty(SupportsGet = true)] public string? WhatsAppFilter { get; set; }
    public bool CanWhatsApp => WhatsAppCampaignService.CanUse(User);
    public Dictionary<string, DateTime> WhatsAppLastSent { get; private set; } = new();
    public Dictionary<string, WhatsAppContact> WhatsAppConsents { get; private set; } = new();
    public DateTime WhatsAppNow => whatsapp.Now;
    private async Task LoadWhatsAppIndicatorsAsync()
    {
        var companyId = User.GetCompanyId();
        if (!companyId.HasValue) return;
        WhatsAppLastSent = await db.WhatsAppCampaignRecipients.AsNoTracking().Where(x => x.CompanyId == companyId && x.Status == "Sent" && x.DeclaredSentAt != null)
            .GroupBy(x => x.Phone).Select(x => new { Phone = x.Key, Date = x.Max(r => r.DeclaredSentAt)!.Value }).ToDictionaryAsync(x => x.Phone, x => x.Date);
        WhatsAppConsents = await db.WhatsAppContacts.AsNoTracking().Where(x => x.CompanyId == companyId).ToDictionaryAsync(x => x.Phone);
    }
    public sealed class WhatsAppRequest
    {
        public Guid CampaignId { get; set; }
        public Guid? RecipientId { get; set; }
        public int[] Ids { get; set; } = [];
        public string Message { get; set; } = "";
        public string Action { get; set; } = "";
        public bool ExcludePrevious { get; set; }
    }
    private async Task<IActionResult> WhatsAppResult(Func<Task<object>> operation)
    {
        if (!CanWhatsApp) return new JsonResult(new { error = "Selecciona una empresa e inicia sesión con permiso de edición." }) { StatusCode = 403 };
        try { return new JsonResult(await operation()); }
        catch (UnauthorizedAccessException) { return new JsonResult(new { error = "No tienes acceso a este registro o campaña." }) { StatusCode = 403 }; }
        catch (InvalidOperationException ex) { return new JsonResult(new { error = ex.Message }) { StatusCode = 409 }; }
    }
    private Task<IActionResult> WhatsAppMutation(WhatsAppRequest? input, Func<WhatsAppRequest, Task<object>> operation)
    {
        if (input is null || !ModelState.IsValid) return Task.FromResult<IActionResult>(new JsonResult(new { error = "Solicitud inválida." }) { StatusCode = 400 });
        return WhatsAppResult(() => operation(input));
    }
    public Task<IActionResult> OnGetWhatsAppStateAsync() => WhatsAppResult(async () => await whatsapp.StateAsync(User));
    public Task<IActionResult> OnGetWhatsAppHistoryAsync(int recordId) => WhatsAppResult(async () => await whatsapp.HistoryAsync(User, recordId));
    public Task<IActionResult> OnPostWhatsAppPreviewAsync([FromBody] WhatsAppRequest? input) => WhatsAppMutation(input, async request => await whatsapp.PreviewAsync(User, request.Ids, request.ExcludePrevious));
    public Task<IActionResult> OnPostWhatsAppMessageAsync([FromBody] WhatsAppRequest? input) => WhatsAppMutation(input, async request => new { saved = await whatsapp.SaveMessageAsync(User, request.Message) });
    public Task<IActionResult> OnPostWhatsAppCreateAsync([FromBody] WhatsAppRequest? input) => WhatsAppMutation(input, async request => new { id = await whatsapp.CreateAsync(User, request.CampaignId, request.Ids, request.Message, request.ExcludePrevious) });
    public Task<IActionResult> OnPostWhatsAppActionAsync([FromBody] WhatsAppRequest? input) => WhatsAppMutation(input, async request => new { saved = await whatsapp.ActionAsync(User, request.CampaignId, request.RecipientId, request.Action), at = whatsapp.Now });
}
