using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using MightAndMagic1Trainer.Game;
using MightAndMagic1Trainer.Memory;

namespace MightAndMagic1Trainer.ViewModels;

public sealed class ProcessInfo
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Display => $"{Name}  (pid {Id})";
    private static readonly string[] EmulatorHints =
        { "dosbox", "scummvm", "pcem", "86box", "qemu", "vdosplus", "dosemu" };
    public bool IsLikelyEmulator =>
        EmulatorHints.Any(h => Name.Contains(h, StringComparison.OrdinalIgnoreCase));
}

public sealed class MainViewModel : ObservableObject
{
    private ProcessMemory? _mem;
    // Written on the UI thread (locate result / Detach) and read from the auto-fight loop's
    // background thread — volatile so the writes are visible across threads without a lock.
    private volatile DataSegment? _dataSeg;
    private readonly DispatcherTimer _timer;

    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(RefreshProcesses);
        AttachCommand = new RelayCommand(Attach, () => SelectedProcess != null && !IsAttached);
        DetachCommand = new RelayCommand(Detach, () => IsAttached);
        RescanCommand = new RelayCommand(Rescan, () => IsAttached);
        RefreshSelectedCommand = new RelayCommand(() => SelectedCharacter?.PullFromMemory(), () => SelectedCharacter?.IsLive == true);
        RefreshAllCommand = new RelayCommand(RefreshAll, () => IsAttached);
        WriteSelectedCommand = new RelayCommand(() => SelectedCharacter?.PushAll(), () => SelectedCharacter?.IsLive == true);
        MaxHpAllCommand = new RelayCommand(() => ForEachChar(c => c.MaxHp()), () => Characters.Count > 0);
        MaxSpAllCommand = new RelayCommand(() => ForEachChar(c => c.MaxSp()), () => Characters.Count > 0);
        MaxStatsAllCommand = new RelayCommand(() => ForEachChar(c => c.MaxStats()), () => Characters.Count > 0);
        MaxResistancesAllCommand = new RelayCommand(() => ForEachChar(c => c.MaxResistances()), () => Characters.Count > 0);
        MaxEverythingSelectedCommand = new RelayCommand(() => SelectedCharacter?.MaxEverything(), () => SelectedCharacter != null);
        MaxResistancesSelectedCommand = new RelayCommand(() => SelectedCharacter?.MaxResistances(), () => SelectedCharacter != null);
        MaxEverythingAllCommand = new RelayCommand(() => ForEachChar(c => c.MaxEverything()), () => Characters.Count > 0);
        LoadRosterFileCommand = new RelayCommand(p => LoadRosterFile(p as string));
        SaveRosterFileCommand = new RelayCommand(SaveRosterFile, CanSaveRosterFile);
        SaveSnapshotCommand = new RelayCommand(p => SaveSnapshot(p as string), _ => Characters.Count > 0);
        LoadSnapshotCommand = new RelayCommand(p => LoadSnapshot(p as string), _ => Characters.Count > 0);
        CopyToTargetCommand = new RelayCommand(CopyToTarget, CanUseSlotTarget);
        SwapWithTargetCommand = new RelayCommand(SwapWithTarget, CanUseSlotTarget);

