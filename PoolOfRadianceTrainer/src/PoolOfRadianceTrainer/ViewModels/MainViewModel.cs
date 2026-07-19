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
    public IReadOnlyList<WalkthroughSection> Guide => Walkthrough.Sections;

    public MemorySearchViewModel MemorySearch { get; } = new();
    public SaveEditorViewModel SaveEditor { get; } = new();
    public LiveInventoryViewModel LiveInventory { get; } = new();
    public MapsViewModel Maps { get; } = new();

    /// <summary>Auto-re-rolls a new character on the create-a-character screen until a target roll is hit.</summary>
    public CharacterRollerViewModel Roller { get; }

    // --- state ---------------------------------------------------------------
    private ProcessEntry? _selectedProcess;
    public ProcessEntry? SelectedProcess { get => _selectedProcess; set { SetProperty(ref _selectedProcess, value); RaiseCommands(); } }

    private CharacterViewModel? _selectedCharacter;
    public CharacterViewModel? SelectedCharacter { get => _selectedCharacter; set => SetProperty(ref _selectedCharacter, value); }

    private CharacterViewModel? _selectedEnemy;
    public CharacterViewModel? SelectedEnemy { get => _selectedEnemy; set => SetProperty(ref _selectedEnemy, value); }

    public bool IsAttached => _mem is { IsOpen: true };

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
        KillEnemyCommand = new RelayCommand(_ => SelectedEnemy?.KillNow(), _ => SelectedEnemy != null);
        KillAllEnemiesCommand = new RelayCommand(_ => { foreach (var e in Enemies) e.KillNow(); }, _ => Enemies.Count > 0);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _poll.Tick += (_, _) => PollTick();

        Roller = new CharacterRollerViewModel(
            () => _mem,
            () => IsAttached ? SelectedProcess?.Id : null,
            s => Status = s);

        RefreshProcesses();
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
        OnPropertyChanged(nameof(FreezeAll));
        OnPropertyChanged(nameof(IsAttached));
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
            Status = Party.Count == 0 && Enemies.Count == 0
                ? "No records found. Make sure a party is loaded (past the title screen), then Re-scan."
                : $"Found {Party.Count} character(s) and {Enemies.Count} combatant/monster record(s).";
        }
        catch (OperationCanceledException) { Status = "Scan cancelled."; }
        catch (Exception ex) { Status = "Scan error: " + ex.Message; }
        finally { IsScanning = false; RaiseCommands(); }
    }

    // --- party-wide actions --------------------------------------------------
    private void ForEachParty(Action<CharacterViewModel> action)
    {
        foreach (var c in Party) action(c);
        Status = "Applied to the whole party.";
    }

    public void HealParty()
    {
        foreach (var c in Party) c.FullHeal();
        Status = "Party healed.";
    }

    // --- poll loop -----------------------------------------------------------
    // One scratch buffer reused across all characters each tick — RefreshLiveSummary copies
    // out of it immediately, so no per-tick allocation. The live summary is refreshed for the
    // selected record too (it only raises read-only summary props, never the editor fields, so
    // an in-progress edit isn't clobbered) — so you can watch the selected character take damage.
    private readonly byte[] _pollBuf = new byte[PorFormat.RecordSize];

    private void PollTick()
    {
        if (_mem == null) return;
        foreach (var c in Party)
        {
            c.ApplyFreeze();
            if (CharacterLocator.Reread(_mem, c.Address, _pollBuf)) c.RefreshLiveSummary(_pollBuf);
        }
        foreach (var e in Enemies)
        {
            if (CharacterLocator.Reread(_mem, e.Address, _pollBuf)) e.RefreshLiveSummary(_pollBuf);
        }
        LiveInventory.ApplyFreeze();
        MemorySearch.RefreshValues();
        Maps.Tick();
    }

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
