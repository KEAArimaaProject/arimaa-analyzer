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

/// <summary>Display labels for <see cref="MovePathTarget"/> in compact UI controls.</summary>
public static class MovePathTargetDisplay
{
    public static string Full(MovePathTarget target) => target switch
    {
        MovePathTarget.MainOnly => "Main board has MovePath",
        MovePathTarget.AnalysisOnly => "Analysis boards have MovePath",
        MovePathTarget.Both => "Both have MovePath",
        MovePathTarget.Neither => "Neither have MovePath",
        _ => target.ToString(),
    };

    public static string Short(MovePathTarget target) => target switch
    {
        MovePathTarget.MainOnly => "Main",
        MovePathTarget.AnalysisOnly => "Anal",
        MovePathTarget.Both => "Both",
        MovePathTarget.Neither => "None",
        _ => target.ToString(),
    };
}
