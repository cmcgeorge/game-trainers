namespace BattleTech1Trainer.ViewModels;

/// <summary>
/// One surviving candidate from a value scan: an address and the value last read there. Read-only in the
/// grid — the user narrows the candidate set with follow-up scans, then pins a survivor to the freeze
/// table to edit it.
/// </summary>
public sealed class ScanResultViewModel : ObservableObject
{
    /// <summary>Absolute address of this candidate in the attached process.</summary>
    public nuint Address { get; }

    private long _value;
    public long Value { get => _value; private set => SetField(ref _value, value); }

    public string AddressHex => $"0x{(ulong)Address:X}";

    public ScanResultViewModel(nuint address, long value)
    {
        Address = address;
        _value = value;
    }

    /// <summary>Updates the displayed value from a fresh read (poll loop).</summary>
    public void RefreshLive(long value) => Value = value;
}
