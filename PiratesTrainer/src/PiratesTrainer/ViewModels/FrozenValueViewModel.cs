namespace PiratesTrainer.ViewModels;

/// <summary>
/// A pinned address the user wants to control: it shows the live value, holds a user-set
/// <see cref="Target"/> that is poked into RAM on edit, and — when <see cref="Frozen"/> — is
/// re-written every poll tick so the game can't move it back. Freezing matters in Pirates! because the
/// game keeps rewriting these fields: the monthly tick advances the calendar and pays estate income,
/// food is eaten every day at sea, and crew desert — so a one-shot poke is eaten unless it is held.
/// Writes follow the read-validate-write pattern via the host; a value that doesn't fit the pin's width
/// is rejected before it can corrupt neighbouring bytes.
/// </summary>
public sealed class FrozenValueViewModel : ObservableObject
{
    private readonly IScanHost _host;

    /// <summary>Absolute address of the pinned value.</summary>
    public nuint Address { get; }

    /// <summary>Width this pin was captured at (a pin outlives the searcher, so it carries its own width).</summary>
    public ScanWidth Width { get; }

    /// <summary>Optional human label for the pin (e.g. "Gold"), shown in the freeze grid.</summary>
    public string Label { get; }

    /// <summary>
    /// Whether the game reads this field as a signed value, so the Live/Target columns should show the
    /// signed form. Pirates!'s own player fields are unsigned (gold saturates at 0xFFFF rather than
    /// wrapping negative), so the auto-located pins all leave this false; it exists for values a user
    /// finds by scanning that turn out to be signed.
    /// </summary>
    public bool Signed { get; }

    public string AddressHex => $"0x{(ulong)Address:X}";
    public string WidthLabel => Width.ToString();

    private long _live;
    /// <summary>Most recent value read from RAM (display only).</summary>
    public long Live { get => _live; private set => SetField(ref _live, value); }

    private long _target;
    /// <summary>
    /// The value to write. Editing it pokes RAM once immediately; if the value doesn't fit the pin's
    /// width the edit is rejected and the box snaps back.
    /// </summary>
    public long Target
    {
        get => _target;
        set
        {
            if (!FitsPin(value))
            {
                OnPropertyChanged(nameof(Target));   // reject: revert the box
                return;
            }
            if (!SetField(ref _target, value)) return;
            if (!_host.Write(Address, value, Width)) _host.ReportWriteFailure(Address);
        }
    }

    /// <summary>
    /// Whether <paramref name="value"/> is a legal Target for this pin. Unsigned pins accept the full
    /// byte range for their width; a <see cref="Signed"/> pin is restricted to the signed range, so a
    /// Target above 32767 on a signed 16-bit pin (which the game would read back as a negative number) is
    /// rejected before it can surprise the user, rather than silently wrapping.
    /// </summary>
    private bool FitsPin(long value)
    {
        if (!Signed) return ScanValue.FitsWidth(value, Width);
        return Width switch
        {
            ScanWidth.Byte  => value is >= sbyte.MinValue and <= sbyte.MaxValue,
            ScanWidth.Int16 => value is >= short.MinValue and <= short.MaxValue,
            _               => value is >= int.MinValue and <= int.MaxValue,
        };
    }

    private bool _frozen;
    public bool Frozen { get => _frozen; set => SetField(ref _frozen, value); }

    public FrozenValueViewModel(IScanHost host, nuint address, ScanWidth width, long current,
        string label = "", bool signed = false)
    {
        _host = host;
        Address = address;
        Width = width;
        Label = label;
        Signed = signed;
        _live = Adjust(current);
        _target = Adjust(current);
    }

    /// <summary>Re-writes the target if frozen. Called from the poll loop.</summary>
    public void ApplyFreeze()
    {
        if (_frozen && !_host.Write(Address, _target, Width)) _host.ReportWriteFailure(Address);
    }

    /// <summary>Updates the live column from a fresh read without disturbing the target.</summary>
    public void RefreshLive(long value) => Live = Adjust(value);

    /// <summary>Reinterprets an unsigned little-endian read as signed when the pin is a signed field.</summary>
    private long Adjust(long raw)
    {
        if (!Signed) return raw;
        int bits = (int)Width * 8;
        long signBit = 1L << (bits - 1);
        return (raw ^ signBit) - signBit;   // sign-extend a `bits`-wide value
    }
}
