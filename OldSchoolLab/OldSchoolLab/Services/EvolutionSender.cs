using System.Net.Http.Json;
using System.Text.Json;

namespace OldSchoolLab.Services;

public sealed record EvolutionConnection(string BaseUrl, string Instance, string ApiKey)
{
    public string Key => BaseUrl.TrimEnd('/') + "/" + Instance;
    public bool Ready => Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo)
        && !string.IsNullOrWhiteSpace(Instance) && !string.IsNullOrWhiteSpace(ApiKey)
        && Key.Length <= 600;

    public static EvolutionConnection For(IConfiguration config, int companyId)
    {
        var section = config.GetSection($"Evolution:Companies:{companyId}");
        return new(section["BaseUrl"] ?? "", section["Instance"] ?? "", section["ApiKey"] ?? "");
    }
}

public sealed class EvolutionSender(IHttpClientFactory clients)
{
    public async Task<(string Status, string Id, string Detail)> SendAsync(
        EvolutionConnection connection, string phone, string message, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            connection.BaseUrl.TrimEnd('/') + "/message/sendText/" + Uri.EscapeDataString(connection.Instance));
        request.Headers.Add("apikey", connection.ApiKey);
        request.Content = JsonContent.Create(new { number = phone, text = message });
        using var response = await clients.CreateClient("evolution-bulk").SendAsync(request, cancellation);
        if (!response.IsSuccessStatusCode)
            return ((int)response.StatusCode >= 500 ? "Unknown" : "Failed", "",
                $"Evolution respondió HTTP {(int)response.StatusCode}. Revisa la instancia antes de continuar.");
        try
        {
            using var result = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellation), cancellationToken: cancellation);
            var id = result.RootElement.GetProperty("key").GetProperty("id").GetString();
            if (string.IsNullOrEmpty(id) || id.Length > 150) return ("Unknown", "", "Evolution respondió sin identificador de mensaje.");
            return ("Accepted", id, "Aceptado por Evolution. Entrega no confirmada.");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return ("Unknown", "", "No se pudo confirmar la respuesta de Evolution.");
        }
    }
}
