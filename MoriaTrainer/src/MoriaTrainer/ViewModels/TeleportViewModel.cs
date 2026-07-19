using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using MoriaTrainer.Game;

namespace MoriaTrainer.ViewModels;

/// <summary>
/// Backs the Teleport tab: locates the player's <c>char_row</c> and <c>char_col</c> globals by
/// relative scanning, then writes them to teleport. UMoria is DPMI so the addresses change every
/// session — there is no static anchor. The workflow mirrors a Cheat-Engine unknown-value scan,
/// done **twice** — once per axis:
///
/// 1. **Snapshot Position** — captures a baseline of every committed memory cell at Int16 width
///    (both globals are <c>int16</c>, see <c>.docs/ReverseEngineering.md</c> §3.5).
/// 2. **Walk one square** in a cardinal direction in-game (North = row−1, South = row+1, East = col+1,
///    West = col−1). Click the matching **Walked** button — it narrows the candidates by the
///    relative comparison (<see cref="ScanCompare.Increased"/> / <see cref="ScanCompare.Decreased"/>).
/// 3. **Repeat along the SAME axis** until one candidate remains. A directional narrowing pass
///    eliminates the other coordinate (it didn't change), so you must scan one axis at a time.
/// 4. **Pin** the survivor as Col (if you walked E/W) or Row (if you walked N/S).
/// 5. **Reset** and **Snapshot Position** again, then repeat steps 2-4 for the other axis.
/// 6. Type a target X (column) and Y (row) and click **Teleport**. The engine redraws the player on
///    the new cell next frame.
///
/// Matching by direction (not by exact delta) is deliberate: the trainer doesn't know the dungeon
/// bounds in advance, and a relative scan tolerates a candidate that drifted by 1 even if another
/// field also moved. The two globals are the only int16 cells that track cardinal moves consistently.
/// </summary>
public sealed class TeleportViewModel : ObservableObject, IScanHost, IDisposable
{
    private ProcessMemory? _mem;
    private MemorySearcher? _searcher;
    private CancellationTokenSource? _scanCts;
    private readonly DispatcherTimer _poll;

    private const int MaxResultRows = 500;
    private const int LiveRefreshThreshold = 100;

    public ObservableCollection<ScanResultViewModel> Candidates { get; } = new();

