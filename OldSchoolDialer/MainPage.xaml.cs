using System.Text.Json;
using System.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace OldSchoolDialer;

public partial class MainPage : ContentPage
{
    readonly HttpClient http = ServiceEndpoints.CreateClient();
    readonly DialingSession session = new();
    readonly DialingProgress progress = new();
    bool totalKnown;
    readonly List<Prospect> prospects = [];
    readonly string[] outcomes = ["Contestó", "No contestó", "Competencia", "No interesado"];
    string? token; Uri? apiUri; int currentIndex, cursor; bool busy;
    string? currentQuery;
    public MainPage()
    {
        InitializeComponent();
        QueuePanel.Children.Remove(CallWorkspace);
        UsernameEntry.Text = Preferences.Default.Get("username", "");
        BatchPicker.ItemsSource = new[] { "50 prospectos", "100 prospectos", "250 prospectos", "500 prospectos" }; BatchPicker.SelectedIndex = 1;
        OutcomePicker.ItemsSource = outcomes;
        FromPicker.Date = DateTime.Today.AddMonths(-1); ToPicker.Date = DateTime.Today;
        DatesCheck.IsChecked = true;
        AutomaticSwitch.IsToggled = true;
        session.AutoAdvance = true;
        RestoreFilters();
        BatchPicker.SelectedIndexChanged += (_, _) => SaveFilters();
        DatesCheck.CheckedChanged += (_, _) => SaveFilters();
        FromPicker.DateSelected += (_, _) => SaveFilters();
        ToPicker.DateSelected += (_, _) => SaveFilters();
        AutomaticSwitch.Toggled += (_, _) => SaveFilters();
        CallbackDate.Date = DateTime.Today.AddDays(1); CallbackTime.Time = new TimeSpan(10, 0, 0);
    }
    const string SessionKey = "oldschool.mobile.session";
    bool companiesLoaded, restoreAttempted;
    CallPage? callPage;
    async Task ShowCallPageAsync()
    {
        callPage ??= new CallPage(CallWorkspace, async () =>
        {
            if (busy || !Navigation.ModalStack.Contains(callPage)) return;
            session.Pause();
            await Navigation.PopModalAsync();
            Controls();
        });
        if (!Navigation.ModalStack.Contains(callPage)) await Navigation.PushModalAsync(callPage);
    }
    sealed class FilterSettings
    {
        public int Batch { get; set; } = 1;
        public bool Dates { get; set; } = true;
        public bool Automatic { get; set; } = true;
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }
    void SaveFilters() => Preferences.Default.Set("dialer.filters", JsonSerializer.Serialize(new FilterSettings
    {
        Batch = BatchPicker.SelectedIndex, Dates = DatesCheck.IsChecked,
        Automatic = AutomaticSwitch.IsToggled, From = FromPicker.Date, To = ToPicker.Date
    }));
    void RestoreFilters()
    {
        try
        {
            var value = JsonSerializer.Deserialize<FilterSettings>(Preferences.Default.Get("dialer.filters", "null"));
            if (value is null) return;
            BatchPicker.SelectedIndex = Math.Clamp(value.Batch, 0, 3);
            DatesCheck.IsChecked = value.Dates; AutomaticSwitch.IsToggled = value.Automatic; session.AutoAdvance = value.Automatic;
            FromPicker.Date = value.From ?? DateTime.Today.AddMonths(-1); ToPicker.Date = value.To ?? DateTime.Today;
        }
        catch (JsonException) { Preferences.Default.Remove("dialer.filters"); }
    }
    void UseLogin(Login login)
    {
        token = login.Token; apiUri = ServiceEndpoints.Api;
        LoginPanel.IsVisible = false; QueuePanel.IsVisible = true;
        SessionLabel.Text = $"{login.CompanyName} · {login.Username}";
        cursor = 0; progress.Reset(); totalKnown = false; session.Reset(); session.AutoAdvance = AutomaticSwitch.IsToggled;
        currentQuery = BuildQuery();
    }
    async Task RestoreLoginAsync()
    {
        string? raw;
        try { raw = await SecureStorage.Default.GetAsync(SessionKey); }
        catch { SecureStorage.Default.Remove(SessionKey); return; }
        if (string.IsNullOrEmpty(raw)) return;
        try
        {
            var login = JsonSerializer.Deserialize<Login>(raw);
            var payload = login?.Token?.Split('.')[1].Replace('-', '+').Replace('_', '/');
            if (payload is null || !login!.CompanyId.HasValue) return;
            using var jwt = JsonDocument.Parse(Convert.FromBase64String(payload.PadRight((payload.Length + 3) / 4 * 4, '=')));
            if (!jwt.RootElement.TryGetProperty("exp", out var expiry) || expiry.GetInt64() <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            { SecureStorage.Default.Remove(SessionKey); return; }
            UseLogin(login);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or IndexOutOfRangeException)
        { SecureStorage.Default.Remove(SessionKey); return; }
        await LoadAsync();
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!restoreAttempted && !busy)
        { restoreAttempted = true; await RunAsync(RestoreLoginAsync); }
        if (!companiesLoaded && token is null && !busy)
            await RunAsync(LoadCompaniesAsync);
    }
    static string Url(Uri root, string path) => root.ToString().TrimEnd('/') + "/" + path;
    void Message(string text, bool error = false)
    {
        MessageLabel.Text = CallMessageLabel.Text = text;
        MessageLabel.TextColor = CallMessageLabel.TextColor = error ? Colors.Firebrick : Color.FromArgb("#047857");
    }
    async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true; Controls();
        try { await action(); }
        catch (Exception ex) { session.Pause(); Message(ex.Message, true); }
        finally { busy = false; Controls(); }
    }
    void Controls()
    {
        BusyIndicator.IsVisible = BusyIndicator.IsRunning = busy;
        CallBusyIndicator.IsVisible = CallBusyIndicator.IsRunning = busy;
        SkipButton.IsEnabled = !busy && currentIndex < prospects.Count;
        CompaniesButton.IsEnabled = !busy && !session.AwaitingResult;
        LoadButton.IsEnabled = !busy;
        LoginButton.IsEnabled = !busy && companiesLoaded && !session.AwaitingResult;
        StartButton.IsEnabled = !busy && currentIndex < prospects.Count;
        StartButton.Text = session.AwaitingResult ? "Continuar llamada actual" : "Iniciar llamadas masivas";
        SaveResultButton.IsEnabled = !busy && session.AwaitingResult && OutcomePicker.SelectedItem is string;
        // Pausing is always available, including while a save is in flight.
        SaveResultButton.Text = "Guardar y llamar al siguiente";
    }
    async Task EnsureAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized) { session.Pause(); token = null; SecureStorage.Default.Remove(SessionKey); LoginPanel.IsVisible = true; QueuePanel.IsVisible = false; throw new InvalidOperationException("La sesión venció. Sal y vuelve a iniciar sesión; no se avanzó la cola."); }
        if (response.IsSuccessStatusCode) return;
        var text = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"El servidor respondió {(int)response.StatusCode}: {text[..Math.Min(300, text.Length)]}");
    }
    HttpRequestMessage Request(HttpMethod method, string path)
    {
        if (apiUri is null || token is null) throw new InvalidOperationException("Inicia sesión primero.");
        var request = new HttpRequestMessage(method, Url(apiUri, path)); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); return request;
    }
    async void OnCompaniesClicked(object? sender, EventArgs e) => await RunAsync(LoadCompaniesAsync);
    async Task LoadCompaniesAsync()
    {
        CompaniesButton.IsVisible = false;
        Message("Cargando empresas…");
        try
        {
            using var response = await http.GetAsync(Url(ServiceEndpoints.Auth, "api/auth/companies"));
            await EnsureAsync(response);
            var companies = await response.Content.ReadFromJsonAsync<List<Company>>() ?? [];
            CompanyPicker.ItemsSource = companies;
            companiesLoaded = companies.Count > 0;
            if (companies.Count == 1) CompanyPicker.SelectedIndex = 0;
            if (!companiesLoaded) throw new InvalidOperationException("No hay empresas disponibles en este momento.");
            Message("Selecciona tu empresa e ingresa tus credenciales.");
        }
        catch
        {
            companiesLoaded = false;
            CompaniesButton.IsVisible = true;
            throw new InvalidOperationException("No se pudo cargar las empresas. Comprueba tu conexión a Internet y pulsa Reintentar conexión.");
        }
    }
    async void OnLoginClicked(object? sender, EventArgs e) => await RunAsync(async () =>
    {
        if (CompanyPicker.SelectedItem is not Company company || string.IsNullOrWhiteSpace(UsernameEntry.Text) || string.IsNullOrWhiteSpace(PasswordEntry.Text)) throw new InvalidOperationException("Selecciona empresa, usuario y contraseña.");
        var authUri = ServiceEndpoints.Auth; apiUri = ServiceEndpoints.Api;
        using var response = await http.PostAsJsonAsync(Url(authUri, "api/auth/login"), new { username = UsernameEntry.Text.Trim(), password = PasswordEntry.Text, companyId = company.Id, rememberSession = true });
        await EnsureAsync(response); var login = await response.Content.ReadFromJsonAsync<Login>() ?? throw new InvalidOperationException("Respuesta de sesión inválida.");
        if (string.IsNullOrWhiteSpace(login.Token) || login.CompanyId != company.Id) throw new InvalidOperationException("La cuenta no tiene la empresa seleccionada.");
        token = login.Token; PasswordEntry.Text = "";
        Preferences.Default.Set("username", UsernameEntry.Text.Trim());
        LoginPanel.IsVisible = false; QueuePanel.IsVisible = true; SessionLabel.Text = $"{login.CompanyName ?? company.Name} · {login.Username}";
        await SecureStorage.Default.SetAsync(SessionKey, JsonSerializer.Serialize(login));
        UseLogin(login); await LoadAsync();
    });
    string BuildQuery()
    {
        var take = new[] { 50, 100, 250, 500 }[Math.Max(0, BatchPicker.SelectedIndex)];
        var query = $"callFilter=uncalled&take={take}";
        if (DatesCheck.IsChecked)
        {
            if (!FromPicker.Date.HasValue || !ToPicker.Date.HasValue || FromPicker.Date.Value.Date > ToPicker.Date.Value.Date)
                throw new InvalidOperationException("Selecciona un rango de fechas válido.");
            query += $"&fromDate={FromPicker.Date:yyyy-MM-dd}&toDate={ToPicker.Date:yyyy-MM-dd}";
        }
        return query;
    }
    async Task LoadAsync()
    {
        if (session.AwaitingResult) throw new InvalidOperationException("Guarda el resultado pendiente antes de cambiar la cola.");
        session.Pause();
        using var request = Request(HttpMethod.Get, $"api/records/prospect-calls/queue?{currentQuery}&afterId={cursor}");
        using var response = await http.SendAsync(request); await EnsureAsync(response);
        var loaded = await response.Content.ReadFromJsonAsync<List<Prospect>>() ?? [];
        if (cursor == 0 && response.Headers.TryGetValues("X-Total-Count", out var totals) && int.TryParse(totals.FirstOrDefault(), out var total))
        { progress.SetTotal(total); totalKnown = true; }
        if (!totalKnown) progress.AddLoaded(loaded.Count);
        if (response.Headers.TryGetValues("X-Next-Cursor", out var values) && int.TryParse(values.FirstOrDefault(), out var next)) cursor = next;
        prospects.Clear(); prospects.AddRange(loaded); currentIndex = 0; ShowCurrent(); Message(loaded.Count == 0 ? "Sin prospectos en esta tanda. Puedes cargar la siguiente o cambiar filtros." : "Tanda lista. Pulsa Iniciar llamadas masivas para comenzar.");
    }
    async void OnLoadQueueClicked(object? sender, EventArgs e)
    {
        if (busy) return;
        string query;
        try { query = BuildQuery(); }
        catch (Exception ex) { Message(ex.Message, true); return; }
        session.Pause();
        if (session.AwaitingResult)
        {
            var choice = await DisplayActionSheetAsync("Hay una llamada sin guardar", "Volver", null,
                "Guardar resultado y filtrar", "Descartar intento y filtrar");
            if (choice == "Guardar resultado y filtrar")
            {
                if (OutcomePicker.SelectedItem is not string)
                { Message("Selecciona el resultado de la llamada y vuelve a pulsar Aplicar filtros.", true); return; }
                await SaveResultAsync(false);
                if (session.AwaitingResult) return; // Failed saves keep the pending attempt intact.
            }
            else if (choice == "Descartar intento y filtrar")
            {
                session.CancelAttempt(); OutcomePicker.SelectedIndex = -1; NotesEditor.Text = "";
            }
            else { Controls(); return; }
        }
        await RunAsync(async () => { currentQuery = query; cursor = 0; progress.Reset(); totalKnown = false; await LoadAsync(); });
    }
    async Task CallAsync()
    {
        if (currentIndex >= prospects.Count || session.AwaitingResult) return;
        await ShowCallPageAsync();
        var campaignCall = session.Running;
        await PhoneCalls.StartAsync(prospects[currentIndex].Cellphone, () => !campaignCall || session.Running);
        session.CallStarted(); Message("Al terminar la llamada, vuelve aquí, elige el resultado y guárdalo.");
    }
    async void OnStartClicked(object? sender, EventArgs e) => await RunAsync(async () =>
    {
        await ShowCallPageAsync();
        if (!session.AwaitingResult) { session.Start(); await CallAsync(); }
    });
    async void OnSkipClicked(object? sender, EventArgs e) => await RunAsync(async () =>
    {
        if (currentIndex >= prospects.Count) return;
        session.CancelAttempt(); // Explicit skip never invents a completed call or posts to the API.
        progress.RecordSkipped();
        currentIndex++; OutcomePicker.SelectedIndex = -1; NotesEditor.Text = "";
        if (currentIndex >= prospects.Count) await LoadAsync();
        ShowCurrent();
        if (currentIndex < prospects.Count)
        { session.Start(); await CallAsync(); }
        else Message(currentIndex < prospects.Count ? "Prospecto omitido. Puedes llamar al siguiente." : "No quedan prospectos con estos filtros.");
    });
    void OnAutomaticToggled(object? sender, ToggledEventArgs e) { session.AutoAdvance = e.Value; LockLabel.Text = e.Value ? "🔒 Avance automático activado" : "🔓 Avance manual"; Controls(); }
    void OnOutcomeSelected(object? sender, EventArgs e)
    {
        if (sender is Button button) OutcomePicker.SelectedItem = button.CommandParameter as string;
    }
    void OnOutcomeChanged(object? sender, EventArgs e)
    {
        var selected = OutcomePicker.SelectedItem as string;
        CallbackPanel.IsVisible = selected == "Volver a llamar";
        SelectedOutcomeLabel.Text = selected is null ? "Elige una opción para habilitar el guardado." : $"Resultado: {selected}";
        foreach (var button in OutcomeChoices.Children.OfType<Button>())
        {
            var active = button.CommandParameter as string == selected;
            button.BackgroundColor = Color.FromArgb(active ? "#2563EB" : "#E2E8F0");
            button.TextColor = active ? Colors.White : Color.FromArgb("#334155");
        }
        Controls();
    }
    async void OnSaveResultClicked(object? sender, EventArgs e) => await SaveResultAsync(true);
    async Task SaveResultAsync(bool callNext, bool repeatCurrent = false) => await RunAsync(async () =>
    {
        if (!session.AwaitingResult || currentIndex >= prospects.Count || OutcomePicker.SelectedItem is not string outcome) throw new InvalidOperationException("Elige el resultado de la llamada pendiente.");
        DateTime? nextCallAt = outcome == "Volver a llamar" ? DateTime.SpecifyKind(CallbackDate.Date.GetValueOrDefault().Date + CallbackTime.Time.GetValueOrDefault(), DateTimeKind.Unspecified) : null;
        if (callNext) session.Start(); else session.Pause();
        using var request = Request(HttpMethod.Post, $"api/records/{prospects[currentIndex].Id}/prospect-call-result");
        request.Content = JsonContent.Create(new { requestId = session.PendingAttemptId, outcome, notes = NotesEditor.Text, nextCallAt });
        using var response = await http.SendAsync(request); await EnsureAsync(response);
        var saved = await response.Content.ReadFromJsonAsync<SavedCall>() ?? throw new InvalidOperationException("No se pudo confirmar el resultado. Reintenta guardar.");
        var current = prospects[currentIndex];
        current.CallCount = saved.CallCount; current.NoAnswerCount = saved.NoAnswerCount; current.LastOutcome = outcome;
        var advance = session.ResultSaved(callNext);
        progress.RecordSaved();
        if (!repeatCurrent) currentIndex++;
        OutcomePicker.SelectedIndex = -1; NotesEditor.Text = ""; ShowCurrent();
        if (repeatCurrent)
        {
            Message("Resultado guardado. Volviendo a llamar al mismo prospecto…");
            await CallAsync();
            return;
        }
        if (advance && session.Running && currentIndex >= prospects.Count)
        {
            await LoadAsync();
            if (prospects.Count > 0) session.Start();
        }
        if (advance && session.Running && currentIndex < prospects.Count)
        { Message("Resultado guardado. Llamando al siguiente…"); await CallAsync(); }
        else if (currentIndex >= prospects.Count)
        { session.Pause(); Message("No quedan prospectos con estos filtros. Puedes aplicar otros filtros."); }
        else Message("Resultado guardado.");
    });
    void ShowCurrent()
    {
        CallProgressLabel.Text = QueueSummaryLabel.Text = progress.Summary;
        QueueProgressBar.Progress = CallProgressBar.Progress = progress.Fraction;
        CurrentProspectPanel.IsVisible = currentIndex < prospects.Count;
        if (!CurrentProspectPanel.IsVisible) return;
        var p = prospects[currentIndex];
        NoAnswerButton.Text = $"No contestó ({p.NoAnswerCount + 1} {(p.NoAnswerCount == 0 ? "vez" : "veces")})";
        ProspectNameLabel.Text = string.IsNullOrWhiteSpace(p.NameOrReference) ? "Prospecto" : p.NameOrReference; ProspectPhoneLabel.Text = p.Cellphone;
        ProspectPositionLabel.Text = $"PROSPECTO {progress.Saved + progress.Skipped + 1} DE {progress.Total}";
        CallCountLabel.Text = p.CallCount == 0 ? "Sin llamar" : $"Se llamó {p.CallCount} {(p.CallCount == 1 ? "vez" : "veces")}";
        PreviousOutcomeLabel.Text = p.LastOutcome is null ? "Aún no tiene intentos registrados." : $"Último resultado: {p.LastOutcome} · {p.LastCallAt:dd/MM HH:mm}";
        Controls();
    }
    async void OnLogoutClicked(object? sender, EventArgs e)
    {
        if (busy) return;
        if (session.AwaitingResult && !await DisplayAlertAsync("Resultado pendiente", "¿Salir sin guardar el resultado de esta llamada?", "Salir", "Cancelar")) return;
        SecureStorage.Default.Remove(SessionKey);
        session.Reset(); token = null; prospects.Clear(); QueuePanel.IsVisible = false; LoginPanel.IsVisible = true; AutomaticSwitch.IsToggled = true; session.AutoAdvance = true; Message("Sesión cerrada.");
    }
    public sealed class Company { public int Id { get; set; } public string Name { get; set; } = ""; }
    sealed class Login { public string? Token { get; set; } public string? Username { get; set; } public int? CompanyId { get; set; } public string? CompanyName { get; set; } }
    sealed class SavedCall { public int CallCount { get; set; } public int NoAnswerCount { get; set; } }
    sealed class Prospect { public int NoAnswerCount { get; set; } public int Id { get; set; } public string Cellphone { get; set; } = ""; public string? NameOrReference { get; set; } public int CallCount { get; set; } public string? LastOutcome { get; set; } public DateTime? LastCallAt { get; set; } }
}
