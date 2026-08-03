namespace DarkDesigns1Trainer.Game;

/// <summary>
/// A typed, mutable view over a <see cref="CharacterFormat.RecordSize"/>-byte Dark Designs I
/// character record. The backing <see cref="Bytes"/> array can come from a file, a memory
/// dump, or live process memory; edits mutate the buffer in place so the caller can write it
/// back.
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
        (uint)(Bytes[o] | (Bytes[o + 1] << 8) | (Bytes[o + 2] << 16) | (Bytes[o + 3] << 24));
    private void U32(int o, long v)
    {
        v = Math.Clamp(v, 0, uint.MaxValue);
        Bytes[o] = (byte)(v & 0xFF);
        Bytes[o + 1] = (byte)((v >> 8) & 0xFF);
        Bytes[o + 2] = (byte)((v >> 16) & 0xFF);
        Bytes[o + 3] = (byte)((v >> 24) & 0xFF);
    }

    // --- name ----------------------------------------------------------------
    public string Name
    {
        get
        {
            int len = Math.Min((int)Bytes[CharacterFormat.OffNameLen], CharacterFormat.NameLength);
            if (len <= 0) return "";
            return System.Text.Encoding.ASCII.GetString(Bytes, CharacterFormat.OffName, len).TrimEnd('\0');
        }
        set
        {
            string s = value ?? "";
            int len = Math.Min(s.Length, CharacterFormat.NameLength);
            Bytes[CharacterFormat.OffNameLen] = (byte)len;
            Array.Clear(Bytes, CharacterFormat.OffName, CharacterFormat.NameLength);
            if (len > 0)
                System.Text.Encoding.ASCII.GetBytes(s, 0, len, Bytes, CharacterFormat.OffName);
        }
    }

    // --- class / level -------------------------------------------------------
    public int Class
    {
        get => U8(CharacterFormat.OffClass);
        set => U8(CharacterFormat.OffClass, value);
    }

    public int Level
    {
        get => U16(CharacterFormat.OffLevel);
        set => U16(CharacterFormat.OffLevel, value);
    }

    // --- attributes (uint16 LE) ----------------------------------------------
    public int GetAttribute(int index) => U16(CharacterFormat.AttributeOffsets[index]);
    public void SetAttribute(int index, int value) => U16(CharacterFormat.AttributeOffsets[index], value);

    public int Strength { get => GetAttribute(0); set => SetAttribute(0, value); }
    public int Dexterity { get => GetAttribute(1); set => SetAttribute(1, value); }
    public int Constitution { get => GetAttribute(2); set => SetAttribute(2, value); }
    public int Intelligence { get => GetAttribute(3); set => SetAttribute(3, value); }
    public int Piety { get => GetAttribute(4); set => SetAttribute(4, value); }

    // --- vitals (uint16 LE) --------------------------------------------------
    public int BodyCurrent { get => U16(CharacterFormat.OffBodyCur); set => U16(CharacterFormat.OffBodyCur, value); }
    public int BodyMax { get => U16(CharacterFormat.OffBodyMax); set => U16(CharacterFormat.OffBodyMax, value); }
    public int MagicCurrent { get => U16(CharacterFormat.OffMagicCur); set => U16(CharacterFormat.OffMagicCur, value); }
    public int MagicMax { get => U16(CharacterFormat.OffMagicMax); set => U16(CharacterFormat.OffMagicMax, value); }

    // --- progression ---------------------------------------------------------
    public long Experience { get => U32(CharacterFormat.OffExperience); set => U32(CharacterFormat.OffExperience, value); }
    public long NextLevel { get => U32(CharacterFormat.OffNextLevel); set => U32(CharacterFormat.OffNextLevel, value); }
    public int Gold { get => U16(CharacterFormat.OffGold); set => U16(CharacterFormat.OffGold, value); }

    // --- status --------------------------------------------------------------
    public int Status { get => U8(CharacterFormat.OffStatus); set => U8(CharacterFormat.OffStatus, value); }

    // --- readied equipment ---------------------------------------------------
    public int RightHand { get => U8(CharacterFormat.OffReadyRightHand); set => U8(CharacterFormat.OffReadyRightHand, value); }
    public int LeftHand { get => U8(CharacterFormat.OffReadyLeftHand); set => U8(CharacterFormat.OffReadyLeftHand, value); }
    public int Armor { get => U8(CharacterFormat.OffReadyArmor); set => U8(CharacterFormat.OffReadyArmor, value); }
    public int Ring { get => U8(CharacterFormat.OffReadyRing); set => U8(CharacterFormat.OffReadyRing, value); }

    /// <summary>Reads a readied-equipment slot by <see cref="ItemBook.ReadySlot"/>.</summary>
    public int GetReadied(ItemBook.ReadySlot slot) => U8(ItemBook.ReadyOffset(slot));

    /// <summary>Writes a readied-equipment slot by <see cref="ItemBook.ReadySlot"/>.</summary>
    public void SetReadied(ItemBook.ReadySlot slot, int itemId) =>
        U8(ItemBook.ReadyOffset(slot), Math.Clamp(itemId, 0, CharacterFormat.MaxItemId));

    // --- carried inventory ---------------------------------------------------
    /// <summary>Item id in carried pack slot <paramref name="slot"/> (0-based, keys A–J).</summary>
    public int GetItem(int slot)
    {
        if (slot < 0 || slot >= CharacterFormat.ItemSlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return U8(CharacterFormat.ItemOffset(slot));
    }

    /// <summary>Sets carried pack slot <paramref name="slot"/>; 0 empties it.</summary>
    public void SetItem(int slot, int itemId)
    {
        if (slot < 0 || slot >= CharacterFormat.ItemSlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        U8(CharacterFormat.ItemOffset(slot), Math.Clamp(itemId, 0, CharacterFormat.MaxItemId));
    }

    /// <summary>The ten carried item ids, in slot order.</summary>
    public int[] Items
    {
        get
        {
            var ids = new int[CharacterFormat.ItemSlotCount];
            for (int i = 0; i < ids.Length; i++) ids[i] = GetItem(i);
            return ids;
        }
    }

    /// <summary>Number of occupied carried-item slots.</summary>
    public int ItemCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < CharacterFormat.ItemSlotCount; i++)
                if (GetItem(i) != 0) n++;
            return n;
        }
    }

    /// <summary>Empties every carried pack slot, leaving readied equipment alone.</summary>
    public void ClearItems()
    {
        for (int i = 0; i < CharacterFormat.ItemSlotCount; i++) SetItem(i, 0);
    }

    /// <summary>
    /// Puts <paramref name="itemId"/> in the first empty pack slot and returns that slot,
    /// or -1 when the pack is full.
    /// </summary>
    public int AddItem(int itemId)
    {
        for (int i = 0; i < CharacterFormat.ItemSlotCount; i++)
        {
            if (GetItem(i) != 0) continue;
            SetItem(i, itemId);
            return i;
        }
        return -1;
    }

    // --- derived -------------------------------------------------------------
    public string ClassName => CharacterFormat.ClassName(Class);
    public string StatusName => CharacterFormat.StatusName(Status);

    /// <summary>
    /// True when this slot holds a real character: exists flag = 1 and name starts with a letter.
    /// </summary>
    public bool IsOccupied
    {
        get
        {
            if (Bytes[CharacterFormat.OffExists] != 1) return false;
            char c = (char)Bytes[CharacterFormat.OffName];
            return char.IsLetter(c);
        }
    }

    public CharacterRecord Clone() => new(Bytes);

    public override string ToString() => $"{Name} (L{Level} {ClassName})";
}
