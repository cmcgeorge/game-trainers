using System.Collections.ObjectModel;
using AmberstarTrainer.Game;
using AmberstarTrainer.Memory;

namespace AmberstarTrainer.ViewModels;

/// <summary>
/// Editable view over a single located Amberstar character record. Every setter mutates the
/// backing <see cref="Record"/> buffer and, when attached, writes just the changed field to
/// the game's live memory so edits take effect immediately. All multi-byte values are
/// big-endian (Amberstar's native format).
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly ICharacterHost _host;

    public nuint Address { get; }
    public int Slot { get; }
    public CharacterRecord Record { get; }

    public ObservableCollection<NamedValueViewModel> Attributes { get; } = new();
    public ObservableCollection<NamedValueViewModel> Skills { get; } = new();

    public string[] RaceOptions => RaceBook.Selectable;
    public string[] ClassOptions => ClassBook.Selectable;

    // --- freeze toggles ------------------------------------------------------
    private bool _freezeHp;
    public bool FreezeHp { get => _freezeHp; set => SetField(ref _freezeHp, value); }

    private bool _freezeSp;
    public bool FreezeSp { get => _freezeSp; set => SetField(ref _freezeSp, value); }

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
            Attributes.Add(new NamedValueViewModel(CharacterFormat.AttributeShort[i],
                () => Record.GetAttrCur(idx),
                v => { Record.SetAttribute(idx, v); Poke(CharacterFormat.OffAttrCur + idx * 2, 2);
                       Poke(CharacterFormat.OffAttrMax + idx * 2, 2); RaiseDerived(); }));
        }

        for (int i = 0; i < CharacterFormat.SkillCount; i++)
        {
            int idx = i;
            Skills.Add(new NamedValueViewModel(CharacterFormat.SkillNames[i],
                () => Record.GetSkillCur(idx),
                v => { Record.SetSkill(idx, v); Poke(CharacterFormat.OffSkillsCur + idx, 1);
                       Poke(CharacterFormat.OffSkillsMax + idx, 1); }));
        }
    }

    // --- identity / summary --------------------------------------------------
    public string Name
    {
        get => Record.Name;
        set { Record.Name = value; Poke(CharacterFormat.OffName, CharacterFormat.NameLength); OnPropertyChanged(); RaiseDerived(); }
    }

    public string Title => $"{Record.Name}  —  L{Record.Level} {Record.ClassName}";
    public string Summary =>
        $"HP {Record.HpCur}/{Record.HpMax}   SP {Record.SpCur}/{Record.SpMax}   SLP {Record.Slp}   " +
        $"Gold {Record.Gold}   Food {Record.Food}   Exp {Record.Experience}   " +
        $"[{Record.PhysicalAilmentsName}] [{Record.MentalAilmentsName}]";
    public string ListLabel => $"{Record.Name}  (L{Record.Level} {Record.ClassName})";

    public int GenderIndex
    {
        get => Record.Gender;
        set { Record.Gender = value; Poke(CharacterFormat.OffGender, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public int RaceIndex
    {
        get => Record.Race >= 0 && Record.Race < RaceBook.Selectable.Length ? Record.Race : -1;
        set { if (value >= 0) { Record.Race = value; Poke(CharacterFormat.OffRace, 1); OnPropertyChanged(); RaiseDerived(); } }
    }

    public int ClassIndex
    {
        get => Record.Class >= 0 && Record.Class < ClassBook.Selectable.Length ? Record.Class : -1;
        set { if (value >= 0) { Record.Class = value; Poke(CharacterFormat.OffClass, 1); OnPropertyChanged(); RaiseDerived(); } }
    }

    public int Level
    {
        get => Record.Level;
        set { Record.Level = value; Poke(CharacterFormat.OffLevel, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public long Experience
    {
        get => Record.Experience;
        set { Record.Experience = value; Poke(CharacterFormat.OffExperience, 4); OnPropertyChanged(); RaiseDerived(); }
    }

    public int Gold
    {
        get => Record.Gold;
        set { Record.Gold = value; Poke(CharacterFormat.OffGold, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    public int Food
    {
        get => Record.Food;
        set { Record.Food = value; Poke(CharacterFormat.OffFood, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- vitals --------------------------------------------------------------
    public int HpCur
    {
        get => Record.HpCur;
        set { Record.HpCur = value; Poke(CharacterFormat.OffHpCur, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int HpMax
    {
        get => Record.HpMax;
        set { Record.HpMax = value; Poke(CharacterFormat.OffHpMax, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int SpCur
    {
        get => Record.SpCur;
        set { Record.SpCur = value; Poke(CharacterFormat.OffSpCur, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int SpMax
    {
        get => Record.SpMax;
        set { Record.SpMax = value; Poke(CharacterFormat.OffSpMax, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int Slp
    {
        get => Record.Slp;
        set { Record.Slp = value; Poke(CharacterFormat.OffSlp, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- combat --------------------------------------------------------------
    public int BaseDef
    {
        get => Record.BaseDef;
        set { Record.BaseDef = value; Poke(CharacterFormat.OffBaseDef, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int BaseDam
    {
        get => Record.BaseDam;
        set { Record.BaseDam = value; Poke(CharacterFormat.OffBaseDam, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- ailments ------------------------------------------------------------
    public int PhysicalAilments
    {
        get => Record.PhysicalAilments;
        set { Record.PhysicalAilments = value; Poke(CharacterFormat.OffPhysicalAilments, 1); OnPropertyChanged(); OnPropertyChanged(nameof(PhysicalAilmentsName)); RaiseDerived(); }
    }
    public int MentalAilments
    {
        get => Record.MentalAilments;
        set { Record.MentalAilments = value; Poke(CharacterFormat.OffMentalAilments, 1); OnPropertyChanged(); OnPropertyChanged(nameof(MentalAilmentsName)); RaiseDerived(); }
    }
    public string PhysicalAilmentsName => Record.PhysicalAilmentsName;
    public string MentalAilmentsName => Record.MentalAilmentsName;

    // --- spells --------------------------------------------------------------
    public long SpellsWhite
    {
        get => Record.SpellsWhite;
        set { Record.SpellsWhite = value; Poke(CharacterFormat.OffSpellsWhite, 4); OnPropertyChanged(); }
    }
    public long SpellsGrey
    {
        get => Record.SpellsGrey;
        set { Record.SpellsGrey = value; Poke(CharacterFormat.OffSpellsGrey, 4); OnPropertyChanged(); }
    }
    public long SpellsBlack
    {
        get => Record.SpellsBlack;
        set { Record.SpellsBlack = value; Poke(CharacterFormat.OffSpellsBlack, 4); OnPropertyChanged(); }
    }
    public long SpellsSpecial
    {
        get => Record.SpellsSpecial;
        set { Record.SpellsSpecial = value; Poke(CharacterFormat.OffSpellsSpecial, 4); OnPropertyChanged(); }
    }

    // --- commands ------------------------------------------------------------
    public void FullHeal()
    {
        Record.HpCur = Record.HpMax; Poke(CharacterFormat.OffHpCur, 2);
        Record.SpCur = Record.SpMax; Poke(CharacterFormat.OffSpCur, 2);
        Record.PhysicalAilments = 0; Poke(CharacterFormat.OffPhysicalAilments, 1);
        Record.MentalAilments = 0; Poke(CharacterFormat.OffMentalAilments, 1);
        OnPropertyChanged(nameof(HpCur)); OnPropertyChanged(nameof(SpCur));
        OnPropertyChanged(nameof(PhysicalAilments)); OnPropertyChanged(nameof(MentalAilments));
        OnPropertyChanged(nameof(PhysicalAilmentsName)); OnPropertyChanged(nameof(MentalAilmentsName));
        RaiseDerived();
    }

    public void MaxAttributes()
    {
        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
        {
            Record.SetAttribute(i, CharacterFormat.MaxAttribute);
            Poke(CharacterFormat.OffAttrCur + i * 2, 2);
            Poke(CharacterFormat.OffAttrMax + i * 2, 2);
        }
        foreach (var a in Attributes) a.Refresh();
        RaiseDerived();
    }

    public void MaxSkills()
    {
        for (int i = 0; i < CharacterFormat.SkillCount; i++)
        {
            Record.SetSkill(i, CharacterFormat.MaxSkill);
            Poke(CharacterFormat.OffSkillsCur + i, 1);
            Poke(CharacterFormat.OffSkillsMax + i, 1);
        }
        foreach (var s in Skills) s.Refresh();
    }

    public void LearnAllSpells()
    {
        Record.LearnAllSpells();
        Poke(CharacterFormat.OffSpellsWhite, 4);
        Poke(CharacterFormat.OffSpellsGrey, 4);
        Poke(CharacterFormat.OffSpellsBlack, 4);
        Poke(CharacterFormat.OffSpellsSpecial, 4);
        OnPropertyChanged(nameof(SpellsWhite));
        OnPropertyChanged(nameof(SpellsGrey));
        OnPropertyChanged(nameof(SpellsBlack));
        OnPropertyChanged(nameof(SpellsSpecial));
    }

    public void MaxMoney()
    {
        Record.Gold = CharacterFormat.MaxGold; Poke(CharacterFormat.OffGold, 2);
        Record.Food = CharacterFormat.MaxFood; Poke(CharacterFormat.OffFood, 2);
        OnPropertyChanged(nameof(Gold));
        OnPropertyChanged(nameof(Food));
    }

    public void MaxEverything()
    {
        MaxAttributes();
        MaxSkills();
        LearnAllSpells();
        Record.HpMax = CharacterFormat.MaxVital; Poke(CharacterFormat.OffHpMax, 2);
        Record.HpCur = CharacterFormat.MaxVital; Poke(CharacterFormat.OffHpCur, 2);
        Record.SpMax = CharacterFormat.MaxVital; Poke(CharacterFormat.OffSpMax, 2);
        Record.SpCur = CharacterFormat.MaxVital; Poke(CharacterFormat.OffSpCur, 2);
        Record.Slp = CharacterFormat.MaxVital; Poke(CharacterFormat.OffSlp, 2);
        Record.Experience = CharacterFormat.MaxExperience; Poke(CharacterFormat.OffExperience, 4);
        Record.PhysicalAilments = 0; Poke(CharacterFormat.OffPhysicalAilments, 1);
        Record.MentalAilments = 0; Poke(CharacterFormat.OffMentalAilments, 1);
        MaxMoney();
        RefreshEditors(); RaiseDerived();
    }

    // --- freeze / live refresh ----------------------------------------------
    public void ApplyFreeze()
    {
        if (!_host.IsAttached) return;
        bool changed = false;
        if (FreezeHp && Record.HpCur != Record.HpMax)
        { Record.HpCur = Record.HpMax; Poke(CharacterFormat.OffHpCur, 2); OnPropertyChanged(nameof(HpCur)); changed = true; }
        if (FreezeSp && Record.SpCur != Record.SpMax)
        { Record.SpCur = Record.SpMax; Poke(CharacterFormat.OffSpCur, 2); OnPropertyChanged(nameof(SpCur)); changed = true; }
        if (FreezeStatus && (Record.PhysicalAilments != 0 || Record.MentalAilments != 0))
        {
            Record.PhysicalAilments = 0; Poke(CharacterFormat.OffPhysicalAilments, 1);
            Record.MentalAilments = 0; Poke(CharacterFormat.OffMentalAilments, 1);
            OnPropertyChanged(nameof(PhysicalAilments)); OnPropertyChanged(nameof(MentalAilments));
            OnPropertyChanged(nameof(PhysicalAilmentsName)); OnPropertyChanged(nameof(MentalAilmentsName));
            changed = true;
        }
        if (changed) RaiseDerived();
    }

    /// <summary>
    /// Poll-tick refresh: copy the latest game bytes into the record and raise only the
    /// read-only summary properties, so watching HP tick never clobbers a value being typed.
    /// </summary>
    public void RefreshLiveSummary(byte[] fresh)
    {
        Array.Copy(fresh, 0, Record.Bytes, 0, CharacterFormat.RecordSize);
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
        foreach (var s in Skills) s.Refresh();
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(HpCur)); OnPropertyChanged(nameof(HpMax));
        OnPropertyChanged(nameof(SpCur)); OnPropertyChanged(nameof(SpMax)); OnPropertyChanged(nameof(Slp));
        OnPropertyChanged(nameof(Level)); OnPropertyChanged(nameof(Experience));
        OnPropertyChanged(nameof(Gold)); OnPropertyChanged(nameof(Food));
        OnPropertyChanged(nameof(BaseDef)); OnPropertyChanged(nameof(BaseDam));
        OnPropertyChanged(nameof(GenderIndex)); OnPropertyChanged(nameof(RaceIndex)); OnPropertyChanged(nameof(ClassIndex));
        OnPropertyChanged(nameof(PhysicalAilments)); OnPropertyChanged(nameof(MentalAilments));
    }
}
