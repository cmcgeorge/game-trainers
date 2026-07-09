using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using BardsTale1Trainer.Game;
using BardsTale1Trainer.Memory;
using BardsTale1Trainer.Mvvm;

namespace BardsTale1Trainer.ViewModels;

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
        MaxEverythingSelectedCommand = new RelayCommand(() => SelectedCharacter?.MaxEverything(), () => SelectedCharacter != null);
        MaxEverythingAllCommand = new RelayCommand(() => ForEachChar(c => c.MaxEverything()), () => Characters.Count > 0);
        LoadTpwFileCommand = new RelayCommand(p => LoadTpwFile(p as string));
        SaveTpwFilesCommand = new RelayCommand(SaveTpwFiles, CanSaveTpwFiles);
        SaveSnapshotCommand = new RelayCommand(p => SaveSnapshot(p as string), _ => Characters.Count > 0);
        LoadSnapshotCommand = new RelayCommand(p => LoadSnapshot(p as string), _ => Characters.Count > 0);
        CopyToTargetCommand = new RelayCommand(CopyToTarget, CanUseSlotTarget);
        SwapWithTargetCommand = new RelayCommand(SwapWithTarget, CanUseSlotTarget);

        MemorySearch = new MemorySearchViewModel(() => _mem, s => Status = s);
        PairSearch = new PairSearchViewModel(() => _mem, s => Status = s);
        MemoryDump = new MemoryDumpViewModel(() => _mem, s => Status = s);
        DumpDiff = new DumpDiffViewModel(s => Status = s);
        MapReference = new MapReferenceViewModel(
            () => _mem,
            () => PairSearch.LockedAddress,   // the live marker follows the X/Y search's lone survivor
            s => Status = s);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        RefreshProcesses();
        TryAutoAttach();
    }

    private void TryAutoAttach()
    {
        if (!IsAttached && SelectedProcess?.IsLikelyEmulator == true)
            Attach();
    }

    public MemorySearchViewModel MemorySearch { get; }
    public PairSearchViewModel PairSearch { get; }
    public MemoryDumpViewModel MemoryDump { get; }

    /// <summary>Compares two saved dumps and lists the changed bytes by process address.</summary>
    public DumpDiffViewModel DumpDiff { get; }

    /// <summary>Area grids shown in the Maps tab, plus the live party marker / teleport.</summary>
    public MapReferenceViewModel MapReference { get; }

    public SpellReferenceViewModel SpellReference { get; } = new();
    public ItemReferenceViewModel ItemReference { get; } = new();

    /// <summary>Read-only bestiary (the game's full monster name table) shown in the Monsters tab.</summary>
    public MonsterReferenceViewModel MonsterReference { get; } = new();

    public ClassReferenceViewModel ClassReference { get; } = new();

    // --- process list -----------------------------------------------------------
    public ObservableCollection<ProcessInfo> Processes { get; } = new();

    private ProcessInfo? _selectedProcess;
    public ProcessInfo? SelectedProcess
    {
        get => _selectedProcess;
        set { if (SetField(ref _selectedProcess, value)) RaiseCommands(); }
    }

    public ObservableCollection<PartyLocation> Locations { get; } = new();

    private PartyLocation? _selectedLocation;
    public PartyLocation? SelectedLocation
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
    public bool LiveRefresh { get => _liveRefresh; set => SetField(ref _liveRefresh, value); }

    private bool _autoReadParty = true;
    public bool AutoReadParty { get => _autoReadParty; set => SetField(ref _autoReadParty, value); }

    // Set by the View while a TextBox in the editor has keyboard focus. The periodic
    // auto re-read raises PropertyChanged on the friendly fields, which updates a bound
    // TextBox's displayed text even mid-edit (LostFocus only defers the reverse,
    // target→source, direction). Pausing the auto-read while editing avoids clobbering a
    // value the user is part-way through typing; it resumes the moment focus leaves.
    private bool _editorHasFocus;
    public bool EditorHasFocus { get => _editorHasFocus; set => SetField(ref _editorHasFocus, value); }

    // Also list empty / special slots (the summon slot, vacated members) instead of just
    // the occupied party members.
    private bool _showEmptySlots;
    public bool ShowEmptySlots
    {
        get => _showEmptySlots;
        set { if (SetField(ref _showEmptySlots, value) && _selectedLocation != null) BuildCharacters(_selectedLocation.Value); }
    }

    private const int AutoReadPartyTicks = 13;   // 150 ms × 13 ≈ 2 s
    private int _autoReadCounter;

    // --- party-wide freeze toggles ----------------------------------------------
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

    private bool _freezeGoldAll = true;
    public bool FreezeGoldAll
    {
        get => _freezeGoldAll;
        set { if (SetField(ref _freezeGoldAll, value)) ForEachChar(c => c.FreezeGold = value); }
    }

    private bool _freezeExpAll;
    public bool FreezeExpAll
    {
        get => _freezeExpAll;
        set { if (SetField(ref _freezeExpAll, value)) ForEachChar(c => c.FreezeExp = value); }
    }

    private string _status = "Not attached. Launch the game (load a party past the title), then pick its process and Attach.";
    public string Status { get => _status; set => SetField(ref _status, value); }

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
    public RelayCommand MaxEverythingSelectedCommand { get; }
    public RelayCommand MaxEverythingAllCommand { get; }
    public RelayCommand LoadTpwFileCommand { get; }
    public RelayCommand SaveTpwFilesCommand { get; }
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
            Detach();
            _mem = ProcessMemory.Open(SelectedProcess.Id);
            IsAttached = true;
            Status = $"Attached to {SelectedProcess.Display}. Scanning for the party…";
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
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = null;
        Characters.Clear();
        Locations.Clear();
        SelectedCharacter = null;
        SelectedLocation = null;
        MemorySearch.Reset();
        PairSearch.Reset();
        MemoryDump.CancelDump();

        _mem?.Dispose();
        _mem = null;
        IsAttached = false;
        ScanProgress = 0;
    }

    private async void Rescan() => await ScanAsync();

    private CancellationTokenSource? _scanCts;

    private async Task ScanAsync()
    {
        if (_mem == null) return;
        var mem = _mem;

        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        var progress = new Progress<double>(v => { if (!ct.IsCancellationRequested) ScanProgress = v; });
        Status = "Scanning process memory for the game's data segment…";
        List<PartyLocation> found;
        try
        {
            found = await Task.Run(() => PartyLocator.FindAll(mem, progress, ct), ct);
        }
        catch (OperationCanceledException)
        {
            return;
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
            Status = "No party found. Make sure a party is loaded into the game (past the title screen) and try Re-scan.";
            return;
        }
        Status = $"Found {found.Count} candidate data segment(s). Showing the first match.";
        SelectedLocation = found[0];
    }

    private void BuildCharacters(PartyLocation loc)
    {
        Characters.Clear();
        SelectedCharacter = null;
        SlotTarget = null;
        _tpwPaths.Clear();   // the characters are the live game's now, not the files'
        if (_mem == null) return;

        // Read every slot (0..6); show occupied ones always, empties only when asked.
        // Slot indices map 1:1 to the on-screen roster rows that carry the names.
        for (int slot = 0; slot < PartyFormat.PartySlots; slot++)
        {
            nuint addr = loc.SlotAddress(slot);
            var buf = _mem.Read(addr, PartyFormat.RecordSize);
            if (buf.Length < PartyFormat.RecordSize) break;
            var rec = new CharacterRecord(buf) { Address = addr, Slot = slot };

            nuint nameAddr = loc.RowAddress(slot);
            var nameBuf = _mem.Read(nameAddr, PartyFormat.PartyRowNameLength);
            rec.Name = CharacterRecord.DecodeRosterName(nameBuf);

            if (!rec.IsOccupied && !ShowEmptySlots) continue;

            var vm = new CharacterViewModel(rec, _mem) { NameAddress = nameAddr };
            Characters.Add(vm);
        }
        ApplyFreezeStateToParty();
        SelectedCharacter = Characters.FirstOrDefault();
        RaiseCommands();
    }

    private void ApplyFreezeStateToParty() =>
        ForEachChar(c =>
        {
            c.FreezeHp = _freezeHpAll; c.FreezeSp = _freezeSpAll;
            c.FreezeGold = _freezeGoldAll; c.FreezeExp = _freezeExpAll;
        });

    // --- offline .TPW file editing -------------------------------------------------
    // Each offline-loaded character remembers the .TPW it came from, so the edited record
    // can be written back (with a .bak of the previous version). Attaching to a live game
    // rebuilds the character list, so file mode ends there.
    private readonly Dictionary<CharacterViewModel, string> _tpwPaths = new();

    private void LoadTpwFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            var bytes = File.ReadAllBytes(path);
            var rec = CharacterRecord.FromTpw(bytes);
            if (rec == null) { Status = "That file is too small to be a .TPW character."; return; }
            rec.Slot = Characters.Count;
            var vm = new CharacterViewModel(rec, null);   // file-only (writes go to Save .TPW)
            Characters.Add(vm);
            _tpwPaths[vm] = path;
            ApplyFreezeStateToParty();
            SelectedCharacter ??= Characters.FirstOrDefault();
            Status = $"Loaded {Path.GetFileName(path)}. Edits stay in the trainer until you click 💾 Save .TPW file(s).";
            RaiseCommands();
        }
        catch (Exception ex)
        {
            Status = "Failed to read .TPW file: " + ex.Message;
        }
    }

    // Save is offered only while the loaded characters are actually the files' (file-only
    // view models); after an attach the same collection holds live characters instead.
    private bool CanSaveTpwFiles() =>
        _tpwPaths.Count > 0 && Characters.Count > 0 && Characters.All(c => !c.IsLive);

    private void SaveTpwFiles()
    {
        int saved = 0;
        foreach (var c in Characters)
        {
            if (!_tpwPaths.TryGetValue(c, out var path)) continue;
            try
            {
                // One-step undo: the file as it was before this save lands in "<file>.bak".
                if (File.Exists(path))
                    File.Copy(path, path + ".bak", overwrite: true);
                File.WriteAllBytes(path, c.Record.ToTpw());
                saved++;
            }
            catch (Exception ex)
            {
                Status = $"Failed to save {Path.GetFileName(path)}: {ex.Message}";
                return;
            }
        }
        Status = $"Saved {saved} character(s) back to their .TPW file(s) (previous versions kept as .bak).";
    }

    // --- party snapshots ------------------------------------------------------------
    // A snapshot is a 7-block .TPW-format file of the current party (live or offline),
    // restorable later by slot. Restoring writes straight through the normal record path,
    // so live characters are pushed to the game immediately.
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
            foreach (var (slot, name, record) in slots)
            {
                var target = Characters.FirstOrDefault(c => c.Record.Slot == slot);
                if (target == null) { skipped++; continue; }   // no such character any more
                // The snapshot block carries the .TPW disk marker; a live record wants 0 there.
                record[PartyFormat.OffDiskMarker] = (byte)(target.IsLive ? 0 : 1);
                target.Record.Load(record);
                target.PushAll();      // live characters are written to the game right away
                target.Name = name;    // pushes the roster-row name too (no-op offline)
                target.RaiseAll();
                restored++;
            }
            string skippedNote = skipped > 0 ? $" ({skipped} snapshot slot(s) had no matching character and were skipped)" : "";
            Status = restored == 0 && skipped == 0
                ? "The snapshot contains no characters."
                : $"Restored {restored} character(s) from {Path.GetFileName(path)}.{skippedNote}";
            if (restored > 0 && Characters.All(c => !c.IsLive))
                Status += " Click 💾 Save .TPW file(s) to write them to disk.";
        }
        catch (Exception ex)
        {
            Status = "Failed to restore snapshot: " + ex.Message;
        }
    }

    // --- slot tools (copy / swap between party slots) ---------------------------------
    private CharacterViewModel? _slotTarget;
    /// <summary>The "other" character that Copy onto / Swap with act against.</summary>
    public CharacterViewModel? SlotTarget
    {
        get => _slotTarget;
        set { if (SetField(ref _slotTarget, value)) RaiseCommands(); }
    }

    private bool CanUseSlotTarget() =>
        SelectedCharacter != null && SlotTarget != null && !ReferenceEquals(SelectedCharacter, SlotTarget);

    /// <summary>Overwrites the target's record with a copy of the selected character's. The
    /// BT1 record carries no slot index to fix up, but the name lives in the game's per-slot
    /// roster row — setting <see cref="CharacterViewModel.Name"/> writes it to the target's row.</summary>
    private void CopyToTarget()
    {
        if (SelectedCharacter is not { } src || SlotTarget is not { } dst || ReferenceEquals(src, dst)) return;
        string oldName = dst.Record.Name;
        dst.Record.Load(WithMarkerOf(dst, src.Record.ToArray()));
        dst.PushAll();
        dst.Name = src.Record.Name;
        dst.RaiseAll();
        Status = $"Copied {src.Record.Name} onto slot {dst.Record.Slot + 1} ({oldName}).";
    }

    /// <summary>Swaps the selected character's record (and roster-row name) with the target's.</summary>
    private void SwapWithTarget()
    {
        if (SelectedCharacter is not { } a || SlotTarget is not { } b || ReferenceEquals(a, b)) return;
        var aBytes = a.Record.ToArray();
        var bBytes = b.Record.ToArray();
        var aName = a.Record.Name;
        var bName = b.Record.Name;
        a.Record.Load(WithMarkerOf(a, bBytes)); b.Record.Load(WithMarkerOf(b, aBytes));
        a.PushAll(); b.PushAll();
        a.Name = bName; b.Name = aName;
        a.RaiseAll(); b.RaiseAll();
        Status = $"Swapped slots {a.Record.Slot + 1} and {b.Record.Slot + 1} ({a.Record.Name} ⇄ {b.Record.Name}).";
    }

    /// <summary>The disk-marker byte (0x01 on disk, 0x00 live) belongs to the slot's medium,
    /// not to the character data — keep the destination's own value when records move
    /// between slots. Matters when the list mixes live slots with offline-loaded .TPWs.</summary>
    private static byte[] WithMarkerOf(CharacterViewModel dst, byte[] record)
    {
        record[PartyFormat.OffDiskMarker] = dst.Record.GetByte(PartyFormat.OffDiskMarker);
        return record;
    }

    // --- global hotkey entry points (invoked by MainWindow's RegisterHotKey hook) ------
    /// <summary>Ctrl+F1: toggles the party-wide HP/SP freezes ("god mode") as one switch.</summary>
    public void ToggleGodModeHotkey()
    {
        bool on = !(FreezeHpAll && FreezeSpAll);
        FreezeHpAll = on;
        FreezeSpAll = on;
        Status = on ? "God mode ON (HP and SP frozen for the whole party)."
                    : "God mode OFF (HP/SP freezes released).";
    }

    /// <summary>Ctrl+F2: one-shot heal — current HP/SP to max, party-wide.</summary>
    public void HealPartyHotkey()
    {
        if (Characters.Count == 0) { Status = "Heal hotkey: no party loaded."; return; }
        ForEachChar(c => c.Heal());
        Status = "Party healed (HP/SP refilled).";
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

        if (!IsAttached) return;
        foreach (var c in Characters) c.ApplyFreezes();
        // Both the per-tick LiveRefresh and the periodic auto re-read below re-read the
        // selected character and raise PropertyChanged on its bound fields, which would
        // overwrite a value the user is mid-typing — so both pause while the editor has focus.
        if (LiveRefresh && !EditorHasFocus && SelectedCharacter is { IsLive: true } sel)
            sel.PullFromMemory();

        // Paused while editing (see above); the counter is short-circuited so it resumes
        // counting from where it was once focus leaves the editor.
        if (AutoReadParty && !EditorHasFocus && ++_autoReadCounter >= AutoReadPartyTicks)
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
        MaxEverythingSelectedCommand.RaiseCanExecuteChanged();
        MaxEverythingAllCommand.RaiseCanExecuteChanged();
        SaveTpwFilesCommand.RaiseCanExecuteChanged();
        SaveSnapshotCommand.RaiseCanExecuteChanged();
        LoadSnapshotCommand.RaiseCanExecuteChanged();
        CopyToTargetCommand.RaiseCanExecuteChanged();
        SwapWithTargetCommand.RaiseCanExecuteChanged();
        MemoryDump.RefreshState();
    }

    private void RefreshAll() => ForEachChar(c => c.PullFromMemory());
}
