using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using ThePerfectGeneral2Trainer.Game;

namespace ThePerfectGeneral2Trainer.ViewModels;

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
/// Root view-model. The Perfect General II is a DPMI program whose state lives in a run-time heap.
/// The trainer auto-locates the purchase state by scanning for the constant <c>D:\ICONS\MSGR.DAT</c>
/// anchor string the game loads into its DPMI heap, then derives the count array, Buy Points, and
/// Units Purchased at fixed offsets (see <c>Game/GameLocator.cs</c>). A Cheat-Engine-style value scan
/// remains available for scalars the locator doesn't cover (a unit's hit points during battle, turn
/// counters, etc.). A read-only Unit Reference surfaces the confirmed <c>UNITINFO.DOC</c> rules.
/// </summary>
public sealed class MainViewModel : ObservableObject, IScanHost, IDisposable
{
    private static readonly string[] EmulatorHints =
        { "dosbox", "dosbox-x", "dosbox-staging", "scummvm", "pcem", "86box", "qemu", "boxer" };

    /// <summary>Cap on rows copied into the results grid so a broad scan can't flood the UI.</summary>
    private const int MaxResultRows = 1000;

    /// <summary>Only live-refresh the results grid once it is this small (keeps the poll cheap).</summary>
    private const int LiveRefreshThreshold = 200;

    private ProcessMemory? _mem;
    private MemorySearcher? _searcher;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;

    public ObservableCollection<ProcessEntry> Processes { get; } = new();
    public ObservableCollection<ScanResultViewModel> Results { get; } = new();
    public ObservableCollection<FrozenValueViewModel> Frozen { get; } = new();
    public ObservableCollection<PurchaseItemViewModel> PurchaseItems { get; } = new();
    public ObservableCollection<UnitInfo> Units { get; } = new(UnitReference.Units);

    /// <summary>The scan widths offered in the UI.</summary>
    public IReadOnlyList<ScanWidth> Widths { get; } = new[] { ScanWidth.Byte, ScanWidth.Int16, ScanWidth.Int32 };

    private ScanWidth _selectedWidth = ScanWidth.Byte;
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

    /// <summary>Inverse of <see cref="IsScanning"/>, so the width combo can disable itself during a scan.</summary>
    public bool NotScanning => !_isScanning;

    /// <summary>True when the auto-locator found the game's anchor string (game is loaded).</summary>
    public bool IsLocated { get => _isLocated; private set => SetField(ref _isLocated, value); }
    private bool _isLocated;

    /// <summary>True when the purchase screen is active (count array validated).</summary>
    public bool PurchaseScreenActive { get => _purchaseScreenActive; private set => SetField(ref _purchaseScreenActive, value); }
    private bool _purchaseScreenActive;

    private string _matchCountText = "";
    public string MatchCount { get => _matchCountText; private set => SetField(ref _matchCountText, value); }

    private string _status = "Launch The Perfect General II in DOSBox, pick the process, and Attach.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- commands ------------------------------------------------------------
    /// <summary>Re-enumerates running processes (emulators first).</summary>
    public ICommand RefreshProcessesCommand { get; }
    /// <summary>Opens the selected process for reading/writing and starts the poll loop.</summary>
    public ICommand AttachCommand { get; }
    /// <summary>Closes the target handle and clears results/freezes.</summary>
    public ICommand DetachCommand { get; }
    /// <summary>Runs the first scan (exact value, or an unknown-value baseline when the box is blank).</summary>
    public ICommand FirstScanCommand { get; }
    /// <summary>Narrows the surviving candidates by the passed <see cref="ScanCompare"/>.</summary>
    public ICommand NextScanCommand { get; }
    /// <summary>Discards candidates and starts a fresh scan at the current width.</summary>
    public ICommand NewScanCommand { get; }
    /// <summary>Adds the selected candidate to the freeze table.</summary>
    public ICommand PinCommand { get; }
    /// <summary>Removes the selected pinned row.</summary>
    public ICommand RemoveFrozenCommand { get; }
    /// <summary>Freezes every pinned row at once.</summary>
    public ICommand FreezeAllCommand { get; }
    /// <summary>Sets up and explains the guided scan for Buy Points Remaining.</summary>
    public ICommand BuyPointsGuideCommand { get; }
    /// <summary>Auto-locates the purchase state (Buy Points + unit counts) by scanning for the game's anchor string.</summary>
    public ICommand AutoLocateCommand { get; }
    /// <summary>Freezes every purchase item at once.</summary>
    public ICommand FreezeAllPurchaseCommand { get; }
    /// <summary>Unfreezes every purchase item at once.</summary>
    public ICommand UnfreezeAllPurchaseCommand { get; }

