using BardsTale1Trainer.Game;

namespace BardsTale1Trainer.ViewModels;

/// <summary>One attribute row (current + max), editable, persisted to memory via the owner.
/// Bard's Tale stores each attribute as two u16s: max (permanent) then current (active).</summary>
public sealed class StatViewModel : ObservableObject
{
    private readonly CharacterViewModel _owner;
    private readonly int _index;

    public string Name { get; }

    public StatViewModel(CharacterViewModel owner, int index, string name)
    {
        _owner = owner;
        _index = index;
        Name = name;
    }

    private int OffMax => PartyFormat.OffStatsMax + _index * 2;
    private int OffCur => PartyFormat.OffStatsCur + _index * 2;

    /// <summary>Permanent stat value (restored after drains).</summary>
    public int Normal
    {
        get => _owner.Record.GetStatMax(_index);
        set
        {
            var v = Clamp(value);
            if (_owner.Record.GetStatMax(_index) == v) return;
            _owner.Record.SetStatMax(_index, v);
            _owner.PushRange(OffMax, 2);
            _owner.RaiseHex(OffMax); _owner.RaiseHex(OffMax + 1);
            OnPropertyChanged();
        }
    }

    /// <summary>Active (current) stat value used by the game right now.</summary>
    public int Current
    {
        get => _owner.Record.GetStatCur(_index);
        set
        {
            var v = Clamp(value);
            if (_owner.Record.GetStatCur(_index) == v) return;
            _owner.Record.SetStatCur(_index, v);
            _owner.PushRange(OffCur, 2);
            _owner.RaiseHex(OffCur); _owner.RaiseHex(OffCur + 1);
            OnPropertyChanged();
        }
    }

    public void SetBoth(int value)
    {
        Normal = value;
        Current = value;
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Normal));
        OnPropertyChanged(nameof(Current));
    }

    private static ushort Clamp(int v) => (ushort)Math.Clamp(v, 0, 0xFFFF);
}
