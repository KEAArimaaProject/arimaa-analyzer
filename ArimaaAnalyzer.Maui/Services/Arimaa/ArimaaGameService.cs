namespace ArimaaAnalyzer.Maui.Services.Arimaa;

// Simple stateful service to drive the UI. Keeps a selected square and exposes move methods.
public sealed class ArimaaGameService
{
    public ArimaaGameService(GameState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public GameState State { get; }

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
}
