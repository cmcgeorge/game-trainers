namespace DarklandsTrainer.ViewModels;

/// <summary>
/// A pinned address the user wants to control: it shows the live value, holds a user-set
/// <see cref="Target"/> that is poked into RAM on edit, and — when <see cref="Frozen"/> — is re-written
/// every poll tick so the game can't move it back. Writes follow the read-validate-write pattern via the
/// host; a value that doesn't fit the pin's width is rejected before it can corrupt neighbouring bytes.
/// </summary>
public sealed class FrozenValueViewModel : ObservableObject
{
    private readonly IScanHost _host;

    /// <summary>Absolute address of the pinned value.</summary>
    public nuint Address { get; }

    /// <summary>Width this pin was captured at (a pin outlives the searcher, so it carries its own width).</summary>
    public ScanWidth Width { get; }

    /// <summary>Optional human label for the pin (e.g. "Endurance"), shown in the freeze grid.</summary>
    public string Label { get; }

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
            if (!ScanValue.FitsWidth(value, Width))
            {
                OnPropertyChanged(nameof(Target));   // reject: revert the box
                return;
            }
            if (!SetField(ref _target, value)) return;
            if (!_host.Write(Address, value, Width)) _host.ReportWriteFailure(Address);
        }
    }

    private bool _frozen;
    public bool Frozen { get => _frozen; set => SetField(ref _frozen, value); }

    public FrozenValueViewModel(IScanHost host, nuint address, ScanWidth width, long current, string label = "")
    {
        _host = host;
        Address = address;
        Width = width;
        Label = label;
        _live = current;
        _target = current;
    }

    /// <summary>Re-writes the target if frozen. Called from the poll loop.</summary>
    public void ApplyFreeze()
    {
        if (_frozen && !_host.Write(Address, _target, Width)) _host.ReportWriteFailure(Address);
    }

    /// <summary>Updates the live column from a fresh read without disturbing the target.</summary>
    public void RefreshLive(long value) => Live = value;
}
