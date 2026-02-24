using System;
using System.Collections.Generic;
using System.IO;
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
}
