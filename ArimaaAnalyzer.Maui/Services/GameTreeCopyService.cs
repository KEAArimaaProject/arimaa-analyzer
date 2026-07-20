using System.Text;
using System.Text.RegularExpressions;
using ArimaaAnalyzer.Maui.Models;
using ArimaaAnalyzer.Maui.Services.Arimaa;

namespace ArimaaAnalyzer.Maui.Services;

/// <summary>
/// Copy / export helpers for game-tree slices.
/// Copy-to-library keeps ancestors of the selected node, the node, and <b>all</b> of its
/// descendants; uncles (siblings of the path) are excluded.
/// </summary>
public static class GameTreeCopyService
{
    /// <summary>
    /// Build a detached tree: path root→<paramref name="selected"/> (no uncles),
    /// plus a full deep clone of every descendant under <paramref name="selected"/>.
    /// </summary>
    public static GameTurn ClonePathWithFullSubtree(GameTurn selected)
    {
        if (selected is null) throw new ArgumentNullException(nameof(selected));

        var path = GetPathFromRoot(selected);
        if (path.Count == 0)
            throw new InvalidOperationException("Selected node has an empty path.");

        GameTurn? newRoot = null;
        GameTurn? pathCursor = null;

        for (var i = 0; i < path.Count; i++)
        {
            var source = path[i];
            var isSelected = ReferenceEquals(source, selected) || i == path.Count - 1;

            // Path spine is the main line of the saved game.
            var clone = CloneNodeShallow(source, forceMainLine: true);

            if (newRoot is null)
            {
                newRoot = clone;
                pathCursor = clone;
            }
            else
            {
                pathCursor!.AddChild(clone);
                pathCursor = clone;
            }

            if (isSelected)
            {
                // Full branching under the selected node (not mainline-only).
                var children = source.Children;
                var anyMain = children.Any(c => c.IsMainLine);
                for (var c = 0; c < children.Count; c++)
                {
                    var child = children[c];
                    var childMain = child.IsMainLine || (!anyMain && c == 0);
                    pathCursor.AddChild(CloneSubtree(child, forceMainLine: childMain));
                }
            }
        }

        return newRoot!;
    }

    /// <summary>
    /// Deep-clone a subtree, preserving branch structure. Main-line flags are coerced
    /// so a main-line child is never attached under a non-main-line parent.
    /// </summary>
    public static GameTurn CloneSubtree(GameTurn source, bool forceMainLine)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var clone = CloneNodeShallow(source, forceMainLine);
        var children = source.Children;
        if (children.Count == 0) return clone;

