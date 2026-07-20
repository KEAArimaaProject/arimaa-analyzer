using ArimaaAnalyzer.Maui.Models;
using ArimaaAnalyzer.Maui.Services;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.Services;

public class GameTurnTreeMapperTests
{
    [Fact(DisplayName = "ToDto/FromDto round-trips a branched tree")]
    public void RoundTrip_Branched_Tree()
    {
        var empty = "setposition g \"                                                                \"";
        var root = new GameTurn(empty, empty, "0", Sides.Gold, Array.Empty<string>(), true);
        var a = new GameTurn(empty, empty, "1", Sides.Gold, new[] { "Ra1" }, true);
        var b1 = new GameTurn(empty, empty, "1", Sides.Silver, new[] { "ra8" }, true);
        var b2 = new GameTurn(empty, empty, "1", Sides.Silver, new[] { "rb8" }, false);
        root.AddChild(a);
        a.AddChild(b1);
        a.AddChild(b2);

        var dto = GameTurnTreeMapper.ToDto(root);
        var restored = GameTurnTreeMapper.FromDto(dto);

        restored.Children.Should().HaveCount(1);
        restored.Children[0].Children.Should().HaveCount(2);
        restored.Children[0].Children[0].Moves.Should().Contain("ra8");
        restored.Children[0].Children[1].Moves.Should().Contain("rb8");
        restored.Children[0].Children[1].IsMainLine.Should().BeFalse();
    }
}
