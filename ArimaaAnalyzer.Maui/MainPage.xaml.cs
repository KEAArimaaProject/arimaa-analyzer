namespace ArimaaAnalyzer.Maui;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        // Ensure the WebView can receive keyboard input for hotkeys (j/l/i/k/g/s).
        Loaded += (_, _) =>
        {
            try { blazorWebView?.Focus(); } catch { /* ignore */ }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        try { blazorWebView?.Focus(); } catch { /* ignore */ }
    }
}