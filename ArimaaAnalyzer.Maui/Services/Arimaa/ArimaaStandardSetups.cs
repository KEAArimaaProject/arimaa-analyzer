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
}
