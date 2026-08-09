using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using SwordOfAragonTrainer.Game;
using SwordOfAragonTrainer.Memory;

namespace SwordOfAragonTrainer.ViewModels;

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
/// Root view-model. Sword of Aragon (SSI, 1989) is a compiled QuickBASIC 3.0 DOS game that runs under
/// DOSBox, and its code stream does not disassemble (a BRUN30 statement stream with the run-time module
/// absent from the image). Its <i>data</i>, on the other hand, is almost entirely legible — which sets
/// the shape of this trainer:
///
/// <list type="bullet">
/// <item><b>Save editor (primary).</b> <c>ARAGON.HS?</c> is plain CSV and <c>ARAGON.HR?</c> is an array
/// of 80 fixed 100-byte records. Both are edited in place, field by field, with everything else in the
/// file round-tripped verbatim and a one-off <c>.bak</c> taken before the first write.</item>
/// <item><b>Live tab (secondary).</b> The game's variables have no statically recoverable addresses, so
/// nothing is hard-coded. Instead the trainer signature-scans DOSBox for an <c>ARAGON.EXE</c> string
/// literal whose data-segment offset is known, accepts the hit only when at least two of three further
/// literals line up at their own offsets, derives <c>DS:0000</c> from it, and searches that one 64 KiB
/// window — with a whole-process value scanner as the build-independent fallback.</item>
/// </list>
///
/// Gold is a QuickBASIC single, i.e. Microsoft Binary Format rather than IEEE 754, so the trainer both
/// scans and writes it through <see cref="Mbf"/>.
/// </summary>
public sealed class MainViewModel : ObservableObject, IScanHost, IEditHost, IDisposable
{
    private const int MaxResultRows = 500;
    private const int LiveRefreshThreshold = 200;

    /// <summary>How far from the typed value a gold candidate may sit — the game displays it rounded.</summary>
    public const double WealthTolerance = 1.0;

    /// <summary>
    /// Width the "Find counter" guided search uses. Fixed at 16 bits because that is what
    /// <see cref="DgroupLocator.FindInt16"/> looks for; candidates carry it so a later pin cannot
    /// inherit an unrelated width from the Width combo box.
    /// </summary>
    private const ScanWidth SegmentCounterWidth = ScanWidth.Int16;

    private readonly byte[] _ioBuf = new byte[4];
    private readonly DispatcherTimer _poll;

    private ProcessMemory? _mem;
    private MemorySearcher? _searcher;
    private CancellationTokenSource? _scanCts;
    private int _targetPid;
    private DgroupLocation? _dgroup;
    private string _pendingPinLabel = "";
    private PinKind _pendingPinKind = PinKind.Raw;

    private KingdomFile? _kingdom;
    private RosterFile? _roster;

    // ---------------------------------------------------------------- collections
    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ObservableCollection<SaveSet> SaveSets { get; } = new();
    public ObservableCollection<CityViewModel> Cities { get; } = new();
    public ObservableCollection<RosterSlotViewModel> RosterSlots { get; } = new();
    public ObservableCollection<ScanResultViewModel> Results { get; } = new();
    public ObservableCollection<FrozenValueViewModel> Frozen { get; } = new();

    public IReadOnlyList<UnitType> UnitTypes { get; } = UnitBook.Types;
    public IReadOnlyList<Spell> Spells { get; } = SpellBook.Spells;
    public IReadOnlyList<CityInfo> ReferenceCities { get; } = CityBook.Cities;
    public IReadOnlyList<ProtectionAnswer> ProtectionAnswers { get; } = ProtectionBook.Answers;
    public IReadOnlyList<string> WorldTerrain { get; } = TerrainBook.WorldTerrain;
    public IReadOnlyList<CityInfo> TeleportTargets { get; } = CityBook.WithHexes.ToArray();
    public IReadOnlyList<ScanWidth> Widths { get; } = new[] { ScanWidth.Byte, ScanWidth.Int16, ScanWidth.Int32 };
    public string ProtectionPrompt => ProtectionBook.PromptText;

    /// <summary>The per-field candidate lists, for players without the poster.</summary>
    public IReadOnlyList<string> ProtectionFieldSummaries { get; } = ProtectionBook.Fields
        .Select(f => $"{f}: {string.Join(" · ", ProtectionBook.CandidatesFor(f))}")
        .ToArray();

    /// <summary>Equipment reference rows, flattened across the eight slots.</summary>
    public IReadOnlyList<EquipmentReferenceRow> EquipmentReference { get; } = UnitBook.Slots
        .SelectMany(slot => UnitBook.Items(slot).Where(i => i.Index > 0)
            .Select(i => new EquipmentReferenceRow(UnitBook.SlotName(slot), i)))
        .ToArray();

