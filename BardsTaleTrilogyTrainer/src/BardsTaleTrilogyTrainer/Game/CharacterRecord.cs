using System.Text;
using BardsTaleTrilogyTrainer.Memory;

namespace BardsTaleTrilogyTrainer.Game;

/// <summary>
/// A typed live view over one IL2CPP character object in the target process.
/// Unlike the DOS trainers (which keep a local byte buffer and sync), this record
/// reads and writes directly to process memory at the character object's address.
/// The trainer keeps one of these per located party slot.
/// </summary>
public sealed class CharacterRecord
{
    private readonly IMemorySource _mem;

    /// <summary>Absolute address of the IL2CPP character object in the target process.</summary>
    public nuint Address { get; }

    /// <summary>Party slot index (0 = special/summon slot, 1–6 = members).</summary>
    public int Slot { get; set; }

    /// <summary>Display name (read from the IL2CPP String at <see cref="CharacterFormat.OffName"/>).</summary>
    public string Name { get; private set; } = "";

    public CharacterRecord(IMemorySource mem, nuint address, int slot)
    {
        _mem = mem;
        Address = address;
        Slot = slot;
        Name = ReadName();
    }

    // --- primitive accessors ----------------------------------------------------
    public int ReadI32(int offset)
    {
        var buf = new byte[4];
        return _mem.Read(Address + (nuint)offset, buf, 4) == 4
            ? buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24)
            : 0;
    }

    public bool WriteI32(int offset, int value)
    {
        var buf = new byte[]
        {
            (byte)(value & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 24) & 0xFF),
        };
        return _mem.Write(Address + (nuint)offset, buf);
    }

    public byte ReadByte(int offset)
    {
        var buf = new byte[1];
        return _mem.Read(Address + (nuint)offset, buf, 1) == 1 ? buf[0] : (byte)0;
    }

    public bool WriteByte(int offset, byte value)
    {
        var buf = new byte[] { value };
        return _mem.Write(Address + (nuint)offset, buf);
    }

    // --- confirmed fields -------------------------------------------------------
    /// <summary>[Confirmed] Experience points.</summary>
    public int Experience
    {
        get => ReadI32(CharacterFormat.OffExperience);
        set => WriteI32(CharacterFormat.OffExperience, value);
    }

    /// <summary>[Confirmed] Current hit points.</summary>
    public int HpCur
    {
        get => ReadI32(CharacterFormat.OffHpCur);
        set => WriteI32(CharacterFormat.OffHpCur, value);
    }

    /// <summary>[Confirmed] Current spell points (mana).</summary>
    public int SpCur
    {
        get => ReadI32(CharacterFormat.OffSpCur);
        set => WriteI32(CharacterFormat.OffSpCur, value);
    }

    // --- inferred fields --------------------------------------------------------
    /// <summary>[Inferred] Race (0=Human … 6=Gnome).</summary>
    public int Race
    {
        get => ReadI32(CharacterFormat.OffRace);
        set => WriteI32(CharacterFormat.OffRace, value);
    }

    /// <summary>[Inferred] Class (0=Warrior … 9=Wizard).</summary>
    public int Class
    {
        get => ReadI32(CharacterFormat.OffClass);
        set => WriteI32(CharacterFormat.OffClass, value);
    }

    /// <summary>[Inferred] Status bitfield.</summary>
    public int Status
    {
        get => ReadI32(CharacterFormat.OffStatus);
        set => WriteI32(CharacterFormat.OffStatus, value);
    }

    /// <summary>[Inferred] Character level.</summary>
    public int Level
    {
        get => ReadI32(CharacterFormat.OffLevel);
        set => WriteI32(CharacterFormat.OffLevel, value);
    }

    /// <summary>[Inferred] Maximum hit points.</summary>
    public int HpMax
    {
        get => ReadI32(CharacterFormat.OffHpMax);
        set => WriteI32(CharacterFormat.OffHpMax, value);
    }

    /// <summary>[Inferred] Maximum spell points.</summary>
    public int SpMax
    {
        get => ReadI32(CharacterFormat.OffSpMax);
        set => WriteI32(CharacterFormat.OffSpMax, value);
    }

    /// <summary>[Inferred] Base armor class.</summary>
    public int ArmorClass
    {
        get => ReadI32(CharacterFormat.OffArmorClass);
        set => WriteI32(CharacterFormat.OffArmorClass, value);
    }

    // --- attributes (inferred) --------------------------------------------------
    public int GetStatCur(int index) => ReadI32(CharacterFormat.OffStrCur + index * 4);
    public void SetStatCur(int index, int v) => WriteI32(CharacterFormat.OffStrCur + index * 4, v);
    public int GetStatMax(int index) => ReadI32(CharacterFormat.OffStrMax + index * 4);
    public void SetStatMax(int index, int v) => WriteI32(CharacterFormat.OffStrMax + index * 4, v);

    // --- spell-class levels (inferred) ------------------------------------------
    public byte GetSpellLevel(int spellClassIndex) => ReadByte(CharacterFormat.OffConjurerLevel + spellClassIndex);
    public void SetSpellLevel(int spellClassIndex, byte v) => WriteByte(CharacterFormat.OffConjurerLevel + spellClassIndex, v);

    /// <summary>Sets all four spell-class levels to 7, granting knowledge of all standard spells.</summary>
    public void LearnAllClassSpells()
    {
        SetSpellLevel(0, 7); // Conjurer
        SetSpellLevel(1, 7); // Magician
        SetSpellLevel(2, 7); // Sorcerer
        SetSpellLevel(3, 7); // Wizard
    }

    // --- inventory (inferred) ---------------------------------------------------
    /// <summary>Reads the inventory array pointer, then reads each item's charge count.
    /// Returns null if the inventory array is not readable.</summary>
    public int?[] ReadItemCharges()
    {
        var result = new int?[CharacterFormat.InventorySlots];
        nuint invPtr = ReadPtr(CharacterFormat.OffInventory);
        if (invPtr == 0) return result;

        for (int i = 0; i < CharacterFormat.InventorySlots; i++)
        {
            nuint itemPtr = ReadPtrAt(invPtr, (nuint)(CharacterFormat.ArrayHeaderSize + i * 8));
            if (itemPtr == 0) { result[i] = null; continue; }
            var buf = new byte[4];
            if (_mem.Read(itemPtr + (nuint)CharacterFormat.ItemChargesOffset, buf, 4) == 4)
                result[i] = buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24);
        }
        return result;
    }

    /// <summary>Sets the charge count of every carried item to zero (infinite uses).
    /// The game engine treats items with zero charges as unlimited.</summary>
    public bool SetAllItemsInfinite()
    {
        bool ok = true;
        nuint invPtr = ReadPtr(CharacterFormat.OffInventory);
        if (invPtr == 0) return false;

        for (int i = 0; i < CharacterFormat.InventorySlots; i++)
        {
            nuint itemPtr = ReadPtrAt(invPtr, (nuint)(CharacterFormat.ArrayHeaderSize + i * 8));
            if (itemPtr == 0) continue;
            if (!WriteI32At(itemPtr + (nuint)CharacterFormat.ItemChargesOffset, 0))
                ok = false;
        }
        return ok;
    }

    // --- name -------------------------------------------------------------------
    private string ReadName()
    {
        nuint strPtr = ReadPtr(CharacterFormat.OffName);
        if (strPtr == 0) return "";

        // IL2CPP String: header (16 bytes) + length (4 bytes) + chars (UTF-16)
        var lenBuf = new byte[4];
        if (_mem.Read(strPtr + 0x10, lenBuf, 4) != 4) return "";
        int len = lenBuf[0] | (lenBuf[1] << 8) | (lenBuf[2] << 16) | (lenBuf[3] << 24);
        if (len < 0 || len > 256) return "";

        var charBuf = new byte[len * 2];
        if (_mem.Read(strPtr + 0x14, charBuf, len * 2) != len * 2) return "";
        return Encoding.Unicode.GetString(charBuf);
    }

    // --- pointer helpers --------------------------------------------------------
    private nuint ReadPtr(int offset)
    {
        var buf = new byte[8];
        if (_mem.Read(Address + (nuint)offset, buf, 8) != 8) return 0;
        return (nuint)(
            (long)buf[0] | ((long)buf[1] << 8) | ((long)buf[2] << 16) | ((long)buf[3] << 24) |
            ((long)buf[4] << 32) | ((long)buf[5] << 40) | ((long)buf[6] << 48) | ((long)buf[7] << 56));
    }

    private nuint ReadPtrAt(nuint baseAddr, nuint offset)
    {
        var buf = new byte[8];
        if (_mem.Read(baseAddr + offset, buf, 8) != 8) return 0;
        return (nuint)(
            (long)buf[0] | ((long)buf[1] << 8) | ((long)buf[2] << 16) | ((long)buf[3] << 24) |
            ((long)buf[4] << 32) | ((long)buf[5] << 40) | ((long)buf[6] << 48) | ((long)buf[7] << 56));
    }

    private bool WriteI32At(nuint addr, int value)
    {
        var buf = new byte[]
        {
            (byte)(value & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 24) & 0xFF),
        };
        return _mem.Write(addr, buf);
    }

    // --- display helpers --------------------------------------------------------
    public string ClassName => CharacterFormat.ClassName(Class);
    public string RaceName => CharacterFormat.RaceName(Race);
    public bool IsOccupied => Address != 0 && Class >= 0 && Class <= 9 && HpMax > 0;

    public override string ToString() =>
        $"{Slot}: {Name} (L{Level} {RaceName} {ClassName})";
}
