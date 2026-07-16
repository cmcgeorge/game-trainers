namespace WastelandTrainer.Game;

/// <summary>A single packed (id, value) entry from the skill or inventory list.</summary>
public readonly record struct PackedEntry(int Slot, int Id, int Value);

/// <summary>
/// A typed, mutable view over a <see cref="CharacterFormat.RecordSize"/>-byte Wasteland character
/// record. The backing <see cref="Bytes"/> array can come from a file, a memory dump, or live
/// process memory; edits mutate the buffer in place so the caller can write it back with a
/// read-validate-write poke. Skills and inventory are variable-length packed 2-byte arrays.
/// </summary>
public sealed class CharacterRecord
{
    public byte[] Bytes { get; }

    public CharacterRecord(byte[] buffer, int offset = 0)
    {
        Bytes = new byte[CharacterFormat.RecordSize];
        int n = Math.Min(CharacterFormat.RecordSize, buffer.Length - offset);
        if (n > 0) Array.Copy(buffer, offset, Bytes, 0, n);
    }

    // --- primitive accessors -------------------------------------------------
    private byte U8(int o) => Bytes[o];
    private void U8(int o, int v) => Bytes[o] = (byte)Math.Clamp(v, 0, 255);

    private int U16(int o) => Bytes[o] | (Bytes[o + 1] << 8);
    private void U16(int o, int v)
    {
        v = Math.Clamp(v, 0, 0xFFFF);
        Bytes[o] = (byte)(v & 0xFF);
        Bytes[o + 1] = (byte)((v >> 8) & 0xFF);
    }

    private long U24(int o) => Bytes[o] | (Bytes[o + 1] << 8) | (Bytes[o + 2] << 16);
    private void U24(int o, long v)
    {
        uint u = (uint)Math.Clamp(v, 0, 0xFFFFFF);
        Bytes[o] = (byte)(u & 0xFF);
        Bytes[o + 1] = (byte)((u >> 8) & 0xFF);
        Bytes[o + 2] = (byte)((u >> 16) & 0xFF);
    }

    // --- name / rank ---------------------------------------------------------
    public string Name
    {
        get => WastelandText.Decode(Bytes, CharacterFormat.OffName, CharacterFormat.NameLength);
        set => WastelandText.Encode(Bytes, CharacterFormat.OffName, CharacterFormat.NameLength, value);
    }

    public string Rank
    {
        get => WastelandText.Decode(Bytes, CharacterFormat.OffRank, CharacterFormat.RankLength);
        set => WastelandText.Encode(Bytes, CharacterFormat.OffRank, CharacterFormat.RankLength, value);
    }

    // --- attributes ----------------------------------------------------------
    public int GetAttribute(int index) => U8(CharacterFormat.OffAttributes + index);

    /// <summary>
    /// Sets an attribute, clamped to <c>1..<see cref="CharacterFormat.MaxAttribute"/></c> — the same
    /// range the locator's occupancy test requires (1..100), so an edit can never write a value that
    /// would make the record fail validation and drop the party out of the next scan.
    /// </summary>
    public void SetAttribute(int index, int value) =>
        U8(CharacterFormat.OffAttributes + index, Math.Clamp(value, 1, CharacterFormat.MaxAttribute));

