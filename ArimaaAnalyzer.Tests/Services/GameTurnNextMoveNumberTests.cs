using ArimaaAnalyzer.Maui.Models;
using ArimaaAnalyzer.Maui.Services;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.Services;

public class GameTurnNextMoveNumberTests
{
    [Theory(DisplayName = "NextMoveNumber pairs Gold/Silver like Gamesearch")]
    [InlineData("0", Sides.Gold, "1")]
    [InlineData("1", Sides.Silver, "1")]
    [InlineData("1", Sides.Gold, "2")]
    [InlineData("4", Sides.Silver, "4")]
    [InlineData("4", Sides.Gold, "5")]
    [InlineData(null, Sides.Gold, "0")]
    [InlineData("", Sides.Silver, "0")]
    [InlineData("setup", Sides.Gold, "setup")]
    public void NextMoveNumber_GamesearchPairing(string? parent, Sides side, string expected)
    {
        GameTurn.NextMoveNumber(parent, side).Should().Be(expected);
    }

    [Fact(DisplayName = "NextMoveNumber sequence after root: 1g, 1s, 2g, 2s")]
    public void NextMoveNumber_FullSequenceFromRoot()
    {
        var n = "0";
        n = GameTurn.NextMoveNumber(n, Sides.Gold);
        n.Should().Be("1");
        n = GameTurn.NextMoveNumber(n, Sides.Silver);
        n.Should().Be("1");
        n = GameTurn.NextMoveNumber(n, Sides.Gold);
        n.Should().Be("2");
        n = GameTurn.NextMoveNumber(n, Sides.Silver);
        n.Should().Be("2");
    }
}
