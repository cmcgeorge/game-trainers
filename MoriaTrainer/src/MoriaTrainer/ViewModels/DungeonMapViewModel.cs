using System.Windows.Input;
using MoriaTrainer.Game;

namespace MoriaTrainer.ViewModels;

/// <summary>
/// Backs the Map Reveal tab. Locates the <c>cave[66][198]</c> grid in DOSBox guest RAM by scanning
/// for the row-0 outer-wall signature (the first <see cref="PlayerFormat.CaveCols"/> cells all have
/// <c>fval == FvalPermWall</c>), then writes <c>fm = 1</c> (field-mark) into every cell to reveal
/// the entire current dungeon level on the in-game map (<c>m</c> command).
///
/// <para>
/// The <c>cave_type</c> struct is 4 bytes (confirmed: <see cref="PlayerFormat.CaveCellSize"/>), with
/// sub-field offsets in <see cref="PlayerFormat.CellFvalOff"/>..<see cref="PlayerFormat.CellTlOff"/>.
/// The outer boundary (row 0, row 65, col 0, col 197) is guaranteed to be <c>FvalPermWall</c> (4),
/// giving a selective 198-cell signature for the scan.
/// </para>
///
/// <para>
/// Only the <c>fm</c> byte (offset +2) is written per cell — <c>fval</c>, <c>lr</c>, and <c>tl</c>
/// are untouched so the game's terrain, lighting, and monster logic are unaffected. The writes are
/// one byte per cell rather than a block re-write to avoid overwriting concurrent game-state changes.
/// </para>
///
/// <para>
/// If the dungeon level changes (staircase / recall) the cave array is re-generated in the same
/// memory region; Reset and Locate Cave again after descending.
/// </para>
/// </summary>
public sealed class DungeonMapViewModel : ObservableObject, IDisposable
{
    private ProcessMemory? _mem;
    private CancellationTokenSource? _cts;

    /// <summary>Per-region read cap mirrors <c>MemorySearcher.MaxRegionBytes</c> (256 MB).</summary>
    private const long MaxRegionBytes = 256L * 1024 * 1024;

    // --- state --------------------------------------------------------------
    public bool IsAttached => _mem is { IsOpen: true };

    private bool _isSearching;
    public bool IsSearching { get => _isSearching; private set { if (SetField(ref _isSearching, value)) RaiseCommands(); } }

    private bool _isLocated;
    public bool IsLocated { get => _isLocated; private set { if (SetField(ref _isLocated, value)) RaiseCommands(); } }

    private nuint? _caveBase;

    private string _status =
        "Attach on the Character tab, then click Locate Cave to find the dungeon grid in memory.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    private string _caveAddressHex = "(not found)";
    public string CaveAddressHex { get => _caveAddressHex; private set => SetField(ref _caveAddressHex, value); }

    // --- commands -----------------------------------------------------------
    public ICommand LocateCaveCommand { get; }
    public ICommand RevealMapCommand { get; }
    public ICommand ResetCommand { get; }

    public DungeonMapViewModel()
    {
        LocateCaveCommand = new RelayCommand(_ => _ = LocateCaveAsync(), _ => IsAttached && !IsSearching && !IsLocated);
        RevealMapCommand  = new RelayCommand(_ => _ = RevealMapAsync(),  _ => IsAttached && IsLocated && !IsSearching);
        ResetCommand      = new RelayCommand(_ => Reset(), _ => IsLocated);
    }

    // --- attach/detach ------------------------------------------------------
    public void OnAttached(ProcessMemory mem)
    {
        _mem = mem;
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Status = "Attached. Make sure you are inside a dungeon level (not the town), then click Locate Cave.";
    }

    public void OnDetached()
    {
        _cts?.Cancel();
        _mem = null;
        _caveBase = null;
        IsLocated = false;
        IsSearching = false;
        CaveAddressHex = "(not found)";
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Status = "Detached.";
    }

    // --- locate cave[] ------------------------------------------------------

    /// <summary>
    /// Scans memory for a <see cref="PlayerFormat.CaveBytes"/>-byte region whose first row is all
    /// <c>FvalPermWall</c> (4) — the permanent wall that borders the dungeon grid.
    /// </summary>
    private async Task LocateCaveAsync()
    {
        if (_mem is not { IsOpen: true } mem) return;

        IsSearching = true;
        Status = "Scanning for the cave[] outer wall signature… (may take a few seconds)";
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        nuint? found = null;
        try
        {
            found = await Task.Run(() => FindCaveBase(mem, ct), ct);
        }
        catch (OperationCanceledException) { Status = "Cancelled."; return; }
        catch (Exception ex) { Status = "Scan error: " + ex.Message; return; }
        finally { IsSearching = false; }

        // Staleness guard: if the user detached and re-attached while the scan ran, discard.
        if (mem != _mem) return;

        if (found == null)
        {
            Status = "cave[] not found. Make sure you are in a dungeon level (not the town) and the level is " +
                     "fully loaded. Walk around so the game draws the initial area, then try again.";
            return;
        }

        _caveBase = found;
        IsLocated = true;
        CaveAddressHex = $"0x{(ulong)found.Value:X}";
        Status = $"cave[] located at {CaveAddressHex} ({PlayerFormat.CaveBytes:N0} bytes). " +
                 "Click Reveal Map to set fm=1 on all cells (press 'm' in-game to view the map).";
    }

