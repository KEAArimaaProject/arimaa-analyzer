namespace ArimaaAnalyzer.Maui.Services.Arimaa;

public sealed class Piece
{
    public Piece(PieceType type, Side side)
    {
        Type = type;
        Side = side;
    }

    public PieceType Type { get; }
    public Side Side { get; }

    public string SvgFileName =>
        Side switch
        {
            Side.Gold => $"white-{Type}.svg",
            Side.Silver => $"black-{Type}.svg",
            _ => ""
        };
}
