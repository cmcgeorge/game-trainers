using System.Collections.ObjectModel;
using DarkDesigns1Trainer.Game;
using DarkDesigns1Trainer.Memory;

namespace DarkDesigns1Trainer.ViewModels;

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

    public string[] ClassOptions => CharacterFormat.ClassNames[1..];

    private bool _freezeBody;
    public bool FreezeBody { get => _freezeBody; set => SetField(ref _freezeBody, value); }

    private bool _freezeMagic;
    public bool FreezeMagic { get => _freezeMagic; set => SetField(ref _freezeMagic, value); }

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
                () => Record.GetAttribute(idx),
                v => { Record.SetAttribute(idx, v); Poke(CharacterFormat.AttributeOffsets[idx], 2); RaiseDerived(); }));
        }
    }

    // --- identity / summary --------------------------------------------------
    public string Name
    {
        get => Record.Name;
        set { Record.Name = value; Poke(CharacterFormat.OffNameLen, CharacterFormat.NameLength + 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public string Title => $"{Record.Name}  —  L{Record.Level} {Record.ClassName}";
    public string Summary =>
        $"Body {Record.BodyCurrent}/{Record.BodyMax}   MP {Record.MagicCurrent}   " +
        $"XP {Record.Experience}   Gold {Record.Gold}   [{Record.StatusName}]";
    public string ListLabel => $"{Record.Name}  (L{Record.Level} {Record.ClassName})";

    public int ClassIndex
    {
        get => Record.Class - 1;
        set { Record.Class = value + 1; Poke(CharacterFormat.OffClass, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public int Level
    {
        get => Record.Level;
        set { Record.Level = value; Poke(CharacterFormat.OffLevel, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public long Experience
    {
        get => Record.Experience;
        set { Record.Experience = (int)value; Poke(CharacterFormat.OffExperience, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    public long Gold
    {
        get => Record.Gold;
        set { Record.Gold = (int)value; Poke(CharacterFormat.OffGold, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- vitals --------------------------------------------------------------
    public int BodyCurrent
    {
        get => Record.BodyCurrent;
        set { Record.BodyCurrent = value; Poke(CharacterFormat.OffBodyCur, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int BodyMax
    {
        get => Record.BodyMax;
        set { Record.BodyMax = value; Poke(CharacterFormat.OffBodyMax, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int MagicCurrent
    {
        get => Record.MagicCurrent;
        set { Record.MagicCurrent = value; Poke(CharacterFormat.OffMagicCur, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- status --------------------------------------------------------------
    public int Status
    {
        get => Record.Status;
        set { Record.Status = value; Poke(CharacterFormat.OffStatus, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- quick actions -------------------------------------------------------
    public void FullHeal()
    {
        Record.BodyCurrent = Record.BodyMax; Poke(CharacterFormat.OffBodyCur, 2);
        Record.Status = CharacterFormat.StatusFine; Poke(CharacterFormat.OffStatus, 2);
        OnPropertyChanged(nameof(BodyCurrent)); OnPropertyChanged(nameof(Status)); RaiseDerived();
    }

    public void MaxAttributes()
    {
        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
        { Record.SetAttribute(i, CharacterFormat.MaxAttribute); Poke(CharacterFormat.AttributeOffsets[i], 2); }
        foreach (var a in Attributes) a.Refresh();
        RaiseDerived();
    }

    public void MaxMoney()
    {
        Record.Gold = CharacterFormat.MaxGold; Poke(CharacterFormat.OffGold, 2);
        OnPropertyChanged(nameof(Gold));
    }

    public void MaxEverything()
    {
        MaxAttributes();
        Record.BodyMax = CharacterFormat.MaxVital; Poke(CharacterFormat.OffBodyMax, 2);
        Record.BodyCurrent = CharacterFormat.MaxVital; Poke(CharacterFormat.OffBodyCur, 2);
        Record.MagicCurrent = CharacterFormat.MaxVital; Poke(CharacterFormat.OffMagicCur, 2);
        Record.Level = CharacterFormat.MaxLevel; Poke(CharacterFormat.OffLevel, 1);
        Record.Experience = CharacterFormat.MaxExperience; Poke(CharacterFormat.OffExperience, 2);
        Record.Gold = CharacterFormat.MaxGold; Poke(CharacterFormat.OffGold, 2);
        Record.Status = CharacterFormat.StatusFine; Poke(CharacterFormat.OffStatus, 2);
        RefreshEditors(); RaiseDerived();
    }

    // --- freeze / live refresh ----------------------------------------------
    public void ApplyFreeze()
    {
        if (!_host.IsAttached) return;
        if (FreezeBody && Record.BodyCurrent != Record.BodyMax)
        { Record.BodyCurrent = Record.BodyMax; Poke(CharacterFormat.OffBodyCur, 2); }
        if (FreezeMagic && Record.MagicCurrent < CharacterFormat.MaxVital)
        { Record.MagicCurrent = CharacterFormat.MaxVital; Poke(CharacterFormat.OffMagicCur, 2); }
        if (FreezeStatus && Record.Status != CharacterFormat.StatusFine)
        { Record.Status = CharacterFormat.StatusFine; Poke(CharacterFormat.OffStatus, 2); }
    }

    public void RefreshLiveSummary(byte[] fresh)
    {
        Array.Copy(fresh, 0, Record.Bytes, 0, CharacterFormat.RecordSize);
        RefreshEditors();
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
        OnPropertyChanged(nameof(BodyCurrent)); OnPropertyChanged(nameof(BodyMax));
        OnPropertyChanged(nameof(MagicCurrent));
        OnPropertyChanged(nameof(Level)); OnPropertyChanged(nameof(Experience)); OnPropertyChanged(nameof(Gold));
        OnPropertyChanged(nameof(ClassIndex));
        OnPropertyChanged(nameof(Status));
    }
}
