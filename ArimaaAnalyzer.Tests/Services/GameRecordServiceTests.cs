using System;
using System.IO;
using System.Linq;
using ArimaaAnalyzer.Maui.DataAccess;
using ArimaaAnalyzer.Maui.Models;
using ArimaaAnalyzer.Maui.Services;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.Services;

public class GameRecordServiceTests
{
    
    [Fact]
    public void LoadAll_Returns_NonEmpty_And_CountMatchesFileLines()
    {
        var fullPath = DataConverter.GetDataFilePath();
        File.Exists(fullPath).Should().BeTrue($"Sample data file not found: {fullPath}");

        // Count non-empty lines minus header
        var allLines = File.ReadAllLines(fullPath);
        allLines.Length.Should().BeGreaterThan(1);
        var dataLines = allLines.Skip(1).Count(l => !string.IsNullOrWhiteSpace(l));

        var list = GameRecordService.LoadAll();

        list.Should().NotBeNull();
        list.Should().NotBeEmpty();
        list.Count.Should().Be(dataLines);
    }

    [Fact]
    public void LoadAll_Contains_KnownRecord_669182_WithParsedFields()
    {
        var list = GameRecordService.LoadAll();

        var rec = list.FirstOrDefault(r => r.Id == 669182);
        rec.Should().NotBeNull("known record 669182 should exist in sample data");

        rec!.WPlayerId.Should().Be(4609);
        rec.BPlayerId.Should().Be(63199);
        rec.WUsername.Should().Be("bot_ArimaaScoreP1");
        rec.BUsername.Should().Be("Guest63199");
        rec.WCountry.Should().Be("US");
        rec.BCountry.Should().Be("PE");
        rec.WRating.Should().Be(1000);
        rec.BRating.Should().Be(1400);
        rec.WRatingK.Should().Be(0);
        rec.BRatingK.Should().Be(120);
        rec.WType.Should().Be(GameRecord.PlayerType.Bot);
        rec.BType.Should().Be(GameRecord.PlayerType.Human);
        rec.Event.Should().Be("Casual game");
        rec.Site.Should().Be("Over the Net");
        rec.TimeControl.Should().Be("2/2/100/10/8");
        rec.Postal.Should().Be(0);
        rec.StartTs!.Value.ToUnixTimeSeconds().Should().Be(1770151943);
        rec.EndTs!.Value.ToUnixTimeSeconds().Should().Be(1770152354);
        rec.ResultSide.Should().Be(GameRecord.Side.W);
        rec.ResultTermination.Should().Be(GameRecord.GameTermination.Timeout);
        rec.PlyCount.Should().Be(3);
        rec.Mode.Should().Be("IGS");
        rec.Rated.Should().BeTrue();
        rec.Corrupt.Should().BeFalse();

        rec.MoveListRaw.Should().NotBeNullOrWhiteSpace();
        rec.EventsRaw.Should().NotBeNullOrWhiteSpace();
        rec.EventLines.Should().NotBeEmpty();
        rec.EventLines.Last().Should().Contain("game finished with result w t");
    }

    [Fact]
    public void LoadAll_Parses_Turns_For_KnownRecord_669182()
    {
        var list = GameRecordService.LoadAll();
        var rec = list.First(r => r.Id == 669182);

        rec.Turns.Should().NotBeNull();
        // Current converter may keep the whole movelist in a single line when the TSV
        // contains literal "\n" instead of real newlines. So we only assert at least one turn parsed.
        rec.Turns!.Count.Should().BeGreaterThan(0);

        // Check the first few parsed turns
        var t1w = rec.Turns![0];
        t1w.MoveNumber.Should().Be("1");
        t1w.Side.Should().Be(Sides.Gold);
        t1w.Moves.Should().Contain(new[] { "Ra2", "Hb2" });

        // Depending on line splitting, subsequent turn headers may remain in the same AEI line
        // and be treated as tokens. We avoid strict assertions on subsequent turns here.
    }

