using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LordsTrainer;

/// <summary>
/// A pinned address in the watch table: shows its live value, can be frozen
/// (continuously re-written) and can be set to a new value on demand.
/// </summary>
public sealed class WatchEntry : INotifyPropertyChanged
{
    public uint Address { get; init; }
    public ValueType Type { get; init; }

    /// <summary>Optional second address written alongside <see cref="Address"/> — used
    /// for the human lord, whose treasury has both an authoritative slot and a display
    /// cache that must be kept in sync.</summary>
    public uint? MirrorAddress { get; init; }

    private string _description = "";
    public string Description
    {
        get => _description;
        set { _description = value; OnChanged(); }
    }

    private long _value;
    public long Value
    {
        get => _value;
        set { _value = value; OnChanged(); OnChanged(nameof(DisplayValue)); }
    }

    // The value the user wants to hold when Freeze is on.
    private long _freezeValue;
    public long FreezeValue
    {
        get => _freezeValue;
        set { _freezeValue = value; OnChanged(); OnChanged(nameof(DisplayFreezeValue)); }
    }

    /// <summary>
    /// Multiplier from the stored memory units to the number shown in-game. 1 for a
    /// plain 1:1 value; the armoury weapons are stored in batches of 50, so they use
    /// <c>DisplayScale = 50</c>. <see cref="Value"/>/<see cref="FreezeValue"/> stay in
    /// raw memory units (what actually gets read/written); the Display* projections
    /// present and accept the in-game number so the user never has to divide by 50.
    /// </summary>
    public int DisplayScale { get; init; } = 1;

    /// <summary>Live value in the game's own units (raw × scale).</summary>
    public long DisplayValue => _value * DisplayScale;

    /// <summary>The "Set to" target in the game's own units; setting it stores the
    /// equivalent raw amount. A typed value that isn't a whole multiple of the scale is
    /// rounded to the NEAREST batch (e.g. at scale 50, 149 → 150) rather than truncated
    /// down, so the value the user sees back is the closest achievable one.</summary>
    public long DisplayFreezeValue
    {
        get => _freezeValue * DisplayScale;
        set => FreezeValue = DisplayScale <= 1 ? value : (value + DisplayScale / 2) / DisplayScale;
    }

    private bool _freeze;
    public bool Freeze
    {
        get => _freeze;
        // Arming freeze must NOT overwrite FreezeValue. That field is bound to the
        // "Set value" column the user types into; it is seeded to the live value when
        // the entry is created (see MainWindow.OnResultDoubleClick). From then on the
        // user owns it — so ticking Freeze holds whatever number they entered, not the
        // last-observed live value.
        set { _freeze = value; OnChanged(); }
    }

    public string AddressHex => "0x" + Address.ToString("X5");
    public string TypeLabel => Type == ValueType.Int32 ? "i32" : "i16";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
