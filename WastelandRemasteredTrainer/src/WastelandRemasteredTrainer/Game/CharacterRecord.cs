using System.Text;
using WastelandRemasteredTrainer.Memory;

namespace WastelandRemasteredTrainer.Game;

/// <summary>
/// A packed skill entry: id and level, read from or written to the managed byte[] at
/// <c>Player.SKILLS</c>. The array holds up to 30 such pairs, terminated by a 0x00 id.
/// </summary>
public readonly record struct SkillEntry(int Slot, int Id, int Level)
{
    public string Name => SkillBook.SkillName(Id);
}

/// <summary>
/// A packed inventory entry: item id and quantity byte, read from or written to the managed
/// byte[] at <c>Player.ITEMS</c>. The array holds up to 30 such pairs.
///
/// <para>The quantity byte is not a plain count: bit 7 flags a jammed weapon and the low seven
/// bits are the ammo/charge count. <see cref="Ammo"/> reports the count with the flag removed,
/// so a jammed rifle holding 20 rounds reads as 20 rather than 148.</para>
/// </summary>
public readonly record struct ItemEntry(int Slot, int Id, int Quantity)
{
    public string Name => ItemBook.ItemName(Id);

    /// <summary>Ammo/charge count with the jam bit masked off.</summary>
    public int Ammo => CharacterFormat.AmmoOf(Quantity);

    /// <summary>True when the jammed-weapon bit is set.</summary>
    public bool Jammed => CharacterFormat.IsJammed(Quantity);
}

/// <summary>
/// A typed live view over one <c>Player</c> object in the game process. Unlike the DOS
/// trainers (which keep a local byte buffer and sync it), this record reads and writes
/// straight through to the object's address, so every property is the game's current value.
///
/// <para>Reads come in two flavours. The plain properties return 0 when a read fails, which is
/// fine for display; the <c>TryGet*</c> forms report the failure and must be used by anything
/// that feeds a value back into a write. A freeze that cannot tell "money is 0" from "the page
/// was briefly unreadable" will happily pin a character's cash to zero.</para>
/// </summary>
public sealed class CharacterRecord
{
    private readonly IMemorySource _mem;

    /// <summary>Absolute address of the IL2CPP Player object in the target process.</summary>
    public nuint Address { get; }

    /// <summary>Index in <c>Party.players</c>.</summary>
    public int Slot { get; }

    public CharacterRecord(IMemorySource mem, nuint address, int slot)
    {
        _mem = mem;
        Address = address;
        Slot = slot;
    }

    // --- primitive accessors ----------------------------------------------------
    public int ReadI32(int offset) => _mem.ReadI32(Address + (nuint)offset);
    public bool WriteI32(int offset, int value) => _mem.WriteI32(Address + (nuint)offset, value);
    public byte ReadByte(int offset) => _mem.ReadByte(Address + (nuint)offset);
    public bool WriteByte(int offset, byte value) => _mem.WriteByte(Address + (nuint)offset, value);

    public bool TryReadI32(int offset, out int value) => _mem.TryReadI32(Address + (nuint)offset, out value);
    public bool TryReadByte(int offset, out byte value) => _mem.TryReadByte(Address + (nuint)offset, out value);

    /// <summary>True when the object still reads back as a live, plausible character record.</summary>
    public bool IsReadable
    {
        get
        {
            var buf = new byte[CharacterFormat.ProbeSize];
            return _mem.Read(Address, buf, buf.Length) == buf.Length;
        }
    }

    // --- typed field access -----------------------------------------------------
    // Each editable field has a plain property (convenient, discards the result) and a TrySet*
    // form that reports whether the write actually landed. Anything that must know -- the view
    // model's Write(), which keeps an edit pending until it succeeds -- uses the TrySet* form.

    // --- identity ---------------------------------------------------------------
    /// <summary>Display name, read fresh from the managed string each time.</summary>
    public string Name => _mem.ReadManagedString(_mem.ReadPtr(Address + (nuint)CharacterFormat.OffName));

    /// <summary>The character name bytes from the CName byte[] (ASCII, same as original).</summary>
    public string CName => ReadAsciiByteArray(CharacterFormat.OffCName);

    /// <summary>The rank string read from the RANK byte[] (ASCII, same as original).</summary>
    public string Rank => ReadAsciiByteArray(CharacterFormat.OffRank);

