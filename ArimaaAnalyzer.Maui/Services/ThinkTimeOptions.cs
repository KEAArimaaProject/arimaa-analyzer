namespace ArimaaAnalyzer.Maui.Services;

/// <summary>
/// Presets, bounds, and helpers for engine think time (milliseconds).
/// </summary>
public static class ThinkTimeOptions
{
    /// <summary>Minimum allowed custom/preset think time in milliseconds.</summary>
    public const int MinMs = 1;

    /// <summary>Maximum allowed think time in milliseconds (1 minute).</summary>
    public const int MaxMs = 60_000;

    /// <summary>Default think time (matches historical UI default).</summary>
    public const int DefaultMs = 200;

    /// <summary>
    /// Predefined think times: 200, 400, …, 8000 ms (same set as the former native select).
    /// </summary>
    public static IReadOnlyList<int> Presets { get; } =
        Enumerable.Range(1, 40).Select(i => i * 200).ToArray();

    public static int Clamp(int ms) => Math.Clamp(ms, MinMs, MaxMs);

    public static bool IsPreset(int ms) => Presets.Contains(ms);

    public static string Format(int ms) => $"{ms} ms";

    /// <summary>
    /// Parses a custom ms string. Returns false for empty/non-numeric input.
    /// Out-of-range values are clamped and still considered valid.
    /// </summary>
    public static bool TryParseCustom(string? text, out int ms)
    {
        ms = DefaultMs;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (!int.TryParse(text.Trim(), out var parsed))
            return false;

        ms = Clamp(parsed);
        return true;
    }
}
