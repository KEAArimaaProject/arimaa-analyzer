using ArimaaAnalyzer.Maui.Models;
using ArimaaAnalyzer.Maui.Services;
using ArimaaAnalyzer.Maui.Services.Arimaa;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.Services;

public class GameTreeCopyServiceTests
{
    private static GameTurn Root() =>
        new(
            oldAEIstring: "setposition g \"                                                                \"",
            updatedAEIstring: "setposition g \"                                                                \"",
            MoveNumber: "0",
            Side: Sides.Gold,
            Moves: Array.Empty<string>(),
            isMainLine: true);

    private static GameTurn Child(
        GameTurn parent,
        string moveNumber,
        Sides side,
        string moves,
        bool isMainLine = true)
    {
        var moveList = moves.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // Keep AEI distinct but valid enough for parent links; export uses Moves text.
        var aei = parent.AEIstring;
        var node = new GameTurn(aei, aei, moveNumber, side, moveList, isMainLine);
        parent.AddChild(node);
        return node;
    }

    [Fact(DisplayName = "Export path excludes uncle / sibling branches")]
    public void Export_Excludes_Uncles()
    {
        var root = Root();
        var t1w = Child(root, "1", Sides.Gold, "Ra1 Rb1 Rc1");
        var t1b = Child(t1w, "1", Sides.Silver, "ra8 rb8 rc8");
        var main2w = Child(t1b, "2", Sides.Gold, "Ra1n Rb1n");
        var uncle2w = Child(t1b, "2", Sides.Gold, "Rc1n Rd1n", isMainLine: false);
        var deeper = Child(uncle2w, "2", Sides.Silver, "ra8s rb8s", isMainLine: false);

        var notation = GameTreeCopyService.ToGameNotation(deeper);
        notation.Should().NotBeNullOrWhiteSpace();

        // Path through uncle branch is kept
        notation.Should().Contain("1w Ra1 Rb1 Rc1");
        notation.Should().Contain("1b ra8 rb8 rc8");
        notation.Should().Contain("2w Rc1n Rd1n");
        notation.Should().Contain("2b ra8s rb8s");

        // Mainline sibling of uncle is excluded
        notation.Should().NotContain("Ra1n Rb1n");
    }

    [Fact(DisplayName = "Export includes preferred mainline descendants under selected")]
    public void Export_Includes_Mainline_Descendants()
    {
        var root = Root();
        var t1w = Child(root, "1", Sides.Gold, "Ra1 Rb1");
        var t1b = Child(t1w, "1", Sides.Silver, "ra8 rb8");
        var selected = Child(t1b, "2", Sides.Gold, "Ra1n");
        var cont = Child(selected, "2", Sides.Silver, "ra8s");
        var alt = Child(selected, "2", Sides.Silver, "rb8s", isMainLine: false);

        var notation = GameTreeCopyService.ToGameNotation(selected);
        notation.Should().Contain("2w Ra1n");
        notation.Should().Contain("2b ra8s");
        notation.Should().NotContain("rb8s");
    }

    [Fact(DisplayName = "Synthetic root is not written as a turn line")]
    public void Export_Skips_Synthetic_Root()
    {
        var root = Root();
        var t1w = Child(root, "1", Sides.Gold, "Ra1 Rb1 Rc1 Rd1 Re1 Rf1 Rg1 Rh1");

        var notation = GameTreeCopyService.ToGameNotation(t1w);
        notation.Should().Be("1w Ra1 Rb1 Rc1 Rd1 Re1 Rf1 Rg1 Rh1");
        notation.Should().NotStartWith("0");
    }

    [Fact(DisplayName = "Exported notation re-parses via ExtractTurnsWithMoves")]
    public void Export_RoundTrips_Through_Parser()
    {
        var root = Root();
        var t1w = Child(root, "1", Sides.Gold, "Ra1 Rb1 Rc1 Rd1 Re1 Rf1 Rg1 Rh1 Ca2 Df2 Hg2 Hh2");
        var t1b = Child(t1w, "1", Sides.Silver, "ra8 rb8 rc8 rd8 re8 rf8 rg8 rh8 ca7 df7 hg7 hh7");
        var t2w = Child(t1b, "2", Sides.Gold, "Hg2n Hh2n");

        var notation = GameTreeCopyService.ToGameNotation(t2w);
        notation.Should().NotBeNull();

        var parsed = NotationService.ExtractTurnsWithMoves(notation!);
        parsed.Should().NotBeNull();
        parsed!.Children.Should().HaveCount(1);
        parsed.Children[0].Moves.Should().Contain("Ra1");
    }

