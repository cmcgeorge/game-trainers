using System.Threading;
using System.Windows.Input;
using Civilization3ConquestsTrainer.Game;

namespace Civilization3ConquestsTrainer.ViewModels;

/// <summary>
/// The Map tab: what the locator recovered about the world, and the two map-wide actions the trainer
/// offers — revealing and hiding the terrain.
///
/// <para>Reveal is deliberately gated behind an explicit confirmation rather than being a one-click
/// "max" button like the others. The <c>Map</c> header itself is confirmed (its width, height and
/// seed were checked against <c>conquests.ini</c>'s <c>WorldSeed</c>), but the per-tile visibility
/// masks are <b>inferred</b> from the C3X struct header and have not been round-tripped through the
/// game's own display. Writing three bitmasks across several thousand tile objects on the strength of
/// an unconfirmed offset is exactly the kind of thing that scribbles over unrelated state, so the tab
/// says so instead of hiding it. (Three, not four: <c>Tile_Body.Fog_Of_War</c> is deliberately left
/// alone — see <see cref="Civ3Layout.TileFogOfWar"/>.)</para>
/// </summary>
public sealed class MapViewModel : ObservableObject
{
    private readonly IGameHost _host;
    private Civ3Location? _loc;
    private CancellationTokenSource? _sweepCts;

    private string _summary = "Not located.";
    public string Summary { get => _summary; private set => SetField(ref _summary, value); }

    private string _tilePointerNote = "";
    public string TilePointerNote { get => _tilePointerNote; private set => SetField(ref _tilePointerNote, value); }

