namespace ArimaaAnalyzer.Maui.Components;

/// <summary>
/// Top-bar mode for game-tree child actions (delete, copy, etc.).
/// </summary>
public enum GameTreeActionMode
{
    /// <summary>No per-child action buttons.</summary>
    None = 0,

    /// <summary>Show delete buttons on each game-tree child.</summary>
    Delete = 1,

    /// <summary>Reserved for copy (not implemented yet).</summary>
    Copy = 2,
}

/// <summary>Display labels for <see cref="GameTreeActionMode"/> in compact UI controls.</summary>
public static class GameTreeActionModeDisplay
{
    public static string Full(GameTreeActionMode mode) => mode switch
    {
        GameTreeActionMode.None => "None",
        GameTreeActionMode.Delete => "Delete",
        GameTreeActionMode.Copy => "Copy",
        _ => mode.ToString(),
    };

    public static string Short(GameTreeActionMode mode) => mode switch
    {
        GameTreeActionMode.None => "N",
        GameTreeActionMode.Delete => "D",
        GameTreeActionMode.Copy => "C",
        _ => mode.ToString(),
    };
}
