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

    /// <summary>
    /// Immediately load a single node into the specified mini board index.
    /// Safe no-op if index is out of range.
    /// </summary>
    public void SetNode(int index, GameTurn node)
    {
        if (node == null) return;
        if (index < 0 || index >= _miniGames.Count) return;
        var svc = _miniGames[index];
        svc?.Load(node);
    }

    public void Clear()
    {
        _miniGames.Clear();
    }

    /// <summary>
    /// Wipe all mini boards to a neutral empty position so users can clearly see
    /// when new analysis results arrive. Keeps the registered mini-board services intact.
    /// </summary>
    public void WipeAll()
    {
        if (_miniGames.Count == 0) return;

        // Build an empty board AEI with Gold to move, then wrap it into a GameTurn
        // so we can reuse the normal Load path of each mini board service.
        var emptyAei = NotationService.BoardToAei(NotationService.InitializeEmptyBoard(), Sides.Gold);
        var blankNode = new GameTurn(oldAEIstring: emptyAei,
                                     updatedAEIstring: emptyAei,
                                     MoveNumber: "0",
                                     Side: Sides.Gold,
                                     Moves: Array.Empty<string>(),
                                     isMainLine: true);

        foreach (var svc in _miniGames)
        {
            svc?.Load(blankNode);
        }
    }
}
