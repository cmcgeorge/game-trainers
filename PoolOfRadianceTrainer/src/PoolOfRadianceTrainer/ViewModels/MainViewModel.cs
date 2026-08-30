using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using PoolOfRadianceTrainer.Game;
using PoolOfRadianceTrainer.Memory;
using PoolOfRadianceTrainer.Mvvm;

namespace PoolOfRadianceTrainer.ViewModels;

/// <summary>A selectable target process.</summary>
public sealed class ProcessEntry
{
    public int Id { get; }
    public string Name { get; }
    public bool IsEmulator { get; }
    public string Display => $"{Name}  (pid {Id})";
    public override string ToString() => Display;

    public ProcessEntry(int id, string name, bool isEmulator)
    {
        Id = id; Name = name; IsEmulator = isEmulator;
    }
}

/// <summary>
/// Root view-model: process attach/scan, the located party and enemy lists, the god-mode /
/// freeze poll loop, the reference tabs, and the memory-search tab.
/// </summary>
public sealed class MainViewModel : ObservableObject, ICharacterHost, IDisposable
{
    private static readonly string[] EmulatorHints =
        { "dosbox", "dosbox-x", "dosbox-staging", "scummvm", "pcem", "86box", "qemu", "boxer" };

    private ProcessMemory? _mem;
    private readonly DispatcherTimer _poll;
    private GlobalHotkeys? _hotkeys;
    private CancellationTokenSource? _scanCts;

    // --- collections ---------------------------------------------------------
    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ObservableCollection<CharacterViewModel> Party { get; } = new();
    public ObservableCollection<CharacterViewModel> Enemies { get; } = new();

    public IReadOnlyList<MonsterInfo> Monsters => _monsterView;
    private List<MonsterInfo> _monsterView = MonsterBook.All.ToList();
    public IReadOnlyList<SpellInfo> Spells => _spellView;
    private List<SpellInfo> _spellView = SpellBook.All.ToList();
    public IReadOnlyList<ClassInfo> ClassRef => ClassRaceBook.Classes;
    public IReadOnlyList<RaceInfo> RaceRef => ClassRaceBook.Races;
    public IReadOnlyList<XpRow> XpTable => ClassRaceBook.XpTable;
    public IReadOnlyList<LevelProgressionRow> LevelProgression => ClassRaceBook.LevelProgression;
    public IReadOnlyList<WalkthroughSection> Guide => Walkthrough.Sections;

    public MemorySearchViewModel MemorySearch { get; } = new();
    public SaveEditorViewModel SaveEditor { get; } = new();
    public LiveInventoryViewModel LiveInventory { get; } = new();
    public MapsViewModel Maps { get; } = new();
    public CluebookViewModel Cluebook { get; } = new();

    /// <summary>Auto-re-rolls a new character on the create-a-character screen until a target roll is hit.</summary>
    public CharacterRollerViewModel Roller { get; }

    /// <summary>Rolls a whole good-aligned party and writes it over the live party or a saved one.</summary>
    public PartyGeneratorViewModel PartyGen { get; }

    // --- state ---------------------------------------------------------------
    private ProcessEntry? _selectedProcess;
    public ProcessEntry? SelectedProcess { get => _selectedProcess; set { SetProperty(ref _selectedProcess, value); RaiseCommands(); } }

    private CharacterViewModel? _selectedCharacter;
    public CharacterViewModel? SelectedCharacter { get => _selectedCharacter; set => SetProperty(ref _selectedCharacter, value); }

    private CharacterViewModel? _selectedEnemy;
    public CharacterViewModel? SelectedEnemy { get => _selectedEnemy; set => SetProperty(ref _selectedEnemy, value); }

    public bool IsAttached => _mem is { IsOpen: true };

