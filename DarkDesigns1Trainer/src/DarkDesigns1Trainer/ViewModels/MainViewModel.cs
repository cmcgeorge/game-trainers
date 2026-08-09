using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using DarkDesigns1Trainer.Game;
using DarkDesigns1Trainer.Memory;

namespace DarkDesigns1Trainer.ViewModels;

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
/// Root view-model: process attach/scan, the located party list, the freeze poll loop,
/// the party-wide quick actions, and the offline save editor.
/// </summary>
public sealed class MainViewModel : ObservableObject, ICharacterHost, IDisposable
{
    private static readonly string[] EmulatorHints =
        { "dosbox", "dosbox-x", "dosbox-staging", "scummvm", "pcem", "86box", "qemu", "boxer" };

    private ProcessMemory? _mem;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;

    // The pid we actually attached to. SelectedProcess can be changed in the dropdown afterwards,
    // and the roller has to send keystrokes to the process it is reading memory from.
    private int? _attachedPid;

    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ObservableCollection<CharacterViewModel> Party { get; } = new();
    public ReferenceViewModel Reference { get; } = new();

    /// <summary>The create-screen roller: locates the rolled stat pool, re-rolls, and can write it.</summary>
    public CharacterRollerViewModel Roller { get; }

    /// <summary>The Maps tab: where the party is standing, the level schematic, and teleport.</summary>
    public MapsViewModel Maps { get; }

    /// <summary>
    /// Base address of the located roster array — the first record the scan matched, backed up by
    /// its slot index. The map locator starts from this, because the party position and the map
    /// buffer sit at constant offsets from it inside the same data segment. Null until a scan
    /// succeeds, and dropped on detach along with everything else tied to the process.
    /// </summary>
    private nuint? _rosterBase;

    private ProcessEntry? _selectedProcess;
    public ProcessEntry? SelectedProcess { get => _selectedProcess; set { SetField(ref _selectedProcess, value); RaiseCommands(); } }

    private CharacterViewModel? _selectedCharacter;
    public CharacterViewModel? SelectedCharacter { get => _selectedCharacter; set => SetField(ref _selectedCharacter, value); }

    public bool IsAttached => _mem is { IsOpen: true };

    private bool _isScanning;
    public bool IsScanning { get => _isScanning; set { SetField(ref _isScanning, value); RaiseCommands(); } }

    private string _status = "Launch Dark Designs I in DOSBox, then pick the process and Attach.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- party-wide freeze toggles ------------------------------------------
    private bool _freezeBody;
    public bool FreezeBody
    {
        get => _freezeBody;
        set { if (SetField(ref _freezeBody, value)) { foreach (var c in Party) c.FreezeBody = value; Status = value ? "Body frozen for the party." : "Body freeze OFF."; } }
    }

    private bool _freezeMagic;
    public bool FreezeMagic
    {
        get => _freezeMagic;
        set { if (SetField(ref _freezeMagic, value)) { foreach (var c in Party) c.FreezeMagic = value; Status = value ? "Magic frozen for the party." : "Magic freeze OFF."; } }
    }

    private bool _freezeStatus;
    public bool FreezeStatus
    {
        get => _freezeStatus;
        set { if (SetField(ref _freezeStatus, value)) { foreach (var c in Party) c.FreezeStatus = value; Status = value ? "Status frozen for the party." : "Status freeze OFF."; } }
    }

    // --- item potency patches ------------------------------------------------
    // Dark Designs has no charges. On (U)se it applies the effect, rolls random(256) and destroys
    // the item unless potency > roll; a magic weapon's special effect fires on the same test.
    // Pinning potency to 256 makes both outcomes certain. This edits the game's *item table*, not
    // a character, so it is global, never saved to DDCHARS.DAT, and undone on detach.
    private nuint? _itemTableBase;
    private readonly Dictionary<int, int> _originalPotency = new();

    /// <summary>Which group of items a potency patch applies to.</summary>
    private enum PotencySet { Consumables, MagicWeapons }

