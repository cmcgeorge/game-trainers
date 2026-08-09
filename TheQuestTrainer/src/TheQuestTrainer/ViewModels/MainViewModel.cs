using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using TheQuestTrainer.Game;
using TheQuestTrainer.Memory;

namespace TheQuestTrainer.ViewModels;

/// <summary>
/// The session: attach, locate, refresh, write.
///
/// The shape is deliberately flat — one view model owning the scalars, two row collections and a
/// timer — because the interesting behaviour is not in the object graph, it is in three rules:
///
/// <list type="bullet">
/// <item>A refresh never overwrites a box being typed into (<see cref="EditorHasFocus"/>), but a
/// freshly built row always takes the game's value.</item>
/// <item>A freeze latches the value it was ticked at, so it cannot chase the number it is holding
/// down.</item>
/// <item>Every write re-validates the record first, so a save, a load or a death between two ticks
/// cannot turn an edit into a write to a freed heap block.</item>
/// </list>
/// </summary>
public sealed class MainViewModel : ObservableObject, IGameHost, IDisposable
{
    /// <summary>How often the display refreshes and freezes are re-applied.</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(250);

    private readonly DispatcherTimer _timer;
    private readonly FreezeWriter _freezes = new();

    private ProcessMemory? _memory;
    private ProcessMemorySource? _source;
    private TrainerActions? _actions;
    private PeImage? _image;
    private Process? _process;
    private uint _record;
    private bool _rowsBuilt;
    private string? _gameFolder;

    private string _status = "Start The Quest, load or begin a game, then press Attach.";
    private string _buildNote = "";
    private bool _isAttached;
    private bool _isReadOnly;
    private ProcessEntry? _selectedProcess;

    private IReadOnlyList<ItemType> _catalog = Array.Empty<ItemType>();
    private ItemRowViewModel? _selectedItem;
    private ItemType? _selectedCatalogEntry;
    private string _catalogFilter = "";
    private string _inventoryNote = "";

    private string _conditionSummary = "—";
    private bool _isAfflicted;

    private string _characterName = "";
    private string _portraitId = "";
    private string _raceName = "";
    private int _level;
    private long _experience;
    private long _experienceForNextLevel;
    private int _health;
    private int _mana;
    private long _gold;
    private int _fame;
    private long _crime;
    private int _attributePoints;
    private int _skillPoints;

    /// <summary>Builds the session and starts the refresh timer.</summary>
    public MainViewModel()
    {
        Attributes = new ObservableCollection<AttributeRowViewModel>();
        Skills = new ObservableCollection<SkillRowViewModel>();
        Items = new ObservableCollection<ItemRowViewModel>();
        CatalogView = new ObservableCollection<ItemType>();
        Processes = new ObservableCollection<ProcessEntry>();
        Reference = new ObservableCollection<ReferenceRow>();
        Map = new MapViewModel(this);

        AttachCommand = new RelayCommand(Attach, () => !IsAttached);
        DetachCommand = new RelayCommand(Detach, () => IsAttached);
        RefreshProcessesCommand = new RelayCommand(RefreshProcesses);
        MaxSkillsCommand = new RelayCommand(MaxSkills, () => IsAttached && !IsReadOnly);
        LevelUpCommand = new RelayCommand(LevelUp, () => IsAttached && !IsReadOnly);
        ClearCrimeCommand = new RelayCommand(ClearCrime, () => IsAttached && !IsReadOnly);
        CureConditionsCommand = new RelayCommand(CureConditions, () => CanEdit && IsAfflicted);
        RestoreItemCommand = new RelayCommand(RestoreSelectedItem, () => CanEdit && SelectedItem is { CanRestore: true });
        RestoreAllItemsCommand = new RelayCommand(RestoreAllItems, () => CanEdit && Items.Count > 0);
        ReplaceItemCommand = new RelayCommand(ReplaceSelectedItem,
            () => CanEdit && SelectedItem is { IsEquipped: false } && SelectedCatalogEntry is not null);
        ScanCatalogCommand = new RelayCommand(ScanCatalog, () => IsAttached);
        ScanMapsCommand = new RelayCommand(ScanMaps, () => IsAttached);

        // Built up front rather than on attach, so the window shows what it can edit before it is
        // pointed at anything. The rows carry no game state until the first refresh fills them.
        EnsureRows();
        BuildReference();
        UpdateInventoryNote();
        RefreshProcesses();

        _timer = new DispatcherTimer { Interval = Tick };
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();
        TryAutoAttach();
    }

    /// <summary>On startup, attach automatically when the pre-selected process is the game. Stays a no-op (just the populated process list) when the game is not running, rather than attaching to some unrelated process and scanning it fruitlessly.</summary>
    private void TryAutoAttach()
    {
        if (!IsAttached && SelectedProcess?.Match == ProcessMatch.Exact) Attach();
    }

    // ---- collections and commands ----------------------------------------------------------

    /// <summary>The five base attributes.</summary>
    public ObservableCollection<AttributeRowViewModel> Attributes { get; }

    /// <summary>The twenty skills, in the game's id order.</summary>
    public ObservableCollection<SkillRowViewModel> Skills { get; }

    /// <summary>The character's carried items, in the game's own order.</summary>
    public ObservableCollection<ItemRowViewModel> Items { get; }

    /// <summary>The item types offered by the replacement picker, filtered by <see cref="CatalogFilter"/>.</summary>
    public ObservableCollection<ItemType> CatalogView { get; }

