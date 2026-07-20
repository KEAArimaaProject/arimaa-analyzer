using System;
using System.Collections.Generic;
using System.Linq;
using ArimaaAnalyzer.Maui.Models;

namespace ArimaaAnalyzer.Maui.Services;

/// <summary>
/// Maps between live <see cref="GameTurn"/> graphs and serializable <see cref="GameTurnDto"/> trees.
/// </summary>
public static class GameTurnTreeMapper
{
    public static GameTurnDto ToDto(GameTurn node)
    {
        if (node is null) throw new ArgumentNullException(nameof(node));

        return new GameTurnDto
        {
            MoveNumber = node.MoveNumber ?? "0",
            Side = node.Side,
            Moves = (node.Moves ?? Array.Empty<string>()).ToList(),
            IsMainLine = node.IsMainLine,
            AEIstring = node.AEIstring ?? string.Empty,
            Children = node.Children.Select(ToDto).ToList(),
        };
    }

    /// <summary>
    /// Materialize a new <see cref="GameTurn"/> tree (Parents wired via <see cref="GameTurn.AddChild"/>).
    /// </summary>
    public static GameTurn FromDto(GameTurnDto dto)
    {
        if (dto is null) throw new ArgumentNullException(nameof(dto));
        return FromDto(dto, parentIsMainLine: true);
    }

    private static GameTurn FromDto(GameTurnDto dto, bool parentIsMainLine)
    {
        // Enforce main-line parent constraint when materializing.
        var isMainLine = parentIsMainLine && dto.IsMainLine;
        var moves = dto.Moves is { Count: > 0 }
            ? (IReadOnlyList<string>)dto.Moves
            : Array.Empty<string>();

        var aei = dto.AEIstring ?? string.Empty;
        var node = new GameTurn(
            oldAEIstring: aei,
            updatedAEIstring: aei,
            MoveNumber: string.IsNullOrWhiteSpace(dto.MoveNumber) ? "0" : dto.MoveNumber,
            Side: dto.Side,
            Moves: moves,
            isMainLine: isMainLine);

        foreach (var childDto in dto.Children ?? Enumerable.Empty<GameTurnDto>())
            node.AddChild(FromDto(childDto, parentIsMainLine: isMainLine));

        return node;
    }
}