    private string ReadAsciiByteArray(int offset)
    {
        nuint arr = _mem.ReadPtr(Address + (nuint)offset);
        if (arr == 0) return "";
        int len = _mem.ReadArrayLength(arr);
        if (len <= 0 || len > 64) return "";
        var bytes = _mem.ReadByteArray(arr, len);
        int end = Array.IndexOf(bytes, (byte)0);
        if (end >= 0) bytes = bytes[..end];
        return Encoding.ASCII.GetString(bytes);
    }

    public int Sex
    {
        get => ReadByte(CharacterFormat.OffSex);
        set => TrySetSex(value);
    }

    public bool TrySetSex(int value) =>
        WriteByte(CharacterFormat.OffSex, (byte)Math.Clamp(value, 0, CharacterFormat.Genders.Length - 1));

    public int Nationality
    {
        get => ReadByte(CharacterFormat.OffNationality);
        set => TrySetNationality(value);
    }

    public bool TrySetNationality(int value) =>
        WriteByte(CharacterFormat.OffNationality,
            (byte)Math.Clamp(value, 0, CharacterFormat.Nationalities.Length - 1));

    public int AC
    {
        get => ReadByte(CharacterFormat.OffAC);
        set => TrySetAC(value);
    }

    public bool TrySetAC(int value) =>
        WriteByte(CharacterFormat.OffAC, (byte)Math.Clamp(value, 0, byte.MaxValue));

    // --- vitals -----------------------------------------------------------------
    public int MaxCon
    {
        get => ReadI32(CharacterFormat.OffMaxCon);
        set => TrySetMaxCon(value);
    }

    public bool TrySetMaxCon(int value) =>
        WriteI32(CharacterFormat.OffMaxCon, Math.Clamp(value, 1, GameFacts.MaxCon));

    public bool TryGetMaxCon(out int value) => TryReadI32(CharacterFormat.OffMaxCon, out value);

    public int CurCon
    {
        get => ReadI32(CharacterFormat.OffCurCon);
        set => TrySetCurCon(value);
    }

    public bool TrySetCurCon(int value) =>
        WriteI32(CharacterFormat.OffCurCon, Math.Clamp(value, 0, GameFacts.MaxCon));

    public int UncCon
    {
        get => ReadI32(CharacterFormat.OffUncCon);
        set => TrySetUncCon(value);
    }

    public bool TrySetUncCon(int value) =>
        WriteI32(CharacterFormat.OffUncCon, Math.Clamp(value, 0, GameFacts.MaxCon));

    // --- progression ------------------------------------------------------------
    public int Money
    {
        get => ReadI32(CharacterFormat.OffMoney);
        set => TrySetMoney(value);
    }

    public bool TrySetMoney(int value) =>
        WriteI32(CharacterFormat.OffMoney, Math.Clamp(value, 0, GameFacts.MaxMoney));

    public bool TryGetMoney(out int value) => TryReadI32(CharacterFormat.OffMoney, out value);

    public int Experience
    {
        get => ReadI32(CharacterFormat.OffExperience);
        set => TrySetExperience(value);
    }

    public bool TrySetExperience(int value) =>
        WriteI32(CharacterFormat.OffExperience, Math.Clamp(value, 0, GameFacts.MaxExperience));

    public int Level
    {
        get => ReadByte(CharacterFormat.OffLevel);
        set => TrySetLevel(value);
    }

    public bool TrySetLevel(int value) =>
        WriteByte(CharacterFormat.OffLevel, (byte)Math.Clamp(value, 1, GameFacts.MaxLevel));

    public int SkillPoints
    {
        get => ReadByte(CharacterFormat.OffSkillPoints);
        set => TrySetSkillPoints(value);
    }

    public bool TrySetSkillPoints(int value) =>
        WriteByte(CharacterFormat.OffSkillPoints, (byte)Math.Clamp(value, 0, GameFacts.MaxSkillPoints));

    public int Weapon
    {
        get => ReadByte(CharacterFormat.OffWeapon);
        set => TrySetWeapon(value);
    }

    public bool TrySetWeapon(int value) =>
        WriteByte(CharacterFormat.OffWeapon, (byte)Math.Clamp(value, 0, byte.MaxValue));

