using System.Collections.ObjectModel;
using System.Diagnostics;
using BardsTaleTrilogyTrainer.Cluebooks;
using BardsTaleTrilogyTrainer.Game;
using BardsTaleTrilogyTrainer.Memory;

namespace BardsTaleTrilogyTrainer.ViewModels;

/// <summary>
/// Top-level view model. Handles process attachment, auto-locate, the character list, the
/// party purse, spell assignment, item charges — and hosts the Maps tab, which shows where the
/// party is standing and teleports it anywhere in the trilogy.
/// </summary>
public sealed class MainViewModel : ObservableObject, ICharacterHost, IDisposable
{
    /// <summary>How often frozen values are re-written and the party position re-read.</summary>
    private const int PollIntervalMs = 400;

    private ProcessMemory? _proc;
    private ProcessMemorySource? _mem;
    private GameLocation? _location;
    private MapNavigator? _navigator;
    private MapArchive? _archive;
    private nuint _moduleBase;
    private nuint _moduleSize;
    private System.Threading.Timer? _pollTimer;

    private Il2CppRuntime? _runtime;
    private SpellCatalog _spells = SpellCatalog.Empty;

    private string _statusMessage = "Attach to the game to begin.";
    private bool _isAttached;
    private bool _isLocating;
    private int _selectedCharacterIndex = -1;
    private long _gold;
    private string _spellCode = "";
    private bool _allowGameAllocation = true;
    private string _runtimeStatus = "Not attached.";

    public MainViewModel()
    {
        Maps = new MapsViewModel(() => _navigator, () => _archive, OnMessage);
        Cluebook = new CluebookViewModel();

        // The map files belong to the installation, not to a running process, so the Maps tab
        // is useful before anything is attached.
        OpenMapArchive(null);

        AttachCommand = new RelayCommand(_ => Attach(), _ => !IsAttached);
        LocateCommand = new RelayCommand(_ => Locate(), _ => IsAttached && !IsLocating);
        // `_location?.PartyObject != 0` would be a lifted comparison that is *true* when
        // _location is null, enabling the button before any locate has run.
        WriteGoldCommand = new RelayCommand(_ => WriteGold(), _ => IsAttached && _location is { PartyObject: not 0 });
        AssignSpellCommand = new RelayCommand(_ => AssignSpell(),
            _ => IsAttached && SelectedCharacter != null && !string.IsNullOrWhiteSpace(SpellCode));
        LearnAllSpellsAllCommand = new RelayCommand(_ => LearnAllSpellsAll(), _ => IsAttached && Characters.Count > 0);
        SetInfiniteItemsAllCommand = new RelayCommand(_ => SetInfiniteItemsAll(), _ => IsAttached && Characters.Count > 0);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);

