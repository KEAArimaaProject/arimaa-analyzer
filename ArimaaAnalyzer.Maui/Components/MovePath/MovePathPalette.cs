using System;

namespace ArimaaAnalyzer.Maui.Components.MovePath;

/// <summary>
/// Fixed color palette for move-path overlays, ordered by first-piece-moved.
/// </summary>
public static class MovePathPalette
{
    /// <summary>
    /// Colors in assignment order:
    /// 1st piece → Deep Navy Blue, 2nd → Strong Red, 3rd → Deep Brown-Red, 4th → Medium Blue.
    /// </summary>
    public static readonly string[] Colors =
    {
        "#004488", // Deep Navy Blue
        "#BE0032", // Strong Red / Vermillion
        "#882D17", // Deep Brown-Red
        "#0067A5", // Medium Blue
    };

    public static string GetColor(int orderIndex)
    {
        if (orderIndex < 0) orderIndex = 0;
        // If more than 4 pieces move (unusual), cycle the palette.
        return Colors[orderIndex % Colors.Length];
    }
}
