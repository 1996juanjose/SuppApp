using Android.Content;
using Microsoft.Maui.ApplicationModel;

namespace OldSchoolDialer;

public static class PhoneCalls
{
    public static async Task StartAsync(string number, Func<bool>? canContinue = null)
    {
        var digits = new string(number.Where(char.IsDigit).ToArray());
        if (digits.Length is < 9 or > 15) throw new InvalidOperationException("El numero no es valido.");
        var permission = await Permissions.RequestAsync<DirectCallPermission>();
        if (permission != PermissionStatus.Granted)
            throw new InvalidOperationException("Activa el permiso Telefono para iniciar llamadas desde la app.");
        if (canContinue is not null && !canContinue()) throw new OperationCanceledException("La tanda fue pausada antes de iniciar la llamada.");
        var phone = (number.StartsWith('+') ? "+" : "") + digits;
        var context = Platform.CurrentActivity ?? throw new InvalidOperationException("Abre la app para llamar.");
        var intent = new Intent(Intent.ActionCall, Android.Net.Uri.Parse("tel:" + phone));
        context.StartActivity(intent);
    }
    public static Task OpenWhatsAppBusinessAsync(string number, Func<bool>? canContinue = null)
    {
        var link = WhatsAppLinks.Chat(number);
        if (canContinue is not null && !canContinue()) throw new OperationCanceledException("La tanda fue detenida.");
        var context = Platform.CurrentActivity ?? throw new InvalidOperationException("Abre la app para continuar.");
        var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(link));
        intent.SetPackage("com.whatsapp.w4b");
        try { context.StartActivity(intent); }
        catch (ActivityNotFoundException)
        { throw new InvalidOperationException("Instala y configura WhatsApp Business en este dispositivo, o selecciona Línea telefónica en los filtros."); }
        return Task.CompletedTask;
    }
    public sealed class DirectCallPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions => [(Android.Manifest.Permission.CallPhone, true)];
    }
}
