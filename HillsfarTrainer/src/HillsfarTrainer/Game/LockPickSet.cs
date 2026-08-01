namespace HillsfarTrainer.Game;

/// <summary>
/// One of the twelve five-byte lock-pick slots.
/// </summary>
/// <param name="Slot">Index 0..11.</param>
/// <param name="ShapeA">First shape/sprite index — always <see cref="ShapeD"/> plus 20 in shipped data.</param>
/// <param name="ShapeB">Second shape index.</param>
/// <param name="ShapeC">Third shape index — always <see cref="ShapeB"/> plus 20 in shipped data.</param>
/// <param name="ShapeD">Fourth shape index.</param>
/// <param name="State">
/// The fifth byte: 0, 2 or 3 in every shipped record. Either a count or a condition — the manual
/// distinguishes present, broken and absent picks. Inferred.
/// </param>
public readonly record struct LockPick(
    int Slot, byte ShapeA, byte ShapeB, byte ShapeC, byte ShapeD, byte State)
{
    /// <summary>True when this slot holds a usable pick, i.e. its state byte is non-zero.</summary>
    public bool IsPresent => State != 0;

    /// <summary>
    /// True when the slot's four shape bytes hold the exact <c>+20</c> pairing seen in every shipped
    /// record. A slot that fails this is either empty or not pick data.
    /// </summary>
    public bool HasExpectedGeometry =>
        ShapeC == (byte)(ShapeB + LockPickSet.ShapePairDelta) &&
        ShapeA == (byte)(ShapeD + LockPickSet.ShapePairDelta);
}

/// <summary>
/// The lock-pick block at <see cref="CharacterFormat.OffLockPicks"/>: twelve records of five bytes.
///
/// <para>The stride is not a guess — the game's own initialiser computes <c>si = index * 5</c> after
/// seeding the first slot. Within a slot the four shape bytes are two values and their <c>+20</c>
/// counterparts: across all 24 slots of the two shipped thieves,
/// <c>byte2 - byte1 == 20</c> and <c>byte0 - byte3 == 20</c> hold exactly. That pairing matches a
/// pick having two ends — the manual has you flip a pick over — and their tumbler shapes.</para>
///
/// <para>The trainer reads and displays this block and can fill the state bytes, but does not
/// synthesise shape values: the shapes decide which tumblers a pick fits, and inventing them could
/// produce a set that opens nothing. Buy picks at the guild for a properly-generated set.</para>
/// </summary>
public static class LockPickSet
{
    /// <summary>Number of pick slots.</summary>
    public const int SlotCount = 12;

    /// <summary>Bytes per slot.</summary>
    public const int SlotLength = 5;

    /// <summary>Total length of the block.</summary>
    public const int BlockLength = SlotCount * SlotLength;

    /// <summary>The constant difference between each paired shape byte in shipped data.</summary>
    public const int ShapePairDelta = 20;

    /// <summary>Offset of the state byte within a slot.</summary>
    public const int StateOffset = 4;

    /// <summary>The highest state value seen in shipped records.</summary>
    public const byte MaxState = 3;

    /// <summary>Decodes all twelve slots from a record.</summary>
    public static IReadOnlyList<LockPick> Read(ReadOnlySpan<byte> record)
    {
        var picks = new List<LockPick>(SlotCount);
        if (record.Length < CharacterFormat.OffLockPicks + BlockLength) return picks;
        for (int i = 0; i < SlotCount; i++)
        {
            int at = CharacterFormat.OffLockPicks + i * SlotLength;
            picks.Add(new LockPick(i, record[at], record[at + 1], record[at + 2],
                                   record[at + 3], record[at + StateOffset]));
        }
        return picks;
    }

    /// <summary>How many slots currently hold a usable pick.</summary>
    public static int CountPresent(ReadOnlySpan<byte> record)
    {
        int n = 0;
        foreach (var p in Read(record)) if (p.IsPresent) n++;
        return n;
    }

    /// <summary>
    /// How many slots hold pick shape data at all, whatever their condition.
    ///
    /// <para>This is what distinguishes "this character owns no picks" from "every pick is already in
    /// good condition" — both of which leave <see cref="RepairAll"/> with nothing to change, and which
    /// need opposite messages.</para>
    /// </summary>
    public static int CountWithGeometry(ReadOnlySpan<byte> record)
    {
        int n = 0;
        foreach (var p in Read(record)) if (!IsEmpty(p)) n++;
        return n;
    }

    /// <summary>True when a slot holds no shape data at all.</summary>
    private static bool IsEmpty(LockPick p) =>
        p.ShapeA == 0 && p.ShapeB == 0 && p.ShapeC == 0 && p.ShapeD == 0;

    /// <summary>
    /// True when every slot that has pick geometry follows the <c>+20</c> pairing — a cheap sanity
    /// check that the block really is pick data. Slots whose shape bytes are all zero are ignored,
    /// since a character who has never owned picks has an empty block.
    /// </summary>
    public static bool GeometryLooksRight(ReadOnlySpan<byte> record)
    {
        foreach (var p in Read(record))
            if (!IsEmpty(p) && !p.HasExpectedGeometry) return false;
        return true;
    }

    /// <summary>
    /// Sets the state byte of every slot that already has pick geometry to
    /// <see cref="MaxState"/> — "repair all my picks" without inventing any shapes.
    /// Returns how many slots were changed.
    /// </summary>
    public static int RepairAll(byte[] record, Action<int, int>? flush = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Length < CharacterFormat.OffLockPicks + BlockLength) return 0;

        int changed = 0;
        foreach (var p in Read(record))
        {
            if (IsEmpty(p) || p.State == MaxState) continue;
            int at = CharacterFormat.OffLockPicks + p.Slot * SlotLength + StateOffset;
            record[at] = MaxState;
            flush?.Invoke(at, 1);
            changed++;
        }
        return changed;
    }
}
