using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace OldSchoolDialer;

public partial class MainPage : ContentPage
{
    private readonly HttpClient httpClient = new();
    private readonly List<Prospect> prospects = [];
    private string? token;
    private Uri? apiUri;
    private int currentIndex;
    private bool dialerOpened;
    private static readonly string[] Outcomes = ["Contestó", "No contestó", "Ocupado", "Número incorrecto", "Volver a llamar"];

    public MainPage()
    {
        InitializeComponent();
        AuthUrlEntry.Text = Preferences.Default.Get("auth_url", "");
        ApiUrlEntry.Text = Preferences.Default.Get("api_url", "");
        OutcomePicker.ItemsSource = Outcomes;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        if (!Uri.TryCreate(AuthUrlEntry.Text?.Trim(), UriKind.Absolute, out var authUri)
            || !Uri.TryCreate(ApiUrlEntry.Text?.Trim(), UriKind.Absolute, out apiUri)
            || string.IsNullOrWhiteSpace(UsernameEntry.Text)
            || string.IsNullOrWhiteSpace(PasswordEntry.Text))
        {
            MessageLabel.Text = "Completa URLs válidas, usuario y contraseña.";
            return;
        }

        if (authUri.Scheme != Uri.UriSchemeHttps || apiUri.Scheme != Uri.UriSchemeHttps)
        {
            MessageLabel.Text = "Usa URLs HTTPS para proteger las credenciales.";
            return;
        }

        await SetBusyAsync(true);
        try
        {
            using var response = await httpClient.PostAsJsonAsync(CombineUrl(authUri, "api/auth/login"), new
            {
                username = UsernameEntry.Text.Trim(),
                password = PasswordEntry.Text
            });
            if (!response.IsSuccessStatusCode)
            {
                MessageLabel.Text = "No se pudo iniciar sesión. Revisa tus credenciales y la URL de AuthService.";
                return;
            }

            var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (string.IsNullOrWhiteSpace(login?.Token))
            {
                MessageLabel.Text = "El servicio de autenticación no devolvió un token.";
                return;
            }

            token = login.Token;
            Preferences.Default.Set("auth_url", authUri.ToString().TrimEnd('/'));
            Preferences.Default.Set("api_url", apiUri!.ToString().TrimEnd('/'));
            AuthUrlEntry.IsEnabled = false;
            ApiUrlEntry.IsEnabled = false;
            UsernameEntry.IsEnabled = false;
            PasswordEntry.IsEnabled = false;
            LoginButton.IsVisible = false;
            QueuePanel.IsVisible = true;
            MessageLabel.TextColor = Colors.Green;
            MessageLabel.Text = $"Sesión iniciada: {login.Username ?? UsernameEntry.Text}";
            await LoadQueueAsync();
        }
        catch (Exception ex)
        {
            MessageLabel.TextColor = Colors.Red;
            MessageLabel.Text = $"Error de conexión: {ex.Message}";
        }
        finally
        {
            await SetBusyAsync(false);
        }
    }

    private async void OnLoadQueueClicked(object? sender, EventArgs e) => await LoadQueueAsync();

    private async Task LoadQueueAsync()
    {
        if (apiUri is null || string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        await SetBusyAsync(true);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, CombineUrl(apiUri, "api/records/prospect-calls/queue"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            prospects.Clear();
            prospects.AddRange(await response.Content.ReadFromJsonAsync<List<Prospect>>() ?? []);
            currentIndex = 0;
            MessageLabel.Text = string.Empty;
            ShowCurrentProspect();
        }
        catch (Exception ex)
        {
            MessageLabel.TextColor = Colors.Red;
            MessageLabel.Text = $"No se pudo cargar la cola: {ex.Message}";
        }
        finally
        {
            await SetBusyAsync(false);
        }
    }

    private async void OnCallClicked(object? sender, EventArgs e)
    {
        if (currentIndex >= prospects.Count)
        {
            return;
        }

        try
        {
            var number = prospects[currentIndex].Cellphone;
            await Launcher.Default.OpenAsync(new Uri($"tel:{Uri.EscapeDataString(number)}"));
            dialerOpened = true;
            MessageLabel.TextColor = Colors.Green;
            MessageLabel.Text = "Completa la llamada en el marcador y vuelve para registrar el resultado.";
        }
        catch (Exception ex)
        {
            MessageLabel.TextColor = Colors.Red;
            MessageLabel.Text = $"No se pudo abrir el marcador: {ex.Message}";
        }
    }

    private async void OnSaveResultClicked(object? sender, EventArgs e)
    {
        if (currentIndex >= prospects.Count || OutcomePicker.SelectedItem is not string outcome)
        {
            MessageLabel.TextColor = Colors.Red;
            MessageLabel.Text = "Selecciona el resultado de la llamada.";
            return;
        }

        if (!dialerOpened)
        {
            var confirm = await DisplayAlert("Confirmar llamada", "No se abrió el marcador desde la app. ¿Quieres guardar el resultado de todos modos?", "Guardar", "Cancelar");
            if (!confirm)
            {
                return;
            }
        }

        await SetBusyAsync(true);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                CombineUrl(apiUri!, $"api/records/{prospects[currentIndex].Id}/prospect-call-result"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new { outcome, notes = NotesEditor.Text });
            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            currentIndex++;
            dialerOpened = false;
            OutcomePicker.SelectedItem = null;
            NotesEditor.Text = string.Empty;
            MessageLabel.TextColor = Colors.Green;
            MessageLabel.Text = "Resultado guardado.";
            ShowCurrentProspect();
        }
        catch (Exception ex)
        {
            MessageLabel.TextColor = Colors.Red;
            MessageLabel.Text = $"No se pudo guardar el resultado: {ex.Message}";
        }
        finally
        {
            await SetBusyAsync(false);
        }
    }

    private void ShowCurrentProspect()
    {
        QueueSummaryLabel.Text = $"{prospects.Count} prospectos con teléfono válido";
        var hasCurrent = currentIndex < prospects.Count;
        CurrentProspectPanel.IsVisible = hasCurrent;
        if (!hasCurrent)
        {
            QueueSummaryLabel.Text = prospects.Count == 0
                ? "No hay prospectos con teléfono válido."
                : "Has terminado la cola de prospectos.";
            return;
        }

        var prospect = prospects[currentIndex];
        ProspectNameLabel.Text = string.IsNullOrWhiteSpace(prospect.NameOrReference) ? "Prospecto" : prospect.NameOrReference;
        ProspectPhoneLabel.Text = prospect.Cellphone;
        ProspectPositionLabel.Text = $"Prospecto {currentIndex + 1} de {prospects.Count}";
    }

    private async Task SetBusyAsync(bool isBusy)
    {
        BusyIndicator.IsVisible = isBusy;
        BusyIndicator.IsRunning = isBusy;
        LoginButton.IsEnabled = !isBusy;
        CallButton.IsEnabled = !isBusy;
        SaveResultButton.IsEnabled = !isBusy;
        await Task.CompletedTask;
    }

    private static string CombineUrl(Uri baseUri, string path) => $"{baseUri.ToString().TrimEnd('/')}/{path}";

    private sealed class LoginResponse
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }

    private sealed class Prospect
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("cellphone")]
        public string Cellphone { get; set; } = string.Empty;

        [JsonPropertyName("nameOrReference")]
        public string? NameOrReference { get; set; }
    }
}
