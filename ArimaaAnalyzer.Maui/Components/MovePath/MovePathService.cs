using System;
using System.Collections.Generic;
using System.Linq;
using ArimaaAnalyzer.Maui.Models;
using ArimaaAnalyzer.Maui.Services.Arimaa;

namespace ArimaaAnalyzer.Maui.Components.MovePath;

/// <summary>
/// Computes orthogonal move paths for pieces that moved during a turn,
/// assigning colors by first-move order.
/// </summary>
public static class MovePathService
{
    /// <summary>
    /// Build paths from a loaded <see cref="GameTurn"/> (uses its step list).
    /// </summary>
    public static IReadOnlyList<PieceMovePath> Compute(GameTurn? turn, BoardOrientation orientation)
    {
        if (turn is null || turn.Moves is null || turn.Moves.Count == 0)
            return Array.Empty<PieceMovePath>();

        return Compute(turn.Moves, orientation);
    }

    /// <summary>
    /// Build display-space paths from Arimaa step tokens (e.g. Ed2n, hb5s).
    /// Captures (…x) and setup placements (Ra1) are ignored for path geometry.
    /// </summary>
    public static IReadOnlyList<PieceMovePath> Compute(IReadOnlyList<string> moves, BoardOrientation orientation)
    {
        if (moves is null || moves.Count == 0)
            return Array.Empty<PieceMovePath>();

        // Track in-progress paths keyed by current end square (normalized) + piece char.
        // List order = first-move order for color assignment.
        var active = new List<PathBuilder>();

        foreach (var raw in moves)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var move = raw.Trim();

            if (!TryParseStep(move, out var piece, out var startNorm, out var endNorm))
                continue;

            // Continue an existing path if this step starts where that piece currently is.
            PathBuilder? builder = null;
            for (var i = 0; i < active.Count; i++)
            {
                var p = active[i];
                if (p.Piece == piece && p.EndNormalized == startNorm)
                {
                    builder = p;
                    break;
                }
            }

            if (builder is null)
            {
                builder = new PathBuilder(piece, startNorm, active.Count);
                active.Add(builder);
            }

            builder.AddStep(endNorm);
        }

        if (active.Count == 0)
            return Array.Empty<PieceMovePath>();

        var result = new List<PieceMovePath>(active.Count);
        foreach (var b in active)
        {
            if (b.NormalizedSquares.Count < 2)
                continue;

            var displaySquares = b.NormalizedSquares
                .Select(p =>
                {
                    var (dr, dc) = BoardRotationService.MapNormalizedToDisplay(p.Row, p.Col, orientation);
                    return new Position(dr, dc);
                })
                .ToList();

            result.Add(new PieceMovePath(
                displaySquares,
                MovePathPalette.GetColor(b.OrderIndex),
                b.OrderIndex));
        }

        return result;
    }

    /// <summary>
    /// Parse a single step token into piece + normalized start/end positions.
    /// Returns false for captures, setup, or invalid tokens.
    /// </summary>
    internal static bool TryParseStep(string move, out char piece, out Position start, out Position end)
    {
        piece = default;
        start = default;
        end = default;

        // Step: P[a-h][1-8][nsew]  (length 4). Captures end with 'x' — skip.
        if (move.Length != 4) return false;

        piece = move[0];
        var file = char.ToLowerInvariant(move[1]);
        var rank = move[2];
        var dir = char.ToLowerInvariant(move[3]);

        if (dir == 'x') return false; // capture marker, not a move segment
        if (file is < 'a' or > 'h') return false;
        if (rank is < '1' or > '8') return false;

        var col = file - 'a';
        var row = 8 - (rank - '0'); // rank 8 -> row 0, rank 1 -> row 7
        start = new Position(row, col);

        int dr = 0, dc = 0;
        switch (dir)
        {
            case 'n': dr = -1; break;
            case 's': dr = 1; break;
            case 'e': dc = 1; break;
            case 'w': dc = -1; break;
            default: return false;
        }

        end = new Position(row + dr, col + dc);
        if (!end.IsOnBoard) return false;
        return true;
    }

    private sealed class PathBuilder
    {
        public PathBuilder(char piece, Position startNormalized, int orderIndex)
        {
            Piece = piece;
            OrderIndex = orderIndex;
            NormalizedSquares = new List<Position> { startNormalized };
        }

        public char Piece { get; }
        public int OrderIndex { get; }
        public List<Position> NormalizedSquares { get; }
        public Position EndNormalized => NormalizedSquares[^1];

        public void AddStep(Position endNormalized)
        {
            // Each Arimaa step is already a single orthogonal step; append the landing square.
            NormalizedSquares.Add(endNormalized);
        }
    }
}
