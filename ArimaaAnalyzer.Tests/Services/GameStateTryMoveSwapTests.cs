using ArimaaAnalyzer.Maui.Services;
using ArimaaAnalyzer.Maui.Services.Arimaa;
using FluentAssertions;
using Xunit;

namespace ArimaaAnalyzer.Tests.Services;

public class GameStateTryMoveSwapTests
{
    private static string[] EmptyBoard() => new[]
    {
        "........",
        "........",
        "........",
        "........",
        "........",
        "........",
        "........",
        "........"
    };

    private static string ReplaceChar(string s, int index, char ch)
    {
        var arr = s.ToCharArray();
        arr[index] = ch;
        return new string(arr);
    }

    [Fact(DisplayName = "TryMove onto empty square moves the piece")]
    public void TryMove_OntoEmpty_MovesPiece()
    {
        var board = EmptyBoard();
        board[6] = ReplaceChar(board[6], 0, 'E'); // a2

        var state = new GameState(NotationService.BoardToAei(board, Sides.Gold));
        var game = new ArimaaGameService(state);

        var ok = game.TryMove(new Position(6, 0), new Position(5, 0)); // a2 -> a3
        ok.Should().BeTrue();

        state.GetPieceChar(new Position(6, 0)).Should().Be(' ');
        state.GetPieceChar(new Position(5, 0)).Should().Be('E');
    }

    [Fact(DisplayName = "TryMove onto occupied square swaps the two pieces")]
    public void TryMove_OntoOccupied_SwapsPieces()
    {
        var board = EmptyBoard();
        board[6] = ReplaceChar(board[6], 0, 'E'); // a2 gold elephant
        board[5] = ReplaceChar(board[5], 0, 'r'); // a3 silver rabbit

        var state = new GameState(NotationService.BoardToAei(board, Sides.Gold));
        var game = new ArimaaGameService(state);

        var ok = game.TryMove(new Position(6, 0), new Position(5, 0)); // a2 -> a3
        ok.Should().BeTrue();

        state.GetPieceChar(new Position(6, 0)).Should().Be('r');
        state.GetPieceChar(new Position(5, 0)).Should().Be('E');
    }

    [Fact(DisplayName = "TryMove swap works for same-side pieces")]
    public void TryMove_OntoOccupiedSameSide_SwapsPieces()
    {
        var board = EmptyBoard();
        board[7] = ReplaceChar(board[7], 3, 'M'); // d1 gold camel
        board[7] = ReplaceChar(board[7], 4, 'H'); // e1 gold horse

        var state = new GameState(NotationService.BoardToAei(board, Sides.Gold));
        var game = new ArimaaGameService(state);

        var ok = game.TryMove(new Position(7, 3), new Position(7, 4)); // d1 -> e1
        ok.Should().BeTrue();

        state.GetPieceChar(new Position(7, 3)).Should().Be('H');
        state.GetPieceChar(new Position(7, 4)).Should().Be('M');
    }
}
