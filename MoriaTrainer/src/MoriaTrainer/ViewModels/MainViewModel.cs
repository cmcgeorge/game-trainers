using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using MoriaTrainer.Game;

namespace MoriaTrainer.ViewModels;

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
/// Root view-model. Because UMoria 5.5.2 is a DPMI program whose state lives in a run-time heap with
/// no strong static signature (see <c>.docs/ReverseEngineering.md</c>), the reliable primitive is a
/// Cheat-Engine-style <b>value scan</b>: attach to the emulator, snapshot memory, and narrow by what
/// the on-screen number does (HP, gold, level, stats…). Survivors are pinned to a freeze table that
/// re-writes them every poll tick. The Teleport tab uses a separate relative-scan workflow to locate
/// <c>char_row</c>/<c>char_col</c> for teleportation. The Maps/Paragraphs/Items/Reference tabs are
/// read-only game-knowledge surfaces that need no attach.
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

    public IReadOnlyList<ScanWidth> Widths { get; } = new[] { ScanWidth.Byte, ScanWidth.Int16, ScanWidth.Int32 };
    public IReadOnlyList<ScanRecipe> Recipes => ScanGuide.Recipes;

    // --- child tabs ---------------------------------------------------------
    public TeleportViewModel Teleport { get; } = new();
    public CharacterViewModel Character { get; } = new();
    public LiveInventoryViewModel LiveInventory { get; } = new();
    public DungeonMapViewModel DungeonMap { get; } = new();
    public MapsViewModel Maps { get; } = new();
    public ParagraphsViewModel Paragraphs { get; } = new();
    public ItemsViewModel Items { get; } = new();
    public ReferenceViewModel Reference { get; } = new();

    private ScanWidth _selectedWidth = ScanWidth.Int32;
    public ScanWidth SelectedWidth
    {
        get => _selectedWidth;
        set { if (SetField(ref _selectedWidth, value)) NewScan(); }
    }

    /// <summary>
    /// The last guided-scan recipe the user applied, if any. Used to label pinned rows so the freeze
    /// table stays readable when several recipes share a width (gold and current HP are both Int32).
    /// Cleared by any action that breaks the link to that recipe (manual width change, New Scan).
    /// </summary>
    private ScanRecipe? _lastGuideRecipe;

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

    private string _matchCountText = "";
    public string MatchCount { get => _matchCountText; private set => SetField(ref _matchCountText, value); }

    private string _status = "Launch UMoria in DOSBox, pick the process, and Attach.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- commands -----------------------------------------------------------
    public ICommand RefreshProcessesCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand DetachCommand { get; }
    public ICommand FirstScanCommand { get; }
    public ICommand NextScanCommand { get; }
    public ICommand NewScanCommand { get; }
    public ICommand PinCommand { get; }
    public ICommand RemoveFrozenCommand { get; }
    public ICommand FreezeAllCommand { get; }
    public ICommand GuideScanCommand { get; }

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
        GuideScanCommand = new RelayCommand(p => ApplyGuideScan(p as ScanRecipe), _ => IsAttached && !IsScanning);

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
            Teleport.OnAttached(_mem);
            Character.OnAttached(_mem);
            LiveInventory.OnAttached(_mem);
            DungeonMap.OnAttached(_mem);
            _poll.Start();
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). " +
                     "Enter a value you can see on-screen and First Scan, or click a guided scan.";
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
        Teleport.OnDetached();
        Character.OnDetached();
        LiveInventory.OnDetached();
        DungeonMap.OnDetached();
        _mem?.Dispose();
        _mem = null;
        _searcher = null;
        _lastGuideRecipe = null;
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
        _lastGuideRecipe = null;   // a manual new scan breaks the link to the previous guided recipe
        if (_mem != null) _searcher = new MemorySearcher(_mem, SelectedWidth);
        Results.Clear();
        SelectedResult = null;
        MatchCount = "";
        OnPropertyChanged(nameof(HasResults));
        RaiseCommands();
        if (IsAttached) Status = $"New {SelectedWidth} scan. Enter a value and First Scan.";
    }

    /// <summary>
    /// Pre-fills the scan box with a guided scan recipe's width and default value, and surfaces a
    /// how-to-read hint in the status line. The user then does the First Scan themselves so they
    /// confirm the on-screen value before committing to a scan.
    /// </summary>
    private void ApplyGuideScan(ScanRecipe? recipe)
    {
        if (recipe == null) return;
        // Setting the width starts a fresh scan via the setter (which clears _lastGuideRecipe); set
        // the recipe after the width change so it survives. If the width already matches, NewScan
        // clears it and we restore it below.
        if (SelectedWidth != recipe.Width) SelectedWidth = recipe.Width;
        else NewScan();
        _lastGuideRecipe = recipe;
        ScanText = recipe.SuggestedDefault.ToString();
        Status = $"{recipe.DisplayName} guide: {recipe.HowToRead} Type the value you see and First Scan; " +
                 $"change it in-game (rest, buy, quaff a potion…), type the new value, and Exact. " +
                 $"Repeat until one row remains, then Pin it. ({recipe.Notes})";
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
        // Label the pin from the tracked guided recipe, if any. A manual (non-guided) scan leaves
        // _lastGuideRecipe null, so the row just gets a blank label the user can fill in.
        string label = _lastGuideRecipe?.DisplayName ?? "";
        Frozen.Add(new FrozenValueViewModel(this, r.Address, SelectedWidth, r.Value) { Label = label });
        RaiseCommands();
        Status = $"Pinned {r.AddressHex} ({SelectedWidth}). Edit its Target to poke a value, or tick Freeze to hold it.";
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
            if (ScanIo.ReadAt(_mem, f.Address, f.Width, out long live)) f.RefreshLive(live);
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
    // single-threaded searcher that a background scan may be driving. The byte-by-byte little-endian
    // translation is shared with the Teleport tab via <see cref="ScanIo"/>.
    bool IScanHost.Write(nuint address, long value, ScanWidth width) => ScanIo.WriteAt(_mem, address, value, width);
    bool IScanHost.Read(nuint address, ScanWidth width, out long value) => ScanIo.ReadAt(_mem, address, width, out value);
    void IScanHost.ReportWriteFailure(nuint address) =>
        Status = $"Write failed at 0x{(ulong)address:X}.";

    private void RaiseCommands()
    {
        (RefreshProcessesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AttachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (FirstScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NewScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PinCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemoveFrozenCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (FreezeAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (GuideScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        Teleport.Dispose();
        Character.Dispose();
        LiveInventory.Dispose();
        DungeonMap.Dispose();
        _mem?.Dispose();
    }
}