    /// <summary>Attachable processes, best match first.</summary>
    public ObservableCollection<ProcessEntry> Processes { get; }

    /// <summary>Static reference data for the Reference tab.</summary>
    public ObservableCollection<ReferenceRow> Reference { get; }

    /// <summary>Where the player is, where they could be, and the one write that moves them.</summary>
    public MapViewModel Map { get; }

    /// <summary>Attaches to the selected process.</summary>
    public RelayCommand AttachCommand { get; }

    /// <summary>Releases the process handle and thaws every freeze.</summary>
    public RelayCommand DetachCommand { get; }

    /// <summary>Re-enumerates processes.</summary>
    public RelayCommand RefreshProcessesCommand { get; }

    /// <summary>Raises every skill to the game's own cap.</summary>
    public RelayCommand MaxSkillsCommand { get; }

    /// <summary>Advances the character one level, keeping the experience fields consistent.</summary>
    public RelayCommand LevelUpCommand { get; }

    /// <summary>Sets crime to zero.</summary>
    public RelayCommand ClearCrimeCommand { get; }

    /// <summary>Cures poison, disease, curse and paralysis.</summary>
    public RelayCommand CureConditionsCommand { get; }

    /// <summary>Repairs, recharges or refills the selected item.</summary>
    public RelayCommand RestoreItemCommand { get; }

    /// <summary>Does the same to every carried item that has something to restore.</summary>
    public RelayCommand RestoreAllItemsCommand { get; }

    /// <summary>Turns the selected item into the selected catalog entry.</summary>
    public RelayCommand ReplaceItemCommand { get; }

    /// <summary>Sweeps the game's heap for item types again.</summary>
    public RelayCommand ScanCatalogCommand { get; }

    /// <summary>Re-reads the world's map list and the world map picture.</summary>
    public RelayCommand ScanMapsCommand { get; }

    // ---- session state ---------------------------------------------------------------------

    /// <summary>Latest status line.</summary>
    public string Status { get => _status; private set => SetField(ref _status, value); }

    /// <summary>What the attached build looks like, and whether it is the one the offsets came from.</summary>
    public string BuildNote { get => _buildNote; private set => SetField(ref _buildNote, value); }

    /// <inheritdoc/>
    public bool IsAttached
    {
        get => _isAttached;
        private set
        {
            if (!SetField(ref _isAttached, value)) return;
            OnPropertyChanged(nameof(CanEdit));
            RaiseCommandStates();
        }
    }

