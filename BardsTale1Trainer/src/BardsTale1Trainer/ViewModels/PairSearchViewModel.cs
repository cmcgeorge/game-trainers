using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Threading;
using BardsTale1Trainer.Memory;

namespace BardsTale1Trainer.ViewModels;

/// <summary>
/// One row in the X/Y search results: an address and the pair last read there. X and Y are
/// editable — typing a new value in the grid writes the two bytes straight to the game via
/// the <c>write</c> callback. A refresh updates the displayed values through
/// <see cref="SetFromMemory"/>, which suppresses the write so re-reads don't echo back.
/// </summary>
public sealed class PairResultViewModel : ObservableObject
{
    private readonly Func<nuint, int, int, bool>? _write;
    private bool _suppressWrite;

    public nuint Address { get; }
    private int _x;
    private int _y;

    public PairResultViewModel(nuint address, int x, int y, Func<nuint, int, int, bool>? write = null)
    {
        Address = address;
        _x = x;
        _y = y;
        _write = write;
    }

    public string AddressHex => $"0x{(ulong)Address:X}";

    public int X
    {
        get => _x;
        set { if (SetCoord(ref _x, value, nameof(X))) WriteIfUserEdit(); }
    }

    public int Y
    {
        get => _y;
        set { if (SetCoord(ref _y, value, nameof(Y))) WriteIfUserEdit(); }
    }

    /// <summary>The two raw bytes as stored: X then Y, one byte each (both clamped to 0–255).</summary>
    public string BytesHex => $"{_x:X2} {_y:X2}";

    /// <summary>Updates the displayed pair from a fresh memory read, without writing back.</summary>
    public void SetFromMemory(int x, int y)
    {
        _suppressWrite = true;
        X = x;
        Y = y;
        _suppressWrite = false;
    }

    private bool SetCoord(ref int field, int value, string name)
    {
        // Each coordinate is a single byte. Reject (rather than silently clamp) out-of-range
        // input so the grid's ValidatesOnExceptions binding flags it and no bad write fires;
        // refreshes always feed 0–255, so this never throws for them.
        if (value < 0 || value > 0xFF)
            throw new ArgumentOutOfRangeException(name, value, "Each coordinate must be 0–255.");
        if (!SetField(ref field, value, name)) return false;
        OnPropertyChanged(nameof(BytesHex));
        return true;
    }

    private void WriteIfUserEdit()
    {
        if (!_suppressWrite) _write?.Invoke(Address, _x, _y);
    }
}

/// <summary>
/// "X / Y Search": finds an address holding a pair of values stored as two adjacent
/// bytes — e.g. X=10, Y=5 is the byte pattern 0A 05. This is how the game keeps the
/// party's map position (North/East), which the roster format doesn't cover. A single
/// pair matches in many incidental places, so the useful flow is: Search, then move one
/// step in-game, enter the new X/Y, and Narrow — repeating until one address survives.
/// That address's X/Y can then be edited in place (writes straight to the game), and an
/// optional auto-refresh re-reads the survivors so you can watch them change as you move.
/// </summary>
public sealed class PairSearchViewModel : ObservableObject
{
    /// <summary>Rows materialised into the grid (the survivor count can be far larger).</summary>
    private const int DisplayCap = 500;

    /// <summary>Auto-refresh only kicks in once narrowed to at most this many survivors, so a
    /// broad first scan isn't re-reading hundreds of addresses every tick.</summary>
    private const int AutoRefreshMaxRows = 16;

    private readonly Func<ProcessMemory?> _getMem;
    private readonly Action<string> _setStatus;
    private readonly DispatcherTimer _autoTimer;
    private CancellationTokenSource? _cts;
    private List<nuint>? _candidates;   // surviving addresses, kept across narrowing passes
    // Once any pass is truncated the candidate set is permanently incomplete, so this is
    // sticky across narrowing (an un-truncated narrow can't un-drop earlier survivors);
    // only Reset() clears it.
    private bool _truncated;
    private bool _autoBusy;             // re-entrancy guard for the auto-refresh tick
    private bool _gridEditing;          // true while a grid cell is open for editing

