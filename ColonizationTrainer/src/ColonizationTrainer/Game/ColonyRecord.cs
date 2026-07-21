namespace ColonizationTrainer.Game;

/// <summary>
/// A typed, mutable view over one 202-byte colony record inside a decoded save buffer. Like
/// <see cref="NationRecord"/> it writes straight into the save's byte array. Only the fields the
/// trainer edits are exposed (name, population, hammers, the 16-good stockpile); the packed building
/// bitfield and per-colonist arrays are left untouched so an edit can't scramble them.
/// </summary>
public sealed class ColonyRecord
{
    private readonly byte[] _data;
    private readonly int _base;

    /// <summary>Zero-based index of this colony in the save's colony section.</summary>
    public int Index { get; }

    internal ColonyRecord(byte[] data, int baseOffset, int index)
    {
        _data = data;
        _base = baseOffset;
        Index = index;
    }

    public int X => Bytes.U8(_data, _base + SaveFormat.Col_X);
    public int Y => Bytes.U8(_data, _base + SaveFormat.Col_Y);

    /// <summary>Owning nation index (0..3 European, higher = native).</summary>
    public int Nation => Bytes.U8(_data, _base + SaveFormat.Col_Nation);

    public string Name
    {
        get => ColonyText.ReadName(_data, _base + SaveFormat.Col_Name, SaveFormat.Col_NameMax);
        set => ColonyText.WriteName(_data, _base + SaveFormat.Col_Name, SaveFormat.Col_NameMax, value);
    }

    /// <summary>
    /// Number of colonists in the colony. Clamped to the 32-colonist array capacity
    /// (<see cref="SaveFormat.MaxColonists"/>) rather than the raw byte range, since a higher value
    /// describes more colonists than the record can hold and can make the colony screen misbehave.
    /// </summary>
    public int Population
    {
        get => Bytes.U8(_data, _base + SaveFormat.Col_Population);
        set => Bytes.WriteU8(_data, _base + SaveFormat.Col_Population, Math.Clamp(value, 0, SaveFormat.MaxColonists));
    }

    /// <summary>Accumulated construction hammers (u16).</summary>
    public int Hammers
    {
        get => Bytes.U16(_data, _base + SaveFormat.Col_Hammers);
        set => Bytes.WriteU16(_data, _base + SaveFormat.Col_Hammers, Math.Clamp(value, 0, ushort.MaxValue));
    }

    public int WarehouseLevel => Bytes.U8(_data, _base + SaveFormat.Col_WarehouseLevel);

    /// <summary>Reads good <paramref name="goodId"/>'s stockpile quantity (signed 16-bit).</summary>
    public short GetStock(int goodId)
    {
        RangeCheck(goodId);
        return Bytes.S16(_data, _base + SaveFormat.Col_Stock + goodId * 2);
    }

    /// <summary>Writes good <paramref name="goodId"/>'s stockpile quantity (floored at 0, like the other setters).</summary>
    public void SetStock(int goodId, short value)
    {
        RangeCheck(goodId);
        short clamped = (short)Math.Clamp((int)value, 0, SaveFormat.GoodsMax);
        Bytes.WriteS16(_data, _base + SaveFormat.Col_Stock + goodId * 2, clamped);
    }

    /// <summary>Fills every good's stockpile to <paramref name="amount"/>.</summary>
    public void FillAllStock(short amount)
    {
        for (int g = 0; g < SaveFormat.GoodsCount; g++) SetStock(g, amount);
    }

    private static void RangeCheck(int goodId)
    {
        if (goodId < 0 || goodId >= SaveFormat.GoodsCount)
            throw new ArgumentOutOfRangeException(nameof(goodId), "good id must be 0..15.");
    }
}
