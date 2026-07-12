using WarOfTheLanceTrainer.Game;

namespace WarOfTheLanceTrainer.ViewModels;

/// <summary>
/// One editable current-strength cell of the located WL.DAT working buffer: a labelled unit whose
/// live strength can be edited, maxed, and frozen. Writes go straight through to guest RAM via the
/// host, following the read-validate-write pattern. Live values read from RAM keep their real
/// 0..240 range (0 = a destroyed unit), while a user-typed edit is clamped to 1..240 so a stray
/// keystroke can't zero a unit that is still on the map.
/// </summary>
public sealed class UnitStrengthViewModel : ObservableObject
{
    private readonly IStrengthHost _host;

    /// <summary>Absolute address of this cell in the attached process.</summary>
    public nuint Address { get; }

    public int Index { get; }
    public string Side { get; }
    public string Nation { get; }
    public string UnitType { get; }
    public byte BaseNumber { get; }

    public string Label => $"{Nation} — {UnitType}";

    private int _strength;

    /// <summary>
    /// Live strength, clamped to 1..240 on write. Typed as <see cref="int"/> (not <see cref="byte"/>)
    /// so the WPF text binding can hand us an over-range value like 300 and let <see cref="Clamp"/>
    /// pull it down to 240, instead of the converter rejecting it before the clamp ever runs.
    /// </summary>
    public int Strength
    {
        get => _strength;
        set
        {
            byte clamped = Clamp(value);
            if (clamped == _strength)
            {
                // The stored value didn't change, but the user's raw text (e.g. 300) might differ
                // from the clamped value; refresh so the box snaps back to what we actually hold.
                if (value != clamped) OnPropertyChanged(nameof(Strength));
                return;
            }
            if (_host.WriteStrength(Address, clamped))
            {
                _strength = clamped;
                OnPropertyChanged(nameof(Strength));
            }
            else
            {
                _host.ReportWriteFailure(Label);
                OnPropertyChanged(nameof(Strength));   // revert the box to the unchanged value
            }
        }
    }

    private bool _freeze;
    public bool Freeze
    {
        get => _freeze;
        set => SetField(ref _freeze, value);
    }

    public UnitStrengthViewModel(IStrengthHost host, StrengthEntry entry, nuint address, byte strength)
    {
        _host = host;
        Address = address;
        Index = entry.Index;
        Side = entry.Side;
        Nation = entry.Nation;
        UnitType = entry.UnitType;
        BaseNumber = entry.BaseNumber;
        _strength = NormalizeLive(strength);
    }

    /// <summary>Sets the cell to the engine's ceiling (240).</summary>
    public void Max() => Strength = GameFacts.MaxStrength;

    /// <summary>Restores the cell to its campaign-start base number.</summary>
    public void RestoreBase() => Strength = BaseNumber;

    /// <summary>Re-writes the frozen value if this cell is pinned. Called from the poll loop.</summary>
    public void ApplyFreeze()
    {
        if (_freeze) _host.WriteStrength(Address, (byte)_strength);
    }

    /// <summary>Updates the displayed value from a fresh RAM read without re-writing.</summary>
    public void RefreshLive(byte value)
    {
        if (_freeze) return;   // a frozen cell owns its value; don't let a stale read fight it
        SetField(ref _strength, NormalizeLive(value), nameof(Strength));
    }

    /// <summary>Clamps a user-typed edit to a valid live strength (1..240).</summary>
    private static byte Clamp(int value)
    {
        if (value < 1) return 1;
        if (value > GameFacts.MaxStrength) return GameFacts.MaxStrength;
        return (byte)value;
    }

    /// <summary>
    /// Normalizes a value observed in RAM to the real 0..240 range, preserving a legitimate 0 for a
    /// destroyed unit (only the engine ceiling is enforced) so freezing never resurrects it.
    /// </summary>
    private static byte NormalizeLive(int value)
    {
        if (value < 0) return 0;
        if (value > GameFacts.MaxStrength) return GameFacts.MaxStrength;
        return (byte)value;
    }
}
