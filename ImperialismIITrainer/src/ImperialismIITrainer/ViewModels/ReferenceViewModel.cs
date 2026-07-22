using ImperialismIITrainer.Game;

namespace ImperialismIITrainer.ViewModels;

/// <summary>A built-in cheat entry point recovered from the linker map (informational, not invoked).</summary>
public sealed record CheatFact(string Command, string Owner, string What);

/// <summary>A titled note for the reference tab.</summary>
public sealed record Note(string Title, string Body);

/// <summary>
/// Static game-knowledge for the Reference tab: the commodity list, the cheat/scripting surface the
/// game itself carries (found by name in <c>Imperialism II.map</c>), and how-to / reverse-engineering
/// notes. None of this touches the live process — it explains what the scanner is aiming at.
/// </summary>
public sealed class ReferenceViewModel : ObservableObject
{
    public IReadOnlyList<Commodity> Commodities { get; } = CommodityBook.Commodities;

    /// <summary>
    /// Cheat/script hooks the game binary contains (mangled symbol names in the map). They are listed
    /// so you know the model — e.g. that treasury is set by name, and warehouses/labour by script —
    /// not because the trainer calls them (it edits memory directly instead).
    /// </summary>
    public IReadOnlyList<CheatFact> Cheats { get; } = new[]
    {
        new CheatFact("settreasury <n>", "UCheaters",  "Console command that sets the player's treasury (\"players treasury set\")."),
        new CheatFact("ShowMeTheMoney",  "TAssetMgr",  "A money cheat entry point."),
        new CheatFact("SetMinorMagicMoney", "TSimMgr", "Grants minor powers money (AI/testing hook)."),
        new CheatFact("ScSetTreasury",   "TSimMgr",    "Scenario-script command: set a country's treasury."),
        new CheatFact("ScSetWarehouse",  "TSimMgr",    "Scenario-script command: set warehouse stockpiles."),
        new CheatFact("ScSetLabor",      "TSimMgr",    "Scenario-script command: set labour."),
        new CheatFact("ScSetCapacity",   "TSimMgr",    "Scenario-script command: set (transport) capacity."),
        new CheatFact("ScAddTech",       "TSimMgr",    "Scenario-script command: grant a technology."),
        new CheatFact("gCheatingIsEnabled", "USimMgr", "Global flag the game checks before honouring cheats."),
    };

    public IReadOnlyList<Note> Notes { get; } = new[]
    {
        new Note("What this trainer edits",
            "Imperialism II keeps each nation's economy in a heap object (the map calls the playable ones " +
            "TGreatPower, deriving from TCountry). The treasury is a signed 32-bit long; each warehouse " +
            "stockpile is a signed 16-bit short read by GetStockpile(commodity). This trainer finds those " +
            "bytes with a value scan and lets you set or freeze them — it does not call the game's own cheats."),

        new Note("Why a scanner and not one-click offsets",
            "A 1999 MFC exe has a fixed load address and no ASLR, so in principle its globals sit at constant " +
            "addresses. But the shipped exe (PE timestamp June 1999) is a later build than the bundled " +
            "Imperialism II.map (January 1999): the map's global addresses land on unrelated bytes in the live " +
            "process, and RTTI is stripped for the game's own classes, so there is no stable anchor to hang a " +
            "one-click locator on. The value scan sidesteps all of that — it works regardless of build."),

        new Note("Treasury — step by step",
            "1) Attach to Imperialism II.  2) Click the Treasury guide (sets a 32-bit scan).  3) Read your cash " +
            "on the top bar, type just the digits, First Scan.  4) End a turn or trade so it changes, type the " +
            "new value, Exact.  5) Repeat until one row is left.  6) Pin it, set a Target, tick Freeze."),

        new Note("Resources — step by step",
            "Pick the good in the Commodity box, click the Resource guide (sets a 16-bit scan). Read that good's " +
            "warehouse amount, First Scan, change it (transport/consume), Exact, narrow to one row, Pin and freeze. " +
            "Freezing matters: the economic turn recomputes stockpiles, so an unfrozen poke is undone next turn."),

        new Note("Freeze vs. poke",
            "Editing a pinned row's Target writes once, immediately. Ticking Freeze re-writes it every ~200 ms so " +
            "the simulation can't move it back. For treasury, a freeze holds you at a fixed bankroll; untick it to " +
            "let the number climb again from income."),

        new Note("Safety",
            "Read-validate-write: a value that doesn't fit the pinned width is rejected before it is written, so a " +
            "16-bit warehouse edit can't spill into the next field. If you set a wildly out-of-range value the game " +
            "may clamp or misbehave — freeze at believable numbers and keep a save."),
    };
}