    // Locating the table is a full address-space scan, so two toggles flipped in quick succession
    // would otherwise overlap and could land in the wrong order.
    private readonly SemaphoreSlim _potencyGate = new(1, 1);

    private bool _itemsNeverBreak;
    public bool ItemsNeverBreak
    {
        get => _itemsNeverBreak;
        set { if (SetField(ref _itemsNeverBreak, value)) _ = ApplyPotencyAsync(PotencySet.Consumables, value); }
    }

    private bool _weaponsAlwaysTrigger;
    public bool WeaponsAlwaysTrigger
    {
        get => _weaponsAlwaysTrigger;
        set { if (SetField(ref _weaponsAlwaysTrigger, value)) _ = ApplyPotencyAsync(PotencySet.MagicWeapons, value); }
    }

    private bool DesiredState(PotencySet set) =>
        set == PotencySet.Consumables ? _itemsNeverBreak : _weaponsAlwaysTrigger;

    private static IEnumerable<ItemBook.Item> ItemsIn(PotencySet set) =>
        set == PotencySet.Consumables ? ItemBook.Consumables : ItemBook.MagicWeapons;

    private static string LabelFor(PotencySet set) =>
        set == PotencySet.Consumables ? "Usable items" : "Magic weapons";

    /// <summary>
    /// When set, detaching leaves the item-table patches in place instead of undoing them, so they
    /// last until the game itself exits. Off by default: a patch nothing is attached to is a patch
    /// nothing can undo.
    /// </summary>
    private bool _keepPatchesOnDetach;
    public bool KeepPatchesOnDetach { get => _keepPatchesOnDetach; set => SetField(ref _keepPatchesOnDetach, value); }

    /// <summary>
    /// Pins (or restores) the potency word for a set of items. The table is located by content on
    /// first use and cached; original values are kept so the toggle is reversible.
    /// </summary>
    private async Task ApplyPotencyAsync(PotencySet set, bool on)
    {
        var mem = _mem;
        if (mem == null)
        {
            // Not an error: the toggle is remembered and applied when we next attach.
            Status = on ? "Attach to the game, and this will be applied." : "Not attached.";
            return;
        }

        await _potencyGate.WaitAsync();
        try
        {
            if (mem != _mem) return;
            // The toggle may have been flipped again while we queued; the last intent wins.
            if (DesiredState(set) != on) return;

            if (_itemTableBase is null)
            {
                Status = "Locating the game's item table…";
                var found = await Task.Run(() => ItemTableLocator.Find(mem));
                if (mem != _mem || DesiredState(set) != on) return;
                if (found is null)
                {
                    Status = "Could not find the item table — is the game past the title screen?";
                    return;
                }
                _itemTableBase = found;
            }

            ApplyPotency(mem, _itemTableBase.Value, ItemsIn(set), on, LabelFor(set));
        }
        finally { _potencyGate.Release(); }
    }

    private void ApplyPotency(ProcessMemory mem, nuint table, IEnumerable<ItemBook.Item> items,
                              bool on, string label)
    {
        int changed = 0;
        foreach (var item in items)
        {
            if (on)
            {
                if (!_originalPotency.ContainsKey(item.Id))
                {
                    int live = ItemTableLocator.ReadPotency(mem, table, item.Id);
                    if (live < 0) continue;
                    _originalPotency[item.Id] = live;
                }
                if (ItemTableLocator.WritePotency(mem, table, item.Id, ItemBook.PotencyAlways)) changed++;
            }
            else
            {
                if (!_originalPotency.TryGetValue(item.Id, out int original)) continue;
                if (ItemTableLocator.WritePotency(mem, table, item.Id, original)) changed++;
                _originalPotency.Remove(item.Id);
            }
        }

        Status = on
            ? $"{label}: {changed} item(s) pinned — they no longer fail their chance roll."
            : $"{label}: {changed} item(s) restored to their original odds.";
    }

