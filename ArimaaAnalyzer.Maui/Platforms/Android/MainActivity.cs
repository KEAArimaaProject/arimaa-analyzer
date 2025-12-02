using Android.App;
using Android.Content.PM;
using Android.OS;
using Microsoft.Maui;

namespace ArimaaAnalyzer.Maui.Platforms.Android;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize |
                           ConfigChanges.Orientation |
                           ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize |
                           ConfigChanges.Density |
                           ConfigChanges.KeyboardHidden)]
public class MainActivity : MauiAppCompatActivity
{
    // ← INTENTIONALLY EMPTY
    // Safe areas are handled automatically in .NET 8+ / .NET 10
    // Your CSS with env(safe-area-inset-top) does everything
}