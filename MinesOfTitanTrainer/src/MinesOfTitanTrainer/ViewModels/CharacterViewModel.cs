using System.Collections.ObjectModel;
using MinesOfTitanTrainer.Game;
using MinesOfTitanTrainer.Memory;

namespace MinesOfTitanTrainer.ViewModels;

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

    public string[] SexOptions { get; } = { "Male", "Female" };

    private long _creditsFreezeValue;
    private bool _freezeCredits;
    public bool FreezeCredits
    {
        get => _freezeCredits;
        set { if (SetField(ref _freezeCredits, value) && value) _creditsFreezeValue = Record.Credits; }
    }

    public CharacterViewModel(ICharacterHost host, LocatedCharacter located)
    {
        _host = host;
        Address = located.Address;
        Slot = located.Slot;
        Record = located.Record;

        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
        {
            int idx = i;
            Attributes.Add(new NamedValueViewModel(CharacterFormat.AttributeNames[i],
                () => Record.GetAttribute(idx),
                v => { Record.SetAttribute(idx, v); Poke(CharacterFormat.OffAttributes + idx, 1); RaiseDerived(); }));
        }

        for (int i = 0; i < CharacterFormat.SkillCount; i++)
        {
            int idx = i;
            Skills.Add(new NamedValueViewModel(CharacterFormat.SkillNames[i],
                () => Record.GetSkill(idx),
                v => { Record.SetSkill(idx, v); Poke(CharacterFormat.OffSkills + idx, 1); }));
        }
    }

    // --- identity / summary --------------------------------------------------
    public string Name
    {
        get => Record.Name;
        set { Record.Name = value; Poke(CharacterFormat.OffName, CharacterFormat.NameLength); OnPropertyChanged(); RaiseDerived(); }
    }

    public int SexIndex
    {
        get => Record.Sex is 'F' or 'f' ? 1 : 0;
        set { Record.Sex = value == 1 ? 'F' : 'M'; Poke(CharacterFormat.OffSex, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public int Age
    {
        get => Record.Age;
        set { Record.Age = value; Poke(CharacterFormat.OffAge, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public long Credits
    {
        get => Record.Credits;
        set
        {
            Record.Credits = value; Poke(CharacterFormat.OffCredits, 4);
            if (_freezeCredits) _creditsFreezeValue = Record.Credits;
            OnPropertyChanged(); RaiseDerived();
        }
    }

    public string Title => $"{Record.Name}  —  {Record.SexName}, age {Record.Age}";
    public string Summary => $"Credits {Record.Credits:N0}   [slot {Slot + 1}]";
    public string ListLabel => $"{Record.Name}  ({Record.SexName})";

    // --- commands ------------------------------------------------------------
    public void MaxAttributes()
    {
        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
        { Record.SetAttribute(i, CharacterFormat.MaxAttribute); Poke(CharacterFormat.OffAttributes + i, 1); }
        foreach (var a in Attributes) a.Refresh();
        RaiseDerived();
    }

    public void MaxSkills()
    {
        for (int i = 0; i < CharacterFormat.SkillCount; i++)
        { Record.SetSkill(i, CharacterFormat.MaxSkill); Poke(CharacterFormat.OffSkills + i, 1); }
        foreach (var s in Skills) s.Refresh();
    }

    public void MaxCredits()
    {
        Record.Credits = CharacterFormat.MaxCredits; Poke(CharacterFormat.OffCredits, 4);
        if (_freezeCredits) _creditsFreezeValue = Record.Credits;
        OnPropertyChanged(nameof(Credits)); RaiseDerived();
    }

    public void MaxEverything()
    {
        MaxAttributes();
        MaxSkills();
        MaxCredits();
        RefreshEditors(); RaiseDerived();
    }

    // --- freeze / live refresh ----------------------------------------------
    /// <summary>
    /// Called each poll tick: re-pin credits to the frozen value in live memory. Writes
    /// unconditionally while frozen — the poll's subsequent re-read copies live bytes into
    /// <see cref="Record"/>, so a stale cached value here must not gate the re-pin (otherwise a
    /// drain the game just applied would linger for a whole tick before being overwritten).
    /// </summary>
    public void ApplyFreeze()
    {
        if (!_host.IsAttached || !_freezeCredits) return;
        Record.Credits = _creditsFreezeValue;
        Poke(CharacterFormat.OffCredits, 4);
    }

    /// <summary>
    /// Poll-tick refresh: copy the latest game bytes into the record and raise only the read-only
    /// summary properties, so watching a value tick never clobbers a field being typed.
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
        OnPropertyChanged(nameof(SexIndex));
        OnPropertyChanged(nameof(Age));
        OnPropertyChanged(nameof(Credits));
    }
}