        GrantSpellCommand = new RelayCommand(GrantSpell, _ => IsAttached && SelectedCharacter != null);
        GrantSpecialSpellsCommand = new RelayCommand(_ => SelectedCharacter?.GrantSpecialSpells(),
            _ => IsAttached && SelectedCharacter != null);
        GrantSpecialSpellsAllCommand = new RelayCommand(_ => GrantSpecialSpellsAll(),
            _ => IsAttached && Characters.Count > 0);
        RevokeSpellCommand = new RelayCommand(RevokeSpell, _ => IsAttached && SelectedCharacter != null);
    }

    public ObservableCollection<CharacterViewModel> Characters { get; } = new();

    public CharacterViewModel? SelectedCharacter =>
        _selectedCharacterIndex >= 0 && _selectedCharacterIndex < Characters.Count
            ? Characters[_selectedCharacterIndex] : null;

    /// <summary>The Maps tab: every area of the trilogy, the live marker, and teleport.</summary>
    public MapsViewModel Maps { get; }

    public CluebookViewModel Cluebook { get; }

    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

    public bool IsAttached
    {
        get => _isAttached;
        set { SetField(ref _isAttached, value); RaiseAllCanExecuteChanged(); }
    }

    public bool IsLocating
    {
        get => _isLocating;
        set { SetField(ref _isLocating, value); RaiseAllCanExecuteChanged(); }
    }

    public int SelectedCharacterIndex
    {
        get => _selectedCharacterIndex;
        set
        {
            SetField(ref _selectedCharacterIndex, value);
            OnPropertyChanged(nameof(SelectedCharacter));
            RaiseAllCanExecuteChanged();
        }
    }

    /// <summary>The party purse — a 64-bit field on <c>Party</c>.</summary>
    public long Gold { get => _gold; set => SetField(ref _gold, value); }

    public string SpellCode { get => _spellCode; set => SetField(ref _spellCode, value); }

    // --- spells -----------------------------------------------------------------
    /// <summary>
    /// The game's own spell table. Read from <c>GlobalSpells.Instance</c> at locate time, which
    /// is the only accurate source for a spell's code, school and level — those live in the
    /// game's serialized assets, not in its code.
    /// </summary>
    public SpellCatalog Spells
    {
        get => _spells;
        private set
        {
            SetField(ref _spells, value);
            OnPropertyChanged(nameof(SpellTable));
            OnPropertyChanged(nameof(SpecialSpellRows));
            OnPropertyChanged(nameof(SpellTableStatus));
        }
    }

    /// <summary>Every spell in the game's table, for the reference grid.</summary>
    public IReadOnlyList<SpellEntry> SpellTable => Spells.All;

    /// <summary>
    /// The spells no school level can grant. Read from the game once located; before that, the
    /// four cross-game ones are still offered, because their ids come from the enum rather than
    /// from the table.
    /// </summary>
    public IReadOnlyList<SpecialSpellRow> SpecialSpellRows =>
        Spells.IsLive
            ? Spells.Special
                .Select(e => new SpecialSpellRow(e.Id, e.Code, e.Name, e.Games, e.Cost))
                .ToList()
            : SpecialSpells.All
                .Select(e => new SpecialSpellRow(e.Id, e.Code, e.Name, e.Note, 0))
                .ToList();

    /// <summary>Whether the spell table came from the game or is still the offline fallback.</summary>
    public string SpellTableStatus => Spells.IsLive
        ? $"{Spells.All.Count} spells read from the game, {Spells.Special.Count} of them grantable only outright."
        : "Not read yet — attach and locate to pull the game's own spell table. " +
          "The four cross-game spells below can still be granted.";

    /// <summary>
    /// Whether the trainer may ask the game to allocate when a learnt-spell list has no room
    /// left. Turning this off keeps the trainer to plain reads and writes, at the cost of not
    /// being able to teach a character whose list is full — which a fresh character's always is.
    /// </summary>
    public bool AllowGameAllocation
    {
        get => _allowGameAllocation;
        set
        {
            if (!SetField(ref _allowGameAllocation, value)) return;
            UpdateRuntime();
        }
    }

    /// <summary>What the growth path can currently do, shown next to its toggle.</summary>
    public string RuntimeStatus { get => _runtimeStatus; private set => SetField(ref _runtimeStatus, value); }

    /// <summary>The injection helper, or null when it is off or unavailable.</summary>
    public Il2CppRuntime? Runtime => _allowGameAllocation ? _runtime : null;

    // --- commands ---------------------------------------------------------------
    public RelayCommand AttachCommand { get; }
    public RelayCommand LocateCommand { get; }
    public RelayCommand WriteGoldCommand { get; }
    public RelayCommand AssignSpellCommand { get; }
    public RelayCommand LearnAllSpellsAllCommand { get; }
    public RelayCommand SetInfiniteItemsAllCommand { get; }
    public RelayCommand DetachCommand { get; }

    /// <summary>Grants one spell, passed as a <see cref="SpecialSpellRow"/> or a <see cref="SpellEntry"/>.</summary>
    public RelayCommand GrantSpellCommand { get; }

    /// <summary>Grants ZZGO, NUKE, GILL and DIVA to the selected character.</summary>
    public RelayCommand GrantSpecialSpellsCommand { get; }

    /// <summary>Grants the cross-game spells to every located character.</summary>
    public RelayCommand GrantSpecialSpellsAllCommand { get; }

    /// <summary>Removes a granted spell, passed as a <see cref="LearntSpellViewModel"/>.</summary>
    public RelayCommand RevokeSpellCommand { get; }

    private void Attach()
    {
        // Disposed on the way out: reading .Modules opens native handles, and the trainer holds
        // the game open through its own ProcessMemory rather than through this object.
        using var proc = GameLocator.FindGameProcess();
        if (proc == null)
        {
            StatusMessage = $"Process '{GameFacts.ProcessName}.exe' not found. Start the game first.";
            return;
        }

        try
        {
            _proc = ProcessMemory.Open(proc.Id);
            try
            {
                _mem = new ProcessMemorySource(_proc);
                _moduleBase = GameLocator.FindModuleBase(proc, GameFacts.GameModuleName);
                _moduleSize = GameLocator.FindModuleSize(proc, GameFacts.GameModuleName);
                OpenMapArchive(proc);

                // Last, and only once nothing above can still throw: the inner catch tears the
                // memory source back down, and a view left reporting "attached" with no source
                // behind it just makes Locate return without saying why.
                IsAttached = true;

                StatusMessage = _moduleBase != 0
                    ? $"Attached to PID {proc.Id}. {GameFacts.GameModuleName} @ 0x{_moduleBase:X}. Click Locate."
                    : $"Attached to PID {proc.Id}, but {GameFacts.GameModuleName} was not found — " +
                      "locate will fall back to scanning.";

                UpdateRuntime();
                _pollTimer = new System.Threading.Timer(_ => PollCallback(), null, PollIntervalMs, PollIntervalMs);
            }
            catch
            {
                _mem?.Dispose();
                _mem = null;
                _proc.Dispose();
                _proc = null;
                throw;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Attach failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Opens the installed game's <c>resources.assets</c> so the Maps tab can draw real terrain.
    /// This depends on the installation, not the running process, so it is done at start-up from
    /// the usual install locations and only redone on attach if the running game turns out to
    /// live somewhere else. Failing here is not fatal — the catalogue and teleport still work.
    /// </summary>
    private void OpenMapArchive(Process? proc)
    {
        string? dir = GameLocator.FindGameDirectory(proc);
        if (dir == null)
        {
            if (_archive != null) return;    // keep whatever is already open
            Maps.OnArchiveOpened(null,
                "could not work out where the game is installed, so map terrain is unavailable. " +
                "The map list itself still works.");
            return;
        }

        // Already reading this installation: nothing to do.
        if (_archive != null &&
            _archive.Path.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) return;

        _archive?.Dispose();
        _archive = MapArchive.TryOpen(dir, out string error);
        Maps.OnArchiveOpened(_archive, error);
    }

    private void Locate()
    {
        if (_mem == null) return;
        IsLocating = true;
        StatusMessage = "Resolving the game's classes and finding the party…";

        var mem = _mem;
        nuint moduleBase = _moduleBase, moduleSize = _moduleSize;

        Task.Run(() =>
        {
            try
            {
                var found = GameLocator.Locate(mem, moduleBase, moduleSize);
                System.Windows.Application.Current?.Dispatcher.Invoke(() => ApplyLocation(mem, found));
            }
            catch (OperationCanceledException)
            {
                Report("Scan cancelled.");
            }
            catch (Exception ex)
            {
                Report($"Scan failed: {ex.Message}");
            }
        });

        void Report(string message) =>
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                IsLocating = false;
                StatusMessage = message;
            });
    }

    private void ApplyLocation(IMemorySource mem, GameLocation? found)
    {
        IsLocating = false;
        if (!ReferenceEquals(mem, _mem)) return;      // detached while the scan was running

        _location = found;
        Characters.Clear();
        _navigator = null;

        if (found == null)
        {
            StatusMessage = "Nothing found. Load a saved game (or start one) so the party exists, then locate again.";
            RaiseAllCanExecuteChanged();
            return;
        }

        if (found.Classes.HasMapClasses)
            _navigator = new MapNavigator(mem, found.Classes);

        // The spell table is what turns a four-letter code into a spell id, and it is the only
        // honest source for a spell's school and level, so read it as soon as the classes resolve.
        Spells = SpellCatalog.Read(mem, found.Classes.GlobalSpells);

        int slot = 0;
        foreach (var addr in found.CharacterAddresses)
            Characters.Add(new CharacterViewModel(new CharacterRecord(mem, addr, slot++), this));

        if (Characters.Count > 0)
        {
            SelectedCharacterIndex = 0;
            ReadGold();
        }

        Maps.Tick();

        string maps = found.Classes.HasMapClasses
            ? "Player and GlobalMaps resolved — the Maps tab is live."
            : "The map classes could not be resolved, so location and teleport are unavailable.";
        StatusMessage = $"{found.Summary}. {Characters.Count} character(s) loaded. {maps}";
        RaiseAllCanExecuteChanged();
    }

    private void ReadGold()
    {
        if (_mem == null || _location == null || _location.PartyObject == 0) return;
        Gold = _mem.ReadI64(_location.PartyObject + (nuint)CharacterFormat.PartyGold);
    }

    private void WriteGold()
    {
        if (_mem == null || _location == null || _location.PartyObject == 0) return;
        OnMessage(_mem.WriteI64(_location.PartyObject + (nuint)CharacterFormat.PartyGold, _gold)
            ? $"Party gold set to {_gold:N0}"
            : "Failed to write the party purse.");
    }

    /// <summary>
    /// Grants the school a spell belongs to, at the level that spell needs. The remaster ties
    /// spell knowledge to <c>m_spellLevel</c> indexed by class, so raising the school is what
    /// actually teaches the spell.
    /// </summary>
    private void AssignSpell()
    {
        var chr = SelectedCharacter;
        if (chr == null) return;

        string code = SpellCode.Trim();

        // Prefer the game's own table; fall back to the four cross-game codes so ZZGO and NUKE
        // can still be typed in before a locate has run.
        var entry = Spells.FindByCode(code);
        if (entry == null)
        {
            var special = SpecialSpells.FindByCode(code);
            if (special == null)
            {
                OnMessage(Spells.IsLive
                    ? $"Unknown spell code '{code}'. The Spells tab lists every code the game knows."
                    : $"Unknown spell code '{code}'. Locate the party first to read the game's spell " +
                      "table, or use ZZGO, NUKE, GILL or DIVA.");
                return;
            }
            chr.GrantSpell(special.Id, $"{special.Code} — {special.Name}");
            return;
        }

        // A spell with no school level can only be held in the learnt list.
        if (entry.IsSpecial)
        {
            chr.GrantSpell(entry.Id, entry.Display);
            return;
        }

        var row = chr.SpellLevels.FirstOrDefault(r => r.ClassId == entry.ClassId);
        if (row == null)
        {
            OnMessage($"{chr.Name}: {entry.Code} belongs to {entry.SchoolName}, which is not one of the " +
                      "seven casting schools — granting it outright instead.");
            chr.GrantSpell(entry.Id, entry.Display);
            return;
        }

        if (row.Level >= entry.Level)
        {
            OnMessage($"{chr.Name}: already holds {entry.SchoolName} level {row.Level} " +
                      $"(≥ {entry.Level} needed for {entry.Code}).");
            return;
        }

        if (chr.WriteSpellLevel(entry.ClassId, entry.Level))
        {
            chr.Refresh();
            OnMessage($"{chr.Name}: {entry.SchoolName} set to spell level {entry.Level}, " +
                      $"which grants {entry.Code} — {entry.Name}.");
        }
        else
        {
            OnMessage($"{chr.Name}: could not write the spell level (m_spellLevel was not readable).");
        }
    }

    /// <summary>Grants one spell to the selected character, from either spell list in the UI.</summary>
    private void GrantSpell(object? parameter)
    {
        var chr = SelectedCharacter;
        if (chr == null) return;

        switch (parameter)
        {
            case SpecialSpellRow row:
                chr.GrantSpell(row.Id, row.Label);
                break;
            case SpellEntry entry:
                chr.GrantSpell(entry.Id, entry.Display);
                break;
            default:
                OnMessage("Nothing to grant — pick a spell from the list first.");
                break;
        }
    }

    private void RevokeSpell(object? parameter)
    {
        if (SelectedCharacter is { } chr && parameter is LearntSpellViewModel spell)
            chr.RevokeSpell(spell);
    }

    private void GrantSpecialSpellsAll()
    {
        foreach (var chr in Characters) chr.GrantSpecialSpells();
        OnMessage($"All {Characters.Count} characters: the cross-game spells were applied.");
    }

    private void LearnAllSpellsAll()
    {
        foreach (var chr in Characters) chr.LearnAllSpells();
        OnMessage($"All {Characters.Count} characters: every magical school set to level {CharacterFormat.MaxSpellLevel}.");
    }

    private void SetInfiniteItemsAll()
    {
        foreach (var chr in Characters) chr.SetInfiniteItems();
        OnMessage($"All {Characters.Count} characters: item charges zeroed (no longer consumed).");
    }

    private void Detach()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
        _runtime?.Dispose();
        _runtime = null;
        _mem?.Dispose();
        _mem = null;
        _proc?.Dispose();
        _proc = null;
        _location = null;
        _navigator = null;
        _moduleBase = 0;
        _moduleSize = 0;
        IsAttached = false;
        Characters.Clear();
        SelectedCharacterIndex = -1;
        Gold = 0;
        Spells = SpellCatalog.Empty;
        RuntimeStatus = "Not attached.";
        Maps.Tick();
        StatusMessage = "Detached. Attach to the game to begin.";
    }

    /// <summary>
    /// Opens (or closes) the injection helper that grows a full learnt-spell list, and reports
    /// what the trainer will actually be able to do. Failing to open it is not an error: every
    /// other feature works without it, and appending into a list that has room still works.
    /// </summary>
    private void UpdateRuntime()
    {
        OnPropertyChanged(nameof(Runtime));

        if (!_allowGameAllocation)
        {
            _runtime?.Dispose();
            _runtime = null;
            RuntimeStatus = "Off — a spell can only be granted to a character whose learnt-spell " +
                            "list still has a free slot.";
            return;
        }

        if (_proc == null || _mem == null)
        {
            RuntimeStatus = "Not attached.";
            return;
        }

        if (_runtime != null)
        {
            RuntimeStatus = "Ready — a full learnt-spell list will be grown by the game itself.";
            return;
        }

        _runtime = Il2CppRuntime.TryOpen(_proc.ProcessId, _mem, _moduleBase, out string error);
        RuntimeStatus = _runtime != null
            ? "Ready — a full learnt-spell list will be grown by the game itself."
            : $"Unavailable — {error} Spells can still be granted where the list has a free slot.";
    }

    /// <summary>Poll tick: re-apply freezes and refresh the party's position.</summary>
    private void PollCallback()
    {
        if (_mem == null) return;
        try
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                foreach (var chr in Characters) chr.PollFreezes();
                Maps.Tick();
            });
        }
        catch (Exception)
        {
            // A tick that lands during shutdown or a detach is not worth reporting.
        }
    }

    public void OnMessage(string msg) =>
        System.Windows.Application.Current?.Dispatcher.Invoke(() => StatusMessage = msg);

    public void Dispose()
    {
        _pollTimer?.Dispose();
        _archive?.Dispose();
        _runtime?.Dispose();
        _mem?.Dispose();
        _proc?.Dispose();
    }

    private void RaiseAllCanExecuteChanged()
    {
        AttachCommand.RaiseCanExecuteChanged();
        LocateCommand.RaiseCanExecuteChanged();
        WriteGoldCommand.RaiseCanExecuteChanged();
        AssignSpellCommand.RaiseCanExecuteChanged();
        LearnAllSpellsAllCommand.RaiseCanExecuteChanged();
        SetInfiniteItemsAllCommand.RaiseCanExecuteChanged();
        DetachCommand.RaiseCanExecuteChanged();
        GrantSpellCommand.RaiseCanExecuteChanged();
        GrantSpecialSpellsCommand.RaiseCanExecuteChanged();
        GrantSpecialSpellsAllCommand.RaiseCanExecuteChanged();
        RevokeSpellCommand.RaiseCanExecuteChanged();
    }
}

/// <summary>
/// A spell offered on the Spells tab that no school level can grant, so the only way to hold it
/// is an outright grant into the character's learnt-spell list.
/// </summary>
/// <param name="Id">The id written into <c>m_learntSpells</c>.</param>
/// <param name="Code">The game's four-letter code.</param>
/// <param name="Name">A readable name.</param>
/// <param name="Note">Where it comes from in the trilogy, or which games it belongs to.</param>
/// <param name="Cost">Spell points per cast; 0 before the game's table has been read.</param>
public sealed record SpecialSpellRow(SpellId Id, string Code, string Name, string Note, int Cost)
{
    public string Label => $"{Code} — {Name}";

    public string CostText => Cost > 0 ? $"{Cost} SP" : "";
}
