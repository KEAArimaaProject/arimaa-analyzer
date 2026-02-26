using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArimaaAnalyzer.Maui.DataAccess;
using ArimaaAnalyzer.Maui.Models;
using ArimaaAnalyzer.Maui.Services;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.DataAccess;

public class DataConverterTests
{
    private static string DataFilePath = DataConverter.rawDataFilePath;
    //private static readonly string DataFilePath =
     //   Path.Combine(AppContext.BaseDirectory,
     //       "..", "..", "..", "..",
     //       "ArimaaAnalyzer.Maui", "DataAccess", "allgames202602.txt");

    private static (string header, List<string> lines) LoadSample()
    {
        var fullPath = Path.GetFullPath(DataFilePath);
        File.Exists(fullPath).Should().BeTrue($"Sample data file not found: {fullPath}");
        var all = File.ReadAllLines(fullPath);
        all.Length.Should().BeGreaterThan(2, "sample file should contain header and multiple data rows");
        var header = all[0];
        var lines = all.Skip(1).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        return (header, lines);
    }

    [Fact]
    public void FromTsv_ParsesWindowsLineEndingsInMovelistAndEvents()
    {
        var header = string.Join('\t', new[]
        {
            "id","wusername","busername","movelist","events"
        });

        var movelist = string.Join("\r\n", new[]
        {
            "1w Aa1 Bb2 Cc3 Dd4",
            "1b aa7 bb7 cc7 dd7"
        });
        var eventsRaw = string.Join("\r\n", new[]
        {
            "1769941000 [Sun Feb  1 10:16:40 2026] b player joining",
            "1769942000 [Sun Feb  1 10:33:20 2026] game finished with result w g"
        });

        var line = string.Join('\t', new[] { "5005", "W", "B", movelist, eventsRaw });

        var rec = DataConverter.FromTsv(header, line);

        // AEI lines should preserve original lines (including \r if present),
        // while move tokens are parsed from trimmed text
        rec.Turns!.Count.Should().Be(2);
        rec.Turns![0].AEIstring.Should().Be("1w Aa1 Bb2 Cc3 Dd4\r");
        rec.Turns![0].Moves.Should().Equal(new[] { "Aa1", "Bb2", "Cc3", "Dd4" });

        rec.EventLines.Should().HaveCount(2);
        rec.EventLines[0].Should().Contain("b player joining");
        rec.EventLines[1].Should().Contain("game finished");
    }

    [Fact]
    public void FromTsv_TrimsWhitespaceButPreservesOriginalAeiString()
    {
        var header = string.Join('\t', new[]
        {
            "id","movelist"
        });

        // Leading/trailing spaces around the turn line
        var movelist = "   12w   Aa1   Bb2   Cc3   Dd4   \n 12b  ee7  ff7  gg7  hh7  ";
        var line = string.Join('\t', new[] { "6006", movelist });

        var rec = DataConverter.FromTsv(header, line);
        rec.Turns!.Count.Should().Be(2);

        var tW = rec.Turns![0];
        tW.MoveNumber.Should().Be("12");
        tW.Side.Should().Be(Sides.Gold);
        tW.Moves.Should().Equal(new[] { "Aa1", "Bb2", "Cc3", "Dd4" });
        // AEIstring is the original line segment before trimming
        tW.AEIstring.Should().Be("   12w   Aa1   Bb2   Cc3   Dd4   ");
    }

    [Fact]
    public void FromTsv_BoolVariants_RatedAndCorrupt_AcceptedCases_And_MixedCaseIsNull()
    {
        var header = string.Join('\t', new[]
        {
            "id","rated","corrupt"
        });

        // Accepted lower/upper variants (1/0, true/false, True/False)
        var rec1 = DataConverter.FromTsv(header, string.Join('\t', new[] { "7001", "1", "0" }));
        rec1.Rated.Should().BeTrue();
        rec1.Corrupt.Should().BeFalse();

        var rec2 = DataConverter.FromTsv(header, string.Join('\t', new[] { "7002", "true", "false" }));
        rec2.Rated.Should().BeTrue();
        rec2.Corrupt.Should().BeFalse();

        var rec3 = DataConverter.FromTsv(header, string.Join('\t', new[] { "7003", "True", "False" }));
        rec3.Rated.Should().BeTrue();
        rec3.Corrupt.Should().BeFalse();

        // Mixed case not explicitly supported -> null
        var rec4 = DataConverter.FromTsv(header, string.Join('\t', new[] { "7004", "TrUe", "FaLsE" }));
        rec4.Rated.Should().BeNull();
        rec4.Corrupt.Should().BeNull();
    }

    [Fact]
    public void FromTsv_ResultAndTermination_AcceptCaseInsensitive()
    {
        var header = string.Join('\t', new[]
        {
            "id","result","termination"
        });

        var rec1 = DataConverter.FromTsv(header, string.Join('\t', new[] { "7101", "W", "G" }));
        rec1.ResultSide.Should().Be(GameRecord.Side.Gold);
        rec1.ResultTermination.Should().Be(GameRecord.GameTermination.Goal);

        var rec2 = DataConverter.FromTsv(header, string.Join('\t', new[] { "7102", "b", "R" }));
        rec2.ResultSide.Should().Be(GameRecord.Side.Silver);
        rec2.ResultTermination.Should().Be(GameRecord.GameTermination.Resignation);
    }

    [Fact]
    public void FromTsv_EpochZeroNegativeOverflow_YieldNullTimestamps_AndNonNumericPlyCount()
    {
        var header = string.Join('\t', new[]
        {
            "id","startts","endts","plycount"
        });

        var rec0 = DataConverter.FromTsv(header, string.Join('\t', new[] { "7200", "0", "-1", "abc" }));
        rec0.StartTs.Should().BeNull();
        rec0.EndTs.Should().BeNull();
        rec0.PlyCount.Should().BeNull();

        // Overflow/invalid -> null
        var rec1 = DataConverter.FromTsv(header, string.Join('\t', new[] { "7201", "99999999999999999999", "nan", "42" }));
        rec1.StartTs.Should().BeNull();
        rec1.EndTs.Should().BeNull();
        rec1.PlyCount.Should().Be(42);
    }

    [Fact]
    public void FromTsv_MalformedTurnHeaders_CurrentBehavior()
    {
        var header = string.Join('\t', new[]
        {
            "id","movelist"
        });

        // Includes: invalid order ("w1"), unexpected suffix ("1x"), missing suffix ("2"), and a valid line ("3b …")
        var movelist = string.Join('\n', new[]
        {
            "w1 Aa1 Bb2",   // last char is '1' -> treated as Silver per current code
            "1x Cc3 Dd4",   // last char not 'w' -> treated as Silver
            "2 Ee5",        // last char '2' -> treated as Silver
            "3b ff7 gg7"    // valid black
        });

        var line = string.Join('\t', new[] { "7300", movelist });
        var rec = DataConverter.FromTsv(header, line);

        // '2' header has length < 2 => skipped by current implementation, so only 3 turns are parsed
        rec.Turns!.Count.Should().Be(3);
        rec.Turns![0].Side.Should().Be(Sides.Silver);
        rec.Turns![1].Side.Should().Be(Sides.Silver);
        rec.Turns![2].Side.Should().Be(Sides.Silver);
    }

    [Fact]
    public void FromTsv_HeaderOrderIrrelevant_MapsByName()
    {
        // Intentionally permute column order
        var header = string.Join('\t', new[]
        {
            "busername","id","result","wusername","termination","movelist","rated","corrupt"
        });

        var movelist = "1w Aa1 Bb2 Cc3 Dd4\n1b aa7 bb7 cc7 dd7";
        var line = string.Join('\t', new[]
        {
            "Bob","8800","W","Alice","g", movelist, "true","false"
        });

        var rec = DataConverter.FromTsv(header, line);
        rec.Id.Should().Be(8800);
        rec.WUsername.Should().Be("Alice");
        rec.BUsername.Should().Be("Bob");
        rec.ResultSide.Should().Be(GameRecord.Side.Gold);
        rec.ResultTermination.Should().Be(GameRecord.GameTermination.Goal);
        rec.Rated.Should().BeTrue();
        rec.Corrupt.Should().BeFalse();
        rec.Turns!.Count.Should().Be(2);
    }

    [Fact]
    public void FromTsv_EmptyToNull_ForOptionalTextFields_AndPostalParsing()
    {
        var header = string.Join('\t', new[]
        {
            "id","event","site","timecontrol","postal"
        });

        var rec1 = DataConverter.FromTsv(header, string.Join('\t', new[] { "9001", "", " ", "  ", "123" }));
        rec1.Event.Should().BeNull();
        rec1.Site.Should().BeNull();
        rec1.TimeControl.Should().BeNull();
        rec1.Postal.Should().Be(123);

        var rec2 = DataConverter.FromTsv(header, string.Join('\t', new[] { "9002", "Open", "ServerA", "60|3", "abc" }));
        rec2.Event.Should().Be("Open");
        rec2.Site.Should().Be("ServerA");
        rec2.TimeControl.Should().Be("60|3");
        rec2.Postal.Should().BeNull();
    }

    [Fact]
    public void FromTsv_Parses_testGame_BasicFields()
    {
        // Header mirroring the sample file
        var header = string.Join('\t', new[]
        {
            "id","wplayerid","bplayerid","wusername","busername","wtitle","btitle","wcountry","bcountry",
            "wrating","brating","wratingk","bratingk","wtype","btype","event","site","timecontrol","postal",
            "startts","endts","result","termination","plycount","mode","rated","corrupt","movelist","events"
        });

        // The long TSV row stored in this test file
        var line = testGame.Replace("\\n", "\n");

        var rec = DataConverter.FromTsv(header, line);

        // Spot-check key fields from the known row
        rec.Id.Should().Be(669123);
        rec.WPlayerId.Should().Be(63152);
        rec.BPlayerId.Should().Be(16866);
        rec.WUsername.Should().Be("bot_dolores_v1");
        rec.BUsername.Should().Be("browni3141");
        rec.WCountry.Should().Be("US");
        rec.BCountry.Should().Be("US");
        rec.WRating.Should().Be(2630);
        rec.BRating.Should().Be(2565);
        rec.WRatingK.Should().Be(30);
        rec.BRatingK.Should().Be(30);
        rec.WType.Should().Be(GameRecord.PlayerType.Bot);
        rec.BType.Should().Be(GameRecord.PlayerType.Human);

        rec.StartTs!.Value.ToUnixTimeSeconds().Should().Be(1769942402);
        rec.EndTs!.Value.ToUnixTimeSeconds().Should().Be(1769944188);
        rec.ResultSide.Should().Be(GameRecord.Side.Gold);
        rec.ResultTermination.Should().Be(GameRecord.GameTermination.Goal);
        rec.PlyCount.Should().Be(54);
        rec.Mode.Should().Be("IGS");
        rec.Rated.Should().BeTrue();
        rec.Corrupt.Should().BeFalse();
    }

    [Fact]
    public void FromTsv_Parses_testGame_Movelist_And_Events()
    {
        var header = string.Join('\t', new[]
        {
            "id","wplayerid","bplayerid","wusername","busername","wtitle","btitle","wcountry","bcountry",
            "wrating","brating","wratingk","bratingk","wtype","btype","event","site","timecontrol","postal",
            "startts","endts","result","termination","plycount","mode","rated","corrupt","movelist","events"
        });

        var line = testGame.Replace("\\n", "\n");
        var rec = DataConverter.FromTsv(header, line);

        // Movelist integrity
        rec.MoveListRaw.Should().NotBeNullOrWhiteSpace();
        rec.Turns.Should().NotBeNull();
        var moveLines = rec.MoveListRaw!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        rec.Turns!.Count.Should().Be(moveLines.Length);
        rec.Turns![0].AEIstring.Should().Be(moveLines[0]);
        rec.Turns![^1].AEIstring.Should().Be(moveLines[^1]);

        // Events integrity
        rec.EventsRaw.Should().NotBeNullOrWhiteSpace();
        rec.EventLines.Should().NotBeEmpty();
        rec.EventLines[^1].Should().Contain("game finished with result w g");
    }
    
