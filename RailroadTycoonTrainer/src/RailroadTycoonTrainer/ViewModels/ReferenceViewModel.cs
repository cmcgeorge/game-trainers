using RailroadTycoonTrainer.Game;

namespace RailroadTycoonTrainer.ViewModels;

/// <summary>A titled note for the reference tab.</summary>
public sealed record Note(string Title, string Body);

/// <summary>
/// Static game-knowledge for the Reference tab: the locomotive rosters (which double as the startup
/// copy-protection answer key), the station / scenario / difficulty tables, and how-to /
/// reverse-engineering notes. None of this touches the live process — it explains what the scanner and
/// auto-locate are aiming at, and helps a player through the game.
/// </summary>
public sealed class ReferenceViewModel : ObservableObject
{
    public IReadOnlyList<Locomotive> Locomotives { get; } = LocomotiveBook.All;
    public IReadOnlyList<StationKind> Stations { get; } = GameFacts.Stations;
    public IReadOnlyList<Improvement> Improvements { get; } = GameFacts.Improvements;
    public IReadOnlyList<Scenario> Scenarios { get; } = GameFacts.Scenarios;
    public IReadOnlyList<Difficulty> Difficulties { get; } = GameFacts.Difficulties;

    public IReadOnlyList<Note> Notes { get; } = new[]
    {
        new Note("What this trainer edits",
            "Railroad Tycoon keeps the player's cash as a signed 16-bit word in units of $1,000 — a screen " +
            "reading of \"$1,000,000\" is stored as 1000. This trainer finds that word (either by auto-locating " +
            "the game's data segment, or by a value scan) and lets you set or freeze it. It edits the game's " +
            "live memory; it does not touch your save files."),

        new Note("Auto-locate vs. value scan",
            "Auto-locate finds the data segment by two static text labels the game always keeps in memory " +
            "(\"Outstanding Loans: \" and \"Stockholders Equity: \"), then reads the cash word at its fixed offset " +
            "from there — one click, no searching. If your GAME.EXE is a different build (the cash offset differs between builds — a " +
            "third-party disassembly of another build reported a different offset), auto-locate reports it couldn't validate; the value " +
            "scan then works regardless, because it searches for the number itself."),

        new Note("Cash — step by step (value scan)",
            "1) Attach to the dosbox process.  2) Click the Cash guide (sets a 16-bit scan).  3) Read your cash on " +
            "the top-right panel and drop the last three zeros ($1,000,000 → 1000); First Scan.  4) Earn or spend " +
            "so it changes, type the new thousands value, Exact.  5) Repeat to one row.  6) Pin it, set a Target " +
            "(in $1,000s), tick Freeze."),

        new Note("Freeze vs. poke",
            "Editing a pinned row's Target writes once, immediately. Ticking Freeze re-writes it every ~200 ms so " +
            "the fiscal tick (revenue − maintenance − interest) can't move it back. Freeze cash to hold a fixed " +
            "bankroll; untick it to let income accumulate normally."),

        new Note("Keep it believable ($30M ceiling)",
            "The game clamps cash to $30,000,000 (stored 30000) during its own accounting, so \"Set max cash\" " +
            "targets exactly that. A larger poke will hold at rest but the next accounting pass may snap it back. " +
            "Cash is a signed 16-bit word, so a Target above 32767 (about $32.7M) wraps to a negative balance in " +
            "the game — stay at or below $30M, and keep a save."),

        new Note("The startup locomotive quiz",
            "Before a game begins, Railroad Tycoon shows one locomotive and asks you to identify it from a list; a " +
            "wrong answer handicaps your whole game (its overlay is still active in this build). Match the picture's " +
            "wheel arrangement and era to the roster below. The quiz labels a couple of engines with shorter names " +
            "than the buy-list does — the buyable \"2-6-6-2 Mallet\" appears in the quiz as \"Challenger\" and the " +
            "\"'F' Series Diesel\" as \"F3A-Series\" — and its options are drawn from the US and England rosters at " +
            "once, so read the picture, not just the names."),

        new Note("Single-player, offline",
            "This is a single-player cheat tool for your own game. It reads and writes the emulator's memory only; " +
            "it never touches the network. Supply your own legally-obtained copy of Railroad Tycoon."),
    };
}
