using System;
using System.Collections.Generic;
using ArimaaAnalyzer.Maui.Models;
using ArimaaAnalyzer.Maui.Services;

namespace ArimaaAnalyzer.Maui.DataAccess;

/// <summary>
/// Provides conversion/parsing helpers for data sources (e.g., TSV exports).
/// Keeps parsing logic separate from pure data models.
/// </summary>
public static class DataConverter
{
    
    public static string rawDataFilePath =
        Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "ArimaaAnalyzer.Maui", "Resources", "Raw", "allgames202602.txt");
    
    public static string GetDataFilePath()
    {
        return Path.GetFullPath(rawDataFilePath);
    }
    
    /// <summary>
    /// Parse a TSV header + line into a <see cref="GameRecord"/>.
    /// Unknown/missing fields are left null.
    /// </summary>
    public static GameRecord FromTsv(string header, string line)
    {
        if (header is null) throw new ArgumentNullException(nameof(header));
        if (line is null) throw new ArgumentNullException(nameof(line));

        // Split by tab; preserve empty entries.
        var cols = line.Split('\t');
        var names = header.Split('\t');

        string Get(string key)
        {
            for (int i = 0; i < names.Length && i < cols.Length; i++)
            {
                if (string.Equals(names[i], key, StringComparison.OrdinalIgnoreCase))
                    return cols[i];
            }
            return string.Empty;
        }

        return new GameRecord
        {
            Id = TryLong(Get("id")),
            WPlayerId = TryIntN(Get("wplayerid")),
            BPlayerId = TryIntN(Get("bplayerid")),

            WUsername = EmptyToNull(Get("wusername")),
            BUsername = EmptyToNull(Get("busername")),
            WTitle = EmptyToNull(Get("wtitle")),
            BTitle = EmptyToNull(Get("btitle")),
            WCountry = EmptyToNull(Get("wcountry")),
            BCountry = EmptyToNull(Get("bcountry")),

            WRating = TryIntN(Get("wrating")),
            BRating = TryIntN(Get("brating")),
            WRatingK = TryIntN(Get("wratingk")),
            BRatingK = TryIntN(Get("bratingk")),

            WType = ParsePlayerType(Get("wtype")),
            BType = ParsePlayerType(Get("btype")),

            Event = EmptyToNull(Get("event")),
            Site = EmptyToNull(Get("site")),
            TimeControl = EmptyToNull(Get("timecontrol")),

            Postal = TryIntN(Get("postal")),
            StartTs = FromEpoch(Get("startts")),
            EndTs = FromEpoch(Get("endts")),
            ResultSide = ParseSide(Get("result")),
            ResultTermination = ParseTermination(Get("termination")),
            PlyCount = TryIntN(Get("plycount")),
            Mode = EmptyToNull(Get("mode")),
            Rated = TryBoolN(Get("rated")),
            Corrupt = TryBoolN(Get("corrupt")),

            MoveListRaw = Get("movelist"),
            EventsRaw = Get("events"),
            Turns = ParseTurns(Get("movelist"))
        };

        // Local helper functions
        static long TryLong(string s) => long.TryParse(s, out var v) ? v : 0L;
        static int? TryIntN(string s) => int.TryParse(s, out var v) ? v : null;
        static bool? TryBoolN(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (s is "1" or "true" or "True") return true;
            if (s is "0" or "false" or "False") return false;
            return null;
        }
        static DateTimeOffset? FromEpoch(string s)
        {
            if (!long.TryParse(s, out var sec) || sec <= 0) return null;
            try { return DateTimeOffset.FromUnixTimeSeconds(sec); }
            catch { return null; }
        }
        static GameRecord.PlayerType? ParsePlayerType(string s) => s?.Trim().ToLowerInvariant() switch
        {
            "h" => GameRecord.PlayerType.Human,
            "b" => GameRecord.PlayerType.Bot,
            _ => null
        };
        static GameRecord.Side? ParseSide(string s) => s?.Trim().ToLowerInvariant() switch
        {
            "w" => GameRecord.Side.W,
            "b" => GameRecord.Side.B,
            _ => null
        };
        static GameRecord.GameTermination? ParseTermination(string s) => s?.Trim().ToLowerInvariant() switch
        {
            "r" => GameRecord.GameTermination.Resignation,
            "t" => GameRecord.GameTermination.Timeout,
            "f" => GameRecord.GameTermination.Forfeit,
            "g" => GameRecord.GameTermination.Goal,
            "e" => GameRecord.GameTermination.Elimination,
            _ => null
        };
        static string? EmptyToNull(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
        static IReadOnlyList<GameTurn> ParseTurns(string raw)
        {
            var result = new List<GameTurn>();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var originalLine in lines)
            {
                var line = originalLine.Trim();
                if (line.Length == 0) continue;

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                var header = parts[0]; // e.g., "1w" or "12b"
                if (header.Length < 2)
                    continue;

                char sideCh = char.ToLowerInvariant(header[^1]);
                Sides side = sideCh == 'w' ? Sides.Gold : Sides.Silver;
                var moveNumber = header.Substring(0, header.Length - 1);

                var moves = new List<string>();
                for (int i = 1; i < parts.Length; i++) moves.Add(parts[i]);

                // Use updatedAEIstring = original AEI line to avoid recomputation dependency
                var turn = new GameTurn(string.Empty, originalLine, moveNumber, side, moves);
                result.Add(turn);
            }
            return result;
        }
    }
}