    private static nuint? FindCaveBase(ProcessMemory mem, CancellationToken ct)
    {
        int caveBytes = PlayerFormat.CaveBytes;    // 52,272
        int cellSize  = PlayerFormat.CaveCellSize; // 4
        int cols      = PlayerFormat.CaveCols;     // 198
        int rows      = PlayerFormat.CaveRows;     // 66
        byte permWall = PlayerFormat.FvalPermWall; // 4
        int fvalOff   = PlayerFormat.CellFvalOff;  // 0

        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            if (region.Size < (nuint)caveBytes) continue;

            long want = (long)Math.Min(region.Size, (nuint)MaxRegionBytes);
            byte[] data = mem.Read(region.Base, (int)want);
            if (data.Length < caveBytes) continue;

            // Slide over the region byte-by-byte (no alignment assumption on the array base).
            for (int offset = 0; offset + caveBytes <= data.Length; offset++)
            {
                ct.ThrowIfCancellationRequested();

                // Fast pre-filter: first fval byte must be permWall.
                if (data[offset + fvalOff] != permWall) continue;

                // Check all 198 row-0 fval bytes == permWall.
                bool rowOk = true;
                for (int c = 0; c < cols; c++)
                {
                    if (data[offset + c * cellSize + fvalOff] != permWall) { rowOk = false; break; }
                }
                if (!rowOk) continue;

                // Check last row first cell also == permWall.
                int lastRowOff = offset + (rows - 1) * cols * cellSize;
                if (lastRowOff + cellSize > data.Length) continue;
                if (data[lastRowOff + fvalOff] != permWall) continue;

                // Middle row interior: all fval bytes in 1..14 (valid terrain).
                int midOff = offset + (rows / 2) * cols * cellSize;
                bool midOk = true;
                for (int c = 1; c < cols - 1 && midOk; c++)
                {
                    byte fv = data[midOff + c * cellSize + fvalOff];
                    if (fv < 1 || fv > 14) midOk = false;
                }
                if (!midOk) continue;

                return region.Base + (nuint)offset;
            }
        }
        return null;
    }

    // --- reveal map ---------------------------------------------------------
    private async Task RevealMapAsync()
    {
        if (_mem is not { IsOpen: true } mem || _caveBase is not { } caveBase) return;

        IsSearching = true;
        Status = "Revealing map (writing fm=1 to each cell)…";
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        int written = 0;
        try
        {
            written = await Task.Run(() => RevealAll(mem, caveBase, ct), ct);
        }
        catch (OperationCanceledException) { Status = "Cancelled."; return; }
        catch (Exception ex) { Status = "Write error: " + ex.Message; return; }
        finally { IsSearching = false; }

        if (mem != _mem) return;

        Status = $"Map revealed ({written:N0} cells marked). Press 'm' in-game to see the full level. " +
                 "After descending a staircase, Reset and Locate Cave again.";
    }

    private static int RevealAll(ProcessMemory mem, nuint caveBase, CancellationToken ct)
    {
        int cellSize = PlayerFormat.CaveCellSize;
        int rows     = PlayerFormat.CaveRows;
        int cols     = PlayerFormat.CaveCols;
        int fmOff    = PlayerFormat.CellFmOff;
        int written  = 0;

        // Write only the fm byte for each cell using a targeted single-byte write. This avoids the
        // read-modify-write-block race where a bulk write would overwrite concurrent game changes to
        // fval/lr/tl (monster moves, lighting changes, terrain updates).
        var fmBuf = new byte[] { 1 };
        for (int r = 0; r < rows; r++)
        {
            ct.ThrowIfCancellationRequested();
            for (int c = 0; c < cols; c++)
            {
                nuint fmAddr = caveBase + (nuint)((r * cols + c) * cellSize + fmOff);
                if (mem.Write(fmAddr, fmBuf)) written++;
            }
        }
        return written;
    }

    // --- reset --------------------------------------------------------------
    private void Reset()
    {
        _caveBase = null;
        IsLocated = false;
        CaveAddressHex = "(not found)";
        Status = "Reset. Move to a new dungeon level if needed, then Locate Cave again.";
    }

    private void RaiseCommands()
    {
        (LocateCaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RevealMapCommand  as RelayCommand)?.RaiseCanExecuteChanged();
        (ResetCommand      as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
