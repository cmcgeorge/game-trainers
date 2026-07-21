namespace ColonizationTrainer.Game;

/// <summary>
/// A typed, mutable view over one 316-byte nation record inside a decoded save buffer. It holds a
/// reference to the save's own byte array and a base offset, so every setter writes straight into
/// the buffer that <see cref="SaveGame.Save"/> will persist (read-validate-write: values are clamped
/// to a safe range before the poke). Nation order is England, France, Spain, Netherlands (0..3).
/// </summary>
public sealed class NationRecord
{
    private readonly byte[] _data;
    private readonly int _base;

    /// <summary>Save index of this nation (0 England … 3 Netherlands).</summary>
    public int Index { get; }

    /// <summary>Display name from <see cref="NationBook"/>.</summary>
    public string Name => NationBook.NameOf(Index);

    internal NationRecord(byte[] data, int baseOffset, int index)
    {
        _data = data;
        _base = baseOffset;
        Index = index;
    }

    // --- gold ---------------------------------------------------------------------
    /// <summary>The treasury (u32). Clamped to a safe positive range on write.</summary>
    public long Gold
    {
        get => Bytes.U32(_data, _base + SaveFormat.Nat_Gold);
        set => Bytes.WriteU32(_data, _base + SaveFormat.Nat_Gold, Math.Clamp(value, 0, SaveFormat.GoldCap));
    }

    // --- tax ----------------------------------------------------------------------
    /// <summary>Tax rate, percent. Clamped to 0..99.</summary>
    public int TaxRate
    {
        get => Bytes.U8(_data, _base + SaveFormat.Nat_TaxRate);
        set => Bytes.WriteU8(_data, _base + SaveFormat.Nat_TaxRate, Math.Clamp(value, 0, SaveFormat.MaxTaxRate));
    }

    // --- liberty bells / crosses --------------------------------------------------
    public int LibertyBells
    {
        get => Bytes.U16(_data, _base + SaveFormat.Nat_LibertyBellsTotal);
        set => Bytes.WriteU16(_data, _base + SaveFormat.Nat_LibertyBellsTotal, Math.Clamp(value, 0, ushort.MaxValue));
    }

    public int Crosses
    {
        get => Bytes.U16(_data, _base + SaveFormat.Nat_Crosses);
        set => Bytes.WriteU16(_data, _base + SaveFormat.Nat_Crosses, Math.Clamp(value, 0, ushort.MaxValue));
    }

    // --- founding fathers ---------------------------------------------------------
    /// <summary>The 32-bit acquired-Fathers bitfield.</summary>
    private long FathersBits
    {
        get => Bytes.U32(_data, _base + SaveFormat.Nat_FoundingFathers);
        set => Bytes.WriteU32(_data, _base + SaveFormat.Nat_FoundingFathers, value);
    }

    public int FoundingFatherCount
    {
        get => Bytes.U16(_data, _base + SaveFormat.Nat_FoundingFatherCount);
        set => Bytes.WriteU16(_data, _base + SaveFormat.Nat_FoundingFatherCount, Math.Clamp(value, 0, ushort.MaxValue));
    }

    /// <summary>Number of Father bits in the field (bit 18 is a dead slot, but still a valid index).</summary>
    private const int FatherBitCount = 25;

    /// <summary>Whether Father <paramref name="bit"/> (0..24) is acquired.</summary>
    public bool HasFather(int bit)
    {
        RangeCheckBit(bit);
        return (FathersBits & (1L << bit)) != 0;
    }

    /// <summary>Sets or clears Father <paramref name="bit"/> and keeps <see cref="FoundingFatherCount"/> consistent.</summary>
    public void SetFather(int bit, bool acquired)
    {
        RangeCheckBit(bit);
        long bits = FathersBits;
        if (acquired) bits |= 1L << bit;
        else bits &= ~(1L << bit);
        FathersBits = bits;
        SyncFatherCount(bits);
    }

    private static void RangeCheckBit(int bit)
    {
        // Guard the shift: 1L << bit only behaves for bit in 0..63, and only 0..24 are real Fathers.
        if (bit < 0 || bit >= FatherBitCount)
            throw new ArgumentOutOfRangeException(nameof(bit), "founding-father bit must be 0..24.");
    }

    /// <summary>Grants every real Father (all bits except the game's dead slot 18) at once.</summary>
    public void GrantAllFathers()
    {
        long bits = 0;
        foreach (var f in FoundingFatherBook.Fathers)
            if (f.Category != "—") bits |= 1L << f.Bit;
        FathersBits = bits;
        SyncFatherCount(bits);
    }

    /// <summary>Removes every Father.</summary>
    public void ClearAllFathers()
    {
        FathersBits = 0;
        FoundingFatherCount = 0;
    }

    private void SyncFatherCount(long bits) => FoundingFatherCount = System.Numerics.BitOperations.PopCount((ulong)bits);

    // --- boycotts -----------------------------------------------------------------
    /// <summary>The per-good boycott bitmap (one bit per good). Set 0 to lift all boycotts.</summary>
    public int Boycotts
    {
        get => Bytes.U16(_data, _base + SaveFormat.Nat_BoycottBitmap);
        set => Bytes.WriteU16(_data, _base + SaveFormat.Nat_BoycottBitmap, value & 0xFFFF);
    }

    public bool HasAnyBoycott => Boycotts != 0;

    /// <summary>Clears every good boycott.</summary>
    public void ClearBoycotts() => Boycotts = 0;
}
