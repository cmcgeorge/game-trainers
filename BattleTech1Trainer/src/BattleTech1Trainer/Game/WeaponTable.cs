namespace BattleTech1Trainer.Game;

/// <summary>One decoded record of the weapon/equipment table: its index, name, five stat bytes and a
/// weapon-class tag.</summary>
public readonly record struct WeaponEntry(int Index, string Name, byte S0, byte S1, byte S2, byte S3, byte S4, byte ClassTag)
{
    /// <summary>Human label for the class tag byte (see <see cref="WeaponTable"/>).</summary>
    public string ClassName => ClassTag switch
    {
        0x01 => "Small-arms",
        0x02 => "Ballistic",
        0x03 => "'Mech-scale",
        _    => $"0x{ClassTag:X2}",
    };
}

/// <summary>
/// Decoder for the <b>Confirmed</b> 17-byte-stride weapon/equipment table baked into
/// <c>BTECH.EXE</c> (see <c>.docs/ReverseEngineering.md</c> §3.1). Each record is an 11-byte
/// NUL/space-padded ASCII name followed by six stat bytes, the last of which is a weapon-class tag
/// (<c>0x01</c> small-arms, <c>0x02</c> ballistic personal, <c>0x03</c> 'Mech-scale). The stride of
/// 17 was verified by anchoring on twelve consecutive personal-weapon names exactly <c>0x11</c> apart.
/// This is a read-only format decoder — the trainer never writes the static table; it is surfaced as
/// reference and regression-tested by <c>FormatCheck</c> against a captured slice.
/// </summary>
public static class WeaponTable
{
    /// <summary>Bytes in one record: 11-byte name + 6 stat bytes.</summary>
    public const int RecordSize = 17;

    /// <summary>Bytes of the name field at the start of each record.</summary>
    public const int NameLength = 11;

    /// <summary>
    /// Decodes a single 17-byte record. Throws if the span is not exactly <see cref="RecordSize"/>
    /// bytes so a mis-aligned read can never silently shift the name/stat boundary.
    /// </summary>
    public static WeaponEntry Decode(ReadOnlySpan<byte> record, int index = 0)
    {
        if (record.Length != RecordSize)
            throw new ArgumentException($"A weapon record must be exactly {RecordSize} bytes.", nameof(record));

        string name = ReadName(record[..NameLength]);
        return new WeaponEntry(index, name,
            record[11], record[12], record[13], record[14], record[15], record[16]);
    }

    /// <summary>
    /// Decodes a contiguous run of records. Throws if the block length is not a whole multiple of
    /// <see cref="RecordSize"/>, so a truncated capture can't produce a garbled final row.
    /// </summary>
    public static IReadOnlyList<WeaponEntry> DecodeTable(ReadOnlySpan<byte> block)
    {
        if (block.Length == 0 || block.Length % RecordSize != 0)
            throw new ArgumentException($"A weapon table must be a non-zero multiple of {RecordSize} bytes.", nameof(block));

        int count = block.Length / RecordSize;
        var list = new List<WeaponEntry>(count);
        for (int i = 0; i < count; i++)
            list.Add(Decode(block.Slice(i * RecordSize, RecordSize), i));
        return list;
    }

    /// <summary>Trims the NUL/space padding from a fixed-width name field.</summary>
    private static string ReadName(ReadOnlySpan<byte> field)
    {
        int end = 0;
        while (end < field.Length && field[end] != 0x00) end++;
        var chars = new char[end];
        for (int i = 0; i < end; i++) chars[i] = (char)field[i];
        return new string(chars).TrimEnd(' ');
    }
}
