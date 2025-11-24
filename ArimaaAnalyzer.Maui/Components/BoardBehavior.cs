namespace ArimaaAnalyzer.Maui.Components;

public class BoardBehavior
{
    public bool IsInteractive { get; init; }
    

    /// <summary>
    /// Standard board for human play.
    /// </summary>
    public static BoardBehavior Playable => new() 
    { 
        IsInteractive = true, 
       
    };

    /// <summary>
    /// Read-only board for displaying AI moves or history.
    /// </summary>
    public static BoardBehavior Spectator => new() 
    { 
        IsInteractive = false, 
        
    };
}