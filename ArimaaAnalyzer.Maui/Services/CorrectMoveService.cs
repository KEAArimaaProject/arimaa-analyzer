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
    private static readonly (int dr, int dc, char dch)[] Directions =
        { (-1, 0, 'n'), (0, 1, 'e'), (1, 0, 's'), (0, -1, 'w') };
    private static bool InBounds(int r, int c) => r >= 0 && r < 8 && c >= 0 && c < 8;
    private static char DirChar(int dr, int dc)
    {
        if (dr == -1 && dc == 0) return 'n';
        if (dr == 1 && dc == 0) return 's';
        if (dr == 0 && dc == 1) return 'e';
        if (dr == 0 && dc == -1) return 'w';
        return '?';
    }
    private static bool IsRabbitBackwardMove(char piece, int dr)
    {
        if (ToUpper(piece) != 'R') return false;
        return IsGold(piece) ? dr == 1 : dr == -1;
    }

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

            // Generate Push/Pull two-step moves (counts as +2 steps)
            foreach (var next in GeneratePushPull(cur))
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
        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
        {
            var ch = state.Board[r, c];
            if (ch == ' ' || (IsGold(ch) ? Side.Gold : Side.Silver) != state.SideToMove) continue;

            if (IsFrozen(state.Board, r, c)) continue;

            foreach (var (dr, dc, dch) in Directions)
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

    private static IEnumerable<BoardState> GeneratePushPull(BoardState state)
    {
        // Two-step generators; ensure we do not exceed 4 steps total
        if (state.StepsCount > 2) yield break; // need room for +2

        for (var r = 0; r < 8; r++)
        for (var c = 0; c < 8; c++)
        {
            var pusher = state.Board[r, c];
            if (pusher == ' ' || (IsGold(pusher) ? Side.Gold : Side.Silver) != state.SideToMove) continue;
            if (IsFrozen(state.Board, r, c)) continue;

            // inspect adjacent enemy pieces
            foreach (var (drE, dcE, _) in Directions)
            {
                var er = r + drE;
                var ec = c + dcE;
                if (!InBounds(er, ec)) continue;
                var enemy = state.Board[er, ec];
                if (enemy == ' ') continue;
                var enemySide = IsGold(enemy) ? Side.Gold : Side.Silver;
                if (enemySide == state.SideToMove) continue;
                if (!PieceHierarchy.CanPushOrPull(pusher, enemy)) continue;

                // Try PUSH: enemy moves away from pusher along (drE, dcE), then pusher moves into (er,ec)
                var er2 = er + drE;
                var ec2 = ec + dcE;
                if (InBounds(er2, ec2) && state.Board[er2, ec2] == ' ')
                {
                    // Check rabbit backward restriction for pusher's second step into (er,ec)
                    if (!IsRabbitBackwardMove(pusher, drE))
                    {
                        var next = state.CloneForNext();
                        // Step 1: move enemy to (er2,ec2)
                        next.Board[er2, ec2] = enemy;
                        next.Board[er, ec] = ' ';
                        next.AppendStep(enemy, er, ec, er2, ec2, DirChar(drE, dcE));
                        ApplyTrapCaptures(next.Board);

                        // If enemy got captured on a trap, the destination will be empty; that's fine for step 2.
                        // Step 2: move pusher into (er,ec)
                        next.Board[er, ec] = pusher; // should be empty after step1
                        next.Board[r, c] = ' ';
                        next.AppendStep(pusher, r, c, er, ec, DirChar(drE, dcE));
                        ApplyTrapCaptures(next.Board);

                        yield return next;
                    }
                }

                // Try PULL: pusher moves to any adjacent empty square not (er,ec), then enemy moves into (r,c)
                foreach (var (drP, dcP, _) in Directions)
                {
                    var pr2 = r + drP;
                    var pc2 = c + dcP;
                    if (!InBounds(pr2, pc2)) continue;
                    if (pr2 == er && pc2 == ec) continue; // cannot move into enemy square
                    if (state.Board[pr2, pc2] != ' ') continue;
                    // rabbit backward restriction for pusher first step
                    if (IsRabbitBackwardMove(pusher, drP)) continue;

                    var next2 = state.CloneForNext();
                    // Step 1: move pusher to (pr2,pc2)
                    next2.Board[pr2, pc2] = pusher;
                    next2.Board[r, c] = ' ';
                    next2.AppendStep(pusher, r, c, pr2, pc2, DirChar(drP, dcP));
                    ApplyTrapCaptures(next2.Board);

                    // If enemy was captured due to leaving support (e.g., on trap), pull cannot proceed
                    if (next2.Board[er, ec] != enemy) continue;

                    // Step 2: enemy moves into original pusher square (r,c)
                    // This square is empty after step1 by construction
                    next2.Board[r, c] = enemy;
                    next2.Board[er, ec] = ' ';
                    // Direction from enemy (er,ec) to (r,c) is (-drE, -dcE)
                    next2.AppendStep(enemy, er, ec, r, c, DirChar(-drE, -dcE));
                    ApplyTrapCaptures(next2.Board);

                    yield return next2;
                }
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