    public int Armor
    {
        get => ReadByte(CharacterFormat.OffArmor);
        set => TrySetArmor(value);
    }

    public bool TrySetArmor(int value) =>
        WriteByte(CharacterFormat.OffArmor, (byte)Math.Clamp(value, 0, byte.MaxValue));

    /// <summary>Disease/condition byte. Clamped, so an out-of-range value cannot wrap.</summary>
    public int Disease
    {
        get => ReadByte(CharacterFormat.OffDisease);
        set => TrySetDisease(value);
    }

    public bool TrySetDisease(int value) =>
        WriteByte(CharacterFormat.OffDisease, (byte)Math.Clamp(value, 0, byte.MaxValue));

    // --- attributes -------------------------------------------------------------
    /// <summary>Attribute by index: 0 = STR, 1 = IQ, 2 = LCK, 3 = SPD, 4 = AGL, 5 = DEX, 6 = CHR.</summary>
    public int GetAttribute(int index) =>
        InRange(index) ? ReadByte(CharacterFormat.OffStrength + index) : 0;

    public bool SetAttribute(int index, int value)
    {
        if (!InRange(index)) return false;
        return WriteByte(CharacterFormat.OffStrength + index,
            (byte)Math.Clamp(value, GameFacts.MinAttribute, GameFacts.MaxAttribute));
    }

    private static bool InRange(int index) => index >= 0 && index < CharacterFormat.AttributeCount;

    // --- packed arrays ----------------------------------------------------------
    /// <summary>The managed byte[] holding packed (skillId, level) pairs.</summary>
    private nuint SkillsArray => _mem.ReadPtr(Address + (nuint)CharacterFormat.OffSkills);

    /// <summary>The managed byte[] holding packed (itemId, quantity) pairs.</summary>
    private nuint ItemsArray => _mem.ReadPtr(Address + (nuint)CharacterFormat.OffItems);

    /// <summary>
    /// How many (id, value) slots a packed array actually holds. Derived from the array's own
    /// length and clamped to the format's slot count, so an array that is shorter or longer
    /// than expected is still usable — reads and writes agree on the same bound instead of
    /// reads accepting a size that writes reject.
    /// </summary>
    private int SlotsIn(nuint array, int formatSlots)
    {
        if (array == 0) return 0;
        int len = _mem.ReadArrayLength(array);
        if (len < CharacterFormat.SlotSize) return 0;
        return Math.Min(len / CharacterFormat.SlotSize, formatSlots);
    }

    /// <summary>Reads all skills from the packed byte array.</summary>
    public List<SkillEntry> ReadSkills()
    {
        var result = new List<SkillEntry>();
        nuint arr = SkillsArray;
        int slots = SlotsIn(arr, GameFacts.SkillSlots);

        for (int slot = 0; slot < slots; slot++)
        {
            int at = slot * CharacterFormat.SlotSize;
            int id = _mem.ReadByteArrayElement(arr, at);
            if (id == 0) break;                                  // 0x00 id terminates the list
            int level = _mem.ReadByteArrayElement(arr, at + 1);
            result.Add(new SkillEntry(slot, id, level));
        }
        return result;
    }

    /// <summary>Sets a skill's level in the packed byte array, or adds it if not present.</summary>
    public bool SetSkill(int skillId, int level)
    {
        if (skillId <= 0 || skillId > byte.MaxValue) return false;
        if (level < 0 || level > GameFacts.MaxSkillLevel) return false;

        nuint arr = SkillsArray;
        int slots = SlotsIn(arr, GameFacts.SkillSlots);

        for (int slot = 0; slot < slots; slot++)
        {
            int at = slot * CharacterFormat.SlotSize;
            int id = _mem.ReadByteArrayElement(arr, at);

            if (id == skillId)
                return _mem.WriteByteArrayElement(arr, at + 1, (byte)level);

            if (id == 0)
            {
                if (!_mem.WriteByteArrayElement(arr, at, (byte)skillId)) return false;
                return _mem.WriteByteArrayElement(arr, at + 1, (byte)level);
            }
        }
        return false;   // no free slot
    }

