using System.Collections.ObjectModel;
using WastelandTrainer.Game;

namespace WastelandTrainer.ViewModels;

/// <summary>
/// Backs the Maps tab: an offline area/landmark reference plus a live "where am I" readout. Wasteland
/// keeps the party's X/Y in a 256-byte party-state header immediately before the roster
/// (<see cref="CharacterFormat.PartyHeaderSize"/> bytes at <c>rosterBase − 0x100</c>). Because the Party
/// tab already locates the roster by structure, this view-model just reads that header through a
/// <see cref="nuint"/> supplied by the main view-model, so the live position lights up as soon as the
/// party is found — no move-search needed.
///
/// <para><b>Teleport is intentionally not offered.</b> The party-state header is a <i>write-only shadow</i>:
/// the game copies the party's position into it every step (which is exactly what makes this readout
/// track movement) but never reads it back to place the party. Live reverse-engineering with the
/// DOSBox-X debug server (see <c>.docs\Wasteland-Reverse-Engineering.md §5</c>) confirmed the on-map
/// position is virtualized — a world position, a scrolling viewport origin, a screen-cell offset and
/// several shadow copies, all rewritten per step with the map repainted incrementally — so <b>no single
/// memory write relocates the party</b>. Only the game's own map-load/placement code moves it, which a
/// host-memory trainer can't drive. This tab therefore <i>reads</i> the trusted live X/Y and offers the
/// manual Areas reference; it never writes position.</para>
///
/// Only the X/Y are surfaced: the header's 0xD0 map-name field is not a reliable current-map label (it
/// read "Ranger Ctr." with the party standing in Highpool), so the Areas list is the manual map reference.
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

    /// <summary>Fixed schematic size. The confirmed desert overworld is 64×64; interiors are smaller.</summary>
    private const int GridSize = 64;

    public int GridWidth => GridSize;
    public int GridHeight => GridSize;

    public IReadOnlyList<MapArea> Areas => MapBook.Areas;
    public ObservableCollection<MapLandmark> Landmarks { get; } = new();

    public MapsViewModel(Func<ProcessMemory?> getMem, Func<nuint?> getHeaderBase)
    {
        _getMem = getMem;
        _getHeaderBase = getHeaderBase;
        SelectedArea = Areas.FirstOrDefault();
    }

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

    // --- live position (drives the green dot on the schematic) ---------------
    private int _liveX;
    public int LiveX { get => _liveX; private set => SetField(ref _liveX, value); }

    private int _liveY;
    public int LiveY { get => _liveY; private set => SetField(ref _liveY, value); }

    private bool _hasParty;
    /// <summary>True once a readable live position has been found (shows the green dot).</summary>
    public bool HasParty { get => _hasParty; private set => SetField(ref _hasParty, value); }

    private string _livePosition = "";
    /// <summary>"X 55 · Y 62" once the party is located; empty otherwise. The header's map-name field
    /// (0x0D0) is not a reliable current-map label, so only the trusted X/Y are surfaced — see
    /// <c>.docs\Wasteland-Reverse-Engineering.md §2</c>.</summary>
    public string LivePosition { get => _livePosition; private set => SetField(ref _livePosition, value); }

    private string _status =
        "Reference only until attached. Attach on the Party tab and the live position appears here automatically.";
    public string Status { get => _status; private set => SetField(ref _status, value); }

    // --- lifecycle (called by the main view-model) ---------------------------
    public void OnAttached() =>
        Status = "Attached. Once the party is found the live position shows here; use the Areas list as a map reference.";

    public void OnDetached()
    {
        ClearLive();
        Status = "Detached. Attach on the Party tab to track the party again.";
    }

    private void ClearLive()
    {
        _staleReads = 0;
        LivePosition = "";
        HasParty = false;
    }

    /// <summary>Poll-tick refresh: re-read the party-state header for the live X/Y. The 0xD0 map-name
    /// field is not a reliable current-map label, so only the coordinates are surfaced.</summary>
    public void Tick()
    {
        var mem = _getMem();
        var headerBase = _getHeaderBase();
        if (mem is not { IsOpen: true } || headerBase == null)
        {
            if (HasParty || LivePosition.Length > 0) ClearLive();
            return;
        }

        // A failed read, or an implausible header (out-of-range X/Y — e.g. the header being rewritten as
        // a new map loads), is ridden out for a few ticks so a map crossing doesn't blank the position.
        if (mem.Read(headerBase.Value, _headerBuf, CharacterFormat.PartyHeaderSize) != CharacterFormat.PartyHeaderSize
            || !PartyHeader.IsPlausible(_headerBuf))
        {
            if (++_staleReads < MaxStaleReads) return;
            ClearLive();
            return;
        }

        _staleReads = 0;
        LiveX = _headerBuf[CharacterFormat.HeaderPartyX];
        LiveY = _headerBuf[CharacterFormat.HeaderPartyY];
        LivePosition = $"X {LiveX} · Y {LiveY}";
        HasParty = true;
    }
}
