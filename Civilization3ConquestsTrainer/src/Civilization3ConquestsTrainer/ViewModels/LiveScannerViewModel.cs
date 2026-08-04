using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Civilization3ConquestsTrainer.ViewModels;

/// <summary>
/// The Cheat-Engine-style value scanner, kept as the build-independent fallback for when the locator
/// cannot validate the game (a patch that moved the globals, or a build this table was not recovered
/// against).
///
/// <para>One Civ3-specific caveat governs this whole tab and the guides below say so: <b>an exact
/// scan for your treasury will always return nothing</b>. The game stores gold as two fields whose
/// sum is the real number, seeded per civ per game, so the value on your top bar is never in memory.
/// The only workable route is a relative scan — snapshot, spend or earn, narrow by Changed — which
/// converges on the encoded half. City food and shields, unit damage and the turn counter are all
/// plain integers and scan normally.</para>
///
/// <para>The process is owned by <see cref="MainViewModel"/>, which attaches once and shares the
/// handle, so the scanner never opens the game a second time.</para>
/// </summary>
public sealed class LiveScannerViewModel : ObservableObject, IScanHost, IDisposable
{
    private const int MaxResultRows = 1000;
    private const int LiveRefreshThreshold = 200;

    private readonly byte[] _ioBuf = new byte[4];

    private ProcessMemory? _mem;
    private MemorySearcher? _searcher;
    private CancellationTokenSource? _scanCts;
    private string _pendingPinLabel = "";

    public ObservableCollection<ScanResultViewModel> Results { get; } = new();
    public ObservableCollection<FrozenValueViewModel> Frozen { get; } = new();

    public IReadOnlyList<ScanWidth> Widths { get; } = new[] { ScanWidth.Byte, ScanWidth.Int16, ScanWidth.Int32 };

    private ScanWidth _selectedWidth = ScanWidth.Int32;
    public ScanWidth SelectedWidth
    {
        get => _selectedWidth;
        set { if (SetField(ref _selectedWidth, value)) NewScan(); }
    }

    private string _scanText = "";
    public string ScanText { get => _scanText; set => SetField(ref _scanText, value); }

    private ScanResultViewModel? _selectedResult;
    public ScanResultViewModel? SelectedResult
    {
        get => _selectedResult;
        set { SetField(ref _selectedResult, value); RaiseCommands(); }
    }

    private FrozenValueViewModel? _selectedFrozen;
    public FrozenValueViewModel? SelectedFrozen
    {
        get => _selectedFrozen;
        set { SetField(ref _selectedFrozen, value); RaiseCommands(); }
    }

