using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using QuestForGlory1Trainer.Game;
using QuestForGlory1Trainer.Memory;

namespace QuestForGlory1Trainer.ViewModels;

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

/// <summary>One time-of-day preset for the Day/Time editor.</summary>
public sealed class TimePreset
{
    public string Label { get; }
    public int Ticks { get; }
    public string Tooltip { get; }

    public TimePreset(string label, int ticks, string tooltip)
    {
        Label = label; Ticks = ticks; Tooltip = tooltip;
    }
}

/// <summary>
/// Root view-model. Quest for Glory I runs on the Sierra SCI0 interpreter whose heap is
/// dynamically allocated in DOSBox guest RAM each session — global variables and Ego object
/// properties have no stable static byte signature to anchor a locator to. The reliable
/// primitive is therefore a Cheat-Engine-style <b>value scan</b>: attach to the emulator,
/// snapshot memory, and narrow by what the on-screen number does. Survivors are pinned to a
/// freeze table that re-writes them every poll tick.
///
/// Two higher-level editors sit on top of the scan layer:
/// <list type="bullet">
/// <item><b>Day/Time tab</b> — once the game-clock address is pinned, lets the user set the
///   in-game day and hour directly.</item>
/// <item><b>Teleport tab</b> — once the room-number address is pinned, lets the user pick a
///   room from a named dropdown and warp there on the next room transition.</item>
/// </list>
///
/// All three special-address pins (clock, day, room) are restricted to <see cref="ScanWidth.Int16"/>
/// candidates because SCI0 global variables are 16-bit words; pinning a byte or 32-bit result
/// would corrupt the adjacent byte when the poll writes back.
/// </summary>
public sealed class MainViewModel : ObservableObject, IScanHost, IDisposable
{
    private static readonly string[] EmulatorHints =
        { "dosbox", "dosbox-x", "dosbox-staging", "scummvm", "pcem", "86box", "qemu", "boxer" };

    private const int MaxResultRows = 1000;
    private const int LiveRefreshThreshold = 200;

    private ProcessMemory? _mem;
    private MemorySearcher? _searcher;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;
    private int _targetPid;

    private string _pendingPinLabel = "";

    // ---- pinned special addresses -------------------------------------------
    private nuint _clockAddress;
    private nuint _dayAddress;
    private nuint _roomAddress;

    public bool HasClockPin => _clockAddress != nuint.Zero;
    public bool HasDayPin   => _dayAddress != nuint.Zero;
    public bool HasRoomPin  => _roomAddress != nuint.Zero;

    // ---- stat locator -------------------------------------------------------
    private LocatedStats? _locatedStats;
    public bool HasLocatedStats => _locatedStats != null;

    private HeroViewModel? _hero;
    public HeroViewModel? Hero { get => _hero; private set => SetField(ref _hero, value); }

    private bool _isLocating;
    public bool IsLocating
    {
        get => _isLocating;
        set { if (SetField(ref _isLocating, value)) { OnPropertyChanged(nameof(NotLocating)); RaiseCommands(); } }
    }
    public bool NotLocating => !_isLocating;

    private string _locateStatus = "Attach to DOSBox, then use the Hero Stats tab to find & freeze your stats with a guided value scan.";
    public string LocateStatus { get => _locateStatus; private set => SetField(ref _locateStatus, value); }

    // -------------------------------------------------------------------------

    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ObservableCollection<ScanResultViewModel> Results { get; } = new();
    public ObservableCollection<FrozenValueViewModel> Frozen { get; } = new();
    public ObservableCollection<SkillInfo> Stats { get; } = new(SkillBook.Stats);
    public ObservableCollection<RoomEntry> Rooms { get; } = new(RoomBook.Rooms);

    public IReadOnlyList<ScanWidth> Widths { get; } = new[] { ScanWidth.Byte, ScanWidth.Int16, ScanWidth.Int32 };

