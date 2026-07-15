using System.Collections.Generic;
using ArimaaAnalyzer.Maui.Services.Arimaa;

namespace ArimaaAnalyzer.Maui.Components.MovePath;

/// <summary>
/// Path taken by a single piece during a turn, in display-space coordinates.
/// </summary>
public sealed class PieceMovePath
{
    public PieceMovePath(IReadOnlyList<Position> squares, string color, int orderIndex)
    {
        Squares = squares;
        Color = color;
        OrderIndex = orderIndex;
    }

    /// <summary>
    /// Ordered display-space squares the piece occupied (start, intermediate, end).
    /// Consecutive squares are always orthogonally adjacent.
    /// </summary>
    public IReadOnlyList<Position> Squares { get; }

    /// <summary>
    /// Hex color for this piece's path (from <see cref="MovePathPalette"/>).
    /// </summary>
    public string Color { get; }

    /// <summary>
    /// 0-based order in which this piece first moved during the turn.
    /// </summary>
    public int OrderIndex { get; }
}