    public MainViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(_ => RefreshProcesses());
        AttachCommand = new RelayCommand(_ => Attach(), _ => SelectedProcess != null && !IsAttached && !IsScanning);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached && !IsScanning);
        FirstScanCommand = new RelayCommand(_ => FirstScan(), _ => IsAttached && !IsScanning && !HasResults);
        NextScanCommand = new RelayCommand(p => NextScan(p), _ => IsAttached && !IsScanning && HasResults);
        NewScanCommand = new RelayCommand(_ => NewScan(), _ => IsAttached && !IsScanning && HasResults);
        PinCommand = new RelayCommand(_ => PinSelected(), _ => SelectedResult != null);
        RemoveFrozenCommand = new RelayCommand(_ => RemoveFrozen(), _ => SelectedFrozen != null);
        FreezeAllCommand = new RelayCommand(_ => SetAllFrozen(true), _ => Frozen.Count > 0);
        BuyPointsGuideCommand = new RelayCommand(_ => ShowBuyPointsGuide(), _ => IsAttached && !IsScanning);
        AutoLocateCommand = new RelayCommand(_ => _ = AutoLocateAsync(), _ => IsAttached && !IsScanning);
        FreezeAllPurchaseCommand = new RelayCommand(_ => SetAllPurchaseFrozen(true), _ => PurchaseItems.Count > 0);
        UnfreezeAllPurchaseCommand = new RelayCommand(_ => SetAllPurchaseFrozen(false), _ => PurchaseItems.Count > 0);

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
                bool emu = EmulatorHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));
                list.Add(new ProcessEntry(p.Id, name, emu));
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                /* process exited or is inaccessible between enumeration and query */
            }
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
            _searcher = new MemorySearcher(_mem, SelectedWidth);
            Results.Clear();
            OnPropertyChanged(nameof(IsAttached));
            OnPropertyChanged(nameof(HasResults));
            RaiseCommands();
            _poll.Start();
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). " +
                     "Enter a value you can see on-screen and First Scan, or use the Buy Points guide.";
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
        Results.Clear();
        Frozen.Clear();
        PurchaseItems.Clear();
        SelectedResult = null;
        SelectedFrozen = null;
        MatchCount = "";
        IsLocated = false;
        PurchaseScreenActive = false;
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
            // Bail if anything moved under us: detach/re-attach (new _mem), a New Scan or width
            // change (new _searcher for the same process), or a cancellation — publishing now would
            // show stale candidates.
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
        Frozen.Add(new FrozenValueViewModel(this, r.Address, SelectedWidth, r.Value));
        RaiseCommands();
        Status = $"Pinned {r.AddressHex}. Edit its Target to poke a value, or tick Freeze to hold it.";
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

    private void ShowBuyPointsGuide()
    {
        // Budgets can exceed 255, so scan at word width. Assigning a new width already starts a fresh
        // scan via the setter; only call NewScan directly when the width is already Int16.
        if (_selectedWidth != ScanWidth.Int16) SelectedWidth = ScanWidth.Int16;
        else NewScan();
        Status = "Buy Points guide: on the purchase screen type the Buy Points Remaining number → First Scan; " +
                 "buy or sell one unit so the number changes → type the new number → Exact. Repeat until one " +
                 "row remains, then Pin it and edit the Target.";
    }

    // --- auto-locate --------------------------------------------------------
    /// <summary>
    /// Scans the attached process for the <c>D:\ICONS\MSGR.DAT</c> anchor and, if found, populates the
    /// Purchase tab with Buy Points and per-type unit counts at fixed offsets from the anchor. When the
    /// game is not on the purchase screen, the count-array validator rejects the far-pointer soup that
    /// overwrites the area, and the status line tells the user to navigate to the purchase screen.
    /// </summary>
    private async Task AutoLocateAsync()
    {
        var mem = _mem;
        if (mem == null) return;

        IsScanning = true;
        Status = "Auto-locating purchase state…";
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        LocatedState state;
        try { state = await Task.Run(() => GameLocator.Locate(mem, ct), ct); }
        catch (OperationCanceledException) { IsScanning = false; RaiseCommands(); return; }
        catch (Exception ex) { IsScanning = false; Status = "Auto-locate error: " + ex.Message; RaiseCommands(); return; }

        if (mem != _mem) { IsScanning = false; RaiseCommands(); return; }    // detached mid-scan

        PurchaseItems.Clear();
        IsLocated = state.AnchorFound;
        PurchaseScreenActive = state.PurchaseScreenActive;

        if (!state.AnchorFound)
        {
            Status = state.Truncated
                ? "Game not found (memory scan was truncated — try again)."
                : "Game not found. Launch The Perfect General II in DOSBox and play into a scenario first.";
        }
        else if (!state.PurchaseScreenActive)
        {
            Status = "Game loaded, but the purchase screen is not active. Navigate to the purchase screen " +
                     "and click Auto-Locate again to edit Buy Points and unit counts.";
        }
        else
        {
            PopulatePurchaseItems(state);
            Status = $"Purchase state located. Buy Points: {state.BuyPoints}, Units Purchased: {state.UnitsPurchased}. " +
                     "Edit any Target to poke a value, or tick Freeze to hold it.";
        }

        IsScanning = false;
        RaiseCommands();
    }

    private void PopulatePurchaseItems(LocatedState state)
    {
        PurchaseItems.Clear();

        // Buy Points Remaining (Int16).
        PurchaseItems.Add(new PurchaseItemViewModel(
            this, "Buy Points Remaining", state.BuyPointsAddress, ScanWidth.Int16, state.BuyPoints));

        // Per-type purchased-unit counts (Byte), in purchase-screen order.
        for (int i = 0; i < PurchaseFormat.TypeCount; i++)
        {
            PurchaseItems.Add(new PurchaseItemViewModel(
                this,
                PurchaseFormat.TypeOrder[i],
                state.CountArrayAddress + (nuint)i,
                ScanWidth.Byte,
                state.CountArray.Length > i ? state.CountArray[i] : 0));
        }
    }

    private void SetAllPurchaseFrozen(bool frozen)
    {
        foreach (var p in PurchaseItems) p.Frozen = frozen;
        Status = frozen ? "All purchase values frozen." : "Purchase freeze cleared.";
    }

    // --- poll loop -----------------------------------------------------------
    private void PollTick()
    {
        if (_mem == null) return;
        if (!_mem.IsOpen) { Detach(); Status = "Target process exited."; return; }

        // Frozen rows read/write at their own captured width (independent of the current scan) and go
        // straight through _mem, so they don't touch the single-threaded searcher a scan may be using.
        foreach (var f in Frozen)
        {
            f.ApplyFreeze();
            if (ReadAt(f.Address, f.Width, out long live)) f.RefreshLive(live);
        }

        // Auto-located purchase items: same freeze/refresh cycle, reading at each item's own width.
        foreach (var p in PurchaseItems)
        {
            p.ApplyFreeze();
            if (ReadAt(p.Address, p.Width, out long live)) p.RefreshLive(live);
        }

        if (_searcher != null && !IsScanning && Results.Count > 0 && Results.Count <= LiveRefreshThreshold)
        {
            foreach (var r in Results)
                if (_searcher.ReadValue(r.Address, out long live)) r.RefreshLive(live);
        }
    }

    // --- IScanHost -----------------------------------------------------------
    // Reads and writes go through _mem directly (not the searcher) so a pinned row can use the width it
    // was captured at even after the active scan width changes, and so the poll loop never races the
    // single-threaded searcher that a background scan may be driving.
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
        (RemoveFrozenCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (FreezeAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (BuyPointsGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AutoLocateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (FreezeAllPurchaseCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (UnfreezeAllPurchaseCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _mem?.Dispose();
    }
}
