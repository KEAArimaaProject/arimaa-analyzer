using System;

namespace ArimaaAnalyzer.Maui.Models;

/// <summary>
/// A game previously saved to the local Load game list (tree-backed storage).
/// </summary>
public sealed class PastedGameEntry
{
    public const int NameMinLength = 5;
    public const int NameMaxLength = 30;

    /// <summary>Current on-disk format: tree in <see cref="Root"/>.</summary>
    public const int CurrentFormatVersion = 2;

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User-chosen unique display name (5–30 characters).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When the game was added to the local library (UTC).</summary>
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Storage format version (2 = <see cref="Root"/> tree).</summary>
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>
    /// Serialized game tree (source of truth for open/reload).
    /// </summary>
    public GameTurnDto? Root { get; set; }

    /// <summary>
    /// Optional original paste text (Load game manual) for reference only.
    /// </summary>
    public string? SourceNotation { get; set; }

    /// <summary>Local display label: yyyyMMdd_HH:mm</summary>
    public string DisplayTimestamp =>
        AddedAt.ToLocalTime().ToString("yyyyMMdd_HH:mm");
}
