namespace FountainOfDreamsTrainer.Game;

/// <summary>A single inventory slot entry (item ID and 5 bytes of item-specific data).</summary>
public readonly record struct InventoryEntry(int Slot, int ItemId, ReadOnlyMemory<byte> Data);

/// <summary>
/// A typed, mutable view over a <see cref="CharacterFormat.RecordSize"/>-byte Fountain of Dreams
/// character record. The backing <see cref="Bytes"/> array can come from a file, a memory dump,
/// or live process memory; edits mutate the buffer in place so the caller can write it back with
/// a read-validate-write poke.
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

    private long U32(int o) =>
        (long)((uint)Bytes[o] | ((uint)Bytes[o + 1] << 8) | ((uint)Bytes[o + 2] << 16) | ((uint)Bytes[o + 3] << 24));
    private void U32(int o, long v)
    {
        uint u = (uint)Math.Clamp(v, 0, 0xFFFFFFFF);
        Bytes[o] = (byte)(u & 0xFF);
        Bytes[o + 1] = (byte)((u >> 8) & 0xFF);
        Bytes[o + 2] = (byte)((u >> 16) & 0xFF);
        Bytes[o + 3] = (byte)((u >> 24) & 0xFF);
    }

    // --- name ---------------------------------------------------------------
    /// <summary>The character's name, as null-terminated ASCII from the first 20-byte name field.</summary>
    public string Name
    {
        get
        {
            int len = 0;
            while (len < CharacterFormat.NameFieldLength && Bytes[CharacterFormat.OffName + len] != 0)
                len++;
            return System.Text.Encoding.ASCII.GetString(Bytes, CharacterFormat.OffName, len);
        }
        set
        {
            Array.Clear(Bytes, CharacterFormat.OffName, CharacterFormat.NameFieldLength);
            if (string.IsNullOrEmpty(value)) return;
            var enc = System.Text.Encoding.ASCII.GetBytes(value);
            int n = Math.Min(enc.Length, CharacterFormat.NameFieldLength - 1);
            Array.Copy(enc, 0, Bytes, CharacterFormat.OffName, n);
        }
    }

    // --- attributes ----------------------------------------------------------
    public int GetAttribute(int index) => U8(CharacterFormat.OffAttributes + index);

    /// <summary>Sets an attribute, clamped to <c>1..<see cref="CharacterFormat.MaxAttribute"/></c>.</summary>
    public void SetAttribute(int index, int value) =>
        U8(CharacterFormat.OffAttributes + index, Math.Clamp(value, 1, CharacterFormat.MaxAttribute));

    // --- vitals / progression -----------------------------------------------
    public long Cash { get => U32(CharacterFormat.OffCash); set => U32(CharacterFormat.OffCash, value); }
    public int Profession { get => U8(CharacterFormat.OffProfession); set => U8(CharacterFormat.OffProfession, Math.Clamp(value, 0, 6)); }
    public int Con { get => U8(CharacterFormat.OffCon); set => U8(CharacterFormat.OffCon, Math.Clamp(value, 0, 255)); }
    public int ArmorClass { get => U8(CharacterFormat.OffArmorClass); set => U8(CharacterFormat.OffArmorClass, value); }
    public int MaxCon { get => U16(CharacterFormat.OffMaxCon); set => U16(CharacterFormat.OffMaxCon, Math.Clamp(value, 1, CharacterFormat.MaxPlausibleCon)); }
    public int Level { get => U8(CharacterFormat.OffLevel); set => U8(CharacterFormat.OffLevel, Math.Clamp(value, 1, CharacterFormat.MaxLevel)); }
    public int Rank { get => U16(CharacterFormat.OffRank); set => U16(CharacterFormat.OffRank, value); }
    public long Experience { get => U32(CharacterFormat.OffExperience); set => U32(CharacterFormat.OffExperience, value); }
    public int NextLevelXp { get => U16(CharacterFormat.OffNextLevelXp); set => U16(CharacterFormat.OffNextLevelXp, value); }

    // --- inventory (27 × 6-byte slots, 0xFF = empty) -------------------------
    public int GetItemId(int slot) =>
        U8(CharacterFormat.OffInventory + slot * CharacterFormat.InventorySlotSize);

    public byte[] GetItemData(int slot)
    {
        int o = CharacterFormat.OffInventory + slot * CharacterFormat.InventorySlotSize;
        return new byte[] { Bytes[o], Bytes[o + 1], Bytes[o + 2], Bytes[o + 3], Bytes[o + 4], Bytes[o + 5] };
    }

    public void SetItem(int slot, int itemId, ReadOnlySpan<byte> data)
    {
        int o = CharacterFormat.OffInventory + slot * CharacterFormat.InventorySlotSize;
        U8(o, itemId);
        for (int i = 0; i < 5 && i < data.Length; i++)
            Bytes[o + 1 + i] = data[i];
    }

    public void ClearItem(int slot)
    {
        int o = CharacterFormat.OffInventory + slot * CharacterFormat.InventorySlotSize;
        for (int i = 0; i < CharacterFormat.InventorySlotSize; i++)
            Bytes[o + i] = (byte)CharacterFormat.InventoryEmpty;
    }

    /// <summary>Count of non-empty inventory slots (itemId != 0xFF).</summary>
    public int ItemCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < CharacterFormat.InventorySlots; i++)
                if (GetItemId(i) != CharacterFormat.InventoryEmpty) n++;
            return n;
        }
    }

    public IReadOnlyList<InventoryEntry> GetInventory()
    {
        var list = new List<InventoryEntry>();
        for (int i = 0; i < CharacterFormat.InventorySlots; i++)
        {
            int id = GetItemId(i);
            if (id == CharacterFormat.InventoryEmpty) continue;
            list.Add(new InventoryEntry(i, id, GetItemData(i)));
        }
        return list;
    }

    // --- derived -------------------------------------------------------------
    public string ProfessionName => CharacterFormat.ProfessionName(Profession);

    /// <summary>
    /// True when this slot holds a real character — a thin wrapper over <see cref="IsValidRecord"/>,
    /// the single occupancy test shared with the structural scanner (<c>PartyLocator</c>).
    /// </summary>
    public bool IsOccupied => IsValidRecord(Bytes, 0);

    /// <summary>
    /// The one raw-buffer occupancy test used by both the instance <see cref="IsOccupied"/> and the
    /// structural <c>PartyLocator</c> scan, so the editor's clamps, the locator's gate and the
    /// re-scan validity check can never drift apart. True when the
    /// <see cref="CharacterFormat.RecordSize"/>-byte record at <paramref name="offset"/> in
    /// <paramref name="buffer"/> has a well-formed name (1..18 printable-ASCII bytes,
    /// NUL-terminated, starting with a letter), seven attribute bytes each in 1..20, a plausible
    /// MaxCON (1..999), a plausible level (1..99), and a profession in 0..6.
    /// </summary>
    public static bool IsValidRecord(byte[] buffer, int offset)
    {
        if (offset < 0 || buffer.Length - offset < CharacterFormat.RecordSize) return false;

        // Name: first byte must be a letter
        int nameOff = offset + CharacterFormat.OffName;
        byte first = buffer[nameOff];
        if (!((first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z'))) return false;

        int len = 0;
        bool terminated = false;
        for (int i = 0; i < CharacterFormat.NameFieldLength; i++)
        {
            byte b = buffer[nameOff + i];
            if (b == 0) { terminated = true; break; }
            if (b < 0x20 || b > 0x7E) return false;
            len++;
        }
        if (!terminated || len < 1) return false;

        // Seven attributes in 1..20
        for (int k = 0; k < CharacterFormat.AttributeCount; k++)
        {
            int a = buffer[offset + CharacterFormat.OffAttributes + k];
            if (a < 1 || a > 20) return false;
        }

        // MaxCON plausible
        int maxCon = buffer[offset + CharacterFormat.OffMaxCon]
                   | (buffer[offset + CharacterFormat.OffMaxCon + 1] << 8);
        if (maxCon <= 0 || maxCon > CharacterFormat.MaxPlausibleCon) return false;

        // Level plausible
        int level = buffer[offset + CharacterFormat.OffLevel];
        if (level < 1 || level > CharacterFormat.MaxLevel) return false;

        // Profession 0..6
        int prof = buffer[offset + CharacterFormat.OffProfession];
        if (prof > 6) return false;

        return true;
    }

    public CharacterRecord Clone() => new(Bytes);

    public override string ToString() => $"{Name} (L{Level})";
}
