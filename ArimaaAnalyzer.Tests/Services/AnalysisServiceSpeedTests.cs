using System.IO;
using System.Diagnostics;
using FluentAssertions;
using Xunit;
using ArimaaAnalyzer.Maui.Models;
using ArimaaAnalyzer.Maui.Services;

namespace ArimaaAnalyzer.Tests.Services;

/// <summary>
/// Skeleton tests for AnalysisService.
/// These are placeholders to be filled in with real engine paths and scenarios.
/// </summary>
public class AnalysisServiceSpeedTests
{
    private static readonly string ExePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", // up from bin/<cfg>/<tfm> to solution root
        "ArimaaAnalyzer.Maui", "Aiexecutables", "sharp2015.exe"));

    [Fact(DisplayName = "BuildGameTurnTreeAsync tested for running speed")]
    public async Task BuildGameTurnTreeAsync_test_speed()
    {
        if (!File.Exists(ExePath))
        {
            Console.WriteLine(
                $"[SKIP] Engine executable not found at '{ExePath}'. Place sharp2015.exe there to run this test.");
            false.Should().BeTrue();
        }
        
        

        await using var svc = new AnalysisService();
        try
        {
            // Start engine
            await svc.StartAsync(ExePath, arguments: "aei");

            // Use the same AEI position as the smoke test
            var aei = "setposition g \" MrrrrrrrE  mcdh        R H                      DC  CDH   RRRRR\"";

            var notflippedBoard = NotationService.AeiToBoard(aei);
            int depth = 4;

            var startNode = new GameTurn(aei, aei, "0", Sides.Gold, Array.Empty<string>());

            // calculate start time
            const int MaxMilliseconds = 2000; // adjust threshold as needed for your environment
            var sw = Stopwatch.StartNew();
            var root = await svc.BuildGameTurnTreeAsync(startNode, depth, 2000);
            sw.Stop();
            // end of calculation
            sw.ElapsedMilliseconds.Should().BeLessThan(
                MaxMilliseconds,
                $"BuildGameTurnTreeAsync took {sw.ElapsedMilliseconds} ms, exceeding limit {MaxMilliseconds} ms.");
        }
        finally
        {
            await svc.QuitAsync();
        }
        
        
    }
}