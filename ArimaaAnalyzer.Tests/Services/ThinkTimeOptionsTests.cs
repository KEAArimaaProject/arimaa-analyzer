using ArimaaAnalyzer.Maui.Services;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.Services;

public class ThinkTimeOptionsTests
{
    [Fact]
    public void Presets_Are_200_Through_8000_Step_200()
    {
        ThinkTimeOptions.Presets.Should().HaveCount(40);
        ThinkTimeOptions.Presets.First().Should().Be(200);
        ThinkTimeOptions.Presets.Last().Should().Be(8000);
        ThinkTimeOptions.Presets.Should().OnlyContain(ms => ms % 200 == 0);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(1234, 1234)]
    [InlineData(60_000, 60_000)]
    [InlineData(60_001, 60_000)]
    public void Clamp_Respects_Bounds(int input, int expected)
    {
        ThinkTimeOptions.Clamp(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("abc", false)]
    [InlineData("200", true, 200)]
    [InlineData(" 3500 ", true, 3500)]
    [InlineData("0", true, 1)]
    [InlineData("99999", true, 60_000)]
    public void TryParseCustom_Parses_And_Clamps(string? text, bool ok, int expectedMs = 0)
    {
        var success = ThinkTimeOptions.TryParseCustom(text, out var ms);
        success.Should().Be(ok);
        if (ok)
            ms.Should().Be(expectedMs);
    }

    [Fact]
    public void Format_Includes_Unit()
    {
        ThinkTimeOptions.Format(2000).Should().Be("2000 ms");
    }

    [Fact]
    public void DefaultMs_Is_200()
    {
        ThinkTimeOptions.DefaultMs.Should().Be(200);
    }
}