    /// <summary>
    /// Is a battle on screen right now? True while the arena sweep can see live monster records,
    /// which is the same test the game itself effectively uses — a creature's per-fight block at
    /// <c>0x108</c> is null outside combat and non-null during one (see
    /// <c>docs/reverse-engineering.md</c> §6).
    ///
    /// <para>This drives the warnings on record edits, not a block on them. The character record is
    /// not the combat state: the engine runs a fight off that separate per-combatant block and
    /// rebuilds it every round, so writing the record mid-battle is safe — freezing party HP through
    /// a fight is exactly what god mode is for. What it isn't is <i>reliable</i>: fields the engine
    /// has already copied into the fight can read back unchanged until the battle ends, so a "Max
    /// EVERYTHING" during a round can look like it did nothing. <see cref="CombatCaveat"/> says so
    /// rather than the trainer silently doing nothing.</para>
    /// </summary>
    public bool IsBattleActive => Enemies.Count > 0;

    /// <summary>Appended to the status line for a party/record edit made during a battle.</summary>
    public string CombatCaveat => IsBattleActive
        ? " Battle in progress: the engine runs the fight off its own per-combatant block, so some " +
          "fields won't take effect until it ends. HP and status freezes do hold — and the Combat " +
          "tab edits the creature records the fight is actually reading."
        : "";

    /// <summary>Status text for an edit, with the battle caveat appended when one is on.</summary>
    private string WithCombatCaveat(string text) => text + CombatCaveat;

    private bool _isScanning;
    public bool IsScanning { get => _isScanning; set { SetProperty(ref _isScanning, value); RaiseCommands(); } }

    private string _status = "Launch Pool of Radiance in DOSBox, then pick the process and Attach.";
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    private string _hotkeyStatus = "";
    public string HotkeyStatus { get => _hotkeyStatus; set => SetProperty(ref _hotkeyStatus, value); }

    // Set while FreezeAll drives GodMode+FreezeStatus together, so their individual status
    // messages are suppressed and only FreezeAll's single summary is written.
    private bool _suppressFreezeText;

    private bool _godMode;
    public bool GodMode
    {
        get => _godMode;
        set
        {
            if (!SetProperty(ref _godMode, value)) return;
            foreach (var c in Party) c.FreezeHp = value;
            OnPropertyChanged(nameof(FreezeAll));
            if (!_suppressFreezeText) Status = value ? "God mode ON — party HP frozen." : "God mode OFF.";
        }
    }

    private bool _freezeStatus;
    public bool FreezeStatus
    {
        get => _freezeStatus;
        set
        {
            if (!SetProperty(ref _freezeStatus, value)) return;
            foreach (var c in Party) c.FreezeStatus = value;
            OnPropertyChanged(nameof(FreezeAll));
            if (!_suppressFreezeText) Status = value ? "Party status frozen to Okay." : "Party status freeze OFF.";
        }
    }

    private bool _freezeSpells;
    /// <summary>Party-wide: keep every caster's memorized spells from depleting when cast. Each
    /// character snapshots its memorized-spell block when this switches on, so turn it on right
    /// after resting/memorizing.</summary>
    public bool FreezeSpells
    {
        get => _freezeSpells;
        set
        {
            if (!SetProperty(ref _freezeSpells, value)) return;
            foreach (var c in Party) c.FreezeSpells = value;
            Status = value
                ? "Spell freeze ON — memorized spells won't deplete when cast (snapshot taken now)."
                : "Spell freeze OFF.";
        }
    }

