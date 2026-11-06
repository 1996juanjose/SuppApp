namespace OldSchoolDialer;

// Dedicated native screen; queue controls and results no longer share the filters screen.
public sealed class CallPage : ContentPage
{
    readonly Func<Task> returnToFilters;
    bool returning;
    public CallPage(View workspace, Func<Task> returnToFilters)
    {
        this.returnToFilters = returnToFilters;
        Title = "Llamada actual";
        BackgroundColor = Color.FromArgb("#F5F7FB");
        var back = new Button { Text = "Volver a filtros", BackgroundColor = Color.FromArgb("#E2E8F0"), TextColor = Color.FromArgb("#334155") };
        back.Clicked += async (_, _) => await ReturnAsync();
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = new Thickness(20, 24), Spacing = 14,
            Children = { new Label { Text = "Llamada actual", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#172338") }, back, workspace }
        }};
    }
    async Task ReturnAsync()
    {
        if (returning) return;
        returning = true;
        try { await returnToFilters(); }
        finally { returning = false; }
    }
    protected override bool OnBackButtonPressed()
    {
        _ = ReturnAsync();
        return true;
    }
}
