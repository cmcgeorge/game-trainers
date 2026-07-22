using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using ImperialismIITrainer.Game;

namespace ImperialismIITrainer.ViewModels;

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
/// The live-memory scanner. Unlike the repo's DOSBox trainers, Imperialism II is a native 32-bit
/// Windows program (GOG build, June 1999), so we attach straight to <c>Imperialism II.exe</c> — no
/// emulator, no guest-address translation. It ships a linker map (<c>Imperialism II.map</c>) that
/// names the data model — the human power's treasury is a signed 32-bit <c>long</c> on a
/// <c>TGreatPower</c>, warehouse stockpiles are signed 16-bit. The map's own addresses are stale (it is
/// an earlier build than the shipped exe, Jan vs June 1999), but because the exe has a fixed image base
/// and no ASLR, the June-build anchor recovered by live RE is stable across launches: a static global
/// holds a pointer to the player's nation object. So the primary path is <see cref="AutoLocate"/> (via
/// <see cref="Game.GameLocator"/>) — one click pins the treasury and warehouse with <b>no scanning</b>.
/// The Cheat-Engine-style <b>value scan</b> (attach, snapshot, narrow by what a number does on-screen,
/// pin to a freeze table re-written each poll tick) stays as the fallback and for anything the locator
/// doesn't map.
/// </summary>
public sealed class LiveScannerViewModel : ObservableObject, IScanHost, IDisposable
{
    // Sort the target to the top of the process picker (and auto-select it). Purely cosmetic —
    // you can still attach to any process. ProcessName drops the ".exe", leaving "Imperialism II".
    private static readonly string[] TargetHints = { "imperialism" };

    private const int MaxResultRows = 1000;
    private const int LiveRefreshThreshold = 200;

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

    /// <summary>Commodity names for the resource guide, so a pinned warehouse value is labelled by good.</summary>
    public IReadOnlyList<string> Commodities { get; } = CommodityBook.Names;

    private string _selectedCommodity = "Fabric";
    public string SelectedCommodity { get => _selectedCommodity; set => SetField(ref _selectedCommodity, value); }

    private ScanWidth _selectedWidth = ScanWidth.Int32;
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

    private string _status = "Launch Imperialism II, pick the \"Imperialism II\" process, and Attach.";
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
    public ICommand TreasuryGuideCommand { get; }
    public ICommand ResourceGuideCommand { get; }
    public ICommand LaborGuideCommand { get; }

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
        TreasuryGuideCommand = new RelayCommand(_ => ShowTreasuryGuide(), _ => IsAttached && !IsScanning);
        ResourceGuideCommand = new RelayCommand(_ => ShowResourceGuide(), _ => IsAttached && !IsScanning);
        LaborGuideCommand = new RelayCommand(_ => ShowLaborGuide(), _ => IsAttached && !IsScanning);

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
                     "Use a guided scan below, or First Scan a value you can read in-game.";
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
        Status = $"Pinned {r.AddressHex}. Edit Target to poke a value, or tick Freeze to hold it against the turn tick.";
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
    /// Follows a static-global pointer to the player's nation object and pins its treasury and known
    /// warehouse slots straight to the Freezes table — no value scan. Works because Imperialism II is a
    /// native, no-ASLR exe whose globals sit at constant addresses; if the object can't be validated
    /// (a different build), it falls back to telling the user to use a guided scan.
    /// </summary>
    private void AutoLocate()
    {
        if (_mem is not { IsOpen: true }) return;
        NationLocation? loc;
        try { loc = new GameLocator(_mem).Locate(); }
        catch (Exception ex) { Status = "Auto-locate error: " + ex.Message; return; }

        if (loc == null)
        {
            Status = "Auto-locate couldn't validate a nation object — this game build may differ from the " +
                     "one the offsets were recovered on. Use a guided scan instead (it always works).";
            return;
        }

        int added = 0;
        if (AddPin(loc.TreasuryAddress, ScanWidth.Int32, "Treasury")) added++;
        foreach (var slot in NationLayout.WarehouseSlots)
            if (AddPin(loc.SlotAddress(slot.Offset), ScanWidth.Int16, slot.Name)) added++;

        RaiseCommands();
        Status = $"Auto-located the player nation at 0x{(ulong)loc.ObjectBase:X} (treasury {loc.Treasury:N0}). " +
                 $"Added {added} value(s) to the Freezes tab — edit Target to set, or tick Freeze to hold. " +
                 "No scanning needed.";
    }

    /// <summary>Adds a pinned row for an already-known address (used by auto-locate); skips duplicates.</summary>
    private bool AddPin(nuint address, ScanWidth width, string label)
    {
        if (Frozen.Any(f => f.Address == address)) return false;
        long current = ReadAt(address, width, out long v) ? v : 0;
        Frozen.Add(new FrozenValueViewModel(this, address, width, current, label));
        return true;
    }

    // --- guided scans --------------------------------------------------------
    private void BeginGuide(ScanWidth width, string label)
    {
        if (_selectedWidth != width) SelectedWidth = width;   // setter runs NewScan()
        else NewScan();
        _pendingPinLabel = label;   // set after NewScan(), which clears it, so the next pin is labelled
    }

    private void ShowTreasuryGuide()
    {
        BeginGuide(ScanWidth.Int32, "Treasury");
        Status = "Treasury guide: read your cash on the top bar, type it → First Scan; end a turn or buy/sell " +
                 "something so it changes → type the new value → Exact. Repeat until one row remains, then Pin and " +
                 "freeze. Treasury is a signed 32-bit value (it can go negative in debt). If a 32-bit scan finds " +
                 "nothing, the number you read may include a thousands separator — enter the raw digits only.";
    }

    private void ShowResourceGuide()
    {
        BeginGuide(ScanWidth.Int16, SelectedCommodity);
        Status = $"Resource guide ({SelectedCommodity}): open your warehouse, read the stored amount of " +
                 $"{SelectedCommodity}, type it → First Scan; transport or consume some so it changes → type the " +
                 "new value → Exact. Warehouse stockpiles are 16-bit — pick a different good above before the guide " +
                 "to label a different pin. Pin the survivor and freeze it so the turn's economy can't drain it.";
    }

    private void ShowLaborGuide()
    {
        BeginGuide(ScanWidth.Int16, "Labor");
        Status = "Labour guide: read a workforce count you can see (e.g. available labourers of one grade), type it " +
                 "→ First Scan; assign or free a worker so it changes → type the new value → Exact. Labour counts are " +
                 "small — if an Int16 scan is empty, switch the Type to Byte and start over.";
    }

    // --- poll loop -----------------------------------------------------------
    private void PollTick()
    {
        if (_mem == null) return;
        if (!_mem.IsOpen || HasTargetExited()) { Detach(); Status = "Target process exited."; return; }

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
        (TreasuryGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ResourceGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LaborGuideCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _mem?.Dispose();
    }
}
