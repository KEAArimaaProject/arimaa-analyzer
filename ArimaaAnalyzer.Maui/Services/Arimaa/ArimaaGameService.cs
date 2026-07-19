using System.Collections.Generic;
using ArimaaAnalyzer.Maui.Models;
using ArimaaAnalyzer.Maui.Services; // for Sides

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

    // Currently selected game metadata (if loaded from a GameRecord)
    public GameRecord? SelectedRecord { get; private set; }

    public event Action? SelectedRecordChanged;

    // Raised whenever the CurrentNode changes (e.g., navigation or commit)
    public event Action? CurrentNodeChanged;

    // Snapshots of the state across user-made steps (index 0 = state when the node was loaded).
    // Each successful single-step user move appends a new snapshot.
    private List<GameState>? _snapshots;

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
        // Ensure snapshots are initialized so pending-move logic works even if Load wasn't called recently
        if (_snapshots is null)
        {
            var orientation = State?.boardorientation ?? BoardOrientation.GoldSouthSilverNorth;
            _snapshots = new List<GameState>
            {
                new GameState(State.localAeiSetPosition) { boardorientation = orientation }
            };
        }

        var success = State.TryMove(from, to);
        if (success)
        {
            // After a successful user move, apply trap captures via CorrectMoveService
            CorrectMoveService.ApplyTrapCaptures(State);
            OnStateMutated();
        }
        return success;
    }

    // Place a piece from the editpieces palette onto the board
    public bool TryPlace(Position to, char pieceChar)
    {
        if (!to.IsOnBoard) return false;
        if (_snapshots is null)
        {
            var orientation = State?.boardorientation ?? BoardOrientation.GoldSouthSilverNorth;
            _snapshots = new List<GameState>
            {
                new GameState(State.localAeiSetPosition) { boardorientation = orientation }
            };
        }

        var success = State.TryPlace(to, pieceChar);
        if (success)
        {
            CorrectMoveService.ApplyTrapCaptures(State);
            OnStateMutated();
        }
        return success;
    }

    // Remove a piece (used when dragging from board back to editpieces)
    public void RemovePieceAt(Position p)
    {
        if (!p.IsOnBoard) return;
        if (_snapshots is null)
        {
            var orientation = State?.boardorientation ?? BoardOrientation.GoldSouthSilverNorth;
            _snapshots = new List<GameState>
            {
                new GameState(State.localAeiSetPosition) { boardorientation = orientation }
            };
        }

        State.RemovePieceAt(p);
        CorrectMoveService.ApplyTrapCaptures(State);
        OnStateMutated();
    }

    /// <summary>
    /// Replace the entire board from a 64-char normalized string (spaces for empty).
    /// Used by edit-mode auto-setup and similar bulk placement.
    /// </summary>
    public void ApplyNormalizedBoard(string normalizedBoard64)
    {
        if (normalizedBoard64 is null || normalizedBoard64.Length != 64)
            throw new ArgumentException("Board string must be exactly 64 characters.", nameof(normalizedBoard64));

        if (_snapshots is null)
        {
            var orientation = State?.boardorientation ?? BoardOrientation.GoldSouthSilverNorth;
            _snapshots = new List<GameState>
            {
                new GameState(State.localAeiSetPosition) { boardorientation = orientation }
            };
        }

        State.SetNormalizedBoard(normalizedBoard64);
        CorrectMoveService.ApplyTrapCaptures(State);
        OnStateMutated();
    }

    public const string GoldSetupLabel = "setup (Gold to move)";
    public const string SilverSetupLabel = "setup (Silver to move)";

    /// <summary>
    /// Auto setup (gold): wipe board and place classic 99of9 gold on ranks 1–2.
    /// Live board only — finalize with <see cref="TryCommitSetupPhase"/> (HumanMove).
    /// </summary>
    public void ApplyGoldAutoSetupLive()
    {
        ApplyNormalizedBoard(ArimaaStandardSetups.NinetyNineOfNineGoldOnly());
    }

    /// <summary>
    /// Auto setup (silver): overlay classic 99of9 silver on ranks 7–8 of the live board.
    /// Live board only — finalize with <see cref="TryCommitSetupPhase"/> (HumanMove).
    /// </summary>
    public void ApplySilverAutoSetupLive()
    {
        var current = State.GetNormalizedBoardString() ?? new string(' ', 64);
        ApplyNormalizedBoard(ArimaaStandardSetups.WithNinetyNineOfNineSilver(current));
    }

    /// <summary>
    /// HumanMove setup commits:
    /// <list type="bullet">
    /// <item>Gold-only on home ranks → one child under the tree root labeled gold setup.</item>
    /// <item>Live board added silver on top of a gold-setup node → one child under current, silver setup.</item>
    /// </list>
    /// Returns true if a setup phase was handled (including no-op when already committed).
    /// Returns false so the caller can run normal mid-game HumanMove (side flip).
    /// </summary>
    public bool TryCommitSetupPhase()
    {
        var liveBoard = State.GetNormalizedBoardString();
        if (liveBoard is null || liveBoard.Length != 64)
            return false;

        // Silver setup finalize: current node is gold-only setup; live board has silver pieces.
        if (CurrentNode is not null
            && BoardFromAei(CurrentNode.AEIstring) is { } nodeBoard
            && ArimaaStandardSetups.IsGoldOnlyOnHomeRanks(nodeBoard)
            && BoardHasSilver(liveBoard)
            && !ArimaaStandardSetups.IsGoldOnlyOnHomeRanks(liveBoard))
        {
            CommitSilverSetup(liveBoard);
            return true;
        }

        // Gold setup finalize: live board is gold-only on home ranks
        if (ArimaaStandardSetups.IsGoldOnlyOnHomeRanks(liveBoard))
        {
            // Already sitting on a matching gold-setup node — nothing to commit
            if (CurrentNode is not null
                && BoardFromAei(CurrentNode.AEIstring) is { } curBoard
                && ArimaaStandardSetups.IsGoldOnlyOnHomeRanks(curBoard)
                && SameBoardPayload(CurrentNode.AEIstring, State.localAeiSetPosition))
            {
                return true;
            }

            CommitGoldSetupUnderRoot(liveBoard);
            return true;
        }

        return false;
    }

    private void CommitGoldSetupUnderRoot(string goldBoard64)
    {
        // After gold places pieces, silver is next to set up (AEI side). The node still
        // represents gold's setup phase for labeling (Side = Gold, Moves = gold setup label).
        var goldAei = NormalizedBoardToAei(goldBoard64, Sides.Silver);

        var root = CurrentNode is not null ? GetRoot(CurrentNode) : null;
        if (root is null)
        {
            var orientation = State?.boardorientation ?? BoardOrientation.GoldSouthSilverNorth;
            State = new GameState(goldAei) { boardorientation = orientation };
            _snapshots = new List<GameState>
            {
                new GameState(goldAei) { boardorientation = orientation }
            };
            StateChanged?.Invoke();
            return;
        }

        var child = new GameTurn(
            oldAEIstring: root.AEIstring,
            updatedAEIstring: goldAei,
            MoveNumber: root.MoveNumber,
            Side: Sides.Gold,
            Moves: new List<string> { GoldSetupLabel },
            isMainLine: false);

        root.AddChild(child);
        Load(child);
    }

    private void CommitSilverSetup(string fullBoard64)
    {
        if (CurrentNode is null) return;

        // After silver setup, gold plays first
        var silverAei = NormalizedBoardToAei(fullBoard64, Sides.Gold);

        if (string.Equals(CurrentNode.AEIstring, silverAei, StringComparison.Ordinal))
            return;

        var child = new GameTurn(
            oldAEIstring: CurrentNode.AEIstring,
            updatedAEIstring: silverAei,
            MoveNumber: CurrentNode.MoveNumber,
            Side: Sides.Silver,
            Moves: new List<string> { SilverSetupLabel },
            isMainLine: false);

        CurrentNode.AddChild(child);
        Load(child);
    }

    private static bool BoardHasSilver(string board64)
    {
        for (var i = 0; i < board64.Length; i++)
        {
            var ch = board64[i];
            if (ch != ' ' && char.IsLower(ch))
                return true;
        }
        return false;
    }

    private static string? BoardFromAei(string aei)
    {
        try
        {
            return new GameState(aei).GetNormalizedBoardString();
        }
        catch
        {
            return null;
        }
    }

    private static bool SameBoardPayload(string aeiA, string aeiB)
    {
        var a = ExtractBoardPayload(aeiA);
        var b = ExtractBoardPayload(aeiB);
        return a is not null && b is not null && string.Equals(a, b, StringComparison.Ordinal);
    }

    private static string? ExtractBoardPayload(string aei)
    {
        if (string.IsNullOrWhiteSpace(aei)) return null;
        var first = aei.IndexOf('"');
        var last = aei.LastIndexOf('"');
        if (first < 0 || last <= first) return null;
        var payload = aei.Substring(first + 1, last - first - 1);
        return payload.Length == 64 ? payload : null;
    }

    private static string NormalizedBoardToAei(string normalizedBoard64, Sides side)
    {
        var rows = new string[8];
        for (var r = 0; r < 8; r++)
            rows[r] = normalizedBoard64.Substring(r * 8, 8).Replace(' ', '.');
        return NotationService.BoardToAei(rows, side);
    }

    public void ClearSelection() => Selected = null;

    // Load a GameTurn node and update the underlying GameState accordingly
    public void Load(GameTurn node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        CurrentNode = node;
        //System.Console.WriteLine($"[DEBUG_LOG] Load: node Move#={node.MoveNumber}, isMain={node.IsMainLine}, children={node.Children?.Count ?? 0}");
        // Preserve current board orientation across loads so UI rotation stays consistent
        // Default to canonical orientation: GoldSouth (bottom) vs SilverNorth (top)
        var orientation = State?.boardorientation ?? BoardOrientation.GoldSouthSilverNorth;
        State = new GameState(node);
        State.boardorientation = orientation;
        // Initialize snapshots for pending-move computation (index 0 = loaded state)
        _snapshots = new List<GameState>
        {
            new GameState(node) { boardorientation = orientation }
        };

        // Notify listeners that the active node has changed
        CurrentNodeChanged?.Invoke();
        // Also notify that the underlying board state changed so views like ArimaaBoard re-render
        StateChanged?.Invoke();
    }

    public void SetSelectedRecord(GameRecord? record)
    {
        if (!ReferenceEquals(SelectedRecord, record))
        {
            SelectedRecord = record;
            SelectedRecordChanged?.Invoke();
        }
    }

    public bool CanPrev => CurrentNode?.Parent is not null;
    public bool CanNext => CurrentNode?.Children is { Count: > 0 };
    public bool CanGoToStart => CurrentNode?.Parent is not null;

    public bool CanGoToEnd
    {
        get
        {
            if (CurrentNode is null) return false;
            var leaf = GetMainLineLeaf(GetRoot(CurrentNode));
            return leaf is not null && !ReferenceEquals(leaf, CurrentNode);
        }
    }

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

    /// <summary>
    /// Jump to the game root (position before the first move).
    /// </summary>
    public void GoToStart()
    {
        if (CurrentNode is null) return;
        var root = GetRoot(CurrentNode);
        if (root is not null && !ReferenceEquals(root, CurrentNode))
            Load(root);
    }

    /// <summary>
    /// Jump to the last position on the main line.
    /// </summary>
    public void GoToEnd()
    {
        if (CurrentNode is null) return;
        var root = GetRoot(CurrentNode);
        var leaf = GetMainLineLeaf(root);
        if (leaf is not null && !ReferenceEquals(leaf, CurrentNode))
            Load(leaf);
    }

    private static GameTurn GetRoot(GameTurn node)
    {
        while (node.Parent is not null)
            node = node.Parent;
        return node;
    }

    private static GameTurn? GetMainLineLeaf(GameTurn? node)
    {
        if (node is null) return null;
        while (true)
        {
            var next = node.Children.FirstOrDefault(c => c.IsMainLine) ?? node.Children.FirstOrDefault();
            if (next is null) break;
            node = next;
        }
        return node;
    }

    // Indicates whether the board has diverged from the loaded node (i.e., there is a pending move to commit)
    public bool CanCommitMove => CurrentNode is not null && _snapshots is { Count: >= 2 } &&
                                 !string.Equals(_snapshots[0].localAeiSetPosition, State.localAeiSetPosition, StringComparison.Ordinal);

    // Alias for UI clarity: we can reset whenever we have uncommitted changes
    public bool CanResetPendingMoves => CanCommitMove;

    // Human-readable notation of the pending move sequence (null when none)
    public string? PendingMoveText
    {
        get
        {
            if (!CanResetPendingMoves || _snapshots is null || _snapshots.Count < 2) return null;
            var notation = CorrectMoveService.ComputeMoveSequenceFromSnapshots(_snapshots);
            if (string.IsNullOrWhiteSpace(notation.Item1) || notation.Item2 == "error") return null;
            return notation.Item1;
        }
    }

    // Create a new non-mainline child node from the pending move(s) and move the current position to that child
    public bool CommitMove()
    {
        if (!CanCommitMove || CurrentNode is null || _snapshots is null) return false;

        // Compute the official move notation based on the accumulated snapshots
        var sideToMove = _snapshots[0].SideToMove;
        var notation = CorrectMoveService.ComputeMoveSequenceFromSnapshots(_snapshots);
        if (string.IsNullOrWhiteSpace(notation.Item1) || notation.Item2 == "error")
        {
            return false;
        }

        // Determine side in Sides enum
        var sidesEnum = sideToMove == Sides.Gold ? Sides.Gold : Sides.Silver;

        // Determine the move number string
        var moveNumberStr = CurrentNode.MoveNumber;
        if (int.TryParse(CurrentNode.MoveNumber, out var curNum))
        {
            // Increment when a new Gold move starts a new full number (assuming Silver completes the previous)
            // Heuristic: if side to move is Gold, increment; else keep same number
            var newNum = sideToMove == Sides.Gold ? curNum + 1 : curNum;
            moveNumberStr = newNum.ToString();
        }

        // Build child turn with IsMainLine = false
        var child = new GameTurn(CurrentNode.AEIstring, notation.Item2, moveNumberStr, sidesEnum, new List<string> { notation.Item1 }, isMainLine: false);
        CurrentNode.AddChild(child);
        System.Console.WriteLine($"[DEBUG_LOG] CommitMove: created child under Move#={CurrentNode.MoveNumber}. Parent now has {CurrentNode.Children.Count} children");

        // Load the newly created variation node
        Load(child);
        return true;
    }

    // Revert the board to the state when the current node was loaded (discard uncommitted moves)
    public void ResetPendingMoves()
    {
        if (CurrentNode is null || _snapshots is null || _snapshots.Count == 0) return;

        // Preserve current orientation while restoring the loaded snapshot
        var orientation = State?.boardorientation ?? BoardOrientation.GoldSouthSilverNorth;
        State = new GameState(_snapshots[0].localAeiSetPosition)
        {
            boardorientation = orientation
        };

        // Reset snapshots back to the initial state only
        _snapshots = new List<GameState> { new GameState(State.localAeiSetPosition) { boardorientation = orientation } };

        // Clear any UI selection
        Selected = null;

        // Notify listeners that state (and thus pending moves) changed
        StateChanged?.Invoke();
    }

    // Rotate the board orientation clockwise through the defined enum values
    public void RotateBoardClockwise()
    {
        var current = State.boardorientation;
        BoardOrientation next = current switch
        {
            BoardOrientation.GoldSouthSilverNorth => BoardOrientation.GoldWestSilverEast,
            BoardOrientation.GoldWestSilverEast => BoardOrientation.GoldNorthSilverSouth,
            BoardOrientation.GoldNorthSilverSouth => BoardOrientation.GoldEastSilverWest,
            _ => BoardOrientation.GoldSouthSilverNorth
        };
        State.boardorientation = next;
    }

    // Raised when the State changes without changing the CurrentNode (e.g., user makes or resets pending moves)
    public event Action? StateChanged;

    // Internal helper to raise StateChanged after successful state mutation
    private void OnStateMutated()
    {
        // Append a new snapshot capturing the current state after mutation
        if (_snapshots is not null)
        {
            var orientation = State?.boardorientation ?? BoardOrientation.GoldSouthSilverNorth;
            _snapshots.Add(new GameState(State.localAeiSetPosition) { boardorientation = orientation });
        }
        StateChanged?.Invoke();
    }

    // (No other duplicate TryMove definitions)
}
