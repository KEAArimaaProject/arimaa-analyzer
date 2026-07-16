namespace ArimaaAnalyzer.Maui.Components.HotKeys;

public sealed class HotkeyBinding
{
    public required string Key { get; init; }
    public required HotkeyAction Action { get; init; }
}