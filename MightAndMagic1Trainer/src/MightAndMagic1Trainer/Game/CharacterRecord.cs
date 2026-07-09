using System.Text;

namespace MightAndMagic1Trainer.Game;

/// <summary>
/// A typed view over a single 127-byte character record. The record owns a private
/// byte buffer; friendly properties read/write straight into it. The trainer keeps
/// one of these per character and synchronises it with process memory.
/// </summary>
public sealed class CharacterRecord
{
    private readonly byte[] _data = new byte[RosterFormat.RecordSize];

    /// <summary>Absolute address of this record in the target process (0 if from file).</summary>
    public nuint Address { get; set; }

    /// <summary>Slot index within the roster (0-based).</summary>
    public int Slot { get; set; }

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

    /// <summary>Raw bytes of the record (live reference — edits are reflected immediately).</summary>
    public byte[] Raw => _data;

    public byte[] ToArray() => (byte[])_data.Clone();

    // --- primitive accessors ----------------------------------------------------
    public byte GetByte(int off) => _data[off];
    public void SetByte(int off, byte value) => _data[off] = value;

    public ushort GetU16(int off) => (ushort)(_data[off] | (_data[off + 1] << 8));
    public void SetU16(int off, ushort value)
    {
        _data[off] = (byte)(value & 0xFF);
        _data[off + 1] = (byte)(value >> 8);
    }

    public uint GetU24(int off) =>
        (uint)(_data[off] | (_data[off + 1] << 8) | (_data[off + 2] << 16));
    public void SetU24(int off, uint value)
    {
        _data[off] = (byte)(value & 0xFF);
        _data[off + 1] = (byte)((value >> 8) & 0xFF);
        _data[off + 2] = (byte)((value >> 16) & 0xFF);
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
    public string Name
    {
        get
        {
            int len = 0;
            while (len < RosterFormat.NameLength && _data[RosterFormat.OffName + len] != 0) len++;
            return Encoding.ASCII.GetString(_data, RosterFormat.OffName, len);
        }
        set
        {
            var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
            for (int i = 0; i < RosterFormat.NameLength; i++)
                _data[RosterFormat.OffName + i] = i < bytes.Length ? bytes[i] : (byte)0;
        }
    }

    public byte Sex
    {
        get => _data[RosterFormat.OffSex];
        set => _data[RosterFormat.OffSex] = value;
    }

    public byte Class
    {
        get => _data[RosterFormat.OffClass];
        set => _data[RosterFormat.OffClass] = value;
    }

    public byte LevelCur
    {
        get => _data[RosterFormat.OffLevelCur];
        set => _data[RosterFormat.OffLevelCur] = value;
    }

    public byte LevelMax
    {
        get => _data[RosterFormat.OffLevelMax];
        set => _data[RosterFormat.OffLevelMax] = value;
    }

    public byte Age
    {
        get => _data[RosterFormat.OffAge];
        set => _data[RosterFormat.OffAge] = value;
    }

    public byte ArmorClass
    {
        get => _data[RosterFormat.OffArmorClass];
        set => _data[RosterFormat.OffArmorClass] = value;
    }

    public byte SpellLevel
    {
        get => _data[RosterFormat.OffSpellLevel];
        set => _data[RosterFormat.OffSpellLevel] = value;
    }

    public ushort HpCur
    {
        get => GetU16(RosterFormat.OffHpCur);
        set => SetU16(RosterFormat.OffHpCur, value);
    }

    /// <summary>Temporary/modified max HP (0x35); kept in sync with <see cref="HpMax"/> when maxing.</summary>
    public ushort HpMod
    {
        get => GetU16(RosterFormat.OffHpMod);
        set => SetU16(RosterFormat.OffHpMod, value);
    }

    public ushort HpMax
    {
        get => GetU16(RosterFormat.OffHpMax);
        set => SetU16(RosterFormat.OffHpMax, value);
    }

    public ushort SpCur
    {
        get => GetU16(RosterFormat.OffSpCur);
        set => SetU16(RosterFormat.OffSpCur, value);
    }

    public ushort SpMax
    {
        get => GetU16(RosterFormat.OffSpMax);
        set => SetU16(RosterFormat.OffSpMax, value);
    }

    public uint Experience
    {
        get => GetU32(RosterFormat.OffExperience);
        set => SetU32(RosterFormat.OffExperience, value);
    }

    public uint Gold
    {
        get => GetU24(RosterFormat.OffGold);
        set => SetU24(RosterFormat.OffGold, value);
    }

    public ushort Gems
    {
        get => GetU16(RosterFormat.OffGems);
        set => SetU16(RosterFormat.OffGems, value);
    }

    public byte Food
    {
        get => _data[RosterFormat.OffFood];
        set => _data[RosterFormat.OffFood] = value;
    }

    public byte Condition
    {
        get => _data[RosterFormat.OffCondition];
        set => _data[RosterFormat.OffCondition] = value;
    }

    public string ConditionName => RosterFormat.ConditionName(Condition);

    // Attributes are stored as two bytes each: [normal (permanent), active (temp)].
    // The game uses the active value; resting resets it to the normal value.
    /// <summary>Permanent/normal value of attribute <paramref name="index"/> (0..6).</summary>
    public byte GetStatNormal(int index) => _data[RosterFormat.OffStats + index * 2];
    /// <summary>Active (temporary) value of attribute <paramref name="index"/> (0..6).</summary>
    public byte GetStatActive(int index) => _data[RosterFormat.OffStats + index * 2 + 1];
    public void SetStatNormal(int index, byte v) => _data[RosterFormat.OffStats + index * 2] = v;
    public void SetStatActive(int index, byte v) => _data[RosterFormat.OffStats + index * 2 + 1] = v;

    // Resistances mirror the attribute layout: [normal (permanent), active (temp)] pairs.
    /// <summary>Permanent/normal value of resistance <paramref name="index"/> (0..7), in percent.</summary>
    public byte GetResistanceNormal(int index) => _data[RosterFormat.OffResistances + index * 2];
    /// <summary>Active (temporary) value of resistance <paramref name="index"/> (0..7), in percent.</summary>
    public byte GetResistanceActive(int index) => _data[RosterFormat.OffResistances + index * 2 + 1];
    public void SetResistanceNormal(int index, byte v) => _data[RosterFormat.OffResistances + index * 2] = v;
    public void SetResistanceActive(int index, byte v) => _data[RosterFormat.OffResistances + index * 2 + 1] = v;

    public string ClassName => RosterFormat.ClassName(Class);
    public string SexName => RosterFormat.SexName(Sex);

    public override string ToString() =>
        $"{Slot}: {Name} (L{LevelCur} {ClassName})";
}
