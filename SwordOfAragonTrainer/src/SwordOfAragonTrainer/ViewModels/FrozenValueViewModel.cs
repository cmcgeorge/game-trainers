using SwordOfAragonTrainer.Game;

namespace SwordOfAragonTrainer.ViewModels;

/// <summary>
/// A pinned address the user wants to control: it shows the live value, holds a user-set
/// <see cref="Target"/> that is poked into RAM on edit, and — when <see cref="Frozen"/> — is re-written
/// every poll tick so the game cannot move it back.
///
/// A pin carries its own width and encoding, because it outlives the scan that produced it. An MBF pin
/// is always four bytes and is written through the format converter, so a gold value the user types as
/// <c>250000</c> lands as the QuickBASIC single the game expects rather than as an integer the game
/// would read as a nonsense float.
/// </summary>
public sealed class FrozenValueViewModel : ObservableObject
{
    private readonly IScanHost _host;

    /// <summary>Absolute address of the pinned value.</summary>
    public nuint Address { get; }

    /// <summary>Width this pin was captured at.</summary>
    public ScanWidth Width { get; }

    /// <summary>How the bytes are encoded.</summary>
    public PinKind Kind { get; }

    /// <summary>Optional human label, shown in the grid.</summary>
    public string Label { get; }

    /// <summary>Guest <c>DS:offset</c> if the pin came from a segment scan.</summary>
    public int? DsOffset { get; }

    public string AddressHex => $"0x{(ulong)Address:X}";

    public string DsOffsetHex => DsOffset.HasValue ? $"DS:{DsOffset.Value:X4}" : "";

    public string KindLabel => Kind == PinKind.MbfSingle ? "MBF float" : Width.ToString();

    private double _live;
    /// <summary>Most recent value read from RAM (display only).</summary>
    public double Live { get => _live; private set { if (SetField(ref _live, value)) OnPropertyChanged(nameof(LiveText)); } }

    public string LiveText => Format(Live);

    private double _target;
    /// <summary>
    /// The value to write. Editing it pokes RAM once immediately; a value that will not fit the pin's
    /// width is rejected and the box snaps back rather than corrupting neighbouring bytes.
    /// </summary>
    public double Target
    {
        get => _target;
        set
        {
            if (Kind == PinKind.Raw)
            {
                long rounded = (long)Math.Round(value, MidpointRounding.AwayFromZero);
                if (!ScanValue.FitsWidth(rounded, Width))
                {
                    OnPropertyChanged(nameof(Target));      // reject: revert the box
                    return;
                }
                SetField(ref _target, rounded);
                if (!_host.Write(Address, rounded, Width)) _host.ReportWriteFailure(Address);
                return;
            }

            // double.IsFinite first: NaN compares false against everything, so an Math.Abs test alone
            // would let it through and Mbf.GetBytes(NaN) writes MBF zero — silently emptying the
            // treasury instead of refusing the edit, and re-writing zero every tick once frozen.
            if (!double.IsFinite(value) || Math.Abs(value) > Mbf.MaxMagnitude)
            {
                OnPropertyChanged(nameof(Target));
                return;
            }
            SetField(ref _target, value);
            if (!_host.WriteBytes(Address, Mbf.GetBytes(value))) _host.ReportWriteFailure(Address);
        }
    }

    private bool _frozen;
    public bool Frozen { get => _frozen; set => SetField(ref _frozen, value); }

    public FrozenValueViewModel(IScanHost host, nuint address, ScanWidth width, double current,
                                PinKind kind = PinKind.Raw, string label = "", int? dsOffset = null)
    {
        _host = host;
        Address = address;
        Width = kind == PinKind.MbfSingle ? ScanWidth.Int32 : width;
        Kind = kind;
        Label = label;
        DsOffset = dsOffset;
        _live = current;
        // Defensive: the Target setter refuses a value that would not fit the pin's width, so the
        // initial target must respect the same rule or ApplyFreeze could write a truncated value the
        // user never chose. A value read at this width always fits; a caller that got it wrong is
        // clamped here rather than silently poking the wrong bytes.
        _target = kind == PinKind.MbfSingle || ScanValue.FitsWidth((long)current, Width)
            ? current
            : ScanValue.Canonicalize((long)current, Width);
    }

    /// <summary>Re-writes the target if frozen. Called from the poll loop.</summary>
    public void ApplyFreeze()
    {
        if (!_frozen) return;
        bool ok = Kind == PinKind.MbfSingle
            ? _host.WriteBytes(Address, Mbf.GetBytes(_target))
            : _host.Write(Address, (long)Math.Round(_target, MidpointRounding.AwayFromZero), Width);
        if (!ok) _host.ReportWriteFailure(Address);
    }

    /// <summary>Updates the live column from a fresh read without disturbing the target.</summary>
    public void RefreshLive(double value) => Live = value;

    private string Format(double value) =>
        Kind == PinKind.MbfSingle ? value.ToString("0.####") : ((long)value).ToString();
}
