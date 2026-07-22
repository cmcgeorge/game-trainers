namespace ThePerfectGeneral2Trainer.ViewModels;

/// <summary>
/// One auto-located purchase value: a labelled row on the Purchase tab that shows the live value,
/// holds a user-set target that is poked into RAM on edit, and — when frozen — is re-written every
/// poll tick so spending can't move it back. Uses the same read-validate-write pattern as
/// <see cref="FrozenValueViewModel"/>: a target outside the width is rejected before it can corrupt
/// neighbouring bytes.
/// </summary>
public sealed class PurchaseItemViewModel : ObservableObject
{
    private readonly IScanHost _host;
    private readonly ScanWidth _width;

    /// <summary>Human-readable label (e.g. "Buy Points Remaining" or "Light Tank").</summary>
    public string Label { get; }

    /// <summary>Absolute address of the value in the attached process.</summary>
    public nuint Address { get; }

    /// <summary>Width this item was captured at (Byte for counts, Int16 for Buy Points).</summary>
    public ScanWidth Width => _width;

    public string AddressHex => $"0x{(ulong)Address:X}";
    public string WidthLabel => _width.ToString();

    private long _live;
    /// <summary>Most recent value read from RAM (display only).</summary>
    public long Live { get => _live; private set => SetField(ref _live, value); }

    private long _target;
    /// <summary>
    /// The value to write. Editing it pokes RAM once immediately; a value that doesn't fit the
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

    public PurchaseItemViewModel(IScanHost host, string label, nuint address, ScanWidth width, long current)
    {
        _host = host;
        Label = label;
        Address = address;
        _width = width;
        _live = current;
        _target = current;
    }

    /// <summary>Re-writes the target if frozen. Called from the poll loop.</summary>
    public void ApplyFreeze()
    {
        if (_frozen) _host.Write(Address, _target, _width);
    }

    /// <summary>Updates the live column from a fresh read without disturbing the target.</summary>
    public void RefreshLive(long value) => Live = value;
}