    public PairSearchViewModel(Func<ProcessMemory?> getMem, Action<string> setStatus)
    {
        _getMem = getMem;
        _setStatus = setStatus;

        SearchCommand = new RelayCommand(() => Scan(fresh: true), () => CanScan && PatternValid);
        NarrowCommand = new RelayCommand(() => Scan(fresh: false), () => CanScan && PatternValid && HasResults);
        RefreshCommand = new RelayCommand(() => Refresh(quiet: false), () => HasResults && _getMem() != null);
        ResetCommand = new RelayCommand(Reset, () => _candidates != null || Results.Count > 0);

        // Created stopped; it runs only while there are results to watch (started on the
        // first successful search, stopped on Reset) so it isn't ticking before any search.
        _autoTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _autoTimer.Tick += (_, _) => AutoRefreshTick();
    }

    // --- inputs -----------------------------------------------------------------
    private string _xText = "";
    public string XText { get => _xText; set { if (SetField(ref _xText, value)) OnInputChanged(); } }

    private string _yText = "";
    public string YText { get => _yText; set { if (SetField(ref _yText, value)) OnInputChanged(); } }

    /// <summary>Shows exactly which bytes a Search will look for, so the encoding is never a guess.</summary>
    public string PatternPreview
    {
        get
        {
            var p = BuildPattern();
            return p == null
                ? "Enter X and Y (each 0–255, decimal or 0x…)."
                : $"Searching for: {p[0]:X2} {p[1]:X2}   (X and Y as adjacent bytes)";
        }
    }

    private bool _autoRefresh = true;
    /// <summary>When on, the survivors' live values are re-read periodically (once narrowed).</summary>
    public bool AutoRefresh { get => _autoRefresh; set => SetField(ref _autoRefresh, value); }

    // --- results ----------------------------------------------------------------
    public ObservableCollection<PairResultViewModel> Results { get; } = new();

    private PairResultViewModel? _selectedResult;
    public PairResultViewModel? SelectedResult
    {
        get => _selectedResult;
        set { if (SetField(ref _selectedResult, value)) RaiseCommands(); }
    }

    private string _resultSummary = "No search yet. Enter X and Y, then Search.";
    public string ResultSummary { get => _resultSummary; private set => SetField(ref _resultSummary, value); }

    private bool _isScanning;
    public bool IsScanning { get => _isScanning; private set { if (SetField(ref _isScanning, value)) RaiseCommands(); } }

    public bool HasResults => _candidates is { Count: > 0 } && !_isScanning;

    /// <summary>The single surviving address once the search is narrowed all the way down —
    /// i.e. "the party position lives here". Null while 0 or several candidates remain.
    /// The Maps tab uses this to draw the live marker and to teleport.</summary>
    public nuint? LockedAddress => _candidates is { Count: 1 } sole ? sole[0] : null;

    private bool CanScan => _getMem() != null && !_isScanning;
    private bool PatternValid => TryParseByte(_xText, out _) && TryParseByte(_yText, out _);

    // --- commands ---------------------------------------------------------------
    public RelayCommand SearchCommand { get; }
    public RelayCommand NarrowCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand ResetCommand { get; }