    /// <summary>How many packed skill slots are free for a new skill.</summary>
    public int FreeSkillSlots()
    {
        nuint arr = SkillsArray;
        int slots = SlotsIn(arr, GameFacts.SkillSlots);
        int used = 0;
        for (; used < slots; used++)
        {
            if (_mem.ReadByteArrayElement(arr, used * CharacterFormat.SlotSize) == 0) break;
        }
        return slots - used;
    }

    /// <summary>Reads all inventory items from the packed byte array.</summary>
    public List<ItemEntry> ReadItems()
    {
        var result = new List<ItemEntry>();
        nuint arr = ItemsArray;
        int slots = SlotsIn(arr, GameFacts.ItemSlots);

        for (int slot = 0; slot < slots; slot++)
        {
            int at = slot * CharacterFormat.SlotSize;
            int id = _mem.ReadByteArrayElement(arr, at);
            if (id == 0) break;
            int quantity = _mem.ReadByteArrayElement(arr, at + 1);
            result.Add(new ItemEntry(slot, id, quantity));
        }
        return result;
    }

    /// <summary>
    /// Sets an inventory slot's item and ammo count. The ammo value is masked to the seven-bit
    /// count field, so a large number can never set the jammed-weapon bit by accident.
    /// </summary>
    public bool SetItem(int slot, int itemId, int ammo, bool jammed = false)
    {
        nuint arr = ItemsArray;
        int slots = SlotsIn(arr, GameFacts.ItemSlots);
        if (slot < 0 || slot >= slots) return false;
        if (itemId < 0 || itemId > byte.MaxValue) return false;

        int at = slot * CharacterFormat.SlotSize;
        if (!_mem.WriteByteArrayElement(arr, at, (byte)itemId)) return false;
        return _mem.WriteByteArrayElement(arr, at + 1,
            CharacterFormat.PackQuantity(Math.Clamp(ammo, 0, CharacterFormat.InventoryCountMask), jammed));
    }

    /// <summary>Adds an item to the first empty inventory slot. False when the pack is full.</summary>
    public bool AddItem(int itemId, int ammo)
    {
        if (itemId <= 0 || itemId > byte.MaxValue) return false;

        nuint arr = ItemsArray;
        int slots = SlotsIn(arr, GameFacts.ItemSlots);

        for (int slot = 0; slot < slots; slot++)
        {
            if (_mem.ReadByteArrayElement(arr, slot * CharacterFormat.SlotSize) == 0)
                return SetItem(slot, itemId, ammo);
        }
        return false;
    }

    /// <summary>
    /// Clears an inventory slot and closes the gap, so the 0x00 terminator stays meaningful and
    /// the game does not see a hole in the middle of the pack.
    /// </summary>
    public bool RemoveItem(int slot)
    {
        nuint arr = ItemsArray;
        int slots = SlotsIn(arr, GameFacts.ItemSlots);
        if (slot < 0 || slot >= slots) return false;

        for (int i = slot; i < slots - 1; i++)
        {
            int from = (i + 1) * CharacterFormat.SlotSize;
            int to = i * CharacterFormat.SlotSize;
            byte id = _mem.ReadByteArrayElement(arr, from);
            byte qty = _mem.ReadByteArrayElement(arr, from + 1);
            if (!_mem.WriteByteArrayElement(arr, to, id)) return false;
            if (!_mem.WriteByteArrayElement(arr, to + 1, qty)) return false;
            if (id == 0) return true;
        }

        int last = (slots - 1) * CharacterFormat.SlotSize;
        return _mem.WriteByteArrayElement(arr, last, 0)
            && _mem.WriteByteArrayElement(arr, last + 1, 0);
    }

    /// <summary>
    /// Tops up every ammo-bearing item to max ammo, clearing jammed flags. A slot that fails to
    /// write does not abandon the rest — the goal is to top up as much as possible.
    /// </summary>
    public int MaxAmmo()
    {
        nuint arr = ItemsArray;
        int slots = SlotsIn(arr, GameFacts.ItemSlots);
        int changed = 0;

        for (int slot = 0; slot < slots; slot++)
        {
            int at = slot * CharacterFormat.SlotSize;
            int id = _mem.ReadByteArrayElement(arr, at);
            if (id == 0) break;
            if (!ItemBook.IsAmmoItem(id)) continue;

            int quantity = _mem.ReadByteArrayElement(arr, at + 1);
            byte topped = CharacterFormat.PackQuantity(GameFacts.MaxAmmo, jammed: false);
            if (quantity == topped) continue;

            if (_mem.WriteByteArrayElement(arr, at + 1, topped)) changed++;
        }
        return changed;
    }