    // --- identity / progression ----------------------------------------------
    public long Money { get => U24(CharacterFormat.OffMoney); set => U24(CharacterFormat.OffMoney, value); }
    // Gender and nationality clamp to the ranges the locator's occupancy test accepts (0/1 and 0..4),
    // so — like the attribute and CON setters — no edit through this layer can push the value out of
    // range and drop the ranger from the next scan. (The UI ComboBoxes already only offer valid
    // values; this keeps the invariant true for any other write path too.)
    public int Gender { get => U8(CharacterFormat.OffGender); set => U8(CharacterFormat.OffGender, Math.Clamp(value, 0, 1)); }
    public int Nationality { get => U8(CharacterFormat.OffNationality); set => U8(CharacterFormat.OffNationality, Math.Clamp(value, 0, 4)); }
    public int ArmorClass { get => U8(CharacterFormat.OffArmorClass); set => U8(CharacterFormat.OffArmorClass, value); }
    // MAXCON is clamped to 1..MaxPlausibleCon so an edited ranger always stays within the range the
    // locator treats as a real record — a wildly out-of-range MAXCON would make the whole roster
    // window fail validation on the next scan.
    public int MaxCon { get => U16(CharacterFormat.OffMaxCon); set => U16(CharacterFormat.OffMaxCon, Math.Clamp(value, 1, CharacterFormat.MaxPlausibleCon)); }
    // Current CON is clamped to its max: the locator rejects a record whose CON exceeds MAXCON, so an
    // edit that set CON above MAXCON would make the ranger vanish from the next scan. Raise MAXCON first.
    public int Con { get => U16(CharacterFormat.OffCon); set => U16(CharacterFormat.OffCon, Math.Clamp(value, 0, MaxCon)); }
    public int SkillPoints { get => U8(CharacterFormat.OffSkillPoints); set => U8(CharacterFormat.OffSkillPoints, value); }
    public long Experience { get => U24(CharacterFormat.OffExperience); set => U24(CharacterFormat.OffExperience, value); }
    public int Level { get => U8(CharacterFormat.OffLevel); set => U8(CharacterFormat.OffLevel, value); }

    // --- skills (packed id/level array, 0x00-terminated) ---------------------
    public IReadOnlyList<PackedEntry> GetSkills() =>
        ReadPacked(CharacterFormat.OffSkills, CharacterFormat.SkillSlots);

    /// <summary>Level of a skill by id, or 0 if the character does not have it.</summary>
    public int GetSkillLevel(int id)
    {
        for (int i = 0; i < CharacterFormat.SkillSlots; i++)
        {
            int o = CharacterFormat.OffSkills + i * CharacterFormat.SlotSize;
            int sid = Bytes[o];
            if (sid == 0) break;            // terminator
            if (sid == id) return Bytes[o + 1];
        }
        return 0;
    }

    /// <summary>
    /// Sets a skill's level, reusing the existing entry or appending a new one. Returns false when
    /// the list is full and the skill is not already present. Never removes an entry (a level of 0
    /// is written in place); the game reads the list up to the terminator.
    /// </summary>
    public bool SetSkillLevel(int id, int level) =>
        SetPacked(CharacterFormat.OffSkills, CharacterFormat.SkillSlots, id, level);

    // --- inventory (30 fixed 2-byte slots; gap-tolerant, indexed by slot) ----
    // Unlike the skill list, inventory is edited as 30 independent slots (see the editor and
    // ItemCount), so it is read by index rather than to a 0x00 terminator.
    public int GetItemId(int slot) => U8(CharacterFormat.OffInventory + slot * CharacterFormat.SlotSize);
    public int GetItemQty(int slot) => U8(CharacterFormat.OffInventory + slot * CharacterFormat.SlotSize + 1);

    public void SetItem(int slot, int id, int qty)
    {
        int o = CharacterFormat.OffInventory + slot * CharacterFormat.SlotSize;
        U8(o, id);
        U8(o + 1, qty);
    }

    /// <summary>
    /// Packs the inventory into the gap-free, 0x00-terminated form the running game reads: occupied
    /// slots keep their relative order but shift to the front, and the trailing slots are zero-filled.
    /// The game scans the item list only up to the first empty slot (id 0), so an item written past a
    /// gap is invisible in-game and clearing a middle slot truncates every item after it. Compacting
    /// after each edit keeps every carried item inside the run the game actually reads — this is what
    /// makes an inventory change show up in the game rather than silently landing beyond the terminator.
    /// The equipped-weapon byte (0x1F) is left untouched, as elsewhere.
    /// </summary>
    public void CompactInventory()
    {
        Span<(int id, int qty)> kept = stackalloc (int, int)[CharacterFormat.ItemSlots];
        int n = 0;
        for (int i = 0; i < CharacterFormat.ItemSlots; i++)
        {
            int id = GetItemId(i);
            if (id != 0) kept[n++] = (id, GetItemQty(i));
        }
        for (int i = 0; i < CharacterFormat.ItemSlots; i++)
        {
            if (i < n) SetItem(i, kept[i].id, kept[i].qty);
            else SetItem(i, 0, 0);
        }
    }

