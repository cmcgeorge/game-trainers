using MightAndMagic1Trainer.Game;
using MightAndMagic1Trainer.Mvvm;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>
/// One editable [normal, current] byte-pair row, persisted to memory via the owner.
/// Backs both the seven attributes (based at <see cref="RosterFormat.OffStats"/>) and the
/// eight resistances (based at <see cref="RosterFormat.OffResistances"/>) — the record
/// stores both the same way: byte 0 = permanent value, byte 1 = active/temp value.
/// </summary>
public sealed class StatViewModel : ObservableObject
{
    private readonly CharacterViewModel _owner;
    private readonly int _offNormal;   // byte0 = permanent

    public string Name { get; }

    public StatViewModel(CharacterViewModel owner, int baseOffset, int index, string name)
    {
        _owner = owner;
        _offNormal = baseOffset + index * 2;
        Name = name;
    }

    private int OffCurrent => _offNormal + 1;   // byte1 = temp/active

    /// <summary>Permanent value (restored on rest).</summary>
    public byte Normal
    {
        get => _owner.Record.GetByte(_offNormal);
        set
        {
            if (_owner.Record.GetByte(_offNormal) == value) return;
            _owner.Record.SetByte(_offNormal, value);
            _owner.PushByte(_offNormal);
            _owner.RaiseHex(_offNormal);
            OnPropertyChanged();
        }
    }

    /// <summary>Active (temporary) value used by the game right now.</summary>
    public byte Current
    {
        get => _owner.Record.GetByte(OffCurrent);
        set
        {
            if (_owner.Record.GetByte(OffCurrent) == value) return;
            _owner.Record.SetByte(OffCurrent, value);
            _owner.PushByte(OffCurrent);
            _owner.RaiseHex(OffCurrent);
            OnPropertyChanged();
        }
    }

    public void SetBoth(byte value)
    {
        Normal = value;
        Current = value;
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Normal));
        OnPropertyChanged(nameof(Current));
    }
}
