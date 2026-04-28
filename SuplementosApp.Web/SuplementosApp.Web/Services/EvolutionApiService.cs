using System.Text;
using System.Text.Json;

namespace SuplementosApp.Web.Services;

public class EvolutionApiService
{
    private readonly HttpClient _http;
    private readonly string _instance;
    private readonly string _apiKey;

    public EvolutionApiService(IConfiguration config)
    {
        var baseUrl = config["EvolutionApi:BaseUrl"] ?? "http://localhost:8081";
        _instance   = config["EvolutionApi:Instance"] ?? "suplementos";
        _apiKey     = config["EvolutionApi:ApiKey"] ?? "";

        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _http.DefaultRequestHeaders.Add("apikey", _apiKey);
    }

    public async Task<bool> EnviarMensajeAsync(string numero, string mensaje)
    {
        var numeroLimpio = numero.Replace("+", "").Replace(" ", "").Replace("-", "");

        var payload = new { number = numeroLimpio, text = mensaje };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _http.PostAsync($"/message/sendText/{_instance}", content);
        return response.IsSuccessStatusCode;
    }

    // Devuelve "open", "close", "connecting" o "error"
    public async Task<string> ObtenerEstadoAsync()
    {
        try
        {
            var response = await _http.GetAsync($"/instance/connectionState/{_instance}");
            if (!response.IsSuccessStatusCode) return "error";

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("instance")
                .GetProperty("state")
                .GetString() ?? "error";
        }
        catch { return "error"; }
    }

    public async Task<bool> InstanceConectadaAsync() =>
        await ObtenerEstadoAsync() == "open";

    // Devuelve el base64 del QR (data:image/png;base64,...) o null si falla
    public async Task<string?> ObtenerQrAsync()
    {
        try
        {
            // Reiniciar la instancia para forzar generación de QR
            await _http.PutAsync($"/instance/restart/{_instance}", null);

            // Esperar a que Baileys entre en modo QR (máx 10 intentos × 2s = 20s)
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(2000);

                var response = await _http.GetAsync($"/instance/connect/{_instance}");
                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("base64", out var base64Prop))
                {
                    var base64 = base64Prop.GetString();
                    if (!string.IsNullOrEmpty(base64))
                        return base64;
                }
            }

            return null;
        }
        catch { return null; }
    }

    public async Task CrearInstanciaAsync()
    {
        try
        {
            var payload = new { instanceName = _instance, qrcode = true };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            await _http.PostAsync("/instance/create", content);
        }
        catch { }
    }

    public async Task<bool> DesconectarAsync()
    {
        try
        {
            var response = await _http.DeleteAsync($"/instance/logout/{_instance}");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ConfigurarWebhookAsync(string webhookUrl, string? webhookApiKey = null)
    {
        try
        {
            var payload = new
            {
                url = webhookUrl,
                webhook_by_events = false,
                webhook_base64 = false,
                events = new[] { "MESSAGES_UPSERT" }
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"/webhook/set/{_instance}");
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            if (!string.IsNullOrEmpty(webhookApiKey))
                requestMessage.Headers.TryAddWithoutValidation("apikey", _apiKey);

            var response = await _http.SendAsync(requestMessage);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<string?> ObtenerWebhookUrlAsync()
    {
        try
        {
            var response = await _http.GetAsync($"/webhook/find/{_instance}");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("url", out var urlProp))
                return urlProp.GetString();

            return null;
        }
        catch { return null; }
    }
}