    private bool _autoWeaken;
    /// <summary>
    /// Keep every creature in the arena on 1 HP, AC 20 and THAC0 20 for as long as it is ticked —
    /// the standing version of the Weaken button. The poll loop applies it to whatever the combat
    /// sweep is currently listing, so a battle that starts while this is on is weakened within a
    /// tick or two without anything being clicked; between battles there is nothing to act on and
    /// it does nothing. Loot-safe: the party still lands the killing blows, so bodies, treasure and
    /// XP all count.
    /// </summary>
    public bool AutoWeaken
    {
        get => _autoWeaken;
        set
        {
            if (!SetProperty(ref _autoWeaken, value)) return;
            if (value)
            {
                AutoKill = false;              // two standing edits to the same records; pick one
                int n = ApplyAutoCombat();     // don't make the user wait a tick for the fight on screen
                Status = n > 0
                    ? $"Auto-weaken ON — {n} enemy record(s) put on 1 HP; every new encounter follows automatically."
                    : "Auto-weaken ON — enemies will be put on 1 HP, AC 20, THAC0 20 as each battle starts.";
            }
            else Status = "Auto-weaken OFF — enemies fight at full strength again.";
        }
    }

    private bool _autoKill;
    /// <summary>
    /// Zero every arena record as it appears — the standing version of the Kill button, and it
    /// carries the same cost: the engine never processes these as kills, so encounters pay no XP
    /// and leave no treasure. Left off unless asked for, and it asks once before switching on;
    /// <see cref="AutoWeaken"/> is the toggle that wins fights and keeps the loot.
    /// </summary>
    public bool AutoKill
    {
        get => _autoKill;
        set
        {
            // Confirm before the field changes, so a declined prompt leaves the toggle off. The
            // check box has already drawn itself ticked by the time its binding gets here, and a
            // notification raised inside that update is swallowed — post the correction behind it.
            if (value && !_autoKill && !ConfirmAutoKill())
            {
                _poll.Dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(AutoKill)));
                return;
            }
            if (!SetProperty(ref _autoKill, value)) return;
            if (value)
            {
                AutoWeaken = false;
                int n = ApplyAutoCombat();
                Status = n > 0
                    ? $"Auto-kill ON — {n} enemy record(s) zeroed; every new encounter follows automatically. No XP, no treasure."
                    : "Auto-kill ON — every encounter's records will be zeroed as it starts. No XP, no treasure.";
            }
            else Status = "Auto-kill OFF.";
        }
    }

    /// <summary>
    /// Single toggle for the whole party: freezes HP (god mode) *and* pins status to Okay.
    /// Checked only when both are on; toggling drives both underlying freezes together.
    /// </summary>
    public bool FreezeAll
    {
        get => GodMode && FreezeStatus;
        set
        {
            _suppressFreezeText = true;
            GodMode = value;
            FreezeStatus = value;
            _suppressFreezeText = false;
            Status = value
                ? "Party frozen — HP kept at max and status pinned to Okay."
                : "Party freeze OFF.";
        }
    }

    private string _monsterFilter = "";
    public string MonsterFilter { get => _monsterFilter; set { if (SetProperty(ref _monsterFilter, value)) { _monsterView = MonsterBook.Search(value).ToList(); OnPropertyChanged(nameof(Monsters)); } } }

    private string _spellFilter = "";
    public string SpellFilter { get => _spellFilter; set { if (SetProperty(ref _spellFilter, value)) { _spellView = SpellBook.Search(value).ToList(); OnPropertyChanged(nameof(Spells)); } } }

    // --- commands ------------------------------------------------------------
    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand HealPartyCommand { get; }
    public ICommand MaxPartyCommand { get; }
    public ICommand MaxEverythingPartyCommand { get; }
    public ICommand MaxMoneyPartyCommand { get; }
    public ICommand RandomizeIconColorsPartyCommand { get; }
    public ICommand KillEnemyCommand { get; }
    public ICommand KillAllEnemiesCommand { get; }
    public ICommand WeakenEnemyCommand { get; }
    public ICommand WeakenAllEnemiesCommand { get; }

    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        AttachCommand = new RelayCommand(_ => Attach(), _ => SelectedProcess != null && !IsAttached);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);
        ScanCommand = new RelayCommand(_ => Scan(), _ => IsAttached && !IsScanning);
        HealPartyCommand = new RelayCommand(_ => HealParty(), _ => Party.Count > 0);
        MaxPartyCommand = new RelayCommand(_ => ForEachParty(c => c.MaxStats()), _ => Party.Count > 0);
        MaxEverythingPartyCommand = new RelayCommand(_ => ForEachParty(c => c.MaxEverything()), _ => Party.Count > 0);
        MaxMoneyPartyCommand = new RelayCommand(_ => ForEachParty(c => c.MaxMoney()), _ => Party.Count > 0);
        RandomizeIconColorsPartyCommand = new RelayCommand(_ => ForEachParty(c => c.RandomizeIconColors()), _ => Party.Count > 0);
        KillEnemyCommand = new RelayCommand(_ =>
        {
            if (!ConfirmKill(1)) return;
            SelectedEnemy?.KillNow(); NoteKill(1);
        }, _ => SelectedEnemy != null);
        KillAllEnemiesCommand = new RelayCommand(_ =>
        {
            if (!ConfirmKill(Enemies.Count)) return;
            foreach (var e in Enemies) e.KillNow(); NoteKill(Enemies.Count);
        }, _ => Enemies.Count > 0);
        WeakenEnemyCommand = new RelayCommand(_ => { SelectedEnemy?.WeakenNow(); NoteWeaken(1); }, _ => SelectedEnemy != null);
        WeakenAllEnemiesCommand = new RelayCommand(_ => { foreach (var e in Enemies) e.WeakenNow(); NoteWeaken(Enemies.Count); },
            _ => Enemies.Count > 0);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _poll.Tick += (_, _) => PollTick();

        Roller = new CharacterRollerViewModel(
            () => _mem,
            () => IsAttached ? SelectedProcess?.Id : null,
            s => Status = s);

        // Writing a generated party into the live game is a record edit like any other, so its
        // status line carries the same in-battle caveat the party-wide buttons do.
        PartyGen = new PartyGeneratorViewModel(() => Party, SaveEditor, s => Status = WithCombatCaveat(s));

        RefreshProcesses();
        TryAutoAttach();
    }

    /// <summary>On startup, attach automatically when the pre-selected process looks like a game emulator, so a running game is picked up without a manual click. Stays a no-op (just the populated process list) when nothing emulator-looking is running, rather than attaching to some unrelated process and scanning it fruitlessly.</summary>
    private void TryAutoAttach()
    {
        if (!IsAttached && SelectedProcess?.IsEmulator == true) Attach();
    }

    // --- process management --------------------------------------------------
    public void RefreshProcesses()
    {
        var previous = SelectedProcess?.Id;
        Processes.Clear();
        var list = new List<ProcessEntry>();
        foreach (var p in Process.GetProcesses())
        {
            // Each Process holds a native handle; dispose it once its name/id are captured.
            try
            {
                string name = p.ProcessName;
                bool emu = EmulatorHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));
                list.Add(new ProcessEntry(p.Id, name, emu));
            }
            catch { /* process exited between enumeration and query */ }
            finally { p.Dispose(); }
        }
        // Emulators first, then alphabetical.
        foreach (var e in list.OrderByDescending(e => e.IsEmulator).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            Processes.Add(e);

        SelectedProcess = Processes.FirstOrDefault(e => e.Id == previous)
                          ?? Processes.FirstOrDefault(e => e.IsEmulator)
                          ?? Processes.FirstOrDefault();
    }

    private void Attach()
    {
        if (SelectedProcess == null) return;
        try
        {
            _mem = ProcessMemory.Open(SelectedProcess.Id);
            MemorySearch.Attach(_mem);
            LiveInventory.Attach(_mem);
            Maps.Attach(_mem);
            OnPropertyChanged(nameof(IsAttached));
            RaiseCommands();
            Roller.RefreshCommands();   // the roller can act now that we're attached
            _poll.Start();
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). Now Scan for the party.";
            Scan();
        }
        catch (Exception ex)
        {
            Status = "Attach failed: " + ex.Message;
        }
    }

    private void Detach()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        IsScanning = false;      // a cancelled scan must not block the next attach's auto-scan
        Roller.Reset();          // stop the roll loop before disposing the handle; the locked roll
                                 // address belonged to the process we're leaving anyway
        MemorySearch.Detach();
        LiveInventory.Detach();
        Maps.Detach();
        _mem?.Dispose();
        _mem = null;
        Party.Clear();
        Enemies.Clear();
        SelectedCharacter = null;
        SelectedEnemy = null;
        _godMode = false; OnPropertyChanged(nameof(GodMode));
        _freezeStatus = false; OnPropertyChanged(nameof(FreezeStatus));
        _freezeSpells = false; OnPropertyChanged(nameof(FreezeSpells));
        _autoWeaken = false; OnPropertyChanged(nameof(AutoWeaken));
        _autoKill = false; OnPropertyChanged(nameof(AutoKill));
        OnPropertyChanged(nameof(FreezeAll));
        OnPropertyChanged(nameof(IsAttached));
        PartyGen.Refresh();      // the live party is gone; the generator's readout must say so
        RaiseCommands();
        Status = "Detached.";
    }

    // --- scanning ------------------------------------------------------------
    private async void Scan()
    {
        if (_mem == null || IsScanning) return;
        IsScanning = true;
        Status = "Scanning memory for character records…";
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        var mem = _mem;
        try
        {
            var found = await Task.Run(() => CharacterLocator.FindAll(mem, null, ct), ct);
            // If the user detached (or re-attached) while the scan ran, don't repopulate
            // the party against a now-disposed/replaced process.
            if (mem != _mem) return;
            Party.Clear();
            Enemies.Clear();
            foreach (var lc in found)
            {
                // A record that decodes as a monster but reads impossible combat numbers is a
                // look-alike scratch buffer, not a creature — keep it out of both lists.
                if (lc.IsMonster && !lc.IsLiveMonster) continue;
                var vm = new CharacterViewModel(this, lc);
                if (vm.IsMonster) Enemies.Add(vm); else Party.Add(vm);
            }
            SelectedCharacter = Party.FirstOrDefault();
            SelectedEnemy = Enemies.FirstOrDefault();
            // Rebuild the live-inventory lists from the same address-sorted located records.
            LiveInventory.Load(found);
            if (GodMode) foreach (var c in Party) c.FreezeHp = true;
            if (FreezeStatus) foreach (var c in Party) c.FreezeStatus = true;
            if (FreezeSpells) foreach (var c in Party) c.FreezeSpells = true;
            PartyGen.Refresh();   // the party the generator would write over has just changed
            Status = Party.Count == 0 && Enemies.Count == 0
                ? "No records found. Make sure a party is loaded (past the title screen), then Re-scan."
                : $"Found {Party.Count} character(s) and {Enemies.Count} combatant/monster record(s).";
        }
        catch (OperationCanceledException) { if (mem == _mem) Status = "Scan cancelled."; }
        catch (Exception ex) { if (mem == _mem) Status = "Scan error: " + ex.Message; }
        finally { IsScanning = false; RaiseCommands(); }
    }

    // --- combat actions ------------------------------------------------------

    /// <summary>
    /// Asks the user to confirm an irreversible action. The window supplies a real dialog; left
    /// unset (headless) every action goes ahead, so this is a UI courtesy, not a safety mechanism.
    /// </summary>
    public Func<string, bool> Confirm { get; set; } = _ => true;

    /// <summary>
    /// Kill forfeits the encounter's treasure and XP, and there is no undo once the records are
    /// zeroed — the status line explaining that only appears after the click, which is too late to
    /// be a warning. Ask first.
    /// </summary>
    private bool ConfirmKill(int n) => Confirm(
        (n == 1 ? "Zero this enemy's record?" : $"Zero all {n} enemy records?") +
        "\n\nThe engine never processes this as a kill, so the encounter pays no XP and leaves no " +
        "treasure — and it cannot be undone.\n\nUse Weaken instead to win the fight and keep the loot.");

    /// <summary>Asked once, when <see cref="AutoKill"/> is switched on rather than on every
    /// encounter it then acts on — a prompt per fight would be worse than no prompt at all.</summary>
    private bool ConfirmAutoKill() => Confirm(
        "Zero every enemy record automatically, for every battle from now on?\n\n" +
        "The engine never processes these as kills, so no encounter will pay XP or leave treasure " +
        "while this is on, and none of it can be undone.\n\n" +
        "Auto-weaken wins the fights and keeps the loot.");

    /// <summary>
    /// The standing half of <see cref="AutoWeaken"/>/<see cref="AutoKill"/>: apply the ticked one to
    /// every arena record that still needs it, and return how many were touched. Runs off
    /// <see cref="Enemies"/>, which the arena sweep keeps current, so a battle starting is enough to
    /// bring new creatures under it; creatures already in the target state, and any already out of
    /// the fight, are skipped so nothing is re-written every tick and no corpse is stood back up.
    ///
    /// <para>Each record is re-read and re-identified here rather than on the poll loop's copy.
    /// That is what makes the pass safe to run unattended: these writes follow a remembered
    /// address, and the game frees and reuses heap slots across area and combat transitions, so a
    /// slot that has been handed to something else — a party record shares the same 640 KiB DOS
    /// heap — would otherwise be stamped with 1 HP. The list itself can be up to a tick stale (the
    /// arena sweep runs every other tick, and not at all while the party is unlocated), and the
    /// toggles call this before any sweep has run at all, so the check cannot be left to them.
    /// Reading here also means the decision is made on this instant's hit points, not on the ones
    /// the creature had before the blow that just landed.</para>
    /// </summary>
    private int ApplyAutoCombat()
    {
        // Nothing to do is the common case — leave without touching the game at all.
        if (_mem == null || (!AutoWeaken && !AutoKill)) return 0;

        int n = 0;
        foreach (var e in Enemies)
        {
            // A record being typed into belongs to the user until they're done with it: WeakenNow
            // and KillNow raise the very properties the combat editor binds, and its boxes commit
            // on lost focus, so writing here would wipe a half-typed value.
            if (EnemyEditorFocused && ReferenceEquals(e, SelectedEnemy)) continue;

            // Shares the poll loop's scratch buffer: both run on the UI thread, and the poll tick
            // has finished with it by the time this is called.
            if (!CharacterLocator.Reread(_mem, e.Address, _pollBuf, e.Record)) continue;
            e.RefreshLiveSummary(_pollBuf);

            if (AutoWeaken) { if (e.NeedsWeakening) { e.WeakenNow(); n++; } }
            else if (e.NeedsKilling) { e.KillNow(); n++; }
        }
        return n;
    }

    // Wording for a pass the user didn't click for: name the toggle that did it, so a status line
    // that appears on its own mid-fight is traceable to the checkbox that's still ticked.
    private static string NoteAutoWeaken(int n) =>
        $"Auto-weaken: {n} enemy record(s) on 1 HP, AC 20, THAC0 20 — one hit each, and the kills still pay XP and treasure.";

    private static string NoteAutoKill(int n) =>
        $"Auto-kill: {n} enemy record(s) zeroed. The engine counts none of them as kills, so this encounter pays nothing.";

    // Both messages exist because the difference between the two buttons is not visible on screen
    // until the fight is over and the treasure screen is (or isn't) offered.
    private void NoteWeaken(int n) => Status = n == 1
        ? "Enemy left on 1 HP, AC 20, THAC0 20 — one hit kills it, and the kill counts for XP and treasure."
        : $"{n} enemies left on 1 HP, AC 20, THAC0 20 — one hit each kills them, and the kills count for XP and treasure.";

    private void NoteKill(int n) => Status = n == 1
        ? "Enemy record zeroed. The engine never processes this as a kill, so it forfeits that creature's loot — Weaken instead if you want the treasure."
        : $"{n} enemy records zeroed. The engine never processes these as kills, so the encounter's loot is forfeit — Weaken instead if you want the treasure.";

    // --- party-wide actions --------------------------------------------------
    private void ForEachParty(Action<CharacterViewModel> action)
    {
        foreach (var c in Party) action(c);
        Status = WithCombatCaveat("Applied to the whole party.");
    }

    public void HealParty()
    {
        foreach (var c in Party) c.FullHeal();
        Status = WithCombatCaveat("Party healed.");
    }

    // --- poll loop -----------------------------------------------------------
    // One scratch buffer reused across all characters each tick — RefreshLiveSummary copies
    // out of it immediately, so no per-tick allocation. The live summary is refreshed for the
    // selected record too (it only raises read-only summary props, never the editor fields, so
    // an in-progress edit isn't clobbered) — so you can watch the selected character take damage.
    private readonly byte[] _pollBuf = new byte[PorFormat.RecordSize];

    // Scratch buffer for the combat-arena sweep, and its tick divider — the sweep costs a ~1 MiB
    // read, so it runs every other tick (~1.2 s) rather than on every one.
    private readonly byte[] _arenaBuf = new byte[CharacterLocator.SweepBufferSize];
    private int _tick;

    /// <summary>Set by the view while the Combat tab's editor has keyboard focus, so the live
    /// refresh of a monster's fields never overwrites a number being typed into them.</summary>
    public bool EnemyEditorFocused { get; set; }

    private void PollTick()
    {
        if (_mem == null) return;
        // Each record is checked against the character it was found as before its bytes are
        // adopted: the game frees and reuses heap slots across area and combat transitions, and a
        // slot that has been handed to something else would otherwise be decoded under this
        // character's name — and, since ApplyFreeze writes back through the same address, would be
        // stamped with their HP too. A slot that fails the check is left showing its last known
        // state until the next Scan.
        foreach (var c in Party)
        {
            if (CharacterLocator.Reread(_mem, c.Address, _pollBuf, c.Record))
            {
                c.RefreshLiveSummary(_pollBuf);
                c.ApplyFreeze();
            }
        }

        if (++_tick % 2 == 0) SweepEnemies();

        foreach (var e in Enemies)
        {
            if (CharacterLocator.Reread(_mem, e.Address, _pollBuf, e.Record)) e.RefreshLiveSummary(_pollBuf);
        }
        // Re-reads and re-identifies each record itself, so it is safe here and equally safe from
        // the toggles' setters. Reported over SweepEnemies' "Battle on" line, which it just made
        // untrue.
        int autoDone = ApplyAutoCombat();
        if (autoDone > 0) Status = AutoWeaken ? NoteAutoWeaken(autoDone) : NoteAutoKill(autoDone);

        // The combat editor watches a creature that is being hit while you look at it, so unlike
        // the party panel its fields do track the record — except while they're being typed into.
        if (!EnemyEditorFocused) SelectedEnemy?.RefreshCombatEditors();

        LiveInventory.Tick();
        MemorySearch.RefreshValues();
        Maps.Tick();
    }

    // --- combat arena --------------------------------------------------------
    /// <summary>
    /// Re-finds the battle's monster records and reconciles <see cref="Enemies"/> with them, so the
    /// Combat tab fills when a battle starts and empties when it ends without a manual re-scan.
    /// Monster records are built fresh for every encounter at addresses the last full scan knows
    /// nothing about, which is why the list can't just be the scan's leftovers.
    /// View-models are matched to arena slots by address so selection — and the record a "Kill"
    /// button is aimed at — survives a sweep; a slot that a different creature has taken over gets
    /// a fresh view-model so its editor fields don't describe the creature that used to be there.
    /// </summary>
    private void SweepEnemies()
    {
        if (_mem == null || Party.Count == 0) return;

        nuint low = Party[0].Address, high = Party[0].Address;
        foreach (var c in Party)
        {
            if (c.Address < low) low = c.Address;
            if (c.Address > high) high = c.Address;
        }

        var found = CharacterLocator.FindCombatants(_mem, low, high, _arenaBuf);

        // Between battles (and between rounds of the same one) the arena is unchanged — leave the
        // collection alone so the list doesn't flicker and the selection doesn't move.
        if (found.Count == Enemies.Count)
        {
            bool same = true;
            for (int i = 0; i < found.Count && same; i++)
                same = found[i].Address == Enemies[i].Address && SameCreature(found[i].Record, Enemies[i].Record);
            if (same) return;
        }

        int before = Enemies.Count;
        var existing = new Dictionary<nuint, CharacterViewModel>();
        foreach (var e in Enemies) existing[e.Address] = e;

        var selected = SelectedEnemy;
        var next = new List<CharacterViewModel>(found.Count);
        foreach (var lc in found)
            next.Add(existing.TryGetValue(lc.Address, out var vm) && SameCreature(lc.Record, vm.Record)
                ? vm                                    // same creature still in the fight
                : new CharacterViewModel(this, lc));    // a new creature holds this slot

        Enemies.Clear();
        foreach (var vm in next) Enemies.Add(vm);
        // Clearing the collection drives the list box's selection to null; put it back.
        SelectedEnemy = selected != null && next.Contains(selected) ? selected : next.FirstOrDefault();

        // A battle starting or ending changes what an edit to a party record will actually do.
        if ((before == 0) != (next.Count == 0))
        {
            OnPropertyChanged(nameof(IsBattleActive));
            OnPropertyChanged(nameof(CombatCaveat));
        }

        // The Kill buttons key off Enemies.Count, and nothing else re-queries them when a battle
        // starts while the user's hands are off the trainer.
        RaiseCommands();

        if (before == 0 && next.Count > 0) Status = $"Battle on — {next.Count} monster record(s) in the arena.";
        else if (before > 0 && next.Count == 0) Status = "Battle over — no monster records in memory.";
    }

    /// <summary>Is this the same creature the view-model was built for? Identity is Name, Race,
    /// Class and Gender — the fields a battle never changes. Max HP was previously included but
    /// is dropped by level drain, so a drained creature would read as a different one.</summary>
    private static bool SameCreature(CharacterRecord a, CharacterRecord b) =>
        a.IsSameCreatureAs(b);

    // --- global hotkeys ------------------------------------------------------
    public void InitHotkeys(IntPtr hwnd)
    {
        _hotkeys = new GlobalHotkeys(hwnd);
        _hotkeys.GodModeToggled += () => GodMode = !GodMode;
        _hotkeys.HealRequested += HealParty;
        _hotkeys.MaxRequested += () => ForEachParty(c => c.MaxEverything());

        var parts = new List<string>();
        if (_hotkeys.GodModeRegistered) parts.Add("Ctrl+F1 god mode");
        if (_hotkeys.HealRegistered) parts.Add("Ctrl+F2 heal");
        if (_hotkeys.MaxRegistered) parts.Add("Ctrl+F3 max");
        HotkeyStatus = parts.Count == 3 ? "Hotkeys: " + string.Join(" · ", parts)
            : parts.Count == 0 ? "Global hotkeys unavailable (in use by another app)."
            : "Some hotkeys unavailable; active: " + string.Join(" · ", parts);
    }

    // --- ICharacterHost ------------------------------------------------------
    bool ICharacterHost.WriteBytes(nuint recordAddress, byte[] source, int offset, int length)
        => _mem?.WriteRange(recordAddress, source, offset, length) ?? false;

    private void RaiseCommands()
    {
        (AttachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _hotkeys?.Dispose();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        Roller.Reset();          // stop any in-flight roll loop before the handle closes
        _mem?.Dispose();
    }
}
