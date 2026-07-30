using System.Collections.ObjectModel;
using PiratesTrainer.Game;

namespace PiratesTrainer.ViewModels;

/// <summary>A titled note for the reference tab.</summary>
public sealed record Note(string Title, string Body);

/// <summary>
/// Static game knowledge for the Reference tab: the settlement tables and convoy itineraries decoded out
/// of <c>DISK1</c> (the itineraries double as the 1987 manual's copy-protection answer key), the ship /
/// goods / rank / speciality tables read out of <c>DISKP</c>'s string table, the control list, and
/// how-to and reverse-engineering notes. None of it touches the live process.
/// </summary>
public sealed class ReferenceViewModel : ObservableObject
{
    public IReadOnlyList<ShipType> Ships { get; } = GameFacts.Ships;
    public IReadOnlyList<TradeGood> Goods { get; } = GameFacts.Goods;
    public IReadOnlyList<Rank> Ranks { get; } = GameFacts.Ranks;
    public IReadOnlyList<Speciality> Specialities { get; } = GameFacts.Specialities;
    public IReadOnlyList<DifficultyLevel> Difficulties { get; } = GameFacts.Difficulties;
    public IReadOnlyList<Expedition> Expeditions { get; } = GameFacts.Expeditions;
    public IReadOnlyList<ControlBinding> Controls { get; } = GameFacts.Controls;
    public IReadOnlyList<KnownValue> KnownValues { get; } = PiratesLayout.KnownValues;

    /// <summary>Every convoy stop in every era — the copy-protection answer key, filtered by the pickers.</summary>
    public ObservableCollection<ScheduleRow> Schedule { get; } = new();

    /// <summary>Settlements of the selected era.</summary>
    public ObservableCollection<City> Cities { get; } = new();

    /// <summary>The six era labels, plus the era years the pickers bind to.</summary>
    public IReadOnlyList<string> EraNames { get; } = CityBook.EraNames;

    private int _selectedEraIndex;
    /// <summary>
    /// Which era the Settlements and Convoys grids show (0-5).
    ///
    /// An out-of-range write is <b>rejected</b>, not clamped. Two <c>ComboBox</c>es bind
    /// <c>SelectedIndex</c> to this and <c>Selector.SelectedIndex</c> binds two-way, so a selector writes
    /// -1 back whenever its items detach — which switching tabs does, because it tears the content tree
    /// down. Clamping that -1 to 0 would silently reset the user's era to 1560 every time they looked at
    /// another tab; rejecting it and re-raising <c>PropertyChanged</c> snaps the picker back to what is
    /// actually being shown.
    /// </summary>
    public int SelectedEraIndex
    {
        get => _selectedEraIndex;
        set
        {
            if (value < 0 || value >= CityBook.EraYears.Count)
            {
                OnPropertyChanged(nameof(SelectedEraIndex));   // snap the picker back to the real selection
                return;
            }
            if (SetField(ref _selectedEraIndex, value)) Refresh();
        }
    }

    private string _cityFilter = "";
    /// <summary>Case-insensitive substring filter applied to both grids' city names.</summary>
    public string CityFilter
    {
        get => _cityFilter;
        set { if (SetField(ref _cityFilter, value)) Refresh(); }
    }

    public ReferenceViewModel() => Refresh();

    private void Refresh()
    {
        int era = _selectedEraIndex;      // the setter rejects anything out of range
        int year = CityBook.EraYears[era];
        string needle = CityFilter.Trim();

        Cities.Clear();
        foreach (var c in CityBook.ForEra(era))
            if (Matches(c.Name, needle)) Cities.Add(c);

        Schedule.Clear();
        foreach (var r in FleetSchedule.All)
            if (r.Year == year && Matches(r.City, needle)) Schedule.Add(r);
    }

    private static bool Matches(string name, string needle) =>
        needle.Length == 0 || name.Contains(needle, StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<Note> Notes { get; } = new[]
    {
        new Note("What this trainer edits",
            "Pirates! keeps the player's purse as an unsigned 16-bit word — the exact number of gold pieces the " +
            "party panel shows, saturating at 65,535. The trainer finds that word (by auto-locating the game's " +
            "data segment, or by a value scan) and lets you set or freeze it, along with the crew, your estate " +
            "and the game clock. It edits the running game's memory; it never touches your save disk."),

        new Note("Auto-locate vs. value scan",
            "Auto-locate sweeps the emulator's memory for the title-screen literal \"COPYRIGHT (C)  1987  " +
            "MICROPROSE INC.\", then checks that two more literals — the eight-byte \"PIRATES!\" save magic and the " +
            "JAN..DEC month table — sit at their known offsets from the implied base, and that the era, year and " +
            "settlement table all decode sanely. Only then does it pin anything. If it fails, the value scan " +
            "still works: it searches for the number itself and does not care about layout at all."),

        new Note("Check the summary before you trust it",
            "After a locate, the line above the grids shows your captain's name, the date, the era and your gold, " +
            "and the Settlements grid lists the era's towns by name. That is your verification: if the names are " +
            "readable and the date matches the game's own display, the base is right. If any of it looks like " +
            "garbage, Detach and use a guided scan instead — do not poke."),

        new Note("Gold — step by step (value scan)",
            "1) Attach to the dosbox process.  2) Click the Gold guide (sets a 16-bit scan).  3) Read the gold " +
            "figure on the party panel and type it; First Scan.  4) Spend or gain some, type the new figure, " +
            "Exact.  5) Repeat to one row.  6) Pin it, set a Target, tick Freeze."),

        new Note("Freeze vs. poke",
            "Editing a pinned row's Target writes once, immediately. Ticking Freeze re-writes it every ~200 ms so " +
            "the game's own tick can't undo it. Freezing the day-of-year stops the calendar, which stops you " +
            "ageing out of your career while your crew and food carry on as normal."),

        new Note("Keep it believable (65,535 gold)",
            "Gold is a 16-bit word: the add-gold routine saturates at 65,535 rather than wrapping, so that is the " +
            "true ceiling and what \"Max gold\" targets. Values in that range are safe; there is nothing sensible " +
            "above it. Keep a save on the save disk before experimenting."),

        new Note("Copy protection — the Convoys tab is the answer key",
            "The original 1987 release asked you to name the month in which the Treasure Fleet or Silver Train " +
            "reached a given port in a given year, from a chart in the manual. The Convoys grid is that chart, " +
            "decoded from the game's own route tables in DISK1. The DOS conversion in this repo's target folder " +
            "(PIR.EXE + DISKP) does not ask the question at all — the prompt text is absent from the program's " +
            "complete string table, and the original disk-based check is bypassed by construction, because " +
            "PIR.EXE services the game's raw sector reads out of ordinary files."),

        new Note("Where the money is",
            "The Settlements grid carries each town's starting garrison, population and treasury, straight from " +
            "the game's tables. Treasury is in thousands of gold pieces; garrison is soldiers. Panama, Havana, " +
            "Cartagena and Vera Cruz are the great prizes — and the best defended. The Convoys grid tells you " +
            "where the silver actually is in any given month, which is a far better plan than raiding at random."),

        new Note("Single-player, offline",
            "This is a single-player cheat tool for your own game. It reads and writes the emulator's memory " +
            "only; it never touches the network. Supply your own legally-obtained copy of Pirates!."),
    };
}
