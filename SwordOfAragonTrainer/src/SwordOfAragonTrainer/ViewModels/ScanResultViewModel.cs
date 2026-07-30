using SwordOfAragonTrainer.Game;

namespace SwordOfAragonTrainer.ViewModels;

/// <summary>
/// One surviving candidate from a scan: an address and the value last read there. Read-only in the
/// grid — the user narrows the set with follow-up scans, then pins a survivor to edit it.
///
/// A candidate found inside the located data segment also carries its guest <c>DS:offset</c>, which is
/// the genuinely informative number for a DOS target: the host address changes every DOSBox session
/// but the segment offset does not.
/// </summary>
public sealed class ScanResultViewModel : ObservableObject
{
    /// <summary>Absolute address in the attached process.</summary>
    public nuint Address { get; }

    /// <summary>
    /// The width this candidate was actually found at. A candidate outlives the scan that produced it
    /// and the Width combo box can move underneath it, so the row carries its own width — reading or
    /// writing a 16-bit counter as 32 bits would clobber the neighbouring variable.
    /// </summary>
    public ScanWidth Width { get; }

    /// <summary>How the bytes at <see cref="Address"/> are encoded.</summary>
    public PinKind Kind { get; }

    /// <summary>Guest <c>DS:offset</c> if this came from a segment scan, otherwise null.</summary>
    public int? DsOffset { get; }

    /// <summary>Optional label carried through to the pin ("Wealth", "Population", …).</summary>
    public string Label { get; }

    private double _value;
    public double Value { get => _value; private set => SetField(ref _value, value); }

    public string AddressHex => $"0x{(ulong)Address:X}";

    public string DsOffsetHex => DsOffset.HasValue ? $"DS:{DsOffset.Value:X4}" : "";

    public string ValueText => Kind == PinKind.MbfSingle
        ? Value.ToString("0.####")
        : ((long)Value).ToString();

    public ScanResultViewModel(nuint address, double value, ScanWidth width,
                               PinKind kind = PinKind.Raw, int? dsOffset = null, string label = "")
    {
        Address = address;
        Width = kind == PinKind.MbfSingle ? ScanWidth.Int32 : width;
        Kind = kind;
        DsOffset = dsOffset;
        Label = label;
        _value = value;
    }

    /// <summary>Updates the displayed value from a fresh read (poll loop).</summary>
    public void RefreshLive(double value)
    {
        Value = value;
        OnPropertyChanged(nameof(ValueText));
    }

    /// <summary>
    /// Decodes a raw little-endian word for display.
    ///
    /// 16-bit values are shown <b>signed</b>, because the game's own counters genuinely go negative —
    /// the city block's "changed this month" row carries values like -3 (see <c>docs/RE.md</c> §6.2) —
    /// and 65533 would be a useless way to render that. Writes are unaffected: a negative target folds
    /// back to the same two bytes through <see cref="ScanValue.Canonicalize"/>.
    /// </summary>
    public static double Decode(long raw, PinKind kind, ScanWidth width) => kind switch
    {
        PinKind.MbfSingle => Mbf.FromRaw(unchecked((uint)raw)),
        _ when width == ScanWidth.Int16 => (short)(ushort)raw,
        _ => raw,
    };
}
