using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using RailroadTycoonTrainer.Game;

namespace RailroadTycoonTrainer.ViewModels;

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
/// The live-memory scanner. Railroad Tycoon is a real-mode DOS program, so — like the repo's other
/// DOSBox trainers — we attach to the <b>emulator</b> process (DOSBox / DOSBox-X), whose address space
/// contains the DOS guest's RAM mapped verbatim. Two paths reach the game's state:
/// <list type="bullet">
/// <item><b>Auto-locate cash (no scan):</b> <see cref="GameLocator"/> finds the data segment by its
/// static label strings and pins the player's cash word straight to the Freezes tab — one click, no
/// scanning. This is the primary path.</item>
/// <item><b>Value scan (fallback / everything else):</b> a Cheat-Engine-style flow — snapshot, narrow
/// by what a number does on screen, pin to a freeze table re-written each poll tick. Immune to build
/// shifts, and the way to reach values the locator doesn't map.</item>
/// </list>
/// Cash is a signed 16-bit word in units of $1,000 (a screen value of "$1,000,000" is stored as 1000),
/// so the guided cash scan searches for the dollars-shown ÷ 1000 as an Int16.
/// </summary>
public sealed class LiveScannerViewModel : ObservableObject, IScanHost, IDisposable
{
    // Emulator process names that host a DOS guest. The game itself is not a Windows process — we attach
    // to whichever of these is running the game. Sorted to the top of the picker (still attachable to any).
    private static readonly string[] TargetHints =
        { "dosbox-x", "dosbox", "dosbox-staging" };

    private const int MaxResultRows = 1000;
    private const int LiveRefreshThreshold = 200;

    /// <summary>Label for the auto-located cash pin — the single source both auto-locate and max-cash key on.</summary>
    private const string CashLabel = "Cash ($000s)";

    private readonly byte[] _ioBuf = new byte[4];

    private ProcessMemory? _mem;
    private MemorySearcher? _searcher;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;
    private int _targetPid;
    private string _pendingPinLabel = "";

    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ObservableCollection<ScanResultViewModel> Results { get; } = new();
    public ObservableCollection<FrozenValueViewModel> Frozen { get; } = new();

    public IReadOnlyList<ScanWidth> Widths { get; } = new[] { ScanWidth.Byte, ScanWidth.Int16, ScanWidth.Int32 };

    private ScanWidth _selectedWidth = ScanWidth.Int16;   // cash — the headline value — is a 16-bit word
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

