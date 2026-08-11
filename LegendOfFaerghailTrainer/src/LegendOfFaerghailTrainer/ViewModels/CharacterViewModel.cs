using System.Collections.ObjectModel;
using LegendOfFaerghailTrainer.Game;
using LegendOfFaerghailTrainer.Memory;

namespace LegendOfFaerghailTrainer.ViewModels;

/// <summary>
/// Editable view over one located Legend of Faerghail character. Every setter mutates the backing
/// <see cref="Record"/> buffer and then writes <b>only the bytes that changed</b> back to the game.
/// That matters here: the record is 410 bytes and holds fields the game rewrites constantly
/// (carried weight, the state byte), so blindly flushing the whole record would race the game and
/// occasionally undo its own bookkeeping.
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly ICharacterHost _host;

    public nuint Address { get; }
    public int Slot { get; }
    public CharacterRecord Record { get; }

    /// <summary>True for a roster entry (read/write, but not part of the active party).</summary>
    public bool IsRosterEntry { get; }

    public ObservableCollection<NamedValueViewModel> Attributes { get; } = new();
    public ObservableCollection<NamedValueViewModel> Abilities { get; } = new();
    public ObservableCollection<NamedFlagViewModel> Languages { get; } = new();
    public ObservableCollection<ItemRowViewModel> Items { get; } = new();
    public ObservableCollection<SpellRowViewModel> Spells { get; } = new();

    private static readonly string[] AttributeNames =
        { "Constitution", "Strength", "Dexterity", "Intelligence", "Wisdom" };

    public CharacterViewModel(ICharacterHost host, LocatedCharacter located, bool isRosterEntry = false)
    {
        _host = host;
        Address = located.Address;
        Slot = located.Slot;
        Record = located.Record;
        IsRosterEntry = isRosterEntry;

        for (int i = 0; i < AttributeNames.Length; i++)
        {
            int idx = i;
            Attributes.Add(new NamedValueViewModel(AttributeNames[i],
                () => Record.GetAttribute(idx),
                v => EditBytes(CharacterFormat.AttributeOffsets[idx], 1, () => Record.SetAttribute(idx, v))));
        }

        for (int i = 0; i < AbilityBook.Count; i++)
        {
            int idx = i;
            Abilities.Add(new NamedValueViewModel(AbilityBook.NameOf(i),
                () => Record.GetAbility(idx),
                v => EditBytes(CharacterFormat.AbilityOffsets[idx], 1, () => Record.SetAbility(idx, v)),
                AbilityBook.DescriptionOf(i)));
        }

        for (int i = 0; i < LanguageBook.Count; i++)
        {
            int idx = i;
            Languages.Add(new NamedFlagViewModel(LanguageBook.NameOf(i),
                () => Record.GetLanguage(idx),
                v => EditBytes(CharacterFormat.OffLanguages + idx, 1, () => Record.SetLanguage(idx, v))));
        }

        for (int i = 0; i < CharacterFormat.InventorySlots; i++)
            Items.Add(new ItemRowViewModel(i, Record, WriteItemSlot));

        for (int i = 0; i < CharacterFormat.SpellSlots; i++)
            Spells.Add(new SpellRowViewModel(i, Record, WriteSpellSlot));

        Array.Copy(Record.Bytes, _shown, CharacterFormat.RecordSize);
    }

    // --- identity ---------------------------------------------------------------

    public string Name
    {
        get => Record.Name;
        set
        {
            string s = (value ?? "").Trim();
            if (s.Length is < 1 or > CharacterFormat.MaxNameLength) { OnPropertyChanged(); return; }
            // Guard before Edit, not inside it: the record's name setter clears all 14 bytes, and
            // the shipped records keep stale fragments after the terminator ("Connar\0er"), so a
            // re-push of the *same* name would still look like a change and write 14 bytes.
            if (s == Record.Name) { OnPropertyChanged(); return; }
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                bool ok = i == 0
                    ? (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
                    : ch is >= (char)0x20 and <= (char)0x7E;
                if (!ok) { OnPropertyChanged(); return; }
            }
            Edit(CharacterFormat.OffName, CharacterFormat.NameFieldLength, () => Record.Name = s);
        }
    }

    public int Level
    {
        get => Record.Level;
        set => Edit(CharacterFormat.OffLevel, 1, () => Record.Level = value);
    }

    // The five index properties below are bound to ComboBox.SelectedIndex, which reports -1 whenever
    // it cannot match the bound value (a template being re-applied, an out-of-range value). Clamping
    // -1 up to 0 would quietly rewrite the character's race or trade, so a negative is refused
    // outright and the stored value pushed back to the control.

    public int RaceIndex
    {
        get => Record.Race;
        set { if (value < 0) { OnPropertyChanged(); return; } Edit(CharacterFormat.OffRace, 1, () => Record.Race = value); }
    }

    public int ClassIndex
    {
        get => Record.Class;
        set { if (value < 0) { OnPropertyChanged(); return; } Edit(CharacterFormat.OffClass, 1, () => Record.Class = value); }
    }

    public int SexIndex
    {
        get => Record.Sex;
        set { if (value < 0) { OnPropertyChanged(); return; } Edit(CharacterFormat.OffSex, 1, () => Record.Sex = value); }
    }

    public int AlignmentIndex
    {
        get => Record.Alignment;
        set { if (value < 0) { OnPropertyChanged(); return; } Edit(CharacterFormat.OffAlignment, 1, () => Record.Alignment = value); }
    }

    public int StatusIndex
    {
        get => Record.Status;
        set { if (value < 0) { OnPropertyChanged(); return; } Edit(CharacterFormat.OffStatus, 1, () => Record.Status = value); }
    }

    // --- pools ------------------------------------------------------------------

    // The locator only accepts a record whose current pools sit inside their maxima and whose
    // carried load sits inside its capacity. An editor that could break those invariants would let
    // the user lock themselves out of their own party: the next Attach would refuse the array with
    // "the six records the party pointer reaches do not look like characters" and give no clue why.
    // So the pairs coerce each other instead.

    public int CurHp
    {
        get => Record.CurHp;
        set => Edit(CharacterFormat.OffCurHp, 2, () => Record.CurHp = Math.Min(value, Record.MaxHp));
    }

    public int MaxHp
    {
        get => Record.MaxHp;
        set => Edit(CharacterFormat.OffMaxHp, 2, () =>
        {
            Record.MaxHp = value;
            if (Record.CurHp > Record.MaxHp)
            {
                Record.CurHp = Record.MaxHp;
                Poke(CharacterFormat.OffCurHp, 2);
                OnPropertyChanged(nameof(CurHp));
            }
        });
    }

    public int CurMagic
    {
        get => Record.CurMagic;
        set => Edit(CharacterFormat.OffCurMagic, 1, () => Record.CurMagic = value);
    }

    public int MaxMagic
    {
        get => Record.MaxMagic;
        set => Edit(CharacterFormat.OffMaxMagic, 1, () => Record.MaxMagic = value);
    }

    public long Experience
    {
        get => Record.Experience;
        set => Edit(CharacterFormat.OffExperience, 4, () => Record.Experience = value);
    }

    public long Gold
    {
        get => Record.Gold;
        // Editing while the freeze is on has to move the frozen figure too, or the next poll tick
        // reverts the edit 400 ms later and the value appears to bounce back on its own.
        set => Edit(CharacterFormat.OffGold, 4, () =>
        {
            Record.Gold = value;
            if (_freezeGold) _frozenGold = Record.Gold;
        });
    }

    public int Rations
    {
        get => Record.Rations;
        set => Edit(CharacterFormat.OffRations, 2, () =>
        {
            Record.Rations = value;
            if (_freezeRations) _frozenRations = Record.Rations;
        });
    }

    public int MaxWeight
    {
        get => Record.MaxWeight;
        // Never below what the character is already carrying, and never zero.
        set => Edit(CharacterFormat.OffMaxWeight, 2,
            () => Record.MaxWeight = Math.Max(value, Math.Max(1, Record.CurWeight + 1)));
    }

    public int ArmourPercent
    {
        get => Record.ArmourPercent;
        set => Edit(CharacterFormat.OffArmourPercent, 1, () => Record.ArmourPercent = value);
    }

    /// <summary>
    /// Applies a field edit and writes back only if the record bytes actually moved. The
    /// no-op guard matters: WPF re-pushes a bound value on all sorts of occasions that are not
    /// the user changing anything (a template being re-applied, a DataGrid row being recycled,
    /// the poll loop raising a notification), and each of those would otherwise become a write
    /// into the emulator's memory.
    /// </summary>
    private void Edit(int offset, int length, Action apply,
        [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        EditBytes(offset, length, apply);
        OnPropertyChanged(property);
    }

    /// <summary>
    /// The write half of <see cref="Edit"/>, without the property notification — used by the
    /// attribute, ability and language rows, which raise their own. Returns true if the record
    /// actually changed. Every write-through path goes through here so that the no-op guard and the
    /// <see cref="SyncShown"/> re-baseline can never be forgotten on one of them.
    /// </summary>
    private bool EditBytes(int offset, int length, Action apply)
    {
        Span<byte> before = stackalloc byte[length];
        Record.Bytes.AsSpan(offset, length).CopyTo(before);
        apply();
        if (before.SequenceEqual(Record.Bytes.AsSpan(offset, length))) return false;
        Poke(offset, length);
        SyncShown();
        RaiseDerived();
        return true;
    }

    /// <summary>
    /// Re-baselines what the poll loop considers "already on screen". Called after any edit the
    /// trainer itself made, so the next tick does not report the user's own change back as news.
    /// The record buffer is the last poll's snapshot plus those edits, which is exactly what the
    /// UI is showing.
    /// </summary>
    private void SyncShown() => Array.Copy(Record.Bytes, _shown, CharacterFormat.RecordSize);

    // --- freezes ----------------------------------------------------------------

    private bool _freezeHp;
    /// <summary>Re-pins current hit points to the maximum on every poll tick.</summary>
    public bool FreezeHp { get => _freezeHp; set => SetField(ref _freezeHp, value); }

    private bool _freezeMagic;
    /// <summary>Re-pins current magic points to the maximum on every poll tick.</summary>
    public bool FreezeMagic { get => _freezeMagic; set => SetField(ref _freezeMagic, value); }

    private bool _freezeGold;
    private long _frozenGold;
    /// <summary>Holds gold at the value it had when the toggle was switched on.</summary>
    public bool FreezeGold
    {
        get => _freezeGold;
        // Captured through the same ceiling the setter applies. A target the setter cannot store
        // would never compare equal afterwards, so the freeze would re-write the record on every
        // tick for ever — and quietly lower the player's gold each time.
        set { if (value) _frozenGold = Math.Min(Record.Gold, CharacterFormat.MaxGold); SetField(ref _freezeGold, value); }
    }

    private bool _freezeRations;
    private int _frozenRations;
    /// <summary>Holds rations at the value they had when the toggle was switched on.</summary>
    public bool FreezeRations
    {
        get => _freezeRations;
        set { if (value) _frozenRations = Math.Min(Record.Rations, CharacterFormat.MaxRations); SetField(ref _freezeRations, value); }
    }

    /// <summary>
    /// Applies the active freezes. Called from the poll loop after the record is re-read. Every
    /// target is clamped to what the corresponding setter can actually store, so each freeze
    /// converges after one write instead of firing on every tick.
    /// </summary>
    public void ApplyFreezes()
    {
        int hpTarget = Math.Min(Record.MaxHp, CharacterFormat.MaxHitPoints);
        if (FreezeHp && Record.CurHp != hpTarget)
        {
            Record.CurHp = hpTarget;
            Poke(CharacterFormat.OffCurHp, 2);
        }
        if (FreezeMagic && Record.CurMagic != Record.MaxMagic)
        {
            Record.CurMagic = Record.MaxMagic;
            Poke(CharacterFormat.OffCurMagic, 1);
        }
        if (FreezeGold && Record.Gold != _frozenGold)
        {
            Record.Gold = _frozenGold;
            Poke(CharacterFormat.OffGold, 4);
        }
        if (FreezeRations && Record.Rations != _frozenRations)
        {
            Record.Rations = _frozenRations;
            Poke(CharacterFormat.OffRations, 2);
        }
    }

    // --- quick actions ----------------------------------------------------------

    /// <summary>Restores hit points, magic points, and clears any adverse state.</summary>
    public void FullHeal()
    {
        Record.CurHp = Record.MaxHp;
        Poke(CharacterFormat.OffCurHp, 2);
        Record.CurMagic = Record.MaxMagic;
        Poke(CharacterFormat.OffCurMagic, 1);
        Record.Status = 0;
        Poke(CharacterFormat.OffStatus, 1);
        RefreshAll();
    }

    /// <summary>Sets every attribute to <see cref="CharacterFormat.MaxAttribute"/>.</summary>
    public void MaxAttributes()
    {
        for (int i = 0; i < CharacterFormat.AttributeOffsets.Length; i++)
        {
            Record.SetAttribute(i, CharacterFormat.MaxAttribute);
            Poke(CharacterFormat.AttributeOffsets[i], 1);
        }
        RefreshAll();
    }

    /// <summary>Sets every trained ability to 100%.</summary>
    public void MaxAbilities()
    {
        for (int i = 0; i < AbilityBook.Count; i++)
        {
            Record.SetAbility(i, CharacterFormat.MaxAbility);
            Poke(CharacterFormat.AbilityOffsets[i], 1);
        }
        RefreshAll();
    }

    /// <summary>Marks every language as spoken.</summary>
    public void LearnAllLanguages()
    {
        for (int i = 0; i < LanguageBook.Count; i++)
        {
            Record.SetLanguage(i, true);
            Poke(CharacterFormat.OffLanguages + i, 1);
        }
        RefreshAll();
    }

    /// <summary>Refills the daily uses of every spell the character knows, and mends every item.</summary>
    public void RestockSpellsAndRepairItems()
    {
        for (int i = 0; i < CharacterFormat.SpellSlots; i++)
        {
            var s = Record.GetSpell(i);
            if (s.IsEmpty) continue;
            Record.SetSpell(i, s.SpellId, 99);
            WriteSpellSlot(i);
        }
        for (int i = 0; i < CharacterFormat.InventorySlots; i++)
        {
            var it = Record.GetItem(i);
            if (it.IsEmpty || it.Condition >= 100) continue;
            Record.SetItem(i, it.ItemId, it.Equipped, 100);
            WriteItemSlot(i);
        }
        RefreshAll();
    }

    // --- write plumbing ---------------------------------------------------------

    private void Poke(int offset, int length)
    {
        if (!_host.IsAttached) return;
        _host.WriteBytes(Address, Record.Bytes, offset, length);
    }

    /// <summary>
    /// Writes one inventory slot and, if it moved, the high-water byte the game scans up to.
    /// Without the second write an item placed beyond the old mark is simply never listed.
    /// </summary>
    private void WriteItemSlot(int slot)
    {
        Poke(CharacterFormat.OffInventory + slot * CharacterFormat.InventoryEntrySize,
             CharacterFormat.InventoryEntrySize);
        int mark = Record.InventoryHighWater;
        if (Record.ItemCount != mark)
        {
            Record.ItemCount = mark;
            Poke(CharacterFormat.OffItemCount, 1);
        }
        SyncShown();
    }

    private void WriteSpellSlot(int slot)
    {
        Poke(CharacterFormat.OffSpells + slot * CharacterFormat.SpellEntrySize,
             CharacterFormat.SpellEntrySize);
        int mark = Record.SpellHighWater;
        if (Record.SpellCount != mark)
        {
            Record.SpellCount = mark;
            Poke(CharacterFormat.OffSpellCount, 1);
        }
        SyncShown();
    }

    // --- display ----------------------------------------------------------------

    public string ListLabel =>
        $"{Slot + 1}. {(Record.Name.Length == 0 ? "(empty)" : Record.Name)}  —  Rnk {Record.Level} {Record.ClassName}";

    public string Title =>
        $"{Record.Name}  —  {Record.AlignmentName} {Record.RaceName}-{(Record.Sex == 0 ? "female " : "")}{Record.ClassName}";

    public string Summary =>
        $"Rnk {Record.Level}   HP {Record.CurHp}/{Record.MaxHp}   Magic {Record.CurMagic}/{Record.MaxMagic}   " +
        $"XP {Record.Experience}   Gold {Record.Gold}   Rations {Record.Rations}   " +
        $"Load {Record.CurWeight}/{Record.MaxWeight} lb   Armour {Record.ArmourPercent}%   State {Record.StatusName}";

    public string CarriedWeight => $"{Record.CurWeight} / {Record.MaxWeight} lb";
    public string UnknownCounter => Record.UnknownCounter.ToString();

    // --- polling ----------------------------------------------------------------

    /// <summary>What the UI is currently showing, so the poll loop can tell what actually moved.</summary>
    private readonly byte[] _shown = new byte[CharacterFormat.RecordSize];

    /// <summary>
    /// Takes a freshly read copy of the record from the poll loop, applies any active freezes, and
    /// raises change notifications <b>only for the fields whose bytes actually moved</b>.
    ///
    /// Raising everything on every tick would be wrong, not merely wasteful: the text boxes commit
    /// on lost focus, so a blanket notification four times a second re-reads the record into the box
    /// the user is halfway through typing in and throws the edit away.
    /// </summary>
    public void UpdateFrom(byte[] fresh)
    {
        ArgumentNullException.ThrowIfNull(fresh);
        Array.Copy(fresh, Record.Bytes, CharacterFormat.RecordSize);
        ApplyFreezes();                       // may write back, so diff after it has run
        RefreshChanged();
        Array.Copy(Record.Bytes, _shown, CharacterFormat.RecordSize);
    }

    private bool Moved(int offset, int length)
    {
        for (int i = offset; i < offset + length; i++)
            if (_shown[i] != Record.Bytes[i]) return true;
        return false;
    }

    private void RefreshChanged()
    {
        bool anything = false;

        for (int i = 0; i < Attributes.Count; i++)
            if (Moved(CharacterFormat.AttributeOffsets[i], 1)) { Attributes[i].Refresh(); anything = true; }
        for (int i = 0; i < Abilities.Count; i++)
            if (Moved(CharacterFormat.AbilityOffsets[i], 1)) { Abilities[i].Refresh(); anything = true; }
        for (int i = 0; i < Languages.Count; i++)
            if (Moved(CharacterFormat.OffLanguages + i, 1)) { Languages[i].Refresh(); anything = true; }
        for (int i = 0; i < Items.Count; i++)
            if (Moved(CharacterFormat.OffInventory + i * CharacterFormat.InventoryEntrySize,
                      CharacterFormat.InventoryEntrySize)) { Items[i].Refresh(); anything = true; }
        for (int i = 0; i < Spells.Count; i++)
            if (Moved(CharacterFormat.OffSpells + i * CharacterFormat.SpellEntrySize,
                      CharacterFormat.SpellEntrySize)) { Spells[i].Refresh(); anything = true; }

        foreach (var (offset, length, property) in ScalarFields)
        {
            if (!Moved(offset, length)) continue;
            OnPropertyChanged(property);
            anything = true;
        }

        if (anything) RaiseDerived();
    }

    /// <summary>Every scalar the editor binds, with the record bytes it is backed by.</summary>
    private static readonly (int Offset, int Length, string Property)[] ScalarFields =
    {
        (CharacterFormat.OffName, CharacterFormat.NameFieldLength, nameof(Name)),
        (CharacterFormat.OffLevel, 1, nameof(Level)),
        (CharacterFormat.OffRace, 1, nameof(RaceIndex)),
        (CharacterFormat.OffClass, 1, nameof(ClassIndex)),
        (CharacterFormat.OffSex, 1, nameof(SexIndex)),
        (CharacterFormat.OffAlignment, 1, nameof(AlignmentIndex)),
        (CharacterFormat.OffStatus, 1, nameof(StatusIndex)),
        (CharacterFormat.OffArmourPercent, 1, nameof(ArmourPercent)),
        (CharacterFormat.OffCurHp, 2, nameof(CurHp)),
        (CharacterFormat.OffMaxHp, 2, nameof(MaxHp)),
        (CharacterFormat.OffCurMagic, 1, nameof(CurMagic)),
        (CharacterFormat.OffMaxMagic, 1, nameof(MaxMagic)),
        (CharacterFormat.OffExperience, 4, nameof(Experience)),
        (CharacterFormat.OffGold, 4, nameof(Gold)),
        (CharacterFormat.OffRations, 2, nameof(Rations)),
        (CharacterFormat.OffMaxWeight, 2, nameof(MaxWeight)),
        // Read-only displays, but they still have to be listed: carried weight is the field the
        // game rewrites most often, and without an entry here it would only refresh on the ticks
        // where some other byte happened to move.
        (CharacterFormat.OffCurWeight, 2, nameof(CarriedWeight)),
        (CharacterFormat.OffUnknownCounter, 4, nameof(UnknownCounter)),
    };

    /// <summary>Re-raises every bound property after a quick action rewrote the record wholesale.</summary>
    public void RefreshAll()
    {
        foreach (var a in Attributes) a.Refresh();
        foreach (var a in Abilities) a.Refresh();
        foreach (var l in Languages) l.Refresh();
        foreach (var it in Items) it.Refresh();
        foreach (var sp in Spells) sp.Refresh();
        Array.Copy(Record.Bytes, _shown, CharacterFormat.RecordSize);
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(RaceIndex));
        OnPropertyChanged(nameof(ClassIndex));
        OnPropertyChanged(nameof(SexIndex));
        OnPropertyChanged(nameof(AlignmentIndex));
        OnPropertyChanged(nameof(StatusIndex));
        OnPropertyChanged(nameof(CurHp));
        OnPropertyChanged(nameof(MaxHp));
        OnPropertyChanged(nameof(CurMagic));
        OnPropertyChanged(nameof(MaxMagic));
        OnPropertyChanged(nameof(Experience));
        OnPropertyChanged(nameof(Gold));
        OnPropertyChanged(nameof(Rations));
        OnPropertyChanged(nameof(MaxWeight));
        OnPropertyChanged(nameof(ArmourPercent));
        RaiseDerived();
    }

    private void RaiseDerived()
    {
        OnPropertyChanged(nameof(ListLabel));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(CarriedWeight));
        OnPropertyChanged(nameof(UnknownCounter));
    }
}
