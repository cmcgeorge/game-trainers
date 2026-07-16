using System.Collections.ObjectModel;
using System.Windows.Input;
using WastelandTrainer.Game;

namespace WastelandTrainer.ViewModels;

/// <summary>
/// Backs the Maps tab: an offline area/landmark reference plus a live "where am I / teleport me
/// there" helper. Unlike the Dragon Wars trainer — which has to hunt a moving global for the live
/// position — Wasteland keeps the party's X/Y and current map name in a 256-byte party-state header
/// that sits immediately before the roster (<see cref="CharacterFormat.PartyHeaderSize"/> bytes at
/// <c>rosterBase − 0x100</c>). Because the Party tab already locates the roster by structure, this
/// view-model just reads that header through a <see cref="nuint"/> supplied by the main view-model,
/// so the live position and teleport light up as soon as the party is found — no move-search needed.
///
/// Teleport writes only the two position bytes (X at <see cref="CharacterFormat.HeaderPartyX"/>, Y
/// at <see cref="CharacterFormat.HeaderPartyY"/>) on the current map. Do it while exploring, never
/// mid-combat.
/// </summary>
public sealed class MapsViewModel : ObservableObject
{
    private readonly Func<ProcessMemory?> _getMem;
    private readonly Func<nuint?> _getHeaderBase;

    private readonly byte[] _headerBuf = new byte[CharacterFormat.PartyHeaderSize];
    private int _staleReads;

    // The header address is stable for the whole session, so a failed read is almost always the game
    // rewriting it non-atomically as a new map loads. Ride out this many bad reads before dropping
    // the live position, so crossing between maps doesn't blank it.
    private const int MaxStaleReads = 5;

    /// <summary>Fixed schematic size. The confirmed desert overworld is 64×64; interiors are smaller,
    /// so a target near the edge may fall outside a small interior map — pick a square inside it.</summary>
    private const int GridSize = 64;

    public int GridWidth => GridSize;
    public int GridHeight => GridSize;

    public IReadOnlyList<MapArea> Areas => MapBook.Areas;
    public ObservableCollection<MapLandmark> Landmarks { get; } = new();

    public MapsViewModel(Func<ProcessMemory?> getMem, Func<nuint?> getHeaderBase)
    {
        _getMem = getMem;
        _getHeaderBase = getHeaderBase;
        TeleportCommand = new RelayCommand(_ => Teleport(), _ => CanTeleport());
        SelectedArea = Areas.FirstOrDefault();
    }

    private bool IsAttached => _getMem() is { IsOpen: true };

    // --- reference selection -------------------------------------------------
    private MapArea? _selectedArea;
    public MapArea? SelectedArea
    {
        get => _selectedArea;
        set
        {
            if (!SetField(ref _selectedArea, value)) return;
            Landmarks.Clear();
            if (value != null) foreach (var l in value.Landmarks) Landmarks.Add(l);
        }
    }

    // --- teleport target -----------------------------------------------------
    // Clamp to the grid, and when the clamp rewrites an out-of-range entry to the value already held
    // (e.g. typing 100 while the target is already 63), force a notification so the text box snaps back
    // to the clamped value instead of showing an input the model won't teleport to.
    private int _targetX;
    public int TargetX
    {
        get => _targetX;
        set { int v = Math.Clamp(value, 0, GridSize - 1); if (!SetField(ref _targetX, v) && v != value) OnPropertyChanged(); }
    }

    private int _targetY;
    public int TargetY
    {
        get => _targetY;
        set { int v = Math.Clamp(value, 0, GridSize - 1); if (!SetField(ref _targetY, v) && v != value) OnPropertyChanged(); }
    }

    // --- live position (drives the green dot on the schematic) ---------------
    private int _liveX;
    public int LiveX { get => _liveX; private set => SetField(ref _liveX, value); }

    private int _liveY;
    public int LiveY { get => _liveY; private set => SetField(ref _liveY, value); }

    private string _liveMapName = "";
    public string LiveMapName { get => _liveMapName; private set => SetField(ref _liveMapName, value); }

    private bool _hasParty;
    /// <summary>True once a readable live position has been found (enables the green dot and teleport).</summary>
    public bool HasParty
    {
        get => _hasParty;
        private set { if (SetField(ref _hasParty, value)) RaiseCommands(); }
    }

    private string _livePosition = "";
    /// <summary>"Ranger Ctr. — X 55 · Y 62" once the party is located; empty otherwise.</summary>
    public string LivePosition { get => _livePosition; private set => SetField(ref _livePosition, value); }

    private string _status =
        "Reference only until attached. Attach on the Party tab; the live map and position appear here automatically, then pick a target square and Teleport.";
    public string Status { get => _status; private set => SetField(ref _status, value); }

    public ICommand TeleportCommand { get; }

    // --- lifecycle (called by the main view-model) ---------------------------
    public void OnAttached() =>
        Status = "Attached. Once the party is found the live position shows here; click a square (or type a target) and Teleport.";

    public void OnDetached()
    {
        ClearLive();
        Status = "Detached. Attach on the Party tab to track and teleport the party again.";
    }

    private void ClearLive()
    {
        _staleReads = 0;
        LivePosition = "";
        LiveMapName = "";
        HasParty = false;
    }

    /// <summary>Poll-tick refresh: re-read the party-state header for the live map name and X/Y.</summary>
    public void Tick()
    {
        var mem = _getMem();
        var headerBase = _getHeaderBase();
        if (mem is not { IsOpen: true } || headerBase == null)
        {
            if (HasParty || LivePosition.Length > 0) ClearLive();
            return;
        }

        if (mem.Read(headerBase.Value, _headerBuf, CharacterFormat.PartyHeaderSize) != CharacterFormat.PartyHeaderSize)
        {
            if (++_staleReads < MaxStaleReads) return;
            ClearLive();
            return;
        }

        _staleReads = 0;
        LiveX = _headerBuf[CharacterFormat.HeaderPartyX];
        LiveY = _headerBuf[CharacterFormat.HeaderPartyY];
        LiveMapName = MapBook.MapName(_headerBuf);
        LivePosition = $"{LiveMapName} — X {LiveX} · Y {LiveY}";
        HasParty = true;
    }

    // --- teleport ------------------------------------------------------------
    private bool CanTeleport() => IsAttached && HasParty;

    private void Teleport()
    {
        var mem = _getMem();
        var headerBase = _getHeaderBase();
        if (mem is not { IsOpen: true } || headerBase == null)
        {
            Status = "Attach on the Party tab and let the party be located first.";
            return;
        }

        // Re-read the header to confirm the address is still live before writing.
        if (mem.Read(headerBase.Value, _headerBuf, CharacterFormat.PartyHeaderSize) != CharacterFormat.PartyHeaderSize)
        {
            Status = "The party position could not be read — Re-scan on the Party tab, then try again.";
            return;
        }

        // X and Y are one byte each and adjacent (0x08, 0x09). Stamp the target into the header
        // buffer, then write back just those two bytes so nothing else in the header is touched.
        _headerBuf[CharacterFormat.HeaderPartyX] = (byte)TargetX;
        _headerBuf[CharacterFormat.HeaderPartyY] = (byte)TargetY;
        bool ok = mem.WriteRange(headerBase.Value, _headerBuf, CharacterFormat.HeaderPartyX, 2);

        Status = ok
            ? $"Teleported to ({TargetX}, {TargetY}) on {LiveMapName}. Take one step in-game to redraw the map."
            : "Teleport write failed — Re-scan on the Party tab and try again.";
    }

    private void RaiseCommands() => (TeleportCommand as RelayCommand)?.RaiseCanExecuteChanged();
}