        var anyMain = children.Any(c => c.IsMainLine);
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var childMain = forceMainLine && (child.IsMainLine || (!anyMain && i == 0));
            clone.AddChild(CloneSubtree(child, forceMainLine: childMain));
        }

        return clone;
    }

    private static GameTurn CloneNodeShallow(GameTurn source, bool forceMainLine)
    {
        // Prefer legal tokens; fall back to AEI-derived placements (setup labels, etc.).
        var moves = ResolveMoves(source);
        var moveList = moves.Count > 0 ? moves : Array.Empty<string>();
        var aei = source.AEIstring ?? string.Empty;
        var oldAei = source.Parent?.AEIstring ?? aei;

        return new GameTurn(
            oldAEIstring: oldAei,
            updatedAEIstring: aei,
            MoveNumber: string.IsNullOrWhiteSpace(source.MoveNumber) ? "0" : source.MoveNumber,
            Side: source.Side,
            Moves: moveList,
            isMainLine: forceMainLine);
    }

    /// <summary>
    /// Legal setup / step / capture tokens (same shape as <see cref="NotationService.ValidatePastedGame"/>).
    /// </summary>
    private static readonly Regex LegalMoveToken = new(
        @"^[RCDHMErcdhme][a-h][1-8]([nsew]|x)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Build linear Arimaa game notation for the path root→<paramref name="selected"/>
    /// plus the preferred main-line continuation under <paramref name="selected"/>.
    /// </summary>
    /// <returns>Newline-separated turn lines (e.g. <c>1w Ra1 ...</c>), or null if nothing exportable.</returns>
    public static string? ToGameNotation(GameTurn selected)
    {
        if (selected is null) throw new ArgumentNullException(nameof(selected));

        var turns = CollectExportTurns(selected);
        if (turns.Count == 0) return null;

        var sb = new StringBuilder();
        foreach (var turn in turns)
        {
            var line = FormatTurnLine(turn);
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(line);
        }

        return sb.Length == 0 ? null : sb.ToString();
    }

    /// <summary>
    /// Nodes included in the export, in play order:
    /// path from root to <paramref name="selected"/> (excluding synthetic empty root),
    /// then preferred main-line descendants of <paramref name="selected"/>.
    /// </summary>
    public static IReadOnlyList<GameTurn> CollectExportTurns(GameTurn selected)
    {
        if (selected is null) throw new ArgumentNullException(nameof(selected));

        var path = GetPathFromRoot(selected);
        var result = new List<GameTurn>(path.Count + 8);

        foreach (var node in path)
        {
            if (IsSyntheticRoot(node)) continue;
            result.Add(node);
        }

        // Preferred continuation under selected (not siblings of selected).
        var cursor = PreferMainlineChild(selected);
        while (cursor is not null)
        {
            result.Add(cursor);
            cursor = PreferMainlineChild(cursor);
        }

        return result;
    }

    /// <summary>
    /// Root → … → <paramref name="node"/> (inclusive), oldest first.
    /// </summary>
    public static IReadOnlyList<GameTurn> GetPathFromRoot(GameTurn node)
    {
        if (node is null) throw new ArgumentNullException(nameof(node));

        var stack = new Stack<GameTurn>();
        for (var n = node; n is not null; n = n.Parent)
            stack.Push(n);

        return stack.ToList();
    }

    /// <summary>
    /// Prefer an <see cref="GameTurn.IsMainLine"/> child; otherwise the first child.
    /// </summary>
    public static GameTurn? PreferMainlineChild(GameTurn node)
    {
        if (node?.Children is not { Count: > 0 } children) return null;
        return children.FirstOrDefault(c => c.IsMainLine) ?? children[0];
    }

    /// <summary>
    /// Initial empty position node created by <see cref="NotationService.ExtractTurnsWithMoves"/>.
    /// </summary>
    public static bool IsSyntheticRoot(GameTurn node)
    {
        if (node is null) return false;
        if (node.Parent is not null) return false;
        if (node.Moves is { Count: > 0 }) return false;
        return string.Equals(node.MoveNumber, "0", StringComparison.Ordinal);
    }

    /// <summary>
    /// One turn line: <c>{number}{w|b} {moves...}</c>. Null if the turn has no resolvable moves.
    /// </summary>
    public static string? FormatTurnLine(GameTurn turn)
    {
        if (turn is null) return null;

        var moves = ResolveMoves(turn);
        if (moves.Count == 0) return null;

        var side = turn.Side == Sides.Gold ? "w" : "b";
        // Setup commits under the synthetic root keep MoveNumber "0"; export as "1" so
        // the result matches normal pasted games (1w setup / 1b setup / 2w ...).
        var rawNumber = string.IsNullOrWhiteSpace(turn.MoveNumber) ? "0" : turn.MoveNumber.Trim();
        var number = string.Equals(rawNumber, "0", StringComparison.Ordinal) ? "1" : rawNumber;
        return $"{number}{side} {string.Join(" ", moves)}";
    }

    /// <summary>
    /// Stored legal move tokens, or AEI-derived steps/placements when the list is empty
    /// or holds UI-only labels (e.g. <see cref="ArimaaGameService.GoldSetupLabel"/>).
    /// </summary>
    public static IReadOnlyList<string> ResolveMoves(GameTurn turn)
    {
        if (turn is null) return Array.Empty<string>();

        if (turn.Moves is { Count: > 0 } stored)
        {
            // CommitMove stores a whole turn as one list entry ("Ed2n Ed3n ..."); split to tokens.
            var cleaned = stored
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .SelectMany(m => m.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToArray();

            // Accept only real Arimaa tokens — reject "setup (Gold to move)" labels.
            if (cleaned.Length > 0 && cleaned.All(IsLegalMoveToken))
                return cleaned;
        }

        return ResolveMovesFromAei(turn);
    }

    /// <summary>
    /// Prefer step notation (play); fall back to placement tokens for setup-style board fills.
    /// </summary>
    private static IReadOnlyList<string> ResolveMovesFromAei(GameTurn turn)
    {
        var parent = turn.Parent;
        if (parent is null
            || string.IsNullOrWhiteSpace(parent.AEIstring)
            || string.IsNullOrWhiteSpace(turn.AEIstring))
        {
            return Array.Empty<string>();
        }

        try
        {
            var before = new GameState(parent.AEIstring);
            var after = new GameState(turn.AEIstring);
            var (notation, aei) = CorrectMoveService.ComputeMoveSequence(before, after);
            if (!string.IsNullOrWhiteSpace(notation)
                && !string.Equals(notation, "error", StringComparison.Ordinal)
                && !string.Equals(aei, "error", StringComparison.Ordinal))
            {
                var steps = notation
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(IsLegalMoveToken)
                    .ToArray();
                if (steps.Length > 0)
                    return steps;
            }
        }
        catch
        {
            /* fall through to placement diff */
        }

        return DiffBoardsToPlacementMoves(parent.AEIstring, turn.AEIstring);
    }

    public static bool IsLegalMoveToken(string? token) =>
        !string.IsNullOrWhiteSpace(token) && LegalMoveToken.IsMatch(token);

    /// <summary>
    /// Emit setup-style placement tokens for squares that gained (or changed) a piece
    /// from <paramref name="parentAei"/> to <paramref name="childAei"/> (e.g. gold/silver setup).
    /// </summary>
    public static IReadOnlyList<string> DiffBoardsToPlacementMoves(string parentAei, string childAei)
    {
        var before = ExtractBoardPayload(parentAei);
        var after = ExtractBoardPayload(childAei);
        if (before is null || after is null || before.Length != 64 || after.Length != 64)
            return Array.Empty<string>();

        var list = new List<string>(16);
        for (var i = 0; i < 64; i++)
        {
            var a = after[i];
            var b = before[i];
            if (a == b) continue;
            if (a is ' ' or '.') continue;
            if (!IsPieceLetter(a)) continue;

            var row = i / 8;
            var col = i % 8;
            var file = (char)('a' + col);
            var rank = 8 - row;
            list.Add($"{a}{file}{rank}");
        }

        return list;
    }

    private static bool IsPieceLetter(char c) =>
        c is 'R' or 'C' or 'D' or 'H' or 'M' or 'E'
            or 'r' or 'c' or 'd' or 'h' or 'm' or 'e';

    private static string? ExtractBoardPayload(string aei)
    {
        if (string.IsNullOrWhiteSpace(aei)) return null;
        var first = aei.IndexOf('"');
        var last = aei.LastIndexOf('"');
        if (first < 0 || last <= first) return null;
        return aei.Substring(first + 1, last - first - 1);
    }
}