        SpellMacros = new SpellMacrosViewModel(
            () => IsAttached ? SelectedProcess?.Id : null,
            s => Status = s);
        MemorySearch = new MemorySearchViewModel(() => _mem, s => Status = s);
        PairSearch = new PairSearchViewModel(() => _mem, s => Status = s);
        MemoryDump = new MemoryDumpViewModel(() => _mem, s => Status = s);
        DumpDiff = new DumpDiffViewModel(s => Status = s);
        MapReference = new MapReferenceViewModel(
            () => _mem,
            () => PairSearch.LockedAddress,   // the live marker follows the X/Y search's lone survivor
            s => Status = s);
        DrawnMap = new DrawnMapViewModel(
            () => _mem,
            () => PairSearch.LockedAddress,   // same position lock, on a map drawn from Mazedata.dta
            () => _dataSeg,                   // for auto current-map detection + learned position offset
            s => Status = s);
        AutoCombat = new AutoCombatViewModel(
            () => IsAttached ? SelectedProcess?.Id : null,
            () => _dataSeg,
            s => Status = s);
        RollPredictor = new RollPredictorViewModel(() => _dataSeg);
        Roller = new CharacterRollerViewModel(
            () => _mem,
            () => IsAttached ? SelectedProcess?.Id : null,
            s => Status = s);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        RefreshProcesses();
        TryAutoAttach();
    }

    /// <summary>On startup, attach automatically when the pre-selected process looks like a game
    /// emulator, so a running game is picked up without a manual click. Stays a no-op (just the
    /// populated process list) when nothing emulator-looking is running, rather than attaching to
    /// some unrelated process and scanning it fruitlessly.</summary>
    private void TryAutoAttach()
    {
        if (!IsAttached && SelectedProcess?.IsLikelyEmulator == true)
            Attach();
    }

    /// <summary>Quick-cast key macros (sends keystrokes to the attached game window).</summary>
    public SpellMacrosViewModel SpellMacros { get; }

    /// <summary>Cheat-Engine-style value scanner for finding/editing off-roster state (e.g. position).</summary>
    public MemorySearchViewModel MemorySearch { get; }

    /// <summary>Byte-pattern scanner for an X/Y pair (two adjacent bytes, e.g. map North/East).</summary>
    public PairSearchViewModel PairSearch { get; }

    /// <summary>Dumps the attached process's whole memory to a file (plus a region index).</summary>
    public MemoryDumpViewModel MemoryDump { get; }

    /// <summary>Compares two saved dumps and lists the changed bytes by process address.</summary>
    public DumpDiffViewModel DumpDiff { get; }

    /// <summary>Auto-rolls a new character on the CREATE NEW CHARACTERS screen until a target roll is hit.</summary>
    public CharacterRollerViewModel Roller { get; }

    /// <summary>Read-only spell tables shown in the Spells reference tab.</summary>
    public SpellReferenceViewModel SpellReference { get; } = new();

    /// <summary>Bundled area maps shown in the Maps tab, plus the live party marker / teleport.</summary>
    public MapReferenceViewModel MapReference { get; }

    /// <summary>Maps drawn from the decoded Mazedata.dta, with the live party cell and click-teleport.</summary>
    public DrawnMapViewModel DrawnMap { get; }

    /// <summary>Auto-fight: while combat is detected, replays a key sequence into the game.</summary>
    public AutoCombatViewModel AutoCombat { get; }

    /// <summary>Live roll predictor: reads the RNG (LFSR) state and predicts upcoming dice rolls.</summary>
    public RollPredictorViewModel RollPredictor { get; }

    /// <summary>Read-only item &amp; equipment reference shown in the Items tab.</summary>
    public ItemReferenceViewModel ItemReference { get; } = new();

    /// <summary>Read-only bestiary (the game's full monster table) shown in the Monsters tab.</summary>
    public MonsterReferenceViewModel MonsterReference { get; } = new();

    /// <summary>Read-only class reference (min stats, HP/level, XP table) shown in the Classes tab.</summary>
    public ClassReferenceViewModel ClassReference { get; } = new();

    /// <summary>Read-only solution walkthrough shown in the Walkthrough tab.</summary>
    public WalkthroughViewModel Walkthrough { get; } = new();

    // --- process list -----------------------------------------------------------
    public ObservableCollection<ProcessInfo> Processes { get; } = new();

    private ProcessInfo? _selectedProcess;
    public ProcessInfo? SelectedProcess
    {
        get => _selectedProcess;
        set { if (SetField(ref _selectedProcess, value)) RaiseCommands(); }
    }

    public ObservableCollection<RosterLocation> Locations { get; } = new();

    private RosterLocation? _selectedLocation;
    public RosterLocation? SelectedLocation
    {
        get => _selectedLocation;
        set { if (SetField(ref _selectedLocation, value) && value != null) BuildCharacters(value.Value); }
    }

    public ObservableCollection<CharacterViewModel> Characters { get; } = new();

    private CharacterViewModel? _selectedCharacter;
    public CharacterViewModel? SelectedCharacter
    {
        get => _selectedCharacter;
        set
        {
            if (SetField(ref _selectedCharacter, value))
            {
                SpellMacros.SetCaster(value);   // spellbook casts as the selected character
                RaiseCommands();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(HasNoSelection));
            }
        }
    }

    public bool HasSelection => _selectedCharacter != null;
    public bool HasNoSelection => _selectedCharacter == null;

    private bool _isAttached;
    public bool IsAttached
    {
        get => _isAttached;
        private set { if (SetField(ref _isAttached, value)) RaiseCommands(); }
    }

    private bool _liveRefresh;
    public bool LiveRefresh
    {
        get => _liveRefresh;
        set => SetField(ref _liveRefresh, value);
    }

    // Periodically re-read the whole party from the game so the displayed values track what
    // happens in-game (gold spent, HP lost, etc.) without a manual "Re-read" click. Defaults
    // on; the freeze timer (150 ms) drives it via a tick counter so there's no second timer.
    private bool _autoReadParty = true;
    public bool AutoReadParty
    {
        get => _autoReadParty;
        set => SetField(ref _autoReadParty, value);
    }

    // Ticks between automatic party re-reads (150 ms timer → ~2 s). Small enough to feel live,
    // large enough not to fight a value the user is mid-edit on a LostFocus-bound field.
    private const int AutoReadPartyTicks = 13;
    private int _autoReadCounter;

    // --- party-wide freeze toggles ----------------------------------------------
    // Setting one of these propagates to every character's own freeze flag; newly
    // built characters (after a scan or file load) inherit the current state.
    // Party-wide freeze toggles default ON; newly built/loaded characters inherit
    // this state via ApplyFreezeStateToParty, so a fresh attach is frozen out of the box.
    private bool _freezeHpAll = true;
    public bool FreezeHpAll
    {
        get => _freezeHpAll;
        set { if (SetField(ref _freezeHpAll, value)) ForEachChar(c => c.FreezeHp = value); }
    }

    private bool _freezeSpAll = true;
    public bool FreezeSpAll
    {
        get => _freezeSpAll;
        set { if (SetField(ref _freezeSpAll, value)) ForEachChar(c => c.FreezeSp = value); }
    }

    private bool _freezeConditionAll = true;
    public bool FreezeConditionAll
    {
        get => _freezeConditionAll;
        set { if (SetField(ref _freezeConditionAll, value)) ForEachChar(c => c.FreezeCondition = value); }
    }

    // "No-loss" party toggles: the party can still gain gold/gems/food, but never lose any.
    private bool _freezeGoldAll = true;
    public bool FreezeGoldAll
    {
        get => _freezeGoldAll;
        set { if (SetField(ref _freezeGoldAll, value)) ForEachChar(c => c.FreezeGold = value); }
    }

    private bool _freezeGemsAll = true;
    public bool FreezeGemsAll
    {
        get => _freezeGemsAll;
        set { if (SetField(ref _freezeGemsAll, value)) ForEachChar(c => c.FreezeGems = value); }
    }

    private bool _freezeFoodAll = true;
    public bool FreezeFoodAll
    {
        get => _freezeFoodAll;
        set { if (SetField(ref _freezeFoodAll, value)) ForEachChar(c => c.FreezeFood = value); }
    }

    // Item-charge freeze defaults ON (like HP/SP/wealth) so charged items (wands, rings, etc.)
    // are never used up out of the box.
    private bool _freezeChargesAll = true;
    public bool FreezeChargesAll
    {
        get => _freezeChargesAll;
        set { if (SetField(ref _freezeChargesAll, value)) ForEachChar(c => c.FreezeAllCharges = value); }
    }

    private string _status = "Not attached. Launch the game, then pick its process and Attach.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // Live game state read by exact DS offsets once the data segment is located (the "supercharge":
    // precise reads instead of memory scans). Empty until attached + located.
    private string _gameStateText = "";
    public string GameStateText { get => _gameStateText; private set => SetField(ref _gameStateText, value); }

    private double _scanProgress;
    public double ScanProgress { get => _scanProgress; set => SetField(ref _scanProgress, value); }

    // --- commands ---------------------------------------------------------------
    public RelayCommand RefreshProcessesCommand { get; }
    public RelayCommand AttachCommand { get; }
    public RelayCommand DetachCommand { get; }
    public RelayCommand RescanCommand { get; }
    public RelayCommand RefreshSelectedCommand { get; }
    public RelayCommand RefreshAllCommand { get; }
    public RelayCommand WriteSelectedCommand { get; }
    public RelayCommand MaxHpAllCommand { get; }
    public RelayCommand MaxSpAllCommand { get; }
    public RelayCommand MaxStatsAllCommand { get; }
    public RelayCommand MaxResistancesAllCommand { get; }
    public RelayCommand MaxEverythingSelectedCommand { get; }
    public RelayCommand MaxResistancesSelectedCommand { get; }
    public RelayCommand MaxEverythingAllCommand { get; }
    public RelayCommand LoadRosterFileCommand { get; }
    public RelayCommand SaveRosterFileCommand { get; }
    public RelayCommand SaveSnapshotCommand { get; }
    public RelayCommand LoadSnapshotCommand { get; }
    public RelayCommand CopyToTargetCommand { get; }
    public RelayCommand SwapWithTargetCommand { get; }

    private void RefreshProcesses()
    {
        var current = SelectedProcess?.Id;
        Processes.Clear();
        var list = Process.GetProcesses()
            .Select(p =>
            {
                try { return new ProcessInfo { Id = p.Id, Name = p.ProcessName }; }
                catch { return null; }
                finally { p.Dispose(); }
            })
            .Where(p => p != null)!
            .Cast<ProcessInfo>()
            .OrderByDescending(p => p.IsLikelyEmulator)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var p in list) Processes.Add(p);
        SelectedProcess = Processes.FirstOrDefault(p => p.Id == current)
                          ?? Processes.FirstOrDefault(p => p.IsLikelyEmulator)
                          ?? Processes.FirstOrDefault();
        Status = $"Found {Processes.Count} processes. Emulator-looking ones are listed first.";
    }

    private async void Attach()
    {
        if (SelectedProcess == null) return;
        try
        {
            Detach();   // cancels any in-flight scan and releases the old handle
            _mem = ProcessMemory.Open(SelectedProcess.Id);
            IsAttached = true;
            Roller.RefreshCommands();   // the roller can act now that we're attached
            Status = $"Attached to {SelectedProcess.Display}. Scanning for the roster…";
            await ScanAsync();
        }
        catch (Exception ex)
        {
            Status = "Attach failed: " + ex.Message;
            Detach();
        }
    }

    private void Detach()
    {
        AutoCombat.Stop();   // stop replaying keys into a process we're about to drop
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;
        Characters.Clear();
        Locations.Clear();
        SelectedCharacter = null;
        SelectedLocation = null;
        MemorySearch.Reset();    // candidate addresses are meaningless once detached
        PairSearch.Reset();      // ditto for the X/Y search candidates
        Roller.Reset();          // the locked roll address belonged to the old process
        MemoryDump.CancelDump(); // an in-flight dump stops at its next chunk and cleans up

        // Disposing immediately is safe even with pool-thread readers (dump, roster scan,
        // memory searches) mid-flight: ProcessMemory holds a SafeProcessHandle, so an in-flight
        // read completes on the live handle and later reads fail benignly instead of touching
        // a freed (or OS-recycled) handle.
        _mem?.Dispose();
        _mem = null;
        _dataSeg = null;
        GameStateText = "";
        IsAttached = false;
        ScanProgress = 0;
    }

    private async void Rescan() => await ScanAsync();

    private CancellationTokenSource? _scanCts;

    private async Task ScanAsync()
    {
        if (_mem == null) return;
        var mem = _mem;

        // Cancel a previous scan and start a fresh, cancellable one. Capturing the
        // token locally means a Detach (which cancels + nulls _scanCts) can't leave
        // this scan writing results against a disposed ProcessMemory.
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        var progress = new Progress<double>(v => { if (!ct.IsCancellationRequested) ScanProgress = v; });
        Status = "Scanning process memory for the character roster…";
        List<RosterLocation> found;
        try
        {
            found = await Task.Run(() => RosterLocator.FindAll(mem, progress, ct), ct);
        }
        catch (OperationCanceledException)
        {
            return;   // superseded by a newer scan or a detach
        }
        catch (Exception ex)
        {
            Status = "Scan error: " + ex.Message;
            return;
        }
        if (ct.IsCancellationRequested || !ReferenceEquals(mem, _mem)) return;
        ScanProgress = 1;
        Locations.Clear();
        foreach (var loc in found) Locations.Add(loc);
        if (found.Count == 0)
        {
            Status = "No roster found. Make sure the game is loaded to the main game (past the title) and try Re-scan.";
            return;
        }
        Status = $"Found {found.Count} candidate location(s). Showing the best match ({found[0].RecordCount} records).";
        SelectedLocation = found[0];   // triggers BuildCharacters

        // Supercharge: locate the data segment so game state can be read by exact DS offset.
        _ = LocateDataSegmentAsync(mem, ct);
    }

    // Find the data segment's base in the attached process by string anchor (see DataSegment).
    // Runs off the UI thread; a detach (which nulls _mem) makes the result a benign no-op.
    private async Task LocateDataSegmentAsync(ProcessMemory mem, CancellationToken ct)
    {
        try
        {
            var ds = await Task.Run(() => DataSegment.Locate(mem, ct), ct);
            if (ct.IsCancellationRequested || !ReferenceEquals(mem, _mem)) return;
            _dataSeg = ds;
            GameStateText = ds == null
                ? "Data segment not located (couldn't anchor on the game's strings)."
                : $"🎯 Game state located @ DS 0x{(ulong)ds.BaseAddress:X}.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { GameStateText = "Data segment locate failed: " + ex.Message; }
    }

    // Refresh the live game-state readout from exact DS offsets (cheap; runs on the timer).
    private void UpdateGameState()
    {
        if (_dataSeg is not { } ds) return;
        try
        {
            var rec = ds.ReadCurrentCharacterRecord();
            string who = rec != null ? DecodeName(rec) : "(none)";
            GameStateText = $"🎯 DS 0x{(ulong)ds.BaseAddress:X} · in-play character: {who} · "
                          + $"in combat: {(ds.InCombat ? "yes" : "no")}";
        }
        catch { /* transient read failure; keep the last text */ }
    }

    private static string DecodeName(byte[] rec)
    {
        int n = 0;
        while (n < RosterFormat.NameLength && rec[n] != 0) n++;
        return n == 0 ? "(none)" : System.Text.Encoding.ASCII.GetString(rec, 0, n);
    }

    private void BuildCharacters(RosterLocation loc)
    {
        Characters.Clear();
        SelectedCharacter = null;
        SlotTarget = null;
        _rosterFilePath = null;    // the characters are the live game's now, not the file's
        _rosterFileBytes = null;
        if (_mem == null) return;

        for (int slot = 0; slot < loc.RecordCount; slot++)
        {
            nuint addr = loc.BaseAddress + (nuint)(slot * RosterFormat.MemoryStride);
            var buf = _mem.Read(addr, RosterFormat.RecordSize);
            if (buf.Length < RosterFormat.RecordSize) break;
            var rec = new CharacterRecord(buf) { Address = addr, Slot = slot };
            Characters.Add(new CharacterViewModel(rec, _mem));
        }
        ApplyFreezeStateToParty();
        SelectedCharacter = Characters.FirstOrDefault();
        RaiseCommands();
    }

    /// <summary>Pushes the party-wide freeze toggles onto every current character.</summary>
    private void ApplyFreezeStateToParty() =>
        ForEachChar(c =>
        {
            c.FreezeHp = _freezeHpAll; c.FreezeSp = _freezeSpAll;
            c.FreezeCondition = _freezeConditionAll;
            c.FreezeGold = _freezeGoldAll; c.FreezeGems = _freezeGemsAll; c.FreezeFood = _freezeFoodAll;
            c.FreezeAllCharges = _freezeChargesAll;
        });

    // --- offline roster file editing ---------------------------------------------
    // When a Roster.dta is browsed in, the path and the original file bytes are kept so the
    // edited records can be written back: the save patches each character's record into a
    // copy of the original bytes, leaving any unrecognised slots untouched. Attaching to a
    // live game replaces the characters, so file mode ends there.
    private string? _rosterFilePath;
    private byte[]? _rosterFileBytes;

    private void LoadRosterFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            var bytes = File.ReadAllBytes(path);
            Characters.Clear();
            SelectedCharacter = null;
            for (int slot = 0; slot < RosterFormat.MaxSlots; slot++)
            {
                int off = slot * RosterFormat.FileStride;
                if (off + RosterFormat.RecordSize > bytes.Length) break;
                if (!RosterFormat.LooksLikeRecord(bytes, off)) continue;
                var rec = new CharacterRecord(bytes.AsSpan(off, RosterFormat.RecordSize)) { Slot = slot };
                Characters.Add(new CharacterViewModel(rec, null));   // file-only (writes go to Save)
            }
            _rosterFilePath = path;
            _rosterFileBytes = bytes;
            ApplyFreezeStateToParty();
            SelectedCharacter = Characters.FirstOrDefault();
            Status = $"Loaded {Characters.Count} character(s) from file. Edits stay in the trainer until you click 💾 Save roster file.";
            RaiseCommands();
        }
        catch (Exception ex)
        {
            Status = "Failed to read roster file: " + ex.Message;
        }
    }

    // Save is offered only while the loaded characters are actually the file's (file-only
    // view models); after an attach the same collection holds live characters instead.
    private bool CanSaveRosterFile() =>
        _rosterFilePath != null && _rosterFileBytes != null
        && Characters.Count > 0 && Characters.All(c => !c.IsLive);

    private void SaveRosterFile()
    {
        if (_rosterFilePath is not { } path || _rosterFileBytes is not { } original) return;
        try
        {
            // One-step undo: the file as it was before this save lands in "<file>.bak".
            if (File.Exists(path))
                File.Copy(path, path + ".bak", overwrite: true);

            var bytes = (byte[])original.Clone();
            foreach (var c in Characters)
            {
                int off = c.Record.Slot * RosterFormat.FileStride;
                if (off + RosterFormat.RecordSize > bytes.Length) continue;   // loaded within bounds, so never expected
                Array.Copy(c.Record.Raw, 0, bytes, off, RosterFormat.RecordSize);
            }
            File.WriteAllBytes(path, bytes);
            _rosterFileBytes = bytes;   // subsequent saves diff against what's now on disk
            Status = $"Saved {Characters.Count} character(s) to {Path.GetFileName(path)} (previous version kept as .bak).";
        }
        catch (Exception ex)
        {
            Status = "Failed to save roster file: " + ex.Message;
        }
    }

    // --- party snapshots ----------------------------------------------------------
    // A snapshot is a roster-format file of the current party (live or offline), restorable
    // later by slot. Restoring writes straight through the normal record path, so live
    // characters are pushed to the game immediately.
    private void SaveSnapshot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || Characters.Count == 0) return;
        try
        {
            File.WriteAllBytes(path, PartySnapshot.Build(Characters.Select(c => c.Record)));
            Status = $"Snapshot of {Characters.Count} character(s) saved to {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            Status = "Failed to save snapshot: " + ex.Message;
        }
    }

    private void LoadSnapshot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            var slots = PartySnapshot.Read(File.ReadAllBytes(path));
            int restored = 0, skipped = 0;
            foreach (var (slot, record) in slots)
            {
                var target = Characters.FirstOrDefault(c => c.Record.Slot == slot);
                if (target == null) { skipped++; continue; }   // no such character any more
                // Normalise the record's stored slot byte to its new home, exactly as Copy/Swap do,
                // so a hand-edited or cross-arranged snapshot can't leave a record claiming a slot it
                // isn't in. For a snapshot built from a normal party this is a no-op.
                target.Record.Load(WithSlotByte(record, slot));
                target.PushAll();      // live characters are written to the game right away
                target.RaiseAll();
                restored++;
            }
            string skippedNote = skipped > 0 ? $" ({skipped} snapshot slot(s) had no matching character and were skipped)" : "";
            Status = restored == 0 && skipped == 0
                ? "The snapshot contains no characters."
                : $"Restored {restored} character(s) from {Path.GetFileName(path)}.{skippedNote}";
            if (restored > 0 && Characters.All(c => !c.IsLive))
                Status += " Click 💾 Save roster file to write them to the file.";
        }
        catch (Exception ex)
        {
            Status = "Failed to restore snapshot: " + ex.Message;
        }
    }

    // --- slot tools (copy / swap between roster slots) -----------------------------
    private CharacterViewModel? _slotTarget;
    /// <summary>The "other" character that Copy onto / Swap with act against.</summary>
    public CharacterViewModel? SlotTarget
    {
        get => _slotTarget;
        set { if (SetField(ref _slotTarget, value)) RaiseCommands(); }
    }

    private bool CanUseSlotTarget() =>
        SelectedCharacter != null && SlotTarget != null && !ReferenceEquals(SelectedCharacter, SlotTarget);

    /// <summary>Overwrites the target's record with a copy of the selected character's
    /// (the slot-index byte is fixed up so the copy belongs to its new slot).</summary>
    private void CopyToTarget()
    {
        if (SelectedCharacter is not { } src || SlotTarget is not { } dst || ReferenceEquals(src, dst)) return;
        dst.Record.Load(WithSlotByte(src.Record.ToArray(), dst.Record.Slot));
        dst.PushAll();
        dst.RaiseAll();
        Status = $"Copied {src.Record.Name} onto slot {dst.Record.Slot + 1} ({dst.Record.Name}).";
    }

    /// <summary>Swaps the selected character's record with the target's, slot bytes fixed up.</summary>
    private void SwapWithTarget()
    {
        if (SelectedCharacter is not { } a || SlotTarget is not { } b || ReferenceEquals(a, b)) return;
        var aBytes = a.Record.ToArray();
        var bBytes = b.Record.ToArray();
        a.Record.Load(WithSlotByte(bBytes, a.Record.Slot));
        b.Record.Load(WithSlotByte(aBytes, b.Record.Slot));
        a.PushAll(); b.PushAll();
        a.RaiseAll(); b.RaiseAll();
        Status = $"Swapped slots {a.Record.Slot + 1} and {b.Record.Slot + 1} ({b.Record.Name} ⇄ {a.Record.Name}).";
    }

    private static byte[] WithSlotByte(byte[] record, int slot)
    {
        record[RosterFormat.OffSlotIndex] = (byte)slot;
        return record;
    }

    // --- global hotkey entry points (invoked by MainWindow's RegisterHotKey hook) ---
    /// <summary>Ctrl+F1: toggles the party-wide HP/SP/condition freezes ("god mode") as one switch.</summary>
    public void ToggleGodModeHotkey()
    {
        bool on = !(FreezeHpAll && FreezeSpAll && FreezeConditionAll);
        FreezeHpAll = on;
        FreezeSpAll = on;
        FreezeConditionAll = on;
        Status = on ? "God mode ON (HP, SP and condition frozen for the whole party)."
                    : "God mode OFF (HP/SP/condition freezes released).";
    }

    /// <summary>Ctrl+F2: one-shot heal — current HP/SP to max and condition cleared, party-wide.</summary>
    public void HealPartyHotkey()
    {
        if (Characters.Count == 0) { Status = "Heal hotkey: no party loaded."; return; }
        ForEachChar(c => c.Heal());
        Status = "Party healed (HP/SP refilled, conditions cleared).";
    }

    /// <summary>Ctrl+F3: ★ Max EVERYTHING for the whole party.</summary>
    public void MaxEverythingHotkey()
    {
        if (Characters.Count == 0) { Status = "Max hotkey: no party loaded."; return; }
        ForEachChar(c => c.MaxEverything());
        Status = "★ Maxed EVERYTHING for the whole party (hotkey).";
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        // Runs even when detached: it tracks the position lock itself (and hides the
        // marker once the lock is gone), and is a no-op two-byte read otherwise.
        MapReference.Tick();
        DrawnMap.Tick();
        RollPredictor.Tick();   // self-clears its readout when the data segment is gone (like the ticks above)

        if (!IsAttached) return;
        if (_dataSeg != null) UpdateGameState();
        // Each freeze reads live memory itself, so it works for the whole party regardless of
        // which character is selected or whether live refresh is on.
        foreach (var c in Characters) c.ApplyFreezes();
        if (LiveRefresh && SelectedCharacter is { IsLive: true } sel)
            sel.PullFromMemory();

        // Periodically re-read the whole party so the UI tracks in-game changes hands-free.
        if (AutoReadParty && ++_autoReadCounter >= AutoReadPartyTicks)
        {
            _autoReadCounter = 0;
            RefreshAll();
        }
    }

    private void ForEachChar(Action<CharacterViewModel> action)
    {
        foreach (var c in Characters) action(c);
    }

    private void RaiseCommands()
    {
        AttachCommand.RaiseCanExecuteChanged();
        DetachCommand.RaiseCanExecuteChanged();
        RescanCommand.RaiseCanExecuteChanged();
        RefreshSelectedCommand.RaiseCanExecuteChanged();
        RefreshAllCommand.RaiseCanExecuteChanged();
        WriteSelectedCommand.RaiseCanExecuteChanged();
        MaxHpAllCommand.RaiseCanExecuteChanged();
        MaxSpAllCommand.RaiseCanExecuteChanged();
        MaxStatsAllCommand.RaiseCanExecuteChanged();
        MaxResistancesAllCommand.RaiseCanExecuteChanged();
        MaxEverythingSelectedCommand.RaiseCanExecuteChanged();
        MaxResistancesSelectedCommand.RaiseCanExecuteChanged();
        MaxEverythingAllCommand.RaiseCanExecuteChanged();
        SaveRosterFileCommand.RaiseCanExecuteChanged();
        SaveSnapshotCommand.RaiseCanExecuteChanged();
        LoadSnapshotCommand.RaiseCanExecuteChanged();
        CopyToTargetCommand.RaiseCanExecuteChanged();
        SwapWithTargetCommand.RaiseCanExecuteChanged();
        MemoryDump.RefreshState();   // its dump button tracks IsAttached
        OnPropertyChanged(nameof(HasParty));
    }

    /// <summary>True when at least one character is loaded — gates the destructive
    /// party-wide "Max EVERYTHING" button so it can't fire on an empty roster.</summary>
    public bool HasParty => Characters.Count > 0;

    private void RefreshAll() => ForEachChar(c => c.PullFromMemory());
}