    // ------------------------------------------------------------------ save state
    private string _gameDirectory = "";
    public string GameDirectory { get => _gameDirectory; private set => SetField(ref _gameDirectory, value); }

    private SaveSet? _selectedSaveSet;
    public SaveSet? SelectedSaveSet
    {
        get => _selectedSaveSet;
        set { if (SetField(ref _selectedSaveSet, value)) RaiseCommands(); }
    }

    private bool _isSaveLoaded;
    public bool IsSaveLoaded
    {
        get => _isSaveLoaded;
        private set { if (SetField(ref _isSaveLoaded, value)) RaiseCommands(); }
    }

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        private set { if (SetField(ref _isDirty, value)) RaiseCommands(); }
    }

    private string _loadedLabel = "No save loaded.";
    public string LoadedLabel { get => _loadedLabel; private set => SetField(ref _loadedLabel, value); }

    private string _chronicle = "";
    public string Chronicle { get => _chronicle; private set => SetField(ref _chronicle, value); }

    public double Wealth
    {
        get => _kingdom?.Wealth ?? 0;
        set
        {
            if (_kingdom == null) return;
            _kingdom.Wealth = value;
            OnPropertyChanged(nameof(Wealth));
            MarkDirty($"treasury set to {_kingdom.Wealth:0.##} GP");
        }
    }

    public int Score
    {
        get => _kingdom?.Score ?? 0;
        set
        {
            if (_kingdom == null) return;
            _kingdom.Score = value;
            OnPropertyChanged(nameof(Score));
            MarkDirty($"score set to {_kingdom.Score}");
        }
    }

    public string SaveDate => _kingdom?.Date ?? "—";
    public string SaveIncome => _kingdom == null ? "—" : $"{_kingdom.Income:0.##} GP";
    public string SaveMaintenance => _kingdom == null ? "—" : $"{_kingdom.Maintenance:0.##} GP";
    public string ScoreOutOf => $"of {GameFacts.MaxScore}";
    public string PlayerSummary => _roster == null
        ? "—"
        : $"{_roster.Player.Name} the {_roster.Player.TypeName}, level {_roster.Player.Level}";

    private CityViewModel? _selectedCity;
    public CityViewModel? SelectedCity
    {
        get => _selectedCity;
        set
        {
            if (!SetField(ref _selectedCity, value)) return;
            value?.EnsureDevelopmentRows();
            RaiseCommands();
        }
    }

    private RosterSlotViewModel? _selectedSlot;
    public RosterSlotViewModel? SelectedSlot
    {
        get => _selectedSlot;
        set { if (SetField(ref _selectedSlot, value)) RaiseCommands(); }
    }

    private bool _showEmptySlots;
    /// <summary>Whether the roster grid lists the unused slots as well as the occupied ones.</summary>
    public bool ShowEmptySlots
    {
        get => _showEmptySlots;
        set { if (SetField(ref _showEmptySlots, value)) RebuildRosterRows(); }
    }

    private CityInfo? _teleportTarget;
    public CityInfo? TeleportTarget { get => _teleportTarget; set { SetField(ref _teleportTarget, value); RaiseCommands(); } }

    // ------------------------------------------------------------------ live state
    private ProcessEntry? _selectedProcess;
    public ProcessEntry? SelectedProcess
    {
        get => _selectedProcess;
        set { if (SetField(ref _selectedProcess, value)) RaiseCommands(); }
    }

    private ScanResultViewModel? _selectedResult;
    public ScanResultViewModel? SelectedResult
    {
        get => _selectedResult;
        set { if (SetField(ref _selectedResult, value)) RaiseCommands(); }
    }

    private FrozenValueViewModel? _selectedFrozen;
    public FrozenValueViewModel? SelectedFrozen
    {
        get => _selectedFrozen;
        set { if (SetField(ref _selectedFrozen, value)) RaiseCommands(); }
    }

    private ScanWidth _selectedWidth = ScanWidth.Int16;
    public ScanWidth SelectedWidth
    {
        get => _selectedWidth;
        set { if (SetField(ref _selectedWidth, value)) NewScan(); }
    }

    private string _scanText = "";
    public string ScanText { get => _scanText; set => SetField(ref _scanText, value); }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (!SetField(ref _isScanning, value)) return;
            OnPropertyChanged(nameof(NotScanning));
            RaiseCommands();
        }
    }

    public bool NotScanning => !_isScanning;
    public bool IsAttached => _mem is { IsOpen: true };
    public bool HasResults => _searcher is { HasMatches: true } || Results.Count > 0;
    public bool HasDgroup => _dgroup != null;

    private string _dgroupLabel = "Data segment not located.";
    public string DgroupLabel { get => _dgroupLabel; private set => SetField(ref _dgroupLabel, value); }

    private string _matchCount = "";
    public string MatchCount { get => _matchCount; private set => SetField(ref _matchCount, value); }

    private string _status =
        "Open a Sword of Aragon save (ARAGON.HS?) to edit it, or attach to DOSBox for live editing.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // -------------------------------------------------------------------- commands
    public ICommand BrowseSaveCommand { get; }
    public ICommand RefreshSavesCommand { get; }
    public ICommand LoadSaveCommand { get; }
    public ICommand SaveChangesCommand { get; }
    public ICommand DiscardChangesCommand { get; }
    public ICommand MaxWealthCommand { get; }
    public ICommand MaxScoreCommand { get; }
    public ICommand DevelopCityCommand { get; }
    public ICommand RestoreCityCommand { get; }
    public ICommand DevelopAllOwnedCommand { get; }
    public ICommand RestoreAllOwnedCommand { get; }
    public ICommand MaxLevelCommand { get; }
    public ICommand FillUnitCommand { get; }
    public ICommand RefillMoveCommand { get; }
    public ICommand EquipBestCommand { get; }
    public ICommand TeleportCommand { get; }
    public ICommand GatherArmyCommand { get; }

    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand LocateDgroupCommand { get; }
    public ICommand FindWealthCommand { get; }
    public ICommand FindCounterCommand { get; }
    public ICommand FirstScanCommand { get; }
    public ICommand NextScanCommand { get; }
    public ICommand NewScanCommand { get; }
    public ICommand PinCommand { get; }
    public ICommand RemoveFrozenCommand { get; }
    public ICommand FreezeAllCommand { get; }
    public ICommand FreezeNoneCommand { get; }

    public MainViewModel()
    {
        BrowseSaveCommand = new RelayCommand(_ => BrowseSave(), _ => !IsDirty);
        RefreshSavesCommand = new RelayCommand(_ => RefreshSaveSets(), _ => GameDirectory.Length > 0);
        LoadSaveCommand = new RelayCommand(_ => LoadSelectedSave(), _ => SelectedSaveSet != null && !IsDirty);
        SaveChangesCommand = new RelayCommand(_ => SaveChanges(), _ => IsSaveLoaded && IsDirty);
        DiscardChangesCommand = new RelayCommand(_ => DiscardChanges(), _ => IsSaveLoaded && IsDirty);
        MaxWealthCommand = new RelayCommand(_ => Wealth = GameFacts.MaxWealth, _ => IsSaveLoaded);
        MaxScoreCommand = new RelayCommand(_ => Score = GameFacts.MaxScore, _ => IsSaveLoaded);
        DevelopCityCommand = new RelayCommand(_ => SelectedCity?.DevelopToCeiling(), _ => SelectedCity != null);
        RestoreCityCommand = new RelayCommand(_ => SelectedCity?.RestoreMood(), _ => SelectedCity != null);
        DevelopAllOwnedCommand = new RelayCommand(_ => ForEachOwnedCity(c => c.DevelopToCeiling()), _ => IsSaveLoaded);
        RestoreAllOwnedCommand = new RelayCommand(_ => ForEachOwnedCity(c => c.RestoreMood()), _ => IsSaveLoaded);
        MaxLevelCommand = new RelayCommand(_ => SelectedSlot?.MaxLevel(), _ => SelectedSlot is { IsOccupied: true });
        FillUnitCommand = new RelayCommand(_ => SelectedSlot?.FillToStackingLimit(),
                                           _ => SelectedSlot is { IsOccupied: true, IsUnitSlot: true });
        RefillMoveCommand = new RelayCommand(_ => SelectedSlot?.RefillMovement(), _ => SelectedSlot is { IsOccupied: true });
        EquipBestCommand = new RelayCommand(_ => SelectedSlot?.EquipBest(), _ => SelectedSlot is { IsOccupied: true });
        TeleportCommand = new RelayCommand(_ => TeleportSelected(),
                                           _ => SelectedSlot is { IsOccupied: true } && TeleportTarget != null);
        GatherArmyCommand = new RelayCommand(_ => GatherArmy(),
            _ => IsSaveLoaded && TeleportTarget != null && RosterSlots.Any(r => r.IsOccupied));

        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        AttachCommand = new RelayCommand(_ => Attach(), _ => SelectedProcess != null && !IsAttached && !IsScanning);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);
        LocateDgroupCommand = new RelayCommand(_ => LocateDgroup(), _ => IsAttached && !IsScanning);
        FindWealthCommand = new RelayCommand(_ => FindWealth(), _ => HasDgroup && !IsScanning);
        FindCounterCommand = new RelayCommand(_ => FindCounter(), _ => HasDgroup && !IsScanning);
        FirstScanCommand = new RelayCommand(_ => FirstScan(), _ => IsAttached && !IsScanning);
        NextScanCommand = new RelayCommand(NextScan, _ => IsAttached && !IsScanning && _searcher is { HasMatches: true });
        NewScanCommand = new RelayCommand(_ => NewScan(), _ => IsAttached && !IsScanning);
        PinCommand = new RelayCommand(_ => PinSelected(), _ => SelectedResult != null && IsAttached);
        RemoveFrozenCommand = new RelayCommand(_ => RemoveFrozen(), _ => SelectedFrozen != null);
        FreezeAllCommand = new RelayCommand(_ => SetAllFrozen(true), _ => Frozen.Count > 0);
        FreezeNoneCommand = new RelayCommand(_ => SetAllFrozen(false), _ => Frozen.Count > 0);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _poll.Tick += (_, _) => PollTick();

        RefreshProcesses();
        TryAutoAttach();
    }

    /// <summary>On startup, attach automatically when the pre-selected process looks like a game emulator, so a running game is picked up without a manual click. Stays a no-op (just the populated process list) when nothing emulator-looking is running, rather than attaching to some unrelated process and scanning it fruitlessly.</summary>
    private void TryAutoAttach()
    {
        if (!IsAttached && SelectedProcess?.IsEmulator == true) Attach();
    }

    // ======================================================== save editor: loading
    private void BrowseSave()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open a Sword of Aragon kingdom save",
            Filter = "Sword of Aragon saves (ARAGON.HS*)|ARAGON.HS*|All files (*.*)|*.*",
            InitialDirectory = GameDirectory,
        };
        if (dialog.ShowDialog() != true) return;

        string? directory = Path.GetDirectoryName(dialog.FileName);
        if (string.IsNullOrEmpty(directory))
        {
            Status = "Could not determine the folder that save lives in.";
            return;
        }

        GameDirectory = directory;
        RefreshSaveSets();

        char letter = LetterFromPath(dialog.FileName);
        SelectedSaveSet = SaveSets.FirstOrDefault(s => s.Letter == letter) ?? SaveSets.FirstOrDefault();
        if (SelectedSaveSet != null) LoadSelectedSave();
    }

    /// <summary>The save letter a kingdom-file path encodes — the last character of its extension.</summary>
    private static char LetterFromPath(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Length > 0 ? char.ToUpperInvariant(extension[^1]) : '\0';
    }

    private void RefreshSaveSets()
    {
        var previous = SelectedSaveSet?.Letter;
        SaveSets.Clear();
        foreach (var set in SaveSet.Discover(GameDirectory)) SaveSets.Add(set);
        SelectedSaveSet = SaveSets.FirstOrDefault(s => s.Letter == previous) ?? SaveSets.FirstOrDefault();
        Status = SaveSets.Count == 0
            ? $"No ARAGON.HS? saves in '{GameDirectory}'."
            : $"Found {SaveSets.Count} save{(SaveSets.Count == 1 ? "" : "s")} in '{GameDirectory}'.";
        RaiseCommands();
    }

    private void LoadSelectedSave()
    {
        var set = SelectedSaveSet;
        if (set == null) return;

        try
        {
            var kingdom = KingdomFile.Load(set.KingdomPath);
            RosterFile? roster = null;
            if (File.Exists(set.RosterPath)) roster = RosterFile.Load(set.RosterPath);

            _kingdom = kingdom;
            _roster = roster;
            RebuildCityRows();
            RebuildRosterRows();
            Chronicle = set.ReadChronicle();
            IsSaveLoaded = true;
            IsDirty = false;
            LoadedLabel = $"Save {set.Letter} — {kingdom.Date}" +
                          (roster == null ? "  (no roster file: unit editing unavailable)" : "");
            RefreshGlobals();
            Status = roster == null
                ? $"Loaded save {set.Letter}. {GameFacts.RosterFileName(set.Letter)} is missing, so the " +
                  "Army tab is empty; kingdom editing still works."
                : $"Loaded save {set.Letter}: {PlayerSummary}, {kingdom.Wealth:0.##} GP, " +
                  $"{Cities.Count(c => c.IsPlayerOwned)} cities held.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _kingdom = null;
            _roster = null;
            Cities.Clear();
            RosterSlots.Clear();
            Chronicle = "";
            IsSaveLoaded = false;
            IsDirty = false;
            LoadedLabel = "No save loaded.";
            RefreshGlobals();
            Status = "Could not load that save: " + ex.Message;
        }
    }

    private void RebuildCityRows()
    {
        Cities.Clear();
        SelectedCity = null;
        if (_kingdom == null) return;
        foreach (var city in _kingdom.Cities) Cities.Add(new CityViewModel(city, this));
        SelectedCity = Cities.FirstOrDefault(c => c.IsPlayerOwned) ?? Cities.FirstOrDefault();
    }

    private void RebuildRosterRows()
    {
        int? previous = SelectedSlot?.Slot;
        RosterSlots.Clear();
        SelectedSlot = null;
        if (_roster == null) return;

        foreach (var record in _roster.Records)
        {
            if (!record.IsOccupied && !ShowEmptySlots) continue;
            RosterSlots.Add(new RosterSlotViewModel(_roster, record, this));
        }
        SelectedSlot = RosterSlots.FirstOrDefault(r => r.Slot == previous) ?? RosterSlots.FirstOrDefault();
    }

    private void RefreshGlobals()
    {
        foreach (var name in new[]
                 {
                     nameof(Wealth), nameof(Score), nameof(SaveDate), nameof(SaveIncome),
                     nameof(SaveMaintenance), nameof(PlayerSummary),
                 })
            OnPropertyChanged(name);
    }

    // ========================================================= save editor: writing
    private void SaveChanges()
    {
        if (_kingdom == null) return;
        try
        {
            // Save() returns the backup path only when it actually created one, so the status line can
            // say what is really recoverable instead of implying a rolling undo the .bak is not.
            var created = new[] { _kingdom.Save(), _roster?.Save() }
                .Where(p => p != null)
                .Select(p => Path.GetFileName(p!))
                .ToArray();
            IsDirty = false;
            Status = created.Length > 0
                ? $"Written. The state from before this trainer first touched the save is kept in " +
                  $"{string.Join(", ", created)}. Load this save letter from the game's Old Game menu " +
                  "to see the changes."
                : "Written. A .bak from an earlier trainer session already existed and was left alone, " +
                  "so it does not undo this edit. Load this save letter from the game's Old Game menu " +
                  "to see the changes.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = "Could not write the save: " + ex.Message;
        }
    }

    private void DiscardChanges()
    {
        IsDirty = false;         // cleared first so the reload guard lets us through
        LoadSelectedSave();
        Status = "Reloaded from disk; edits discarded.";
    }

    void IEditHost.MarkDirty(string what) => MarkDirty(what);

    void IEditHost.NotifyRosterRecomputed()
    {
        foreach (var slot in RosterSlots) slot.RefreshAll();
    }

    private void MarkDirty(string what)
    {
        IsDirty = true;
        RefreshGlobals();
        Status = what + " — click Save to write it to the file.";
    }

    private void ForEachOwnedCity(Action<CityViewModel> action)
    {
        var owned = Cities.Where(c => c.IsPlayerOwned).ToArray();
        if (owned.Length == 0)
        {
            Status = "No city in this save carries the \"changed this month\" figures that mark it as yours.";
            return;
        }
        foreach (var city in owned) action(city);
        Status = $"Applied to {owned.Length} of your cities — click Save to write it to the file.";
    }

    private void TeleportSelected()
    {
        var slot = SelectedSlot;
        var target = TeleportTarget;
        if (slot == null || target == null) return;
        slot.MoveTo(target.X, target.Y, $"{target.DisplayName} {target.Position}");
    }

    private void GatherArmy()
    {
        var target = TeleportTarget;
        if (target == null || _roster == null) return;
        int moved = 0;
        foreach (var slot in RosterSlots.Where(r => r.IsOccupied))
        {
            slot.MoveTo(target.X, target.Y, target.DisplayName);
            moved++;
        }
        Status = $"Moved {moved} commands and units to {target.DisplayName} {target.Position} — " +
                 "click Save to write it to the file.";
    }

    // ==================================================================== live: attach
    public void RefreshProcesses()
    {
        var previous = SelectedProcess?.Id;
        Processes.Clear();
        var list = new List<ProcessEntry>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                string name = p.ProcessName;
                bool emulator = GameSignatures.EmulatorHints
                    .Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));
                list.Add(new ProcessEntry(p.Id, name, emulator));
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
            finally { p.Dispose(); }
        }
        foreach (var entry in list.OrderByDescending(e => e.IsEmulator)
                                  .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            Processes.Add(entry);

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
            _targetPid = SelectedProcess.Id;
            _searcher = new MemorySearcher(_mem, SelectedWidth);
            _dgroup = null;
            DgroupLabel = "Data segment not located.";
            Results.Clear();
            OnPropertyChanged(nameof(IsAttached));
            OnPropertyChanged(nameof(HasDgroup));
            OnPropertyChanged(nameof(HasResults));
            RaiseCommands();
            _poll.Start();
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). " +
                     "With the game on the World Map, click \"Locate data segment\".";
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
        _mem?.Dispose();
        _mem = null;
        _searcher = null;
        _targetPid = 0;
        _dgroup = null;
        DgroupLabel = "Data segment not located.";
        Results.Clear();
        Frozen.Clear();
        SelectedResult = null;
        SelectedFrozen = null;
        MatchCount = "";
        OnPropertyChanged(nameof(IsAttached));
        OnPropertyChanged(nameof(HasDgroup));
        OnPropertyChanged(nameof(HasResults));
        RaiseCommands();
        Status = "Detached.";
    }

    // ============================================================ live: data segment
    private async void LocateDgroup()
    {
        var mem = _mem;
        if (mem == null || IsScanning) return;

        IsScanning = true;
        Status = "Scanning for ARAGON.EXE's data segment...";
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        try
        {
            var found = await Task.Run(() => DgroupLocator.Locate(mem, ct), ct);
            if (mem != _mem || ct.IsCancellationRequested) return;

            _dgroup = found;
            if (found == null)
            {
                DgroupLabel = "Data segment not located.";
                Status = "No match. The game must be running ARAGON.EXE (the World Map) — the front end " +
                         "and the battle module have their own data. If it still fails, use the " +
                         "whole-process scanner below.";
            }
            else
            {
                var location = found.Value;
                DgroupLabel = $"DS:0000 at 0x{(ulong)location.Base:X} " +
                              $"({location.ValidatorsMatched + 1}/{GameSignatures.WorldMapValidators.Count + 1} anchors)";
                Status = $"Data segment located at 0x{(ulong)location.Base:X}. " +
                         "Now read a number off the City Status screen and use a guided find.";
            }
        }
        catch (OperationCanceledException) { if (mem == _mem) Status = "Scan cancelled."; }
        catch (Exception ex) { if (mem == _mem) Status = "Scan error: " + ex.Message; }
        finally
        {
            IsScanning = false;
            OnPropertyChanged(nameof(HasDgroup));
            RaiseCommands();
        }
    }

    private async void FindWealth()
    {
        if (!ScanValue.TryParseDouble(ScanText, out double target))
        {
            Status = "Type the Wealth figure from the City Status screen, then click Find gold.";
            return;
        }
        await RunSegmentSearch("Wealth", PinKind.MbfSingle,
            (segment, segmentBase) => DgroupLocator.FindMbfNear(segment, segmentBase, target, WealthTolerance),
            $"gold within {WealthTolerance:0.#} of {target:0.##}");
    }

    private async void FindCounter()
    {
        if (!ScanValue.TryParse(ScanText, out long target) || target is < short.MinValue or > short.MaxValue)
        {
            Status = "Type a whole number the game shows (population, morale, recruits...), " +
                     "then click Find counter.";
            return;
        }
        await RunSegmentSearch("Counter", PinKind.Raw,
            (segment, segmentBase) => DgroupLocator.FindInt16(segment, segmentBase, (int)target),
            $"16-bit words equal to {target}");
    }

    private async Task RunSegmentSearch(string label, PinKind kind,
        Func<byte[], nuint, List<DgroupLocator.Candidate>> search, string description)
    {
        var mem = _mem;
        var location = _dgroup;
        if (mem == null || location == null || IsScanning) return;

        IsScanning = true;
        Status = $"Searching the data segment for {description}...";
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        try
        {
            var found = await Task.Run(() =>
            {
                var segment = DgroupLocator.ReadSegment(mem, location.Value);
                ct.ThrowIfCancellationRequested();
                return search(segment, location.Value.Base);
            }, ct);

            if (mem != _mem || ct.IsCancellationRequested) return;

            // A segment search does not go through the searcher, so reset it (at the width the Width
            // combo shows, keeping the two in step) rather than leaving stale candidates behind that
            // the Narrow buttons could then "refine" against a grid they no longer describe.
            _searcher = new MemorySearcher(mem, SelectedWidth);
            var candidateWidth = kind == PinKind.MbfSingle ? ScanWidth.Int32 : SegmentCounterWidth;
            Results.Clear();
            foreach (var candidate in found.Take(MaxResultRows))
                Results.Add(new ScanResultViewModel(candidate.Address, candidate.Value, candidateWidth,
                                                    kind, candidate.DsOffset, label));
            SelectedResult = Results.FirstOrDefault();
            _pendingPinLabel = label;
            _pendingPinKind = kind;

            MatchCount = found.Count > MaxResultRows
                ? $"{found.Count:N0} candidates (showing first {MaxResultRows:N0})"
                : $"{found.Count:N0} candidate{(found.Count == 1 ? "" : "s")}";
            Status = found.Count switch
            {
                0 => $"No {description} in the data segment. Check the number, or try the " +
                     "whole-process scanner — not every value the game shows lives in DGROUP.",
                1 => "One candidate — Pin it, then edit Target or tick Freeze.",
                _ => $"{found.Count} candidates. Change the value in-game and search again to tell them apart.",
            };
        }
        catch (OperationCanceledException) { if (mem == _mem) Status = "Search cancelled."; }
        catch (Exception ex) { if (mem == _mem) Status = "Search error: " + ex.Message; }
        finally
        {
            IsScanning = false;
            OnPropertyChanged(nameof(HasResults));
            RaiseCommands();
        }
    }

    // ======================================================= live: whole-process scan
    private async void FirstScan()
    {
        if (_searcher == null || IsScanning) return;
        bool hasValue = ScanValue.TryParse(ScanText, out long value);
        if (!hasValue && !string.IsNullOrWhiteSpace(ScanText))
        {
            Status = "Enter a whole number, or clear the box to scan for an unknown value.";
            return;
        }
        if (hasValue && !ScanValue.FitsWidth(value, SelectedWidth))
        {
            Status = $"{value} does not fit a {SelectedWidth} scan — pick a wider type.";
            return;
        }

        long stored = ScanValue.Canonicalize(value, SelectedWidth);
        var searcher = _searcher;
        _pendingPinKind = PinKind.Raw;
        _pendingPinLabel = "";
        await RunScan(hasValue ? $"First scan for {value}..." : "First scan (unknown value)...",
            ct =>
            {
                if (hasValue) searcher.FirstScanExact(stored, ct);
                else searcher.FirstScanUnknown(ct);
            });
    }

    private async void NextScan(object? parameter)
    {
        if (_searcher == null || IsScanning) return;
        if (parameter is not ScanCompare compare && !Enum.TryParse(parameter?.ToString(), out compare)) return;

        long value = 0;
        if (compare == ScanCompare.Exact)
        {
            if (!ScanValue.TryParse(ScanText, out value))
            {
                Status = "Enter a value for an Exact scan.";
                return;
            }
            if (!ScanValue.FitsWidth(value, SelectedWidth))
            {
                Status = $"{value} does not fit a {SelectedWidth} scan.";
                return;
            }
            value = ScanValue.Canonicalize(value, SelectedWidth);
        }

        var searcher = _searcher;
        await RunScan($"Narrowing ({compare})...", ct => searcher.NextScan(compare, value, ct));
    }

    private async Task RunScan(string message, Action<CancellationToken> work)
    {
        IsScanning = true;
        Status = message;
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        var searcher = _searcher!;
        var mem = _mem;
        try
        {
            await Task.Run(() => work(ct), ct);
            if (mem != _mem || searcher != _searcher || ct.IsCancellationRequested) return;
            PublishResults(searcher);
        }
        catch (OperationCanceledException) { if (mem == _mem) Status = "Scan cancelled."; }
        catch (Exception ex) { if (mem == _mem) Status = "Scan error: " + ex.Message; }
        finally
        {
            IsScanning = false;
            OnPropertyChanged(nameof(HasResults));
            RaiseCommands();
        }
    }

    private void PublishResults(MemorySearcher searcher)
    {
        int count = searcher.MatchCount;
        Results.Clear();
        if (count >= 0)
            foreach (var match in searcher.Take(MaxResultRows))
                Results.Add(new ScanResultViewModel(match.Address, match.Value, searcher.Width));

        string shown = count < 0
            ? "baseline captured — narrow with a comparison"
            : count > MaxResultRows ? $"{count:N0} matches (showing first {MaxResultRows:N0})"
            : $"{count:N0} match{(count == 1 ? "" : "es")}";
        MatchCount = shown + (searcher.Truncated ? " (coverage truncated)" : "");
        Status = $"Scan complete: {shown}.";
        SelectedResult = Results.FirstOrDefault();
    }

    private void NewScan()
    {
        _scanCts?.Cancel();
        _pendingPinLabel = "";
        _pendingPinKind = PinKind.Raw;
        if (_mem != null) _searcher = new MemorySearcher(_mem, SelectedWidth);
        Results.Clear();
        SelectedResult = null;
        MatchCount = "";
        OnPropertyChanged(nameof(HasResults));
        RaiseCommands();
        if (IsAttached) Status = $"New {SelectedWidth} scan. Enter a value and click First Scan.";
    }

    // ================================================================== live: pinning
    private void PinSelected()
    {
        var result = SelectedResult;
        if (result == null) return;
        if (Frozen.Any(f => f.Address == result.Address))
        {
            Status = $"{result.AddressHex} is already pinned.";
            return;
        }

        // The pin inherits the candidate's own width and encoding, never the Width combo's — the combo
        // can have moved since the scan, and writing a 16-bit counter as 32 bits would overwrite the
        // variable next to it.
        var kind = result.Kind != PinKind.Raw ? result.Kind : _pendingPinKind;
        string label = result.Label.Length > 0 ? result.Label : _pendingPinLabel;
        Frozen.Add(new FrozenValueViewModel(this, result.Address, result.Width, result.Value, kind, label,
                                            result.DsOffset));
        RaiseCommands();
        Status = $"Pinned {result.AddressHex}. Edit Target to poke a value, or tick Freeze to hold it.";
    }

    private void RemoveFrozen()
    {
        if (SelectedFrozen == null) return;
        Frozen.Remove(SelectedFrozen);
        SelectedFrozen = null;
        RaiseCommands();
    }

    private void SetAllFrozen(bool frozen)
    {
        foreach (var pin in Frozen) pin.Frozen = frozen;
        Status = frozen ? "All pinned values frozen." : "Freeze cleared.";
    }

    // ==================================================================== poll loop
    private void PollTick()
    {
        if (_mem == null) return;
        if (!_mem.IsOpen || HasTargetExited())
        {
            Detach();
            Status = "Target process exited.";
            return;
        }

        foreach (var pin in Frozen)
        {
            pin.ApplyFreeze();
            if (ReadAt(pin.Address, pin.Width, out long raw))
                pin.RefreshLive(ScanResultViewModel.Decode(raw, pin.Kind, pin.Width));
        }

        if (!IsScanning && Results.Count > 0 && Results.Count <= LiveRefreshThreshold)
            foreach (var result in Results)
                if (ReadAt(result.Address, result.Width, out long raw))
                    result.RefreshLive(ScanResultViewModel.Decode(raw, result.Kind, result.Width));
    }

    private bool HasTargetExited()
    {
        if (_targetPid == 0) return false;
        try
        {
            using var process = Process.GetProcessById(_targetPid);
            return process.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    // ==================================================================== IScanHost
    private bool ReadAt(nuint address, ScanWidth width, out long value)
    {
        value = 0;
        var mem = _mem;
        if (mem is not { IsOpen: true }) return false;
        int w = (int)width;
        if (mem.Read(address, _ioBuf, w) < w) return false;
        long result = 0;
        for (int i = 0; i < w; i++) result |= (long)_ioBuf[i] << (8 * i);
        value = result;
        return true;
    }

    private bool WriteAt(nuint address, long value, ScanWidth width)
    {
        var mem = _mem;
        if (mem is not { IsOpen: true }) return false;
        int w = (int)width;
        ulong v = unchecked((ulong)value);
        for (int i = 0; i < w; i++) { _ioBuf[i] = (byte)(v & 0xFF); v >>= 8; }
        return mem.WriteRange(address, _ioBuf, 0, w);
    }

    bool IScanHost.Read(nuint address, ScanWidth width, out long value) => ReadAt(address, width, out value);
    bool IScanHost.Write(nuint address, long value, ScanWidth width) => WriteAt(address, value, width);

    bool IScanHost.WriteBytes(nuint address, byte[] bytes)
    {
        var mem = _mem;
        return mem is { IsOpen: true } && mem.Write(address, bytes);
    }

    void IScanHost.ReportWriteFailure(nuint address) =>
        Status = $"Write failed at 0x{(ulong)address:X} — the value was not applied.";

    // ===================================================================== plumbing
    private void RaiseCommands()
    {
        foreach (var command in new[]
                 {
                     BrowseSaveCommand, RefreshSavesCommand, LoadSaveCommand, SaveChangesCommand,
                     DiscardChangesCommand, MaxWealthCommand, MaxScoreCommand, DevelopCityCommand,
                     RestoreCityCommand, DevelopAllOwnedCommand, RestoreAllOwnedCommand, MaxLevelCommand,
                     FillUnitCommand, RefillMoveCommand, EquipBestCommand, TeleportCommand,
                     GatherArmyCommand, RefreshProcessesCommand, AttachCommand, DetachCommand,
                     LocateDgroupCommand, FindWealthCommand, FindCounterCommand, FirstScanCommand,
                     NextScanCommand, NewScanCommand, PinCommand, RemoveFrozenCommand, FreezeAllCommand,
                     FreezeNoneCommand,
                 })
            (command as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _mem?.Dispose();
    }
}

/// <summary>One row of the equipment reference table.</summary>
public sealed record EquipmentReferenceRow(string Slot, EquipmentItem Item)
{
    public string Name => Item.Name;
    public int Buy => Item.Buy;
    public int Train => Item.Train;
    public double Maint => Item.MaintTenths / 10.0;
    public int Weight => Item.Weight;
    public string Level => Item.MinLevel > 0 ? Item.MinLevel.ToString() : "—";
}
