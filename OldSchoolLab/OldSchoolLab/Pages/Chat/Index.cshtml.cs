using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OldSchoolLab.Services;

namespace OldSchoolLab.Pages.Chat;

public class IndexModel(ChatApiClient chatApiClient, IHttpContextAccessor httpContextAccessor) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string PhoneNumber { get; set; } = string.Empty;

    [BindProperty]
    public string MessageText { get; set; } = string.Empty;

    public ChatThreadResponse? ThreadData { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber))
        {
            return Page();
        }

        await LoadThreadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadAsync(CancellationToken cancellationToken)
    {
        await LoadThreadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSendAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber) || string.IsNullOrWhiteSpace(MessageText))
        {
            ErrorMessage = "Debes indicar un teléfono y un mensaje.";
            return Page();
        }

        var token = httpContextAccessor.HttpContext?.Session.GetString("ChatApiJwt");
        if (string.IsNullOrWhiteSpace(token))
        {
            ErrorMessage = "La sesión no tiene token de chat. Vuelve a iniciar sesión.";
            return Page();
        }

        await chatApiClient.SendMessageAsync(new ChatMessageRequest
        {
            PhoneNumber = PhoneNumber.Trim(),
            MessageText = MessageText.Trim()
        }, token, cancellationToken);

        return RedirectToPage(new { phoneNumber = PhoneNumber.Trim() });
    }

    private async Task LoadThreadAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        ThreadData = null;

        if (string.IsNullOrWhiteSpace(PhoneNumber))
        {
            return;
        }

        var token = httpContextAccessor.HttpContext?.Session.GetString("ChatApiJwt");
        if (string.IsNullOrWhiteSpace(token))
        {
            ErrorMessage = "Inicia sesión de nuevo para cargar el chat.";
            return;
        }

        ThreadData = await chatApiClient.GetThreadAsync(PhoneNumber.Trim(), token, cancellationToken: cancellationToken);
        if (ThreadData is null)
        {
            ErrorMessage = "No se encontró conversación para ese número todavía.";
        }
    }
}