    /// <summary>Safety catch: when on, every write is refused and nothing touches the game.</summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            if (!SetField(ref _isReadOnly, value)) return;
            if (_actions is not null) _actions.ReadOnly = value;
            if (value)
            {
                _freezes.ThawAll();
                RaiseFreezeStates();
            }
            OnPropertyChanged(nameof(CanEdit));
            RaiseCommandStates();
        }
    }

    /// <summary>Whether the editors should be enabled.</summary>
    public bool CanEdit => IsAttached && !IsReadOnly;

    /// <summary>Process chosen in the picker.</summary>
    public ProcessEntry? SelectedProcess { get => _selectedProcess; set => SetField(ref _selectedProcess, value); }

    /// <summary>Address the record was found at, for the status bar.</summary>
    public string RecordAddress => _record == 0 ? "—" : $"0x{_record:X8}";

    /// <summary>
    /// Set by the window to a probe over <c>FocusManager</c>'s logical focus. Defaults to "no", so a
    /// headless test sees a plain refresh.
    /// </summary>
    public Func<bool>? EditorFocusProbe { get; set; }

    /// <inheritdoc/>
    public bool EditorHasFocus => EditorFocusProbe?.Invoke() ?? false;

    // ---- character scalars ------------------------------------------------------------------

    /// <summary>Character name, as the game holds it.</summary>
    public string CharacterName { get => _characterName; private set => SetField(ref _characterName, value); }

    /// <summary>Portrait resource id — the only place the game records which face was chosen.</summary>
    public string PortraitId { get => _portraitId; private set => SetField(ref _portraitId, value); }

    /// <summary>Race name.</summary>
    public string RaceName { get => _raceName; private set => SetField(ref _raceName, value); }

    /// <summary>Character level. Writing it also fixes experience and the cached next-level threshold.</summary>
    public int Level
    {
        get => _level;
        set
        {
            int previous = _level;
            if (!SetField(ref _level, value)) return;
            // A level edit is the one write that moves three fields — the level, the experience
            // floor for it, and the game's cached next-level threshold. Resyncing only the level
            // would leave the Experience box showing the pre-edit number for as long as any editor
            // in the window holds focus, so the whole sheet is re-read instead.
            if (Apply(() => _actions!.SetLevel(_record, value), ref _level, previous, nameof(Level), w => (int)w))
                Refresh(initial: true);
        }
    }

    /// <summary>Total experience.</summary>
    public long Experience
    {
        get => _experience;
        set
        {
            long previous = _experience;
            if (!SetField(ref _experience, value)) return;
            Apply(() => _actions!.SetExperience(_record, value), ref _experience, previous, nameof(Experience), w => w);
        }
    }

    /// <summary>The threshold the game has cached for the next level. Read-only; set it via the level.</summary>
    public long ExperienceForNextLevel
    {
        get => _experienceForNextLevel;
        private set => SetField(ref _experienceForNextLevel, value);
    }

    /// <summary>Current health. The maximum is derived by the game and is not stored anywhere.</summary>
    public int Health
    {
        get => _health;
        set
        {
            int previous = _health;
            if (!SetField(ref _health, value)) return;
            Apply(() => _actions!.SetHealth(_record, value), ref _health, previous, nameof(Health),
                  w => (int)w, FrozenField.Health);
        }
    }

    /// <summary>Current mana.</summary>
    public int Mana
    {
        get => _mana;
        set
        {
            int previous = _mana;
            if (!SetField(ref _mana, value)) return;
            Apply(() => _actions!.SetMana(_record, value), ref _mana, previous, nameof(Mana),
                  w => (int)w, FrozenField.Mana);
        }
    }

    /// <summary>Gold.</summary>
    public long Gold
    {
        get => _gold;
        set
        {
            long previous = _gold;
            if (!SetField(ref _gold, value)) return;
            Apply(() => _actions!.SetGold(_record, value), ref _gold, previous, nameof(Gold),
                  w => w, FrozenField.Gold);
        }
    }

    /// <summary>Fame, -100..+100.</summary>
    public int Fame
    {
        get => _fame;
        set
        {
            int previous = _fame;
            if (!SetField(ref _fame, value)) return;
            Apply(() => _actions!.SetFame(_record, value), ref _fame, previous, nameof(Fame), w => (int)w);
            OnPropertyChanged(nameof(FameBand));
        }
    }

    /// <summary>Reputation word for the current fame.</summary>
    public string FameBand => GameTables.FameBand(_fame);

    /// <summary>Outstanding crime.</summary>
    public long Crime
    {
        get => _crime;
        set
        {
            long previous = _crime;
            if (!SetField(ref _crime, value)) return;
            Apply(() => _actions!.SetCrime(_record, value), ref _crime, previous, nameof(Crime),
                  w => w, FrozenField.Crime);
        }
    }

    /// <summary>Unspent attribute points.</summary>
    public int AttributePoints
    {
        get => _attributePoints;
        set
        {
            int previous = _attributePoints;
            if (!SetField(ref _attributePoints, value)) return;
            Apply(() => _actions!.SetAttributePoints(_record, value), ref _attributePoints, previous,
                  nameof(AttributePoints), w => (int)w);
        }
    }

    /// <summary>Unspent skill points.</summary>
    public int SkillPoints
    {
        get => _skillPoints;
        set
        {
            int previous = _skillPoints;
            if (!SetField(ref _skillPoints, value)) return;
            Apply(() => _actions!.SetSkillPoints(_record, value), ref _skillPoints, previous,
                  nameof(SkillPoints), w => (int)w);
        }
    }

    // ---- conditions ---------------------------------------------------------------------------

    /// <summary>
    /// What is wrong with the character, one line per affliction, or "None." when nothing is.
    /// Read-only: a condition is a list of effect objects, so there is no number to type into.
    /// </summary>
    public string ConditionSummary { get => _conditionSummary; private set => SetField(ref _conditionSummary, value); }

    /// <summary>Whether the character has any of the four adverse conditions right now.</summary>
    public bool IsAfflicted
    {
        get => _isAfflicted;
        private set
        {
            if (!SetField(ref _isAfflicted, value)) return;
            CureConditionsCommand.RaiseCanExecuteChanged();
        }
    }

    private void CureConditions()
    {
        if (_actions is null) { Report("Attach first."); return; }
        Report(_actions.CureConditions(_record).Message);
        Refresh(initial: true);
    }

    private void UpdateConditions()
    {
        var conditions = _source is null || _record == 0 ? null : ConditionReader.Read(_source, _record);

        // A read that failed is *not* reported as a clean bill of health. The structures are the
        // ones the cure writes to, so "they did not read back as what they should be" is exactly
        // when the trainer must say nothing and refuse rather than show "None."
        ConditionSummary = conditions?.Summary ?? "Could not be read.";
        IsAfflicted = conditions?.Any ?? false;
    }

    // ---- inventory ----------------------------------------------------------------------------

    /// <summary>The item the Inventory tab is acting on.</summary>
    public ItemRowViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!SetField(ref _selectedItem, value)) return;
            RaiseItemCommandStates();
        }
    }

    /// <summary>The catalog entry "Replace" would stamp onto <see cref="SelectedItem"/>.</summary>
    public ItemType? SelectedCatalogEntry
    {
        get => _selectedCatalogEntry;
        set
        {
            if (!SetField(ref _selectedCatalogEntry, value)) return;
            ReplaceItemCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Substring the picker is narrowed by; matched against both the name and the internal id.</summary>
    public string CatalogFilter
    {
        get => _catalogFilter;
        set
        {
            if (!SetField(ref _catalogFilter, value)) return;
            ApplyCatalogFilter();
        }
    }

    /// <summary>Line under the item list: how much is carried, and how big the catalog is.</summary>
    public string InventoryNote { get => _inventoryNote; private set => SetField(ref _inventoryNote, value); }

    /// <summary>
    /// Sweeps the game's heap for item types.
    ///
    /// Run once on attach and then only on request. It costs about a third of a second, which is far
    /// too much for the 250 ms refresh, and the answer barely moves: types are loaded with the game's
    /// data, so the only thing that changes the catalog mid-session is the game loading an area from
    /// an expansion it had not touched yet.
    /// </summary>
    public void ScanCatalog()
    {
        if (_source is null || _record == 0) { Report("Attach first."); return; }

        _catalog = ItemCatalog.Sweep(_source, _record - QuestLayout.RecordInEngine);
        ApplyCatalogFilter();
        UpdateInventoryNote();
        Report($"Found {_catalog.Count:N0} item type(s) in the loaded game.");
    }

    /// <summary>
    /// Rebuilds <see cref="CatalogView"/> from the filter, ordered by category and then by name so
    /// an unfiltered thousand-entry list is still something a person can read.
    /// </summary>
    private void ApplyCatalogFilter()
    {
        var previous = SelectedCatalogEntry;
        string needle = _catalogFilter.Trim();

        var matches = _catalog
            .Where(t => needle.Length == 0
                     || t.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                     || t.Id.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(t => t.Id, StringComparer.Ordinal);

        CatalogView.Clear();
        foreach (var type in matches) CatalogView.Add(type);

        // Keep the selection if it survived the filter, so typing to narrow the list does not
        // silently disarm the Replace button.
        SelectedCatalogEntry = previous is not null && CatalogView.Contains(previous) ? previous : null;
    }

    /// <summary>
    /// Brings <see cref="Items"/> into line with a fresh read.
    ///
    /// Rows are matched to items by address, not by position: the vector closes up when the player
    /// drops or sells something, so position 3 is a different item afterwards. When the addresses
    /// still line up — overwhelmingly the common case — the rows are updated in place and the grid's
    /// selection and scroll position survive.
    /// </summary>
    private void UpdateItems(InventorySnapshot inventory, bool initial)
    {
        bool sameShape = Items.Count == inventory.Items.Count;
        if (sameShape)
        {
            for (int i = 0; i < Items.Count; i++)
                if (Items[i].Address != inventory.Items[i].Address) { sameShape = false; break; }
        }

        if (sameShape)
        {
            for (int i = 0; i < Items.Count; i++) Items[i].Update(inventory.Items[i], initial);
            UpdateInventoryNote();
            // Restoring an item to full is exactly the case that leaves the button enabled with
            // nothing left to do, so the commands are re-queried even when no row came or went.
            RaiseItemCommandStates();
            return;
        }

        uint? selected = SelectedItem?.Address;
        var existing = Items.ToDictionary(r => r.Address);

        Items.Clear();
        foreach (var item in inventory.Items)
        {
            if (existing.TryGetValue(item.Address, out var row)) row.Update(item, initial: true);
            else row = new ItemRowViewModel(this, item);
            Items.Add(row);
        }

        SelectedItem = selected is { } address ? Items.FirstOrDefault(r => r.Address == address) : null;
        UpdateInventoryNote();
        RaiseItemCommandStates();
    }

    private void UpdateInventoryNote()
    {
        int count = Items.Count;
        string catalog = _catalog.Count == 0
            ? "Catalog not scanned yet."
            : $"{_catalog.Count:N0} item type(s) available to place.";
        InventoryNote = $"{count} item(s) carried. {catalog}";
    }

    private void RestoreSelectedItem()
    {
        if (SelectedItem is not { } row) { Report("Pick an item first."); return; }
        row.Restore();
        Refresh(initial: true);
    }

    private void RestoreAllItems()
    {
        if (_actions is null) { Report("Attach first."); return; }
        Report(_actions.RestoreAllItems(_record).Message);
        Refresh(initial: true);
    }

    private void ReplaceSelectedItem()
    {
        if (_actions is null) { Report("Attach first."); return; }
        if (SelectedItem is not { } row) { Report("Pick an item to replace."); return; }
        if (SelectedCatalogEntry is not { } type) { Report("Pick what to replace it with."); return; }

        Report(_actions.ReplaceItem(_record, row.Address, type).Message);
        Refresh(initial: true);
    }

    private void RaiseItemCommandStates()
    {
        RestoreItemCommand.RaiseCanExecuteChanged();
        RestoreAllItemsCommand.RaiseCanExecuteChanged();
        ReplaceItemCommand.RaiseCanExecuteChanged();
        ScanCatalogCommand.RaiseCanExecuteChanged();
    }

    // ---- the map -----------------------------------------------------------------------------

    /// <summary>
    /// Re-reads the world's map list and its map picture.
    ///
    /// Both are paid on attach and then only on request, for the same reason the item catalog is:
    /// the atlas is four reads for each of a couple of hundred maps and the picture is a zip lookup
    /// and a block decode, neither of which belongs on a 250 ms timer. Neither is needed for the
    /// position readout or for a teleport — they are the reference half of the tab — so a failure is
    /// reported and nothing else stops working.
    /// </summary>
    public void ScanMaps()
    {
        if (_source is null || _record == 0)
        {
            Map.SetAtlas(Array.Empty<WorldMap>());
            Map.SetPicture(null, "Attach first.");
            return;
        }

        var atlas = MapReader.ReadAtlas(_source, _record);
        Map.SetAtlas(atlas);

        var where = MapReader.Read(_source, _record);
        if (where is null)
        {
            Map.SetPicture(null, "No world is loaded, so there is no map picture to read.");
            return;
        }

        // The picture covers the outdoor grid, so its scale comes from how wide that grid is — which
        // the atlas has just told us, rather than a constant that would be wrong for the expansion.
        int cells = atlas.Where(m => m.Column is not null).Select(m => m.Column!.Value).DefaultIfEmpty(0).Max();
        var picture = WorldPictureLoader.Load(_gameFolder, where.WorldPack, where.PictureId,
                                              cells * MapLayout.GridMapTiles, out string note);
        Map.SetPicture(picture, note);
    }

    /// <summary>
    /// The folder the attached game runs from, which is where its <c>.pak</c> files are.
    ///
    /// Unlike the DOS trainers in this repository, the attached process <i>is</i> the game, so there
    /// is nothing to ask the user for. Reading the path can still fail — a 32-bit process queried
    /// from a 64-bit one, or one that exits between the two calls — and that is not fatal.
    /// </summary>
    private static string? FolderOf(Process process)
    {
        try { return Path.GetDirectoryName(process.MainModule?.FileName); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { return null; }
    }

    // ---- freezes ------------------------------------------------------------------------------

    /// <summary>Holds health at whatever it was when the box was ticked.</summary>
    public bool FreezeHealth { get => _freezes.IsFrozen(FrozenField.Health); set => SetFreeze(FrozenField.Health, value, _health); }

    /// <summary>Holds mana at whatever it was when the box was ticked.</summary>
    public bool FreezeMana { get => _freezes.IsFrozen(FrozenField.Mana); set => SetFreeze(FrozenField.Mana, value, _mana); }

    /// <summary>Holds gold at whatever it was when the box was ticked.</summary>
    public bool FreezeGold { get => _freezes.IsFrozen(FrozenField.Gold); set => SetFreeze(FrozenField.Gold, value, _gold); }

    /// <summary>Holds crime at whatever it was when the box was ticked — usually zero.</summary>
    public bool FreezeCrime { get => _freezes.IsFrozen(FrozenField.Crime); set => SetFreeze(FrozenField.Crime, value, _crime); }

    /// <summary>
    /// Re-runs the cure on every tick, so an adverse condition is taken off within a quarter of a
    /// second of the game inflicting it. There is no value to latch, hence the zero.
    /// </summary>
    public bool FreezeConditions { get => _freezes.IsFrozen(FrozenField.Conditions); set => SetFreeze(FrozenField.Conditions, value, 0); }

    private void SetFreeze(FrozenField field, bool on, long latched)
    {
        if (on && (!IsAttached || IsReadOnly))
        {
            Report(IsAttached ? "Read-only mode is on; nothing was frozen." : "Attach first.");
            OnPropertyChanged(FreezeProperty(field));
            return;
        }

        if (on) _freezes.Freeze(field, latched);
        else _freezes.Thaw(field);
        OnPropertyChanged(FreezeProperty(field));
    }

    private static string FreezeProperty(FrozenField field) => field switch
    {
        FrozenField.Health => nameof(FreezeHealth),
        FrozenField.Mana => nameof(FreezeMana),
        FrozenField.Gold => nameof(FreezeGold),
        FrozenField.Conditions => nameof(FreezeConditions),
        _ => nameof(FreezeCrime),
    };

    private void RaiseFreezeStates()
    {
        OnPropertyChanged(nameof(FreezeHealth));
        OnPropertyChanged(nameof(FreezeMana));
        OnPropertyChanged(nameof(FreezeGold));
        OnPropertyChanged(nameof(FreezeCrime));
        OnPropertyChanged(nameof(FreezeConditions));
    }

    // ---- attach / detach ------------------------------------------------------------------------

    /// <summary>Re-enumerates candidate processes, keeping the current selection if it survives.</summary>
    public void RefreshProcesses()
    {
        int? previous = SelectedProcess?.Id;
        int own = Environment.ProcessId;

        var entries = new List<ProcessEntry>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (!ProcessPicker.IsSelectable(p.Id, own)) continue;
                if (ProcessPicker.Rank(p.ProcessName) == ProcessMatch.None) continue;
                entries.Add(new ProcessEntry(p.Id, p.ProcessName, SafeWindowTitle(p)));
            }
            catch (InvalidOperationException) { /* exited between enumeration and read */ }
            finally { p.Dispose(); }
        }

        var ordered = ProcessPicker.Order(entries, e => e.Match, e => e.Name).ToList();
        Processes.Clear();
        foreach (var e in ordered) Processes.Add(e);
        SelectedProcess = ProcessPicker.ChooseDefault(ordered, e => e.Match, e => e.Id, previous);

        if (ordered.Count == 0)
            Status = "The Quest does not appear to be running.";
    }

    private static string SafeWindowTitle(Process p)
    {
        try { return p.MainWindowTitle; }
        catch (InvalidOperationException) { return ""; }
    }

    /// <summary>Opens the selected process, resolves the module, and locates the character record.</summary>
    public void Attach()
    {
        if (SelectedProcess is not { } entry)
        {
            Status = "Pick a process first.";
            return;
        }

        Detach();

        Process process;
        try
        {
            process = Process.GetProcessById(entry.Id);
        }
        catch (ArgumentException)
        {
            Status = "That process is gone. Press Refresh.";
            RefreshProcesses();
            return;
        }

        ProcessMemory memory;
        try
        {
            memory = ProcessMemory.Open(entry.Id);
        }
        catch (Exception e)
        {
            process.Dispose();
            Status = $"Could not open the process: {e.Message}";
            return;
        }

        // Resolution parses headers out of arbitrary target pages, so it is wrapped: an exception
        // here would otherwise escape to the app-level handler with the process handle and the
        // memory handle already open and not yet stored in fields, i.e. leaked for the session.
        ModuleLocation module;
        try
        {
            module = ModuleResolver.Resolve(process, memory);
        }
        catch (Exception e)
        {
            memory.Dispose();
            process.Dispose();
            Status = $"Could not read the game's module: {e.Message}";
            return;
        }

        if (!module.Found)
        {
            memory.Dispose();
            process.Dispose();
            Status = module.How;
            return;
        }

        _process = process;
        _memory = memory;
        _gameFolder = FolderOf(process);
        _image = module.Image;
        _source = new ProcessMemorySource(memory, module.Base, module.Size);
        _actions = new TrainerActions(_source, _image) { ReadOnly = IsReadOnly };

        BuildNote = DescribeBuild(module);

        var located = CharacterLocator.Locate(_source, _image);
        if (!located.Found)
        {
            Status = located.Detail;
            Detach();
            return;
        }

        _record = located.Record;
        _rowsBuilt = false;
        IsAttached = true;
        OnPropertyChanged(nameof(RecordAddress));
        Status = $"Attached to {entry.Name} ({entry.Id}). {located.Detail}";
        Refresh(initial: true);

        // The catalog sweep costs about a third of a second and is what makes the Inventory tab's
        // picker useful, so it is paid once here rather than on the refresh. A failure is not fatal:
        // everything else on that tab works without it.
        _catalog = ItemCatalog.Sweep(_source, _record - QuestLayout.RecordInEngine);
        ApplyCatalogFilter();
        UpdateInventoryNote();

        ScanMaps();
    }

    private string DescribeBuild(ModuleLocation module)
    {
        if (module.Image is null) return module.How;
        string known = module.Image.TimeDateStamp == GameFacts.KnownTimeDateStamp
            ? GameFacts.KnownVersion
            : "an unrecognised build — offsets may not apply";
        string aslr = module.Image.HasAslr ? "ASLR on" : "no ASLR";
        return $"{module.How} Link stamp 0x{module.Image.TimeDateStamp:X8} — {known}. {aslr}.";
    }

    /// <summary>Releases the process handle, thaws freezes and clears the display.</summary>
    public void Detach()
    {
        _freezes.ThawAll();
        RaiseFreezeStates();

        _memory?.Dispose();
        _process?.Dispose();
        _memory = null;
        _process = null;
        _source = null;
        _actions = null;
        _image = null;
        _record = 0;
        _rowsBuilt = false;
        _gameFolder = null;

        // The item rows and the catalog both name addresses in a process that is no longer open, so
        // they go rather than linger as a list that looks live and would refuse every write.
        Items.Clear();
        CatalogView.Clear();
        _catalog = Array.Empty<ItemType>();
        SelectedItem = null;
        SelectedCatalogEntry = null;
        UpdateInventoryNote();

        ConditionSummary = "—";
        IsAfflicted = false;

        // The atlas and the schematic name maps in a process that is no longer open, and the
        // position they were drawn around is gone with it.
        Map.Clear();

        if (IsAttached) Status = "Detached.";
        IsAttached = false;
        OnPropertyChanged(nameof(RecordAddress));
    }

    /// <summary>
    /// Ends the session and leaves <paramref name="reason"/> on the status bar.
    ///
    /// The order matters and is the whole point of this method existing: <see cref="Detach"/> writes
    /// "Detached." whenever it had a session to end, so a caller that sets the reason *first* has it
    /// silently destroyed — and the reason is the only place the validator's explanation is ever
    /// shown. Every path that ends a session because something went wrong goes through here.
    /// </summary>
    private void DetachBecause(string reason)
    {
        Detach();
        Status = reason;
    }

    // ---- refresh --------------------------------------------------------------------------------

    private void OnTick()
    {
        if (!IsAttached) return;

        if (_process is { HasExited: true })
        {
            DetachBecause("The game exited.");
            return;
        }

        // Freezes go out before the refresh so the display never flickers through the damaged
        // value. If the record moved, they simply fail; the refresh below notices and says so, and
        // the next tick re-applies them at the new address.
        int missed = 0;
        if (!IsReadOnly && _freezes.Any && _actions is not null)
            missed = _freezes.Frozen.Count - _freezes.Tick(_actions, _record);

        uint before = _record;
        Refresh(initial: false);

        if (missed > 0 && IsAttached && _record == before)
            Report($"{missed} freeze(s) could not be written.");
    }

    private void Refresh(bool initial)
    {
        if (_source is null || _record == 0) return;

        // The record is re-validated, not merely re-read. A freed or replaced heap block usually
        // stays committed and readable, so "the read succeeded" is exactly the case that would
        // otherwise let the window sit there displaying whatever the stale bytes decode to while
        // every edit is silently refused.
        if (!CharacterLocator.Validate(_source, _image, _record, out string why))
        {
            if (!TryReacquire(why)) return;
            initial = true;     // a different record: every box and row must take its values
        }

        var snapshot = CharacterReader.Read(_source, _record);
        if (snapshot is null)
        {
            DetachBecause("Lost the character record — press Attach again.");
            return;
        }

        CharacterName = snapshot.Name;
        PortraitId = snapshot.PortraitId;
        RaceName = snapshot.RaceName;
        ExperienceForNextLevel = snapshot.ExperienceForNextLevel;

        if (initial || !EditorHasFocus)
        {
            SetField(ref _level, snapshot.Level, nameof(Level));
            SetField(ref _experience, snapshot.Experience, nameof(Experience));
            SetField(ref _health, snapshot.Health, nameof(Health));
            SetField(ref _mana, snapshot.Mana, nameof(Mana));
            SetField(ref _gold, snapshot.Gold, nameof(Gold));
            SetField(ref _crime, snapshot.Crime, nameof(Crime));
            SetField(ref _attributePoints, snapshot.AttributePoints, nameof(AttributePoints));
            SetField(ref _skillPoints, snapshot.SkillPoints, nameof(SkillPoints));
            if (SetField(ref _fame, snapshot.Fame, nameof(Fame))) OnPropertyChanged(nameof(FameBand));
        }

        EnsureRows();

        foreach (var row in Attributes)
            row.Update(snapshot.Attributes[row.Id], initial || !_rowsBuilt);

        foreach (var row in Skills)
        {
            int cap = GameFacts.SkillCapFor(snapshot.Attributes[row.GoverningAttribute]);
            bool available = TrainerActions.SkillAvailableTo(row.Id, snapshot.RaceId);
            row.Update(snapshot.Skills[row.Id], snapshot.StartingSkills[row.Id], cap, available, initial || !_rowsBuilt);
        }

        // Conditions are read separately for the same reason the inventory is: they are vectors of
        // heap pointers outside the snapshotted part of the record, and none of them is a field a
        // player types into, so the editor-focus rule above does not apply to them.
        UpdateConditions();

        // The position is not in the record at all — it hangs off the engine object the record is
        // embedded in — so it is read on its own, and a chain that cannot be followed (the player is
        // at the main menu) empties the tab rather than ending the session.
        Map.Update(MapReader.Read(_source, _record));

        // The inventory is read separately from the record: it is a vector of heap pointers rather
        // than fields inside the snapshot, and a pack that cannot be read is a reason to show no
        // items, not a reason to end the session.
        var inventory = InventoryReader.Read(_source, _record);
        if (inventory is not null) UpdateItems(inventory, initial || !_rowsBuilt);
        else if (Items.Count > 0) { Items.Clear(); SelectedItem = null; UpdateInventoryNote(); }

        _rowsBuilt = true;
    }

    /// <summary>
    /// The record stopped validating — a save, a load, a death or a new game replaced it. Tries the
    /// cheap chain, the module's own engine pointer, to pick up its replacement.
    ///
    /// The heap sweep is deliberately *not* run here: it takes about a second, and this is on the
    /// UI thread four times a second. If the cheap chain cannot find it, the session ends and the
    /// user is told to press Attach, which does run both chains.
    /// </summary>
    private bool TryReacquire(string why)
    {
        var again = CharacterLocator.LocateViaStaticSlot(_source!, _image);
        if (again.Found)
        {
            _record = again.Record;
            _rowsBuilt = false;
            OnPropertyChanged(nameof(RecordAddress));
            Status = $"The character record moved ({why}) — re-acquired at 0x{_record:X8}.";
            return true;
        }

        DetachBecause($"Lost the character record ({why}) — press Attach again.");
        return false;
    }

    private void EnsureRows()
    {
        if (Attributes.Count == 0)
            foreach (var a in GameTables.Attributes)
                Attributes.Add(new AttributeRowViewModel(this, a));

        if (Skills.Count == 0)
            foreach (var s in GameTables.Skills)
                Skills.Add(new SkillRowViewModel(this, s));
    }

    // ---- commands --------------------------------------------------------------------------------

    private void MaxSkills()
    {
        if (_actions is null) { Report("Attach first."); return; }
        Report(_actions.MaxSkills(_record).Message);
        Refresh(initial: true);
    }

    private void LevelUp()
    {
        if (_actions is null) { Report("Attach first."); return; }
        Report(_actions.SetLevel(_record, _level + 1).Message);
        Refresh(initial: true);
    }

    private void ClearCrime()
    {
        if (_actions is null) { Report("Attach first."); return; }
        var result = _actions.SetCrime(_record, 0);
        ReLatch(FrozenField.Crime, result);   // else the next tick puts the old bounty back
        Report(result.Message);
        Refresh(initial: true);
    }

    // ---- IGameHost -------------------------------------------------------------------------------

    /// <inheritdoc/>
    public ActionResult WriteAttribute(int id, int value) =>
        _actions is null ? ActionResult.Failure("Attach first.") : _actions.SetAttribute(_record, id, value);

    /// <inheritdoc/>
    public ActionResult WriteSkill(int id, int value) =>
        _actions is null ? ActionResult.Failure("Attach first.") : _actions.SetSkill(_record, id, value);

    /// <inheritdoc/>
    public ActionResult WriteItemMeter(uint item, int value) =>
        _actions is null ? ActionResult.Failure("Attach first.") : _actions.SetItemMeter(_record, item, value);

    /// <inheritdoc/>
    public ActionResult RestoreItem(uint item) =>
        _actions is null ? ActionResult.Failure("Attach first.") : _actions.RestoreItem(_record, item);

    /// <inheritdoc/>
    public ActionResult Teleport(int localX, int localY)
    {
        if (_actions is null) return ActionResult.Failure("Attach first.");
        var result = _actions.Teleport(_record, localX, localY);
        // The position is not a scalar with an editor, so nothing has to be settled — but the
        // readout and the schematic's marker should show the new tile now rather than in 250 ms.
        if (result.Ok && _source is not null) Map.Update(MapReader.Read(_source, _record));
        return result;
    }

    /// <inheritdoc/>
    public void Report(string message)
    {
        if (!string.IsNullOrEmpty(message)) Status = message;
    }

    // ---- plumbing ---------------------------------------------------------------------------------

    /// <summary>
    /// Runs a write and leaves the backing field showing what the game actually holds.
    ///
    /// Two ways that differs from what was typed. A refused write reverts to
    /// <paramref name="previous"/>. A write that *succeeded* may still have been clamped — every
    /// write clamps to the field it is going into — so <paramref name="fromWritten"/> converts the
    /// value that landed back into the property's type and the field is put in step with it. The
    /// refresh cannot be relied on to do this: it deliberately skips every scalar while any editor
    /// in the window has focus, so a box could keep showing 9,999,999,999 for the rest of the
    /// session after the game took 999,999,999.
    ///
    /// <paramref name="freezable"/> names the freeze that holds this field, if any. An explicit
    /// edit re-latches it — the "a freeze never re-derives its target" rule is about the refresh
    /// overwriting the display, not about the user deliberately setting a new value. Without this,
    /// typing into a frozen Gold box, or pressing Clear crime with Crime frozen, is reported as
    /// success and then silently undone a quarter of a second later.
    /// </summary>
    /// <returns>Whether the write landed, so a caller whose edit moves more than one field can
    /// follow up.</returns>
    private bool Apply<T>(Func<ActionResult> write, ref T field, T previous, string property,
                          Func<long, T>? fromWritten = null, FrozenField? freezable = null)
    {
        if (_actions is null)
        {
            field = previous;
            OnPropertyChanged(property);
            Report("Attach first.");
            return false;
        }

        var result = write();
        if (!result.Ok)
        {
            field = previous;
            OnPropertyChanged(property);
        }
        else if (result.Written is { } written)
        {
            if (fromWritten is not null)
            {
                T settled = fromWritten(written);
                if (!EqualityComparer<T>.Default.Equals(field, settled))
                {
                    field = settled;
                    OnPropertyChanged(property);
                }
            }
            if (freezable is { } f && _freezes.IsFrozen(f)) _freezes.Freeze(f, written);
        }
        Report(result.Message);
        return result.Ok;
    }

    /// <summary>
    /// Re-latches <paramref name="field"/> after a command (rather than an editor) changed it.
    /// Same reason as <see cref="Apply{T}"/>: otherwise the next tick puts the old value back.
    /// </summary>
    private void ReLatch(FrozenField field, ActionResult result)
    {
        if (result is { Ok: true, Written: { } written } && _freezes.IsFrozen(field))
            _freezes.Freeze(field, written);
    }

    private void RaiseCommandStates()
    {
        AttachCommand.RaiseCanExecuteChanged();
        DetachCommand.RaiseCanExecuteChanged();
        MaxSkillsCommand.RaiseCanExecuteChanged();
        LevelUpCommand.RaiseCanExecuteChanged();
        ClearCrimeCommand.RaiseCanExecuteChanged();
        CureConditionsCommand.RaiseCanExecuteChanged();
        ScanMapsCommand.RaiseCanExecuteChanged();
        Map.RaiseCommandStates();
        RaiseItemCommandStates();
    }

    private void BuildReference()
    {
        Reference.Add(new ReferenceRow("— Attributes —", "", ""));
        foreach (var a in GameTables.Attributes)
            Reference.Add(new ReferenceRow(a.Name, $"id {a.Id}", a.Effect));

        Reference.Add(new ReferenceRow("— Skills —", "", "A skill's base value caps at twice its governing attribute."));
        foreach (var s in GameTables.Skills)
            Reference.Add(new ReferenceRow(s.Name, $"id {s.Id} · {GameTables.Attribute(s.GoverningAttribute)?.Name}", s.Effect));

        Reference.Add(new ReferenceRow("— Races —", "", "Race id 0 is the engine's placeholder for non-player creatures."));
        for (int i = 0; i < GameTables.Races.Count; i++)
            Reference.Add(new ReferenceRow(GameTables.Races[i], $"id {i}", ""));

        Reference.Add(new ReferenceRow("— Conditions —", "",
            "The four the game names. Each is a list of effect objects, not a flag, which is why the trainer cures rather than clears a bit."));
        foreach (var c in ConditionTables.All)
            Reference.Add(new ReferenceRow(ConditionTables.Name(c),
                ConditionTables.EffectKind(c) is { } kind ? $"effect kind 0x{kind:X2}" : "its own list",
                ConditionTables.Effect(c)));

        Reference.Add(new ReferenceRow("— Reputation —", "", "Fame runs -100 to +100."));
        foreach (int f in new[] { 100, 80, 50, 20, 1, 0, -1, -20, -50, -80, -100 })
            Reference.Add(new ReferenceRow(GameTables.FameBand(f), f.ToString(), ""));

        Reference.Add(new ReferenceRow("— Wardrobe —", "", "Outfit is summed from what is worn; the trainer cannot set it."));
        foreach (int o in new[] { 0, 11, 21, 41, 61, 81, 91, 96 })
            Reference.Add(new ReferenceRow(GameTables.OutfitBand(o), $"{o}+", ""));
    }

    /// <summary>Stops the timer and releases the process handle.</summary>
    public void Dispose()
    {
        _timer.Stop();
        Detach();
    }
}
