using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace OldSchoolDialer;

public partial class MainPage : ContentPage
{
    readonly HttpClient http = ServiceEndpoints.CreateClient();
    readonly DialingSession session = new();
    readonly List<Prospect> prospects = [];
    readonly string[] filters = ["Sin llamar", "Ya llamados", "Volver a llamar (pendientes)", "Todos"];
    readonly string[] filterValues = ["uncalled", "called", "callback", "all"];
    readonly string[] outcomes = ["Contestó", "No contestó", "Ocupado", "Número incorrecto", "Volver a llamar"];
    string? token; Uri? apiUri; int currentIndex, cursor; bool busy;
    string? currentQuery;
    public MainPage()
    {
        InitializeComponent();
        UsernameEntry.Text = Preferences.Default.Get("username", "");
        FilterPicker.ItemsSource = filters; FilterPicker.SelectedIndex = 0;
        BatchPicker.ItemsSource = new[] { "50 prospectos", "100 prospectos", "250 prospectos", "500 prospectos" }; BatchPicker.SelectedIndex = 1;
        OutcomePicker.ItemsSource = outcomes;
        FromPicker.Date = DateTime.Today.AddMonths(-1); ToPicker.Date = DateTime.Today;
        DatesCheck.IsChecked = true;
        AutomaticSwitch.IsToggled = true;
        session.AutoAdvance = true;
        CallbackDate.Date = DateTime.Today.AddDays(1); CallbackTime.Time = new TimeSpan(10, 0, 0);
    }
    bool companiesLoaded;
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!companiesLoaded && token is null && !busy)
            await RunAsync(LoadCompaniesAsync);
    }
    static string Url(Uri root, string path) => root.ToString().TrimEnd('/') + "/" + path;
    void Message(string text, bool error = false) { MessageLabel.Text = text; MessageLabel.TextColor = error ? Colors.Firebrick : Color.FromArgb("#047857"); }
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
        CompaniesButton.IsEnabled = NextBatchButton.IsEnabled = !busy && !session.AwaitingResult;
        LoadButton.IsEnabled = !busy;
        LoginButton.IsEnabled = !busy && companiesLoaded && !session.AwaitingResult;
        StartButton.IsEnabled = !busy && !session.AwaitingResult && currentIndex < prospects.Count;
        CallButton.IsEnabled = !busy && currentIndex < prospects.Count;
        CallButton.Text = session.AwaitingResult ? "Guardar resultado y volver a llamar a este prospecto" : "Llamar a este prospecto";
        SaveOnlyButton.IsEnabled = SaveResultButton.IsEnabled = !busy && session.AwaitingResult && OutcomePicker.SelectedItem is string;
        CancelAttemptButton.IsVisible = session.AwaitingResult;
        CancelAttemptButton.IsEnabled = !busy;
        // Pausing is always available, including while a save is in flight.
        SaveResultButton.Text = "Guardar y llamar al siguiente";
    }
    async Task EnsureAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized) { session.Pause(); throw new InvalidOperationException("La sesión venció. Sal y vuelve a iniciar sesión; no se avanzó la cola."); }
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
        using var response = await http.PostAsJsonAsync(Url(authUri, "api/auth/login"), new { username = UsernameEntry.Text.Trim(), password = PasswordEntry.Text, companyId = company.Id });
        await EnsureAsync(response); var login = await response.Content.ReadFromJsonAsync<Login>() ?? throw new InvalidOperationException("Respuesta de sesión inválida.");
        if (string.IsNullOrWhiteSpace(login.Token) || login.CompanyId != company.Id) throw new InvalidOperationException("La cuenta no tiene la empresa seleccionada.");
        token = login.Token; PasswordEntry.Text = "";
        Preferences.Default.Set("username", UsernameEntry.Text.Trim());
        LoginPanel.IsVisible = false; QueuePanel.IsVisible = true; SessionLabel.Text = $"{login.CompanyName ?? company.Name} · {login.Username}";
        cursor = 0; session.Reset(); AutomaticSwitch.IsToggled = true; session.AutoAdvance = true; currentQuery = BuildQuery(); await LoadAsync();
    });
    string BuildQuery()
    {
        var take = new[] { 50, 100, 250, 500 }[Math.Max(0, BatchPicker.SelectedIndex)];
        var query = $"callFilter={filterValues[Math.Max(0, FilterPicker.SelectedIndex)]}&take={take}&search={Uri.EscapeDataString(SearchEntry.Text?.Trim() ?? "")}";
        foreach (var (name, text) in new[] { ("minAttempts", MinCallsEntry.Text), ("maxAttempts", MaxCallsEntry.Text) })
            if (!string.IsNullOrWhiteSpace(text)) { if (!int.TryParse(text, out var value) || value < 0) throw new InvalidOperationException("Los intentos deben ser enteros positivos."); query += $"&{name}={value}"; }
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
        await RunAsync(async () => { currentQuery = query; cursor = 0; await LoadAsync(); });
    }
    async void OnNextBatchClicked(object? sender, EventArgs e) => await RunAsync(LoadAsync);
    async Task CallAsync()
    {
        if (currentIndex >= prospects.Count || session.AwaitingResult) return;
        var campaignCall = session.Running;
        await PhoneCalls.StartAsync(prospects[currentIndex].Cellphone, () => !campaignCall || session.Running);
        session.CallStarted(); Message("Al terminar la llamada, vuelve aquí, elige el resultado y guárdalo.");
    }
    async void OnStartClicked(object? sender, EventArgs e) => await RunAsync(async () => { session.Start(); await CallAsync(); });
    async void OnCallClicked(object? sender, EventArgs e)
    {
        if (session.AwaitingResult) await SaveResultAsync(false, repeatCurrent: true);
        else await RunAsync(CallAsync);
    }
    void OnPauseClicked(object? sender, EventArgs e) { session.Pause(); Message("Tanda pausada. El resultado pendiente se conserva."); Controls(); }
    async void OnCancelAttemptClicked(object? sender, EventArgs e)
    {
        if (busy || !session.AwaitingResult) return;
        if (!await DisplayAlertAsync("Cancelar intento", "Úsalo si no llegaste a realizar la llamada. No se guardará ni se contará el intento.", "Cancelar intento", "Volver")) return;
        session.CancelAttempt(); OutcomePicker.SelectedIndex = -1; NotesEditor.Text = ""; Message("Intento cancelado. La tanda está pausada."); Controls();
    }
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
    async void OnSaveOnlyClicked(object? sender, EventArgs e) => await SaveResultAsync(false);
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
        current.CallCount = saved.CallCount; current.LastOutcome = outcome;
        var advance = session.ResultSaved(callNext);
        if (!repeatCurrent) currentIndex++;
        OutcomePicker.SelectedIndex = -1; NotesEditor.Text = ""; ShowCurrent();
        if (repeatCurrent)
        {
            Message("Resultado guardado. Volviendo a llamar al mismo prospecto…");
            await CallAsync();
            return;
        }
        Message("Resultado guardado. " + (advance ? "Llamando al siguiente…" : "Elige cuándo llamar al siguiente."));
        if (advance && session.Running && currentIndex < prospects.Count) await CallAsync();
        if (currentIndex >= prospects.Count) { session.Pause(); Message("Tanda terminada. Carga la siguiente para continuar."); }
    });
    void ShowCurrent()
    {
        QueueSummaryLabel.Text = $"{prospects.Count} prospectos en esta tanda · {Math.Min(currentIndex, prospects.Count)} completados";
        CurrentProspectPanel.IsVisible = currentIndex < prospects.Count;
        if (!CurrentProspectPanel.IsVisible) return;
        var p = prospects[currentIndex]; ProspectNameLabel.Text = string.IsNullOrWhiteSpace(p.NameOrReference) ? "Prospecto" : p.NameOrReference; ProspectPhoneLabel.Text = p.Cellphone;
        ProspectPositionLabel.Text = $"PROSPECTO {currentIndex + 1} DE {prospects.Count}";
        CallCountLabel.Text = p.CallCount == 0 ? "Sin llamar" : $"Se llamó {p.CallCount} {(p.CallCount == 1 ? "vez" : "veces")}";
        PreviousOutcomeLabel.Text = p.LastOutcome is null ? "Aún no tiene intentos registrados." : $"Último resultado: {p.LastOutcome} · {p.LastCallAt:dd/MM HH:mm}";
        Controls();
    }
    async void OnLogoutClicked(object? sender, EventArgs e)
    {
        if (busy) return;
        if (session.AwaitingResult && !await DisplayAlertAsync("Resultado pendiente", "¿Salir sin guardar el resultado de esta llamada?", "Salir", "Cancelar")) return;
        session.Reset(); token = null; prospects.Clear(); QueuePanel.IsVisible = false; LoginPanel.IsVisible = true; AutomaticSwitch.IsToggled = true; session.AutoAdvance = true; Message("Sesión cerrada.");
    }
    public sealed class Company { public int Id { get; set; } public string Name { get; set; } = ""; }
    sealed class Login { public string? Token { get; set; } public string? Username { get; set; } public int? CompanyId { get; set; } public string? CompanyName { get; set; } }
    sealed class SavedCall { public int CallCount { get; set; } }
    sealed class Prospect { public int Id { get; set; } public string Cellphone { get; set; } = ""; public string? NameOrReference { get; set; } public int CallCount { get; set; } public string? LastOutcome { get; set; } public DateTime? LastCallAt { get; set; } }
}