    /// <summary>Count of non-empty inventory slots.</summary>
    public int ItemCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < CharacterFormat.ItemSlots; i++)
                if (GetItemId(i) != 0) n++;
            return n;
        }
    }

    // --- packed-array helpers ------------------------------------------------
    private List<PackedEntry> ReadPacked(int baseOff, int slots)
    {
        var list = new List<PackedEntry>();
        for (int i = 0; i < slots; i++)
        {
            int o = baseOff + i * CharacterFormat.SlotSize;
            int id = Bytes[o];
            if (id == 0) break;                     // terminator
            list.Add(new PackedEntry(i, id, Bytes[o + 1]));
        }
        return list;
    }

    private bool SetPacked(int baseOff, int slots, int id, int value)
    {
        int firstFree = -1;
        for (int i = 0; i < slots; i++)
        {
            int o = baseOff + i * CharacterFormat.SlotSize;
            int slotId = Bytes[o];
            if (slotId == id) { Bytes[o + 1] = (byte)Math.Clamp(value, 0, 255); return true; }
            if (slotId == 0) { firstFree = i; break; }
        }
        if (firstFree < 0) return false;            // full and not present
        int fo = baseOff + firstFree * CharacterFormat.SlotSize;
        Bytes[fo] = (byte)Math.Clamp(id, 0, 255);
        Bytes[fo + 1] = (byte)Math.Clamp(value, 0, 255);
        return true;
    }

    // --- derived -------------------------------------------------------------
    public string GenderName => CharacterFormat.GenderName(Gender);
    public string NationalityName => CharacterFormat.NationalityName(Nationality);

    /// <summary>
    /// True when this slot holds a real character — a thin wrapper over <see cref="IsValidRecord"/>,
    /// the single occupancy test shared with the structural scanner (<c>PartyLocator</c>).
    /// </summary>
    public bool IsOccupied => IsValidRecord(Bytes, 0);

    /// <summary>
    /// The one raw-buffer occupancy test used by both the instance <see cref="IsOccupied"/> and the
    /// structural <c>PartyLocator</c> scan, so the editor's clamps, the locator's gate and the
    /// re-scan validity check can never drift apart. True when the <see cref="CharacterFormat.RecordSize"/>-byte
    /// record at <paramref name="offset"/> in <paramref name="buffer"/> has a well-formed name
    /// (2..13 printable-ASCII bytes, NUL-terminated, starting with a letter), seven attribute bytes
    /// in 1..100, a plausible MAXCON, current CON not exceeding MAXCON, gender 0/1 and nationality
    /// 0..4. The extra field checks reject stray byte runs that a name-plus-attributes-only test would
    /// mistake for a member.
    /// </summary>
    public static bool IsValidRecord(byte[] buffer, int offset)
    {
        if (offset < 0 || buffer.Length - offset < CharacterFormat.RecordSize) return false;

        int nameOff = offset + CharacterFormat.OffName;
        byte first = buffer[nameOff];
        // Names are plain 7-bit ASCII, so the first byte must itself be a letter (no high-bit masking:
        // the printable-range loop below rejects any byte > 0x7E anyway).
        if (!((first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z'))) return false;

        int len = 0;
        bool terminated = false;
        for (int i = 0; i < CharacterFormat.NameLength; i++)
        {
            byte b = buffer[nameOff + i];
            if (b == 0) { terminated = true; break; }
            if (b < 0x20 || b > 0x7E) return false;
            len++;
        }
        if (!terminated || len < 2) return false;

        for (int k = 0; k < CharacterFormat.AttributeCount; k++)
        {
            int a = buffer[offset + CharacterFormat.OffAttributes + k];
            if (a < 1 || a > 100) return false;
        }

        int maxCon = buffer[offset + CharacterFormat.OffMaxCon] | (buffer[offset + CharacterFormat.OffMaxCon + 1] << 8);
        if (maxCon <= 0 || maxCon > CharacterFormat.MaxPlausibleCon) return false;

        int con = buffer[offset + CharacterFormat.OffCon] | (buffer[offset + CharacterFormat.OffCon + 1] << 8);
        if (con > maxCon) return false;                               // current CON can't exceed its max

        if (buffer[offset + CharacterFormat.OffGender] > 1) return false;       // 0 = Male, 1 = Female
        if (buffer[offset + CharacterFormat.OffNationality] > 4) return false;  // 0..4
        return true;
    }

    public CharacterRecord Clone() => new(Bytes);

    public override string ToString() => $"{Name} (L{Level})";
}