    public bool IsAttached => _mem is { IsOpen: true };
    public bool HasCandidates => _searcher is { HasMatches: true };

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        set { if (SetField(ref _isScanning, value)) { OnPropertyChanged(nameof(NotScanning)); RaiseCommands(); } }
    }
    public bool NotScanning => !_isScanning;

    private string _matchCount = "";
    public string MatchCount { get => _matchCount; private set => SetField(ref _matchCount, value); }

    private string _status =
        "Attach on the Character tab first, then Snapshot Position and walk cardinal directions in-game to narrow.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- pinned addresses (located char_col / char_row) ---------------------
    private nuint? _colAddress;
    public bool HasCol => _colAddress != null;
    public string ColAddressHex => _colAddress is { } a ? $"0x{(ulong)a:X}" : "(not pinned)";

    private nuint? _rowAddress;
    public bool HasRow => _rowAddress != null;
    public string RowAddressHex => _rowAddress is { } a ? $"0x{(ulong)a:X}" : "(not pinned)";

    private int _targetX = 30;
    /// <summary>Target column (char_col). UMoria's dungeon grid is 66×198, so 0..197.</summary>
    public int TargetX { get => _targetX; set => SetField(ref _targetX, Math.Clamp(value, 0, PlayerFormat.CaveCols - 1)); }

    private int _targetY = 10;
    /// <summary>Target row (char_row). 0..65.</summary>
    public int TargetY { get => _targetY; set => SetField(ref _targetY, Math.Clamp(value, 0, PlayerFormat.CaveRows - 1)); }

    // --- live position (read from pinned addresses for the green-dot display) ---
    private int _liveX = -1;
    public int LiveX { get => _liveX; private set => SetField(ref _liveX, value); }

    private int _liveY = -1;
    public int LiveY { get => _liveY; private set => SetField(ref _liveY, value); }

    private ScanResultViewModel? _selectedCandidate;
    public ScanResultViewModel? SelectedCandidate
    {
        get => _selectedCandidate;
        set { SetField(ref _selectedCandidate, value); RaiseCommands(); }
    }

    // --- commands -----------------------------------------------------------
    public ICommand SnapshotCommand { get; }
    public ICommand WalkedNorthCommand { get; }   // row decreases
    public ICommand WalkedSouthCommand { get; }   // row increases
    public ICommand WalkedEastCommand { get; }    // col increases
    public ICommand WalkedWestCommand { get; }    // col decreases
    public ICommand StayedCommand { get; }        // unchanged (no move)
    public ICommand ResetScanCommand { get; }
    public ICommand PinAsColCommand { get; }
    public ICommand PinAsRowCommand { get; }
    public ICommand TeleportCommand { get; }

    public TeleportViewModel()
    {
        SnapshotCommand    = new RelayCommand(_ => Snapshot(),     _ => IsAttached && !_isScanning && !HasCandidates);
        WalkedNorthCommand = new RelayCommand(_ => Walked(ScanCompare.Decreased), _ => IsAttached && !_isScanning && HasCandidates);
        WalkedSouthCommand = new RelayCommand(_ => Walked(ScanCompare.Increased), _ => IsAttached && !_isScanning && HasCandidates);
        WalkedEastCommand  = new RelayCommand(_ => Walked(ScanCompare.Increased), _ => IsAttached && !_isScanning && HasCandidates);
        WalkedWestCommand  = new RelayCommand(_ => Walked(ScanCompare.Decreased), _ => IsAttached && !_isScanning && HasCandidates);
        StayedCommand      = new RelayCommand(_ => Walked(ScanCompare.Unchanged), _ => IsAttached && !_isScanning && HasCandidates);
        ResetScanCommand   = new RelayCommand(_ => ResetScan(),   _ => !_isScanning && HasCandidates);
        PinAsColCommand    = new RelayCommand(_ => PinAsCol(),    _ => IsAttached && SelectedCandidate != null);
        PinAsRowCommand    = new RelayCommand(_ => PinAsRow(),    _ => IsAttached && SelectedCandidate != null);
        TeleportCommand    = new RelayCommand(_ => Teleport(),    _ => IsAttached && HasCol && HasRow);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _poll.Tick += (_, _) => PollTick();
    }

    // --- attach/detach (called by MainViewModel when the process changes) ----
    public void OnAttached(ProcessMemory mem)
    {
        _mem = mem;
        _searcher = new MemorySearcher(mem, ScanWidth.Int16);
        _poll.Start();
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Status = "Attached. Click Snapshot Position, then walk cardinal directions in-game.";
    }

    public void OnDetached()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _searcher = null;
        _mem = null;
        Candidates.Clear();
        SelectedCandidate = null;
        _colAddress = null;
        _rowAddress = null;
        LiveX = -1;
        LiveY = -1;
        MatchCount = "";
        OnPropertyChanged(nameof(IsAttached));
        OnPropertyChanged(nameof(HasCandidates));
        OnPropertyChanged(nameof(HasCol));
        OnPropertyChanged(nameof(HasRow));
        OnPropertyChanged(nameof(ColAddressHex));
        OnPropertyChanged(nameof(RowAddressHex));
        RaiseCommands();
        Status = "Detached.";
    }

    // --- scanning -----------------------------------------------------------
    private async void Snapshot()
    {
        var searcher = _searcher;
        if (searcher == null || IsScanning) return;
        IsScanning = true;
        Status = "Snapshotting memory (unknown-value baseline at Int16)…";
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        try
        {
            await Task.Run(() => searcher.FirstScanUnknown(ct), ct);
            if (ct.IsCancellationRequested) return;
            // Baseline captured; candidates are not materialised yet (MatchCount returns -1).
            PublishCandidates(searcher);
            Status = "Baseline captured. Walk one square North/South/East/West in-game and click the matching button.";
        }
        catch (OperationCanceledException) { Status = "Snapshot cancelled."; }
        catch (Exception ex) { Status = "Snapshot error: " + ex.Message; }
        finally { IsScanning = false; RaiseCommands(); }
    }

    private async void Walked(ScanCompare compare)
    {
        var searcher = _searcher;
        if (searcher == null || IsScanning) return;
        IsScanning = true;
        Status = $"Narrowing ({compare})…";
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;
        try
        {
            await Task.Run(() => searcher.NextScan(compare, 0, ct), ct);
            if (ct.IsCancellationRequested) return;
            PublishCandidates(searcher);
            int count = searcher.MatchCount;
            Status = count switch
            {
                0  => "No candidates matched. Click Reset and Snapshot again, and make sure you walked exactly one square in the right direction.",
                1  => "One candidate left for this axis. Pin it as Col (if you walked E/W) or Row (if you walked N/S). Then Reset and Snapshot again to scan the other axis.",
                2  => "Two candidates remain for this axis. Continue walking along the SAME axis (N/S for row, E/W for col) until one candidate remains.",
                _  => $"{count} candidates left. Walk another square along the SAME axis (or click Stayed) and click the matching button.",
            };
        }
        catch (OperationCanceledException) { Status = "Narrow cancelled."; }
        catch (Exception ex) { Status = "Narrow error: " + ex.Message; }
        finally { IsScanning = false; RaiseCommands(); }
    }

    private void PublishCandidates(MemorySearcher searcher)
    {
        int count = searcher.MatchCount;
        Candidates.Clear();
        if (count > 0)
        {
            foreach (var m in searcher.Take(MaxResultRows))
                Candidates.Add(new ScanResultViewModel(m.Address, m.Value));
        }
        MatchCount = count < 0
            ? "baseline captured — walk and narrow"
            : count > MaxResultRows ? $"{count:N0} matches (showing first {MaxResultRows:N0})"
            : $"{count:N0} match{(count == 1 ? "" : "es")}";
        OnPropertyChanged(nameof(HasCandidates));
        SelectedCandidate = Candidates.FirstOrDefault();
    }

    private void ResetScan()
    {
        _scanCts?.Cancel();
        _searcher?.Reset();
        Candidates.Clear();
        SelectedCandidate = null;
        MatchCount = "";
        OnPropertyChanged(nameof(HasCandidates));
        RaiseCommands();
        Status = "Scan reset. Click Snapshot Position to begin again.";
    }

    // --- pinning ------------------------------------------------------------
    private void PinAsCol()
    {
        if (SelectedCandidate is { } c)
        {
            _colAddress = c.Address;
            OnPropertyChanged(nameof(HasCol));
            OnPropertyChanged(nameof(ColAddressHex));
            RaiseCommands();
            Status = $"Pinned 0x{(ulong)c.Address:X} as char_col. Walk E/W to verify LiveX tracks it.";
        }
    }

    private void PinAsRow()
    {
        if (SelectedCandidate is { } c)
        {
            _rowAddress = c.Address;
            OnPropertyChanged(nameof(HasRow));
            OnPropertyChanged(nameof(RowAddressHex));
            RaiseCommands();
            Status = $"Pinned 0x{(ulong)c.Address:X} as char_row. Walk N/S to verify LiveY tracks it.";
        }
    }

    // --- teleport -----------------------------------------------------------
    private void Teleport()
    {
        if (_mem is not { IsOpen: true }) { Status = "Attach on the Character tab first."; return; }
        if (_colAddress is not { } colAddr || _rowAddress is not { } rowAddr)
        {
            Status = "Pin both char_col and char_row before teleporting.";
            return;
        }
        if (colAddr == rowAddr)
        {
            Status = "Column and row must be pinned to different addresses. Reset and scan each axis separately.";
            return;
        }

        // Read the current values so we can roll back the first write if the second fails.
        if (!ReadAt(colAddr, ScanWidth.Int16, out long prevX) || !ReadAt(rowAddr, ScanWidth.Int16, out long prevY))
        {
            Status = "Could not read both coordinate globals. The addresses may have gone stale — re-snapshot.";
            return;
        }

        if (!WriteAt(colAddr, TargetX, ScanWidth.Int16))
        {
            Status = "Could not write the column coordinate. The address may have gone stale — re-snapshot.";
            return;
        }

        if (!WriteAt(rowAddr, TargetY, ScanWidth.Int16))
        {
            // Roll back the column write so we don't leave the game at a mixed position. If the
            // rollback itself fails the user needs to know the position may be inconsistent.
            bool restored = WriteAt(colAddr, prevX, ScanWidth.Int16);
            Status = restored
                ? "Could not write the row coordinate; the column was restored. Re-snapshot before teleporting."
                : "Could not write the row coordinate, and the column rollback also failed. Re-attach and re-snapshot — position may be inconsistent.";
            return;
        }

        Status = $"Teleported to col {TargetX}, row {TargetY}. The game redraws the player next frame.";
    }

    // --- poll loop (refresh live X/Y from pinned addresses) -----------------
    private void PollTick()
    {
        if (_mem is not { IsOpen: true }) { OnDetached(); Status = "Target process exited."; return; }

        if (_colAddress is { } colAddr && ReadAt(colAddr, ScanWidth.Int16, out long cx))
            LiveX = (int)cx;
        if (_rowAddress is { } rowAddr && ReadAt(rowAddr, ScanWidth.Int16, out long ry))
            LiveY = (int)ry;

        // Live-refresh the candidates grid if it's small (helps the user watch the values move).
        if (_searcher != null && !IsScanning && Candidates.Count > 0 && Candidates.Count <= LiveRefreshThreshold)
        {
            foreach (var c in Candidates)
                if (_searcher.ReadValue(c.Address, out long live)) c.RefreshLive(live);
        }
    }

    // --- IScanHost (used only if we expose freeze on teleport pins later) ----
    bool IScanHost.Write(nuint address, long value, ScanWidth width) => ScanIo.WriteAt(_mem, address, value, width);
    bool IScanHost.Read(nuint address, ScanWidth width, out long value) => ScanIo.ReadAt(_mem, address, width, out value);
    void IScanHost.ReportWriteFailure(nuint address) =>
        Status = $"Write failed at 0x{(ulong)address:X}.";

    private bool ReadAt(nuint address, ScanWidth width, out long value) => ScanIo.ReadAt(_mem, address, width, out value);
    private bool WriteAt(nuint address, long value, ScanWidth width) => ScanIo.WriteAt(_mem, address, value, width);

    private void RaiseCommands()
    {
        foreach (var cmd in new ICommand[]
        {
            SnapshotCommand, WalkedNorthCommand, WalkedSouthCommand, WalkedEastCommand,
            WalkedWestCommand, StayedCommand, ResetScanCommand, PinAsColCommand,
            PinAsRowCommand, TeleportCommand,
        })
            (cmd as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
    }
}