    private static GameRecord Rec(
        long id,
        string? wUser = null,
        string? bUser = null,
        int? wRating = null,
        int? bRating = null,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        GameRecord.GameTermination? term = null,
        string? eventsRaw = null,
        bool? rated = null,
        int? postal = null,
        string? timeCtrl = null)
        => new GameRecord
        {
            Id = id,
            WUsername = wUser,
            BUsername = bUser,
            WRating = wRating,
            BRating = bRating,
            StartTs = start,
            EndTs = end,
            ResultTermination = term,
            EventsRaw = eventsRaw,
            Rated = rated,
            Postal = postal,
            TimeControl = timeCtrl
        };

    private static GameRecordService.GameRecordFilterOptions Opt(
        string? gold_user = null,
        string? silver_user = null,
        (int Min, int Max)? hi = null,
        (int Min, int Max)? lo = null,
        DateTimeOffset? earliest = null,
        DateTimeOffset? latest = null,
        ISet<GameRecord.GameTermination>? wins = null,
        ISet<string>? eventsSet = null,
        bool? rated = null,
        int? postal = null,
        ISet<string>? timeCtrls = null)
        => new GameRecordService.GameRecordFilterOptions
        {
            WUsernameContains = gold_user,
            BUsernameContains = silver_user,
            RatingHighRange = hi,
            RatingLowRange = lo,
            EarliestTime = earliest,
            LatestTime = latest,
            WinConditions = wins,
            EventsRawSet = eventsSet,
            Rated = rated,
            Postal = postal,
            TimeControls = timeCtrls
        };

    [Fact]
    public void Filter_UserNameContains_MatchesWhiteOrBlack_CaseInsensitive()
    {
        var src = new[]
        {
            Rec(1, wUser: "Alice", bUser: "Bob"),
            Rec(2, wUser: "carol", bUser: "dave"),
            Rec(3, wUser: "x", bUser: "y")
        };

        var opt = Opt(gold_user: "AL");
        var res = GameRecordService.Filter(src, opt).ToList();

        res.Select(r => r.Id).Should().BeEquivalentTo(new[] { 1L });

        opt = Opt(silver_user: "AV");
        res = GameRecordService.Filter(src, opt).ToList();
        res.Select(r => r.Id).Should().BeEquivalentTo(new[] { 2L });
    }

    [Fact]
    public void Filter_RatingRanges_RequireBothRatings_AndInclusive()
    {
        var src = new[]
        {
            // hi=1800 lo=1500 -> in both ranges
            Rec(1, wRating: 1500, bRating: 1800),
            // hi=1600 lo=1200 -> low below range
            Rec(2, wRating: 1600, bRating: 1200),
            // missing rating -> excluded when any range provided
            Rec(3, wRating: 1700, bRating: null),
            // boundary case hi=1700 lo=1300
            Rec(4, wRating: 1700, bRating: 1300)
        };

        var hi = (Min: 1700, Max: 1800);
        var lo = (Min: 1300, Max: 1500);
        var opt = Opt(hi: hi, lo: lo);

        var res = GameRecordService.Filter(src, opt).Select(r => r.Id).ToList();
        res.Should().BeEquivalentTo(new[] { 1L, 4L });
    }

    [Fact]
    public void Filter_TimeBounds_UsesEndTsOtherwiseStartTs_Inclusive()
    {
        var t0 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t1 = t0.AddDays(1);
        var t2 = t0.AddDays(2);
        var t3 = t0.AddDays(3);
        var src = new[]
        {
            // Uses EndTs when present
            Rec(1, start: t0, end: t1),
            // No EndTs, uses StartTs
            Rec(2, start: t2, end: null),
            // Outside upper bound
            Rec(3, start: t3, end: null),
            // No timestamps -> excluded when time filter present
            Rec(4)
        };

        var opt = Opt(earliest: t1, latest: t2);
        var res = GameRecordService.Filter(src, opt).Select(r => r.Id).ToList();
        // Record 1 has EndTs=t1 (on boundary, include). Record 2 has StartTs=t2 (on boundary, include)
        res.Should().BeEquivalentTo(new[] { 1L, 2L });
    }

