using System.Text;

namespace BardsTale1Trainer.Game;

/// <summary>
/// A typed view over a single 92-byte Bard's Tale character record. The record owns a
/// private byte buffer; friendly properties read/write straight into it. The trainer
/// keeps one of these per party slot and synchronises it with process memory.
///
/// The character's name is NOT part of the record (the game stores party names only
/// in its on-screen roster table), so <see cref="Name"/> is carried alongside.
/// </summary>
public sealed class CharacterRecord
{
    private readonly byte[] _data = new byte[PartyFormat.RecordSize];

    /// <summary>Absolute address of this record in the target process (0 if from file).</summary>
    public nuint Address { get; set; }

    /// <summary>Party slot index (0 = special/summon slot, 1..6 = members).</summary>
    public int Slot { get; set; }

    /// <summary>Display name, read from the game's roster row table (or the .TPW header).</summary>
    public string Name { get; set; } = "";

    public CharacterRecord() { }

    public CharacterRecord(ReadOnlySpan<byte> source) => Load(source);

    /// <summary>Copies up to RecordSize bytes from <paramref name="source"/> into this record.</summary>
    public void Load(ReadOnlySpan<byte> source)
    {
        int n = Math.Min(source.Length, _data.Length);
        source[..n].CopyTo(_data);
        if (n < _data.Length)
            Array.Clear(_data, n, _data.Length - n);
    }

