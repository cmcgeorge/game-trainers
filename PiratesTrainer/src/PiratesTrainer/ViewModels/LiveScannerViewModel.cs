using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using PiratesTrainer.Game;

namespace PiratesTrainer.ViewModels;

/// <summary>A selectable target process.</summary>
public sealed class ProcessEntry
{
    public int Id { get; }
    public string Name { get; }
    public bool IsLikelyTarget { get; }
    public string Display => $"{Name}  (pid {Id})";

    public ProcessEntry(int id, string name, bool isLikelyTarget)
    {
        Id = id; Name = name; IsLikelyTarget = isLikelyTarget;
    }
}

/// <summary>
/// The live-memory tab. Pirates! is a real-mode DOS program, so — like the repo's other DOSBox trainers
/// — we attach to the <b>emulator</b> process, whose address space contains the DOS guest's RAM mapped
/// verbatim. Two paths reach the game's state:
/// <list type="bullet">
/// <item><b>Auto-locate (no scan):</b> <see cref="GameLocator"/> finds the data segment by three static
/// literals whose DGROUP offsets are known from the EXE image, then pins gold, the crew, the estate and
/// the game clock straight to the Freezes tab, and lists the era's settlement table live. One click.</item>
/// <item><b>Value scan (fallback):</b> a Cheat-Engine-style flow — snapshot, narrow by what a number does
/// on screen, pin to the freeze table. It cares nothing for layout, so it still works if a differently
/// packaged copy of the game shifts things.</item>
/// </list>
/// </summary>
public sealed class LiveScannerViewModel : ObservableObject, IScanHost, IDisposable
{
    private const int MaxResultRows = 1000;
    private const int LiveRefreshThreshold = 200;

    /// <summary>Label for the auto-located gold pin — the key both auto-locate and "max gold" use.</summary>
    private const string GoldLabel = "Gold";

    private readonly byte[] _ioBuf = new byte[4];

    private ProcessMemory? _mem;
    private MemorySearcher? _searcher;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;
    private int _targetPid;
    private string _pendingPinLabel = "";
    private GameLocation? _location;

    /// <summary>
    /// Pins this trainer derived from a located data segment, and the segment base each was derived
    /// from. Only these are candidates for stale-pin pruning: a pin the user found with the value
    /// scanner is theirs, is the one they actually verified against the screen, and must survive a
    /// later auto-locate even when it happens to carry the same label.
    /// </summary>
    private readonly Dictionary<nuint, nuint> _derivedPinBase = new();

    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ObservableCollection<ScanResultViewModel> Results { get; } = new();
    public ObservableCollection<FrozenValueViewModel> Frozen { get; } = new();

    /// <summary>The era's settlement table as read from the running game; empty until auto-locate runs.</summary>
    public ObservableCollection<LiveCity> Cities { get; } = new();

    public IReadOnlyList<ScanWidth> Widths { get; } = new[] { ScanWidth.Byte, ScanWidth.Int16, ScanWidth.Int32 };

    private ScanWidth _selectedWidth = ScanWidth.Int16;   // gold and crew are both 16-bit words
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

    private LiveCity? _selectedCity;
    public LiveCity? SelectedCity { get => _selectedCity; set { SetField(ref _selectedCity, value); RaiseCommands(); } }

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

    private string _gameSummary = "Not located yet.";
    /// <summary>Captain, date, era and gold as of the last locate — the visible proof the base is right.</summary>
    public string GameSummary { get => _gameSummary; private set => SetField(ref _gameSummary, value); }

    private string _status =
        "Start Pirates! under DOSBox (run PIR.EXE), get into a game, then pick the dosbox process here and " +
        "Attach. Then click \"Auto-locate\" — no manual searching needed.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand FirstScanCommand { get; }
    public ICommand NextScanCommand { get; }
    public ICommand NewScanCommand { get; }
    public ICommand PinCommand { get; }
    public ICommand RemoveFrozenCommand { get; }
    public ICommand FreezeAllCommand { get; }
    public ICommand FreezeNoneCommand { get; }
    public ICommand AutoLocateCommand { get; }
    public ICommand MaxGoldCommand { get; }
    public ICommand PinCityGoldCommand { get; }
    public ICommand GoldGuideCommand { get; }
    public ICommand CrewGuideCommand { get; }
    public ICommand ValueGuideCommand { get; }

