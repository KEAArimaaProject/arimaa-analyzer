namespace ArimaaAnalyzer.Maui.Components;

/// <summary>
/// Controls which boards display move-path overlays.
/// </summary>
public enum MovePathTarget
{
    /// <summary>Only the main game board shows move paths.</summary>
    MainOnly = 0,

    /// <summary>Only analysis mini-boards show move paths (default).</summary>
    AnalysisOnly = 1,

    /// <summary>Both main and analysis boards show move paths.</summary>
    Both = 2,

    /// <summary>No boards show move paths.</summary>
    Neither = 3,
}
