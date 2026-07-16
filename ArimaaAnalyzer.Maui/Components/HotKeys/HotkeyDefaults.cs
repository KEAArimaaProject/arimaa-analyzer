namespace ArimaaAnalyzer.Maui.Components.HotKeys;

public static class HotkeyDefaults
{
    public static IReadOnlyList<HotkeyBinding> Bindings { get; } =
    [
        new() { Key = "j", Action = HotkeyAction.PrevTurn },
        new() { Key = "l", Action = HotkeyAction.NextTurn },
        new() { Key = "i", Action = HotkeyAction.GoToStart },
        new() { Key = "k", Action = HotkeyAction.GoToEnd },
        new() { Key = "g", Action = HotkeyAction.AnalyzeGold },
        new() { Key = "s", Action = HotkeyAction.AnalyzeSilver },
    ];
}