using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArimaaAnalyzer.Maui.DataAccess;
using ArimaaAnalyzer.Maui.Models;

namespace ArimaaAnalyzer.Maui.Services;

/// <summary>
/// Service for loading <see cref="GameRecord"/> items from the sample TSV data file
/// located at ArimaaAnalyzer.Maui/DataAccess/allgames202602.txt.
/// </summary>
public static class GameRecordService
{
    /// <summary>
    /// Options used to filter <see cref="GameRecord"/> items for the future search menu.
    /// Provide only the criteria you want to restrict by; any null/empty criteria are ignored.
    /// </summary>
    public sealed class GameRecordFilterOptions
    {
        /// <summary>
        /// Case-insensitive substring to match against <see cref="GameRecord.WUsername"/> or <see cref="GameRecord.BUsername"/>.
        /// </summary>
        public string? UsernameContains { get; init; }

        /// <summary>
        /// Range for the higher rating of the two players (inclusive). Tuple is (min, max).
        /// If specified, both player ratings must be present.
        /// </summary>
        public (int Min, int Max)? RatingHighRange { get; init; }

        /// <summary>
        /// Range for the lower rating of the two players (inclusive). Tuple is (min, max).
        /// If specified, both player ratings must be present.
        /// </summary>
        public (int Min, int Max)? RatingLowRange { get; init; }

        /// <summary>
        /// Earliest timestamp bound (inclusive). Uses EndTs if available; otherwise StartTs.
        /// </summary>
        public DateTimeOffset? EarliestTime { get; init; }

        /// <summary>
        /// Latest timestamp bound (inclusive). Uses EndTs if available; otherwise StartTs.
        /// </summary>
        public DateTimeOffset? LatestTime { get; init; }

        /// <summary>
        /// Acceptable win conditions (terminations). If provided, only games whose
        /// <see cref="GameRecord.ResultTermination"/> is in the set are returned.
        /// </summary>
        public ISet<GameRecord.GameTermination>? WinConditions { get; init; }

        /// <summary>
        /// Acceptable raw event blobs (exact match against <see cref="GameRecord.EventsRaw"/>).
        /// </summary>
        public ISet<string>? EventsRawSet { get; init; }

        /// <summary>
        /// Rated flag: true = only rated, false = only unrated, null = both.
        /// </summary>
        public bool? Rated { get; init; }

        /// <summary>
        /// Postal option: 1 = only postal, 0 = only non-postal, null = both.
        /// </summary>
        public int? Postal { get; init; }

        /// <summary>
        /// Acceptable time control strings (exact match against <see cref="GameRecord.TimeControl"/>).
        /// </summary>
        public ISet<string>? TimeControls { get; init; }
    }