    private static String testGame = "669123\t63152\t16866\tbot_dolores_v1\tbrowni3141\t\t\tUS\tUS\t2630\t2565\t30\t30\tb\th\tCasual game\tOver the Net\t15s/2m30s/100\t0\t1769942402\t1769944188\tw\tg\t54\tIGS\t1\t0\t1w Da1 Db1 Rc1 Rd1 Ce1 Rf1 Cg1 Eh1 Ha2 Rb2 Rc2 Rd2 Me2 Rf2 Rg2 Hh2\\n1b ee7 mb7 ha7 hh7 dd7 de8 cg7 cb8 rc7 rf7 ra8 rc8 rd8 rf8 rg8 rh8\\n2w Hh2n Hh3n Eh1n Eh2n\\n2b ee7s ee6s hh7s ee5s\\n3w Eh3w Eg3w Ef3w Ha2n\\n3b ee4w ed4s ed3n Rd2n\\n4w Rd3w Rc3w Ee3n Ee4n\\n4b hh6w mb7s ha7s cb8s\\n5w Rg2e Ha3n Da1n Da2n\\n5b ed4w ec4w Ha4n eb4w\\n6w Ee5e Ef5e Eg5w hg6s\\n6b rh8s rh7s rh6s hg5n\\n7w Hh4s rh5s Hh3w rh4s\\n7b Ha5e ea4n mb6e Hb5n\\n8w Ef5e Eg5w hg6s Rb2w\\n8b cg7s hg5e mc6e Hb6e Hc6x\\n9w Db1n Ef5w Ee5n Cg1n\\n9b ea5s ea4e Da3n ha6s\\n10w Hg3n rh3w rg3w rf3x Hg4s\\n10b ha5e Da4n eb4e ec4e\\n11w Hg3n Hg4n cg6n Hg5n\\n11b ed4e md6s md5e me5e\\n12w Hg6w rf7w Hf6n Ee6e\\n12b ee4e mf5e mg5n mg6e\\n13w Me2w Md2n Md3w Ef6e\\n13b hb5n Da5e ef4n cg7e\\n14w Rb3n Db5w Rb4w Mc3w\\n14b ch7n mh6n mh7w ch8s\\n15w Eg6w mg7s mg6e Ef6e\\n15b ch7n mh6n mh7w ch8s\\n16w Eg6w mg7s mg6s Ef6e\\n16b ef5s mg5w dd7s dd6e\\n17w Eg6w mf5w Ef6s Ra4e\\n17b rg8s rg7s me5w md5n\\n18w Ef5n de6s Ef6w Rh2n\\n18b hh5w hg5w de5w md6n\\n19w Da5s Hf7s rg6n Hf6e\\n19b re7e rf7s md7e me7e\\n20w Hg6s Hg5s Cg2n Ee6s\\n20b hf5e ef4w Hg4w rf6e\\n21w Ee5e Hf4e Cg3s Hg4s\\n21b hb6s hb5n Rb4n dd5n\\n22w Mb3n Rb5w hg5s Ef5e\\n22b ee4e hg4e Rh3s hh4s\\n23w Eg5s rg6s Ce1n Db2n\\n23b hb6w Rh2s hh3s ef4w\\n24w Eg4w rg5s Ef4n Ef5w\\n24b rg7s Cg2s hh2w ee4s\\n25w Hg3e rg4s rg3w Hh3w\\n25b mf7s rf8s de8s cb7s\\n26w Ee5e Ef5s mf6s Mb4n\\n26b cb6n de7s de6s mf5e\\n27w Mb5n Da4e Db4e Dc4n\\n27b dd6w de5w rd8s rd7s\\n28w Ef4n Ef5s mg5w Mb6s\\n28b rf7s mf5e ch7s ch6s\\n29w Ef4n Ef5s mg5w Mb5n\\n29b mf5e ra8s rc8w mg5s\\n30w Ef4n mg4w mf4w Ef5s\\n30b me4n me5e mf5e mg5s\\n31w Ef4n mg4w mf4w Ef5s\\n31b me4n me5e mf5e rf6s\\n32w rf5n Ef4n Ef5s mg5w\\n32b mf5e rf6s mg5s ch5s\\n33w mg4n Ef4e mg5e Eg4n\\n33b rd6e dd5e rf5s de5w\\n34w Eg5w rf4e Ef5s Ra2n\\n34b mh5w ch4n rg4e mg5s\\n35w Ef4n mg4w mf4w Ef5s\\n35b me4w md4w Dc5w mc4n\\n36w Hg3e Hh3w rh4s Db5s\\n36b ch5s rh3s ch4s mc5w\\n37w Ra3n Db4e Dc4n Ef4n\\n37b Dc5s mb5e dd5e dc6e\\n38w Ra4e Dc4e Dd4s Rb4w\\n38b de5s de4e df4e mc5w\\n39w Ef5w Ee5e re6s\\n39b rc7e rd7e re7e cb7e\\n40w Db3e re5s Ef5w Ee5w\\n40b re4e mb5s mb4n Ra4e\\n41w Dc3n Dc4n Dc5n Ed5w\\n41b ee3n Ce2n Rf2w hg2w\\n42w cc7n Dc6n Rd1e Cg1n\\n42b ee4w ed4n dd6e ed5n\\n43w Ec5e mb5e mc5s Ed5w\\n43b ed6n ed7s Dc7e cc8s\\n44w mc4s mc3x Ec5s Ec4n Rb4n\\n44b de6s de5s de4n Ce3n\\n45w Dd3n Ce4s Ec5e Rb5e\\n45b dg4n dg5w de5n de6n\\n46w Rc5n Dd4w Dc4n Rc1e\\n46b ed6e Dd7s de7w rb8s\\n47w Mb6s Rc6w Dd6w\\n47b cc7n dd7w ee6w cc8w\\n48w Ce3n rf3w re3w Ce4s\\n48b ed6n Dc6e Dd6e ed7s\\n49w Rb6e Ed5e Mb5n De6n\\n49b ed6s dc7e rb7e dd7s\\n50w rd3w rc3x Ce3w De7w Ee5n\\n50b Dc5s ed5w ra7e cb8e\\n51w dd6s Ee6w Re2n Re3n\\n51b rb7w ec5w Mb6n eb5n\\n52w Dc4n Dd7n rc7e Rc6n\\n52b Mb7n eb6n Rc7s eb7e\\n53w Rc6w Dc5w Rb6n Db5n\\n53b Db6s ha6e hb6w Rb7s\\n54w Rb6n Db5n Mb8w Rb7n\\n54b\t1769942372 [Sun Feb  1 10:39:32 2026] b player joining\\n1769942372 [Sun Feb  1 10:39:32 2026] b player present\\n1769942400 [Sun Feb  1 10:40:00 2026] w player joining\\n1769942401 [Sun Feb  1 10:40:01 2026] w player present\\n1769942402 [Sun Feb  1 10:40:02 2026] w player present\\n1769942403 [Sun Feb  1 10:40:03 2026] start move received from w\\n1769942416 [Sun Feb  1 10:40:16 2026] move 1w received from w [Da1 Db1 Rc1 Rd1 Ce1 Rf1 Cg1 Eh1 Ha2 Rb2 Rc2 Rd2 Me2 Rf2 Rg2 Hh2]\\n1769942416 [Sun Feb  1 10:40:16 2026] after 1w, move used:13 wresv:150 bresv:150 game:13\\n1769942417 [Sun Feb  1 10:40:17 2026] w player present\\n1769942432 [Sun Feb  1 10:40:32 2026] move 1b received from b [ee7 mb7 ha7 hh7 dd7 de8 cg7 cb8 rc7 rf7 ra8 rc8 rd8 rf8 rg8 rh8]\\n1769942432 [Sun Feb  1 10:40:32 2026] after 1b, move used:16 wresv:150 bresv:150 game:29\\n1769942449 [Sun Feb  1 10:40:49 2026] move 2w received from w [Hh2n Hh3n Eh1n Eh2n]\\n1769942449 [Sun Feb  1 10:40:49 2026] after 2w, move used:17 wresv:148 bresv:150 game:46\\n1769942449 [Sun Feb  1 10:40:49 2026] w player present\\n1769942470 [Sun Feb  1 10:41:10 2026] move 2b received from b [ee7s ee6s hh7s ee5s]\\n1769942471 [Sun Feb  1 10:41:11 2026] after 2b, move used:22 wresv:148 bresv:143 game:68\\n1769942483 [Sun Feb  1 10:41:23 2026] move 3w received from w [Eh3w Eg3w Ef3w Ha2n]\\n1769942484 [Sun Feb  1 10:41:24 2026] after 3w, move used:13 wresv:150 bresv:143 game:81\\n1769942484 [Sun Feb  1 10:41:24 2026] w player present\\n1769942495 [Sun Feb  1 10:41:35 2026] move 3b received from b [ee4w ed4s ed3n Rd2n]\\n1769942495 [Sun Feb  1 10:41:35 2026] after 3b, move used:11 wresv:150 bresv:147 game:92\\n1769942511 [Sun Feb  1 10:41:51 2026] move 4w received from w [Rd3w Rc3w Ee3n Ee4n]\\n1769942511 [Sun Feb  1 10:41:51 2026] after 4w, move used:16 wresv:149 bresv:147 game:108\\n1769942511 [Sun Feb  1 10:41:51 2026] w player present\\n1769942540 [Sun Feb  1 10:42:20 2026] move 4b received from b [hh6w mb7s ha7s cb8s]\\n1769942540 [Sun Feb  1 10:42:20 2026] after 4b, move used:29 wresv:149 bresv:133 game:137\\n1769942608 [Sun Feb  1 10:43:28 2026] move 5w received from w [Rg2e Ha3n Da1n Da2n]\\n1769942608 [Sun Feb  1 10:43:28 2026] after 5w, move used:68 wresv:96 bresv:133 game:205\\n1769942608 [Sun Feb  1 10:43:28 2026] w player present\\n1769942629 [Sun Feb  1 10:43:49 2026] move 5b received from b [ed4w ec4w Ha4n eb4w]\\n1769942629 [Sun Feb  1 10:43:49 2026] after 5b, move used:21 wresv:96 bresv:127 game:226\\n1769942656 [Sun Feb  1 10:44:16 2026] move 6w received from w [Ee5e Ef5e Eg5w hg6s]\\n1769942656 [Sun Feb  1 10:44:16 2026] after 6w, move used:27 wresv:84 bresv:127 game:253\\n1769942657 [Sun Feb  1 10:44:17 2026] w player present\\n1769942680 [Sun Feb  1 10:44:40 2026] move 6b received from b [rh8s rh7s rh6s hg5n]\\n1769942680 [Sun Feb  1 10:44:40 2026] after 6b, move used:24 wresv:84 bresv:118 game:277\\n1769942714 [Sun Feb  1 10:45:14 2026] move 7w received from w [Hh4s rh5s Hh3w rh4s]\\n1769942714 [Sun Feb  1 10:45:14 2026] after 7w, move used:34 wresv:65 bresv:118 game:311\\n1769942714 [Sun Feb  1 10:45:14 2026] w player present\\n1769942721 [Sun Feb  1 10:45:21 2026] move 7b received from b [Ha5e ea4n mb6e Hb5n]\\n1769942721 [Sun Feb  1 10:45:21 2026] after 7b, move used:7 wresv:65 bresv:126 game:318\\n1769942747 [Sun Feb  1 10:45:47 2026] move 8w received from w [Ef5e Eg5w hg6s Rb2w]\\n1769942747 [Sun Feb  1 10:45:47 2026] after 8w, move used:26 wresv:54 bresv:126 game:344\\n1769942747 [Sun Feb  1 10:45:47 2026] w player present\\n1769942777 [Sun Feb  1 10:46:17 2026] move 8b received from b [cg7s hg5e mc6e Hb6e Hc6x]\\n1769942777 [Sun Feb  1 10:46:17 2026] after 8b, move used:30 wresv:54 bresv:111 game:374\\n1769942797 [Sun Feb  1 10:46:37 2026] move 9w received from w [Db1n Ef5w Ee5n Cg1n]\\n1769942797 [Sun Feb  1 10:46:37 2026] after 9w, move used:20 wresv:49 bresv:111 game:394\\n1769942797 [Sun Feb  1 10:46:37 2026] w player present\\n1769942824 [Sun Feb  1 10:47:04 2026] move 9b received from b [ea5s ea4e Da3n ha6s]\\n1769942824 [Sun Feb  1 10:47:04 2026] after 9b, move used:27 wresv:49 bresv:99 game:421\\n1769942840 [Sun Feb  1 10:47:20 2026] move 10w received from w [Hg3n rh3w rg3w rf3x Hg4s]\\n1769942840 [Sun Feb  1 10:47:20 2026] after 10w, move used:16 wresv:48 bresv:99 game:437\\n1769942841 [Sun Feb  1 10:47:21 2026] w player present\\n1769942869 [Sun Feb  1 10:47:49 2026] move 10b received from b [ha5e Da4n eb4e ec4e]\\n1769942869 [Sun Feb  1 10:47:49 2026] after 10b, move used:29 wresv:48 bresv:85 game:466\\n1769942884 [Sun Feb  1 10:48:04 2026] move 11w received from w [Hg3n Hg4n cg6n Hg5n]\\n1769942884 [Sun Feb  1 10:48:04 2026] after 11w, move used:15 wresv:48 bresv:85 game:481\\n1769942884 [Sun Feb  1 10:48:04 2026] w player present\\n1769942902 [Sun Feb  1 10:48:22 2026] move 11b received from b [ed4e md6s md5e me5e]\\n1769942902 [Sun Feb  1 10:48:22 2026] after 11b, move used:18 wresv:48 bresv:82 game:499\\n1769942914 [Sun Feb  1 10:48:34 2026] move 12w received from w [Hg6w rf7w Hf6n Ee6e]\\n1769942914 [Sun Feb  1 10:48:34 2026] after 12w, move used:12 wresv:51 bresv:82 game:511\\n1769942914 [Sun Feb  1 10:48:34 2026] w player present\\n1769942963 [Sun Feb  1 10:49:23 2026] move 12b received from b [ee4e mf5e mg5n mg6e]\\n1769942963 [Sun Feb  1 10:49:23 2026] after 12b, move used:49 wresv:51 bresv:48 game:560\\n1769942980 [Sun Feb  1 10:49:40 2026] move 13w received from w [Me2w Md2n Md3w Ef6e]\\n1769942980 [Sun Feb  1 10:49:40 2026] after 13w, move used:17 wresv:49 bresv:48 game:577\\n1769942981 [Sun Feb  1 10:49:41 2026] w player present\\n1769943013 [Sun Feb  1 10:50:13 2026] move 13b received from b [hb5n Da5e ef4n cg7e]\\n1769943013 [Sun Feb  1 10:50:13 2026] after 13b, move used:33 wresv:49 bresv:30 game:610\\n1769943030 [Sun Feb  1 10:50:30 2026] move 14w received from w [Rb3n Db5w Rb4w Mc3w]\\n1769943030 [Sun Feb  1 10:50:30 2026] after 14w, move used:17 wresv:47 bresv:30 game:627\\n1769943031 [Sun Feb  1 10:50:31 2026] w player present\\n1769943040 [Sun Feb  1 10:50:40 2026] move 14b received from b [ch7n mh6n mh7w ch8s]\\n1769943040 [Sun Feb  1 10:50:40 2026] after 14b, move used:10 wresv:47 bresv:35 game:637\\n1769943055 [Sun Feb  1 10:50:55 2026] move 15w received from w [Eg6w mg7s mg6e Ef6e]\\n1769943055 [Sun Feb  1 10:50:55 2026] after 15w, move used:15 wresv:47 bresv:35 game:652\\n1769943055 [Sun Feb  1 10:50:55 2026] w player present\\n1769943059 [Sun Feb  1 10:50:59 2026] move 15b received from b [ch7n mh6n mh7w ch8s]\\n1769943059 [Sun Feb  1 10:50:59 2026] after 15b, move used:4 wresv:47 bresv:46 game:656\\n1769943070 [Sun Feb  1 10:51:10 2026] move 16w received from w [Eg6w mg7s mg6s Ef6e]\\n1769943070 [Sun Feb  1 10:51:10 2026] after 16w, move used:11 wresv:51 bresv:46 game:667\\n1769943070 [Sun Feb  1 10:51:10 2026] w player present\\n1769943082 [Sun Feb  1 10:51:22 2026] move 16b received from b [ef5s mg5w dd7s dd6e]\\n1769943082 [Sun Feb  1 10:51:22 2026] after 16b, move used:12 wresv:51 bresv:49 game:679\\n1769943102 [Sun Feb  1 10:51:42 2026] move 17w received from w [Eg6w mf5w Ef6s Ra4e]\\n1769943102 [Sun Feb  1 10:51:42 2026] after 17w, move used:20 wresv:46 bresv:49 game:699\\n1769943102 [Sun Feb  1 10:51:42 2026] w player present\\n1769943148 [Sun Feb  1 10:52:28 2026] move 17b received from b [rg8s rg7s me5w md5n]\\n1769943148 [Sun Feb  1 10:52:28 2026] after 17b, move used:46 wresv:46 bresv:18 game:745\\n1769943162 [Sun Feb  1 10:52:42 2026] move 18w received from w [Ef5n de6s Ef6w Rh2n]\\n1769943162 [Sun Feb  1 10:52:42 2026] after 18w, move used:14 wresv:47 bresv:18 game:759\\n1769943163 [Sun Feb  1 10:52:43 2026] w player present\\n1769943177 [Sun Feb  1 10:52:57 2026] move 18b received from b [hh5w hg5w de5w md6n]\\n1769943177 [Sun Feb  1 10:52:57 2026] after 18b, move used:15 wresv:47 bresv:18 game:774\\n1769943190 [Sun Feb  1 10:53:10 2026] move 19w received from w [Da5s Hf7s rg6n Hf6e]\\n1769943190 [Sun Feb  1 10:53:10 2026] after 19w, move used:13 wresv:49 bresv:18 game:787\\n1769943190 [Sun Feb  1 10:53:10 2026] w player present\\n1769943196 [Sun Feb  1 10:53:16 2026] move 19b received from b [re7e rf7s md7e me7e]\\n1769943196 [Sun Feb  1 10:53:16 2026] after 19b, move used:6 wresv:49 bresv:27 game:793\\n1769943214 [Sun Feb  1 10:53:34 2026] move 20w received from w [Hg6s Hg5s Cg2n Ee6s]\\n1769943214 [Sun Feb  1 10:53:34 2026] after 20w, move used:18 wresv:46 bresv:27 game:811\\n1769943214 [Sun Feb  1 10:53:34 2026] w player present\\n1769943229 [Sun Feb  1 10:53:49 2026] move 20b received from b [hf5e ef4w Hg4w rf6e]\\n1769943229 [Sun Feb  1 10:53:49 2026] after 20b, move used:15 wresv:46 bresv:27 game:826\\n1769943244 [Sun Feb  1 10:54:04 2026] move 21w received from w [Ee5e Hf4e Cg3s Hg4s]\\n1769943244 [Sun Feb  1 10:54:04 2026] after 21w, move used:15 wresv:46 bresv:27 game:841\\n1769943244 [Sun Feb  1 10:54:04 2026] w player present\\n1769943262 [Sun Feb  1 10:54:22 2026] move 21b received from b [hb6s hb5n Rb4n dd5n]\\n1769943262 [Sun Feb  1 10:54:22 2026] after 21b, move used:18 wresv:46 bresv:24 game:859\\n1769943274 [Sun Feb  1 10:54:34 2026] move 22w received from w [Mb3n Rb5w hg5s Ef5e]\\n1769943274 [Sun Feb  1 10:54:34 2026] after 22w, move used:12 wresv:49 bresv:24 game:871\\n1769943275 [Sun Feb  1 10:54:35 2026] w player present\\n1769943280 [Sun Feb  1 10:54:40 2026] move 22b received from b [ee4e hg4e Rh3s hh4s]\\n1769943280 [Sun Feb  1 10:54:40 2026] after 22b, move used:6 wresv:49 bresv:33 game:877\\n1769943297 [Sun Feb  1 10:54:57 2026] move 23w received from w [Eg5s rg6s Ce1n Db2n]\\n1769943297 [Sun Feb  1 10:54:57 2026] after 23w, move used:17 wresv:47 bresv:33 game:894\\n1769943298 [Sun Feb  1 10:54:58 2026] w player present\\n1769943322 [Sun Feb  1 10:55:22 2026] move 23b received from b [hb6w Rh2s hh3s ef4w]\\n1769943322 [Sun Feb  1 10:55:22 2026] after 23b, move used:25 wresv:47 bresv:23 game:919\\n1769943337 [Sun Feb  1 10:55:37 2026] move 24w received from w [Eg4w rg5s Ef4n Ef5w]\\n1769943337 [Sun Feb  1 10:55:37 2026] after 24w, move used:15 wresv:47 bresv:23 game:934\\n1769943338 [Sun Feb  1 10:55:38 2026] w player present\\n1769943359 [Sun Feb  1 10:55:59 2026] move 24b received from b [rg7s Cg2s hh2w ee4s]\\n1769943359 [Sun Feb  1 10:55:59 2026] after 24b, move used:22 wresv:47 bresv:16 game:956\\n1769943372 [Sun Feb  1 10:56:12 2026] move 25w received from w [Hg3e rg4s rg3w Hh3w]\\n1769943372 [Sun Feb  1 10:56:12 2026] after 25w, move used:13 wresv:49 bresv:16 game:969\\n1769943373 [Sun Feb  1 10:56:13 2026] w player present\\n1769943389 [Sun Feb  1 10:56:29 2026] move 25b received from b [mf7s rf8s de8s cb7s]\\n1769943389 [Sun Feb  1 10:56:29 2026] after 25b, move used:17 wresv:49 bresv:14 game:986\\n1769943407 [Sun Feb  1 10:56:47 2026] move 26w received from w [Ee5e Ef5s mf6s Mb4n]\\n1769943407 [Sun Feb  1 10:56:47 2026] after 26w, move used:18 wresv:46 bresv:14 game:1004\\n1769943408 [Sun Feb  1 10:56:48 2026] w player present\\n1769943417 [Sun Feb  1 10:56:57 2026] move 26b received from b [cb6n de7s de6s mf5e]\\n1769943417 [Sun Feb  1 10:56:57 2026] after 26b, move used:10 wresv:46 bresv:19 game:1014\\n1769943434 [Sun Feb  1 10:57:14 2026] move 27w received from w [Mb5n Da4e Db4e Dc4n]\\n1769943434 [Sun Feb  1 10:57:14 2026] after 27w, move used:17 wresv:44 bresv:19 game:1031\\n1769943435 [Sun Feb  1 10:57:15 2026] w player present\\n1769943445 [Sun Feb  1 10:57:25 2026] move 27b received from b [dd6w de5w rd8s rd7s]\\n1769943445 [Sun Feb  1 10:57:25 2026] after 27b, move used:11 wresv:44 bresv:23 game:1042\\n1769943462 [Sun Feb  1 10:57:42 2026] move 28w received from w [Ef4n Ef5s mg5w Mb6s]\\n1769943462 [Sun Feb  1 10:57:42 2026] after 28w, move used:17 wresv:42 bresv:23 game:1059\\n1769943462 [Sun Feb  1 10:57:42 2026] w player present\\n1769943490 [Sun Feb  1 10:58:10 2026] move 28b received from b [rf7s mf5e ch7s ch6s]\\n1769943490 [Sun Feb  1 10:58:10 2026] after 28b, move used:28 wresv:42 bresv:10 game:1087\\n1769943504 [Sun Feb  1 10:58:24 2026] move 29w received from w [Ef4n Ef5s mg5w Mb5n]\\n1769943504 [Sun Feb  1 10:58:24 2026] after 29w, move used:14 wresv:43 bresv:10 game:1101\\n1769943505 [Sun Feb  1 10:58:25 2026] w player present\\n1769943515 [Sun Feb  1 10:58:35 2026] move 29b received from b [mf5e ra8s rc8w mg5s]\\n1769943515 [Sun Feb  1 10:58:35 2026] after 29b, move used:11 wresv:43 bresv:14 game:1112\\n1769943525 [Sun Feb  1 10:58:45 2026] move 30w received from w [Ef4n mg4w mf4w Ef5s]\\n1769943525 [Sun Feb  1 10:58:45 2026] after 30w, move used:10 wresv:48 bresv:14 game:1122\\n1769943526 [Sun Feb  1 10:58:46 2026] w player present\\n1769943529 [Sun Feb  1 10:58:49 2026] move 30b received from b [me4n me5e mf5e mg5s]\\n1769943529 [Sun Feb  1 10:58:49 2026] after 30b, move used:4 wresv:48 bresv:25 game:1126\\n1769943541 [Sun Feb  1 10:59:01 2026] move 31w received from w [Ef4n mg4w mf4w Ef5s]\\n1769943541 [Sun Feb  1 10:59:01 2026] after 31w, move used:12 wresv:51 bresv:25 game:1138\\n1769943542 [Sun Feb  1 10:59:02 2026] w player present\\n1769943549 [Sun Feb  1 10:59:09 2026] move 31b received from b [me4n me5e mf5e rf6s]\\n1769943549 [Sun Feb  1 10:59:09 2026] after 31b, move used:8 wresv:51 bresv:32 game:1146\\n1769943566 [Sun Feb  1 10:59:26 2026] move 32w received from w [rf5n Ef4n Ef5s mg5w]\\n1769943566 [Sun Feb  1 10:59:26 2026] after 32w, move used:17 wresv:49 bresv:32 game:1163\\n1769943567 [Sun Feb  1 10:59:27 2026] w player present\\n1769943585 [Sun Feb  1 10:59:45 2026] move 32b received from b [mf5e rf6s mg5s ch5s]\\n1769943585 [Sun Feb  1 10:59:45 2026] after 32b, move used:19 wresv:49 bresv:28 game:1182\\n1769943599 [Sun Feb  1 10:59:59 2026] move 33w received from w [mg4n Ef4e mg5e Eg4n]\\n1769943599 [Sun Feb  1 10:59:59 2026] after 33w, move used:14 wresv:50 bresv:28 game:1196\\n1769943599 [Sun Feb  1 10:59:59 2026] w player present\\n1769943631 [Sun Feb  1 11:00:31 2026] move 33b received from b [rd6e dd5e rf5s de5w]\\n1769943631 [Sun Feb  1 11:00:31 2026] after 33b, move used:32 wresv:50 bresv:11 game:1228\\n1769943650 [Sun Feb  1 11:00:50 2026] move 34w received from w [Eg5w rf4e Ef5s Ra2n]\\n1769943650 [Sun Feb  1 11:00:50 2026] after 34w, move used:19 wresv:46 bresv:11 game:1247\\n1769943650 [Sun Feb  1 11:00:50 2026] w player present\\n1769943658 [Sun Feb  1 11:00:58 2026] move 34b received from b [mh5w ch4n rg4e mg5s]\\n1769943658 [Sun Feb  1 11:00:58 2026] after 34b, move used:8 wresv:46 bresv:18 game:1255\\n1769943666 [Sun Feb  1 11:01:06 2026] move 35w received from w [Ef4n mg4w mf4w Ef5s]\\n1769943666 [Sun Feb  1 11:01:06 2026] after 35w, move used:8 wresv:53 bresv:18 game:1263\\n1769943667 [Sun Feb  1 11:01:07 2026] w player present\\n1769943680 [Sun Feb  1 11:01:20 2026] move 35b received from b [me4w md4w Dc5w mc4n]\\n1769943680 [Sun Feb  1 11:01:20 2026] after 35b, move used:14 wresv:53 bresv:19 game:1277\\n1769943695 [Sun Feb  1 11:01:35 2026] move 36w received from w [Hg3e Hh3w rh4s Db5s]\\n1769943695 [Sun Feb  1 11:01:35 2026] after 36w, move used:15 wresv:53 bresv:19 game:1292\\n1769943696 [Sun Feb  1 11:01:36 2026] w player present\\n1769943719 [Sun Feb  1 11:01:59 2026] move 36b received from b [ch5s rh3s ch4s mc5w]\\n1769943719 [Sun Feb  1 11:01:59 2026] after 36b, move used:24 wresv:53 bresv:10 game:1316\\n1769943738 [Sun Feb  1 11:02:18 2026] move 37w received from w [Ra3n Db4e Dc4n Ef4n]\\n1769943738 [Sun Feb  1 11:02:18 2026] after 37w, move used:19 wresv:49 bresv:10 game:1335\\n1769943738 [Sun Feb  1 11:02:18 2026] w player present\\n1769943757 [Sun Feb  1 11:02:37 2026] move 37b received from b [Dc5s mb5e dd5e dc6e]\\n1769943757 [Sun Feb  1 11:02:37 2026] after 37b, move used:19 wresv:49 bresv:6 game:1354\\n1769943774 [Sun Feb  1 11:02:54 2026] move 38w received from w [Ra4e Dc4e Dd4s Rb4w]\\n1769943774 [Sun Feb  1 11:02:54 2026] after 38w, move used:17 wresv:47 bresv:6 game:1371\\n1769943774 [Sun Feb  1 11:02:54 2026] w player present\\n1769943791 [Sun Feb  1 11:03:11 2026] move 38b received from b [de5s de4e df4e mc5w]\\n1769943791 [Sun Feb  1 11:03:11 2026] after 38b, move used:17 wresv:47 bresv:4 game:1388\\n1769943807 [Sun Feb  1 11:03:27 2026] move 39w received from w [Ef5w Ee5e re6s]\\n1769943807 [Sun Feb  1 11:03:27 2026] after 39w, move used:16 wresv:46 bresv:4 game:1404\\n1769943807 [Sun Feb  1 11:03:27 2026] w player present\\n1769943824 [Sun Feb  1 11:03:44 2026] move 39b received from b [rc7e rd7e re7e cb7e]\\n1769943824 [Sun Feb  1 11:03:44 2026] after 39b, move used:17 wresv:46 bresv:2 game:1421\\n1769943840 [Sun Feb  1 11:04:00 2026] move 40w received from w [Db3e re5s Ef5w Ee5w]\\n1769943840 [Sun Feb  1 11:04:00 2026] after 40w, move used:16 wresv:45 bresv:2 game:1437\\n1769943841 [Sun Feb  1 11:04:01 2026] w player present\\n1769943850 [Sun Feb  1 11:04:10 2026] move 40b received from b [re4e mb5s mb4n Ra4e]\\n1769943850 [Sun Feb  1 11:04:10 2026] after 40b, move used:10 wresv:45 bresv:7 game:1447\\n1769943863 [Sun Feb  1 11:04:23 2026] move 41w received from w [Dc3n Dc4n Dc5n Ed5w]\\n1769943863 [Sun Feb  1 11:04:23 2026] after 41w, move used:13 wresv:47 bresv:7 game:1460\\n1769943863 [Sun Feb  1 11:04:23 2026] w player present\\n1769943870 [Sun Feb  1 11:04:30 2026] move 41b received from b [ee3n Ce2n Rf2w hg2w]\\n1769943870 [Sun Feb  1 11:04:30 2026] after 41b, move used:7 wresv:47 bresv:15 game:1467\\n1769943887 [Sun Feb  1 11:04:47 2026] move 42w received from w [cc7n Dc6n Rd1e Cg1n]\\n1769943887 [Sun Feb  1 11:04:47 2026] after 42w, move used:17 wresv:45 bresv:15 game:1484\\n1769943887 [Sun Feb  1 11:04:47 2026] w player present\\n1769943903 [Sun Feb  1 11:05:03 2026] move 42b received from b [ee4w ed4n dd6e ed5n]\\n1769943903 [Sun Feb  1 11:05:03 2026] after 42b, move used:16 wresv:45 bresv:14 game:1500\\n1769943912 [Sun Feb  1 11:05:12 2026] move 43w received from w [Ec5e mb5e mc5s Ed5w]\\n1769943912 [Sun Feb  1 11:05:12 2026] after 43w, move used:9 wresv:51 bresv:14 game:1509\\n1769943913 [Sun Feb  1 11:05:13 2026] w player present\\n1769943928 [Sun Feb  1 11:05:28 2026] move 43b received from b [ed6n ed7s Dc7e cc8s]\\n1769943928 [Sun Feb  1 11:05:28 2026] after 43b, move used:16 wresv:51 bresv:13 game:1525\\n1769943938 [Sun Feb  1 11:05:38 2026] move 44w received from w [mc4s mc3x Ec5s Ec4n Rb4n]\\n1769943938 [Sun Feb  1 11:05:38 2026] after 44w, move used:10 wresv:56 bresv:13 game:1535\\n1769943939 [Sun Feb  1 11:05:39 2026] w player present\\n1769943953 [Sun Feb  1 11:05:53 2026] move 44b received from b [de6s de5s de4n Ce3n]\\n1769943953 [Sun Feb  1 11:05:53 2026] after 44b, move used:15 wresv:56 bresv:13 game:1550\\n1769943974 [Sun Feb  1 11:06:14 2026] move 45w received from w [Dd3n Ce4s Ec5e Rb5e]\\n1769943974 [Sun Feb  1 11:06:14 2026] after 45w, move used:21 wresv:50 bresv:13 game:1571\\n1769943974 [Sun Feb  1 11:06:14 2026] w player present\\n1769943990 [Sun Feb  1 11:06:30 2026] move 45b received from b [dg4n dg5w de5n de6n]\\n1769943990 [Sun Feb  1 11:06:30 2026] after 45b, move used:16 wresv:50 bresv:12 game:1587\\n1769944008 [Sun Feb  1 11:06:48 2026] move 46w received from w [Rc5n Dd4w Dc4n Rc1e]\\n1769944008 [Sun Feb  1 11:06:48 2026] after 46w, move used:18 wresv:47 bresv:12 game:1605\\n1769944008 [Sun Feb  1 11:06:48 2026] w player present\\n1769944021 [Sun Feb  1 11:07:01 2026] move 46b received from b [ed6e Dd7s de7w rb8s]\\n1769944021 [Sun Feb  1 11:07:01 2026] after 46b, move used:13 wresv:47 bresv:14 game:1618\\n1769944036 [Sun Feb  1 11:07:16 2026] move 47w received from w [Mb6s Rc6w Dd6w]\\n1769944036 [Sun Feb  1 11:07:16 2026] after 47w, move used:15 wresv:47 bresv:14 game:1633\\n1769944036 [Sun Feb  1 11:07:16 2026] w player present\\n1769944045 [Sun Feb  1 11:07:25 2026] move 47b received from b [cc7n dd7w ee6w cc8w]\\n1769944045 [Sun Feb  1 11:07:25 2026] after 47b, move used:9 wresv:47 bresv:20 game:1642\\n1769944055 [Sun Feb  1 11:07:35 2026] move 48w received from w [Ce3n rf3w re3w Ce4s]\\n1769944055 [Sun Feb  1 11:07:35 2026] after 48w, move used:10 wresv:52 bresv:20 game:1652\\n1769944056 [Sun Feb  1 11:07:36 2026] w player present\\n1769944060 [Sun Feb  1 11:07:40 2026] move 48b received from b [ed6n Dc6e Dd6e ed7s]\\n1769944060 [Sun Feb  1 11:07:40 2026] after 48b, move used:5 wresv:52 bresv:30 game:1657\\n1769944077 [Sun Feb  1 11:07:57 2026] move 49w received from w [Rb6e Ed5e Mb5n De6n]\\n1769944077 [Sun Feb  1 11:07:57 2026] after 49w, move used:17 wresv:50 bresv:30 game:1674\\n1769944077 [Sun Feb  1 11:07:57 2026] w player present\\n1769944103 [Sun Feb  1 11:08:23 2026] move 49b received from b [ed6s dc7e rb7e dd7s]\\n1769944103 [Sun Feb  1 11:08:23 2026] after 49b, move used:26 wresv:50 bresv:19 game:1700\\n1769944118 [Sun Feb  1 11:08:38 2026] move 50w received from w [rd3w rc3x Ce3w De7w Ee5n]\\n1769944118 [Sun Feb  1 11:08:38 2026] after 50w, move used:15 wresv:50 bresv:19 game:1715\\n1769944118 [Sun Feb  1 11:08:38 2026] w player present\\n1769944127 [Sun Feb  1 11:08:47 2026] move 50b received from b [Dc5s ed5w ra7e cb8e]\\n1769944127 [Sun Feb  1 11:08:47 2026] after 50b, move used:9 wresv:50 bresv:25 game:1724\\n1769944143 [Sun Feb  1 11:09:03 2026] move 51w received from w [dd6s Ee6w Re2n Re3n]\\n1769944143 [Sun Feb  1 11:09:03 2026] after 51w, move used:16 wresv:49 bresv:25 game:1740\\n1769944144 [Sun Feb  1 11:09:04 2026] w player present\\n1769944152 [Sun Feb  1 11:09:12 2026] move 51b received from b [rb7w ec5w Mb6n eb5n]\\n1769944152 [Sun Feb  1 11:09:12 2026] after 51b, move used:9 wresv:49 bresv:31 game:1749\\n1769944159 [Sun Feb  1 11:09:19 2026] move 52w received from w [Dc4n Dd7n rc7e Rc6n]\\n1769944159 [Sun Feb  1 11:09:19 2026] after 52w, move used:7 wresv:57 bresv:31 game:1756\\n1769944159 [Sun Feb  1 11:09:19 2026] w player present\\n1769944164 [Sun Feb  1 11:09:24 2026] move 52b received from b [Mb7n eb6n Rc7s eb7e]\\n1769944164 [Sun Feb  1 11:09:24 2026] after 52b, move used:5 wresv:57 bresv:41 game:1761\\n1769944170 [Sun Feb  1 11:09:30 2026] move 53w received from w [Rc6w Dc5w Rb6n Db5n]\\n1769944170 [Sun Feb  1 11:09:30 2026] after 53w, move used:6 wresv:66 bresv:41 game:1767\\n1769944170 [Sun Feb  1 11:09:30 2026] w player present\\n1769944183 [Sun Feb  1 11:09:43 2026] move 53b received from b [Db6s ha6e hb6w Rb7s]\\n1769944183 [Sun Feb  1 11:09:43 2026] after 53b, move used:13 wresv:66 bresv:43 game:1780\\n1769944188 [Sun Feb  1 11:09:48 2026] move 54w received from w [Rb6n Db5n Mb8w Rb7n]\\n1769944188 [Sun Feb  1 11:09:48 2026] after 54w, move used:5 wresv:76 bresv:43 game:1785\\n1769944188 [Sun Feb  1 11:09:48 2026] game finished with result w g\\n\n";