    /// <summary>
    /// Drops the cached patch state, optionally putting the original potency values back first.
    ///
    /// The cached table address is cleared either way — it belongs to the process being let go of,
    /// and reusing it after re-attaching would write two bytes to a stale address in a different
    /// process. When the patches are deliberately left in place the remembered originals and the
    /// toggle states are kept instead of cleared: the originals are static game data, identical in
    /// every session, so re-attaching and unticking a toggle still restores the true values rather
    /// than re-saving the patched ones.
    /// </summary>
    private void ResetPotency(bool restore)
    {
        bool restored = false;
        if (restore && _mem is { IsOpen: true } mem && _itemTableBase is { } table)
        {
            foreach (var (id, original) in _originalPotency)
                ItemTableLocator.WritePotency(mem, table, id, original);
            restored = true;
        }

        _itemTableBase = null;

        // Only forget the originals once they have actually been put back. Wanting to restore is
        // not the same as having restored: on the keep-on-detach path the cached table address is
        // already gone, so a later restore attempt writes nothing — and clearing here would throw
        // away the only record of the true values while leaving the game patched.
        if (!restored) return;

        _originalPotency.Clear();
        _itemsNeverBreak = false; OnPropertyChanged(nameof(ItemsNeverBreak));
        _weaponsAlwaysTrigger = false; OnPropertyChanged(nameof(WeaponsAlwaysTrigger));
    }

    /// <summary>True when a toggle is on and the patches will outlive the detach.</summary>
    private bool LeavingPatchesBehind =>
        KeepPatchesOnDetach && (ItemsNeverBreak || WeaponsAlwaysTrigger);

    // --- save editor ---------------------------------------------------------
    private string? _saveFilePath;
    public string? SaveFilePath { get => _saveFilePath; set => SetField(ref _saveFilePath, value); }

    private SaveFile? _saveFile;
    public SaveFile? SaveFile { get => _saveFile; set => SetField(ref _saveFile, value); }

    public ObservableCollection<CharacterRecord> SaveCharacters { get; } = new();

    /// <summary>True once a <c>DDCHARS.DAT</c> is open, so the position editor can show itself.</summary>
    public bool HasSaveFile => SaveFile != null;

    // --- saved party position (the DDCHARS.DAT header) -----------------------
    // The header's level / X / Y / facing are exactly the four globals the game plays out of, so
    // editing them here teleports the party on the next run — level included, which the live
    // teleport deliberately does not do (see MapsViewModel).
    private PartyPosition SavePosition
    {
        get => SaveFile?.Position ?? default;
        set
        {
            if (SaveFile is not { } file) return;
            file.Position = value;
            OnPropertyChanged(nameof(SaveLevel));
            OnPropertyChanged(nameof(SaveX));
            OnPropertyChanged(nameof(SaveY));
            OnPropertyChanged(nameof(SaveFacing));
            OnPropertyChanged(nameof(SavePositionText));
        }
    }

    /// <summary>Dungeon level 1–5, or 0 for "in town".</summary>
    public int SaveLevel
    {
        get => SavePosition.Level;
        set => SavePosition = SavePosition with { Level = Math.Clamp(value, MapFormat.TownLevel, MapFormat.MaxLevel) };
    }

    public int SaveX
    {
        get => SavePosition.X;
        set => SavePosition = SavePosition with { X = Math.Clamp(value, 0, MapFormat.GridSize - 1) };
    }

    public int SaveY
    {
        get => SavePosition.Y;
        set => SavePosition = SavePosition with { Y = Math.Clamp(value, 0, MapFormat.GridSize - 1) };
    }

    /// <summary>0 = North, 1 = East, 2 = South, 3 = West.</summary>
    public int SaveFacing
    {
        get => SavePosition.Facing;
        set => SavePosition = SavePosition with { Facing = Math.Clamp(value, 0, MapFormat.Directions - 1) };
    }

