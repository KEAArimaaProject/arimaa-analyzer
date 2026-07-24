using System.Text.Json;
using System.Text.Json.Serialization;
using ArimaaAnalyzer.Maui.Models;
using ArimaaAnalyzer.Maui.Services;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.Services;

public class PastedGameLibraryServiceTests
{
    private static readonly JsonSerializerOptions DeepJsonOptions = new()
    {
        WriteIndented = false,
        MaxDepth = 10_000,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions DefaultDepthJsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact(DisplayName = "Default JSON MaxDepth fails for game trees deeper than 64")]
    public void Default_Json_MaxDepth_Fails_Beyond_64()
    {
        var root = BuildLinearTree(depth: 70);
        var entry = new PastedGameEntry
        {
            Name = "longgame",
            Root = GameTurnTreeMapper.ToDto(root),
        };

        var act = () => JsonSerializer.Serialize(new List<PastedGameEntry> { entry }, DefaultDepthJsonOptions);

        act.Should().Throw<JsonException>()
            .WithMessage("*object cycle*depth*");
    }

    [Fact(DisplayName = "Raised JSON MaxDepth serializes long linear game trees")]
    public void Raised_Json_MaxDepth_Serializes_Long_Trees()
    {
        // ~100 full turns ≈ 200 half-moves + root — well past the default depth of 64
        var root = BuildLinearTree(depth: 200);
        var entry = new PastedGameEntry
        {
            Name = "longgame",
            Root = GameTurnTreeMapper.ToDto(root),
        };

        var json = JsonSerializer.Serialize(new List<PastedGameEntry> { entry }, DeepJsonOptions);
        json.Should().NotBeNullOrWhiteSpace();

        var loaded = JsonSerializer.Deserialize<List<PastedGameEntry>>(json, DeepJsonOptions);
        loaded.Should().NotBeNull().And.HaveCount(1);
        loaded![0].Root.Should().NotBeNull();

        var restored = GameTurnTreeMapper.FromDto(loaded[0].Root!);
        CountDepth(restored).Should().Be(200);
    }

    [Fact(DisplayName = "AddFromTreeAsync persists a tree deeper than 64 and reloads it")]
    public async Task AddFromTreeAsync_Persists_Deep_Tree()
    {
        var service = new PastedGameLibraryService();
        var name = $"deeptest_{Guid.NewGuid():N}"[..30];
        var root = BuildLinearTree(depth: 80);

        PastedGameEntry entry;
        try
        {
            entry = await service.AddFromTreeAsync(root, name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // CI / locked environments without a writable app-data path
            return;
        }

        try
        {
            entry.Root.Should().NotBeNull();
            CountDepth(GameTurnTreeMapper.FromDto(entry.Root!)).Should().Be(80);

            var all = await service.GetAllAsync();
            var found = all.FirstOrDefault(e => e.Id == entry.Id);
            found.Should().NotBeNull();
            found!.Root.Should().NotBeNull();
            CountDepth(GameTurnTreeMapper.FromDto(found.Root!)).Should().Be(80);
        }
        finally
        {
            await service.DeleteAsync(entry.Id);
        }
    }

    /// <summary>Builds a main-line chain of <paramref name="depth"/> nodes under an empty root (depth 0 = root only).</summary>
    private static GameTurn BuildLinearTree(int depth)
    {
        var empty = "setposition g \"                                                                \"";
        var root = new GameTurn(empty, empty, "0", Sides.Gold, Array.Empty<string>(), true);
        var current = root;
        for (var i = 1; i <= depth; i++)
        {
            var side = i % 2 == 1 ? Sides.Gold : Sides.Silver;
            var moveNumber = ((i + 1) / 2).ToString();
            var child = new GameTurn(empty, empty, moveNumber, side, new[] { $"step{i}" }, true);
            current.AddChild(child);
            current = child;
        }

        return root;
    }

    private static int CountDepth(GameTurn node)
    {
        var depth = 0;
        var current = node;
        while (current.Children.Count > 0)
        {
            depth++;
            current = current.Children[0];
        }

        return depth;
    }
}