    /// <summary>
    /// Builds a record from a 109-byte .TPW character file: 16-byte NUL-padded name,
    /// then the record bytes. Returns null when the buffer is too short.
    /// </summary>
    public static CharacterRecord? FromTpw(ReadOnlySpan<byte> file)
    {
        if (file.Length < PartyFormat.TpwRecordOffset + PartyFormat.RecordSize) return null;
        var rec = new CharacterRecord(file.Slice(PartyFormat.TpwRecordOffset, PartyFormat.RecordSize));
        // Some names embed a NUL mid-string (e.g. "A R HELPER" saved as "A\0R HELPER");
        // show the printable bytes so the user sees the same name the game lists.
        var raw = file[..PartyFormat.TpwNameLength];
        var sb = new StringBuilder();
        foreach (var b in raw)
            if (b >= 0x20 && b < 0x7F) sb.Append((char)b);
            else if (b == 0 && sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
        rec.Name = sb.ToString().Trim();
        return rec;
    }

    /// <summary>
    /// Decodes a roster-row name buffer (printable ASCII only, trimmed). BT1 names live
    /// out-of-band in the game's on-screen roster table, so this is shared by the live
    /// party reader and per-character re-reads.
    /// </summary>
    public static string DecodeRosterName(ReadOnlySpan<byte> raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (var b in raw)
            if (b >= 0x20 && b < 0x7F) sb.Append((char)b);
        return sb.ToString().Trim();
    }

    /// <summary>Raw bytes of the record (live reference — edits are reflected immediately).</summary>
    public byte[] Raw => _data;

    public byte[] ToArray() => (byte[])_data.Clone();

    /// <summary>
    /// Serialises this record as a 109-byte .TPW character file: the 16-byte NUL-padded
    /// name, the 92 record bytes with the disk marker set (byte 0 is 0x01 on disk, 0x00
    /// live), and the trailing pad byte. The in-memory record is not modified.
    /// </summary>
    public byte[] ToTpw()
    {
        var file = new byte[PartyFormat.TpwFileSize];
        var nameBytes = Encoding.ASCII.GetBytes(Name ?? "");
        for (int i = 0; i < PartyFormat.TpwNameLength && i < nameBytes.Length; i++)
            file[i] = nameBytes[i];
        _data.CopyTo(file, PartyFormat.TpwRecordOffset);
        file[PartyFormat.TpwRecordOffset + PartyFormat.OffDiskMarker] = 1;
        return file;
    }

    // --- primitive accessors ----------------------------------------------------
    public byte GetByte(int off) => _data[off];
    public void SetByte(int off, byte value) => _data[off] = value;

    public ushort GetU16(int off) => (ushort)(_data[off] | (_data[off + 1] << 8));
    public void SetU16(int off, ushort value)
    {
        _data[off] = (byte)(value & 0xFF);
        _data[off + 1] = (byte)(value >> 8);
    }

    public uint GetU32(int off) => (uint)(_data[off] | (_data[off + 1] << 8)
        | (_data[off + 2] << 16) | (_data[off + 3] << 24));
    public void SetU32(int off, uint value)
    {
        _data[off] = (byte)(value & 0xFF);
        _data[off + 1] = (byte)((value >> 8) & 0xFF);
        _data[off + 2] = (byte)((value >> 16) & 0xFF);
        _data[off + 3] = (byte)((value >> 24) & 0xFF);
    }

    // --- friendly fields --------------------------------------------------------
    /// <summary>0 = OK/occupied; 1 = empty slot (observed on vacated slots).</summary>
    public ushort Status
    {
        get => GetU16(PartyFormat.OffStatus);
        set => SetU16(PartyFormat.OffStatus, value);
    }

    /// <summary>True when this slot holds a character (status 0 isn't an emptied slot).</summary>
    public bool IsOccupied => Status == 0 && (Class != 0 || HpMax != 0 || GetU16(PartyFormat.OffStatsMax) != 0);

    public ushort Class
    {
        get => GetU16(PartyFormat.OffClass);
        set => SetU16(PartyFormat.OffClass, value);
    }

    public byte Race
    {
        get => _data[PartyFormat.OffRace];
        set => _data[PartyFormat.OffRace] = value;
    }

    public short ArmorClass
    {
        get => (short)GetU16(PartyFormat.OffArmorClass);
        set => SetU16(PartyFormat.OffArmorClass, (ushort)value);
    }

    public ushort HpCur
    {
        get => GetU16(PartyFormat.OffHpCur);
        set => SetU16(PartyFormat.OffHpCur, value);
    }

    public ushort HpMax
    {
        get => GetU16(PartyFormat.OffHpMax);
        set => SetU16(PartyFormat.OffHpMax, value);
    }

    public ushort SpCur
    {
        get => GetU16(PartyFormat.OffSpCur);
        set => SetU16(PartyFormat.OffSpCur, value);
    }

    public ushort SpMax
    {
        get => GetU16(PartyFormat.OffSpMax);
        set => SetU16(PartyFormat.OffSpMax, value);
    }

    public uint Experience
    {
        get => GetU32(PartyFormat.OffExperience);
        set => SetU32(PartyFormat.OffExperience, value);
    }

    public uint Gold
    {
        get => GetU32(PartyFormat.OffGold);
        set => SetU32(PartyFormat.OffGold, value);
    }

    public ushort Level
    {
        get => GetU16(PartyFormat.OffLevel);
        set => SetU16(PartyFormat.OffLevel, value);
    }

    public ushort LevelMax
    {
        get => GetU16(PartyFormat.OffLevelMax);
        set => SetU16(PartyFormat.OffLevelMax, value);
    }

    /// <summary>Max (permanent) value of stat <paramref name="index"/> (0..4: St,IQ,Dx,Cn,Lk).</summary>
    public ushort GetStatMax(int index) => GetU16(PartyFormat.OffStatsMax + index * 2);
    /// <summary>Current (active) value of stat <paramref name="index"/>.</summary>
    public ushort GetStatCur(int index) => GetU16(PartyFormat.OffStatsCur + index * 2);
    public void SetStatMax(int index, ushort v) => SetU16(PartyFormat.OffStatsMax + index * 2, v);
    public void SetStatCur(int index, ushort v) => SetU16(PartyFormat.OffStatsCur + index * 2, v);

    /// <summary>Raw item word for inventory slot <paramref name="index"/> (0..7).</summary>
    public ushort GetItemWord(int index) => GetU16(PartyFormat.OffItems + index * 2);
    public void SetItemWord(int index, ushort v) => SetU16(PartyFormat.OffItems + index * 2, v);

    /// <summary>Spell level for spell-class <paramref name="index"/> (0=Magician,1=Conjurer,2=Sorcerer,3=Wizard).</summary>
    public byte GetSpellLevel(int index) => _data[PartyFormat.OffSpellLevels + index];
    public void SetSpellLevel(int index, byte v) => _data[PartyFormat.OffSpellLevels + index] = v;

    public string ClassName => PartyFormat.ClassName(Class);
    public string RaceName => PartyFormat.RaceName(Race);

    public override string ToString() =>
        $"{Slot}: {Name} (L{Level} {RaceName} {ClassName})";
}
