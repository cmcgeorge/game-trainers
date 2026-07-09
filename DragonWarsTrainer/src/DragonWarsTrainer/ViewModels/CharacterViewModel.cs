using System.Collections.ObjectModel;
using DragonWarsTrainer.Game;
using DragonWarsTrainer.Memory;

namespace DragonWarsTrainer.ViewModels;

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
    public ObservableCollection<NamedValueViewModel> Skills { get; } = new();

    public string[] GenderOptions => RosterFormat.Genders;

    private bool _freezeHealth;
    public bool FreezeHealth { get => _freezeHealth; set => SetField(ref _freezeHealth, value); }

    private bool _freezeStun;
    public bool FreezeStun { get => _freezeStun; set => SetField(ref _freezeStun, value); }

    private bool _freezePower;
    public bool FreezePower { get => _freezePower; set => SetField(ref _freezePower, value); }

    public CharacterViewModel(ICharacterHost host, LocatedCharacter located)
    {
        _host = host;
        Address = located.Address;
        Slot = located.Slot;
        Record = located.Record;

        for (int i = 0; i < RosterFormat.AttributeCount; i++)
        {
            int idx = i;
            Attributes.Add(new NamedValueViewModel(RosterFormat.AttributeShort[i],
                () => Record.GetAttribute(idx),
                v => { Record.SetAttribute(idx, v); Poke(RosterFormat.AttributeCurOffsets[idx], 2); RaiseDerived(); }));
        }

        for (int i = 0; i < RosterFormat.SkillCount; i++)
        {
            int idx = i;
            Skills.Add(new NamedValueViewModel(RosterFormat.SkillNames[i],
                () => Record.GetSkill(idx),
                v => { Record.SetSkill(idx, v); Poke(RosterFormat.OffSkills + idx, 1); }));
        }
    }

    // --- identity / summary --------------------------------------------------
    public string Name
    {
        get => Record.Name;
        set { Record.Name = value; Poke(RosterFormat.OffName, RosterFormat.NameLength); OnPropertyChanged(); RaiseDerived(); }
    }

    public string Title => $"{Record.Name}  —  L{Record.Level} {Record.GenderName}";
    public string Summary =>
        $"HP {Record.HealthCurrent}/{Record.HealthMax}   Stun {Record.StunCurrent}/{Record.StunMax}   " +
        $"Pow {Record.PowerCurrent}/{Record.PowerMax}   AV {Record.ArmorValue} DV {Record.DefenseValue} AC {Record.ArmorClass}   [{Record.StatusName}]";
    public string ListLabel => $"{Record.Name}  (L{Record.Level})";

    public int GenderIndex
    {
        get => Record.Gender;
        set { Record.Gender = value; Poke(RosterFormat.OffGender, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public int Level
    {
        get => Record.Level;
        set { Record.Level = value; Poke(RosterFormat.OffLevel, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public long Experience
    {
        get => Record.Experience;
        set { Record.Experience = value; Poke(RosterFormat.OffExperience, 4); OnPropertyChanged(); }
    }
    public long Gold
    {
        get => Record.Gold;
        set { Record.Gold = value; Poke(RosterFormat.OffGold, 4); OnPropertyChanged(); }
    }

    // --- vitals --------------------------------------------------------------
    public int HealthCurrent
    {
        get => Record.HealthCurrent;
        set { Record.HealthCurrent = value; Poke(RosterFormat.OffHealthCur, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int HealthMax
    {
        get => Record.HealthMax;
        set { Record.HealthMax = value; Poke(RosterFormat.OffHealthMax, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int StunCurrent
    {
        get => Record.StunCurrent;
        set { Record.StunCurrent = value; Poke(RosterFormat.OffStunCur, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int StunMax
    {
        get => Record.StunMax;
        set { Record.StunMax = value; Poke(RosterFormat.OffStunMax, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int PowerCurrent
    {
        get => Record.PowerCurrent;
        set { Record.PowerCurrent = value; Poke(RosterFormat.OffPowerCur, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int PowerMax
    {
        get => Record.PowerMax;
        set { Record.PowerMax = value; Poke(RosterFormat.OffPowerMax, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- combat --------------------------------------------------------------
    public int ArmorValue
    {
        get => Record.ArmorValue;
        set { Record.ArmorValue = value; Poke(RosterFormat.OffArmorValue, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int DefenseValue
    {
        get => Record.DefenseValue;
        set { Record.DefenseValue = value; Poke(RosterFormat.OffDefenseValue, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int ArmorClass
    {
        get => Record.ArmorClass;
        set { Record.ArmorClass = value; Poke(RosterFormat.OffArmorClass, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- commands ------------------------------------------------------------
    public void FullHeal()
    {
        Record.HealthCurrent = Record.HealthMax; Poke(RosterFormat.OffHealthCur, 2);
        Record.StunCurrent = Record.StunMax; Poke(RosterFormat.OffStunCur, 2);
        Record.Status = 0; Poke(RosterFormat.OffStatus, 1);
        OnPropertyChanged(nameof(HealthCurrent)); OnPropertyChanged(nameof(StunCurrent)); RaiseDerived();
    }

    public void MaxAttributes()
    {
        for (int i = 0; i < RosterFormat.AttributeCount; i++)
        { Record.SetAttribute(i, RosterFormat.MaxAttribute); Poke(RosterFormat.AttributeCurOffsets[i], 2); }
        foreach (var a in Attributes) a.Refresh();
        RaiseDerived();
    }

    public void MaxSkills()
    {
        for (int i = 0; i < RosterFormat.SkillCount; i++)
        { Record.SetSkill(i, RosterFormat.MaxSkillRank); Poke(RosterFormat.OffSkills + i, 1); }
        foreach (var s in Skills) s.Refresh();
    }

    public void LearnAllSpells()
    {
        Record.LearnAllSpells();
        Poke(RosterFormat.OffSpells, RosterFormat.SpellByteCount);
    }

    public void MaxMoney()
    {
        Record.Gold = RosterFormat.MaxGold; Poke(RosterFormat.OffGold, 4);
        OnPropertyChanged(nameof(Gold));
    }

    public void MaxEverything()
    {
        MaxAttributes();
        MaxSkills();
        LearnAllSpells();
        Record.HealthMax = RosterFormat.MaxVital; Poke(RosterFormat.OffHealthMax, 2);
        Record.HealthCurrent = RosterFormat.MaxVital; Poke(RosterFormat.OffHealthCur, 2);
        Record.StunMax = RosterFormat.MaxVital; Poke(RosterFormat.OffStunMax, 2);
        Record.StunCurrent = RosterFormat.MaxVital; Poke(RosterFormat.OffStunCur, 2);
        Record.PowerMax = RosterFormat.MaxVital; Poke(RosterFormat.OffPowerMax, 2);
        Record.PowerCurrent = RosterFormat.MaxVital; Poke(RosterFormat.OffPowerCur, 2);
        Record.Status = 0; Poke(RosterFormat.OffStatus, 1);
        MaxMoney();
        RefreshEditors(); RaiseDerived();
    }

    // --- freeze / live refresh ----------------------------------------------
    /// <summary>Called each poll tick: re-pin any frozen vital to its max in live memory.</summary>
    public void ApplyFreeze()
    {
        if (!_host.IsAttached) return;
        if (FreezeHealth && Record.HealthCurrent != Record.HealthMax)
        { Record.HealthCurrent = Record.HealthMax; Poke(RosterFormat.OffHealthCur, 2); }
        if (FreezeStun && Record.StunCurrent != Record.StunMax)
        { Record.StunCurrent = Record.StunMax; Poke(RosterFormat.OffStunCur, 2); }
        if (FreezePower && Record.PowerCurrent != Record.PowerMax)
        { Record.PowerCurrent = Record.PowerMax; Poke(RosterFormat.OffPowerCur, 2); }
    }

    /// <summary>
    /// Poll-tick refresh: copy the latest game bytes into the record and raise only the
    /// read-only summary properties, so watching HP tick never clobbers a value being typed.
    /// </summary>
    public void RefreshLiveSummary(byte[] fresh)
    {
        Array.Copy(fresh, 0, Record.Bytes, 0, RosterFormat.RecordSize);
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
        OnPropertyChanged(nameof(HealthCurrent)); OnPropertyChanged(nameof(HealthMax));
        OnPropertyChanged(nameof(StunCurrent)); OnPropertyChanged(nameof(StunMax));
        OnPropertyChanged(nameof(PowerCurrent)); OnPropertyChanged(nameof(PowerMax));
        OnPropertyChanged(nameof(Level)); OnPropertyChanged(nameof(Experience)); OnPropertyChanged(nameof(Gold));
        OnPropertyChanged(nameof(ArmorValue)); OnPropertyChanged(nameof(DefenseValue)); OnPropertyChanged(nameof(ArmorClass));
        OnPropertyChanged(nameof(GenderIndex));
    }
}
