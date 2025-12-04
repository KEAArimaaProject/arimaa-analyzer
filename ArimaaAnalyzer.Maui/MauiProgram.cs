using Microsoft.Extensions.Logging;
using ArimaaAnalyzer.Maui.Services.Arimaa;

namespace ArimaaAnalyzer.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        builder.Services.AddMauiBlazorWebView();

        // Game services
        // Provide a default singleton built from a demo AEI position so DI works anywhere.
        builder.Services.AddSingleton<ArimaaGameService>(_ =>
        {
            // Demo board from tests/documentation
            var board = new[]
            {
                "rrrrrrrr",
                "hdcemcdh",
                "........",
                "........",
                "........",
                "........",
                "HDCMECDH",
                "RRRRRRRR"
            };

            static string BoardToAei(string[] b, string side)
            {
                var flat = string.Join(string.Empty, Array.ConvertAll(b, r => r.Replace('.', ' ')));
                return $"setposition {side} \"{flat}\"";
            }

            var aei = BoardToAei(board, "g");
            var state = new GameState(aei);
            return new ArimaaGameService(state);
        });

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}