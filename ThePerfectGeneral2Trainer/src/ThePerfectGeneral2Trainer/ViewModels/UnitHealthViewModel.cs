using System.Windows.Input;
using ThePerfectGeneral2Trainer.Game;

namespace ThePerfectGeneral2Trainer.ViewModels;

/// <summary>
/// A pinned unit hit-points value on the Unit Health tab. The user scans for a unit's HP with the
/// value scanner, pins the surviving address here, tags it with the unit type (so the trainer knows
/// the max HP), and marks whether it is a player unit. When frozen, the target is re-written every
/// poll tick so combat can't drain it. The <see cref="SetToMaxCommand"/> pokes the unit type's max HP
/// in one click; <see cref="IsPlayer"/> gates the "Freeze all player" batch command so enemy units
/// are never accidentally frozen.
/// </summary>
public sealed class UnitHealthViewModel : ObservableObject
{
    private readonly IScanHost _host;
    private readonly ScanWidth _width;

    /// <summary>Absolute address of the HP value in the attached process.</summary>
    public nuint Address { get; }

    /// <summary>Width this pin was captured at (Byte for TPG2 unit HP, which never exceeds 21).</summary>
    public ScanWidth Width => _width;

    public string AddressHex => $"0x{(ulong)Address:X}";
    public string WidthLabel => _width.ToString();

    /// <summary>Unit types the user can tag this pin with (from <see cref="UnitReference.Units"/>).</summary>
    public IReadOnlyList<UnitInfo> UnitTypes => UnitReference.Units;

    private UnitInfo? _selectedUnit;
    /// <summary>
    /// The unit type the user assigned to this pin. When set, <see cref="MaxHp"/> reflects the
    /// unit's maximum hit points from <see cref="UnitReference"/> (Confirmed from UNITINFO.DOC).
    /// Null for untagged pins or unit types with no HP (mines/fortifications).
    /// </summary>
    public UnitInfo? SelectedUnit
    {
        get => _selectedUnit;
        set
        {
            if (SetField(ref _selectedUnit, value))
            {
                OnPropertyChanged(nameof(MaxHp));
                OnPropertyChanged(nameof(MaxHpDisplay));
                OnPropertyChanged(nameof(UnitName));
                (SetToMaxCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>The unit type's max HP, or null if no type is selected or the type has no HP.</summary>
    public int? MaxHp => _selectedUnit?.HitPoints;

    /// <summary>Display string for the Max HP column ("—" when unknown).</summary>
    public string MaxHpDisplay => MaxHp?.ToString() ?? "—";

    /// <summary>Display string for the unit name column ("(untagged)" when no type selected).</summary>
    public string UnitName => _selectedUnit?.Name ?? "(untagged)";

    private long _live;
    /// <summary>Most recent HP value read from RAM (display only).</summary>
    public long Live { get => _live; private set => SetField(ref _live, value); }

    private long _target;
    /// <summary>
    /// The HP value to write. Editing it pokes RAM once immediately; a value that doesn't fit the
    /// width is rejected and the box snaps back.
    /// </summary>
    public long Target
    {
        get => _target;
        set
        {
            if (!ScanValue.FitsWidth(value, _width))
            {
                OnPropertyChanged(nameof(Target));
                return;
            }
            if (!SetField(ref _target, value)) return;
            if (!_host.Write(Address, value, _width)) _host.ReportWriteFailure(Address);
        }
    }

    private bool _frozen;
    public bool Frozen { get => _frozen; set => SetField(ref _frozen, value); }

    private bool _isPlayer = true;
    /// <summary>
    /// Whether this pin represents a player unit. Default true — the user un-ticks it for enemy
    /// units. Gates the "Freeze all player" batch command so enemy HP is never accidentally held.
    /// </summary>
    public bool IsPlayer { get => _isPlayer; set => SetField(ref _isPlayer, value); }

    /// <summary>Sets <see cref="Target"/> to the unit type's max HP, if known.</summary>
    public ICommand SetToMaxCommand { get; }

    public UnitHealthViewModel(IScanHost host, nuint address, ScanWidth width, long current)
    {
        _host = host;
        Address = address;
        _width = width;
        _live = current;
        _target = current;
        SetToMaxCommand = new RelayCommand(_ => SetToMax(), _ => MaxHp.HasValue);
    }

    private void SetToMax()
    {
        if (MaxHp.HasValue) Target = MaxHp.Value;
    }

    /// <summary>Re-writes the target if frozen. Called from the poll loop.</summary>
    public void ApplyFreeze()
    {
        if (_frozen) _host.Write(Address, _target, _width);
    }

    /// <summary>Updates the live column from a fresh read without disturbing the target.</summary>
    public void RefreshLive(long value) => Live = value;
}
