using System.Collections.ObjectModel;
using FountainOfDreamsTrainer.Game;
using FountainOfDreamsTrainer.Memory;

namespace FountainOfDreamsTrainer.ViewModels;

/// <summary>
/// Editable view over a single located Fountain of Dreams character record. Every setter mutates
/// the backing <see cref="Record"/> buffer and, when attached, writes just the changed field back
/// to the game's live memory (read-validate-write) so edits take effect immediately.
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly ICharacterHost _host;

    public nuint Address { get; }
    public int Slot { get; }
    public CharacterRecord Record { get; }

    public ObservableCollection<NamedValueViewModel> Attributes { get; } = new();
    public ObservableCollection<ItemRowViewModel> Items { get; } = new();

    private bool _freezeHealth;
    /// <summary>Re-pins current constitution to its max every poll tick while set.</summary>
    public bool FreezeHealth { get => _freezeHealth; set => SetField(ref _freezeHealth, value); }

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
                v => { Record.SetAttribute(idx, v); Poke(CharacterFormat.OffAttributes + idx, 1); },
                AttributeBook.DescriptionOf(idx)));
        }

        for (int i = 0; i < CharacterFormat.InventorySlots; i++)
            Items.Add(new ItemRowViewModel(i, Record, RewriteInventory));
    }

    public int ItemCount => Record.ItemCount;

    private void RewriteInventory()
    {
        Poke(CharacterFormat.OffInventory, CharacterFormat.InventoryBytes);
        foreach (var it in Items) it.Refresh();
        OnPropertyChanged(nameof(ItemCount));
        RaiseDerived();
    }

    // --- identity / summary --------------------------------------------------
    public string Name
    {
        get => Record.Name;
        set
        {
            string s = value ?? "";
            int storeLen = Math.Min(s.Length, CharacterFormat.NameFieldLength - 1);
            bool valid = storeLen >= 1;
            for (int i = 0; valid && i < storeLen; i++)
            {
                char ch = s[i];
                valid = i == 0
                    ? (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z')
                    : ch is >= (char)0x20 and <= (char)0x7E;
            }
            if (!valid) { OnPropertyChanged(); return; }
            Record.Name = s; Poke(CharacterFormat.OffName, CharacterFormat.NameFieldLength);
            OnPropertyChanged(); RaiseDerived();
        }
    }

    public string Title =>
        $"{Record.Name}  —  L{Record.Level} {Record.ProfessionName}";

    public string Summary =>
        $"CON {Record.Con}/{Record.MaxCon}   XP {Record.Experience}   ${Record.Cash}" +
        $"   AC {Record.ArmorClass}   Items {Record.ItemCount}";

    public string ListLabel => $"{Record.Name}  (L{Record.Level})";

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

    public long Cash
    {
        get => Record.Cash;
        set { Record.Cash = value; Poke(CharacterFormat.OffCash, 4); OnPropertyChanged(); RaiseDerived(); }
    }

    public int ArmorClass
    {
        get => Record.ArmorClass;
        set { Record.ArmorClass = value; Poke(CharacterFormat.OffArmorClass, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- vitals (constitution) ----------------------------------------------
    public int Con
    {
        get => Record.Con;
        set { Record.Con = value; Poke(CharacterFormat.OffCon, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public int MaxCon
    {
        get => Record.MaxCon;
        set { Record.MaxCon = value; Poke(CharacterFormat.OffMaxCon, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    public int Rank
    {
        get => Record.Rank;
        set { Record.Rank = value; Poke(CharacterFormat.OffRank, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    public int NextLevelXp
    {
        get => Record.NextLevelXp;
        set { Record.NextLevelXp = value; Poke(CharacterFormat.OffNextLevelXp, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- quick actions -------------------------------------------------------
    public void FullHeal()
    {
        Record.Con = Record.MaxCon; Poke(CharacterFormat.OffCon, 1);
        OnPropertyChanged(nameof(Con)); RaiseDerived();
    }

    public void MaxAttributes()
    {
        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
            Record.SetAttribute(i, CharacterFormat.MaxAttribute);
        Poke(CharacterFormat.OffAttributes, CharacterFormat.AttributeCount);
        foreach (var a in Attributes) a.Refresh();
    }

    public void MaxMoney()
    {
        Record.Cash = CharacterFormat.MaxCash; Poke(CharacterFormat.OffCash, 4);
        OnPropertyChanged(nameof(Cash)); RaiseDerived();
    }

    public void MaxEverything()
    {
        MaxAttributes();
        Record.MaxCon = CharacterFormat.MaxCon; Poke(CharacterFormat.OffMaxCon, 2);
        Record.Con = CharacterFormat.MaxCon; Poke(CharacterFormat.OffCon, 1);
        Record.Experience = CharacterFormat.MaxExperience; Poke(CharacterFormat.OffExperience, 4);
        MaxMoney();
        RefreshEditors();
        RaiseDerived();
    }

    // --- freeze / live refresh ----------------------------------------------
    public void ApplyFreeze()
    {
        if (!_host.IsAttached) return;
        if (FreezeHealth && Record.Con != Record.MaxCon)
        {
            Record.Con = Record.MaxCon;
            Poke(CharacterFormat.OffCon, 1);
            OnPropertyChanged(nameof(Con));
            RaiseDerived();
        }
    }

    /// <summary>
    /// Poll-tick refresh: copy the latest game bytes into the record and raise only the read-only
    /// summary properties, so watching CON tick never clobbers a value being typed.
    /// </summary>
    public void RefreshLiveSummary(byte[] fresh)
    {
        bool inventoryChanged = !fresh.AsSpan(CharacterFormat.OffInventory, CharacterFormat.InventoryBytes)
            .SequenceEqual(Record.Bytes.AsSpan(CharacterFormat.OffInventory, CharacterFormat.InventoryBytes));
        Array.Copy(fresh, 0, Record.Bytes, 0, CharacterFormat.RecordSize);
        if (inventoryChanged)
        {
            foreach (var it in Items) it.Refresh();
            OnPropertyChanged(nameof(ItemCount));
        }
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
        foreach (var it in Items) it.Refresh();
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Level)); OnPropertyChanged(nameof(Experience));
        OnPropertyChanged(nameof(Cash)); OnPropertyChanged(nameof(ArmorClass));
        OnPropertyChanged(nameof(Con)); OnPropertyChanged(nameof(MaxCon));
        OnPropertyChanged(nameof(Rank)); OnPropertyChanged(nameof(NextLevelXp));
        OnPropertyChanged(nameof(ItemCount));
    }
}