    private static string GetDataFilePath()
    {
        // Resolve the file relative to the test/app base directory to work in IDE and test runs
        var path = Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "ArimaaAnalyzer.Maui", "DataAccess", "allgames202602.txt");
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Loads and parses all game records from the TSV data file.
    /// Returns an empty list if the file is missing or empty.
    /// </summary>
    public static List<GameRecord> LoadAll()
    {
        var file = GetDataFilePath();
        var result = new List<GameRecord>();
        if (!File.Exists(file)) return result;

        using var reader = new StreamReader(file);
        var header = reader.ReadLine();
        if (string.IsNullOrEmpty(header)) return result;

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var rec = DataConverter.FromTsv(header!, line!);
                result.Add(rec);
            }
            catch
            {
                // Ignore malformed lines; continue loading the rest
            }
        }

        return result;
    }

    /// <summary>
    /// Parse a rating range from a string formatted as "min-max". Whitespace is allowed.
    /// Returns null if the string is null/empty/invalid or min&gt;max.
    /// </summary>
    public static (int Min, int Max)? TryParseRatingRange(string? range)
    {
        if (string.IsNullOrWhiteSpace(range)) return null;
        var parts = range.Split('-', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return null;
        if (!int.TryParse(parts[0], out var min)) return null;
        if (!int.TryParse(parts[1], out var max)) return null;
        if (min > max) return null;
        return (min, max);
    }

    /// <summary>
    /// Filters the given <paramref name="source"/> sequence using the provided <paramref name="options"/>.
    /// Any null/empty criteria are ignored. All provided criteria are combined with logical AND.
    /// </summary>
    public static IEnumerable<GameRecord> Filter(IEnumerable<GameRecord> source, GameRecordFilterOptions options)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (options == null) throw new ArgumentNullException(nameof(options));

        // Username substring (case-insensitive)
        if (!string.IsNullOrWhiteSpace(options.UsernameContains))
        {
            var needle = options.UsernameContains.Trim();
            source = source.Where(r =>
                (!string.IsNullOrEmpty(r.WUsername) && r.WUsername!.Contains(needle, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(r.BUsername) && r.BUsername!.Contains(needle, StringComparison.OrdinalIgnoreCase))
            );
        }

        // Rating ranges: require both ratings to be present if either range is specified
        if (options.RatingHighRange.HasValue || options.RatingLowRange.HasValue)
        {
            source = source.Where(r =>
            {
                if (!r.WRating.HasValue || !r.BRating.HasValue) return false;
                var hi = Math.Max(r.WRating.Value, r.BRating.Value);
                var lo = Math.Min(r.WRating.Value, r.BRating.Value);

                if (options.RatingHighRange.HasValue)
                {
                    var (minH, maxH) = options.RatingHighRange.Value;
                    if (hi < minH || hi > maxH) return false;
                }
                if (options.RatingLowRange.HasValue)
                {
                    var (minL, maxL) = options.RatingLowRange.Value;
                    if (lo < minL || lo > maxL) return false;
                }
                return true;
            });
        }

        // Time bounds: use EndTs if available, otherwise StartTs. Require timestamp to be within [Earliest, Latest].
        if (options.EarliestTime.HasValue || options.LatestTime.HasValue)
        {
            var earliest = options.EarliestTime;
            var latest = options.LatestTime;
            source = source.Where(r =>
            {
                var ts = r.EndTs ?? r.StartTs; // prefer EndTs when present
                if (!ts.HasValue) return false;
                if (earliest.HasValue && ts.Value < earliest.Value) return false;
                if (latest.HasValue && ts.Value > latest.Value) return false;
                return true;
            });
        }

        // Win conditions
        if (options.WinConditions is { Count: > 0 })
        {
            source = source.Where(r => r.ResultTermination.HasValue && options.WinConditions.Contains(r.ResultTermination.Value));
        }

        // EventsRaw exact match set
        if (options.EventsRawSet is { Count: > 0 })
        {
            source = source.Where(r => r.EventsRaw != null && options.EventsRawSet.Contains(r.EventsRaw));
        }

        // Rated flag
        if (options.Rated.HasValue)
        {
            var wantRated = options.Rated.Value;
            source = source.Where(r => r.Rated.HasValue && r.Rated.Value == wantRated);
        }

        // Postal option
        if (options.Postal.HasValue)
        {
            var wantPostal = options.Postal.Value;
            source = source.Where(r => r.Postal.HasValue && r.Postal.Value == wantPostal);
        }

        // Time control set
        if (options.TimeControls is { Count: > 0 })
        {
            source = source.Where(r => r.TimeControl != null && options.TimeControls.Contains(r.TimeControl));
        }

        return source;
    }

    /// <summary>
    /// Loads all records and applies <see cref="Filter(IEnumerable{GameRecord}, GameRecordFilterOptions)"/>.
    /// Returns all matches without a cap.
    /// </summary>
    public static List<GameRecord> LoadAndFilter(GameRecordFilterOptions options)
    {
        var all = LoadAll();
        return Filter(all, options).ToList();
    }

    /// <summary>
    /// Loads all records, applies filters, and returns up to <paramref name="maxResults"/> items.
    /// If no criteria are provided, this returns the first <paramref name="maxResults"/> games.
    /// </summary>
    public static List<GameRecord> LoadAndFilter(GameRecordFilterOptions options, int maxResults)
    {
        if (maxResults <= 0) return new List<GameRecord>();
        var all = LoadAll();
        return Filter(all, options).Take(maxResults).ToList();
    }

    /// <summary>
    /// Async variant of <see cref="LoadAll"/> with cancellation support.
    /// </summary>
    public static async Task<List<GameRecord>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        var file = GetDataFilePath();
        var result = new List<GameRecord>();
        if (!File.Exists(file)) return result;

        using var reader = new StreamReader(file);
        var header = await reader.ReadLineAsync();
        if (string.IsNullOrEmpty(header)) return result;

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var rec = DataConverter.FromTsv(header!, line!);
                result.Add(rec);
            }
            catch
            {
                // Ignore malformed lines; continue loading the rest
            }
        }

        return result;
    }

    /// <summary>
    /// Async variant of <see cref="LoadAndFilter(GameRecordFilterOptions)"/> with cancellation support.
    /// </summary>
    public static async Task<List<GameRecord>> LoadAndFilterAsync(GameRecordFilterOptions options, CancellationToken cancellationToken = default)
    {
        var all = await LoadAllAsync(cancellationToken);
        return Filter(all, options).ToList();
    }

    /// <summary>
    /// Async variant of <see cref="LoadAndFilter(GameRecordFilterOptions, int)"/> with cancellation support.
    /// </summary>
    public static async Task<List<GameRecord>> LoadAndFilterAsync(GameRecordFilterOptions options, int maxResults, CancellationToken cancellationToken = default)
    {
        if (maxResults <= 0) return new List<GameRecord>();
        var all = await LoadAllAsync(cancellationToken);
        return Filter(all, options).Take(maxResults).ToList();
    }
}
