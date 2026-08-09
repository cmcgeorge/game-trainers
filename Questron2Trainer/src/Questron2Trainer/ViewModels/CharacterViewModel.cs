using System.Collections.ObjectModel;
using Questron2Trainer.Game;
using Questron2Trainer.Memory;

namespace Questron2Trainer.ViewModels;

/// <summary>
/// Editable view over the located character record. Every setter mutates the backing
/// <see cref="Record"/> buffer and, when attached, writes just the changed field to the game's
/// live memory so edits take effect immediately.
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly ICharacterHost _host;

    public nuint Address { get; }
    public CharacterRecord Record { get; }

    public ObservableCollection<NamedValueViewModel> Attributes { get; } = new();

    // --- freeze toggles -----------------------------------------------------
    private bool _freezeHP;
    public bool FreezeHP { get => _freezeHP; set => SetField(ref _freezeHP, value); }

    private bool _freezeFood;
    public bool FreezeFood { get => _freezeFood; set => SetField(ref _freezeFood, value); }

    private bool _freezeGold;
    public bool FreezeGold { get => _freezeGold; set => SetField(ref _freezeGold, value); }

    private int _hpFreezeValue;
    private int _foodFreezeValue;
    private int _goldFreezeValue;

    public CharacterViewModel(ICharacterHost host, LocatedCharacter located)
    {
        _host = host;
        Address = located.Address;
        Record = located.Record;

        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
        {
            int idx = i;
            Attributes.Add(new NamedValueViewModel(CharacterFormat.AttributeShort[idx],
                () => Record.GetAttribute(idx),
                v => { Record.SetAttribute(idx, v); Poke(CharacterFormat.OffAttributes + idx, 1); RaiseDerived(); }));
        }

        _hpFreezeValue = Record.HP;
        _foodFreezeValue = Record.Food;
        _goldFreezeValue = Record.Gold;
    }

    // --- identity / summary -------------------------------------------------
    public string Name
    {
        get => Record.Name;
        set { Record.Name = value; Poke(CharacterFormat.OffName, CharacterFormat.NameLength); OnPropertyChanged(); RaiseDerived(); }
    }

    public string Title => $"{Record.Name}  —  L{Record.Level} {Record.LevelName}";
    public string Summary =>
        $"HP {Record.HP}   Food {Record.Food}   Gold {Record.Gold}   " +
        $"Weapon {WeaponBook.Name(Record.Weapon)}   Armor {ArmorBook.Name(Record.Armor)}";
    public string ListLabel => $"{Record.Name}  (L{Record.Level} {Record.LevelName})";

    // --- vitals -------------------------------------------------------------
    public int HP
    {
        get => Record.HP;
        set { Record.HP = value; Poke(CharacterFormat.OffHP, 2); OnPropertyChanged(); RaiseDerived(); UpdateFreezeValues(); }
    }
    public int Food
    {
        get => Record.Food;
        set { Record.Food = value; Poke(CharacterFormat.OffFood, 2); OnPropertyChanged(); RaiseDerived(); UpdateFreezeValues(); }
    }
    public int Gold
    {
        get => Record.Gold;
        set { Record.Gold = value; Poke(CharacterFormat.OffGold, 2); OnPropertyChanged(); RaiseDerived(); UpdateFreezeValues(); }
    }

    // --- equipment ----------------------------------------------------------
    public int Weapon
    {
        get => Record.Weapon;
        set { Record.Weapon = value; Poke(CharacterFormat.OffWeapon, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int Armor
    {
        get => Record.Armor;
        set { Record.Armor = value; Poke(CharacterFormat.OffArmor, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- progression --------------------------------------------------------
    public int Level
    {
        get => Record.Level;
        set { Record.Level = value; Poke(CharacterFormat.OffLevel, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- spells -------------------------------------------------------------
    public int GetSpellCharges(int slot) => Record.GetSpellCharges(slot);
    public void SetSpellCharges(int slot, int value)
    {
        Record.SetSpellCharges(slot, value);
        Poke(CharacterFormat.OffSpellCharges + slot, 1);
    }

    // --- quick actions ------------------------------------------------------
    public void FullHeal()
    {
        Record.HP = CharacterFormat.MaxHP; Poke(CharacterFormat.OffHP, 2);
        Record.Food = CharacterFormat.MaxFood; Poke(CharacterFormat.OffFood, 2);
        OnPropertyChanged(nameof(HP)); OnPropertyChanged(nameof(Food)); RaiseDerived(); UpdateFreezeValues();
    }

    public void MaxAttributes()
    {
        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
        { Record.SetAttribute(i, CharacterFormat.MaxAttribute); Poke(CharacterFormat.OffAttributes + i, 1); }
        foreach (var a in Attributes) a.Refresh();
        RaiseDerived();
    }

    public void MaxGold()
    {
        Record.Gold = CharacterFormat.MaxGold; Poke(CharacterFormat.OffGold, 2);
        OnPropertyChanged(nameof(Gold)); UpdateFreezeValues();
    }

    public void MaxSpells()
    {
        Record.SetAllSpellCharges(CharacterFormat.MaxSpellCharges);
        Poke(CharacterFormat.OffSpellCharges, CharacterFormat.SpellSlotCount);
    }

    public void MaxEverything()
    {
        MaxAttributes();
        Record.HP = CharacterFormat.MaxHP; Poke(CharacterFormat.OffHP, 2);
        Record.Food = CharacterFormat.MaxFood; Poke(CharacterFormat.OffFood, 2);
        Record.Gold = CharacterFormat.MaxGold; Poke(CharacterFormat.OffGold, 2);
        Record.Level = CharacterFormat.MaxLevel; Poke(CharacterFormat.OffLevel, 1);
        MaxSpells();
        RefreshEditors(); RaiseDerived(); UpdateFreezeValues();
    }

    // --- freeze / live refresh ----------------------------------------------
    /// <summary>Called each poll tick: re-pin any frozen vital to its stored value in live memory.</summary>
    public void ApplyFreeze()
    {
        if (!_host.IsAttached) return;
        if (FreezeHP && Record.HP != _hpFreezeValue)
        { Record.HP = _hpFreezeValue; Poke(CharacterFormat.OffHP, 2); }
        if (FreezeFood && Record.Food != _foodFreezeValue)
        { Record.Food = _foodFreezeValue; Poke(CharacterFormat.OffFood, 2); }
        if (FreezeGold && Record.Gold != _goldFreezeValue)
        { Record.Gold = _goldFreezeValue; Poke(CharacterFormat.OffGold, 2); }
    }

    /// <summary>
    /// Poll-tick refresh: copy the latest game bytes into the record and raise only the
    /// read-only summary properties, so watching HP tick never clobbers a value being typed.
    /// Only updates freeze baselines for vitals whose freeze toggle is OFF, so a frozen
    /// vital's target is not silently overwritten with the drifted live value.
    /// </summary>
    public void RefreshLiveSummary(byte[] fresh)
    {
        Array.Copy(fresh, 0, Record.Bytes, 0, CharacterFormat.RecordSize);
        RaiseDerived();
        if (!FreezeHP) _hpFreezeValue = Record.HP;
        if (!FreezeFood) _foodFreezeValue = Record.Food;
        if (!FreezeGold) _goldFreezeValue = Record.Gold;
    }

    // --- write plumbing -----------------------------------------------------
    private void Poke(int offset, int length)
    {
        if (_host.IsAttached) _host.WriteBytes(Address, Record.Bytes, offset, length);
    }

    private void RaiseDerived()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(ListLabel));
    }

    private void RefreshEditors()
    {
        foreach (var a in Attributes) a.Refresh();
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(HP)); OnPropertyChanged(nameof(Food)); OnPropertyChanged(nameof(Gold));
        OnPropertyChanged(nameof(Level)); OnPropertyChanged(nameof(Weapon)); OnPropertyChanged(nameof(Armor));
    }

    private void UpdateFreezeValues()
    {
        _hpFreezeValue = Record.HP;
        _foodFreezeValue = Record.Food;
        _goldFreezeValue = Record.Gold;
    }
}
