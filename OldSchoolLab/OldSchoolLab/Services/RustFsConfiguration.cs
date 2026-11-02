using System.Net.Http.Json;
using System.Text.Json;

namespace OldSchool.Storage;

public static class RustFsConfiguration
{
    // Only import object-storage settings; the courses' database and auth stay separate.
    public static async Task LoadAsync(ConfigurationManager config)
    {
        if (!config.GetValue<bool>("ObjectStorage:Enabled") || !config.GetValue<bool>("OpenBao:Enabled")) return;
        string Required(string key) => !string.IsNullOrWhiteSpace(config[$"OpenBao:{key}"])
            ? config[$"OpenBao:{key}"]! : throw new InvalidOperationException($"Falta OpenBao:{key}.");
        using var http = new HttpClient { BaseAddress = new Uri(Required("Address").TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(30) };
        var authMount = config["OpenBao:AuthMount"] ?? "approle";
        using var login = await http.PostAsJsonAsync($"v1/auth/{authMount}/login", new { role_id = Required("RoleId"), secret_id = Required("SecretId") });
        if (!login.IsSuccessStatusCode) throw new InvalidOperationException("No se pudo autenticar en OpenBao para RustFS.");
        using var loginJson = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        http.DefaultRequestHeaders.Add("X-Vault-Token", loginJson.RootElement.GetProperty("auth").GetProperty("client_token").GetString());
        try
        {
            using var response = await http.GetAsync($"v1/{Required("Mount")}/data/{Required("SecretPath")}");
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("No se pudo leer la configuracion RustFS en OpenBao.");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var storage = json.RootElement.GetProperty("data").GetProperty("data").GetProperty("ObjectStorage");
            var settings = new Dictionary<string, string?>();
            foreach (var name in new[] { "Endpoint", "Bucket", "Region", "AccessKey", "SecretKey" })
                if (storage.TryGetProperty(name, out var value) && !string.IsNullOrWhiteSpace(value.GetString()))
                    settings[$"ObjectStorage:{name}"] = value.GetString();
            config.AddInMemoryCollection(settings);
        }
        finally
        {
            // Existing AppRole policies may forbid revoke-self; tokens then expire by their TTL.
            try { using var revoke = await http.PostAsync("v1/auth/token/revoke-self", null); }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
        }
    }
}
