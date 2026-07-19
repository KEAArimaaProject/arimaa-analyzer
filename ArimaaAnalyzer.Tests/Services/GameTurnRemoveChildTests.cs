using ArimaaAnalyzer.Maui.Models;
using ArimaaAnalyzer.Maui.Services;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.Services;

public class GameTurnRemoveChildTests
{
    private static GameTurn MakeNode(string moveNumber, Sides side, string moves = "Ed2n")
        => new(
            oldAEIstring: "setposition g \"                                                                \"",
            updatedAEIstring: "setposition s \"                                                                \"",
            MoveNumber: moveNumber,
            Side: side,
            Moves: new[] { moves },
            isMainLine: false);

    [Fact(DisplayName = "RemoveChild detaches child and clears Parent")]
    public void RemoveChild_DetachesAndClearsParent()
    {
        var parent = MakeNode("1", Sides.Gold);
        var child = MakeNode("1", Sides.Silver);
        parent.AddChild(child);

        parent.Children.Should().ContainSingle();
        child.Parent.Should().BeSameAs(parent);

        parent.RemoveChild(child).Should().BeTrue();
        parent.Children.Should().BeEmpty();
        child.Parent.Should().BeNull();
        child.Children.Should().BeEmpty();
    }

    [Fact(DisplayName = "RemoveChild recursively detaches all descendants")]
    public void RemoveChild_DetachesEntireSubtree()
    {
        var parent = MakeNode("0", Sides.Gold, moves: "");
        var child = MakeNode("1", Sides.Gold);
        var grand = MakeNode("1", Sides.Silver);
        var great = MakeNode("2", Sides.Gold);
        var siblingUnderChild = MakeNode("1", Sides.Silver, moves: "Ra2n");

        parent.AddChild(child);
        child.AddChild(grand);
        child.AddChild(siblingUnderChild);
        grand.AddChild(great);

        parent.RemoveChild(child).Should().BeTrue();

        parent.Children.Should().BeEmpty();

        // Entire deleted branch is unlinked
        child.Parent.Should().BeNull();
        child.Children.Should().BeEmpty();
        grand.Parent.Should().BeNull();
        grand.Children.Should().BeEmpty();
        siblingUnderChild.Parent.Should().BeNull();
        siblingUnderChild.Children.Should().BeEmpty();
        great.Parent.Should().BeNull();
        great.Children.Should().BeEmpty();
    }

    [Fact(DisplayName = "RemoveChild does not remove sibling branches of the parent")]
    public void RemoveChild_LeavesSiblingBranchesIntact()
    {
        var parent = MakeNode("0", Sides.Gold, moves: "");
        var keep = MakeNode("1", Sides.Gold, moves: "Ed2n");
        var drop = MakeNode("1", Sides.Gold, moves: "Ee2n");
        var keepGrand = MakeNode("1", Sides.Silver);
        var dropGrand = MakeNode("1", Sides.Silver, moves: "ra7s");

        parent.AddChild(keep);
        parent.AddChild(drop);
        keep.AddChild(keepGrand);
        drop.AddChild(dropGrand);

        parent.RemoveChild(drop).Should().BeTrue();

        parent.Children.Should().ContainSingle().Which.Should().BeSameAs(keep);
        keep.Parent.Should().BeSameAs(parent);
        keep.Children.Should().ContainSingle().Which.Should().BeSameAs(keepGrand);
        keepGrand.Parent.Should().BeSameAs(keep);

        drop.Parent.Should().BeNull();
        drop.Children.Should().BeEmpty();
        dropGrand.Parent.Should().BeNull();
    }

    [Fact(DisplayName = "RemoveChild returns false when child is not attached")]
    public void RemoveChild_UnknownChild_ReturnsFalse()
    {
        var parent = MakeNode("1", Sides.Gold);
        var orphan = MakeNode("1", Sides.Silver);

        parent.RemoveChild(orphan).Should().BeFalse();
        parent.RemoveChild(null!).Should().BeFalse();
    }
}
