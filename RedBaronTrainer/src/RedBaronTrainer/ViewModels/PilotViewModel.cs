using RedBaronTrainer.Game;

namespace RedBaronTrainer.ViewModels;

/// <summary>A pilot slot, either from the live roster or from <c>ROSTER.DAT</c>.</summary>
public sealed class PilotViewModel : ObservableObject
{
    private PilotRecord _record;
    private string _name;

    public PilotViewModel(int slot, PilotRecord record, bool isActiveCareer = false)
    {
        ArgumentNullException.ThrowIfNull(record);
        Slot = slot;
        _record = record;
        _name = record.Name;
        IsActiveCareer = isActiveCareer;
    }

    /// <summary>Roster index, or -1 for the career currently being flown.</summary>
    public int Slot { get; }

    public bool IsActiveCareer { get; }

    public string SlotLabel => IsActiveCareer ? "Active career" : $"Slot {Slot + 1}";

    public bool IsOccupied => _record.IsOccupied;

    public string Name
    {
        get => _name;
        set
        {
            if (!SetField(ref _name, value ?? "")) return;
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    /// <summary>True once the edited name differs from what was read.</summary>
    public bool IsDirty => IsOccupied && _name != _record.Name;

    public string HexDump => _record.ToHexDump();

    /// <summary>The record with the edited name folded in, ready to be written back.</summary>
    public PilotRecord ToRecord()
    {
        var copy = new PilotRecord(_record.ToArray());
        copy.SetName(_name);
        return copy;
    }

    /// <summary>Replaces the backing record after a re-read or a successful write.</summary>
    public void Reload(PilotRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _record = record;
        _name = record.Name;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(IsOccupied));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(HexDump));
    }

    public override string ToString() => IsOccupied ? $"{SlotLabel}: {Name}" : $"{SlotLabel}: (empty)";
}
