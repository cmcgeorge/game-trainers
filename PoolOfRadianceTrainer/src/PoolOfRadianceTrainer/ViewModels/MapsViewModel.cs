using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using PoolOfRadianceTrainer.Game;
using PoolOfRadianceTrainer.Memory;
using PoolOfRadianceTrainer.Mvvm;

namespace PoolOfRadianceTrainer.ViewModels;

/// <summary>
/// The 🗺 Maps tab: an offline area/location reference plus a manual teleport helper. The party's
/// map X/Y is NOT in the character record and its address changes every session, so the workflow is:
/// find the X (and Y) address once with the 🔍 Memory scanner, paste them here, pick a location (or
/// type target X/Y), and Teleport pokes the coordinates. Do it while exploring, never mid-combat.
/// </summary>
public sealed class MapsViewModel : ObservableObject
{
    private ProcessMemory? _mem;

    public IReadOnlyList<MapArea> Areas => MapBook.Areas;
    public ObservableCollection<MapLocation> Locations { get; } = new();

    public MapsViewModel()
    {
        TeleportCommand = new RelayCommand(_ => Teleport(), _ => CanTeleport());
        SelectedArea = Areas.FirstOrDefault();
    }

    private MapArea? _selectedArea;
    public MapArea? SelectedArea
    {
        get => _selectedArea;
        set
        {
            if (!SetProperty(ref _selectedArea, value)) return;
            Locations.Clear();
            if (value != null) foreach (var l in value.Locations) Locations.Add(l);
            SelectedLocation = Locations.FirstOrDefault();
        }
    }

    private MapLocation? _selectedLocation;
    public MapLocation? SelectedLocation
    {
        get => _selectedLocation;
        set
        {
            if (!SetProperty(ref _selectedLocation, value)) return;
            if (value != null) { TargetX = value.X; TargetY = value.Y; }
        }
    }

    private string _xAddress = "";
    public string XAddress { get => _xAddress; set { if (SetProperty(ref _xAddress, value)) RaiseCanTeleport(); } }

    private string _yAddress = "";
    public string YAddress { get => _yAddress; set { if (SetProperty(ref _yAddress, value)) RaiseCanTeleport(); } }

    private int _targetX;
    public int TargetX { get => _targetX; set => SetProperty(ref _targetX, Math.Clamp(value, 0, 255)); }

    private int _targetY;
    public int TargetY { get => _targetY; set => SetProperty(ref _targetY, Math.Clamp(value, 0, 255)); }

    private string _status =
        "Reference only until attached. To teleport: scan for the party's X (and Y) on the 🔍 Memory tab, paste the addresses here, pick a location, and Teleport (while exploring — never in combat).";
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    public ICommand TeleportCommand { get; }

    public void Attach(ProcessMemory mem)
    {
        _mem = mem;
        Status = "Attached. Scan for the party X/Y on the 🔍 Memory tab, paste the addresses, then Teleport.";
        RaiseCanTeleport();
    }

    public void Detach()
    {
        _mem = null;
        Status = "Detached — reference only. Re-attach and re-scan the X/Y addresses to teleport.";
        RaiseCanTeleport();
    }

    private bool CanTeleport() =>
        _mem is { IsOpen: true } && TryHex(XAddress, out _) && TryHex(YAddress, out _);

    private void Teleport()
    {
        if (_mem is not { IsOpen: true }) { Status = "Attach first (Memory tab)."; return; }
        if (!TryHex(XAddress, out var xa)) { Status = "X address must be hex (e.g. 0x1F1790000)."; return; }
        if (!TryHex(YAddress, out var ya)) { Status = "Y address must be hex."; return; }

        // Coordinates are small (grids are 16×16 / 18×20), so a single byte per axis is the safe
        // universal write — it can't spill into an adjacent field the way a 16-bit write could.
        bool okX = _mem.Write((nuint)xa, new[] { (byte)TargetX });
        bool okY = _mem.Write((nuint)ya, new[] { (byte)TargetY });
        if (okX && okY)
            Status = $"Teleported to ({TargetX}, {TargetY}). Take one step in-game to redraw the map.";
        else if (!okX && !okY)
            Status = "Teleport failed — neither write landed. Re-scan the X/Y addresses (they move when you change maps or restart DOSBox).";
        else
            // Report the partial state so the user knows one axis moved and which address to re-check.
            Status = $"Partial teleport — the {(okX ? "Y" : "X")} write failed, so only {(okX ? "X" : "Y")} moved. Re-check that address, then Teleport again.";
    }

    private static bool TryHex(string? s, out ulong value)
    {
        value = 0;
        s = s?.Trim();
        if (string.IsNullOrEmpty(s)) return false;
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return ulong.TryParse(s, NumberStyles.HexNumber, null, out value);
    }

    private void RaiseCanTeleport() => (TeleportCommand as RelayCommand)?.RaiseCanExecuteChanged();
}