    public IReadOnlyList<TimePreset> TimePresets { get; } = new[]
    {
        new TimePreset("Dawn",          GameOffsets.TimeDawn,          "~midnight / early dawn (tick 0)"),
        new TimePreset("Mid-morning",   GameOffsets.TimeMidMorning,    "~4 AM — shops start to open (tick 450)"),
        new TimePreset("Midday",        GameOffsets.TimeMidDay,        "~7 AM — all shops open (tick 1050)"),
        new TimePreset("Mid-afternoon", GameOffsets.TimeMidAfternoon,  "~11 AM — peak activity (tick 1650)"),
        new TimePreset("Sunset",        GameOffsets.TimeSunset,        "~3 PM — shops begin closing (tick 2250)"),
        new TimePreset("Night",         GameOffsets.TimeNight,         "~7 PM — Thieves' Guild active (tick 2850)"),
        new TimePreset("Midnight",      GameOffsets.TimeMidnight,      "~10 PM — darkest hours (tick 3300)"),
    };

    private ScanWidth _selectedWidth = ScanWidth.Int16;
    public ScanWidth SelectedWidth
    {
        get => _selectedWidth;
        set { if (SetField(ref _selectedWidth, value)) NewScan(); }
    }

    private string _scanText = "";
    public string ScanText { get => _scanText; set => SetField(ref _scanText, value); }

    private ProcessEntry? _selectedProcess;
    public ProcessEntry? SelectedProcess { get => _selectedProcess; set { SetField(ref _selectedProcess, value); RaiseCommands(); } }

    private ScanResultViewModel? _selectedResult;
    public ScanResultViewModel? SelectedResult { get => _selectedResult; set { SetField(ref _selectedResult, value); RaiseCommands(); } }

    private FrozenValueViewModel? _selectedFrozen;
    public FrozenValueViewModel? SelectedFrozen { get => _selectedFrozen; set { SetField(ref _selectedFrozen, value); RaiseCommands(); } }

