using System.Collections.ObjectModel;
using BardsTale1Trainer.Game;
using BardsTale1Trainer.Memory;

namespace BardsTale1Trainer.ViewModels;

/// <summary>
/// Editable view of one Bard's Tale character (party slot). Friendly properties and the
/// raw hex grid both write into the same <see cref="CharacterRecord"/> buffer; whenever a
/// byte changes it is pushed to the live process (if attached).
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
    public ObservableCollection<SpellLevelViewModel> SpellLevels { get; } = new();

    /// <summary>The 8 inventory slots (item word: id + equipped flag).</summary>
    public ObservableCollection<ItemSlotViewModel> Items { get; } = new();

    public CharacterViewModel(CharacterRecord record, ProcessMemory? mem)
    {
        Record = record;
        _mem = mem;

        for (int off = 0; off < PartyFormat.RecordSize; off++)
            HexBytes.Add(new HexByteViewModel(this, off, LabelFor(off)));

        for (int i = 0; i < PartyFormat.StatCount; i++)
            Stats.Add(new StatViewModel(this, i, PartyFormat.Stats[i]));

        for (int i = 0; i < PartyFormat.SpellClassCount; i++)
            SpellLevels.Add(new SpellLevelViewModel(this, i, PartyFormat.SpellClasses[i]));

        for (int i = 0; i < PartyFormat.ItemSlotCount; i++)
            Items.Add(new ItemSlotViewModel(this, i, $"Slot {i + 1}"));
    }

    // --- selection list display -------------------------------------------------
    public string Title => $"{Record.Slot + 1}. {(string.IsNullOrEmpty(Record.Name) ? "(empty)" : Record.Name)}";
    public string Subtitle => $"L{Record.Level} {Record.RaceName} {Record.ClassName}  ·  HP {Record.HpCur}/{Record.HpMax}";

    // --- friendly fields --------------------------------------------------------
    public string Name
    {
        get => Record.Name;
        // The name lives in the game's roster table, not the record; expose it for display
        // and let the live writer (NameAddress) push it. Editing here updates the buffer name
        // and pushes the row text when attached.
        set { if (Record.Name != value) { Record.Name = value ?? ""; PushName(); RaiseAll(); } }
    }

    /// <summary>Absolute address of this slot's name row in the game's roster table (0 if offline).</summary>
    public nuint NameAddress { get; set; }

    public int ClassIndex
    {
        get => Record.Class;
        set { if (Record.Class != value && value >= 0) { Record.Class = (ushort)value; PushRange(PartyFormat.OffClass, 2); RaiseAll(); } }
    }

    public int RaceIndex
    {
        get => Record.Race;
        set { if (Record.Race != value && value >= 0) { Record.Race = (byte)value; PushByte(PartyFormat.OffRace); RaiseAll(); } }
    }

    public int ArmorClass
    {
        get => Record.ArmorClass;
        set { var v = (short)Math.Clamp(value, short.MinValue, short.MaxValue); if (Record.ArmorClass != v) { Record.ArmorClass = v; PushRange(PartyFormat.OffArmorClass, 2); RaiseAll(); } }
    }

    public int HpCur
    {
        get => Record.HpCur;
        set { var v = Clamp16(value); if (Record.HpCur != v) { Record.HpCur = v; PushRange(PartyFormat.OffHpCur, 2); RaiseAll(); } }
    }

    public int HpMax
    {
        get => Record.HpMax;
        set { var v = Clamp16(value); if (Record.HpMax != v) { Record.HpMax = v; PushRange(PartyFormat.OffHpMax, 2); RaiseAll(); } }
    }

    public int SpCur
    {
        get => Record.SpCur;
        set { var v = Clamp16(value); if (Record.SpCur != v) { Record.SpCur = v; PushRange(PartyFormat.OffSpCur, 2); RaiseAll(); } }
    }

    public int SpMax
    {
        get => Record.SpMax;
        set { var v = Clamp16(value); if (Record.SpMax != v) { Record.SpMax = v; PushRange(PartyFormat.OffSpMax, 2); RaiseAll(); } }
    }

    public long Experience
    {
        get => Record.Experience;
        set { var v = (uint)Math.Clamp(value, 0, uint.MaxValue); if (Record.Experience != v) { Record.Experience = v; PushRange(PartyFormat.OffExperience, 4); RaiseAll(); } }
    }

    public long Gold
    {
        get => Record.Gold;
        set { var v = (uint)Math.Clamp(value, 0, uint.MaxValue); if (Record.Gold != v) { Record.Gold = v; PushRange(PartyFormat.OffGold, 4); RaiseAll(); } }
    }

    public int Level
    {
        get => Record.Level;
        set { var v = Clamp16(value); if (Record.Level != v) { Record.Level = v; PushRange(PartyFormat.OffLevel, 2); RaiseAll(); } }
    }

    public string SongsLine => "Bard songs: " + string.Join(", ", Spellbook.BardSongs);

    // --- freeze flags (applied by the main timer) -------------------------------
    private bool _freezeHp;
    public bool FreezeHp { get => _freezeHp; set => SetField(ref _freezeHp, value); }

    private bool _freezeSp;
    public bool FreezeSp { get => _freezeSp; set => SetField(ref _freezeSp, value); }

    // No-loss ratchets: the party may still gain gold/experience, but never lose any.
    private long _goldHigh = -1, _expHigh = -1;

    private bool _freezeGold;
    public bool FreezeGold { get => _freezeGold; set { if (SetField(ref _freezeGold, value)) _goldHigh = -1; } }

    private bool _freezeExp;
    public bool FreezeExp { get => _freezeExp; set { if (SetField(ref _freezeExp, value)) _expHigh = -1; } }

    // --- bulk actions -----------------------------------------------------------
    public void MaxHp() { HpMax = 0xFFFF; HpCur = 0xFFFF; }
    public void MaxSp() { SpMax = 0xFFFF; SpCur = 0xFFFF; }

    public void MaxStats(int value = 30)
    {
        foreach (var s in Stats) s.SetBoth(value);
    }

    /// <summary>Tops up current HP/SP to their maximums — a "heal" without touching the
    /// maximums the way the Max buttons do. (The status word at 0x01 is left alone: its
    /// values beyond 0=occupied / 1=empty aren't decoded, so rewriting it could corrupt a
    /// slot rather than cure it.)</summary>
    public void Heal()
    {
        HpCur = HpMax;
        SpCur = SpMax;
    }

    public void MaxEverything()
    {
        MaxHp();
        MaxSp();
        MaxStats();
        if (Level < 0xFFFF) Level = Math.Max(Level, 50);
        Experience = 4_000_000_000;     // huge but within u32
        Gold = 4_000_000_000;
        ArmorClass = -10;               // best possible AC (shows as "LO")
        // Max out whichever art this class casts.
        int sp = PartyFormat.SpellLevelIndexForClass(Record.Class);
        if (sp >= 0) SpellLevels[sp].Level = 7;
    }

    // --- live sync --------------------------------------------------------------
    /// <summary>Re-reads this record from process memory (no-op when file-only).</summary>
    public void PullFromMemory()
    {
        if (!IsLive) return;
        var buf = _mem!.Read(Record.Address, PartyFormat.RecordSize);
        if (buf.Length < PartyFormat.RecordSize) return;
        _suppressPush = true;
        Record.Load(buf);
        // Refresh the name from the roster row too (it can change in-game).
        if (NameAddress != 0)
        {
            var nameBuf = _mem!.Read(NameAddress, PartyFormat.PartyRowNameLength);
            if (nameBuf.Length == PartyFormat.PartyRowNameLength)
                Record.Name = CharacterRecord.DecodeRosterName(nameBuf);
        }
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
        ApplyClampFreeze(_freezeHp, PartyFormat.OffHpCur, PartyFormat.OffHpMax, nameof(HpCur));
        ApplyClampFreeze(_freezeSp, PartyFormat.OffSpCur, PartyFormat.OffSpMax, nameof(SpCur));
        ApplyNoLoss(_freezeGold, ref _goldHigh, PartyFormat.OffGold, 4, nameof(Gold));
        ApplyNoLoss(_freezeExp, ref _expHigh, PartyFormat.OffExperience, 4, nameof(Experience));
    }

    /// <summary>
    /// Pins a 16-bit "current" value to its 16-bit "maximum" (HP/SP full-freeze). Both read
    /// live from memory each tick so it works for every character, not just the selected one.
    /// </summary>
    private void ApplyClampFreeze(bool enabled, int offCur, int offMax, string propName)
    {
        if (!enabled || _suppressPush) return;
        var cur = _mem!.Read(Record.Address + (nuint)offCur, 2);
        var max = _mem!.Read(Record.Address + (nuint)offMax, 2);
        if (cur.Length < 2 || max.Length < 2) return;

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
    /// bytes: a rise becomes the new high-water mark; a drop is rewritten back up to it.
    /// </summary>
    private void ApplyNoLoss(bool enabled, ref long high, int off, int len, string propName)
    {
        if (!enabled || _suppressPush) return;
        var buf = _mem!.Read(Record.Address + (nuint)off, len);
        if (buf.Length < len) return;

        long live = 0;
        for (int k = len - 1; k >= 0; k--) live = (live << 8) | buf[k];

        if (high < 0) high = live;
        else if (live > high) high = live;
        else if (live == high) return;
        else
        {
            WriteLE(Record.Raw, off, len, high);
            _mem!.WriteRange(Record.Address, Record.Raw, off, len);
            NotifyField(propName, off, len);
            return;
        }

        if (WriteLE(Record.Raw, off, len, high))
            NotifyField(propName, off, len);
    }

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

    // --- name (lives in the roster row, not the record) -------------------------
    private void PushName()
    {
        if (_suppressPush || _mem == null || NameAddress == 0) return;
        var row = new byte[PartyFormat.PartyRowNameLength];
        var bytes = System.Text.Encoding.ASCII.GetBytes(Record.Name ?? "");
        for (int i = 0; i < row.Length; i++) row[i] = i < bytes.Length ? bytes[i] : (byte)0;
        _mem.Write(NameAddress, row);
    }

    // --- called by hex grid / sub-rows ------------------------------------------
    public void OnRawByteEdited(int offset)
    {
        PushByte(offset);
        RaiseFriendly();
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
        OnPropertyChanged(nameof(ClassIndex));
        OnPropertyChanged(nameof(RaceIndex));
        OnPropertyChanged(nameof(ArmorClass));
        OnPropertyChanged(nameof(HpCur));
        OnPropertyChanged(nameof(HpMax));
        OnPropertyChanged(nameof(SpCur));
        OnPropertyChanged(nameof(SpMax));
        OnPropertyChanged(nameof(Experience));
        OnPropertyChanged(nameof(Gold));
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        foreach (var s in Stats) s.Refresh();
        foreach (var sl in SpellLevels) sl.Refresh();
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
        if (off == PartyFormat.OffDiskMarker) return "Disk marker (1=file,0=live)";
        if (off == PartyFormat.OffStatus) return "Status (0=OK) lo";
        if (off == PartyFormat.OffStatus + 1) return "Status hi";
        if (off == PartyFormat.OffClass) return "Class (lo)";
        if (off == PartyFormat.OffClass + 1) return "Class (hi)";
        if (off >= PartyFormat.OffStatsMax && off < PartyFormat.OffStatsMax + PartyFormat.StatCount * 2)
        {
            int idx = (off - PartyFormat.OffStatsMax) / 2;
            return $"{PartyFormat.Stats[idx]} max ({((off - PartyFormat.OffStatsMax) % 2 == 0 ? "lo" : "hi")})";
        }
        if (off >= PartyFormat.OffStatsCur && off < PartyFormat.OffStatsCur + PartyFormat.StatCount * 2)
        {
            int idx = (off - PartyFormat.OffStatsCur) / 2;
            return $"{PartyFormat.Stats[idx]} cur ({((off - PartyFormat.OffStatsCur) % 2 == 0 ? "lo" : "hi")})";
        }
        if (off == PartyFormat.OffArmorClass) return "Armor class (lo)";
        if (off == PartyFormat.OffArmorClass + 1) return "Armor class (hi)";
        if (off == PartyFormat.OffHpCur) return "HP current (lo)";
        if (off == PartyFormat.OffHpCur + 1) return "HP current (hi)";
        if (off == PartyFormat.OffHpMax) return "HP max (lo)";
        if (off == PartyFormat.OffHpMax + 1) return "HP max (hi)";
        if (off == PartyFormat.OffSpCur) return "SP current (lo)";
        if (off == PartyFormat.OffSpCur + 1) return "SP current (hi)";
        if (off == PartyFormat.OffSpMax) return "SP max (lo)";
        if (off == PartyFormat.OffSpMax + 1) return "SP max (hi)";
        if (off >= PartyFormat.OffItems && off < PartyFormat.OffItems + PartyFormat.ItemSlotCount * 2)
        {
            int idx = (off - PartyFormat.OffItems) / 2;
            return $"Item slot {idx + 1} ({((off - PartyFormat.OffItems) % 2 == 0 ? "id" : "flags")})";
        }
        if (off >= PartyFormat.OffExperience && off <= PartyFormat.OffExperience + 3) return "Experience (u32)";
        if (off >= PartyFormat.OffGold && off <= PartyFormat.OffGold + 3) return "Gold (u32)";
        if (off == PartyFormat.OffLevel) return "Level (lo)";
        if (off == PartyFormat.OffLevel + 1) return "Level (hi)";
        if (off == PartyFormat.OffLevelMax) return "Level max (lo)";
        if (off == PartyFormat.OffLevelMax + 1) return "Level max (hi)";
        if (off >= PartyFormat.OffSpellLevels && off < PartyFormat.OffSpellLevels + PartyFormat.SpellClassCount)
            return $"{PartyFormat.SpellClasses[off - PartyFormat.OffSpellLevels]} spell level";
        if (off == PartyFormat.OffRace) return "Race";
        return "";
    }
}
