using System.Text;
using ArimaaAnalyzer.Maui.Services.Arimaa;

namespace ArimaaAnalyzer.Maui.Services;

/// <summary>
/// Stateless service for Arimaa move validation and trap capture logic.
/// All methods are static and contain no mutable state.
/// 
/// Responsibilities:
/// - Compute legal move sequences (up to 4 steps) from board state transitions
/// - Apply trap captures after moves
/// - Validate freezing and rabbit movement rules
/// 
/// Limitations (initial implementation):
/// - Supports only slide steps (orthogonal move by one square into empty).
/// - Enforces rabbit non-backward rule and freezing (cannot move a frozen piece).
/// - Applies trap captures after each step.
/// - Does NOT implement push/pull, goal, repetition, or advanced rules yet.
/// </summary>
public static class CorrectMoveService
{
    private static bool IsGold(char ch) => ch != ' ' && char.IsUpper(ch);
    private static char ToUpper(char ch) => char.ToUpperInvariant(ch);

    /// <summary>
    /// Try to compute a legal sequence (1..4 steps) for <paramref name="sideToMove"/> that transforms
    /// <paramref name="before"/> into <paramref name="after"/>. Returns official notation string or "error".
    /// </summary>
    public static string ComputeMoveSequence(GameState before, GameState after, Side sideToMove)
    {
        if (before == null || after == null) return "error";

        var expectedAfterSide = Opposite(sideToMove);

        var start = BoardState.From(before, sideToMove);
        var goal = BoardState.From(after, expectedAfterSide);

        if (start.BoardEquals(goal)) return "error";

        var seen = new HashSet<string> { start.Hash };
        var q = new Queue<BoardState>();
        q.Enqueue(start);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();

            if (cur.StepsCount is > 0 and <= 4 && cur.BoardEquals(goal))
            {
                return cur.RenderNotation();
            }

            if (cur.StepsCount >= 4) continue;

            foreach (var next in GenerateSlides(cur))
            {
                if (seen.Add(next.Hash))
                {
                    q.Enqueue(next);
                }
            }
        }