    /// <summary>The saved position in words, so the four boxes above can be read at a glance.</summary>
    public string SavePositionText => SaveFile == null ? "" : SavePosition.Describe();

    private void RefreshSavePosition()
    {
        OnPropertyChanged(nameof(HasSaveFile));
        OnPropertyChanged(nameof(SaveLevel));
        OnPropertyChanged(nameof(SaveX));
        OnPropertyChanged(nameof(SaveY));
        OnPropertyChanged(nameof(SaveFacing));
        OnPropertyChanged(nameof(SavePositionText));
    }

    private CharacterRecord? _selectedSaveCharacter;
    public CharacterRecord? SelectedSaveCharacter
    {
        get => _selectedSaveCharacter;
        set { if (SetField(ref _selectedSaveCharacter, value)) RebuildSaveItemSlots(); }
    }

    /// <summary>The selected save character's ten carried pack slots.</summary>
    public ObservableCollection<ItemSlotViewModel> SaveInventory { get; } = new();

    /// <summary>The selected save character's four readied-equipment slots.</summary>
    public ObservableCollection<ItemSlotViewModel> SaveEquipment { get; } = new();

    /// <summary>Adapts a loaded save record to <see cref="IItemPack"/> for the duplicate button.</summary>
    private sealed class SavePack : IItemPack
    {
        private readonly CharacterRecord _rec;
        private readonly MainViewModel _owner;

        public SavePack(CharacterRecord rec, MainViewModel owner) { _rec = rec; _owner = owner; }

        public bool HasFreeSlot => _rec.ItemCount < CharacterFormat.ItemSlotCount;

        public bool TryAddItem(int itemId)
        {
            if (itemId == 0 || _rec.AddItem(itemId) < 0) return false;
            _owner.SaveFile?.MarkModified();
            foreach (var s in _owner.SaveInventory) s.Refresh();
            return true;
        }
    }

    private void RebuildSaveItemSlots()
    {
        SaveInventory.Clear();
        SaveEquipment.Clear();
        if (_selectedSaveCharacter is not { } rec) return;

        var pack = new SavePack(rec, this);
        for (int i = 0; i < CharacterFormat.ItemSlotCount; i++)
        {
            int slot = i;
            SaveInventory.Add(new ItemSlotViewModel(
                ((char)('A' + slot)).ToString(),
                () => rec.GetItem(slot),
                id => { rec.SetItem(slot, id); SaveFile?.MarkModified(); },
                pack: pack));
        }

        foreach (ItemBook.ReadySlot rs in Enum.GetValues<ItemBook.ReadySlot>())
        {
            var slot = rs;
            SaveEquipment.Add(new ItemSlotViewModel(
                ItemBook.ReadyLabel(slot),
                () => rec.GetReadied(slot),
                id => { rec.SetReadied(slot, id); SaveFile?.MarkModified(); },
                slot));
        }
    }

