namespace ArimaaAnalyzer.Maui.Services.Arimaa;

public readonly record struct Position(int Row, int Col)
{
    public bool IsOnBoard => Row is >= 0 and < 8 && Col is >= 0 and < 8;

    public IEnumerable<Position> Neighbors()
    {
        yield return new Position(Row - 1, Col);
        yield return new Position(Row + 1, Col);
        yield return new Position(Row, Col - 1);
        yield return new Position(Row, Col + 1);
    }
}