    [Fact]
    public void FromTsv_ParsesWholeGivenLine_669123()
    {
        // Full TSV header from sample file
        var header = string.Join('\t', new[]
        {
            "id","wplayerid","bplayerid","wusername","busername","wtitle","btitle","wcountry","bcountry",
            "wrating","brating","wratingk","bratingk","wtype","btype","event","site","timecontrol","postal",
            "startts","endts","result","termination","plycount","mode","rated","corrupt","movelist","events"
        });

        // The user-provided row (with \n as separators inside movelist/events fields)
        var lineRaw = @"669123	63152	16866	bot_dolores_v1	browni3141			US	US	2630	2565	30	30	b	h	Casual game	Over the Net	15s/2m30s/100	0	1769942402	1769944188	w	g	54	IGS	1	0	1w Da1 Db1 Rc1 Rd1 Ce1 Rf1 Cg1 Eh1 Ha2 Rb2 Rc2 Rd2 Me2 Rf2 Rg2 Hh2\n1b ee7 mb7 ha7 hh7 dd7 de8 cg7 cb8 rc7 rf7 ra8 rc8 rd8 rf8 rg8 rh8\n2w Hh2n Hh3n Eh1n Eh2n\n2b ee7s ee6s hh7s ee5s\n3w Eh3w Eg3w Ef3w Ha2n\n3b ee4w ed4s ed3n Rd2n\n4w Rd3w Rc3w Ee3n Ee4n\n4b hh6w mb7s ha7s cb8s\n5w Rg2e Ha3n Da1n Da2n\n5b ed4w ec4w Ha4n eb4w\n6w Ee5e Ef5e Eg5w hg6s\n6b rh8s rh7s rh6s hg5n\n7w Hh4s rh5s Hh3w rh4s\n7b Ha5e ea4n mb6e Hb5n\n8w Ef5e Eg5w hg6s Rb2w\n8b cg7s hg5e mc6e Hb6e Hc6x\n9w Db1n Ef5w Ee5n Cg1n\n9b ea5s ea4e Da3n ha6s\n10w Hg3n rh3w rg3w rf3x Hg4s\n10b ha5e Da4n eb4e ec4e\n11w Hg3n Hg4n cg6n Hg5n\n11b ed4e md6s md5e me5e\n12w Hg6w rf7w Hf6n Ee6e\n12b ee4e mf5e mg5n mg6e\n13w Me2w Md2n Md3w Ef6e\n13b hb5n Da5e ef4n cg7e\n14w Rb3n Db5w Rb4w Mc3w\n14b ch7n mh6n mh7w ch8s\n15w Eg6w mg7s mg6e Ef6e\n15b ch7n mh6n mh7w ch8s\n16w Eg6w mg7s mg6s Ef6e\n16b ef5s mg5w dd7s dd6e\n17w Eg6w mf5w Ef6s Ra4e\n17b rg8s rg7s me5w md5n\n18w Ef5n de6s Ef6w Rh2n\n18b hh5w hg5w de5w md6n\n19w Da5s Hf7s rg6n Hf6e\n19b re7e rf7s md7e me7e\n20w Hg6s Hg5s Cg2n Ee6s\n20b hf5e ef4w Hg4w rf6e\n21w Ee5e Hf4e Cg3s Hg4s\n21b hb6s hb5n Rb4n dd5n\n22w Mb3n Rb5w hg5s Ef5e\n22b ee4e hg4e Rh3s hh4s\n23w Eg5s rg6s Ce1n Db2n\n23b hb6w Rh2s hh3s ef4w\n24w Eg4w rg5s Ef4n Ef5w\n24b rg7s Cg2s hh2w ee4s\n25w Hg3e rg4s rg3w Hh3w\n25b mf7s rf8s de8s cb7s\n26w Ee5e Ef5s mf6s Mb4n\n26b cb6n de7s de6s mf5e\n27w Mb5n Da4e Db4e Dc4n\n27b dd6w de5w rd8s rd7s\n28w Ef4n Ef5s mg5w Mb6s\n28b rf7s mf5e ch7s ch6s\n29w Ef4n Ef5s mg5w Mb5n\n29b mf5e ra8s rc8w mg5s\n30w Ef4n mg4w mf4w Ef5s\n30b me4n me5e mf5e mg5s\n31w Ef4n mg4w mf4w Ef5s\n31b me4n me5e mf5e rf6s\n32w rf5n Ef4n Ef5s mg5w\n32b mf5e rf6s mg5s ch5s\n33w mg4n Ef4e mg5e Eg4n\n33b rd6e dd5e rf5s de5w\n34w Eg5w rf4e Ef5s Ra2n\n34b mh5w ch4n rg4e mg5s\n35w Ef4n mg4w mf4w Ef5s\n35b me4w md4w Dc5w mc4n\n36w Hg3e Hh3w rh4s Db5s\n36b ch5s rh3s ch4s mc5w\n37w Ra3n Db4e Dc4n Ef4n\n37b Dc5s mb5e dd5e dc6e\n38w Ra4e Dc4e Dd4s Rb4w\n38b de5s de4e df4e mc5w\n39w Ef5w Ee5e re6s\n39b rc7e rd7e re7e cb7e\n40w Db3e re5s Ef5w Ee5w\n40b re4e mb5s mb4n Ra4e\n41w Dc3n Dc4n Dc5n Ed5w\n41b ee3n Ce2n Rf2w hg2w\n42w cc7n Dc6n Rd1e Cg1n\n42b ee4w ed4n dd6e ed5n\n43w Ec5e mb5e mc5s Ed5w\n43b ed6n ed7s Dc7e cc8s\n44w mc4s mc3x Ec5s Ec4n Rb4n\n44b de6s de5s de4n Ce3n\n45w Dd3n Ce4s Ec5e Rb5e\n45b dg4n dg5w de5n de6n\n46w Rc5n Dd4w Dc4n Rc1e\n46b ed6e Dd7s de7w rb8s\n47w Mb6s Rc6w Dd6w\n47b cc7n dd7w ee6w cc8w\n48w Ce3n rf3w re3w Ce4s\n48b ed6n Dc6e Dd6e ed7s\n49w Rb6e Ed5e Mb5n De6n\n49b ed6s dc7e rb7e dd7s\n50w rd3w rc3x Ce3w De7w Ee5n\n50b Dc5s ed5w ra7e cb8e\n51w dd6s Ee6w Re2n Re3n\n51b rb7w ec5w Mb6n eb5n\n52w Dc4n Dd7n rc7e Rc6n\n52b Mb7n eb6n Rc7s eb7e\n53w Rc6w Dc5w Rb6n Db5n\n53b Db6s ha6e hb6w Rb7s\n54w Rb6n Db5n Mb8w Rb7n	1769942372 [Sun Feb  1 10:39:32 2026] b player joining\n1769942372 [Sun Feb  1 10:39:32 2026] b player present\n1769942400 [Sun Feb  1 10:40:00 2026] w player joining\n1769942401 [Sun Feb  1 10:40:01 2026] w player present\n1769942402 [Sun Feb  1 10:40:02 2026] w player present\n1769942403 [Sun Feb  1 10:40:03 2026] start move received from w\n1769942416 [Sun Feb  1 10:40:16 2026] move 1w received from w [Da1 Db1 Rc1 Rd1 Ce1 Rf1 Cg1 Eh1 Ha2 Rb2 Rc2 Rd2 Me2 Rf2 Rg2 Hh2]\n1769942416 [Sun Feb  1 10:40:16 2026] after 1w, move used:13 wresv:150 bresv:150 game:13\n1769942417 [Sun Feb  1 10:40:17 2026] w player present\n1769942432 [Sun Feb  1 10:40:32 2026] move 1b received from b [ee7 mb7 ha7 hh7 dd7 de8 cg7 cb8 rc7 rf7 ra8 rc8 rd8 rf8 rg8 rh8]\n1769942432 [Sun Feb  1 10:40:32 2026] after 1b, move used:16 wresv:150 bresv:150 game:29\n1769942449 [Sun Feb  1 10:40:49 2026] move 2w received from w [Hh2n Hh3n Eh1n Eh2n]\n1769942449 [Sun Feb  1 10:40:49 2026] after 2w, move used:17 wresv:148 bresv:150 game:46\n1769942449 [Sun Feb  1 10:40:49 2026] w player present\n1769942470 [Sun Feb  1 10:41:10 2026] move 2b received from b [ee7s ee6s hh7s ee5s]\n1769942471 [Sun Feb  1 10:41:11 2026] after 2b, move used:22 wresv:148 bresv:143 game:68\n1769942483 [Sun Feb  1 10:41:23 2026] move 3w received from w [Eh3w Eg3w Ef3w Ha2n]\n1769942484 [Sun Feb  1 10:41:24 2026] after 3w, move used:13 wresv:150 bresv:143 game:81\n1769942484 [Sun Feb  1 10:41:24 2026] w player present\n1769942495 [Sun Feb  1 10:41:35 2026] move 3b received from b [ee4w ed4s ed3n Rd2n]\n1769942495 [Sun Feb  1 10:41:35 2026] after 3b, move used:11 wresv:150 bresv:147 game:92\n1769942511 [Sun Feb  1 10:41:51 2026] move 4w received from w [Rd3w Rc3w Ee3n Ee4n]\n1769942511 [Sun Feb  1 10:41:51 2026] after 4w, move used:16 wresv:149 bresv:147 game:108\n1769942511 [Sun Feb  1 10:41:51 2026] w player present\n1769942540 [Sun Feb  1 10:42:20 2026] move 4b received from b [hh6w mb7s ha7s cb8s]\n1769942540 [Sun Feb  1 10:42:20 2026] after 4b, move used:29 wresv:149 bresv:133 game:137\n1769942608 [Sun Feb  1 10:43:28 2026] move 5w received from w [Rg2e Ha3n Da1n Da2n]\n1769942608 [Sun Feb  1 10:43:28 2026] after 5w, move used:68 wresv:96 bresv:133 game:205\n1769942608 [Sun Feb  1 10:43:28 2026] w player present\n1769942629 [Sun Feb  1 10:43:49 2026] move 5b received from b [ed4w ec4w Ha4n eb4w]\n1769942629 [Sun Feb  1 10:43:49 2026] after 5b, move used:21 wresv:96 bresv:127 game:226\n1769942656 [Sun Feb  1 10:44:16 2026] move 6w received from w [Ee5e Ef5e Eg5w hg6s]\n1769942656 [Sun Feb  1 10:44:16 2026] after 6w, move used:27 wresv:84 bresv:127 game:253\n1769942657 [Sun Feb  1 10:44:17 2026] w player present\n1769942680 [Sun Feb  1 10:44:40 2026] move 6b received from b [rh8s rh7s rh6s hg5n]\n1769942680 [Sun Feb  1 10:44:40 2026] after 6b, move used:24 wresv:84 bresv:118 game:277\n1769942714 [Sun Feb  1 10:45:14 2026] move 7w received from w [Hh4s rh5s Hh3w rh4s]\n1769942714 [Sun Feb  1 10:45:14 2026] after 7w, move used:34 wresv:65 bresv:118 game:311\n1769942714 [Sun Feb  1 10:45:14 2026] w player present\n1769942721 [Sun Feb  1 10:45:21 2026] move 7b received from b [Ha5e ea4n mb6e Hb5n]\n1769942721 [Sun Feb  1 10:45:21 2026] after 7b, move used:7 wresv:65 bresv:126 game:318\n1769942747 [Sun Feb  1 10:45:47 2026] move 8w received from w [Ef5e Eg5w hg6s Rb2w]\n1769942747 [Sun Feb  1 10:45:47 2026] after 8w, move used:26 wresv:54 bresv:126 game:344\n1769942747 [Sun Feb  1 10:45:47 2026] w player present\n1769942777 [Sun Feb  1 10:46:17 2026] move 8b received from b [cg7s hg5e mc6e Hb6e Hc6x]\n1769942777 [Sun Feb  1 10:46:17 2026] after 8b, move used:30 wresv:54 bresv:111 game:374\n1769942797 [Sun Feb  1 10:46:37 2026] move 9w received from w [Db1n Ef5w Ee5n Cg1n]\n1769942797 [Sun Feb  1 10:46:37 2026] after 9w, move used:20 wresv:49 bresv:111 game:394\n1769942797 [Sun Feb  1 10:46:37 2026] w player present\n1769942824 [Sun Feb  1 10:47:04 2026] move 9b received from b [ea5s ea4e Da3n ha6s]\n1769942824 [Sun Feb  1 10:47:04 2026] after 9b, move used:27 wresv:49 bresv:99 game:421\n1769942840 [Sun Feb  1 10:47:20 2026] move 10w received from w [Hg3n rh3w rg3w rf3x Hg4s]\n1769942840 [Sun Feb  1 10:47:20 2026] after 10w, move used:16 wresv:48 bresv:99 game:437\n1769942841 [Sun Feb  1 10:47:21 2026] w player present\n1769942869 [Sun Feb  1 10:47:49 2026] move 10b received from b [ha5e Da4n eb4e ec4e]\n1769942869 [Sun Feb  1 10:47:49 2026] after 10b, move used:29 wresv:48 bresv:85 game:466\n1769942884 [Sun Feb  1 10:48:04 2026] move 11w received from w [Hg3n Hg4n cg6n Hg5n]\n1769942884 [Sun Feb  1 10:48:04 2026] after 11w, move used:15 wresv:48 bresv:85 game:481\n1769942884 [Sun Feb  1 10:48:04 2026] w player present\n1769942902 [Sun Feb  1 10:48:22 2026] move 11b received from b [ed4e md6s md5e me5e]\n1769942902 [Sun Feb  1 10:48:22 2026] after 11b, move used:18 wresv:48 bresv:82 game:499\n1769942914 [Sun Feb  1 10:48:34 2026] move 12w received from w [Hg6w rf7w Hf6n Ee6e]\n1769942914 [Sun Feb  1 10:48:34 2026] after 12w, move used:12 wresv:51 bresv:82 game:511\n1769942914 [Sun Feb  1 10:48:34 2026] w player present\n1769942963 [Sun Feb  1 10:49:23 2026] move 12b received from b [ee4e mf5e mg5n mg6e]\n1769942963 [Sun Feb  1 10:49:23 2026] after 12b, move used:49 wresv:51 bresv:48 game:560\n1769942980 [Sun Feb  1 10:49:40 2026] move 13w received from w [Me2w Md2n Md3w Ef6e]\n1769942980 [Sun Feb  1 10:49:40 2026] after 13w, move used:17 wresv:49 bresv:48 game:577\n1769942981 [Sun Feb  1 10:49:41 2026] w player present\n1769943013 [Sun Feb  1 10:50:13 2026] move 13b received from b [hb5n Da5e ef4n cg7e]\n1769943013 [Sun Feb  1 10:50:13 2026] after 13b, move used:33 wresv:49 bresv:30 game:610\n1769943030 [Sun Feb  1 10:50:30 2026] move 14w received from w [Rb3n Db5w Rb4w Mc3w]\n1769943030 [Sun Feb  1 10:50:30 2026] after 14w, move used:17 wresv:47 bresv:30 game:627\n1769943031 [Sun Feb  1 10:50:31 2026] w player present\n1769943040 [Sun Feb  1 10:50:40 2026] move 14b received from b [ch7n mh6n mh7w ch8s]\n1769943040 [Sun Feb  1 10:50:40 2026] after 14b, move used:10 wresv:47 bresv:35 game:637\n1769943055 [Sun Feb  1 10:50:55 2026] move 15w received from w [Eg6w mg7s mg6e Ef6e]\n1769943055 [Sun Feb  1 10:50:55 2026] after 15w, move used:15 wresv:47 bresv:35 game:652\n1769943055 [Sun Feb  1 10:50:55 2026] w player present\n1769943059 [Sun Feb  1 10:50:59 2026] move 15b received from b [ch7n mh6n mh7w ch8s]\n1769943059 [Sun Feb  1 10:50:59 2026] after 15b, move used:4 wresv:47 bresv:46 game:656\n1769943070 [Sun Feb  1 10:51:10 2026] move 16w received from w [Eg6w mg7s mg6s Ef6e]\n1769943070 [Sun Feb  1 10:51:10 2026] after 16w, move used:11 wresv:51 bresv:46 game:667\n1769943070 [Sun Feb  1 10:51:10 2026] w player present\n1769943082 [Sun Feb  1 10:51:22 2026] move 16b received from b [ef5s mg5w dd7s dd6e]\n1769943082 [Sun Feb  1 10:51:22 2026] after 16b, move used:12 wresv:51 bresv:49 game:679\n1769943102 [Sun Feb  1 10:51:42 2026] move 17w received from w [Eg6w mf5w Ef6s Ra4e]\n1769943102 [Sun Feb  1 10:51:42 2026] after 17w, move used:20 wresv:46 bresv:49 game:699\n1769943102 [Sun Feb  1 10:51:42 2026] w player present\n1769943148 [Sun Feb  1 10:52:28 2026] move 17b received from b [rg8s rg7s me5w md5n]\n1769943148 [Sun Feb  1 10:52:28 2026] after 17b, move used:46 wresv:46 bresv:18 game:745\n1769943162 [Sun Feb  1 10:52:42 2026] move 18w received from w [Ef5n de6s Ef6w Rh2n]\n1769943162 [Sun Feb  1 10:52:42 2026] after 18w, move used:14 wresv:47 bresv:18 game:759\n1769943163 [Sun Feb  1 10:52:43 2026] w player present\n1769943177 [Sun Feb  1 10:52:57 2026] move 18b received from b [hh5w hg5w de5w md6n]\n1769943177 [Sun Feb  1 10:52:57 2026] after 18b, move used:15 wresv:47 bresv:18 game:774\n1769943190 [Sun Feb  1 10:53:10 2026] move 19w received from w [Da5s Hf7s rg6n Hf6e]\n1769943190 [Sun Feb  1 10:53:10 2026] after 19w, move used:13 wresv:49 bresv:18 game:787\n1769943190 [Sun Feb  1 10:53:10 2026] w player present\n1769943196 [Sun Feb  1 10:53:16 2026] move 19b received from b [re7e rf7s md7e me7e]\n1769943196 [Sun Feb  1 10:53:16 2026] after 19b, move used:6 wresv:49 bresv:27 game:793\n1769943214 [Sun Feb  1 10:53:34 2026] move 20w received from w [Hg6s Hg5s Cg2n Ee6s]\n1769943214 [Sun Feb  1 10:53:34 2026] after 20w, move used:18 wresv:46 bresv:27 game:811\n1769943214 [Sun Feb  1 10:53:34 2026] w player present\n1769943229 [Sun Feb  1 10:53:49 2026] move 20b received from b [hf5e ef4w Hg4w rf6e]\n1769943229 [Sun Feb  1 10:53:49 2026] after 20b, move used:15 wresv:46 bresv:27 game:826\n1769943244 [Sun Feb  1 10:54:04 2026] move 21w received from w [Ee5e Hf4e Cg3s Hg4s]\n1769943244 [Sun Feb  1 10:54:04 2026] after 21w, move used:15 wresv:46 bresv:27 game:841\n1769943244 [Sun Feb  1 10:54:04 2026] w player present\n1769943262 [Sun Feb  1 10:54:22 2026] move 21b received from b [hb6s hb5n Rb4n dd5n]\n1769943262 [Sun Feb  1 10:54:22 2026] after 21b, move used:18 wresv:46 bresv:24 game:859\n1769943274 [Sun Feb  1 10:54:34 2026] move 22w received from w [Mb3n Rb5w hg5s Ef5e]\n1769943274 [Sun Feb  1 10:54:34 2026] after 22w, move used:12 wresv:49 bresv:24 game:871\n1769943275 [Sun Feb  1 10:54:35 2026] w player present\n1769943280 [Sun Feb  1 10:54:40 2026] move 22b received from b [ee4e hg4e Rh3s hh4s]\n1769943280 [Sun Feb  1 10:54:40 2026] after 22b, move used:6 wresv:49 bresv:33 game:877\n1769943297 [Sun Feb  1 10:54:57 2026] move 23w received from w [Eg5s rg6s Ce1n Db2n]\n1769943297 [Sun Feb  1 10:54:57 2026] after 23w, move used:17 wresv:47 bresv:33 game:894\n1769943298 [Sun Feb  1 10:54:58 2026] w player present\n1769943322 [Sun Feb  1 10:55:22 2026] move 23b received from b [hb6w Rh2s hh3s ef4w]\n1769943322 [Sun Feb  1 10:55:22 2026] after 23b, move used:25 wresv:47 bresv:23 game:919\n1769943337 [Sun Feb  1 10:55:37 2026] move 24w received from w [Eg4w rg5s Ef4n Ef5w]\n1769943337 [Sun Feb  1 10:55:37 2026] after 24w, move used:15 wresv:47 bresv:23 game:934\n1769943338 [Sun Feb  1 10:55:38 2026] w player present\n1769943359 [Sun Feb  1 10:55:59 2026] move 24b received from b [rg7s Cg2s hh2w ee4s]\n1769943359 [Sun Feb  1 10:55:59 2026] after 24b, move used:22 wresv:47 bresv:16 game:956\n1769943372 [Sun Feb  1 10:56:12 2026] move 25w received from w [Hg3e rg4s rg3w Hh3w]\n1769943372 [Sun Feb  1 10:56:12 2026] after 25w, move used:13 wresv:49 bresv:16 game:969\n1769943373 [Sun Feb  1 10:56:13 2026] w player present\n1769943389 [Sun Feb  1 10:56:29 2026] move 25b received from b [mf7s rf8s de8s cb7s]\n1769943389 [Sun Feb  1 10:56:29 2026] after 25b, move used:17 wresv:49 bresv:14 game:986\n1769943407 [Sun Feb  1 10:56:47 2026] move 26w received from w [Ee5e Ef5s mf6s Mb4n]\n1769943407 [Sun Feb  1 10:56:47 2026] after 26w, move used:18 wresv:46 bresv:14 game:1004\n1769943408 [Sun Feb  1 10:56:48 2026] w player present\n1769943417 [Sun Feb  1 10:56:57 2026] move 26b received from b [cb6n de7s de6s mf5e]\n1769943417 [Sun Feb  1 10:56:57 2026] after 26b, move used:10 wresv:46 bresv:19 game:1014\n1769943434 [Sun Feb  1 10:57:14 2026] move 27w received from w [Mb5n Da4e Db4e Dc4n]\n1769943434 [Sun Feb  1 10:57:14 2026] after 27w, move used:17 wresv:44 bresv:19 game:1031\n1769943435 [Sun Feb  1 10:57:15 2026] w player present\n1769943445 [Sun Feb  1 10:57:25 2026] move 27b received from b [dd6w de5w rd8s rd7s]\n1769943445 [Sun Feb  1 10:57:25 2026] after 27b, move used:11 wresv:44 bresv:23 game:1042\n1769943462 [Sun Feb  1 10:57:42 2026] move 28w received from w [Ef4n Ef5s mg5w Mb6s]\n1769943462 [Sun Feb  1 10:57:42 2026] after 28w, move used:17 wresv:42 bresv:23 game:1059\n1769943462 [Sun Feb  1 10:57:42 2026] w player present\n1769943490 [Sun Feb  1 10:58:10 2026] move 28b received from b [rf7s mf5e ch7s ch6s]\n1769943490 [Sun Feb  1 10:58:10 2026] after 28b, move used:28 wresv:42 bresv:10 game:1087\n1769943504 [Sun Feb  1 10:58:24 2026] move 29w received from w [Ef4n Ef5s mg5w Mb5n]\n1769943504 [Sun Feb  1 10:58:24 2026] after 29w, move used:14 wresv:43 bresv:10 game:1101\n1769943505 [Sun Feb  1 10:58:25 2026] w player present\n1769943515 [Sun Feb  1 10:58:35 2026] move 29b received from b [mf5e ra8s rc8w mg5s]\n1769943515 [Sun Feb  1 10:58:35 2026] after 29b, move used:11 wresv:43 bresv:14 game:1112\n1769943525 [Sun Feb  1 10:58:45 2026] move 30w received from w [Ef4n mg4w mf4w Ef5s]\n1769943525 [Sun Feb  1 10:58:45 2026] after 30w, move used:10 wresv:48 bresv:14 game:1122\n1769943526 [Sun Feb  1 10:58:46 2026] w player present\n1769943529 [Sun Feb  1 10:58:49 2026] move 30b received from b [me4n me5e mf5e mg5s]\n1769943529 [Sun Feb  1 10:58:49 2026] after 30b, move used:4 wresv:48 bresv:25 game:1126\n1769943541 [Sun Feb  1 10:59:01 2026] move 31w received from w [Ef4n mg4w mf4w Ef5s]\n1769943541 [Sun Feb  1 10:59:01 2026] after 31w, move used:12 wresv:51 bresv:25 game:1138\n1769943542 [Sun Feb  1 10:59:02 2026] w player present\n1769943549 [Sun Feb  1 10:59:09 2026] move 31b received from b [me4n me5e mf5e rf6s]\n1769943549 [Sun Feb  1 10:59:09 2026] after 31b, move used:8 wresv:51 bresv:32 game:1146\n1769943566 [Sun Feb  1 10:59:26 2026] move 32w received from w [rf5n Ef4n Ef5s mg5w]\n1769943566 [Sun Feb  1 10:59:26 2026] after 32w, move used:17 wresv:49 bresv:32 game:1163\n1769943567 [Sun Feb  1 10:59:27 2026] w player present\n1769943585 [Sun Feb  1 10:59:45 2026] move 32b received from b [mf5e rf6s mg5s ch5s]\n1769943585 [Sun Feb  1 10:59:45 2026] after 32b, move used:19 wresv:49 bresv:28 game:1182\n1769943599 [Sun Feb  1 10:59:59 2026] move 33w received from w [mg4n Ef4e mg5e Eg4n]\n1769943599 [Sun Feb  1 10:59:59 2026] after 33w, move used:14 wresv:50 bresv:28 game:1196\n1769943599 [Sun Feb  1 10:59:59 2026] w player present\n1769943631 [Sun Feb  1 11:00:31 2026] move 33b received from b [rd6e dd5e rf5s de5w]\n1769943631 [Sun Feb  1 11:00:31 2026] after 33b, move used:32 wresv:50 bresv:11 game:1228\n1769943650 [Sun Feb  1 11:00:50 2026] move 34w received from w [Eg5w rf4e Ef5s Ra2n]\n1769943650 [Sun Feb  1 11:00:50 2026] after 34w, move used:19 wresv:46 bresv:11 game:1247\n1769943650 [Sun Feb  1 11:00:50 2026] w player present\n1769943658 [Sun Feb  1 11:00:58 2026] move 34b received from b [mh5w ch4n rg4e mg5s]\n1769943658 [Sun Feb  1 11:00:58 2026] after 34b, move used:8 wresv:46 bresv:18 game:1255\n1769943666 [Sun Feb  1 11:01:06 2026] move 35w received from w [Ef4n mg4w mf4w Ef5s]\n1769943666 [Sun Feb  1 11:01:06 2026] after 35w, move used:8 wresv:53 bresv:18 game:1263\n1769943667 [Sun Feb  1 11:01:07 2026] w player present\n1769943680 [Sun Feb  1 11:01:20 2026] move 35b received from b [me4w md4w Dc5w mc4n]\n1769943680 [Sun Feb  1 11:01:20 2026] after 35b, move used:14 wresv:53 bresv:19 game:1277\n1769943695 [Sun Feb  1 11:01:35 2026] move 36w received from w [Hg3e Hh3w rh4s Db5s]\n1769943695 [Sun Feb  1 11:01:35 2026] after 36w, move used:15 wresv:53 bresv:19 game:1292\n1769943696 [Sun Feb  1 11:01:36 2026] w player present\n1769943719 [Sun Feb  1 11:01:59 2026] move 36b received from b [ch5s rh3s ch4s mc5w]\n1769943719 [Sun Feb  1 11:01:59 2026] after 36b, move used:24 wresv:53 bresv:10 game:1316\n1769943738 [Sun Feb  1 11:02:18 2026] move 37w received from w [Ra3n Db4e Dc4n Ef4n]\n1769943738 [Sun Feb  1 11:02:18 2026] after 37w, move used:19 wresv:49 bresv:10 game:1335\n1769943738 [Sun Feb  1 11:02:18 2026] w player present\n1769943757 [Sun Feb  1 11:02:37 2026] move 37b received from b [Dc5s mb5e dd5e dc6e]\n1769943757 [Sun Feb  1 11:02:37 2026] after 37b, move used:19 wresv:49 bresv:6 game:1354\n1769943774 [Sun Feb  1 11:02:54 2026] move 38w received from w [Ra4e Dc4e Dd4s Rb4w]\n1769943774 [Sun Feb  1 11:02:54 2026] after 38w, move used:17 wresv:47 bresv:6 game:1371\n1769943774 [Sun Feb  1 11:02:54 2026] w player present\n1769943791 [Sun Feb  1 11:03:11 2026] move 38b received from b [de5s de4e df4e mc5w]\n1769943791 [Sun Feb  1 11:03:11 2026] after 38b, move used:17 wresv:47 bresv:4 game:1388\n1769943807 [Sun Feb  1 11:03:27 2026] move 39w received from w [Ef5w Ee5e re6s]\n1769943807 [Sun Feb  1 11:03:27 2026] after 39w, move used:16 wresv:46 bresv:4 game:1404\n1769943807 [Sun Feb  1 11:03:27 2026] w player present\n1769943824 [Sun Feb  1 11:03:44 2026] move 39b received from b [rc7e rd7e re7e cb7e]\n1769943824 [Sun Feb  1 11:03:44 2026] after 39b, move used:17 wresv:46 bresv:2 game:1421\n1769943840 [Sun Feb  1 11:04:00 2026] move 40w received from w [Db3e re5s Ef5w Ee5w]\n1769943840 [Sun Feb  1 11:04:00 2026] after 40w, move used:16 wresv:45 bresv:2 game:1437\n1769943841 [Sun Feb  1 11:04:01 2026] w player present\n1769943850 [Sun Feb  1 11:04:10 2026] move 40b received from b [re4e mb5s mb4n Ra4e]\n1769943850 [Sun Feb  1 11:04:10 2026] after 40b, move used:10 wresv:45 bresv:7 game:1447\n1769943863 [Sun Feb  1 11:04:23 2026] move 41w received from w [Dc3n Dc4n Dc5n Ed5w]\n1769943863 [Sun Feb  1 11:04:23 2026] after 41w, move used:13 wresv:47 bresv:7 game:1460\n1769943863 [Sun Feb  1 11:04:23 2026] w player present\n1769943870 [Sun Feb  1 11:04:30 2026] move 41b received from b [ee3n Ce2n Rf2w hg2w]\n1769943870 [Sun Feb  1 11:04:30 2026] after 41b, move used:7 wresv:47 bresv:15 game:1467\n1769943887 [Sun Feb  1 11:04:47 2026] move 42w received from w [cc7n Dc6n Rd1e Cg1n]\n1769943887 [Sun Feb  1 11:04:47 2026] after 42w, move used:17 wresv:45 bresv:15 game:1484\n1769943887 [Sun Feb  1 11:04:47 2026] w player present\n1769943903 [Sun Feb  1 11:05:03 2026] move 42b received from b [ee4w ed4n dd6e ed5n]\n1769943903 [Sun Feb  1 11:05:03 2026] after 42b, move used:16 wresv:45 bresv:14 game:1500\n1769943912 [Sun Feb  1 11:05:12 2026] move 43w received from w [Ec5e mb5e mc5s Ed5w]\n1769943912 [Sun Feb  1 11:05:12 2026] after 43w, move used:9 wresv:51 bresv:14 game:1509\n1769943913 [Sun Feb  1 11:05:13 2026] w player present\n1769943928 [Sun Feb  1 11:05:28 2026] move 43b received from b [ed6n ed7s Dc7e cc8s]\n1769943928 [Sun Feb  1 11:05:28 2026] after 43b, move used:16 wresv:51 bresv:13 game:1525\n1769943938 [Sun Feb  1 11:05:38 2026] move 44w received from w [mc4s mc3x Ec5s Ec4n Rb4n]\n1769943938 [Sun Feb  1 11:05:38 2026] after 44w, move used:10 wresv:56 bresv:13 game:1535\n1769943939 [Sun Feb  1 11:05:39 2026] w player present\n1769943953 [Sun Feb  1 11:05:53 2026] move 44b received from b [de6s de5s de4n Ce3n]\n1769943953 [Sun Feb  1 11:05:53 2026] after 44b, move used:15 wresv:56 bresv:13 game:1550\n1769943974 [Sun Feb  1 11:06:14 2026] move 45w received from w [Dd3n Ce4s Ec5e Rb5e]\n1769943974 [Sun Feb  1 11:06:14 2026] after 45w, move used:21 wresv:50 bresv:13 game:1571\n1769943974 [Sun Feb  1 11:06:14 2026] w player present\n1769943990 [Sun Feb  1 11:06:30 2026] move 45b received from b [dg4n dg5w de5n de6n]\n1769943990 [Sun Feb  1 11:06:30 2026] after 45b, move used:16 wresv:50 bresv:12 game:1587\n1769944008 [Sun Feb  1 11:06:48 2026] move 46w received from w [Rc5n Dd4w Dc4n Rc1e]\n1769944008 [Sun Feb  1 11:06:48 2026] after 46w, move used:18 wresv:47 bresv:12 game:1605\n1769944008 [Sun Feb  1 11:06:48 2026] w player present\n1769944021 [Sun Feb  1 11:07:01 2026] move 46b received from b [ed6e Dd7s de7w rb8s]\n1769944021 [Sun Feb  1 11:07:01 2026] after 46b, move used:13 wresv:47 bresv:14 game:1618\n1769944036 [Sun Feb  1 11:07:16 2026] move 47w received from w [Mb6s Rc6w Dd6w]\n1769944036 [Sun Feb  1 11:07:16 2026] after 47w, move used:15 wresv:47 bresv:14 game:1633\n1769944036 [Sun Feb  1 11:07:16 2026] w player present\n1769944045 [Sun Feb  1 11:07:25 2026] move 47b received from b [cc7n dd7w ee6w cc8w]\n1769944045 [Sun Feb  1 11:07:25 2026] after 47b, move used:9 wresv:47 bresv:20 game:1642\n1769944055 [Sun Feb  1 11:07:35 2026] move 48w received from w [Ce3n rf3w re3w Ce4s]\n1769944055 [Sun Feb  1 11:07:35 2026] after 48w, move used:10 wresv:52 bresv:20 game:1652\n1769944056 [Sun Feb  1 11:07:36 2026] w player present\n1769944060 [Sun Feb  1 11:07:40 2026] move 48b received from b [ed6n Dc6e Dd6e ed7s]\n1769944060 [Sun Feb  1 11:07:40 2026] after 48b, move used:5 wresv:52 bresv:30 game:1657\n1769944077 [Sun Feb  1 11:07:57 2026] move 49w received from w [Rb6e Ed5e Mb5n De6n]\n1769944077 [Sun Feb  1 11:07:57 2026] after 49w, move used:17 wresv:50 bresv:30 game:1674\n1769944077 [Sun Feb  1 11:07:57 2026] w player present\n1769944103 [Sun Feb  1 11:08:23 2026] move 49b received from b [ed6s dc7e rb7e dd7s]\n1769944103 [Sun Feb  1 11:08:23 2026] after 49b, move used:26 wresv:50 bresv:19 game:1700\n1769944118 [Sun Feb  1 11:08:38 2026] move 50w received from w [rd3w rc3x Ce3w De7w Ee5n]\n1769944118 [Sun Feb  1 11:08:38 2026] after 50w, move used:15 wresv:50 bresv:19 game:1715\n1769944118 [Sun Feb  1 11:08:38 2026] w player present\n1769944127 [Sun Feb  1 11:08:47 2026] move 50b received from b [Dc5s ed5w ra7e cb8e]\n1769944127 [Sun Feb  1 11:08:47 2026] after 50b, move used:9 wresv:50 bresv:25 game:1724\n1769944143 [Sun Feb  1 11:09:03 2026] move 51w received from w [dd6s Ee6w Re2n Re3n]\n1769944143 [Sun Feb  1 11:09:03 2026] after 51w, move used:16 wresv:49 bresv:25 game:1740\n1769944144 [Sun Feb  1 11:09:04 2026] w player present\n1769944152 [Sun Feb  1 11:09:12 2026] move 51b received from b [rb7w ec5w Mb6n eb5n]\n1769944152 [Sun Feb  1 11:09:12 2026] after 51b, move used:9 wresv:49 bresv:31 game:1749\n1769944159 [Sun Feb  1 11:09:19 2026] move 52w received from w [Dc4n Dd7n rc7e Rc6n]\n1769944159 [Sun Feb  1 11:09:19 2026] after 52w, move used:7 wresv:57 bresv:31 game:1756\n1769944159 [Sun Feb  1 11:09:19 2026] w player present\n1769944164 [Sun Feb  1 11:09:24 2026] move 52b received from b [Mb7n eb6n Rc7s eb7e]\n1769944164 [Sun Feb  1 11:09:24 2026] after 52b, move used:5 wresv:57 bresv:41 game:1761\n1769944170 [Sun Feb  1 11:09:30 2026] move 53w received from w [Rc6w Dc5w Rb6n Db5n]\n1769944170 [Sun Feb  1 11:09:30 2026] after 53w, move used:6 wresv:66 bresv:41 game:1767\n1769944170 [Sun Feb  1 11:09:30 2026] w player present\n1769944183 [Sun Feb  1 11:09:43 2026] move 53b received from b [Db6s ha6e hb6w Rb7s]\n1769944183 [Sun Feb  1 11:09:43 2026] after 53b, move used:13 wresv:66 bresv:43 game:1780\n1769944188 [Sun Feb  1 11:09:48 2026] move 54w received from w [Rb6n Db5n Mb8w Rb7n]\n1769944188 [Sun Feb  1 11:09:48 2026] after 54w, move used:5 wresv:76 bresv:43 game:1785\n1769944188 [Sun Feb  1 11:09:48 2026] game finished with result w g";

        var line = lineRaw.Replace("\\n", "\n");

        var rec = DataConverter.FromTsv(header, line);

        // Narrowed to avoid duplication with
        // - FromTsv_Parses_testGame_BasicFields (field spot-checks)
        // - FromTsv_Parses_testGame_Movelist_And_Events (turns/events integrity)
        // This test now only ensures the full, real TSV row parses end-to-end.
        rec.Id.Should().Be(669123);
        rec.MoveListRaw.Should().NotBeNullOrWhiteSpace();
        rec.EventsRaw.Should().NotBeNullOrWhiteSpace();
        rec.Turns.Should().NotBeNull();
    }

