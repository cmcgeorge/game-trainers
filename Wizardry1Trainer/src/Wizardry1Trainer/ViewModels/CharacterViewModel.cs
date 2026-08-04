using System.Collections.ObjectModel;
using Wizardry1Trainer.Game;
using Wizardry1Trainer.Memory;

namespace Wizardry1Trainer.ViewModels;

/// <summary>
/// Editable view over a single located character record. Every setter mutates the backing
/// <see cref="Record"/> buffer and, when attached, writes just the changed field to the game's
/// live memory so edits take effect immediately.
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly ICharacterHost _host;

    public nuint Address { get; }
    public int Slot { get; }
    public CharacterRecord Record { get; }

    public ObservableCollection<NamedValueViewModel> Attributes { get; } = new();

    public string[] RaceOptions => CharacterFormat.RaceNames;
    public string[] ClassOptions => CharacterFormat.ClassNames;
    public string[] AlignmentOptions => CharacterFormat.AlignmentNames;

    private bool _freezeHp;
    public bool FreezeHp { get => _freezeHp; set => SetField(ref _freezeHp, value); }

    private bool _freezeStatus;
    public bool FreezeStatus { get => _freezeStatus; set => SetField(ref _freezeStatus, value); }

    public CharacterViewModel(ICharacterHost host, LocatedCharacter located)
    {
        _host = host;
        Address = located.Address;
        Slot = located.Slot;
        Record = located.Record;

        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
        {
            int idx = i;
            Attributes.Add(new NamedValueViewModel(CharacterFormat.AttributeShort[idx],
                () => Record.GetAttribute(idx),
                v => { Record.SetAttribute(idx, v); Poke(CharacterFormat.OffAttributes, 4); RaiseDerived(); }));
        }
    }

    // --- identity / summary --------------------------------------------------
    public string Name
    {
        get => Record.Name;
        set { Record.Name = value; Poke(CharacterFormat.OffName, CharacterFormat.NameFieldLength); OnPropertyChanged(); RaiseDerived(); }
    }

    public string Title => $"{Record.Name}  --  L{Record.Level} {Record.ClassName}";
    public string Summary =>
        $"HP {Record.HpCurrent}/{Record.HpMax}   AC {Record.ArmorClass}   " +
        $"GP {Record.Gold:N0}   XP {Record.Experience:N0}   [{Record.StatusName}]";
    public string ListLabel => $"{Record.Name}  (L{Record.Level} {Record.ClassName})";

    public int RaceIndex
    {
        get => Record.Race - 1;
        set { Record.Race = value + 1; Poke(CharacterFormat.OffRace, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int ClassIndex
    {
        get => Record.Class;
        set { Record.Class = value; Poke(CharacterFormat.OffClass, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int AlignmentIndex
    {
        get => Record.Alignment - 1;
        set { Record.Alignment = value + 1; Poke(CharacterFormat.OffAlignment, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int StatusValue
    {
        get => Record.Status;
        set { Record.Status = value; Poke(CharacterFormat.OffStatus, 2); OnPropertyChanged(); OnPropertyChanged(nameof(StatusName)); RaiseDerived(); }
    }

    public string StatusName => Record.StatusName;

    // --- progression ---------------------------------------------------------
    public int Level
    {
        get => Record.Level;
        set { Record.Level = value; Poke(CharacterFormat.OffLevel, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public long Experience
    {
        get => Record.Experience;
        set { Record.Experience = value; Poke(CharacterFormat.OffExperience, CharacterFormat.WizLongSize); OnPropertyChanged(); RaiseDerived(); }
    }
    public long Gold
    {
        get => Record.Gold;
        set { Record.Gold = value; Poke(CharacterFormat.OffGold, CharacterFormat.WizLongSize); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- vitals --------------------------------------------------------------
    public int HpCurrent
    {
        get => Record.HpCurrent;
        set { Record.HpCurrent = value; Poke(CharacterFormat.OffHpCurrent, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int HpMax
    {
        get => Record.HpMax;
        set { Record.HpMax = value; Poke(CharacterFormat.OffHpMax, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- combat --------------------------------------------------------------
    public int ArmorClass
    {
        get => Record.ArmorClass;
        set { Record.ArmorClass = value; Poke(CharacterFormat.OffArmorClass, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- spells --------------------------------------------------------------
    public bool GetSpellKnown(int index) => Record.GetSpellKnown(index);
    public void SetSpellKnown(int index, bool known)
    {
        Record.SetSpellKnown(index, known);
        Poke(CharacterFormat.OffSpellKnowledge + (index >> 3), 1);
    }

    public int GetMageSpellCharges(int level) => Record.GetMageSpellCharges(level);
    public void SetMageSpellCharges(int level, int charges)
    {
        Record.SetMageSpellCharges(level, charges);
        Poke(CharacterFormat.OffMageSpells + (level - 1) * 2, 2);
    }

    public int GetPriestSpellCharges(int level) => Record.GetPriestSpellCharges(level);
    public void SetPriestSpellCharges(int level, int charges)
    {
        Record.SetPriestSpellCharges(level, charges);
        Poke(CharacterFormat.OffPriestSpells + (level - 1) * 2, 2);
    }

    // --- commands ------------------------------------------------------------
    public void FullHeal()
    {
        Record.HpCurrent = Record.HpMax; Poke(CharacterFormat.OffHpCurrent, 2);
        Record.Status = CharacterFormat.StatusOK; Poke(CharacterFormat.OffStatus, 2);
        OnPropertyChanged(nameof(HpCurrent)); OnPropertyChanged(nameof(StatusName)); RaiseDerived();
    }

    public void MaxAttributes()
    {
        Record.SetAllAttributes(CharacterFormat.MaxAttribute);
        Poke(CharacterFormat.OffAttributes, 4);
        foreach (var a in Attributes) a.Refresh();
        RaiseDerived();
    }

    public void MaxHp()
    {
        Record.HpMax = CharacterFormat.MaxHp; Poke(CharacterFormat.OffHpMax, 2);
        Record.HpCurrent = CharacterFormat.MaxHp; Poke(CharacterFormat.OffHpCurrent, 2);
        OnPropertyChanged(nameof(HpMax)); OnPropertyChanged(nameof(HpCurrent)); RaiseDerived();
    }

    public void MaxGold()
    {
        Record.Gold = CharacterFormat.MaxGold; Poke(CharacterFormat.OffGold, CharacterFormat.WizLongSize);
        OnPropertyChanged(nameof(Gold));
    }

    public void MaxExperience()
    {
        Record.Experience = CharacterFormat.MaxExperience; Poke(CharacterFormat.OffExperience, CharacterFormat.WizLongSize);
        OnPropertyChanged(nameof(Experience));
    }

    public void LearnAllSpells()
    {
        Record.LearnAllSpells();
        Poke(CharacterFormat.OffSpellKnowledge, CharacterFormat.SpellKnowledgeBytes);
        Record.SetAllSpellCharges(CharacterFormat.MaxSpellCharges);
        Poke(CharacterFormat.OffMageSpells, 14);
        Poke(CharacterFormat.OffPriestSpells, 14);
    }

    public void MaxEverything()
    {
        MaxAttributes();
        MaxHp();
        LearnAllSpells();
        MaxGold();
        MaxExperience();
        Record.Status = CharacterFormat.StatusOK; Poke(CharacterFormat.OffStatus, 2);
        RefreshEditors(); OnPropertyChanged(nameof(StatusName)); RaiseDerived();
    }

    // --- freeze / live refresh ----------------------------------------------
    public void ApplyFreeze()
    {
        if (!_host.IsAttached) return;
        if (FreezeHp && Record.HpCurrent != Record.HpMax)
        { Record.HpCurrent = Record.HpMax; Poke(CharacterFormat.OffHpCurrent, 2); OnPropertyChanged(nameof(HpCurrent)); }
        if (FreezeStatus && Record.Status != CharacterFormat.StatusOK)
        { Record.Status = CharacterFormat.StatusOK; Poke(CharacterFormat.OffStatus, 2); OnPropertyChanged(nameof(StatusName)); }
    }

    /// <summary>
    /// Poll-tick refresh: copy the latest game bytes into the record and raise only the
    /// read-only summary properties, so watching HP tick never clobbers a value being typed.
    /// </summary>
    public void RefreshLiveSummary(byte[] fresh)
    {
        Array.Copy(fresh, 0, Record.Bytes, 0, CharacterFormat.RecordSize);
        OnPropertyChanged(nameof(StatusName));
        RaiseDerived();
    }

    // --- write plumbing ------------------------------------------------------
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
        OnPropertyChanged(nameof(HpCurrent)); OnPropertyChanged(nameof(HpMax));
        OnPropertyChanged(nameof(Level)); OnPropertyChanged(nameof(Experience)); OnPropertyChanged(nameof(Gold));
        OnPropertyChanged(nameof(ArmorClass));
        OnPropertyChanged(nameof(RaceIndex)); OnPropertyChanged(nameof(ClassIndex)); OnPropertyChanged(nameof(AlignmentIndex));
    }
}
