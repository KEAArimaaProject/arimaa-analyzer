namespace ArimaaAnalyzer.Maui.Services.Arimaa;

/// <summary>
/// Well-known Arimaa opening setups as 64-char normalized board strings
/// (north/rank 8 at indices 0–7, south/rank 1 at 56–63; spaces = empty).
/// </summary>
public static class ArimaaStandardSetups
{
    /// <summary>
    /// Classic 99of9 major-piece rank (front row): R H C E M C H R.
    /// </summary>
    public const string NinetyNineOfNineFrontGold = "RHCEMCHR";

    /// <summary>
    /// Classic 99of9 rabbit/dog rank (back row): R R R D D R R R.
    /// </summary>
    public const string NinetyNineOfNineBackGold = "RRRDDRRR";

    /// Silver
    public const string NinetyNineOfNineFrontSilver = "rhcmechr";
    public const string NinetyNineOfNineBackSilver = "rrrddrrr";

    // Normalized board: rank 2 = indices 48–55, rank 1 = indices 56–63 (gold home ranks).
    private const int GoldHomeStartIndex = 48;

    /// <summary>
    /// Empty board with gold pieces only in the classic 99of9 setup.
    /// </summary>
    public static string NinetyNineOfNineGoldOnly()
    {
        // ranks 8–3 empty, rank 2 front, rank 1 back
        return new string(' ', 48) + NinetyNineOfNineFrontGold + NinetyNineOfNineBackGold;
    }

    /// <summary>
    /// Overlay silver 99of9 on ranks 8–7 of an existing board (does not clear gold).
    /// </summary>
    public static string WithNinetyNineOfNineSilver(string currentBoard)
    {
        if (currentBoard is null || currentBoard.Length != 64)
            throw new ArgumentException("Board must be exactly 64 characters.", nameof(currentBoard));

        var chars = currentBoard.ToCharArray();
        var silverFront = NinetyNineOfNineFrontSilver; // rank 7
        var silverBack = NinetyNineOfNineBackSilver;   // rank 8

        for (var i = 0; i < 8; i++)
        {
            chars[i] = silverBack[i];      // rank 8
            chars[8 + i] = silverFront[i]; // rank 7
        }

        return new string(chars);
    }

    /// <summary>
    /// True when the board is ready for the silver half of Auto setup:
    /// only gold pieces are present, and they are confined to gold's two home ranks
    /// (ranks 1–2 / last 16 squares). Empty boards and mixed positions return false
    /// so Auto setup wipes and places gold instead.
    /// </summary>
    public static bool IsGoldOnlyOnHomeRanks(string? board)
    {
        if (board is null || board.Length != 64)
            return false;

        var hasGoldOnHome = false;

        for (var i = 0; i < 64; i++)
        {
            var ch = board[i];
            if (ch == ' ') continue;

            // Any silver piece → not gold-only setup
            if (char.IsLower(ch))
                return false;

            if (!char.IsUpper(ch))
                return false;

            // Gold pieces must stay on ranks 1–2
            if (i < GoldHomeStartIndex)
                return false;

            hasGoldOnHome = true;
        }

        return hasGoldOnHome;
    }
}
