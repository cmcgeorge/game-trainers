using BardsTaleTrilogyTrainer.Game;
using BardsTaleTrilogyTrainer.Memory;

namespace BardsTaleTrilogyTrainer.ViewModels;

/// <summary>
/// View model for a single character in the party. Wraps a <see cref="CharacterRecord"/>
/// and exposes editable properties with change notification. Handles freeze toggles
/// for HP and SP, spell management, and item charge editing.
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly CharacterRecord _record;
    private readonly ICharacterHost _host;

    private bool _freezeHp;
    private bool _freezeSp;
    private int _hpCur;
    private int _hpMax;
    private int _spCur;
    private int _spMax;
    private int _experience;
    private int _level;
    private int _race;
    private int _class;
    private int _armorClass;
    private int _strCur; private int _iqCur; private int _dxCur; private int _cnCur; private int _lkCur;
    private int _strMax; private int _iqMax; private int _dxMax; private int _cnMax; private int _lkMax;
    private byte _conjurerLvl; private byte _magicianLvl; private byte _sorcererLvl; private byte _wizardLvl;

    public CharacterViewModel(CharacterRecord record, ICharacterHost host)
    {
        _record = record;
        _host = host;
        Refresh();
    }

    public int Slot => _record.Slot;
    public string Name => _record.Name;
    public string DisplayClass => CharacterFormat.ClassName(_class);
    public string DisplayRace => CharacterFormat.RaceName(_race);
    public bool IsOccupied => _record.IsOccupied;
    public string Header => $"#{Slot}: {Name} ({DisplayRace} {DisplayClass} L{Level})";

    // --- editable fields ---
    public int HpCur { get => _hpCur; set => SetField(ref _hpCur, value); }
    public int HpMax { get => _hpMax; set => SetField(ref _hpMax, value); }
    public int SpCur { get => _spCur; set => SetField(ref _spCur, value); }
    public int SpMax { get => _spMax; set => SetField(ref _spMax, value); }
    public int Experience { get => _experience; set => SetField(ref _experience, value); }
    public int Level { get => _level; set => SetField(ref _level, value); }
    public int Race { get => _race; set => SetField(ref _race, value); }
    public int Class { get => _class; set => SetField(ref _class, value); }
    public int ArmorClass { get => _armorClass; set => SetField(ref _armorClass, value); }

    public int StrCur { get => _strCur; set => SetField(ref _strCur, value); }
    public int IqCur { get => _iqCur; set => SetField(ref _iqCur, value); }
    public int DxCur { get => _dxCur; set => SetField(ref _dxCur, value); }
    public int CnCur { get => _cnCur; set => SetField(ref _cnCur, value); }
    public int LkCur { get => _lkCur; set => SetField(ref _lkCur, value); }
    public int StrMax { get => _strMax; set => SetField(ref _strMax, value); }
    public int IqMax { get => _iqMax; set => SetField(ref _iqMax, value); }
    public int DxMax { get => _dxMax; set => SetField(ref _dxMax, value); }
    public int CnMax { get => _cnMax; set => SetField(ref _cnMax, value); }
    public int LkMax { get => _lkMax; set => SetField(ref _lkMax, value); }

    public byte ConjurerLevel { get => _conjurerLvl; set => SetField(ref _conjurerLvl, value); }
    public byte MagicianLevel { get => _magicianLvl; set => SetField(ref _magicianLvl, value); }
    public byte SorcererLevel { get => _sorcererLvl; set => SetField(ref _sorcererLvl, value); }
    public byte WizardLevel { get => _wizardLvl; set => SetField(ref _wizardLvl, value); }

    // --- freeze toggles ---
    public bool FreezeHp { get => _freezeHp; set => SetField(ref _freezeHp, value); }
    public bool FreezeSp { get => _freezeSp; set => SetField(ref _freezeSp, value); }

    // --- operations ---
    public void Refresh()
    {
        _hpCur = _record.HpCur;
        _hpMax = _record.HpMax;
        _spCur = _record.SpCur;
        _spMax = _record.SpMax;
        _experience = _record.Experience;
        _level = _record.Level;
        _race = _record.Race;
        _class = _record.Class;
        _armorClass = _record.ArmorClass;
        _strCur = _record.GetStatCur(0); _iqCur = _record.GetStatCur(1); _dxCur = _record.GetStatCur(2);
        _cnCur = _record.GetStatCur(3); _lkCur = _record.GetStatCur(4);
        _strMax = _record.GetStatMax(0); _iqMax = _record.GetStatMax(1); _dxMax = _record.GetStatMax(2);
        _cnMax = _record.GetStatMax(3); _lkMax = _record.GetStatMax(4);
        _conjurerLvl = _record.GetSpellLevel(0);
        _magicianLvl = _record.GetSpellLevel(1);
        _sorcererLvl = _record.GetSpellLevel(2);
        _wizardLvl = _record.GetSpellLevel(3);
        OnPropertyChanged(string.Empty);
    }

    public void WriteAll()
    {
        _record.HpCur = _hpCur;
        _record.HpMax = _hpMax;
        _record.SpCur = _spCur;
        _record.SpMax = _spMax;
        _record.Experience = _experience;
        _record.Level = _level;
        _record.Race = _race;
        _record.Class = _class;
        _record.ArmorClass = _armorClass;
        _record.SetStatCur(0, _strCur); _record.SetStatCur(1, _iqCur); _record.SetStatCur(2, _dxCur);
        _record.SetStatCur(3, _cnCur); _record.SetStatCur(4, _lkCur);
        _record.SetStatMax(0, _strMax); _record.SetStatMax(1, _iqMax); _record.SetStatMax(2, _dxMax);
        _record.SetStatMax(3, _cnMax); _record.SetStatMax(4, _lkMax);
        _record.SetSpellLevel(0, _conjurerLvl);
        _record.SetSpellLevel(1, _magicianLvl);
        _record.SetSpellLevel(2, _sorcererLvl);
        _record.SetSpellLevel(3, _wizardLvl);
        _host.OnMessage($"Wrote all fields for {Name}");
    }

    public void LearnAllSpells()
    {
        _record.LearnAllClassSpells();
        _conjurerLvl = 7; _magicianLvl = 7; _sorcererLvl = 7; _wizardLvl = 7;
        OnPropertyChanged(nameof(ConjurerLevel));
        OnPropertyChanged(nameof(MagicianLevel));
        OnPropertyChanged(nameof(SorcererLevel));
        OnPropertyChanged(nameof(WizardLevel));
        _host.OnMessage($"{Name}: learned all class spells (Conjurer/Magician/Sorcerer/Wizard level 7)");
    }

    /// <summary>Writes the four spell-class level bytes from the view model to game memory.</summary>
    public void WriteSpellLevels()
    {
        _record.SetSpellLevel(0, _conjurerLvl);
        _record.SetSpellLevel(1, _magicianLvl);
        _record.SetSpellLevel(2, _sorcererLvl);
        _record.SetSpellLevel(3, _wizardLvl);
    }

    public void SetInfiniteItems()
    {
        bool ok = _record.SetAllItemsInfinite();
        _host.OnMessage(ok
            ? $"{Name}: all item charges set to 0 (infinite)"
            : $"{Name}: could not write all item charges (inventory array not readable)");
    }

    public void FullHeal()
    {
        _record.HpCur = _record.HpMax;
        _record.SpCur = _record.SpMax;
        _hpCur = _record.HpCur;
        _spCur = _record.SpCur;
        OnPropertyChanged(nameof(HpCur));
        OnPropertyChanged(nameof(SpCur));
        _host.OnMessage($"{Name}: full heal (HP {_hpCur}/{_hpMax}, SP {_spCur}/{_spMax})");
    }

    public void MaxAttributes()
    {
        int max = GameFacts.MaxAttribute;
        for (int i = 0; i < 5; i++)
        {
            _record.SetStatMax(i, max);
            _record.SetStatCur(i, max);
        }
        _strCur = _iqCur = _dxCur = _cnCur = _lkCur = max;
        _strMax = _iqMax = _dxMax = _cnMax = _lkMax = max;
        OnPropertyChanged(string.Empty);
        _host.OnMessage($"{Name}: all attributes set to {max}");
    }

    /// <summary>Called by the poll loop to apply freezes and refresh display values.</summary>
    public void PollFreezes()
    {
        if (_freezeHp && _hpMax > 0)
            _record.HpCur = _hpMax;
        if (_freezeSp && _spMax > 0)
            _record.SpCur = _spMax;
    }
}

/// <summary>Interface the view model uses to send messages to the host.</summary>
public interface ICharacterHost
{
    void OnMessage(string msg);
}