        return "error";
    }

    /// <summary>
    /// Apply trap captures to the GameState using only AEI string operations.
    /// This method mutates the passed GameState by removing unsupported pieces from traps.
    /// </summary>
    public static void ApplyTrapCaptures(GameState state)
    {
        if (state == null) return;

        // Traps: (2,2), (2,5), (5,2), (5,5) -> indices 18, 21, 42, 45
        var trapIndices = new[] { 18, 21, 42, 45 };

        foreach (var trapIdx in trapIndices)
        {
            var piece = state.GetPieceCharAtIndex(trapIdx);
            if (piece == ' ') continue;

            var trapRow = trapIdx / 8;
            var trapCol = trapIdx % 8;
            var trapPos = new Position(trapRow, trapCol);

            var side = IsGold(piece) ? Side.Gold : Side.Silver;
            if (!HasAdjacentFriendly(state, trapRow, trapCol, side))
            {
                state.RemovePieceAt(trapPos);
            }
        }
    }

    private static IEnumerable<BoardState> GenerateSlides(BoardState state)
    {
        var dirs = new (int dr, int dc, char dch)[] { (-1, 0, 'n'), (0, 1, 'e'), (1, 0, 's'), (0, -1, 'w') };

        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
        {
            var ch = state.Board[r, c];
            if (ch == ' ' || (IsGold(ch) ? Side.Gold : Side.Silver) != state.SideToMove) continue;

            if (IsFrozen(state.Board, r, c)) continue;

            foreach (var (dr, dc, dch) in dirs)
            {
                var r2 = r + dr;
                var c2 = c + dc;
                if (r2 < 0 || r2 > 7 || c2 < 0 || c2 > 7) continue;
                if (state.Board[r2, c2] != ' ') continue;

                if (ToUpper(ch) == 'R')
                {
                    if (IsGold(ch) && dr == 1) continue;
                    if (!IsGold(ch) && dr == -1) continue;
                }

                var next = state.CloneForNext();
                next.Board[r2, c2] = ch;
                next.Board[r, c] = ' ';
                next.AppendStep(ch, r, c, r2, c2, dch);

                ApplyTrapCaptures(next.Board);

                yield return next;
            }
        }
    }

    private static bool IsFrozen(char[,] board, int r, int c)
    {
        var p = board[r, c];
        if (p == ' ') return false;

        if (HasAdjacentFriendly(board, r, c, IsGold(p) ? Side.Gold : Side.Silver)) return false;

        var myStr = PieceHierarchy.GetHierarchy(p);
        return HasAdjacentStrongerEnemy(board, r, c, IsGold(p) ? Side.Gold : Side.Silver, myStr);
    }

    private static bool HasAdjacentFriendly(char[,] b, int r, int c, Side side)
    {
        for (var k = 0; k < 4; k++)
        {
            var r2 = r + ((k == 0) ? -1 : (k == 2) ? 1 : 0);
            var c2 = c + ((k == 1) ? 1 : (k == 3) ? -1 : 0);
            if (r2 < 0 || r2 > 7 || c2 < 0 || c2 > 7) continue;
            var q = b[r2, c2];
            if (q != ' ' && ((IsGold(q) ? Side.Gold : Side.Silver) == side)) return true;
        }
        return false;
    }

    private static bool HasAdjacentFriendly(GameState state, int r, int c, Side side)
    {
        for (var k = 0; k < 4; k++)
        {
            var r2 = r + ((k == 0) ? -1 : (k == 2) ? 1 : 0);
            var c2 = c + ((k == 1) ? 1 : (k == 3) ? -1 : 0);
            if (r2 < 0 || r2 > 7 || c2 < 0 || c2 > 7) continue;

            var piece = state.GetPieceChar(new Position(r2, c2));
            if (piece != ' ')
            {
                var pieceSide = IsGold(piece) ? Side.Gold : Side.Silver;
                if (pieceSide == side) return true;
            }
        }
        return false;
    }

    private static bool HasAdjacentStrongerEnemy(char[,] b, int r, int c, Side side, int myStr)
    {
        for (var k = 0; k < 4; k++)
        {
            var r2 = r + ((k == 0) ? -1 : (k == 2) ? 1 : 0);
            var c2 = c + ((k == 1) ? 1 : (k == 3) ? -1 : 0);
            if (r2 < 0 || r2 > 7 || c2 < 0 || c2 > 7) continue;
            var q = b[r2, c2];
            if (q != ' ')
            {
                var qSide = IsGold(q) ? Side.Gold : Side.Silver;
                if (qSide != side && PieceHierarchy.GetHierarchy(q) > myStr) return true;
            }
        }
        return false;
    }

    private static void ApplyTrapCaptures(char[,] board)
    {
        var traps = new (int r, int c)[] { (2, 2), (2, 5), (5, 2), (5, 5) };
        foreach (var (r, c) in traps)
        {
            var p = board[r, c];
            if (p == ' ') continue;
            if (!HasAdjacentFriendly(board, r, c, IsGold(p) ? Side.Gold : Side.Silver))
            {
                board[r, c] = ' ';
            }
        }
    }

    private static Side Opposite(Side s) => s == Side.Gold ? Side.Silver : Side.Gold;

    private sealed class BoardState
    {
        public char[,] Board { get; }
        public Side SideToMove { get; }
        private readonly List<string> _steps = new();

        public int StepsCount => _steps.Count;
        public string Hash => ComputeHash(Board, SideToMove);

        private BoardState(char[,] board, Side side, IEnumerable<string>? steps = null)
        {
            Board = board;
            SideToMove = side;
            if (steps != null) _steps.AddRange(steps);
        }

        public static BoardState From(GameState s, Side side)
        {
            var b = new char[8, 8];
            var aei = s.localAeiSetPosition;
            if (string.IsNullOrWhiteSpace(aei))
                throw new ArgumentException("GameState.localAeiSetPosition must be set.");

            var trimmed = aei.Trim();
            var firstQuote = trimmed.IndexOf('"');
            var lastQuote = trimmed.LastIndexOf('"');
            if (firstQuote < 0 || lastQuote <= firstQuote)
                throw new ArgumentException("Malformed AEI setposition: missing quoted board string.");

            var flat = trimmed.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
            if (flat.Length != 64)
                throw new ArgumentException("Malformed AEI setposition: board string must be exactly 64 characters.");

            for (var i = 0; i < 64; i++)
            {
                var r = i / 8;
                var c = i % 8;
                b[r, c] = flat[i];
            }

            return new BoardState(b, side);
        }

        public BoardState CloneForNext()
        {
            var copy = new char[8, 8];
            for (var r = 0; r < 8; r++)
            for (var c = 0; c < 8; c++)
                copy[r, c] = Board[r, c];

            return new BoardState(copy, SideToMove, _steps);
        }

        public void AppendStep(char pieceChar, int r1, int c1, int r2, int c2, char dir)
        {
            var sb = new StringBuilder(4);
            sb.Append(char.ToUpperInvariant(pieceChar));
            sb.Append(SquareString(r1, c1));
            sb.Append(dir);
            _steps.Add(sb.ToString());
        }

        public string RenderNotation() => string.Join(" ", _steps);

        public bool BoardEquals(BoardState other)
        {
            for (var r = 0; r < 8; r++)
            for (var c = 0; c < 8; c++)
            {
                if (Board[r, c] != other.Board[r, c]) return false;
            }
            return true;
        }

        private static string ComputeHash(char[,] board, Side side)
        {
            var sb = new StringBuilder(100);
            sb.Append(side == Side.Gold ? 'g' : 's');
            for (var r = 0; r < 8; r++)
            for (var c = 0; c < 8; c++)
            {
                var ch = board[r, c];
                sb.Append(ch == ' ' ? '.' : ch);
            }
            return sb.ToString();
        }

        private static string SquareString(int r, int c)
        {
            char file = (char)('a' + c);
            int rank = 8 - r;
            return string.Create(2, (file, rank), (span, st) =>
            {
                span[0] = st.file;
                span[1] = (char)('0' + st.rank);
            });
        }
    }
}