    /// <summary>Drops the current candidates/results (called on detach as well as Reset).</summary>
    public void Reset()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _candidates = null;
        _truncated = false;
        _autoTimer.Stop();          // nothing left to auto-refresh
        IsScanning = false;
        Results.Clear();
        SelectedResult = null;
        ResultSummary = "No search yet. Enter X and Y, then Search.";
        RaiseCommands();
    }

    // A fresh Search replaces the candidate set; Narrow intersects the new pattern's hits
    // with the survivors so a moved-and-re-entered pair pins down the live address.
    private async void Scan(bool fresh)
    {
        var mem = _getMem();
        if (mem == null) { _setStatus("Attach to the game first."); return; }
        var pattern = BuildPattern();
        if (pattern == null) { _setStatus("Enter X and Y first (each 0–255)."); return; }
        var existing = fresh ? null : _candidates;

        var (myCts, ct) = BeginRun();
        _setStatus(fresh ? "Searching memory for the pair…" : "Narrowing to addresses that now hold the pair…");

        ScanOutcome outcome;
        try
        {
            outcome = await Task.Run(() => RunScan(mem, pattern, existing, ct), ct);
        }
        catch (OperationCanceledException) { EndIfCurrent(myCts); return; }
        catch (Exception ex) { EndIfCurrent(myCts, "Scan error: " + ex.Message); return; }

        // A detach (or newer run) may have happened while we were on the pool thread; only
        // publish if we're still the live run against the same process handle.
        if (ct.IsCancellationRequested || !ReferenceEquals(myCts, _cts) || !ReferenceEquals(mem, _getMem()))
            return;

        if (fresh) _truncated = false;        // a fresh search starts coverage from scratch
        _candidates = outcome.Candidates;
        _truncated |= outcome.Truncated;       // sticky: never un-truncate a pruned set
        IsScanning = false;
        Publish(outcome.Rows, rebuild: true);
    }

    // Re-reads the live values at the current candidates without re-scanning. Runs the reads
    // on the pool thread (a wide result set is one syscall per row) and supersedes like a scan.
    private async void Refresh(bool quiet)
    {
        var mem = _getMem();
        var candidates = _candidates;
        if (mem == null || candidates == null) return;

        var (myCts, ct) = BeginRun();
        if (!quiet) _setStatus("Re-reading values from the game…");

        List<PairRow> rows;
        try
        {
            rows = await Task.Run(() => ReadRows(mem, candidates, ct), ct);
        }
        catch (OperationCanceledException) { EndIfCurrent(myCts); return; }
        catch (Exception ex) { EndIfCurrent(myCts, "Refresh error: " + ex.Message); return; }

        if (ct.IsCancellationRequested || !ReferenceEquals(myCts, _cts) || !ReferenceEquals(mem, _getMem()))
            return;

        IsScanning = false;
        Publish(rows, rebuild: false);
    }

    // Lightweight periodic re-read once the survivors are few: updates the grid rows in place
    // (no rebuild, no status churn, no scan supersession) so it can't clobber a selection or
    // interrupt an in-flight scan. Skips itself while a heavier scan/refresh is running.
    private async void AutoRefreshTick()
    {
        if (_autoBusy || _isScanning || !_autoRefresh) return;
        var mem = _getMem();
        var candidates = _candidates;
        if (mem == null || candidates == null || candidates.Count == 0 || candidates.Count > AutoRefreshMaxRows)
            return;

        _autoBusy = true;
        try
        {
            var rows = await Task.Run(() => ReadRows(mem, candidates, CancellationToken.None));
            // Bail if anything moved under us while we were reading.
            if (_isScanning || !ReferenceEquals(mem, _getMem()) || !ReferenceEquals(candidates, _candidates))
                return;
            ApplyInPlace(rows);
        }
        catch { /* transient read failure; the next tick tries again */ }
        finally { _autoBusy = false; }
    }

    // Pool-thread work: scan for the pattern, intersect with survivors when narrowing, then
    // read the display rows — so the UI thread only ever rebuilds the grid, never does I/O.
    private static ScanOutcome RunScan(ProcessMemory mem, byte[] pattern, List<nuint>? intersectWith, CancellationToken ct)
    {
        var scan = BytePatternScanner.Find(mem, pattern, ct);
        List<nuint> candidates;
        if (intersectWith == null)
        {
            candidates = scan.Addresses;
        }
        else
        {
            var fresh = new HashSet<nuint>(scan.Addresses);
            candidates = new List<nuint>();
            foreach (var a in intersectWith)
            {
                ct.ThrowIfCancellationRequested();
                if (fresh.Contains(a)) candidates.Add(a);
            }
        }
        return new ScanOutcome(candidates, ReadRows(mem, candidates, ct), scan.Truncated);
    }

    private static List<PairRow> ReadRows(ProcessMemory mem, List<nuint> addrs, CancellationToken ct)
    {
        int shown = Math.Min(addrs.Count, DisplayCap);
        var rows = new List<PairRow>(shown);
        for (int i = 0; i < shown; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (x, y) = ReadPair(mem, addrs[i]);
            rows.Add(new PairRow(addrs[i], x, y));
        }
        return rows;
    }

    // UI-thread only: rebuild the grid from already-read rows, or (on refresh) update in place.
    private void Publish(List<PairRow> rows, bool rebuild)
    {
        if (rebuild)
        {
            Results.Clear();
            foreach (var r in rows) Results.Add(new PairResultViewModel(r.Address, r.X, r.Y, TryWrite));
            // Auto-select the sole survivor so it's immediately editable without an extra click.
            SelectedResult = Results.Count == 1 ? Results[0] : null;
            // Run the auto-refresh poll only while there's something to watch.
            if (Results.Count > 0) _autoTimer.Start(); else _autoTimer.Stop();
        }
        else
        {
            ApplyInPlace(rows);
        }

        int total = _candidates?.Count ?? 0;
        string trunc = _truncated ? "  ⚠ scan hit a cap — coverage may be incomplete; narrow and try again" : "";
        string tail = total == 1
            ? " Edit its X or Y in the grid to move the party."
            : " Move one step in-game, enter the new X/Y, and Narrow to home in.";
        ResultSummary = total == 0
            ? "No matches. Check X and Y match what's in-game (or Reset and Search again)."
            : (total > DisplayCap ? $"Showing first {DisplayCap} of {total:N0} match(es)." : $"{total:N0} match(es).")
              + tail + trunc;
        _setStatus($"X/Y search: {ResultSummary}");
        RaiseCommands();
    }

    // Update existing rows' values without recreating them (preserves selection / in-grid edits
    // and avoids list churn). Only valid when the candidate set is unchanged, which is the case
    // for any refresh — addresses line up by index with the displayed rows.
    private void ApplyInPlace(List<PairRow> rows)
    {
        if (Results.Count != rows.Count) return;
        for (int i = 0; i < rows.Count; i++)
        {
            if (Results[i].Address != rows[i].Address) return;   // ordering diverged; leave as-is
            // Don't stomp a row the user is mid-edit on — that would revert their keystrokes
            // (the edit only commits to memory on cell exit). The selected row is the open one.
            if (_gridEditing && ReferenceEquals(Results[i], _selectedResult)) continue;
            Results[i].SetFromMemory(rows[i].X, rows[i].Y);
        }
    }

    /// <summary>The grid raises these around a cell edit so the auto-refresh poll won't
    /// overwrite the value while the user is typing it.</summary>
    public void BeginGridEdit() => _gridEditing = true;
    public void EndGridEdit() => _gridEditing = false;

    // Writer handed to each row: pushes an edited X/Y straight to the game as two bytes.
    private bool TryWrite(nuint addr, int x, int y)
    {
        var mem = _getMem();
        if (mem == null) { _setStatus("Attach to the game first."); return false; }
        bool ok = mem.Write(addr, new[] { (byte)x, (byte)y });
        _setStatus(ok ? $"Wrote X={x}, Y={y} to 0x{(ulong)addr:X}." : $"Write to 0x{(ulong)addr:X} failed.");
        return ok;
    }

    // --- run lifecycle ----------------------------------------------------------
    // Supersede any in-flight run, mirroring MemorySearchViewModel: capture the CTS locally
    // so a later Reset/Detach can't leave this run mutating disposed state.
    private (CancellationTokenSource Cts, CancellationToken Token) BeginRun()
    {
        var oldCts = _cts;
        var myCts = new CancellationTokenSource();
        _cts = myCts;
        oldCts?.Cancel();
        oldCts?.Dispose();
        IsScanning = true;
        return (myCts, myCts.Token);
    }

    // Clear the busy flag only if we're still the current run; a newer run owns that state
    // otherwise, and clearing it here would re-enable the commands mid-run.
    private void EndIfCurrent(CancellationTokenSource myCts, string? status = null)
    {
        if (!ReferenceEquals(myCts, _cts)) return;
        IsScanning = false;
        if (status != null) _setStatus(status);
    }

    // --- helpers ----------------------------------------------------------------
    private byte[]? BuildPattern()
    {
        if (!TryParseByte(_xText, out int x) || !TryParseByte(_yText, out int y)) return null;
        return new[] { (byte)x, (byte)y };
    }

    private static (int X, int Y) ReadPair(ProcessMemory mem, nuint addr)
    {
        var b = mem.Read(addr, 2);
        if (b.Length < 2) return (0, 0);
        return (b[0], b[1]);
    }

    private static bool TryParseByte(string? s, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        int parsed;
        bool ok = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed)
            : int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
        if (!ok || parsed < 0 || parsed > 0xFF) return false;
        value = parsed;
        return true;
    }

    private void OnInputChanged()
    {
        OnPropertyChanged(nameof(PatternPreview));
        RaiseCommands();
    }

    private void RaiseCommands()
    {
        OnPropertyChanged(nameof(HasResults));
        SearchCommand.RaiseCanExecuteChanged();
        NarrowCommand.RaiseCanExecuteChanged();
        RefreshCommand.RaiseCanExecuteChanged();
        ResetCommand.RaiseCanExecuteChanged();
    }

    // A display row read off the pool thread (address + the pair currently stored there).
    private readonly record struct PairRow(nuint Address, int X, int Y);

    // Pool-thread scan result: the surviving candidates, the capped display rows, truncation.
    private sealed record ScanOutcome(List<nuint> Candidates, List<PairRow> Rows, bool Truncated);
}