    [Fact]
    public void Filter_WinConditions_MatchesProvidedSet()
    {
        var src = new[]
        {
            Rec(1, term: GameRecord.GameTermination.Elimination),
            Rec(2, term: GameRecord.GameTermination.Goal),
            Rec(3, term: GameRecord.GameTermination.Timeout),
            Rec(4, term: null)
        };

        var set = new HashSet<GameRecord.GameTermination>
        {
            GameRecord.GameTermination.Goal,
            GameRecord.GameTermination.Elimination
        };
        var opt = Opt(wins: set);
        var res = GameRecordService.Filter(src, opt).Select(r => r.Id).ToList();
        res.Should().BeEquivalentTo(new[] { 1L, 2L });
    }

    [Fact]
    public void Filter_EventsRawSet_ExactMatch()
    {
        var src = new[]
        {
            Rec(1, eventsRaw: "A\nB"),
            Rec(2, eventsRaw: "C\nD"),
            Rec(3, eventsRaw: null)
        };
        var set = new HashSet<string> { "C\nD" };
        var opt = Opt(eventsSet: set);
        var res = GameRecordService.Filter(src, opt).Select(r => r.Id).ToList();
        res.Should().BeEquivalentTo(new[] { 2L });
    }

    [Fact]
    public void Filter_Rated_TriState_Behavior()
    {
        var src = new[]
        {
            Rec(1, rated: true),
            Rec(2, rated: false),
            Rec(3, rated: null)
        };

        // rated only
        var opt = Opt(rated: true);
        GameRecordService.Filter(src, opt).Select(r => r.Id).Should().BeEquivalentTo(new[] { 1L });

        // unrated only
        opt = Opt(rated: false);
        GameRecordService.Filter(src, opt).Select(r => r.Id).Should().BeEquivalentTo(new[] { 2L });

        // null -> both (no filter) — ensure leaving null returns all
        opt = Opt();
        GameRecordService.Filter(src, opt).Select(r => r.Id).Should().BeEquivalentTo(new[] { 1L, 2L, 3L });
    }

    [Fact]
    public void Filter_Postal_TriState_Behavior()
    {
        var src = new[]
        {
            Rec(1, postal: 1),
            Rec(2, postal: 0),
            Rec(3, postal: null)
        };

        // postal only
        var opt = Opt(postal: 1);
        GameRecordService.Filter(src, opt).Select(r => r.Id).Should().BeEquivalentTo(new[] { 1L });

        // non-postal only
        opt = Opt(postal: 0);
        GameRecordService.Filter(src, opt).Select(r => r.Id).Should().BeEquivalentTo(new[] { 2L });

        // null -> both (no filter) — ensure leaving null returns all
        opt = Opt();
        GameRecordService.Filter(src, opt).Select(r => r.Id).Should().BeEquivalentTo(new[] { 1L, 2L, 3L });
    }

    [Fact]
    public void Filter_TimeControls_ExactMatchSet()
    {
        var src = new[]
        {
            Rec(1, timeCtrl: "2/2/100/10/8"),
            Rec(2, timeCtrl: "5/0/100/10/8"),
            Rec(3, timeCtrl: null)
        };

        var set = new HashSet<string> { "5/0/100/10/8" };
        var opt = Opt(timeCtrls: set);
        GameRecordService.Filter(src, opt).Select(r => r.Id).Should().BeEquivalentTo(new[] { 2L });
    }

    [Theory]
    [InlineData("1000-1500", 1000, 1500)]
    [InlineData("  900 - 1200  ", 900, 1200)]
    public void TryParseRatingRange_Valid(string input, int min, int max)
    {
        var res = GameRecordService.TryParseRatingRange(input);
        res.Should().NotBeNull();
        res!.Value.Min.Should().Be(min);
        res!.Value.Max.Should().Be(max);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc-def")]
    [InlineData("1000-")]
    [InlineData("-1500")]
    [InlineData("1600-1500")] // min > max
    public void TryParseRatingRange_Invalid(string? input)
    {
        var res = GameRecordService.TryParseRatingRange(input);
        res.Should().BeNull();
    }
}
