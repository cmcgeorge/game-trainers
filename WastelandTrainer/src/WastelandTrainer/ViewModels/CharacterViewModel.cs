using System.Collections.ObjectModel;
using WastelandTrainer.Game;
using WastelandTrainer.Memory;

namespace WastelandTrainer.ViewModels;

/// <summary>
/// Editable view over a single located Wasteland character record. Every setter mutates the backing
/// <see cref="Record"/> buffer and, when attached, writes just the changed field back to the game's
/// live memory (read-validate-write) so edits take effect immediately.
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly ICharacterHost _host;

    public nuint Address { get; }
    public int Slot { get; }
    public CharacterRecord Record { get; }

    public ObservableCollection<NamedValueViewModel> Attributes { get; } = new();
    public ObservableCollection<SkillRowViewModel> Skills { get; } = new();
    public ObservableCollection<ItemRowViewModel> Items { get; } = new();

    public string[] GenderOptions => CharacterFormat.Genders;
    public string[] NationalityOptions => CharacterFormat.Nationalities;

    private bool _freezeHealth;
    /// <summary>Re-pins current constitution (the ranger's hit points) to its max every poll tick while set.</summary>
    public bool FreezeHealth { get => _freezeHealth; set => SetField(ref _freezeHealth, value); }

    private readonly List<int> _toppedAmmoSlots = new();
    private bool _freezeAmmo;
    /// <summary>
    /// Tops every ammo-bearing item (weapon rounds, clips, charges) up to
    /// <see cref="CharacterFormat.MaxAmmo"/> each poll tick while set, so ammo never runs low.
    /// </summary>
    public bool FreezeAmmo { get => _freezeAmmo; set => SetField(ref _freezeAmmo, value); }

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
                v => { Record.SetAttribute(idx, v); Poke(CharacterFormat.OffAttributes + idx, 1); }));
        }

        foreach (var info in SkillBook.Skills)
        {
            var skill = info;
            Skills.Add(new SkillRowViewModel(skill,
                () => Record.GetSkillLevel(skill.Id),
                v => { Record.SetSkillLevel(skill.Id, v); Poke(CharacterFormat.OffSkills, CharacterFormat.SkillBlockBytes); }));
        }

        for (int i = 0; i < CharacterFormat.ItemSlots; i++)
            Items.Add(new ItemRowViewModel(i, Record, RewriteInventory));
    }

    public int ItemCount => Record.ItemCount;

    /// <summary>
    /// Commits an inventory edit: compact the list to the gap-free, terminated form the game reads,
    /// push the whole 60-byte block back to live memory, then re-raise every row (compaction may have
    /// shifted items up into different slots). Writing the whole block rather than a single slot keeps
    /// the in-game list well-formed no matter which row the user touched.
    /// </summary>
    private void RewriteInventory()
    {
        Record.CompactInventory();
        Poke(CharacterFormat.OffInventory, CharacterFormat.ItemBlockBytes);
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
            // The locator identifies an occupied slot by a name that is at least two characters,
            // starts with an ASCII letter, and is NUL-terminated. A name that fails that test would
            // drop this ranger — or, mid-roster, the whole party — out of the next scan, so reject it
            // and revert the text box to the current stored name. Validate the bytes that will
            // actually be stored: WastelandText masks each character to 7 bits, so a pasted non-ASCII
            // character can mask to NUL/control (e.g. 'Ā' U+0100 → 0x00) and silently shorten or
            // corrupt the name — checking the raw string alone would let that through.
            string s = value ?? "";
            int storeLen = Math.Min(s.Length, CharacterFormat.NameLength - 1);   // Encode keeps one byte for the NUL
            bool valid = storeLen >= 2;
            for (int i = 0; valid && i < storeLen; i++)
            {
                int ch = s[i] & 0x7F;   // the byte Encode will actually write
                valid = i == 0
                    ? (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z')   // first char: a letter
                    : ch is >= 0x20 and <= 0x7E;                             // rest: printable ASCII
            }
            if (!valid) { OnPropertyChanged(); return; }
            Record.Name = s; Poke(CharacterFormat.OffName, CharacterFormat.NameLength); OnPropertyChanged(); RaiseDerived();
        }
    }

    public string Title =>
        $"{Record.Name}  —  L{Record.Level} {Record.GenderName}, {Record.NationalityName}" +
        (string.IsNullOrWhiteSpace(Record.Rank) ? "" : $"  ({Record.Rank})");

    public string Summary =>
        $"CON {Record.Con}/{Record.MaxCon}   XP {Record.Experience}   ${Record.Money}   " +
        $"SKP {Record.SkillPoints}   AC {Record.ArmorClass}   Items {Record.ItemCount}";

    public string ListLabel => $"{Record.Name}  (L{Record.Level})";

    public int GenderIndex
    {
        get => Record.Gender;
        set { Record.Gender = value; Poke(CharacterFormat.OffGender, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public int NationalityIndex
    {
        get => Record.Nationality;
        set { Record.Nationality = value; Poke(CharacterFormat.OffNationality, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public int Level
    {
        get => Record.Level;
        set { Record.Level = value; Poke(CharacterFormat.OffLevel, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public long Experience
    {
        get => Record.Experience;
        set { Record.Experience = value; Poke(CharacterFormat.OffExperience, 3); OnPropertyChanged(); RaiseDerived(); }
    }

    public long Money
    {
        get => Record.Money;
        set { Record.Money = value; Poke(CharacterFormat.OffMoney, 3); OnPropertyChanged(); RaiseDerived(); }
    }

    public int SkillPoints
    {
        get => Record.SkillPoints;
        set { Record.SkillPoints = value; Poke(CharacterFormat.OffSkillPoints, 1); OnPropertyChanged(); RaiseDerived(); }
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
        set { Record.Con = value; Poke(CharacterFormat.OffCon, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    public int MaxCon
    {
        get => Record.MaxCon;
        set { Record.MaxCon = value; Poke(CharacterFormat.OffMaxCon, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- quick actions -------------------------------------------------------
    public void FullHeal()
    {
        Record.Con = Record.MaxCon; Poke(CharacterFormat.OffCon, 2);
        OnPropertyChanged(nameof(Con)); RaiseDerived();
    }

    public void MaxAttributes()
    {
        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
            Record.SetAttribute(i, CharacterFormat.MaxAttribute);
        Poke(CharacterFormat.OffAttributes, CharacterFormat.AttributeCount);
        foreach (var a in Attributes) a.Refresh();
    }

    /// <summary>
    /// Raises every skill the character already knows to the trainer's max level. Adding brand-new
    /// skills is left to the per-skill editor rows, so this never overflows the 30-slot skill list
    /// or grants skills the character cannot use.
    /// </summary>
    public void MaxSkills()
    {
        foreach (var e in Record.GetSkills())
            Record.SetSkillLevel(e.Id, CharacterFormat.MaxSkillLevel);
        Poke(CharacterFormat.OffSkills, CharacterFormat.SkillBlockBytes);
        foreach (var s in Skills) s.Refresh();
    }

    public void MaxMoney()
    {
        Record.Money = CharacterFormat.MaxMoney; Poke(CharacterFormat.OffMoney, 3);
        OnPropertyChanged(nameof(Money)); RaiseDerived();
    }

    public void MaxEverything()
    {
        MaxAttributes();
        MaxSkills();
        Record.MaxCon = CharacterFormat.MaxCon; Poke(CharacterFormat.OffMaxCon, 2);
        Record.Con = CharacterFormat.MaxCon; Poke(CharacterFormat.OffCon, 2);
        Record.Experience = CharacterFormat.MaxExperience; Poke(CharacterFormat.OffExperience, 3);
        Record.SkillPoints = CharacterFormat.MaxSkillPoints; Poke(CharacterFormat.OffSkillPoints, 1);
        MaxMoney();
        RefreshEditors();
        RaiseDerived();
    }

    // --- freeze / live refresh ----------------------------------------------
    /// <summary>
    /// Called each poll tick after <see cref="RefreshLiveSummary"/> has copied in the latest bytes:
    /// re-pin frozen constitution to its max in live memory. Running on the just-read bytes (rather
    /// than the previous tick's snapshot) restores a CON drop the same tick it happens instead of one
    /// poll interval later; the display is refreshed so it never lingers on the momentary drop.
    /// </summary>
    public void ApplyFreeze()
    {
        if (!_host.IsAttached) return;
        if (FreezeHealth && Record.Con != Record.MaxCon)
        {
            Record.Con = Record.MaxCon;
            Poke(CharacterFormat.OffCon, 2);
            OnPropertyChanged(nameof(Con));
            RaiseDerived();
        }
    }

    /// <summary>
    /// Called each poll tick after <see cref="RefreshLiveSummary"/> has copied in the latest inventory
    /// bytes: top every ammo-bearing item up to <see cref="CharacterFormat.MaxAmmo"/> and clear any
    /// jammed-weapon flag. Runs on the just-read bytes (as does <see cref="ApplyFreeze"/>), then pokes
    /// back just the single quantity byte of each slot it changed and re-raises only those rows — so the
    /// Inventory tab shows the topped-up (and un-jammed) amount, without snapping back a quantity being
    /// typed into an unrelated row. Writing one byte per changed slot (rather than the whole block) both
    /// follows the "poke only the changed range" rule and can't clobber a concurrent in-game compaction,
    /// since it never rewrites item ids or other slots.
    /// </summary>
    public void ApplyAmmoFreeze()
    {
        if (!_host.IsAttached || !FreezeAmmo) return;
        if (!AmmoFreeze.TopUp(Record, _toppedAmmoSlots)) return;
        foreach (int slot in _toppedAmmoSlots)
        {
            // +1 = the quantity byte of the (id, qty) pair; the id byte is never rewritten.
            Poke(CharacterFormat.OffInventory + slot * CharacterFormat.SlotSize + 1, 1);
            Items[slot].Refresh();
        }
    }

    /// <summary>
    /// Poll-tick refresh: copy the latest game bytes into the record and raise only the read-only
    /// summary properties, so watching CON tick never clobbers a value being typed. The editable
    /// inventory rows are re-raised only when the game actually changed the inventory block, so an
    /// item id/quantity the user is mid-typing on the Inventory tab is not overwritten each tick.
    /// </summary>
    public void RefreshLiveSummary(byte[] fresh)
    {
        bool inventoryChanged = !fresh.AsSpan(CharacterFormat.OffInventory, CharacterFormat.ItemBlockBytes)
            .SequenceEqual(Record.Bytes.AsSpan(CharacterFormat.OffInventory, CharacterFormat.ItemBlockBytes));
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
        foreach (var s in Skills) s.Refresh();
        foreach (var it in Items) it.Refresh();
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(GenderIndex)); OnPropertyChanged(nameof(NationalityIndex));
        OnPropertyChanged(nameof(Level)); OnPropertyChanged(nameof(Experience)); OnPropertyChanged(nameof(Money));
        OnPropertyChanged(nameof(SkillPoints)); OnPropertyChanged(nameof(ArmorClass));
        OnPropertyChanged(nameof(Con)); OnPropertyChanged(nameof(MaxCon));
        OnPropertyChanged(nameof(ItemCount));
    }
}