    private string _status =
        "Start Railroad Tycoon in DOSBox, pick the dosbox process, and Attach. " +
        "Then click \"Auto-locate cash\" — no manual searching needed.";
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
    public ICommand MaxCashCommand { get; }
    public ICommand CashGuideCommand { get; }
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
        MaxCashCommand = new RelayCommand(_ => MaxCash(), _ => IsAttached && !IsScanning);
        CashGuideCommand = new RelayCommand(_ => ShowCashGuide(), _ => IsAttached && !IsScanning);
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
                bool hit = TargetHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));
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
                     "Click \"Auto-locate cash\" for one-click access, or use a guided scan below.";
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
        Results.Clear();
        Frozen.Clear();
        SelectedResult = null;
        SelectedFrozen = null;
        MatchCount = "";
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
        RaiseCommands();
        Status = $"Pinned {r.AddressHex}. Edit Target to poke a value, or tick Freeze to hold it against the fiscal tick.";
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

    // --- auto-locate (no scan) -----------------------------------------------
    /// <summary>
    /// Finds the game's data segment by its static label strings and pins the player's cash word to the
    /// Freezes tab — no value scan. Runs on a background thread because it scans the whole emulator
    /// address space for the anchor. If nothing validates (a different EXE build, or the game isn't at
    /// a point where the segment is populated) it tells the user to fall back to the guided cash scan.
    /// </summary>
    private async void AutoLocate()
    {
        var (loc, aborted) = await RunLocate("Auto-locating the data segment (scanning the emulator's memory for the game)…");
        if (aborted) return;
        if (loc == null)
        {
            Status = "Auto-locate couldn't find the game's data segment. Make sure Railroad Tycoon is " +
                     "actually running in this DOSBox (past the title screen), or use the guided cash scan below.";
            return;
        }

        var pin = PinCash(loc.CashAddress);
        AddOrGetPin(loc.YearAddress, ScanWidth.Int16, "Year");
        SelectedFrozen = pin;
        Status = $"Found your cash at 0x{(ulong)loc.CashAddress:X} = {loc.CashThousands:N0} " +
                 $"(${loc.CashDollars:N0}); game year {loc.Year}. Added both to Freezes — click " +
                 "\"Set max cash\", edit a Target (cash is in $1,000s), or tick Freeze to hold it " +
                 "(freeze Year to stop the retirement clock). No scanning needed.";
    }

    /// <summary>
    /// One-click "give me money": auto-locates cash (if not already pinned) and sets it to the game's own
    /// $30M ceiling, then freezes it so the fiscal tick can't drain it.
    /// </summary>
    private async void MaxCash()
    {
        var existing = Frozen.FirstOrDefault(f => f.Label == CashLabel);
        if (existing == null)
        {
            var (loc, aborted) = await RunLocate("Locating your cash before maxing it…");
            if (aborted) return;
            if (loc == null)
            {
                Status = "Couldn't locate cash automatically — use the guided cash scan, then edit its Target.";
                return;
            }
            existing = PinCash(loc.CashAddress);
        }

        existing.Target = RtLayout.MaxCashThousands;
        existing.Frozen = true;
        SelectedFrozen = existing;
        Status = $"Cash set to {RtLayout.MaxCashThousands:N0} (${RtLayout.ThousandsToDollars(RtLayout.MaxCashThousands):N0}) " +
                 "and frozen — the game's own $30M ceiling. Untick Freeze to let it move again.";
    }

    /// <summary>
    /// Runs the (background) data-segment locate shared by <see cref="AutoLocate"/> and
    /// <see cref="MaxCash"/>: it owns the busy flag, the cancellation-token lifecycle, the staleness
    /// guard and the error handling, so the two callers differ only in what they do with the result.
    /// Returns <c>(loc, aborted)</c>: <c>aborted</c> is true when the run was cancelled, errored, or the
    /// attachment changed under it (nothing more to do); otherwise <c>loc</c> is the location or null if
    /// the segment wasn't found.
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

    /// <summary>Adds a pinned row for a known address (used by auto-locate); returns the existing one if present.</summary>
    private FrozenValueViewModel AddOrGetPin(nuint address, ScanWidth width, string label, bool signed = false)
    {
        var existing = Frozen.FirstOrDefault(f => f.Address == address);
        if (existing != null) return existing;
        long current = ReadAt(address, width, out long v) ? v : 0;
        var pin = new FrozenValueViewModel(this, address, width, current, label, signed);
        Frozen.Add(pin);
        RaiseCommands();
        return pin;
    }

    /// <summary>
    /// Pins the (signed) cash word at <paramref name="address"/>, first dropping any earlier cash pin left
    /// at a different address — e.g. if the game was quit and restarted inside the same DOSBox, its data
    /// segment can reload at a new host address, and a stale pin would otherwise linger and be the one
    /// <see cref="MaxCash"/> keys on by label.
    /// </summary>
    private FrozenValueViewModel PinCash(nuint address)
    {
        var stale = Frozen.FirstOrDefault(f => f.Label == CashLabel && f.Address != address);
        if (stale != null)
        {
            Frozen.Remove(stale);
            if (SelectedFrozen == stale) SelectedFrozen = null;
        }
        return AddOrGetPin(address, ScanWidth.Int16, CashLabel, signed: true);
    }

    // --- guided scans --------------------------------------------------------
    private void BeginGuide(ScanWidth width, string label)
    {
        if (_selectedWidth != width) SelectedWidth = width;   // setter runs NewScan()
        else NewScan();
        _pendingPinLabel = label;   // set after NewScan(), which clears it, so the next pin is labelled
    }

    private void ShowCashGuide()
    {
        BeginGuide(ScanWidth.Int16, CashLabel);
        Status = "Cash guide: cash is stored in $1,000s, so read the dollar figure on the top-right panel and " +
                 "drop the last three zeros — \"$1,000,000\" → type 1000 → First Scan. Earn or spend some so it " +
                 "changes → type the new thousands value → Exact. Repeat until one row remains, then Pin and freeze. " +
                 "(If the guided scan is empty, the number may include separators — enter raw digits ÷ 1000 only.)";
    }

    private void ShowValueGuide()
    {
        NewScan();
        _pendingPinLabel = "";
        Status = "Value guide: pick a Type above (Byte / Int16 / Int32), read a number you can see in-game, type it " +
                 "→ First Scan; make it change → type the new value → Exact. Narrow to one row, then Pin and freeze. " +
                 "Use this for anything the auto-locate doesn't cover (e.g. a station or train count).";
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
        (MaxCashCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CashGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