    public LiveScannerViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        AttachCommand = new RelayCommand(_ => Attach(), _ => SelectedProcess != null && !IsAttached && !IsScanning);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);
        FirstScanCommand = new RelayCommand(_ => FirstScan(), _ => IsAttached && !IsScanning && !HasResults);
        NextScanCommand = new RelayCommand(NextScan, _ => IsAttached && !IsScanning && HasResults);
        NewScanCommand = new RelayCommand(_ => NewScan(), _ => IsAttached && !IsScanning && HasResults);
        PinCommand = new RelayCommand(_ => PinSelected(), _ => SelectedResult != null);
        RemoveFrozenCommand = new RelayCommand(_ => RemoveFrozen(), _ => SelectedFrozen != null);
        FreezeAllCommand = new RelayCommand(_ => SetAllFrozen(true), _ => Frozen.Count > 0);
        FreezeNoneCommand = new RelayCommand(_ => SetAllFrozen(false), _ => Frozen.Count > 0);
        AutoLocateCommand = new RelayCommand(_ => AutoLocate(), _ => IsAttached && !IsScanning);
        MaxGoldCommand = new RelayCommand(_ => MaxGold(), _ => IsAttached && !IsScanning);
        PinCityGoldCommand = new RelayCommand(_ => PinCityGold(), _ => SelectedCity != null);
        GoldGuideCommand = new RelayCommand(_ => ShowGoldGuide(), _ => IsAttached && !IsScanning);
        CrewGuideCommand = new RelayCommand(_ => ShowCrewGuide(), _ => IsAttached && !IsScanning);
        ValueGuideCommand = new RelayCommand(_ => ShowValueGuide(), _ => IsAttached && !IsScanning);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _poll.Tick += (_, _) => PollTick();

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
            try
            {
                string name = p.ProcessName;
                bool hit = GameFacts.EmulatorProcessHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));
                list.Add(new ProcessEntry(p.Id, name, hit));
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                /* process exited or is inaccessible between enumeration and query */
            }
            finally { p.Dispose(); }
        }
        foreach (var e in list.OrderByDescending(e => e.IsLikelyTarget).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            Processes.Add(e);

        SelectedProcess = Processes.FirstOrDefault(e => e.Id == previous)
                          ?? Processes.FirstOrDefault(e => e.IsLikelyTarget)
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
                     "Click \"Auto-locate\" for one-click access, or use a guided scan below.";
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
        _scanCts?.Dispose();     // cancel-then-dispose; the worker only reads IsCancellationRequested
        _scanCts = null;
        _mem?.Dispose();
        _mem = null;
        _searcher = null;
        _targetPid = 0;
        _location = null;
        Results.Clear();
        Frozen.Clear();
        Cities.Clear();
        _derivedPinBase.Clear();
        SelectedResult = null;
        SelectedFrozen = null;
        SelectedCity = null;
        MatchCount = "";
        GameSummary = "Not located yet.";
        OnPropertyChanged(nameof(IsAttached));
        OnPropertyChanged(nameof(HasResults));
        RaiseCommands();
        Status = "Detached.";
    }

    // --- scanning ------------------------------------------------------------
    private async void FirstScan()
    {
        if (_searcher == null || IsScanning) return;
        bool hasValue = ScanValue.TryParse(ScanText, out long value);
        if (!hasValue && !string.IsNullOrWhiteSpace(ScanText))
        {
            Status = "Enter a number, or clear the box to scan for an unknown value.";
            return;
        }
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
        _pendingPinLabel = "";
        if (_mem != null) _searcher = new MemorySearcher(_mem, SelectedWidth);
        Results.Clear();
        SelectedResult = null;
        MatchCount = "";
        OnPropertyChanged(nameof(HasResults));
        RaiseCommands();
        if (IsAttached) Status = $"New {SelectedWidth} scan. Enter a value and First Scan.";
    }

    // --- pin / freeze --------------------------------------------------------
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
        // Consume the guide's label: only the first pin of a guided scan is the value the guide was for.
        // Leaving it set would label every further pin "Gold" too, and MaxGold keys on that label.
        _pendingPinLabel = "";
        RaiseCommands();
        Status = $"Pinned {r.AddressHex}. Edit Target to poke a value, or tick Freeze to hold it against the game's own tick.";
    }

    private void RemoveFrozen()
    {
        if (SelectedFrozen == null) return;
        _derivedPinBase.Remove(SelectedFrozen.Address);
        Frozen.Remove(SelectedFrozen);
        SelectedFrozen = null;
        RaiseCommands();
    }

    private void SetAllFrozen(bool frozen)
    {
        foreach (var f in Frozen) f.Frozen = frozen;
        Status = frozen ? "All pinned values frozen." : "Freeze cleared.";
    }

    /// <summary>
    /// Pins the selected settlement's treasury byte (record byte 7, in thousands of gold pieces) so a
    /// player can see — or set — how rich a town is before deciding whether it is worth sacking.
    /// </summary>
    private void PinCityGold()
    {
        var city = SelectedCity;
        if (city == null) return;
        var pin = AddOrGetPin(city.GoldAddress, ScanWidth.Byte, $"{city.Name} gold (k)");
        if (pin == null)
        {
            Status = $"Couldn't read {city.Name}'s record at 0x{(ulong)city.GoldAddress:X} — re-run Auto-locate.";
            return;
        }
        // Derived from the current base, so it must be pruned if the segment reloads elsewhere.
        if (_location != null) _derivedPinBase[pin.Address] = _location.DgroupBase;
        SelectedFrozen = pin;
        Status = $"Pinned {city.Name}'s treasury byte at 0x{(ulong)city.GoldAddress:X} — it is in thousands of " +
                 "gold pieces, so 255 is the most a town can hold.";
    }

    // --- auto-locate (no scan) -----------------------------------------------
    /// <summary>
    /// Finds the game's data segment by its static literals and pins everything
    /// <see cref="PiratesLayout.KnownValues"/> maps, plus the live settlement table — no value scan.
    /// Runs on a background thread because it sweeps the whole emulator address space for the anchor.
    /// </summary>
    private async void AutoLocate()
    {
        var (loc, aborted) = await RunLocate("Auto-locating the data segment (scanning the emulator's memory for the game)…");
        if (aborted) return;
        if (loc == null)
        {
            Status = "Auto-locate couldn't find the game's data segment. Make sure Pirates! is actually running " +
                     "in this DOSBox and you are inside a game (past the era/character screens — the settlement " +
                     "table only exists once a game has started). Otherwise use the guided gold scan below.";
            return;
        }

        ApplyLocation(loc);
        var gold = FindGoldPin();
        if (gold != null) SelectedFrozen = gold;

        string shortfall = LastPinnedCount < PiratesLayout.KnownValues.Count
            ? $" ({PiratesLayout.KnownValues.Count - LastPinnedCount} could not be read and were skipped)"
            : "";
        Status = $"Found the data segment at 0x{(ulong)loc.DgroupBase:X}. Pinned {LastPinnedCount} of " +
                 $"{PiratesLayout.KnownValues.Count} values{shortfall} and read {loc.Cities.Count} settlements. " +
                 "Check the summary above matches what the game shows — if it doesn't, Detach and use a " +
                 "guided scan instead.";
    }

    /// <summary>
    /// One-click "fill the hold with gold": auto-locates if needed, then sets the purse to the largest
    /// value its 16-bit word can hold and freezes it so spending can't drain it.
    ///
    /// It only reuses an existing pin when that pin sits at the address the <em>current</em> location
    /// says the purse is at. A pin whose label matches but whose address doesn't — a leftover from a
    /// previous segment load, or one the user labelled through the gold guide — is never poked blind;
    /// the locate is re-run instead.
    /// </summary>
    private async void MaxGold()
    {
        var existing = FindGoldPin();
        if (existing == null)
        {
            var (loc, aborted) = await RunLocate("Locating your gold before maxing it…");
            if (aborted) return;
            if (loc == null)
            {
                Status = "Couldn't locate the data segment — use the guided gold scan, then edit its Target.";
                return;
            }
            ApplyLocation(loc);
            existing = FindGoldPin();
            if (existing == null)
            {
                Status = "Located the data segment, but the gold word at " +
                         $"0x{(ulong)loc.GoldAddress:X} could not be read — so nothing was changed. " +
                         "Re-run Auto-locate, or use the guided gold scan.";
                return;
            }
        }

        existing.Target = PiratesLayout.MaxGold;
        existing.Frozen = true;
        SelectedFrozen = existing;
        Status = $"Gold set to {PiratesLayout.MaxGold:N0} and frozen — the most the game's unsigned 16-bit purse " +
                 "can hold. Untick Freeze to let it move again.";
    }

    /// <summary>
    /// Publishes a fresh location: drops the pins this trainer derived from a <em>previous</em> segment
    /// base, adds a pin for every known value, refreshes the settlement table and rebuilds the summary.
    ///
    /// Staleness is decided by <see cref="_derivedPinBase"/> — which base a pin was derived from — not by
    /// its label and not by an address range. Both of those get it wrong in opposite directions: an
    /// address-range test keeps a genuinely stale pin whenever the segment moves by less than the range's
    /// width, while a label test deletes the user's own scanner-found pin, which is the one they actually
    /// verified. The game can be quit and restarted inside the same DOSBox, reloading the segment
    /// somewhere else, so this has to be right in both directions: a stale pin left frozen writes its
    /// target into unrelated guest RAM every poll tick.
    /// </summary>
    private void ApplyLocation(GameLocation loc)
    {
        // Drop every pin we derived from a different base — known values and settlement treasuries alike.
        foreach (var stale in Frozen.Where(f => _derivedPinBase.TryGetValue(f.Address, out nuint b) && b != loc.DgroupBase).ToList())
        {
            Frozen.Remove(stale);
            _derivedPinBase.Remove(stale.Address);
            if (SelectedFrozen == stale) SelectedFrozen = null;
        }
        _location = loc;

        int pinned = 0;
        foreach (var known in PiratesLayout.KnownValues)
        {
            nuint address = loc.AddressOf(known.Offset);
            var width = known.Bytes == 1 ? ScanWidth.Byte : ScanWidth.Int16;
            if (AddOrGetPin(address, width, known.Label) != null)
            {
                _derivedPinBase[address] = loc.DgroupBase;
                pinned++;
            }
        }
        LastPinnedCount = pinned;

        Cities.Clear();
        foreach (var c in loc.Cities) Cities.Add(c);
        SelectedCity = Cities.FirstOrDefault();

        string name = _mem is { IsOpen: true } ? new GameLocator(_mem).ReadPlayerName(loc.DgroupBase) : "";
        string month = loc.Month >= 0 && loc.Month < PiratesLayout.MonthNames.Count
            ? PiratesLayout.MonthNames[loc.Month] : "?";
        int eraIndex = PiratesLayout.EraIndexFromCode(loc.EraCode);
        string era = eraIndex >= 0 ? CityBook.EraNames[eraIndex] : $"era code {loc.EraCode}";
        GameSummary = $"{(string.IsNullOrEmpty(name) ? "(unnamed captain)" : name)} — {month} {loc.Year} — " +
                      $"{era} — {loc.Gold:N0} gold — {loc.Cities.Count} settlements";
    }

    /// <summary>
    /// Runs the (background) data-segment locate shared by <see cref="AutoLocate"/> and
    /// <see cref="MaxGold"/>: it owns the busy flag, the cancellation-token lifecycle, the staleness
    /// guard and the error handling. Returns <c>(loc, aborted)</c>: <c>aborted</c> is true when the run
    /// was cancelled, errored, or the attachment changed under it.
    /// </summary>
    private async Task<(GameLocation? Loc, bool Aborted)> RunLocate(string busyMessage)
    {
        if (_mem is not { IsOpen: true } || IsScanning) return (null, true);
        IsScanning = true;   // the setter re-raises command states
        Status = busyMessage;
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        var mem = _mem;
        try
        {
            GameLocation? loc = await Task.Run(() => new GameLocator(mem).Locate(ct), ct);
            if (mem != _mem || ct.IsCancellationRequested) return (null, true);
            return (loc, false);
        }
        catch (OperationCanceledException) { if (mem == _mem) Status = "Auto-locate cancelled."; return (null, true); }
        catch (Exception ex) { if (mem == _mem) Status = "Auto-locate error: " + ex.Message; return (null, true); }
        finally { IsScanning = false; }   // the setter re-raises command states
    }

    /// <summary>
    /// The pin sitting at the address the <em>current</em> location computed for the purse. The address
    /// is the authority, not the label: a label match alone could pick up a row the user pinned through
    /// the value scanner or one left over from a previous segment load, and "Max gold" must never poke
    /// an address it has not just derived. Returns null if there is no current location or nothing is
    /// pinned there — callers must re-locate rather than guess.
    /// </summary>
    private FrozenValueViewModel? FindGoldPin()
    {
        if (_location == null) return null;
        nuint expected = _location.GoldAddress;
        return Frozen.FirstOrDefault(f => f.Address == expected);
    }

    /// <summary>How many known values the last <see cref="ApplyLocation"/> actually managed to pin.</summary>
    private int LastPinnedCount { get; set; }

    /// <summary>
    /// Adds a pinned row for a known address; returns the existing one if it is already pinned, or null
    /// if the address could not be read. A failed read must not become a pin: <see cref="ReadAt"/> cannot
    /// distinguish "read failed" from "the value is 0", so a pin created on a failure would sit at
    /// Target 0 and "Freeze all" would then write zeroes into a field that holds something else.
    /// </summary>
    private FrozenValueViewModel? AddOrGetPin(nuint address, ScanWidth width, string label, bool signed = false)
    {
        var existing = Frozen.FirstOrDefault(f => f.Address == address);
        if (existing != null) return existing;
        if (!ReadAt(address, width, out long current)) return null;
        var pin = new FrozenValueViewModel(this, address, width, current, label, signed);
        Frozen.Add(pin);
        RaiseCommands();
        return pin;
    }

    // --- guided scans --------------------------------------------------------
    private void BeginGuide(ScanWidth width, string label)
    {
        if (_selectedWidth != width) SelectedWidth = width;   // setter runs NewScan()
        else NewScan();
        _pendingPinLabel = label;   // set after NewScan(), which clears it, so the next pin is labelled
    }

    // Guided-scan pins get labels distinct from PiratesLayout.KnownValues, so the Freezes grid always
    // says whether a row came from the static layout or from the user's own scan — they can legitimately
    // disagree, and the scanned one is the one that was verified against the screen.
    private const string ScannedGoldLabel = "Gold (scanned)";
    private const string ScannedCrewLabel = "Crew (scanned)";

    private void ShowGoldGuide()
    {
        BeginGuide(ScanWidth.Int16, ScannedGoldLabel);
        Status = "Gold guide: your purse is an unsigned 16-bit word holding the exact number of gold pieces the " +
                 "party panel shows. Type that number → First Scan. Now spend or take some (buy food, divide " +
                 "plunder) → type the new figure → Exact. Repeat until one row remains, then Pin and freeze.";
    }

    private void ShowCrewGuide()
    {
        BeginGuide(ScanWidth.Int16, ScannedCrewLabel);
        Status = "Crew guide: read \"CREW: n MEN\" on the party panel, type n → First Scan. Sign on or lose some " +
                 "crew (a tavern recruit, a boarding action, a desertion) → type the new count → Exact. Narrow to " +
                 "one row, Pin, then freeze it to stop desertions from biting.";
    }

    private void ShowValueGuide()
    {
        NewScan();
        _pendingPinLabel = "";
        Status = "Value guide: pick a Type above (Byte / Int16 / Int32), read a number you can see in-game, type it " +
                 "→ First Scan; make it change → type the new value → Exact. Narrow to one row, then Pin and freeze. " +
                 "Use this for anything auto-locate doesn't cover — food days, cannon, a ship's crew.";
    }

    // --- poll loop -----------------------------------------------------------
    private void PollTick()
    {
        if (_mem == null) return;
        if (!_mem.IsOpen || HasTargetExited()) { Detach(); Status = "Emulator process exited."; return; }

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
    }

    /// <summary>
    /// Whether the attached emulator is gone.
    ///
    /// Only <see cref="ArgumentException"/> actually means that — <see cref="Process.GetProcessById(int)"/>
    /// throws it when no process has the pid. <see cref="InvalidOperationException"/> (handle torn down)
    /// and <see cref="System.ComponentModel.Win32Exception"/> (the query was refused) mean "could not
    /// answer", which is not the same thing: reporting them as an exit would make <see cref="PollTick"/>
    /// call <see cref="Detach"/> and throw away every pin and freeze the user has set up, over a transient
    /// failure, while the emulator is still running. Those are caught and treated as "still there" — if
    /// the process really has gone, the next read through <c>_mem</c> will say so.
    ///
    /// They must still be caught rather than left to escape: an exception out of the
    /// <see cref="DispatcherTimer"/> tick reaches the app's unhandled-exception handler, which shows a
    /// modal dialog and marks it handled <em>without</em> stopping the timer — so the user would get a
    /// fresh dialog every 200 ms.
    /// </summary>
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
            return true;    // no such process
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;   // couldn't ask; don't tear the user's session down over it
        }
    }

    // --- IScanHost -----------------------------------------------------------
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
        (RemoveFrozenCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (FreezeAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (FreezeNoneCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AutoLocateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxGoldCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PinCityGoldCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (GoldGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CrewGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ValueGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _mem?.Dispose();
    }
}
