using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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
/// Persists games for Load game list as <see cref="GameTurn"/> trees (JSON DTOs).
/// </summary>
public sealed class PastedGameLibraryService
{
    private const string FileName = "pasted-games.json";

    /// <summary>
    /// Game trees are stored as nested <see cref="GameTurnDto.Children"/>. A linear game of N half-moves
    /// has depth ~N+1, so the default System.Text.Json MaxDepth of 64 rejects long games. Use a high
    /// limit so length is not restricted in practice.
    /// </summary>
    private const int JsonMaxDepth = 1_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        MaxDepth = JsonMaxDepth,
        Converters = { new JsonStringEnumConverter() },
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
        if (trimmed.Length == 0)
            return $"Name must be at least {PastedGameEntry.NameMinLength} characters.";
        if (trimmed.Length < PastedGameEntry.NameMinLength)
            return $"Name is too short ({trimmed.Length} of {PastedGameEntry.NameMinLength} minimum).";
        if (trimmed.Length > PastedGameEntry.NameMaxLength)
            return $"Name is too long ({trimmed.Length} of {PastedGameEntry.NameMaxLength} maximum).";

        if (!await IsNameAvailableAsync(trimmed, cancellationToken).ConfigureAwait(false))
            return "That name is already in use. Choose a unique name.";

        return null;
    }

    /// <summary>
    /// Persist a game tree under a unique name. Optional <paramref name="sourceNotation"/> keeps the original paste text.
    /// </summary>
    public async Task<PastedGameEntry> AddFromTreeAsync(
        GameTurn root,
        string name,
        string? sourceNotation = null,
        CancellationToken cancellationToken = default)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));

        var validationError = await ValidateNameAsync(name, cancellationToken).ConfigureAwait(false);
        if (validationError is not null)
            throw new InvalidOperationException(validationError);

        var entry = new PastedGameEntry
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            AddedAt = DateTimeOffset.UtcNow,
            FormatVersion = PastedGameEntry.CurrentFormatVersion,
            Root = GameTurnTreeMapper.ToDto(root),
            SourceNotation = string.IsNullOrWhiteSpace(sourceNotation) ? null : sourceNotation.Trim(),
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
    /// Parse linear notation into a tree and persist (Load game manual convenience).
    /// </summary>
    public async Task<PastedGameEntry> AddFromNotationAsync(
        string notation,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notation))
            throw new ArgumentException("Notation cannot be empty.", nameof(notation));

        var root = NotationService.ExtractTurnsWithMoves(notation)
                   ?? throw new InvalidOperationException("No valid turns could be parsed from the notation.");

        return await AddFromTreeAsync(root, name, sourceNotation: notation, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Legacy name kept for call sites; routes to <see cref="AddFromNotationAsync"/>.
    /// </summary>
    public Task<PastedGameEntry> AddAsync(string notation, string name, CancellationToken cancellationToken = default)
        => AddFromNotationAsync(notation, name, cancellationToken);

    /// <summary>
    /// Materialize a detached <see cref="GameTurn"/> tree for an entry (for board load).
    /// </summary>
    public static GameTurn? MaterializeRoot(PastedGameEntry entry)
    {
        if (entry?.Root is null) return null;
        try
        {
            return GameTurnTreeMapper.FromDto(entry.Root);
        }
        catch
        {
            return null;
        }
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

        List<PastedGameEntry> loaded;
        try
        {
            await using var stream = File.OpenRead(path);
            loaded = await JsonSerializer.DeserializeAsync<List<PastedGameEntry>>(stream, JsonOptions, cancellationToken)
                         .ConfigureAwait(false)
                     ?? new List<PastedGameEntry>();
        }
        catch
        {
            // Corrupt or incompatible file — start empty (old linear-only saves are discarded).
            loaded = new List<PastedGameEntry>();
        }

        // Tree format only: drop legacy notation-only rows (user-approved wipe of old saves).
        var treeOnly = loaded
            .Where(e => e.Root is not null && e.FormatVersion >= PastedGameEntry.CurrentFormatVersion)
            .ToList();

        foreach (var e in treeOnly)
        {
            if (string.IsNullOrWhiteSpace(e.Name))
                e.Name = $"Game {e.AddedAt.ToLocalTime():yyyyMMdd_HHmm}";
            e.FormatVersion = PastedGameEntry.CurrentFormatVersion;
        }

        // Rewrite file if we dropped entries so disk matches the new format.
        if (treeOnly.Count != loaded.Count)
        {
            await SaveAsync(treeOnly, cancellationToken).ConfigureAwait(false);
        }

        lock (_lock)
        {
            _cache = treeOnly;
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