    private bool _confirmedRisk;
    /// <summary>The user has acknowledged that the visibility offsets are unconfirmed.</summary>
    public bool ConfirmedRisk
    {
        get => _confirmedRisk;
        set
        {
            if (SetField(ref _confirmedRisk, value))
            {
                (RevealMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (HideMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _isRevealing;
    /// <summary>True while the sweep is running, so the button cannot be pressed twice.</summary>
    public bool IsRevealing
    {
        get => _isRevealing;
        private set
        {
            if (!SetField(ref _isRevealing, value)) return;
            OnPropertyChanged(nameof(CanSweep));
            (RevealMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (HideMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool CanSweep => _loc is { TileCount: > 0 } && ConfirmedRisk && !_isRevealing;

    public ICommand RevealMapCommand { get; }
    public ICommand HideMapCommand { get; }

    public MapViewModel(IGameHost host)
    {
        _host = host;
        RevealMapCommand = new RelayCommand(_ => RunSweep(true), _ => CanSweep);
        HideMapCommand = new RelayCommand(_ => RunSweep(false), _ => CanSweep);
    }

    /// <summary>Takes on a freshly located game.</summary>
    public void Adopt(Civ3Location loc, GameTables tables)
    {
        _loc = loc;
        if (loc.TileCount <= 0)
        {
            Summary = "The map header did not validate — no world is loaded, or the layout differs.";
            TilePointerNote = "";
        }
        else
        {
            Summary = $"{loc.MapWidth} × {loc.MapHeight}, {loc.TileCount:N0} tiles " +
                      $"(Civ3's staggered grid holds width × height ÷ 2). " +
                      $"{tables.Races.Count} civilizations and {tables.UnitTypes.Count} unit types in the loaded ruleset.";
            _host.ReadInt32(loc.Map + (nuint)Civ3Layout.MapTiles, out int tiles);
            TilePointerNote = $"Map struct at 0x{(ulong)loc.Map:X}, tile array at 0x{(uint)tiles:X8}.";
        }
        OnPropertyChanged(nameof(CanSweep));
        (RevealMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (HideMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    /// <summary>Forgets the located game, cancelling any in-flight sweep first.</summary>
    public void Clear()
    {
        CancelSweep();
        _loc = null;
        Summary = "Not located.";
        TilePointerNote = "";
        ConfirmedRisk = false;
        OnPropertyChanged(nameof(CanSweep));
        (RevealMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (HideMapCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Cancels an in-flight sweep. Called from <see cref="MainViewModel.Teardown"/> so a detach
    /// mid-sweep does not leave a background task writing through a host whose process has changed.
    /// </summary>
    public void CancelSweep() => _sweepCts?.Cancel();

    /// <summary>
    /// Sets or clears the human player's bit in each tile's visibility masks. Every tile is validated
    /// by its own 'TILE' tag before anything is written to it, so a wrong tile-array pointer stops the
    /// sweep rather than spraying writes across the heap.
    ///
    /// <para>Runs off the UI thread and reads the whole tile-pointer array in one go: a large map holds
    /// hundreds of thousands of tiles, and doing this inline would freeze the window for minutes.</para>
    ///
    /// <para>The sweep is session-guarded: it captures the <see cref="Civ3Location"/> reference at the
    /// start and checks <see cref="ReferenceEquals"/> against <c>_loc</c> before every tile's writes. If
    /// the session is torn down (detach) or a new session is adopted mid-sweep, the captured reference
    /// is stale and the sweep stops — its tile addresses belong to the old process, and writing them
    /// through the new host would corrupt the new game. A <see cref="CancellationToken"/> from
    /// <see cref="CancelSweep"/> provides the same guarantee for the teardown path.</para>
    /// </summary>
    private async void RunSweep(bool reveal)
    {
        if (_loc is not { } loc || !_host.WritesAllowed || _isRevealing) return;

        if (!_host.ReadInt32(loc.Map + (nuint)Civ3Layout.MapTiles, out int tileArray)
            || !Civ3Layout.LooksLikeHeapPointer((uint)tileArray))
        {
            _host.Report("The tile array pointer did not validate — nothing was written.");
            return;
        }

        _sweepCts = new CancellationTokenSource();
        CancellationToken ct = _sweepCts.Token;
        IsRevealing = true;
        _host.Report($"{(reveal ? "Revealing" : "Hiding")} {loc.TileCount:N0} tiles…");
        try
        {
            var result = await Task.Run(() => Sweep(loc, (nuint)tileArray, ct, reveal));
            _host.Report(result.Written == 0
                ? "No tile validated its 'TILE' tag — nothing was written. The tile layout does not match."
                : $"{(reveal ? "Set" : "Cleared")} the visibility bit on {result.Written:N0} tiles " +
                  $"({result.Skipped:N0} skipped). Scroll or end a turn to force a redraw. " +
                  "If nothing changed on screen, the visibility offsets are wrong for this build — " +
                  "they are marked Inferred for exactly this reason.");
        }
        catch (OperationCanceledException)
        {
            _host.Report("The sweep was cancelled — the session was torn down mid-sweep.");
        }
        catch (Exception ex)
        {
            _host.Report((reveal ? "Reveal" : "Hide") + " failed: " + ex.Message);
        }
        finally
        {
            _sweepCts?.Dispose();
            _sweepCts = null;
            IsRevealing = false;
        }
    }

    /// <summary>
    /// The tile-level sweep. <paramref name="reveal"/> sets the human civ's bit; clearing it hides
    /// the map back to fog of war. Internal so the test harness can drive it over a fake host and
    /// verify the session guard.
    /// </summary>
    internal (int Written, int Skipped) Sweep(Civ3Location loc, nuint tileArray,
                                               CancellationToken ct, bool reveal)
    {
        int mask = 1 << loc.HumanCivId;
        int written = 0, skipped = 0;

        // One bulk read of the pointer array instead of TileCount four-byte reads.
        byte[] pointers = _host.Read(tileArray, loc.TileCount * 4);
        if (pointers.Length < loc.TileCount * 4) return (0, loc.TileCount);

        for (int i = 0; i < loc.TileCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            // If the session was torn down or a new one adopted mid-sweep, the loc we captured is
            // stale — its tile addresses belong to the old process. Stop before writing.
            if (!ReferenceEquals(_loc, loc)) break;

            uint tilePtr = BitConverter.ToUInt32(pointers, i * 4);
            if (!Civ3Layout.LooksLikeHeapPointer(tilePtr)) { skipped++; continue; }

            nuint tile = tilePtr;
            byte[] tag = _host.Read(tile + (nuint)Civ3Layout.TileTagOffset, 4);
            if (tag.Length < 4 || BitConverter.ToUInt32(tag) != Civ3Layout.TagTile) { skipped++; continue; }

            foreach (int off in Civ3Layout.TileVisibilityMasks)
            {
                if (!_host.ReadInt32(tile + (nuint)off, out int current)) continue;
                _host.WriteInt32(tile + (nuint)off, reveal ? current | mask : current & ~mask);
            }
            written++;
        }
        return (written, skipped);
    }
}
