using System;
using System.Collections.Generic;
using System.Linq;

namespace ArimaaAnalyzer.Maui.Components.MovePath;

/// <summary>
/// A named set of four move-path line colors (one per first-move order, cycling if needed).
/// </summary>
public sealed class MovePathPaletteOption
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string[] Colors { get; init; }
}

/// <summary>
/// Curated move-path palette presets. Ordered for UI: app default first, then
/// accessibility-oriented palettes, then common visualization and stylistic sets.
/// </summary>
public static class MovePathPaletteOptions
{
    public const string DefaultId = "default";

    private static readonly MovePathPaletteOption[] Presets =
    [
        new()
        {
            Id = DefaultId,
            Name = "Default",
            Description = "Deep navy, vermillion, brown-red, and medium blue (app default).",
            Colors = ["#004488", "#BE0032", "#882D17", "#0067A5"],
        },
        new()
        {
            Id = "colorblind",
            Name = "Colorblind-safe",
            Description = "Okabe–Ito inspired palette for deuteranopia/protanopia accessibility.",
            Colors = ["#0072B2", "#D55E00", "#009E73", "#CC79A7"],
        },
        new()
        {
            Id = "high-contrast",
            Name = "High contrast",
            Description = "Strong primaries for maximum separation on light board squares.",
            Colors = ["#0000CC", "#CC0000", "#008800", "#CC6600"],
        },
        new()
        {
            Id = "tableau",
            Name = "Tableau",
            Description = "Widely used business-visualization colors (Tableau 10 subset).",
            Colors = ["#4E79A7", "#E15759", "#59A14F", "#F28E2B"],
        },
        new()
        {
            Id = "bold",
            Name = "Bold",
            Description = "Saturated modern primaries.",
            Colors = ["#2563EB", "#DC2626", "#16A34A", "#EA580C"],
        },
        new()
        {
            Id = "muted",
            Name = "Muted",
            Description = "Softer professional tones that stay readable on tan squares.",
            Colors = ["#5B8DBE", "#C75146", "#6B9E78", "#B8860B"],
        },
        new()
        {
            Id = "cool",
            Name = "Cool spectrum",
            Description = "Blue–violet–cyan range for a cohesive cool-toned look.",
            Colors = ["#1D4ED8", "#7C3AED", "#0891B2", "#4F46E5"],
        },
    ];

    public static IReadOnlyList<MovePathPaletteOption> All => Presets;

    public static MovePathPaletteOption Get(string? paletteId)
    {
        if (string.IsNullOrWhiteSpace(paletteId))
            return Presets[0];

        return Presets.FirstOrDefault(p => string.Equals(p.Id, paletteId, StringComparison.OrdinalIgnoreCase))
               ?? Presets[0];
    }

    public static string[] GetColors(string? paletteId) => Get(paletteId).Colors;
}