    public bool IsAttached => _mem is { IsOpen: true };
    public bool HasResults => _searcher is { HasMatches: true };

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set { if (SetField(ref _isScanning, value)) { OnPropertyChanged(nameof(NotScanning)); RaiseCommands(); } }
    }

    public bool NotScanning => !_isScanning;

    private string _matchCountText = "";
    public string MatchCount { get => _matchCountText; private set => SetField(ref _matchCountText, value); }

    private string _status = "Launch Quest for Glory I in DOSBox, pick the emulator process, and Attach.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // ---- Day/Time editor ----------------------------------------------------
    private int _targetDay = 1;
    /// <summary>Day to write when the user clicks Set Time. Clamped to 1–65535.</summary>
    public int TargetDay
    {
        get => _targetDay;
        set
        {
            int clamped = Math.Clamp(value, 1, ushort.MaxValue);
            if (!SetField(ref _targetDay, clamped)) OnPropertyChanged(nameof(TargetDay));
        }
    }

    private int _targetHour;
    /// <summary>Hour (0–23) to write when the user clicks Set Time.</summary>
    public int TargetHour
    {
        get => _targetHour;
        set
        {
            int clamped = Math.Clamp(value, 0, 23);
            if (!SetField(ref _targetHour, clamped)) OnPropertyChanged(nameof(TargetHour));
        }
    }

    private string _clockPinSummary = "Not yet located — use Value Scanner → Game Clock, then Pin As Clock.";
    public string ClockPinSummary { get => _clockPinSummary; private set => SetField(ref _clockPinSummary, value); }

    private string _dayPinSummary = "Not yet located — use Value Scanner → Game Day, then Pin As Day.";
    public string DayPinSummary { get => _dayPinSummary; private set => SetField(ref _dayPinSummary, value); }

    // ---- Teleport -----------------------------------------------------------
    private RoomEntry? _selectedRoom;
    public RoomEntry? SelectedRoom { get => _selectedRoom; set { SetField(ref _selectedRoom, value); RaiseCommands(); } }

    private string _roomPinSummary = "Not yet located — use Value Scanner → Room Number, then Pin As Room.";
    public string RoomPinSummary { get => _roomPinSummary; private set => SetField(ref _roomPinSummary, value); }

    private string _currentRoomDisplay = "—";
    public string CurrentRoomDisplay { get => _currentRoomDisplay; private set => SetField(ref _currentRoomDisplay, value); }

    // ---- Commands -----------------------------------------------------------
    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand FirstScanCommand { get; }
    public ICommand NextScanCommand { get; }
    public ICommand NewScanCommand { get; }
    public ICommand PinCommand { get; }
    public ICommand PinAsClockCommand { get; }
    public ICommand PinAsDayCommand { get; }
    public ICommand PinAsRoomCommand { get; }
    public ICommand RemoveFrozenCommand { get; }
    public ICommand FreezeAllCommand { get; }

    public ICommand HpGuideCommand { get; }
    public ICommand StaminaGuideCommand { get; }
    public ICommand MagicGuideCommand { get; }
    public ICommand GoldGuideCommand { get; }
    public ICommand ClockGuideCommand { get; }
    public ICommand DayGuideCommand { get; }
    public ICommand RoomGuideCommand { get; }

    public ICommand SetTimePresetCommand { get; }
    public ICommand SetTimeCommand { get; }
    public ICommand TeleportCommand { get; }

    public ICommand ScanCommand { get; }
    public ICommand RestoreHpCommand { get; }
    public ICommand RestoreStaminaCommand { get; }
    public ICommand RestoreManaCommand { get; }

    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        AttachCommand           = new RelayCommand(_ => Attach(),    _ => SelectedProcess != null && !IsAttached && !IsScanning);
        DetachCommand           = new RelayCommand(_ => Detach(),    _ => IsAttached && !IsScanning);
        FirstScanCommand        = new RelayCommand(_ => FirstScan(), _ => IsAttached && !IsScanning && !HasResults);
        NextScanCommand         = new RelayCommand(p => NextScan(p), _ => IsAttached && !IsScanning && HasResults);
        NewScanCommand          = new RelayCommand(_ => NewScan(),   _ => IsAttached && !IsScanning && HasResults);

        PinCommand        = new RelayCommand(_ => PinSelected(),        _ => IsAttached && !IsScanning && SelectedResult != null);
        PinAsClockCommand = new RelayCommand(_ => PinSelectedAsClock(), _ => CanPinSpecialAddress());
        PinAsDayCommand   = new RelayCommand(_ => PinSelectedAsDay(),   _ => CanPinSpecialAddress());
        PinAsRoomCommand  = new RelayCommand(_ => PinSelectedAsRoom(),  _ => CanPinSpecialAddress());

        RemoveFrozenCommand = new RelayCommand(_ => RemoveFrozen(),      _ => SelectedFrozen != null);
        FreezeAllCommand    = new RelayCommand(_ => SetAllFrozen(true),  _ => Frozen.Count > 0);

        HpGuideCommand      = new RelayCommand(_ => ShowHpGuide(),      _ => IsAttached && !IsScanning);
        StaminaGuideCommand = new RelayCommand(_ => ShowStaminaGuide(), _ => IsAttached && !IsScanning);
        MagicGuideCommand   = new RelayCommand(_ => ShowMagicGuide(),   _ => IsAttached && !IsScanning);
        GoldGuideCommand    = new RelayCommand(_ => ShowGoldGuide(),     _ => IsAttached && !IsScanning);
        ClockGuideCommand   = new RelayCommand(_ => ShowClockGuide(),   _ => IsAttached && !IsScanning);
        DayGuideCommand     = new RelayCommand(_ => ShowDayGuide(),     _ => IsAttached && !IsScanning);
        RoomGuideCommand    = new RelayCommand(_ => ShowRoomGuide(),    _ => IsAttached && !IsScanning);

        SetTimePresetCommand = new RelayCommand(p => ApplyTimePreset(p), _ => HasClockPin);
        SetTimeCommand       = new RelayCommand(_ => SetTime(),          _ => HasClockPin || HasDayPin);
        TeleportCommand      = new RelayCommand(_ => Teleport(),         _ => HasRoomPin && SelectedRoom != null);

        ScanCommand           = new RelayCommand(_ => Scan(),           _ => IsAttached && !IsScanning && !IsLocating);
        RestoreHpCommand      = new RelayCommand(_ => RestoreHp(),      _ => HasLocatedStats);
        RestoreStaminaCommand = new RelayCommand(_ => RestoreStamina(), _ => HasLocatedStats);
        RestoreManaCommand    = new RelayCommand(_ => RestoreMana(),    _ => HasLocatedStats);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _poll.Tick += (_, _) => PollTick();

        RefreshProcesses();
    }

    // ---- helpers ------------------------------------------------------------
    /// <summary>
    /// Special-address pins (clock, day, room) are only valid for Int16 candidates, because SCI0
    /// global variables are 16-bit words. Pinning a Byte or Int32 result would over- or under-read
    /// when the poll writes back two bytes at the captured address.
    /// </summary>
    private bool CanPinSpecialAddress()
        => IsAttached && !IsScanning && SelectedResult != null && SelectedWidth == ScanWidth.Int16;

    // ---- process management -------------------------------------------------
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
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
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
            _targetPid = SelectedProcess.Id;
            _searcher = new MemorySearcher(_mem, SelectedWidth);
            Results.Clear();
            OnPropertyChanged(nameof(IsAttached));
            OnPropertyChanged(nameof(HasResults));
            RaiseCommands();
            _poll.Start();
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). " +
                     "Open the Hero Stats tab to find & freeze HP/Stamina/Mana with a guided scan (works in combat).";
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
        _clockAddress = nuint.Zero;
        _dayAddress = nuint.Zero;
        _roomAddress = nuint.Zero;
        _locatedStats = null;
        Hero = null;
        Results.Clear();
        Frozen.Clear();
        SelectedResult = null;
        SelectedFrozen = null;
        MatchCount = "";
        ClockPinSummary = "Not yet located — use Value Scanner → Game Clock, then Pin As Clock.";
        DayPinSummary = "Not yet located — use Value Scanner → Game Day, then Pin As Day.";
        RoomPinSummary = "Not yet located — use Value Scanner → Room Number, then Pin As Room.";
        CurrentRoomDisplay = "—";
        LocateStatus = "Attach to DOSBox, then use the Hero Stats tab to find & freeze your stats with a guided value scan.";
        OnPropertyChanged(nameof(IsAttached));
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasClockPin));
        OnPropertyChanged(nameof(HasDayPin));
        OnPropertyChanged(nameof(HasRoomPin));
        OnPropertyChanged(nameof(HasLocatedStats));
        RaiseCommands();
        Status = "Detached.";
    }

    // ---- scanning -----------------------------------------------------------
    private async void FirstScan()
    {
        if (_searcher == null || IsScanning) return;
        bool hasValue = ScanValue.TryParse(ScanText, out long value);
        if (hasValue && !ScanValue.FitsWidth(value, SelectedWidth))
        {
            Status = $"{value} does not fit a {SelectedWidth} scan — pick a wider type or a smaller value.";
            return;
        }

        long stored = ScanValue.Canonicalize(value, SelectedWidth);
        var searcher = _searcher;
        await RunScan(
            hasValue ? $"First scan for {value}…" : "First scan (unknown value)…",
            ct =>
            {
                if (hasValue) searcher.FirstScanExact(stored, ct);
                else searcher.FirstScanUnknown(ct);
            });
    }

    private async void NextScan(object? parameter)
    {
        if (_searcher == null || IsScanning) return;
        if (parameter is not ScanCompare compare && !Enum.TryParse(parameter?.ToString(), out compare))
            return;

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
        await RunScan($"Narrowing ({compare})…", ct => searcher.NextScan(compare, value, ct));
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
        {
            foreach (var m in searcher.Take(MaxResultRows))
                Results.Add(new ScanResultViewModel(m.Address, m.Value));
        }

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
        _pendingPinLabel = "";   // a plain/manual scan makes unlabeled pins; guides set the label afterwards
        if (_mem != null) _searcher = new MemorySearcher(_mem, SelectedWidth);
        Results.Clear();
        SelectedResult = null;
        MatchCount = "";
        OnPropertyChanged(nameof(HasResults));
        RaiseCommands();
        if (IsAttached) Status = $"New {SelectedWidth} scan. Enter a value and First Scan.";
    }

    // ---- pin / freeze -------------------------------------------------------
    private void PinSelected()
    {
        var r = SelectedResult;
        if (r == null) return;
        if (Frozen.Any(f => f.Address == r.Address))
        {
            Status = $"{r.AddressHex} is already pinned.";
            return;
        }
        Frozen.Add(new FrozenValueViewModel(this, r.Address, SelectedWidth, r.Value, _pendingPinLabel));
        RaiseCommands();
        Status = $"Pinned {r.AddressHex}. Edit Target to poke a value, or tick Freeze to hold it.";
    }

    private void PinSelectedAsClock()
    {
        var r = SelectedResult;
        if (r == null) return;
        _clockAddress = r.Address;
        ClockPinSummary = $"Clock pinned at {r.AddressHex}  (live value: {r.Value})";
        OnPropertyChanged(nameof(HasClockPin));
        RaiseCommands();
        Status = $"Game-clock address pinned at {r.AddressHex}. You can now use the Day/Time tab.";
    }

    private void PinSelectedAsDay()
    {
        var r = SelectedResult;
        if (r == null) return;
        _dayAddress = r.Address;
        DayPinSummary = $"Day pinned at {r.AddressHex}  (live value: {r.Value})";
        OnPropertyChanged(nameof(HasDayPin));
        RaiseCommands();
        Status = $"Game-day address pinned at {r.AddressHex}. You can now write the day in the Day/Time tab.";
    }

    private void PinSelectedAsRoom()
    {
        var r = SelectedResult;
        if (r == null) return;
        _roomAddress = r.Address;
        RoomPinSummary = $"Room pinned at {r.AddressHex}  (live value: {r.Value})";
        OnPropertyChanged(nameof(HasRoomPin));
        RaiseCommands();
        UpdateCurrentRoomDisplay((int)r.Value);
        Status = $"Room-number address pinned at {r.AddressHex}. You can now use the Teleport tab.";
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
        foreach (var f in Frozen) f.Frozen = frozen;
        Status = frozen ? "All pinned values frozen." : "Freeze cleared.";
    }

    // ---- guided scans -------------------------------------------------------
    private void BeginGuide(ScanWidth width, string label)
    {
        // Trigger the reset first (the width setter runs NewScan, which clears the label),
        // then stamp the guided label so the next pin carries it.
        if (_selectedWidth != width) SelectedWidth = width;
        else NewScan();
        _pendingPinLabel = label;
    }

    private void ShowHpGuide()
    {
        BeginGuide(ScanWidth.Int16, "HP");
        Status = "HP guide: open the stats screen and read your current Health value (e.g. 13). " +
                 "Type it → First Scan; take a hit in combat so it drops → type the new value → Exact. " +
                 "Repeat until one row remains, then Pin it.";
    }

    private void ShowStaminaGuide()
    {
        BeginGuide(ScanWidth.Int16, "Stamina");
        Status = "Stamina guide: read your Stamina on the stats screen (e.g. 23). " +
                 "Type it → First Scan; run around or fight so it drops → type the new value → Exact. " +
                 "Repeat until one candidate remains, then Pin it.";
    }

    private void ShowMagicGuide()
    {
        BeginGuide(ScanWidth.Int16, "Mana");
        Status = "Mana guide: read your Mana on the stats screen (e.g. 10). " +
                 "Cast a spell so it drops → type the new value → Exact. " +
                 "Repeat until one candidate remains, then Pin it. (0 if you are not a Magic-user.)";
    }

    private void ShowGoldGuide()
    {
        BeginGuide(ScanWidth.Int16, "Gold");
        Status = "Gold guide: note your Gold Coins total (e.g. 50). " +
                 "Type it → First Scan; buy something cheap so the total changes → type the new value → Exact. " +
                 "Pin the survivor.";
    }

    private void ShowClockGuide()
    {
        BeginGuide(ScanWidth.Int16, "Game Clock");
        Status = "Game-clock guide: the clock is a 0–3599 value that increments each engine cycle. " +
                 "First Scan (unknown) → wait ~2 seconds → Increased; wait again → Increased. " +
                 "When only a few candidates remain, click Pin As Clock (requires Int16 type). " +
                 "The Day/Time tab will then be usable.";
    }

    private void ShowDayGuide()
    {
        BeginGuide(ScanWidth.Int16, "Game Day");
        Status = "Game-day guide: the day counter starts at 1 and increments each time 3600 clock " +
                 "ticks elapse. Type the current day (e.g. 1) → First Scan; sleep at the inn so the " +
                 "day advances → type the new day → Exact. When one candidate remains, click Pin As Day. " +
                 "The Day/Time tab will let you write both day and hour together.";
    }

    private void ShowRoomGuide()
    {
        BeginGuide(ScanWidth.Int16, "Room");
        Status = "Room-number guide: look up your current room in the Teleport tab's table. " +
                 "Type its number → First Scan; walk to an adjacent area → Changed (eliminates static values) " +
                 "→ note the new room number → type it → Exact. " +
                 "Click Pin As Room (requires Int16 type) when one candidate remains.";
    }

    // ---- stat locator -------------------------------------------------------
    private async void Scan()
    {
        if (_mem == null || IsLocating) return;
        IsLocating = true;
        LocateStatus = "Scanning for character stats and game globals…";
        _locatedStats = null;
        OnPropertyChanged(nameof(HasLocatedStats));
        RaiseCommands();

        var mem = _mem;
        LocatedStats?   foundStats   = null;
        LocatedGlobals? foundGlobals = null;
        nuint           actorHpAddr  = nuint.Zero;
        try
        {
            foundStats = await Task.Run(() => StatLocator.Find(mem));
            if (foundStats != null)
            {
                foundGlobals = await Task.Run(() => GlobalLocator.Find(mem, foundStats.StrAddress));
                actorHpAddr  = await Task.Run(() => StatLocator.FindActorHp(mem, foundStats));
            }
        }
        catch (Exception ex)
        {
            LocateStatus = "Scan error: " + ex.Message;
            IsLocating = false;
            return;
        }
        finally
        {
            IsLocating = false;
        }

        if (foundStats == null)
        {
            LocateStatus = "No character found — load a saved game (past the title screen), then click Re-scan.";
            return;
        }

        _locatedStats = foundStats;
        OnPropertyChanged(nameof(HasLocatedStats));
        Hero = new HeroViewModel(this, foundStats, actorHpAddr);

        if (foundGlobals != null)
        {
            _clockAddress = foundGlobals.ClockAddress;
            _dayAddress   = foundGlobals.DayAddress;
            _roomAddress  = foundGlobals.RoomAddress;
            ClockPinSummary = $"Auto-located at 0x{(ulong)_clockAddress:X8} — live tick {foundGlobals.Clock}.";
            DayPinSummary   = $"Auto-located at 0x{(ulong)_dayAddress:X8} — day {foundGlobals.Day}.";
            RoomPinSummary  = $"Auto-located at 0x{(ulong)_roomAddress:X8} — room {foundGlobals.Room}.";
            UpdateCurrentRoomDisplay(foundGlobals.Room);
            OnPropertyChanged(nameof(HasClockPin));
            OnPropertyChanged(nameof(HasDayPin));
            OnPropertyChanged(nameof(HasRoomPin));
        }

        RaiseCommands();

        var parts = new System.Text.StringBuilder();
        parts.Append($"Stats found at 0x{(ulong)foundStats.StrAddress:X8}. ");
        parts.Append($"HP≈{foundStats.HpDisplayed}  Stam≈{foundStats.StaminaDisplayed}  Mana={foundStats.ManaDisplayed}. ");
        parts.Append(foundGlobals != null
            ? $"Day {foundGlobals.Day}, room {foundGlobals.Room}. All values located automatically."
            : "Globals not found — Day/Time and Teleport tabs require manual scan.");
        if (actorHpAddr != nuint.Zero)
            parts.Append($" Actor HP copy: 0x{(ulong)actorHpAddr:X8}.");
        LocateStatus = parts.ToString();
        Status = LocateStatus;
    }

    private void RestoreHp()
    {
        if (_locatedStats == null) return;
        if (!WriteAt(_locatedStats.HpAddress, _locatedStats.HpRaw, ScanWidth.Int16))
            Status = "Write failed — HP not restored.";
        else
            Status = $"HP restored (internal: {_locatedStats.HpRaw}; shown ≈ {_locatedStats.HpDisplayed}).";
    }

    private void RestoreStamina()
    {
        if (_locatedStats == null) return;
        if (!WriteAt(_locatedStats.StaminaAddress, _locatedStats.StaminaRaw, ScanWidth.Int16))
            Status = "Write failed — Stamina not restored.";
        else
            Status = $"Stamina restored (internal: {_locatedStats.StaminaRaw}; shown ≈ {_locatedStats.StaminaDisplayed}).";
    }

    private void RestoreMana()
    {
        if (_locatedStats == null) return;
        if (!WriteAt(_locatedStats.ManaAddress, _locatedStats.ManaRaw, ScanWidth.Int16))
            Status = "Write failed — Mana not restored.";
        else
            Status = $"Mana restored to {_locatedStats.ManaDisplayed}.";
    }

    // ---- Day/Time editor ----------------------------------------------------
    private void ApplyTimePreset(object? parameter)
    {
        if (parameter is not TimePreset preset) return;
        TargetHour = preset.Ticks / GameOffsets.TicksPerHour;
        if (!WriteAt(_clockAddress, preset.Ticks, ScanWidth.Int16))
        {
            Status = $"Write failed — could not set clock to {preset.Label}.";
            return;
        }
        Status = $"Time set to {preset.Label} (tick {preset.Ticks}). Day was not changed.";
    }

    private void SetTime()
    {
        int ticks = TargetHour * GameOffsets.TicksPerHour;
        bool clockWritten = HasClockPin && WriteAt(_clockAddress, ticks, ScanWidth.Int16);
        bool dayWritten   = HasDayPin   && WriteAt(_dayAddress, TargetDay, ScanWidth.Int16);

        if (!clockWritten && !dayWritten)
        {
            Status = "Write failed — could not update the pinned clock/day address.";
            return;
        }

        Status = (clockWritten, dayWritten) switch
        {
            (true, true)   => $"Set day {TargetDay}, {TargetHour:00}:00 ({ticks} ticks).",
            (true, false)  => $"Set time to {TargetHour:00}:00 ({ticks} ticks). Game day not changed (day not pinned).",
            (false, true)  => $"Set game day to {TargetDay}. Clock not changed (clock not pinned).",
            _              => "No values written.",
        };
    }

    // ---- Teleport -----------------------------------------------------------
    private void Teleport()
    {
        if (!HasRoomPin || SelectedRoom == null) return;
        if (!WriteAt(_roomAddress, SelectedRoom.Number, ScanWidth.Int16))
        {
            Status = $"Write failed — could not write room {SelectedRoom.Number} to the pinned address.";
            return;
        }
        UpdateCurrentRoomDisplay(SelectedRoom.Number);
        Status = $"Room number written: {SelectedRoom.Number} ({SelectedRoom.Name}). " +
                 "Walk through a door or use ALT-T in the game to trigger the room transition.";
    }

    private void UpdateCurrentRoomDisplay(int roomNumber)
    {
        var known = RoomBook.Rooms.FirstOrDefault(r => r.Number == roomNumber);
        CurrentRoomDisplay = known is null
            ? $"{roomNumber} (unknown)"
            : $"{roomNumber} — {known.Name}{(known.Confirmed ? "" : " *")}";
    }

    // ---- poll loop ----------------------------------------------------------
    private void PollTick()
    {
        if (_mem == null) return;

        if (!_mem.IsOpen || HasTargetExited())
        {
            Detach();
            Status = "Target process exited.";
            return;
        }

        Hero?.Refresh();

        foreach (var f in Frozen)
        {
            f.ApplyFreeze();
            if (ReadAt(f.Address, f.Width, out long live)) f.RefreshLive(live);
        }

        if (_searcher != null && !IsScanning && Results.Count > 0 && Results.Count <= LiveRefreshThreshold)
        {
            foreach (var r in Results)
                if (_searcher.ReadValue(r.Address, out long live)) r.RefreshLive(live);
        }

        if (HasRoomPin && ReadAt(_roomAddress, ScanWidth.Int16, out long room))
            UpdateCurrentRoomDisplay((int)room);

        if (HasClockPin && ReadAt(_clockAddress, ScanWidth.Int16, out long clockTicks))
            ClockPinSummary = $"Clock pinned at 0x{(ulong)_clockAddress:X}  " +
                              $"(live tick: {clockTicks} / {GameOffsets.TicksPerDay})";
    }

    private bool HasTargetExited()
    {
        if (_targetPid == 0) return false;
        try
        {
            using var p = Process.GetProcessById(_targetPid);
            return p.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    // ---- IScanHost ----------------------------------------------------------
    private bool ReadAt(nuint address, ScanWidth width, out long value)
    {
        value = 0;
        var mem = _mem;
        if (mem is not { IsOpen: true }) return false;
        int w = (int)width;
        var buf = mem.Read(address, w);
        if (buf.Length < w) return false;
        long result = 0;
        for (int i = 0; i < w; i++) result |= (long)buf[i] << (8 * i);
        value = result;
        return true;
    }

    private bool WriteAt(nuint address, long value, ScanWidth width)
    {
        var mem = _mem;
        if (mem is not { IsOpen: true }) return false;
        int w = (int)width;
        var buf = new byte[w];
        ulong v = unchecked((ulong)value);
        for (int i = 0; i < w; i++) { buf[i] = (byte)(v & 0xFF); v >>= 8; }
        return mem.Write(address, buf);
    }

    bool IScanHost.Write(nuint address, long value, ScanWidth width) => WriteAt(address, value, width);
    bool IScanHost.Read(nuint address, ScanWidth width, out long value) => ReadAt(address, width, out value);
    void IScanHost.ReportWriteFailure(nuint address)
        => Status = $"Write failed at 0x{(ulong)address:X} — the value was not applied.";

    private void RaiseCommands()
    {
        (AttachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (FirstScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NewScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PinCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PinAsClockCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PinAsDayCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PinAsRoomCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemoveFrozenCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (FreezeAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (HpGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (StaminaGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MagicGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (GoldGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClockGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DayGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RoomGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SetTimePresetCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SetTimeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (TeleportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RestoreHpCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RestoreStaminaCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RestoreManaCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _mem?.Dispose();
    }
}
