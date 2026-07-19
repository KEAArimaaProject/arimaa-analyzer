using ArimaaAnalyzer.Maui.Models;
using ArimaaAnalyzer.Maui.Services;
using ArimaaAnalyzer.Maui.Services.Arimaa;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.Services;

public class ArimaaGameServiceAutoSetupTests
{
    private static string GameBaseSnippet = @"1w Ra1 Rb1 Rc1 Dd1 Re1 Rf1 Rg1 Rh1 Ca2 Mb2 Rc2 Ed2 De2 Cf2 Hg2 Hh2
1b ee7 mg7 hb7 hh7 dd7 de8 ca7 cg8 rc7 rf7 ra8 rb8 rc8 rd8 rf8 rh8
2w Hg2n Hh2n Ed2n Ed3e";

    private static ArimaaGameService CreateGameFromBase()
    {
        var root = NotationService.ExtractTurnsWithMoves(GameBaseSnippet)!;
        var deep = root;
        while (deep.Children.Count > 0)
            deep = deep.Children[0];

        var game = new ArimaaGameService(new GameState(deep.AEIstring));
        game.Load(deep);
        return game;
    }

    [Fact(DisplayName = "Gold Auto setup is live-only (no tree node until HumanMove)")]
    public void ApplyGoldAutoSetupLive_DoesNotCreateTreeNode()
    {
        var game = CreateGameFromBase();
        var root = GetRoot(game.CurrentNode!);
        var childCountBefore = root.Children.Count;
        var currentBefore = game.CurrentNode;

        game.ApplyGoldAutoSetupLive();

        game.CurrentNode.Should().BeSameAs(currentBefore);
        root.Children.Count.Should().Be(childCountBefore);
        ArimaaStandardSetups.IsGoldOnlyOnHomeRanks(game.State.GetNormalizedBoardString())
            .Should().BeTrue();
    }

    [Fact(DisplayName = "HumanMove after gold Auto setup commits under root with gold setup label")]
    public void TryCommitSetupPhase_Gold_AttachesUnderRoot()
    {
        var game = CreateGameFromBase();
        var root = GetRoot(game.CurrentNode!);
        var hardcoded = root.Children[0];

        game.ApplyGoldAutoSetupLive();
        game.TryCommitSetupPhase().Should().BeTrue();

        game.CurrentNode.Should().NotBeNull();
        game.CurrentNode!.Parent.Should().BeSameAs(root);
        game.CurrentNode.Moves.Should().Contain(ArimaaGameService.GoldSetupLabel);
        game.CurrentNode.Side.Should().Be(Sides.Gold);
        // After gold setup, silver is next to set up
        game.State.SideToMove.Should().Be(Sides.Silver);

        root.Children.Should().HaveCount(2);
        root.Children[0].Should().BeSameAs(hardcoded);
        root.Children[1].Should().BeSameAs(game.CurrentNode);
        ArimaaStandardSetups.IsGoldOnlyOnHomeRanks(game.State.GetNormalizedBoardString())
            .Should().BeTrue();
    }

    [Fact(DisplayName = "HumanMove after silver Auto setup commits under gold setup with silver label")]
    public void TryCommitSetupPhase_Silver_AttachesUnderGoldSetup()
    {
        var game = CreateGameFromBase();
        var root = GetRoot(game.CurrentNode!);

        game.ApplyGoldAutoSetupLive();
        game.TryCommitSetupPhase().Should().BeTrue();
        var goldSetup = game.CurrentNode!;

        game.ApplySilverAutoSetupLive();
        game.TryCommitSetupPhase().Should().BeTrue();

        var silverSetup = game.CurrentNode!;
        silverSetup.Parent.Should().BeSameAs(goldSetup);
        silverSetup.Moves.Should().Contain(ArimaaGameService.SilverSetupLabel);
        silverSetup.Side.Should().Be(Sides.Silver);
        game.State.SideToMove.Should().Be(Sides.Gold);

        // No empty intermediate between gold and silver
        goldSetup.Children.Should().ContainSingle()
            .Which.Should().BeSameAs(silverSetup);

        // From root: gold setup is direct child; one hop to silver via l chain
        root.Children.Should().Contain(goldSetup);
        goldSetup.Parent.Should().BeSameAs(root);
        BoardHasSilver(game.State.GetNormalizedBoardString()!).Should().BeTrue();
    }

    [Fact(DisplayName = "Full setup branch: j hops root ← gold ← silver with correct labels")]
    public void SetupBranch_TraversalHops()
    {
        var game = CreateGameFromBase();

        game.ApplyGoldAutoSetupLive();
        game.TryCommitSetupPhase();
        game.ApplySilverAutoSetupLive();
        game.TryCommitSetupPhase();

        var silver = game.CurrentNode!;
        silver.Moves.Should().Equal(ArimaaGameService.SilverSetupLabel);

        // j → gold setup
        game.GoPrev();
        game.CurrentNode!.Moves.Should().Equal(ArimaaGameService.GoldSetupLabel);
        ArimaaStandardSetups.IsGoldOnlyOnHomeRanks(game.State.GetNormalizedBoardString()).Should().BeTrue();

        // j → root
        game.GoPrev();
        game.CurrentNode!.Parent.Should().BeNull();
        game.CurrentNode.Children.Select(c => string.Join(" ", c.Moves))
            .Should().Contain(ArimaaGameService.GoldSetupLabel);

        // l from gold setup → silver (first child)
        game.Load(game.CurrentNode.Children.First(c =>
            c.Moves.Contains(ArimaaGameService.GoldSetupLabel)));
        game.GoNextMainLine();
        game.CurrentNode!.Moves.Should().Equal(ArimaaGameService.SilverSetupLabel);
    }

    [Fact(DisplayName = "HumanMove on already-committed gold setup is no-op (no extra node)")]
    public void TryCommitSetupPhase_GoldAlreadyCommitted_NoExtraNode()
    {
        var game = CreateGameFromBase();
        var root = GetRoot(game.CurrentNode!);

        game.ApplyGoldAutoSetupLive();
        game.TryCommitSetupPhase();
        var afterFirst = root.Children.Count;

        game.TryCommitSetupPhase().Should().BeTrue();
        root.Children.Count.Should().Be(afterFirst);
        game.CurrentNode!.Moves.Should().Equal(ArimaaGameService.GoldSetupLabel);
    }

    private static GameTurn GetRoot(GameTurn node)
    {
        while (node.Parent is not null)
            node = node.Parent;
        return node;
    }

    private static bool BoardHasSilver(string board)
    {
        foreach (var ch in board)
        {
            if (ch != ' ' && char.IsLower(ch))
                return true;
        }
        return false;
    }
}
