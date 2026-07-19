using System;

namespace ArimaaAnalyzer.Maui.Models;

/// <summary>
/// A game notation previously pasted via Loadgamemanual, stored for later re-open.
/// </summary>
public sealed class PastedGameEntry
{
    public const int NameMinLength = 5;
    public const int NameMaxLength = 30;

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User-chosen unique display name (5–30 characters).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When the game was added to the local library (UTC).</summary>
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Full raw Arimaa game notation text.</summary>
    public string Notation { get; set; } = string.Empty;

    /// <summary>Local display label: yyyyMMdd_HH:mm</summary>
    public string DisplayTimestamp =>
        AddedAt.ToLocalTime().ToString("yyyyMMdd_HH:mm");
}