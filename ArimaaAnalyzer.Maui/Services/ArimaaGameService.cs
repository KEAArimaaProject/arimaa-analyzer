using System.Text.RegularExpressions;

namespace YourApp.Models;

/// <summary>
/// Represents a validated Arimaa game in standard notation.
/// Immutable after creation.
/// </summary>
public record ArimaaGameService
{
    /// <summary>
    /// The original raw notation string (trimmed).
    /// </summary>
    public string RawNotation { get; init; }
    /// <summary>
    /// The individual moves/lines (split by newline and trimmed).
    /// </summary>
    public string[] Moves { get; init; } = Array.Empty<string>();
    public string? ErrorMessage { get; init; }

    public ArimaaGameService(string rawNotation)
    {
        RawNotation = rawNotation.Trim();
        ErrorMessage = FindError(rawNotation);

        Moves = ErrorMessage == null
            ? GenerateMoves(rawNotation)
            : Array.Empty<string>();
    }
    
    private static string[] GenerateMoves(string rawNotation)
    {
        try
        {
            // Normalize escaped literals first (if input contains "\\n" or "\\r\\n")
            rawNotation = rawNotation
                .Replace("\\r\\n", "\n")
                .Replace("\\n", "\n")
                .Replace("\\r", "\n");

            // Normalize all real line endings to \n
            var normalized = rawNotation.Replace("\r\n", "\n").Replace("\r", "\n");

            // Split on \n only, after normalization
            string[] moves = normalized
                .Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(move => move.Trim()) // trim first
                .Where(move => !string.IsNullOrWhiteSpace(move)) // then filter
                .ToArray();

            return moves;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing moves: {ex.Message}");
            return Array.Empty<string>();
        }
    }
    
    
    /// <summary>
    /// Attempts to parse and validate the game notation.
    /// Returns null if valid, an error string otherwise.
    /// </summary>
    private static string? FindError(string rawNotation)
    {
        if (string.IsNullOrWhiteSpace(rawNotation))
        {
            return "Game notation cannot be null or empty.";
        }

        string[] generatedmoves = GenerateMoves(rawNotation);
        if (generatedmoves.Length == 0)
        {
            return "No valid move lines found.";
        }
        
        // Basic validation: each line should match the expected pattern for Arimaa notation
        // Example patterns:
        // "1w Ed2 Mb2 ..."          -> move number + side + spaces + moves
        // "1b ra7 hb7 ..."
        var moveLinePattern = new Regex(@"^\d+[wb]\s+.*", RegexOptions.Compiled);

        foreach (var line in generatedmoves)
        {
            if (!moveLinePattern.IsMatch(line))
            {
                return $"Invalid move line format: '{line}'";
            }
        }

        return null;
    }


}