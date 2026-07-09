using System.Collections.ObjectModel;
using MightAndMagic1Trainer.Game;
using MightAndMagic1Trainer.Memory;
using MightAndMagic1Trainer.Mvvm;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>
/// Editable view of one character. Friendly properties and the raw hex grid both
/// write into the same <see cref="CharacterRecord"/> buffer; whenever a byte changes
/// it is pushed to the live process (if attached).
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly ProcessMemory? _mem;
    private bool _suppressPush;

    public CharacterRecord Record { get; }
    public nuint Address => Record.Address;
    public bool IsLive => _mem != null && Record.Address != 0;

    public ObservableCollection<HexByteViewModel> HexBytes { get; } = new();
    public ObservableCollection<StatViewModel> Stats { get; } = new();

    /// <summary>The 8 elemental/effect resistances as [normal, current] percent pairs.</summary>
    public ObservableCollection<StatViewModel> Resistances { get; } = new();

    /// <summary>The 12 inventory slots: 6 equipped then 6 backpack (item id + charges each).</summary>
    public ObservableCollection<ItemSlotViewModel> Items { get; } = new();

    public CharacterViewModel(CharacterRecord record, ProcessMemory? mem)
    {
        Record = record;
        _mem = mem;

        for (int off = 0; off < RosterFormat.RecordSize; off++)
            HexBytes.Add(new HexByteViewModel(this, off, LabelFor(off)));

        for (int i = 0; i < RosterFormat.StatCount; i++)
            Stats.Add(new StatViewModel(this, RosterFormat.OffStats, i, RosterFormat.Stats[i]));

        for (int i = 0; i < RosterFormat.ResistanceCount; i++)
            Resistances.Add(new StatViewModel(this, RosterFormat.OffResistances, i, RosterFormat.Resistances[i]));

        for (int i = 0; i < RosterFormat.ItemSlotCount; i++)
            Items.Add(new ItemSlotViewModel(this,
                RosterFormat.OffEquipment + i, RosterFormat.OffEquipmentCharges + i, $"Equipped #{i + 1}"));
        for (int i = 0; i < RosterFormat.ItemSlotCount; i++)
            Items.Add(new ItemSlotViewModel(this,
                RosterFormat.OffBackpack + i, RosterFormat.OffBackpackCharges + i, $"Backpack #{i + 1}"));

        // Keep the per-character "Freeze ALL charges" flag in step when a single slot is toggled.
        // Suppressed while FreezeAllCharges itself drives the slots, so the bulk set fires one
        // notification rather than one per slot.
        foreach (var slot in Items)
            slot.PropertyChanged += (_, e) =>
            { if (!_settingAllCharges && e.PropertyName == nameof(ItemSlotViewModel.FreezeCharges)) OnPropertyChanged(nameof(FreezeAllCharges)); };
    }

    // --- selection list display -------------------------------------------------
    public string Title => $"{Record.Slot + 1}. {Record.Name}";
    public string Subtitle => $"L{Record.LevelCur} {Record.ClassName}  ·  HP {Record.HpCur}/{Record.HpMax}";

    // --- friendly fields --------------------------------------------------------
    public string Name
    {
        get => Record.Name;
        set { if (Record.Name != value) { Record.Name = value; PushRange(RosterFormat.OffName, RosterFormat.NameLength); RaiseAll(); } }
    }

    public int SexIndex
    {
        get => Record.Sex;
        set { if (Record.Sex != value) { Record.Sex = (byte)value; PushByte(RosterFormat.OffSex); RaiseAll(); } }
    }

    public int ClassIndex
    {
        get => Record.Class;
        set { if (Record.Class != value) { Record.Class = (byte)value; PushByte(RosterFormat.OffClass); RaiseAll(); } }
    }

    public byte LevelCur
    {
        get => Record.LevelCur;
        set { if (Record.LevelCur != value) { Record.LevelCur = value; PushByte(RosterFormat.OffLevelCur); RaiseAll(); } }
    }

    public byte LevelMax
    {
        get => Record.LevelMax;
        set { if (Record.LevelMax != value) { Record.LevelMax = value; PushByte(RosterFormat.OffLevelMax); RaiseAll(); } }
    }

    public byte Age
    {
        get => Record.Age;
        set { if (Record.Age != value) { Record.Age = value; PushByte(RosterFormat.OffAge); RaiseHex(RosterFormat.OffAge); } }
    }

    public byte ArmorClass
    {
        get => Record.ArmorClass;
        set { if (Record.ArmorClass != value) { Record.ArmorClass = value; PushByte(RosterFormat.OffArmorClass); RaiseHex(RosterFormat.OffArmorClass); } }
    }

    public byte SpellLevel
    {
        get => Record.SpellLevel;
        // Unlike the other byte fields, raise the friendly notification too: the spellbook
        // ("Cast a spell") observes SpellLevel and must rebuild its castable list when it changes.
        set { if (Record.SpellLevel != value) { Record.SpellLevel = value; PushByte(RosterFormat.OffSpellLevel); RaiseHex(RosterFormat.OffSpellLevel); OnPropertyChanged(nameof(SpellLevel)); } }
    }

    public int HpCur
    {
        get => Record.HpCur;
        set { var v = Clamp16(value); if (Record.HpCur != v) { Record.HpCur = v; PushRange(RosterFormat.OffHpCur, 2); RaiseAll(); } }
    }

    public int HpMax
    {
        get => Record.HpMax;
        // HpMod (0x35) and HpMax (0x37) are adjacent, so push both in one 4-byte write.
        set { var v = Clamp16(value); if (Record.HpMax != v) { Record.HpMax = v; Record.HpMod = v; PushRange(RosterFormat.OffHpMod, 4); RaiseAll(); } }
    }

    public int SpCur
    {
        get => Record.SpCur;
        set { var v = Clamp16(value); if (Record.SpCur != v) { Record.SpCur = v; PushRange(RosterFormat.OffSpCur, 2); RaiseAll(); } }
    }

    public int SpMax
    {
        get => Record.SpMax;
        set { var v = Clamp16(value); if (Record.SpMax != v) { Record.SpMax = v; PushRange(RosterFormat.OffSpMax, 2); RaiseAll(); } }
    }

    public long Experience
    {
        get => Record.Experience;
        set { var v = (uint)Math.Clamp(value, 0, uint.MaxValue); if (Record.Experience != v) { Record.Experience = v; PushRange(RosterFormat.OffExperience, 4); RaiseAll(); } }
    }

    public long Gold
    {
        get => Record.Gold;
        set { var v = (uint)Math.Clamp(value, 0, 0xFFFFFF); if (Record.Gold != v) { Record.Gold = v; PushRange(RosterFormat.OffGold, 3); RaiseAll(); } }
    }

    public int Gems
    {
        get => Record.Gems;
        set { var v = Clamp16(value); if (Record.Gems != v) { Record.Gems = v; PushRange(RosterFormat.OffGems, 2); RaiseAll(); } }
    }

    public byte Food
    {
        get => Record.Food;
        set { if (Record.Food != value) { Record.Food = value; PushByte(RosterFormat.OffFood); RaiseHex(RosterFormat.OffFood); } }
    }

    public byte Condition
    {
        get => Record.Condition;
        set { if (Record.Condition != value) { Record.Condition = value; PushByte(RosterFormat.OffCondition); RaiseHex(RosterFormat.OffCondition); OnPropertyChanged(nameof(ConditionName)); } }
    }

    public string ConditionName => Record.ConditionName;

    // --- freeze flags (applied by the main timer) -------------------------------
    private bool _freezeHp;
    public bool FreezeHp { get => _freezeHp; set => SetField(ref _freezeHp, value); }

    private bool _freezeSp;
    public bool FreezeSp { get => _freezeSp; set => SetField(ref _freezeSp, value); }

    // Pins the condition byte to 0 (OK), curing any affliction the game applies.
    private bool _freezeCondition;
    public bool FreezeCondition { get => _freezeCondition; set => SetField(ref _freezeCondition, value); }

    // --- "no-loss" ratchet freezes (gold / gems / food) -------------------------
    // Unlike HP/SP (pinned to max), these only stop the value from *dropping*. The
    // party may still find/earn more; a decrease (spell cost, purchase, theft) is
    // undone. Each tracks a high-water mark seeded from the live value when enabled;
    // toggling the flag resets it so it re-baselines on the next tick.
    private long _goldHigh = -1, _gemsHigh = -1, _foodHigh = -1;

    private bool _freezeGold;
    public bool FreezeGold { get => _freezeGold; set { if (SetField(ref _freezeGold, value)) _goldHigh = -1; } }

    private bool _freezeGems;
    public bool FreezeGems { get => _freezeGems; set { if (SetField(ref _freezeGems, value)) _gemsHigh = -1; } }

    private bool _freezeFood;
    public bool FreezeFood { get => _freezeFood; set { if (SetField(ref _freezeFood, value)) _foodHigh = -1; } }

    // Charge freezes are per inventory slot (each ItemSlotViewModel.FreezeCharges). This
    // convenience flag mirrors the toggle across all 12 slots and reflects "are they all on?".
    private bool _settingAllCharges;
    public bool FreezeAllCharges
    {
        get => Items.Count > 0 && Items.All(it => it.FreezeCharges);
        set
        {
            _settingAllCharges = true;
            foreach (var it in Items) it.FreezeCharges = value;
            _settingAllCharges = false;
            OnPropertyChanged();
        }
    }

    // --- bulk actions -----------------------------------------------------------
    public void MaxHp() { HpMax = 0xFFFF; HpCur = 0xFFFF; }
    public void MaxSp() { SpMax = 0xFFFF; SpCur = 0xFFFF; }

    public void MaxStats(byte value = 0xFF)
    {
        foreach (var s in Stats) { s.SetBoth(value); }
    }

    /// <summary>Sets every resistance to 100% (the values are percentages, so 100 is "always resists").</summary>
    public void MaxResistances(byte value = 100)
    {
        foreach (var r in Resistances) { r.SetBoth(value); }
    }

    public void MaxEverything()
    {
        MaxHp();
        MaxSp();
        MaxStats();
        MaxResistances();
        if (LevelMax < 0xFF) LevelMax = 0xFF;
        LevelCur = LevelMax;
        Experience = 9_999_999;   // huge but stays within the game's sane display range
        Gold = 0xFFFFFF;
        Gems = 0xFFFF;
        Food = 0xFF;
        Condition = 0;            // clear any affliction
    }

    public void CureCondition() => Condition = 0;

    /// <summary>Tops up current HP/SP to their maximums and clears the condition — a "heal",
    /// without touching the maximums the way the Max buttons do.</summary>
    public void Heal()
    {
        HpCur = HpMax;
        SpCur = SpMax;
        Condition = 0;
    }

    // --- live sync --------------------------------------------------------------
    /// <summary>Re-reads this record from process memory (no-op when file-only).</summary>
    public void PullFromMemory()
    {
        if (!IsLive) return;
        var buf = _mem!.Read(Record.Address, RosterFormat.RecordSize);
        if (buf.Length < RosterFormat.RecordSize) return;
        _suppressPush = true;
        Record.Load(buf);
        _suppressPush = false;
        RaiseAll();
    }

    /// <summary>Writes the entire record to memory.</summary>
    public bool PushAll()
    {
        if (!IsLive) return false;
        return _mem!.Write(Record.Address, Record.ToArray());
    }

    /// <summary>Applies any active freezes; called every timer tick (UI thread).</summary>
    public void ApplyFreezes()
    {
        if (!IsLive) return;
        ApplyClampFreeze(_freezeHp, RosterFormat.OffHpCur, RosterFormat.OffHpMax, nameof(HpCur));
        ApplyClampFreeze(_freezeSp, RosterFormat.OffSpCur, RosterFormat.OffSpMax, nameof(SpCur));

        ApplyNoLoss(_freezeGold, ref _goldHigh, RosterFormat.OffGold, 3, nameof(Gold));
        ApplyNoLoss(_freezeGems, ref _gemsHigh, RosterFormat.OffGems, 2, nameof(Gems));
        ApplyNoLoss(_freezeFood, ref _foodHigh, RosterFormat.OffFood, 1, nameof(Food));

        ApplyZeroFreeze(_freezeCondition, RosterFormat.OffCondition, nameof(Condition), nameof(ConditionName));

        foreach (var it in Items) ApplyChargeFreeze(it);
    }

    /// <summary>
    /// Pins a single byte to 0 each tick (used to keep condition at OK). Reads the live byte
    /// from memory — like the clamp freezes — so it works for every character, not just the
    /// live-refreshed one. When the game sets a non-zero value, it is rewritten back to 0.
    /// </summary>
    private void ApplyZeroFreeze(bool enabled, int off, string propName, string nameProp)
    {
        if (!enabled || _suppressPush) return;
        var cur = _mem!.Read(Record.Address + (nuint)off, 1);
        if (cur.Length < 1 || cur[0] == 0) return;   // missing or already OK
        Record.Raw[off] = 0;
        _mem!.WriteRange(Record.Address, Record.Raw, off, 1);
        OnPropertyChanged(propName);
        OnPropertyChanged(nameProp);
        RaiseHex(off);
    }

    /// <summary>
    /// Pins one item slot's charge byte to the value the user set. The buffered value is the
    /// freeze target (unlike HP/SP, charges have no "max" to read); each tick the live byte is
    /// rewritten from the buffer whenever the game has changed it. Editing the Charges field
    /// updates the buffer, so the pin simply tracks whatever value is shown.
    /// </summary>
    private void ApplyChargeFreeze(ItemSlotViewModel slot)
    {
        if (!slot.FreezeCharges || _suppressPush || !IsLive) return;
        int off = slot.ChargeOffset;
        var cur = _mem!.Read(Record.Address + (nuint)off, 1);
        if (cur.Length < 1 || cur[0] == Record.Raw[off]) return;   // missing or already pinned
        _mem!.WriteRange(Record.Address, Record.Raw, off, 1);
        slot.RefreshCharges();
    }

    /// <summary>
    /// Pins a 16-bit "current" value to its 16-bit "maximum" (HP/SP full-freeze). Both are read
    /// live from memory each tick: trusting the cached record buffer fails for any character that
    /// isn't being live-refreshed, because the buffer's current stays pinned at max and the game's
    /// in-memory drop is never seen, so the rewrite never fires.
    /// </summary>
    private void ApplyClampFreeze(bool enabled, int offCur, int offMax, string propName)
    {
        if (!enabled || _suppressPush) return;
        var cur = _mem!.Read(Record.Address + (nuint)offCur, 2);
        var max = _mem!.Read(Record.Address + (nuint)offMax, 2);
        if (cur.Length < 2 || max.Length < 2) return;

        // Keep the buffer (and thus the UI) in sync with the live max.
        Record.Raw[offMax] = max[0];
        Record.Raw[offMax + 1] = max[1];

        if (cur[0] == max[0] && cur[1] == max[1])
        {
            Record.Raw[offCur] = cur[0];
            Record.Raw[offCur + 1] = cur[1];
            return;
        }

        Record.Raw[offCur] = max[0];
        Record.Raw[offCur + 1] = max[1];
        _mem!.WriteRange(Record.Address, Record.Raw, offCur, 2);
        OnPropertyChanged(propName);
        RaiseHex(offCur);
        RaiseHex(offCur + 1);
    }

    /// <summary>
    /// One-directional ("no-loss") freeze for a little-endian field of <paramref name="len"/>
    /// bytes at <paramref name="off"/>. Reads the live value each tick: a rise becomes the new
    /// high-water mark (the party kept what it gained); a drop is rewritten back up to the mark.
    /// The record buffer and bound property are kept in sync so the UI tracks the real value.
    /// </summary>
    private void ApplyNoLoss(bool enabled, ref long high, int off, int len, string propName)
    {
        if (!enabled || _suppressPush) return;
        var buf = _mem!.Read(Record.Address + (nuint)off, len);
        if (buf.Length < len) return;

        long live = 0;
        for (int k = len - 1; k >= 0; k--) live = (live << 8) | buf[k];

        if (high < 0) high = live;          // first tick after enabling: baseline only
        else if (live > high) high = live;  // party gained — accept and remember it
        else if (live == high) return;      // steady — nothing to do
        else                                // would have dropped — restore the high value
        {
            WriteLE(Record.Raw, off, len, high);
            _mem!.WriteRange(Record.Address, Record.Raw, off, len);
            NotifyField(propName, off, len);
            return;
        }

        // Baseline/gain path: reflect the (possibly new) value in the buffer + UI.
        if (WriteLE(Record.Raw, off, len, high))
            NotifyField(propName, off, len);
    }

    /// <summary>Writes <paramref name="value"/> as little-endian into the buffer; returns true if any byte changed.</summary>
    private static bool WriteLE(byte[] buf, int off, int len, long value)
    {
        bool changed = false;
        ulong v = unchecked((ulong)value);
        for (int k = 0; k < len; k++)
        {
            byte b = (byte)(v & 0xFF);
            if (buf[off + k] != b) { buf[off + k] = b; changed = true; }
            v >>= 8;
        }
        return changed;
    }

    private void NotifyField(string propName, int off, int len)
    {
        OnPropertyChanged(propName);
        for (int k = 0; k < len; k++) RaiseHex(off + k);
    }

    // --- called by hex grid / stat rows -----------------------------------------
    public void OnRawByteEdited(int offset)
    {
        PushByte(offset);
        RaiseFriendly();      // friendly fields may have changed
    }

    internal void PushByte(int offset) => PushRange(offset, 1);

    internal void PushRange(int offset, int length)
    {
        if (_suppressPush || !IsLive) return;
        _mem!.WriteRange(Record.Address, Record.Raw, offset, length);
    }

    internal void RaiseHex(int offset)
    {
        if (offset >= 0 && offset < HexBytes.Count) HexBytes[offset].Refresh();
    }

    private void RaiseAllHex()
    {
        foreach (var h in HexBytes) h.Refresh();
    }

    private void RaiseFriendly()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(SexIndex));
        OnPropertyChanged(nameof(ClassIndex));
        OnPropertyChanged(nameof(LevelCur));
        OnPropertyChanged(nameof(LevelMax));
        OnPropertyChanged(nameof(Age));
        OnPropertyChanged(nameof(ArmorClass));
        OnPropertyChanged(nameof(SpellLevel));
        OnPropertyChanged(nameof(HpCur));
        OnPropertyChanged(nameof(HpMax));
        OnPropertyChanged(nameof(SpCur));
        OnPropertyChanged(nameof(SpMax));
        OnPropertyChanged(nameof(Experience));
        OnPropertyChanged(nameof(Gold));
        OnPropertyChanged(nameof(Gems));
        OnPropertyChanged(nameof(Food));
        OnPropertyChanged(nameof(Condition));
        OnPropertyChanged(nameof(ConditionName));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        foreach (var s in Stats) s.Refresh();
        foreach (var r in Resistances) r.Refresh();
        foreach (var it in Items) it.Refresh();
    }

    /// <summary>Refresh everything (friendly + hex).</summary>
    public void RaiseAll()
    {
        RaiseFriendly();
        RaiseAllHex();
    }

    private static ushort Clamp16(int v) => (ushort)Math.Clamp(v, 0, 0xFFFF);

    private static string LabelFor(int off)
    {
        if (off < RosterFormat.NameLength) return off == 0 ? "Name" : "";
        if (off == RosterFormat.OffSex - 1) return "(name terminator)";
        if (off == RosterFormat.OffSex) return "Sex (1=M,2=F)";
        if (off == RosterFormat.OffAlignmentOrig) return "Alignment (original)";
        if (off == RosterFormat.OffAlignment) return "Alignment (current)";
        if (off == RosterFormat.OffRace) return "Race";
        if (off == RosterFormat.OffClass) return "Class (1-6)";
        if (off >= RosterFormat.OffStats && off < RosterFormat.OffStats + RosterFormat.StatCount * 2)
        {
            int idx = (off - RosterFormat.OffStats) / 2;
            bool norm = (off - RosterFormat.OffStats) % 2 == 0;
            return $"{RosterFormat.Stats[idx]} {(norm ? "normal" : "current")}";
        }
        if (off == RosterFormat.OffLevelCur) return "Level (current)";
        if (off == RosterFormat.OffLevelMax) return "Level (base)";
        if (off == RosterFormat.OffAge) return "Age";
        if (off == RosterFormat.OffTimesRested) return "Times rested";
        if (off >= RosterFormat.OffExperience && off <= RosterFormat.OffExperience + 3) return "Experience (u32)";
        if (off == RosterFormat.OffSpCur) return "SP current (lo)";
        if (off == RosterFormat.OffSpCur + 1) return "SP current (hi)";
        if (off == RosterFormat.OffSpMax) return "SP max (lo)";
        if (off == RosterFormat.OffSpMax + 1) return "SP max (hi)";
        if (off == RosterFormat.OffSpellLevel) return "Spell level";
        if (off == RosterFormat.OffGems) return "Gems (lo)";
        if (off == RosterFormat.OffGems + 1) return "Gems (hi)";
        if (off == RosterFormat.OffHpCur) return "HP current (lo)";
        if (off == RosterFormat.OffHpCur + 1) return "HP current (hi)";
        if (off == RosterFormat.OffHpMod) return "HP modified (lo)";
        if (off == RosterFormat.OffHpMod + 1) return "HP modified (hi)";
        if (off == RosterFormat.OffHpMax) return "HP max (lo)";
        if (off == RosterFormat.OffHpMax + 1) return "HP max (hi)";
        if (off >= RosterFormat.OffGold && off <= RosterFormat.OffGold + 2) return "Gold (u24)";
        if (off == RosterFormat.OffArmorClassItems) return "AC from items";
        if (off == RosterFormat.OffArmorClass) return "Armor class (total)";
        if (off == RosterFormat.OffFood) return "Food";
        if (off == RosterFormat.OffCondition) return "Condition (0=OK)";
        if (off >= RosterFormat.OffEquipment && off < RosterFormat.OffEquipment + RosterFormat.ItemSlotCount) return $"Equipped #{off - RosterFormat.OffEquipment + 1} item";
        if (off >= RosterFormat.OffBackpack && off < RosterFormat.OffBackpack + RosterFormat.ItemSlotCount) return $"Backpack #{off - RosterFormat.OffBackpack + 1} item";
        if (off >= RosterFormat.OffEquipmentCharges && off < RosterFormat.OffEquipmentCharges + RosterFormat.ItemSlotCount) return $"Equipped #{off - RosterFormat.OffEquipmentCharges + 1} charges";
        if (off >= RosterFormat.OffBackpackCharges && off < RosterFormat.OffBackpackCharges + RosterFormat.ItemSlotCount) return $"Backpack #{off - RosterFormat.OffBackpackCharges + 1} charges";
        if (off >= RosterFormat.OffResistances && off < RosterFormat.OffResistances + RosterFormat.ResistanceCount * 2)
        {
            int idx = (off - RosterFormat.OffResistances) / 2;
            bool norm = (off - RosterFormat.OffResistances) % 2 == 0;
            return $"{RosterFormat.Resistances[idx]} resistance {(norm ? "normal" : "current")} (%)";
        }
        if (off == RosterFormat.OffSlotIndex) return "Slot index";
        return "";
    }
}
