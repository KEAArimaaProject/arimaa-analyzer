using YourApp.Models;

namespace ArimaaAnalyzer.Maui.Services.Arimaa;

// Simple stateful service to drive the UI. Keeps a selected square and exposes move methods.
public sealed class ArimaaGameService
{
    public ArimaaGameService(GameState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public GameState State { get; private set; }

    public GameTurn? CurrentNode { get; private set; }

    public Position? Selected { get; private set; }

    // Controls whether the board component renders outer UI around the inner 8x8 grid.
    // When false, the outer size matches the inner board size.
    public bool ShowOuterUi { get; set; } = false;

    public void Select(Position p)
    {
        if (!p.IsOnBoard) return;
        var piece = State.GetPiece(p);
        if (piece is not null)
        {
            Selected = p;
        }
    }

    // Drag-and-drop entry point: move directly from a known origin to target.
    public bool TryMove(Position from, Position to)
    {
        if (!from.IsOnBoard || !to.IsOnBoard) return false;
        var success = State.TryMove(from, to);
        if (success)
        {
            // After a successful user move, apply trap captures via CorrectMoveService
            CorrectMoveService.ApplyTrapCaptures(State);
        }
        return success;
    }

    public void ClearSelection() => Selected = null;

    // Load a GameTurn node and update the underlying GameState accordingly
    public void Load(GameTurn node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        CurrentNode = node;
        State = new GameState(node);
    }

    public bool CanPrev => CurrentNode?.Parent is not null;
    public bool CanNext => CurrentNode?.Children is { Count: > 0 };

    public void GoPrev()
    {
        if (CurrentNode?.Parent is { } p)
        {
            Load(p);
        }
    }

    public void GoNextMainLine()
    {
        if (CurrentNode is null) return;
        var next = CurrentNode.Children.FirstOrDefault(c => c.IsMainLine) ?? CurrentNode.Children.FirstOrDefault();
        if (next != null)
        {
            Load(next);
        }
    }
}
