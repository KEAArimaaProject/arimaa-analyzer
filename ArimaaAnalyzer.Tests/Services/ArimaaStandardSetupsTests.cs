using ArimaaAnalyzer.Maui.Services.Arimaa;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.Services;

public class ArimaaStandardSetupsTests
{
    [Fact(DisplayName = "IsGoldOnlyOnHomeRanks: gold-only 99of9 returns true")]
    public void IsGoldOnlyOnHomeRanks_GoldOnlySetup_True()
    {
        var board = ArimaaStandardSetups.NinetyNineOfNineGoldOnly();
        ArimaaStandardSetups.IsGoldOnlyOnHomeRanks(board).Should().BeTrue();
    }

    [Fact(DisplayName = "IsGoldOnlyOnHomeRanks: empty board returns false")]
    public void IsGoldOnlyOnHomeRanks_Empty_False()
    {
        ArimaaStandardSetups.IsGoldOnlyOnHomeRanks(new string(' ', 64)).Should().BeFalse();
        ArimaaStandardSetups.IsGoldOnlyOnHomeRanks(null).Should().BeFalse();
    }

    [Fact(DisplayName = "IsGoldOnlyOnHomeRanks: full gold+silver setup returns false")]
    public void IsGoldOnlyOnHomeRanks_FullSetup_False()
    {
        var gold = ArimaaStandardSetups.NinetyNineOfNineGoldOnly();
        var full = ArimaaStandardSetups.WithNinetyNineOfNineSilver(gold);
        ArimaaStandardSetups.IsGoldOnlyOnHomeRanks(full).Should().BeFalse();
    }

    [Fact(DisplayName = "IsGoldOnlyOnHomeRanks: gold off home ranks returns false")]
    public void IsGoldOnlyOnHomeRanks_GoldOffHome_False()
    {
        var chars = new string(' ', 64).ToCharArray();
        chars[0] = 'E'; // rank 8
        ArimaaStandardSetups.IsGoldOnlyOnHomeRanks(new string(chars)).Should().BeFalse();
    }

    [Fact(DisplayName = "IsGoldOnlyOnHomeRanks: silver on home ranks returns false")]
    public void IsGoldOnlyOnHomeRanks_SilverOnHome_False()
    {
        var chars = new string(' ', 64).ToCharArray();
        chars[56] = 'e';
        ArimaaStandardSetups.IsGoldOnlyOnHomeRanks(new string(chars)).Should().BeFalse();
    }
}
