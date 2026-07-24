namespace ArimaaAnalyzer.Maui.Components.Help;

/// <summary>
/// Page-keyed help content for the right-side help drawer.
/// </summary>
public static class PageHelpCatalog
{
    public sealed record Section(string Heading, string Body);

    public sealed record Doc(string Title, IReadOnlyList<Section> Sections);

    /// <summary>
    /// Resolve a stable page key from the Blazor base-relative path (no query/hash).
    /// </summary>
    public static string ResolvePageKey(string? baseRelativePath)
    {
        if (string.IsNullOrWhiteSpace(baseRelativePath))
            return "home";

        var rel = baseRelativePath.Trim();
        var q = rel.IndexOfAny(['?', '#']);
        if (q >= 0) rel = rel[..q];
        rel = rel.Trim().Trim('/');

        if (string.IsNullOrEmpty(rel))
            return "home";

        // First path segment is the page id (matches @page routes).
        var slash = rel.IndexOf('/');
        var segment = slash >= 0 ? rel[..slash] : rel;
        return segment.ToLowerInvariant();
    }

    public static Doc Get(string pageKey) =>
        Docs.TryGetValue(pageKey, out var doc) ? doc : Fallback;

    private static readonly Doc Fallback = new(
        "Help",
        [
            new Section(
                "About this screen",
                "No specific documentation is available for this page yet. Use the navigation menu (burger button) to move between Home, Gamesearch, Load game list, Load game manual, and the Arimaa board.")
        ]);

    private static readonly IReadOnlyDictionary<string, Doc> Docs =
        new Dictionary<string, Doc>(StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = new(
                "Home",
                [
                    new Section(
                        "What is this app?",
                        "Arimaa Analyzer helps you load, browse, and study Arimaa games. You can search a game database, paste games manually, keep a personal list of pasted games, and analyze positions with an engine on the Arimaa board."),
                    new Section(
                        "Getting started",
                        "Open the menu with the burger button (top left). Typical flow: find or paste a game, then open it on the Arimaa page to step through moves and run analysis.")
                ]),

            ["gamesearch"] = new(
                "Gamesearch",
                [
                    new Section(
                        "Purpose",
                        "Search and browse games from the built-in game database. Use the filters on the left, then pick a game from the results list on the right."),
                    new Section(
                        "Selecting a game",
                        "Click a row in the results to load that game and open the Arimaa board. The selected game summary also appears in the top bar on the Arimaa page."),
                    new Section(
                        "Tips",
                        "If loading fails, an error message appears under the search panel. Retry after checking that game data files are available.")
                ]),

            ["loadgamelist"] = new(
                "Load game list",
                [
                    new Section(
                        "Purpose",
                        "Browse games you previously pasted and saved on the Load game manual page. These are your local pasted-game library, separate from the database search."),
                    new Section(
                        "Open a game",
                        "Click a row to load that game onto the Arimaa board."),
                    new Section(
                        "Sort and delete",
                        "Use Date / Name sort controls in the list toolbar. The Delete toggle in the top bar shows or hides the × button on each row so you can remove saved entries.")
                ]),

            ["loadgamemanual"] = new(
                "Load game manual",
                [
                    new Section(
                        "Purpose",
                        "Paste Arimaa game notation (for example from arimaa.com) to load a game and save it into your personal pasted-game list."),
                    new Section("How to load a game from Arimaa.com",
                        "log in to arimaa.com and find a game, for example: Game 670946 bot_Sharp2014Blitz (2466) vs. browni3141 (2621). -> right-click on the page and select \"View page source\" -> go to the line that starts with \"arimaa.vars.movelist=\"\" -> select and copy the text between the \"\" characters, " +
                        "so you get the following (without the \" characters): \"1w Ra1 Rb1 Rc1 Dd1...hd4s cc4e\\n34w Ef5w Ee5s de6s hf6x Dc5s\" -> paste the text into the notation text area on this page."),
                    new Section(
                        "Name field",
                        "Give the game a unique name (within the allowed length). Leading and trailing spaces are ignored. The name is how the game appears in Load game list."),
                    new Section(
                        "Notation",
                        "Paste the full game notation into the text area, then click Load Game. On success the game is saved and opened on the board."),
                    new Section(
                        "After loading",
                        "Find the game later under Load game list. You can also see it immediately on the Arimaa page after a successful load.")
                ]),

            ["arimaa"] = new(
                "Arimaa board",
                [
                    
                    new Section(
                        "Keyboard shortcuts",
                        "These shortcuts enable you to use the keyboard to go to the next move or previus move, and to use the AI to analyze the game from Golds side or Silvers side: j / l — previous turn / next turn. i / k — go to start / end. g / s — analyze as Gold / Silver (same as MoveG / MoveS). h — HumanMove (Play mode only; same as the HumanMove button). Shortcuts are ignored while typing in a text field."),
                    new Section(
                        "Purpose",
                        "Main study board: view the current position, step through the game tree, set up pieces (in Play mode), and run engine analysis."),
                    new Section(
                        "Top bar — Paths & Colors",
                        "Paths controls which boards show move-path lines (main only, analysis only, both, or none). Colors picks the palette used for those lines."),
                    new Section(
                        "Top bar — Boards & Think",
                        "Boards sets how many analysis mini-boards appear (when not in Play mode). Think sets engine think time per move in milliseconds: pick a preset from the list, or enter a custom value. The engine uses whole seconds (values under 1000 ms may behave similarly)."),
                    new Section(
                        "MoveG / MoveS",
                        "Run analysis with Gold or Silver moving first. With Play off, results fill the mini-boards. With Play on, the engine plays one move on the main board."),
                    new Section(
                        "Outer UI & Play",
                        "Outer UI shows or hides board chrome around the squares. Play toggles play-against-AI mode (piece palette instead of mini-boards). Use MoveG / MoveS for the engine reply. You can move pieces manually by either dragging them or clicking on the piece and then click on the square to move to. You can make pieces swap position by dragging a piece onto another piece."),
                    new Section(
                        "Edit Pieces / Auto setup",
                        "In Play mode, drag pieces from the palette onto the board (or drag board pieces back to remove them). Auto setup places the classic 99of9 opening on the live board only (gold if the board is not gold-only on ranks 1–2; otherwise silver). Press HumanMove (h) to commit: gold setup becomes a root child labeled setup (Gold to move); silver setup becomes its child labeled setup (Silver to move)."),
                    new Section(
                        "HumanMove",
                        "Visible when Play is on (shortcut: h). During setup, commits the current Auto setup phase to the game tree (gold under root, then silver under gold). After setup, switches side to move so humans can alternate without the AI.")
                ]),
        };
}