    [Fact(DisplayName = "CollectExportTurns path order is root-to-leaf without synthetic root")]
    public void Collect_Path_Order()
    {
        var root = Root();
        var a = Child(root, "1", Sides.Gold, "Ra1");
        var b = Child(a, "1", Sides.Silver, "ra8");

        var turns = GameTreeCopyService.CollectExportTurns(b);
        turns.Select(t => string.Join(" ", t.Moves)).Should().Equal("Ra1", "ra8");
    }

    [Fact(DisplayName = "Setup UI labels are exported as placement tokens from AEI, not the label text")]
    public void Export_SetupLabels_Become_PlacementNotation()
    {
        var emptyAei = "setposition g \"                                                                \"";
        // Classic partial gold home ranks (spaces elsewhere)
        var goldBoard = new string(' ', 64).ToCharArray();
        // rank 1 (row 7): RRRRRRRR indices 56-63
        for (var i = 56; i < 64; i++) goldBoard[i] = 'R';
        // rank 2 (row 6): C M R E D C H H — simplified gold row
        goldBoard[48] = 'C';
        goldBoard[49] = 'M';
        goldBoard[50] = 'R';
        goldBoard[51] = 'E';
        goldBoard[52] = 'D';
        goldBoard[53] = 'C';
        goldBoard[54] = 'H';
        goldBoard[55] = 'H';
        var goldAei = $"setposition s \"{new string(goldBoard)}\"";

        var root = new GameTurn(emptyAei, emptyAei, "0", Sides.Gold, Array.Empty<string>(), isMainLine: true);
        var goldSetup = new GameTurn(
            emptyAei,
            goldAei,
            "0",
            Sides.Gold,
            new[] { ArimaaGameService.GoldSetupLabel },
            isMainLine: false);
        root.AddChild(goldSetup);

        var notation = GameTreeCopyService.ToGameNotation(goldSetup);
        notation.Should().NotBeNullOrWhiteSpace();
        notation.Should().NotContain("setup");
        notation.Should().StartWith("1w ");
        notation.Should().Contain("Ra1");
        notation.Should().Contain("Ed2");

        var validation = NotationService.ValidatePastedGame(notation!);
        validation.IsValid.Should().BeTrue(
            because: string.Join(" | ", validation.Errors));
    }

    [Fact(DisplayName = "Silver setup label exports only newly placed silver pieces")]
    public void Export_SilverSetupLabel_PlacementNotation()
    {
        var goldBoard = new string(' ', 64).ToCharArray();
        for (var i = 56; i < 64; i++) goldBoard[i] = 'R';
        var goldAei = $"setposition s \"{new string(goldBoard)}\"";

        var fullBoard = (char[])goldBoard.Clone();
        for (var i = 0; i < 8; i++) fullBoard[i] = 'r'; // rank 8
        var silverAei = $"setposition g \"{new string(fullBoard)}\"";

        var root = new GameTurn(
            "setposition g \"                                                                \"",
            "setposition g \"                                                                \"",
            "0", Sides.Gold, Array.Empty<string>(), true);
        var goldSetup = new GameTurn(root.AEIstring, goldAei, "0", Sides.Gold,
            new[] { ArimaaGameService.GoldSetupLabel }, false);
        root.AddChild(goldSetup);
        var silverSetup = new GameTurn(goldAei, silverAei, "0", Sides.Silver,
            new[] { ArimaaGameService.SilverSetupLabel }, false);
        goldSetup.AddChild(silverSetup);

        var notation = GameTreeCopyService.ToGameNotation(silverSetup);
        notation.Should().NotBeNull();
        notation.Should().NotContain("setup");
        // Two lines: gold placements then silver
        var lines = notation!.Split('\n');
        lines.Should().HaveCount(2);
        lines[0].Should().StartWith("1w ");
        lines[1].Should().StartWith("1b ");
        lines[1].Should().Contain("ra8");

        NotationService.ValidatePastedGame(notation).IsValid.Should().BeTrue();
    }
}
