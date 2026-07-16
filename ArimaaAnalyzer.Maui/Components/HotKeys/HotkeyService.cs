using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ArimaaAnalyzer.Maui.Components.HotKeys;

public sealed class HotkeyService
{
    private readonly Dictionary<string, HotkeyAction> _keyToAction;
    private readonly Dictionary<HotkeyAction, Func<Task>> _handlers = new();
    private readonly Dictionary<HotkeyAction, Func<bool>> _canExecute = new();

    public HotkeyService()
    {
        _keyToAction = new Dictionary<string, HotkeyAction>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in HotkeyDefaults.Bindings)
        {
            _keyToAction[binding.Key] = binding.Action;
        }
    }

    public void Register(HotkeyAction action, Func<Task> handler, Func<bool>? canExecute = null)
    {
        _handlers[action] = handler ?? throw new ArgumentNullException(nameof(handler));
        if (canExecute is null)
            _canExecute.Remove(action);
        else
            _canExecute[action] = canExecute;
    }

    public async Task<bool> TryHandleAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var normalized = key.Trim().ToLowerInvariant();
        if (!_keyToAction.TryGetValue(normalized, out var action))
            return false;

        if (_canExecute.TryGetValue(action, out var canExecute) && !canExecute())
            return false;

        if (!_handlers.TryGetValue(action, out var handler))
            return false;

        await handler();
        return true;
    }
}