    [Fact]
    public void FromTsv_ThrowsOnNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => DataConverter.FromTsv(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => DataConverter.FromTsv("id", null!));
    }

    [Fact]
    public void FromTsv_EmptyMovelist_YieldsNoTurns()
    {
        var header = string.Join('\t', new[]
        {
            "id","wusername","busername","startts","endts","result","termination","rated","corrupt","movelist","events"
        });

        var line = string.Join('\t', new[]
        {
            "1001","Goldie","Silvie","1770000000","1770003600","w","g","0","1","",""
        });

        var rec = DataConverter.FromTsv(header, line);
        rec.Id.Should().Be(1001);
        rec.MoveListRaw.Should().BeEmpty();
        rec.Turns.Should().NotBeNull();
        rec.Turns!.Count.Should().Be(0);
        rec.ResultSide.Should().Be(GameRecord.Side.Gold);
        rec.ResultTermination.Should().Be(GameRecord.GameTermination.Goal);
        rec.Rated.Should().BeFalse();
        rec.Corrupt.Should().BeTrue();
    }

    [Fact]
    public void FromTsv_SyntheticTwoTurns_ParsesWhiteAndBlack()
    {
        var header = string.Join('\t', new[]
        {
            "id","wusername","busername","startts","endts","result","termination","plycount","rated","corrupt","movelist","events"
        });

        var movelist = string.Join('\n', new[]
        {
            "1w Aa1 Bb2 Cc3 Dd4",
            "1b aa7 bb7 cc7 dd7"
        });

        var line = string.Join('\t', new[]
        {
            "2002","W","B","1770000000","1770001200","b","e","8","1","0", movelist, ""
        });

        var rec = DataConverter.FromTsv(header, line);
        rec.Id.Should().Be(2002);
        rec.PlyCount.Should().Be(8);
        rec.ResultSide.Should().Be(GameRecord.Side.Silver);
        rec.ResultTermination.Should().Be(GameRecord.GameTermination.Elimination);
        rec.Rated.Should().BeTrue();
        rec.Corrupt.Should().BeFalse();

        rec.Turns.Should().NotBeNull();
        rec.Turns!.Count.Should().Be(2);

        var tW = rec.Turns[0];
        tW.MoveNumber.Should().Be("1");
        tW.Side.Should().Be(Sides.Gold);
        tW.Moves.Should().Equal(new[] { "Aa1", "Bb2", "Cc3", "Dd4" });
        tW.AEIstring.Should().Be("1w Aa1 Bb2 Cc3 Dd4");

        var tB = rec.Turns[1];
        tB.MoveNumber.Should().Be("1");
        tB.Side.Should().Be(Sides.Silver);
        tB.Moves.Should().Equal(new[] { "aa7", "bb7", "cc7", "dd7" });
        tB.AEIstring.Should().Be("1b aa7 bb7 cc7 dd7");
    }

    [Fact]
    public void FromTsv_UnknownColumnsAreIgnored()
    {
        var header = string.Join('\t', new[]
        {
            "id","wusername","busername","foo","bar","baz","startts","endts","result","termination","rated","corrupt","movelist","events"
        });
        var line = string.Join('\t', new[]
        {
            "3003","Alice","Bob","X","Y","Z","1770000000","1770000010","w","r","true","false","1w Aa1 Bb2 Cc3 Dd4","line1\nline2"
        });

        var rec = DataConverter.FromTsv(header, line);
        rec.Id.Should().Be(3003);
        rec.WUsername.Should().Be("Alice");
        rec.BUsername.Should().Be("Bob");
        rec.ResultSide.Should().Be(GameRecord.Side.Gold);
        rec.ResultTermination.Should().Be(GameRecord.GameTermination.Resignation);
        rec.Rated.Should().BeTrue();
        rec.Corrupt.Should().BeFalse();
        rec.EventLines.Should().Equal(new[] { "line1", "line2" });
        rec.Turns!.Count.Should().Be(1);
    }

    [Fact]
    public void FromTsv_EventsSplitIntoLines()
    {
        var header = string.Join('\t', new[]
        {
            "id","wusername","busername","movelist","events"
        });
        var eventsRaw = string.Join('\n', new[]
        {
            "1769941026 [Sun Feb  1 10:17:06 2026] b player joining",
            "1769941050 [Sun Feb  1 10:17:30 2026] w player joining",
            "1769942363 [Sun Feb  1 10:39:23 2026] game finished with result w r"
        });
        var line = string.Join('\t', new[]
        {
            "4004","W","B","1w Aa1 Bb2 Cc3 Dd4","" + eventsRaw
        });

        var rec = DataConverter.FromTsv(header, line);
        rec.EventsRaw.Should().NotBeNullOrEmpty();
        rec.EventLines.Should().HaveCount(3);
        rec.EventLines[0].Should().Contain("b player joining");
        rec.EventLines[2].Should().Contain("game finished");
    }

    [Fact]
    public void FromTsv_ParsesFirstRow_BasicFields_And_Turns()
    {
        var (header, lines) = LoadSample();
        var line = lines[0];

        var record = DataConverter.FromTsv(header, line);

        var cols = line.Split('\t');
        cols.Length.Should().BeGreaterThan(27);

        long.Parse(cols[0]).Should().Be(record.Id);
        record.WUsername.Should().Be(cols[3]).And.NotBeNullOrWhiteSpace();
        record.BUsername.Should().Be(cols[4]).And.NotBeNullOrWhiteSpace();

        ParseBool(cols[25]).Should().Be(record.Rated);
        ParseBool(cols[26]).Should().Be(record.Corrupt);

        record.StartTs.Should().NotBeNull();
        record.EndTs.Should().NotBeNull();
        record.StartTs!.Value.ToUnixTimeSeconds().Should().Be(long.Parse(cols[19]));
        record.EndTs!.Value.ToUnixTimeSeconds().Should().Be(long.Parse(cols[20]));

        ParseSide(cols[21]).Should().Be(record.ResultSide);
        ParseTermination(cols[22]).Should().Be(record.ResultTermination);

        record.MoveListRaw.Should().NotBeNullOrWhiteSpace();
        record.Turns.Should().NotBeNull();

        var moveLines = record.MoveListRaw!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        record.Turns!.Count.Should().Be(moveLines.Length);

        var t0 = record.Turns[0];
        var t0Parts = moveLines[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var t0Head = t0Parts[0];
        t0.MoveNumber.Should().Be(t0Head[..^1]);
        t0.Side.Should().Be(char.ToLowerInvariant(t0Head[^1]) == 'w' ? Sides.Gold : Sides.Silver);
        t0.Moves.Count.Should().Be(t0Parts.Length - 1);
        t0.AEIstring.Should().Be(moveLines[0]);
    }

    [Fact]
    public void FromTsv_ParsesVariousRows_TurnsIntegrityAndEventLogs()
    {
        var (header, lines) = LoadSample();

        var sampleIdx = new[] { 1, 10, 20, 40, lines.Count - 1 }
            .Where(i => i >= 0 && i < lines.Count)
            .ToArray();

        foreach (var idx in sampleIdx)
        {
            var line = lines[idx];
            var cols = line.Split('\t');
            var rec = DataConverter.FromTsv(header, line);

            rec.Id.Should().Be(long.Parse(cols[0]));
            rec.PlyCount.Should().Be(int.TryParse(cols[23], out var ply) ? ply : null);

            if (!string.IsNullOrWhiteSpace(rec.MoveListRaw))
            {
                rec.Turns.Should().NotBeNull();
                var ml = rec.MoveListRaw!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                rec.Turns!.Count.Should().Be(ml.Length);

                var mid = ml.Length / 2;
                if (ml.Length > 2)
                {
                    var parts = ml[mid].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var head = parts[0];
                    var turn = rec.Turns![mid];
                    turn.MoveNumber.Should().Be(head[..^1]);
                    turn.Side.Should().Be(char.ToLowerInvariant(head[^1]) == 'w' ? Sides.Gold : Sides.Silver);
                    turn.Moves.Count.Should().Be(parts.Length - 1);
                    turn.AEIstring.Should().Be(ml[mid]);
                }
            }

            if (!string.IsNullOrWhiteSpace(rec.EventsRaw))
            {
                rec.EventLines.Count.Should().BeGreaterThan(0);
            }

            ParseSide(cols[21]).Should().Be(rec.ResultSide);
            ParseTermination(cols[22]).Should().Be(rec.ResultTermination);
        }
    }

    [Fact]
    public void FromTsv_GracefullyHandlesMissingOrEmptyOptionalFields()
    {
        var header = string.Join('\t', new[]
        {
            "id","wplayerid","bplayerid","wusername","busername","wtitle","btitle","wcountry","bcountry",
            "wrating","brating","wratingk","bratingk","wtype","btype","event","site","timecontrol","postal",
            "startts","endts","result","termination","plycount","mode","rated","corrupt","movelist","events"
        });

        var line = string.Join('\t', new[]
        {
            "42","","","foo","bar","","","","",
            "","","","","h","b","","","","",
            "1770000000","1770001234","w","r","4","IGS","1","0","1w Aa1 Bb2 Cc3 Dd4\n1b aa7 bb7 cc7 dd7",""
        });

        var rec = DataConverter.FromTsv(header, line);
        rec.Id.Should().Be(42);
        rec.WPlayerId.Should().BeNull();
        rec.BPlayerId.Should().BeNull();
        rec.WType.Should().Be(GameRecord.PlayerType.Human);
        rec.BType.Should().Be(GameRecord.PlayerType.Bot);
        rec.ResultSide.Should().Be(GameRecord.Side.Gold);
        rec.ResultTermination.Should().Be(GameRecord.GameTermination.Resignation);
        rec.Rated.Should().BeTrue();
        rec.Corrupt.Should().BeFalse();
        rec.Turns.Should().NotBeNull();
        rec.Turns!.Count.Should().Be(2);
        rec.Turns[0].MoveNumber.Should().Be("1");
        rec.Turns[0].Side.Should().Be(Sides.Gold);
        rec.Turns[0].Moves.Count.Should().Be(4);
    }

    private static bool? ParseBool(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return s is "1" or "true" or "True" ? true : s is "0" or "false" or "False" ? false : null;
    }

    private static GameRecord.Side? ParseSide(string s)
        => s?.Trim().ToLowerInvariant() switch
        {
            "w" => GameRecord.Side.Gold,
            "b" => GameRecord.Side.Silver,
            _ => null
        };

    private static GameRecord.GameTermination? ParseTermination(string s)
        => s?.Trim().ToLowerInvariant() switch
        {
            "r" => GameRecord.GameTermination.Resignation,
            "t" => GameRecord.GameTermination.Timeout,
            "f" => GameRecord.GameTermination.Forfeit,
            "g" => GameRecord.GameTermination.Goal,
            "e" => GameRecord.GameTermination.Elimination,
            _ => null
        };
}