    public bool IsAttached => _mem is { IsOpen: true };
    public bool HasResults => _searcher is { HasMatches: true };

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set { if (SetField(ref _isScanning, value)) { OnPropertyChanged(nameof(NotScanning)); RaiseCommands(); } }
    }

    public bool NotScanning => !_isScanning;

    private string _matchCount = "";
    public string MatchCount { get => _matchCount; private set => SetField(ref _matchCount, value); }

    private string _status = "Attach on the toolbar above. Auto-locate handles everything this tab does — " +
                             "use the scanner only if it fails.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    public ICommand FirstScanCommand { get; }
    public ICommand NextScanCommand { get; }
    public ICommand NewScanCommand { get; }
    public ICommand PinCommand { get; }
    public ICommand RemoveFrozenCommand { get; }
    public ICommand FreezeAllCommand { get; }
    public ICommand FreezeNoneCommand { get; }
    public ICommand TreasuryGuideCommand { get; }
    public ICommand CityGuideCommand { get; }
    public ICommand UnitGuideCommand { get; }
    public ICommand TurnGuideCommand { get; }

    public LiveScannerViewModel()
    {
        FirstScanCommand = new RelayCommand(_ => FirstScan(), _ => IsAttached && !IsScanning && !HasResults);
        NextScanCommand = new RelayCommand(NextScan, _ => IsAttached && !IsScanning && HasResults);
        NewScanCommand = new RelayCommand(_ => NewScan(), _ => IsAttached && !IsScanning && HasResults);
        PinCommand = new RelayCommand(_ => PinSelected(), _ => SelectedResult != null);
        RemoveFrozenCommand = new RelayCommand(_ => RemoveFrozen(), _ => SelectedFrozen != null);
        FreezeAllCommand = new RelayCommand(_ => SetAllFrozen(true), _ => Frozen.Count > 0);
        FreezeNoneCommand = new RelayCommand(_ => SetAllFrozen(false), _ => Frozen.Count > 0);
        TreasuryGuideCommand = new RelayCommand(_ => ShowTreasuryGuide(), _ => IsAttached && !IsScanning);
        CityGuideCommand = new RelayCommand(_ => ShowCityGuide(), _ => IsAttached && !IsScanning);
        UnitGuideCommand = new RelayCommand(_ => ShowUnitGuide(), _ => IsAttached && !IsScanning);
        TurnGuideCommand = new RelayCommand(_ => ShowTurnGuide(), _ => IsAttached && !IsScanning);
    }

    // --- lifecycle (driven by MainViewModel, which owns the handle) --------------------------------

    /// <summary>
    /// Whether pokes and freezes from this tab are permitted. Set by the shell, which carries the
    /// multiplayer check — without it the scanner would be a way around the read-only guarantee the
    /// rest of the trainer makes for PBEM and offline-MP games.
    /// </summary>
    public bool WritesAllowed { get; set; } = true;

    /// <summary>Adopts the shell's already-open process handle.</summary>
    public void AttachTo(ProcessMemory mem, int pid)
    {
        _mem = mem;
        _searcher = new MemorySearcher(mem, SelectedWidth);
        Results.Clear();
        Frozen.Clear();
        OnPropertyChanged(nameof(IsAttached));
        OnPropertyChanged(nameof(HasResults));
        RaiseCommands();
        Status = $"Scanner ready on pid {pid}. Prefer Auto-locate; use a guided scan only if it fails.";
    }

    /// <summary>Drops the shared handle. The shell disposes it.</summary>
    public void DetachFrom()
    {
        _scanCts?.Cancel();
        WritesAllowed = true;
        _mem = null;
        _searcher = null;
        Results.Clear();
        Frozen.Clear();
        SelectedResult = null;
        SelectedFrozen = null;
        MatchCount = "";
        OnPropertyChanged(nameof(IsAttached));
        OnPropertyChanged(nameof(HasResults));
        RaiseCommands();
    }

    /// <summary>Re-applies freezes and refreshes visible values. Called from the shell's poll loop.</summary>
    public void PollTick()
    {
        if (_mem is not { IsOpen: true }) return;

        foreach (var f in Frozen)
        {
            f.ApplyFreeze();
            if (ReadAt(f.Address, f.Width, out long live)) f.RefreshLive(live);
        }

        if (_searcher != null && !IsScanning && Results.Count > 0 && Results.Count <= LiveRefreshThreshold)
            foreach (var r in Results)
                if (_searcher.ReadValue(r.Address, out long live)) r.RefreshLive(live);
    }

    // --- scanning -----------------------------------------------------------------------------------

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
        if (parameter is not ScanCompare compare && !Enum.TryParse(parameter?.ToString(), out compare)) return;

        long value = 0;
        if (compare == ScanCompare.Exact)
        {
            if (!ScanValue.TryParse(ScanText, out value)) { Status = "Enter a value for an Exact scan."; return; }
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
            foreach (var m in searcher.Take(MaxResultRows))
                Results.Add(new ScanResultViewModel(m.Address, m.Value));

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

    // --- pin / freeze -------------------------------------------------------------------------------

    private void PinSelected()
    {
        var r = SelectedResult;
        if (r == null) return;
        if (Frozen.Any(f => f.Address == r.Address)) { Status = $"{r.AddressHex} is already pinned."; return; }
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

    // --- guided scans --------------------------------------------------------------------------------

    private void BeginGuide(ScanWidth width, string label)
    {
        if (_selectedWidth != width) SelectedWidth = width;   // setter runs NewScan()
        else NewScan();
        _pendingPinLabel = label;   // set after NewScan(), which clears it, so the next pin is labelled
    }

    private void ShowTreasuryGuide()
    {
        BeginGuide(ScanWidth.Int32, "Treasury (encoded half)");
        Status = "Treasury guide — read this before you start: an EXACT scan for the gold on your top bar will " +
                 "find nothing, because Civ3 never stores that number. It keeps two fields that sum to it. " +
                 "So: leave the value box EMPTY and First Scan (unknown value); buy or sell something so your " +
                 "gold moves; narrow with Changed; repeat until few rows remain. What survives is the encoded " +
                 "half, which moves one-for-one with your treasury. Auto-locate does all of this instantly — " +
                 "only come here if it failed.";
    }

    private void ShowCityGuide()
    {
        BeginGuide(ScanWidth.Int32, "City store");
        Status = "City guide: open a city and read its stored food or shields, type it → First Scan; end a turn " +
                 "so the store grows → type the new value → Exact. These are ordinary 32-bit integers, so an " +
                 "exact scan works normally here.";
    }

    private void ShowUnitGuide()
    {
        BeginGuide(ScanWidth.Int32, "Unit damage");
        Status = "Unit guide: the record stores hit points LOST, not remaining — a healthy unit reads 0. Take a " +
                 "unit into combat, note how many bars it lost, scan that number, heal or take more damage and " +
                 "narrow. Movement works the same way (points spent, not left).";
    }

    private void ShowTurnGuide()
    {
        BeginGuide(ScanWidth.Int32, "Turn number");
        Status = "Turn guide: type the current turn number → First Scan; press Enter to end a turn → Increased. " +
                 "Two or three rounds pin it. Note the turn counter is not the displayed year — Civ3 derives the " +
                 "year from the turn and the era's rules.";
    }

    // --- IScanHost ------------------------------------------------------------------------------------

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
        if (!WritesAllowed)
        {
            Status = "Writes are disabled: this is a multiplayer or play-by-email game, and editing " +
                     "one side of a shared game desynchronises it.";
            return false;
        }
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
        foreach (var c in new[]
                 {
                     FirstScanCommand, NextScanCommand, NewScanCommand, PinCommand, RemoveFrozenCommand,
                     FreezeAllCommand, FreezeNoneCommand, TreasuryGuideCommand, CityGuideCommand,
                     UnitGuideCommand, TurnGuideCommand,
                 })
            (c as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
    }
}
