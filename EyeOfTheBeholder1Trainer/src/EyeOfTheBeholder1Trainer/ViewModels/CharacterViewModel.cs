using System.Collections.ObjectModel;
using EyeOfTheBeholder1Trainer.Game;
using EyeOfTheBeholder1Trainer.Memory;

namespace EyeOfTheBeholder1Trainer.ViewModels;

/// <summary>
/// Editable view over a single located character record. Every setter mutates the backing
/// <see cref="Record"/> buffer and, when attached, writes just the changed field to the game's
/// live memory so edits take effect immediately.
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly ICharacterHost _host;
    private readonly bool _isLive;

    public nuint Address { get; }
    public int Slot { get; }
    public CharacterRecord Record { get; }

    public ObservableCollection<NamedValueViewModel> Attributes { get; } = new();

    public string[] RaceOptions => CharacterFormat.RaceNames;
    public string[] ClassOptions => CharacterFormat.ClassNames;
    public string[] AlignmentOptions => CharacterFormat.AlignmentNames;

    // --- freeze toggles ------------------------------------------------------
    private bool _freezeHp;
    public bool FreezeHp { get => _freezeHp; set => SetField(ref _freezeHp, value); }

    private bool _freezeFood;
    public bool FreezeFood { get => _freezeFood; set => SetField(ref _freezeFood, value); }

    public CharacterViewModel(ICharacterHost host, LocatedCharacter located)
    {
        _host = host;
        _isLive = true;
        Address = located.Address;
        Slot = located.Slot;
        Record = located.Record;
        InitializeAttributes();
    }

    /// <summary>Constructor for offline save-editor mode (no live writes).</summary>
    public CharacterViewModel(CharacterRecord record, int slot)
    {
        _host = null!;
        _isLive = false;
        Address = 0;
        Slot = slot;
        Record = record;
        InitializeAttributes();
    }

    private void InitializeAttributes()
    {
        for (int i = 0; i < CharacterFormat.AbilityCount; i++)
        {
            int idx = i;
            Attributes.Add(new NamedValueViewModel(CharacterFormat.AbilityShort[i],
                () => Record.GetAbility(idx),
                v =>
                {
                    if (idx == 0)
                    {
                        Record.Strength = v;
                        Poke(CharacterFormat.OffStrMod, 2);
                        if (v != 18) { Poke(CharacterFormat.OffStrExcMod, 1); Poke(CharacterFormat.OffStrExcBase, 1); }
                    }
                    else
                    {
                        Record.SetAbility(idx, v);
                        Poke(CharacterFormat.AbilityModOffsets[idx], 2);
                    }
                    RaiseDerived();
                }));
        }
    }

    // --- identity / summary --------------------------------------------------
    public string Name
    {
        get => Record.Name;
        set { Record.Name = value; Poke(CharacterFormat.OffName, CharacterFormat.NameLength); OnPropertyChanged(); RaiseDerived(); }
    }

    public string Title => $"{Record.Name}  —  L{Record.EffectiveLevel} {Record.ClassName}";
    public string Summary =>
        $"HP {Record.HpCurrent}/{Record.HpMax}  AC {Record.ArmorClass}  Food {Record.Food}%  " +
        $"{Record.RaceName}  {Record.AlignmentName}  XP {Record.TotalXp}";
    public string ListLabel => $"{Record.Name}  (L{Record.EffectiveLevel})";

    // --- race / class / alignment (combo-backed) ----------------------------
    public int RaceIndex
    {
        get => Record.Race;
        set { Record.Race = value; Poke(CharacterFormat.OffRace, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public int ClassIndex
    {
        get => Record.Class;
        set { Record.Class = value; Poke(CharacterFormat.OffClass, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public int AlignmentIndex
    {
        get => Record.Alignment;
        set { Record.Alignment = value; Poke(CharacterFormat.OffAlignment, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- exceptional strength ------------------------------------------------
    public int StrExcModified
    {
        get => Record.StrExcModified;
        set { Record.StrExcModified = value; Poke(CharacterFormat.OffStrExcMod, 1); OnPropertyChanged(); }
    }
    public int StrExcBase
    {
        get => Record.StrExcBase;
        set { Record.StrExcBase = value; Poke(CharacterFormat.OffStrExcBase, 1); OnPropertyChanged(); }
    }

    // --- vitals --------------------------------------------------------------
    public int HpCurrent
    {
        get => Record.HpCurrent;
        set { Record.HpCurrent = value; Poke(CharacterFormat.OffHpCur, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int HpMax
    {
        get => Record.HpMax;
        set { Record.HpMax = value; Poke(CharacterFormat.OffHpMax, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int ArmorClass
    {
        get => Record.ArmorClass;
        set { Record.ArmorClass = value; Poke(CharacterFormat.OffAC, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int Food
    {
        get => Record.Food;
        set { Record.Food = value; Poke(CharacterFormat.OffFood, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- levels / XP ---------------------------------------------------------
    public int Level1
    {
        get => Record.Level1;
        set { Record.Level1 = value; Poke(CharacterFormat.OffLevel1, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int Level2
    {
        get => Record.Level2;
        set { Record.Level2 = value; Poke(CharacterFormat.OffLevel2, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int Level3
    {
        get => Record.Level3;
        set { Record.Level3 = value; Poke(CharacterFormat.OffLevel3, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public long Xp1
    {
        get => Record.Xp1;
        set { Record.Xp1 = value; Poke(CharacterFormat.OffXp1, 4); OnPropertyChanged(); RaiseDerived(); }
    }
    public long Xp2
    {
        get => Record.Xp2;
        set { Record.Xp2 = value; Poke(CharacterFormat.OffXp2, 4); OnPropertyChanged(); RaiseDerived(); }
    }
    public long Xp3
    {
        get => Record.Xp3;
        set { Record.Xp3 = value; Poke(CharacterFormat.OffXp3, 4); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- quick actions -------------------------------------------------------
    public void FullHeal()
    {
        Record.HpCurrent = Record.HpMax; Poke(CharacterFormat.OffHpCur, 1);
        OnPropertyChanged(nameof(HpCurrent)); RaiseDerived();
    }

    public void MaxAttributes()
    {
        for (int i = 0; i < CharacterFormat.AbilityCount; i++)
        {
            Record.SetAbility(i, CharacterFormat.MaxAttribute);
            Poke(CharacterFormat.AbilityModOffsets[i], 2);
        }
        // Set exceptional strength to 100 for fighters
        Record.StrExcModified = CharacterFormat.MaxStrExc; Poke(CharacterFormat.OffStrExcMod, 1);
        Record.StrExcBase = CharacterFormat.MaxStrExc; Poke(CharacterFormat.OffStrExcBase, 1);
        foreach (var a in Attributes) a.Refresh();
        OnPropertyChanged(nameof(StrExcModified)); OnPropertyChanged(nameof(StrExcBase));
        RaiseDerived();
    }

    public void MaxHp()
    {
        Record.HpMax = CharacterFormat.MaxHp; Poke(CharacterFormat.OffHpMax, 1);
        Record.HpCurrent = CharacterFormat.MaxHp; Poke(CharacterFormat.OffHpCur, 1);
        OnPropertyChanged(nameof(HpMax)); OnPropertyChanged(nameof(HpCurrent)); RaiseDerived();
    }

    public void MaxEverything()
    {
        MaxAttributes();
        MaxHp();
        Record.ArmorClass = CharacterFormat.MinAC; Poke(CharacterFormat.OffAC, 1);
        Record.Food = CharacterFormat.MaxFood; Poke(CharacterFormat.OffFood, 1);
        Record.Level1 = CharacterFormat.MaxLevel; Poke(CharacterFormat.OffLevel1, 1);
        Record.Xp1 = CharacterFormat.MaxXp; Poke(CharacterFormat.OffXp1, 4);
        RefreshEditors(); RaiseDerived();
    }

    // --- freeze / live refresh ----------------------------------------------
    /// <summary>Called each poll tick: re-pin any frozen vital to its value in live memory.</summary>
    public void ApplyFreeze()
    {
        if (!_isLive || _host is null || !_host.IsAttached) return;
        if (FreezeHp && Record.HpCurrent < Record.HpMax)
        { Record.HpCurrent = Record.HpMax; Poke(CharacterFormat.OffHpCur, 1); }
        if (FreezeFood && Record.Food < CharacterFormat.MaxFood)
        { Record.Food = CharacterFormat.MaxFood; Poke(CharacterFormat.OffFood, 1); }
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
        if (_isLive && _host is { IsAttached: true })
            _host.WriteBytes(Address, Record.Bytes, offset, length);
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
        OnPropertyChanged(nameof(ArmorClass)); OnPropertyChanged(nameof(Food));
        OnPropertyChanged(nameof(Level1)); OnPropertyChanged(nameof(Level2)); OnPropertyChanged(nameof(Level3));
        OnPropertyChanged(nameof(Xp1)); OnPropertyChanged(nameof(Xp2)); OnPropertyChanged(nameof(Xp3));
        OnPropertyChanged(nameof(RaceIndex)); OnPropertyChanged(nameof(ClassIndex)); OnPropertyChanged(nameof(AlignmentIndex));
    }
}