    // --- commands ------------------------------------------------------------
    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand HealPartyCommand { get; }
    public ICommand MaxPartyCommand { get; }
    public ICommand MaxEverythingPartyCommand { get; }
    public ICommand MaxMoneyPartyCommand { get; }
    public ICommand LoadSaveCommand { get; }
    public ICommand SaveSaveCommand { get; }
    public ICommand SaveMaxAllCommand { get; }

    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        AttachCommand = new RelayCommand(_ => Attach(), _ => SelectedProcess != null && !IsAttached);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);
        ScanCommand = new RelayCommand(_ => Scan(), _ => IsAttached && !IsScanning);
        HealPartyCommand = new RelayCommand(_ => HealParty(), _ => Party.Count > 0);
        MaxPartyCommand = new RelayCommand(_ => ForEachParty(c => c.MaxAttributes()), _ => Party.Count > 0);
        MaxEverythingPartyCommand = new RelayCommand(_ => ForEachParty(c => c.MaxEverything()), _ => Party.Count > 0);
        MaxMoneyPartyCommand = new RelayCommand(_ => ForEachParty(c => c.MaxMoney()), _ => Party.Count > 0);
        LoadSaveCommand = new RelayCommand(_ => LoadSave(), _ => true);
        SaveSaveCommand = new RelayCommand(_ => SaveSave(), _ => SaveFile != null);
        SaveMaxAllCommand = new RelayCommand(_ => SaveMaxAll(), _ => SaveFile != null);

        Roller = new CharacterRollerViewModel(() => _mem, () => _attachedPid, msg => Status = msg);
        Maps = new MapsViewModel(() => _mem, () => _rosterBase, this, msg => Status = msg);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _poll.Tick += (_, _) => PollTick();

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
            try
            {
                string name = p.ProcessName;
                bool emu = EmulatorHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));
                list.Add(new ProcessEntry(p.Id, name, emu));
            }
            catch { }
            finally { p.Dispose(); }
        }
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
            _attachedPid = SelectedProcess.Id;
            OnPropertyChanged(nameof(IsAttached));
            RaiseCommands();
            Roller.RefreshCommands();
            Maps.RaiseCommands();
            _poll.Start();
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). Scanning for characters…";
            Scan();

            // A toggle left ticked — set while detached, or carried over by "Keep on detach" — has
            // to be re-applied, or the checkbox would claim a patch this process never received.
            if (_itemsNeverBreak) _ = ApplyPotencyAsync(PotencySet.Consumables, true);
            if (_weaponsAlwaysTrigger) _ = ApplyPotencyAsync(PotencySet.MagicWeapons, true);
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
        bool leftBehind = LeavingPatchesBehind;
        ResetPotency(restore: !KeepPatchesOnDetach);
        Roller.Reset();       // its locked address belongs to the process we're letting go of
        Maps.Reset();         // as do the position block and map buffer it found
        _mem?.Dispose();
        _mem = null;
        _attachedPid = null;
        _rosterBase = null;
        Party.Clear();
        SelectedCharacter = null;
        _freezeBody = false; OnPropertyChanged(nameof(FreezeBody));
        _freezeMagic = false; OnPropertyChanged(nameof(FreezeMagic));
        _freezeStatus = false; OnPropertyChanged(nameof(FreezeStatus));
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Roller.RefreshCommands();
        Status = leftBehind
            ? "Detached — item patches left in place; they last until the game exits. Re-attach and untick to undo."
            : "Detached.";
    }

    // --- scanning ------------------------------------------------------------
    private async void Scan()
    {
        if (_mem == null || IsScanning) return;
        IsScanning = true;
        Status = "Scanning memory for the character roster…";
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        var mem = _mem;
        try
        {
            var found = await Task.Run(() => RosterLocator.FindAll(mem, ct), ct);
            if (mem != _mem) return;
            Party.Clear();
            foreach (var lc in found)
                Party.Add(new CharacterViewModel(this, lc));
            SelectedCharacter = Party.FirstOrDefault();

            // Back up from the first hit to the start of the roster array; the map locator works
            // from there. A rescan may land on a different anchor, so recompute it every time.
            _rosterBase = found.Count > 0
                ? found[0].Address - (nuint)(found[0].Slot * CharacterFormat.RecordSize)
                : null;
            Maps.RaiseCommands();
            if (FreezeBody) foreach (var c in Party) c.FreezeBody = true;
            if (FreezeMagic) foreach (var c in Party) c.FreezeMagic = true;
            if (FreezeStatus) foreach (var c in Party) c.FreezeStatus = true;
            Status = Party.Count == 0
                ? "No characters found. Make sure the game is loaded (past the title screen), then Re-scan."
                : $"Found {Party.Count} character(s).";
        }
        catch (OperationCanceledException) { if (mem == _mem) Status = "Scan cancelled."; }
        catch (Exception ex) { if (mem == _mem) Status = "Scan error: " + ex.Message; }
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
    private bool _warnedStale;

    private void PollTick()
    {
        if (_mem == null) return;
        foreach (var c in Party) c.Poll();
        Maps.Tick();

        // Say so once rather than every tick.
        bool anyStale = Party.Any(c => c.IsStale);
        if (anyStale && !_warnedStale)
            Status = "The roster changed in the game — some characters no longer sit where they were found. Click Re-scan.";
        _warnedStale = anyStale;
    }

    // --- save editor ---------------------------------------------------------
    public void LoadSave()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open DDCHARS.DAT",
            Filter = "Dark Designs character file|DDCHARS.DAT|All files|*.*",
            FileName = "DDCHARS.DAT",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            SaveFile?.Dispose();
            SaveFile = new SaveFile(dlg.FileName);
            SaveFilePath = dlg.FileName;
            SaveCharacters.Clear();
            foreach (var c in SaveFile.OccupiedCharacters)
                SaveCharacters.Add(c);
            SelectedSaveCharacter = SaveCharacters.FirstOrDefault();
            RefreshSavePosition();
            Status = SaveCharacters.Count == 0
                ? $"Loaded {dlg.FileName} — no characters found."
                : $"Loaded {dlg.FileName} — {SaveCharacters.Count} character(s), party at {SavePosition.Describe()}";
            RaiseCommands();
        }
        catch (Exception ex)
        {
            Status = "Load failed: " + ex.Message;
        }
    }

    public void SaveSave()
    {
        if (SaveFile == null) return;
        try
        {
            SaveFile.Save();
            Status = $"Saved to {SaveFilePath}.";
        }
        catch (Exception ex)
        {
            Status = "Save failed: " + ex.Message;
        }
    }

    public void SaveMaxAll()
    {
        if (SaveFile == null) return;
        foreach (var c in SaveFile.Characters)
        {
            if (!c.IsOccupied) continue;
            for (int i = 0; i < CharacterFormat.AttributeCount; i++)
                c.SetAttribute(i, CharacterFormat.MaxAttribute);
            c.BodyMax = Math.Max(c.BodyMax, CharacterFormat.MaxVital);
            c.BodyCurrent = c.BodyMax;
            c.MagicMax = Math.Max(c.MagicMax, CharacterFormat.MaxVital);
            c.MagicCurrent = c.MagicMax;
            c.Level = Math.Max(c.Level, CharacterFormat.MaxLevel);
            c.NextLevel = CharacterFormat.MaxNextLevel;
            c.Gold = Math.Max(c.Gold, CharacterFormat.MaxGold);
            c.Status = CharacterFormat.StatusFine;
        }
        SaveFile.MarkModified();
        SaveFile.Save();
        SaveCharacters.Clear();
        foreach (var c in SaveFile.OccupiedCharacters)
            SaveCharacters.Add(c);
        SelectedSaveCharacter = SaveCharacters.FirstOrDefault();
        Status = "Maxed all characters and saved.";
    }

    // --- ICharacterHost ------------------------------------------------------
    bool ICharacterHost.WriteBytes(nuint recordAddress, byte[] source, int offset, int length)
        => _mem?.WriteRange(recordAddress, source, offset, length) ?? false;

    bool ICharacterHost.ReadBytes(nuint address, byte[] destination, int length)
        => (_mem?.Read(address, destination, length) ?? 0) == length;

    private void RaiseCommands()
    {
        (AttachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (HealPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxEverythingPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxMoneyPartyCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveSaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveMaxAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        ResetPotency(restore: !KeepPatchesOnDetach);
        // Cancels the roll loop; it can still be mid-keystroke, so one last R may reach the game
        // before it notices. Memory access stays safe either way (ProcessMemory holds a
        // SafeProcessHandle), so this isn't worth blocking shutdown on.
        Roller.Reset();
        Maps.Reset();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _mem?.Dispose();
        _mem = null;              // keeps the `mem != _mem` staleness guard honest
        _potencyGate.Dispose();
        SaveFile?.Dispose();
    }
}
