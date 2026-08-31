using System.Collections.ObjectModel;
using LegacyOfTheAncientsTrainer.Game;
using LegacyOfTheAncientsTrainer.Memory;

namespace LegacyOfTheAncientsTrainer.ViewModels;

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

    public ObservableCollection<NamedValueViewModel> Characteristics { get; } = new();

    // --- freeze toggles -----------------------------------------------------
    private bool _freezeHP;
    public bool FreezeHP { get => _freezeHP; set => SetField(ref _freezeHP, value); }

    private int _hpFreezeValue;

    public CharacterViewModel(ICharacterHost host, LocatedCharacter located)
    {
        _host = host;
        Address = located.Address;
        Record = located.Record;

        for (int i = 0; i < CharacterFormat.CharacteristicCount; i++)
        {
            int idx = i;
            int off = CharacterFormat.CharacteristicOffsets[idx];
            int size = CharacterFormat.CharacteristicSizes[idx];
            Characteristics.Add(new NamedValueViewModel(CharacterFormat.CharacteristicShort[idx],
                () => Record.GetCharacteristic(idx),
                v => { Record.SetCharacteristic(idx, v); Poke(off, size); RaiseDerived(); }));
        }

        _hpFreezeValue = Record.HP;
    }

    // --- identity / summary -------------------------------------------------
    public string Name
    {
        get => Record.Name;
        set { Record.Name = value; Poke(CharacterFormat.OffName, CharacterFormat.NameLength); OnPropertyChanged(); RaiseDerived(); }
    }

    public string Title => $"{Record.Name}  —  L{Record.Level}";
    public string Summary =>
        $"HP {Record.HP}   STR {Record.Strength}   END {Record.Endurance}   DEX {Record.Dexterity}   " +
        $"INT {Record.Intelligence}   CHA {Record.Charm}";
    public string ListLabel => $"{Record.Name}  (L{Record.Level})";

    // --- vitals -------------------------------------------------------------
    public int HP
    {
        get => Record.HP;
        set { Record.HP = value; Poke(CharacterFormat.OffHP, CharacterFormat.HPSize); OnPropertyChanged(); RaiseDerived(); UpdateFreezeValues(); }
    }

    public int Level
    {
        get => Record.Level;
        set { Record.Level = value; Poke(CharacterFormat.OffLevel, CharacterFormat.LevelSize); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- quick actions ------------------------------------------------------
    public void FullHeal()
    {
        Record.HP = CharacterFormat.MaxHP;
        Poke(CharacterFormat.OffHP, CharacterFormat.HPSize);
        OnPropertyChanged(nameof(HP));
        RaiseDerived();
        UpdateFreezeValues();
    }

    public void MaxCharacteristics()
    {
        for (int i = 0; i < CharacterFormat.CharacteristicCount; i++)
        {
            Record.SetCharacteristic(i, CharacterFormat.MaxCharacteristic);
            Poke(CharacterFormat.CharacteristicOffsets[i], CharacterFormat.CharacteristicSizes[i]);
        }
        foreach (var c in Characteristics) c.Refresh();
        RaiseDerived();
    }

    public void MaxEverything()
    {
        MaxCharacteristics();
        Record.HP = CharacterFormat.MaxHP;
        Poke(CharacterFormat.OffHP, CharacterFormat.HPSize);
        Record.Level = CharacterFormat.MaxLevelValue;
        Poke(CharacterFormat.OffLevel, CharacterFormat.LevelSize);
        RefreshEditors();
        RaiseDerived();
        UpdateFreezeValues();
    }

    // --- freeze / live refresh ----------------------------------------------
    /// <summary>Called each poll tick: re-pin any frozen vital to its stored value in live memory.</summary>
    public void ApplyFreeze()
    {
        if (!_host.IsAttached) return;
        if (FreezeHP && Record.HP != _hpFreezeValue)
        {
            Record.HP = _hpFreezeValue;
            Poke(CharacterFormat.OffHP, CharacterFormat.HPSize);
        }
    }

    /// <summary>
    /// Poll-tick refresh: copy the latest game bytes into the record and raise only the
    /// read-only summary properties, so watching HP tick never clobbers a value being typed.
    /// Only updates freeze baseline when the freeze toggle is OFF.
    /// </summary>
    public void RefreshLiveSummary(byte[] fresh)
    {
        Array.Copy(fresh, 0, Record.Bytes, 0, CharacterFormat.RecordSize);
        RaiseDerived();
        if (!FreezeHP) _hpFreezeValue = Record.HP;
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
        foreach (var c in Characteristics) c.Refresh();
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(HP));
        OnPropertyChanged(nameof(Level));
    }

    private void UpdateFreezeValues()
    {
        _hpFreezeValue = Record.HP;
    }
}
