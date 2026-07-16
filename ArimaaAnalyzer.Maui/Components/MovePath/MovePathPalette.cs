using System;

namespace ArimaaAnalyzer.Maui.Components.MovePath;

/// <summary>
/// Resolves move-path line colors from a selected palette preset.
/// </summary>
public static class MovePathPalette
{
    public static string GetColor(int orderIndex, string? paletteId = null)
    {
        var colors = MovePathPaletteOptions.GetColors(paletteId);
        if (orderIndex < 0) orderIndex = 0;
        if (colors.Length == 0) return "#000000";
        return colors[orderIndex % colors.Length];
    }
}