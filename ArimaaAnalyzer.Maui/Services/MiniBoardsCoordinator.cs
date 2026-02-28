using System.Collections.Generic;
using ArimaaAnalyzer.Maui.Models;
using ArimaaAnalyzer.Maui.Services.Arimaa;

namespace ArimaaAnalyzer.Maui.Services;

/// <summary>
/// A lightweight coordinator that keeps references to the mini-board game services
/// and allows other components (e.g., MainLayout) to distribute generated positions to them.
/// </summary>
public sealed class MiniBoardsCoordinator
{
    private readonly List<ArimaaGameService> _miniGames = new();

    public int Count => _miniGames.Count;

    public void SetMiniGames(IReadOnlyList<ArimaaGameService> services)
    {
        _miniGames.Clear();
        _miniGames.AddRange(services);
    }

    /// <summary>
    /// Load the provided GameTurn nodes into mini-board services in order.
    /// Only the first min(Count, nodes.Count) items are applied.
    /// </summary>
    public void LoadNodes(IReadOnlyList<GameTurn> nodes)
    {
        if (nodes == null || nodes.Count == 0 || _miniGames.Count == 0) return;
        var n = System.Math.Min(_miniGames.Count, nodes.Count);
        for (int i = 0; i < n; i++)
        {
            var node = nodes[i];
            var svc = _miniGames[i];
            if (node != null && svc != null)
            {
                svc.Load(node);
            }
        }
    }

    public void Clear()
    {
        _miniGames.Clear();
    }
}