    /// <summary>Clears the jammed-weapon bit on every inventory slot that carries it.</summary>
    public int ClearJams()
    {
        nuint arr = ItemsArray;
        int slots = SlotsIn(arr, GameFacts.ItemSlots);
        int changed = 0;

        for (int slot = 0; slot < slots; slot++)
        {
            int at = slot * CharacterFormat.SlotSize;
            int id = _mem.ReadByteArrayElement(arr, at);
            if (id == 0) break;

            int quantity = _mem.ReadByteArrayElement(arr, at + 1);
            if (!CharacterFormat.IsJammed(quantity)) continue;
            if (_mem.WriteByteArrayElement(arr, at + 1, CharacterFormat.PackQuantity(quantity, jammed: false)))
                changed++;
        }
        return changed;
    }

    // --- NPC flags --------------------------------------------------------------
    public bool IsNPC => ReadByte(CharacterFormat.OffNPC) != 0;

    // --- quick actions ----------------------------------------------------------
    /// <summary>Full heal: set CurCon to MaxCon.</summary>
    public bool FullHeal()
    {
        if (!TryGetMaxCon(out int max) || max <= 0) return false;
        return CurCon != max && WriteI32(CharacterFormat.OffCurCon, max);
    }

    /// <summary>Max all seven attributes to the configured ceiling.</summary>
    public bool MaxAttributes()
    {
        bool ok = false;
        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
        {
            if (GetAttribute(i) < GameFacts.MaxAttribute && SetAttribute(i, GameFacts.MaxAttribute))
                ok = true;
        }
        return ok;
    }

    /// <summary>Max skill points to the configured ceiling.</summary>
    public bool MaxSkillPoints()
    {
        if (SkillPoints >= GameFacts.MaxSkillPoints) return false;
        SkillPoints = GameFacts.MaxSkillPoints;
        return true;
    }

    /// <summary>Max money to the configured ceiling.</summary>
    public bool MaxMoney()
    {
        if (Money >= GameFacts.MaxMoney) return false;
        Money = GameFacts.MaxMoney;
        return true;
    }

    /// <summary>Max every skill the character already has to level 10.</summary>
    public int MaxSkills()
    {
        int changed = 0;
        foreach (var s in ReadSkills())
        {
            if (s.Level < GameFacts.MaxSkillLevel && SetSkill(s.Id, GameFacts.MaxSkillLevel))
                changed++;
        }
        return changed;
    }

    /// <summary>
    /// Result of a "learn every skill" attempt: how many were added, and which ones did not fit.
    /// </summary>
    public readonly record struct LearnResult(int Learned, IReadOnlyList<string> NotLearned)
    {
        public bool Complete => NotLearned.Count == 0;
    }

    /// <summary>
    /// Adds every skill the character does not already have, at the given level.
    ///
    /// <para>The packed array holds 30 slots but the game has 35 skills, so a full set simply
    /// does not fit. Skills are added in id order until the slots run out, and the ones that
    /// did not fit are reported rather than silently dropped.</para>
    /// </summary>
    public LearnResult LearnAllSkills(int level)
    {
        level = Math.Clamp(level, 1, GameFacts.MaxSkillLevel);

        var have = ReadSkills().Select(s => s.Id).ToHashSet();
        int free = FreeSkillSlots();
        int learned = 0;
        var missed = new List<string>();

        foreach (var skill in SkillBook.Skills.OrderBy(s => s.Id))
        {
            if (have.Contains(skill.Id)) continue;

            if (free <= 0 || !SetSkill(skill.Id, level))
            {
                missed.Add(skill.Name);
                continue;
            }

            free--;
            learned++;
        }

        return new LearnResult(learned, missed);
    }

    /// <summary>Everything at once: heal, max attributes, max skills, max money, max ammo, max skill points.</summary>
    public void MaxEverything()
    {
        FullHeal();
        MaxAttributes();
        MaxSkills();
        MaxMoney();
        MaxAmmo();
        ClearJams();
        MaxSkillPoints();
    }
}
