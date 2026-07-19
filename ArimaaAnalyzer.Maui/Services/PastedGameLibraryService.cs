using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArimaaAnalyzer.Maui.Models;

#if ANDROID || IOS || MACCATALYST || WINDOWS
using Microsoft.Maui.Storage;
#endif

namespace ArimaaAnalyzer.Maui.Services;

public enum PastedGameSortBy
{
    DateNewestFirst,
    DateOldestFirst,
    NameAscending,
    NameDescending,
}

/// <summary>
/// Persists games pasted from arimaa.com so they can be re-opened from Loadgamelist.
/// </summary>
public sealed class PastedGameLibraryService
{
    private const string FileName = "pasted-games.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly object _lock = new();
    private List<PastedGameEntry>? _cache;

    public string StoragePath => Path.Combine(GetDataDirectory(), FileName);

    /// <summary>
    /// All stored games sorted as requested.
    /// </summary>
    public async Task<IReadOnlyList<PastedGameEntry>> GetAllAsync(
        PastedGameSortBy sortBy = PastedGameSortBy.DateNewestFirst,
        CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return Sort(list, sortBy).ToList();
    }

    /// <summary>True if no existing entry uses this name (case-insensitive, trimmed).</summary>
    public async Task<bool> IsNameAvailableAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeName(name);
        if (string.IsNullOrEmpty(normalized)) return false;

        var list = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return list.All(e => !string.Equals(NormalizeName(e.Name), normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validates name length and uniqueness. Returns null when valid, otherwise a user-facing message.
    /// </summary>
    public async Task<string?> ValidateNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length < PastedGameEntry.NameMinLength)
            return $"Name must be at least {PastedGameEntry.NameMinLength} characters.";
        if (trimmed.Length > PastedGameEntry.NameMaxLength)
            return $"Name must be at most {PastedGameEntry.NameMaxLength} characters.";

        if (!await IsNameAvailableAsync(trimmed, cancellationToken).ConfigureAwait(false))
            return "That name is already in use. Choose a unique name.";

        return null;
    }

    /// <summary>
    /// Append a successfully pasted game and persist.
    /// </summary>
    public async Task<PastedGameEntry> AddAsync(string notation, string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notation))
            throw new ArgumentException("Notation cannot be empty.", nameof(notation));

        var validationError = await ValidateNameAsync(name, cancellationToken).ConfigureAwait(false);
        if (validationError is not null)
            throw new InvalidOperationException(validationError);

        var entry = new PastedGameEntry
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            AddedAt = DateTimeOffset.UtcNow,
            Notation = notation.Trim(),
        };

        List<PastedGameEntry> list;
        lock (_lock)
        {
            list = _cache is null
                ? new List<PastedGameEntry>()
                : new List<PastedGameEntry>(_cache);
            list.Add(entry);
            _cache = list;
        }

        await SaveAsync(list, cancellationToken).ConfigureAwait(false);
        return entry;
    }

    /// <summary>
    /// Removes a stored game by id. Returns true if an entry was removed.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var list = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var removed = list.RemoveAll(e => e.Id == id);
        if (removed == 0)
            return false;

        lock (_lock)
        {
            _cache = list;
        }

        await SaveAsync(list, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public static string NormalizeName(string? name) => (name ?? string.Empty).Trim();

    private static IEnumerable<PastedGameEntry> Sort(IEnumerable<PastedGameEntry> source, PastedGameSortBy sortBy)
        => sortBy switch
        {
            PastedGameSortBy.DateOldestFirst => source.OrderBy(e => e.AddedAt).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase),
            PastedGameSortBy.NameAscending => source.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenByDescending(e => e.AddedAt),
            PastedGameSortBy.NameDescending => source.OrderByDescending(e => e.Name, StringComparer.OrdinalIgnoreCase).ThenByDescending(e => e.AddedAt),
            _ => source.OrderByDescending(e => e.AddedAt).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase),
        };

    private async Task<List<PastedGameEntry>> LoadAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_cache is not null)
                return new List<PastedGameEntry>(_cache);
        }

        var path = StoragePath;
        if (!File.Exists(path))
        {
            lock (_lock)
            {
                _cache = new List<PastedGameEntry>();
                return new List<PastedGameEntry>();
            }
        }

        await using var stream = File.OpenRead(path);
        var loaded = await JsonSerializer.DeserializeAsync<List<PastedGameEntry>>(stream, JsonOptions, cancellationToken)
                         .ConfigureAwait(false)
                     ?? new List<PastedGameEntry>();

        // Backfill empty names for older entries so the list always has something to show/sort.
        foreach (var e in loaded)
        {
            if (string.IsNullOrWhiteSpace(e.Name))
                e.Name = $"Game {e.AddedAt.ToLocalTime():yyyyMMdd_HHmm}";
        }

        lock (_lock)
        {
            _cache = loaded;
            return new List<PastedGameEntry>(_cache);
        }
    }

    private async Task SaveAsync(List<PastedGameEntry> list, CancellationToken cancellationToken)
    {
        var path = StoragePath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        await using (var stream = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(stream, list, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Copy(tmp, path, overwrite: true);
        try { File.Delete(tmp); } catch { /* ignore */ }
    }

    private static string GetDataDirectory()
    {
#if ANDROID || IOS || MACCATALYST || WINDOWS
        try
        {
            return FileSystem.AppDataDirectory;
        }
        catch
        {
            // Fall through to generic local app data
        }
#endif
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "ArimaaAnalyzer");
    }
}