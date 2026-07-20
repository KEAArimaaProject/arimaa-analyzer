using System.Collections.Generic;
using System.Text.Json.Serialization;
using ArimaaAnalyzer.Maui.Services;

namespace ArimaaAnalyzer.Maui.Models;

/// <summary>
/// JSON-serializable game-tree node for Load game list storage (no Parent links).
/// </summary>
public sealed class GameTurnDto
{
    public string MoveNumber { get; set; } = "0";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Sides Side { get; set; } = Sides.Gold;

    public List<string> Moves { get; set; } = new();

    public bool IsMainLine { get; set; } = true;

    /// <summary>AEI setposition string after this turn.</summary>
    public string AEIstring { get; set; } = string.Empty;

    public List<GameTurnDto> Children { get; set; } = new();
}
