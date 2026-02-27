using System;
using System.Collections.Generic;

namespace ArimaaAnalyzer.Maui.Models;

/// <summary>
/// Represents a single Arimaa game record parsed from a TSV export.
/// Includes metadata (players, ratings, time control, etc.),
/// move list, and optional event/log lines.
/// </summary>
public class GameRecord
{
    // Basic ids
    public long Id { get; init; }
    public int? WPlayerId { get; init; }
    public int? BPlayerId { get; init; }

    // Players
    public string? WUsername { get; init; }
    public string? BUsername { get; init; }
    public string? WTitle { get; init; }
    public string? BTitle { get; init; }
    public string? WCountry { get; init; }
    public string? BCountry { get; init; }

    // Ratings
    public int? WRating { get; init; }
    public int? BRating { get; init; }
    public int? WRatingK { get; init; }
    public int? BRatingK { get; init; }

    // Player types (b: bot, h: human) if provided
    public PlayerType? WType { get; init; }
    public PlayerType? BType { get; init; }

    // Context
    public string? Event { get; init; }
    
    public IReadOnlyList<string> EventLines =>
        string.IsNullOrEmpty(Event)
            ? Array.Empty<string>()
            : Event.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.TrimEnd('\r', '\n'))   // just in case
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
    public string? Site { get; init; }
    public string? TimeControl { get; init; }

    // Flags/Numbers
    public int? Postal { get; init; }
    public DateTimeOffset? StartTs { get; init; }
    public DateTimeOffset? EndTs { get; init; }
    public Side? ResultSide { get; init; }
    public GameTermination? ResultTermination { get; init; }
    public int? PlyCount { get; init; }
    public string? Mode { get; init; }
    public bool? Rated { get; init; }
    public bool? Corrupt { get; init; }

    // Moves and events/logs
    public string? MoveListRaw { get; init; }
    
    /// <summary>
    /// Parsed turns derived from <see cref="MoveListRaw"/> by the data converter.
    /// If not set by the converter, this will be null.
    /// </summary>
    public IReadOnlyList<GameTurn>? Turns { get; init; }

    public string? OrdEvent { get; init; }

    public enum PlayerType { Human, Bot }
    public enum Side { Gold, Silver }
    public enum GameTermination
    {
        /// <summary>Resignation</summary>
        Resignation,
        /// <summary>Timeout/flag fall</summary>
        Timeout,
        /// <summary>Forfeit or administrative</summary>
        Forfeit,
        /// <summary>Goal (rabbit reaches goal line)</summary>
        Goal,
        /// <summary>Elimination (all rabbits captured)</summary>
        Elimination
    }
}
