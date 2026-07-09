using BardsTale1Trainer.Game;

namespace BardsTale1Trainer.ViewModels;

/// <summary>One of the four magic-art mastery levels (Magician/Conjurer/Sorcerer/Wizard),
/// stored as a single byte (0–7) at 0x41..0x44. A character only casts from the art that
/// matches its class, but the four levels are stored independently (class changes carry
/// the old art's level over), so all four are editable.</summary>
public sealed class SpellLevelViewModel : ObservableObject
{
    private readonly CharacterViewModel _owner;
    private readonly int _index;

    public string Name { get; }

    public SpellLevelViewModel(CharacterViewModel owner, int index, string name)
    {
        _owner = owner;
        _index = index;
        Name = name;
    }

    private int Off => PartyFormat.OffSpellLevels + _index;

    /// <summary>Highest spell level known in this art (0 = none, max 7).</summary>
    public byte Level
    {
        get => _owner.Record.GetSpellLevel(_index);
        set
        {
            var v = (byte)Math.Clamp((int)value, 0, 7);
            if (_owner.Record.GetSpellLevel(_index) == v) return;
            _owner.Record.SetSpellLevel(_index, v);
            _owner.PushByte(Off);
            _owner.RaiseHex(Off);
            OnPropertyChanged();
        }
    }

    public void Refresh() => OnPropertyChanged(nameof(Level));
}
