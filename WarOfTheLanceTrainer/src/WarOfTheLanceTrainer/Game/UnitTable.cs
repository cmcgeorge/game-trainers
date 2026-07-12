namespace WarOfTheLanceTrainer.Game;

/// <summary>
/// One placed-unit slot from a <c>.UNT</c> file (WL.UNT / SCEN.UNT). Each slot is four bytes:
/// <code>
///   +0  u8  X          map column (0xFF when the slot is empty)
///   +1  u8  Y          map row    (0xFF when the slot is empty)
///   +2  u8  TypeCode    engine unit-type / owner code (raw; see .docs RE notes)
///   +3  u8  Flag        always 0x05 on shipped files (an "in play" marker)
/// </code>
/// Verified across WL.UNT and SCEN.UNT: 400 slots, 1600-byte payload, every occupied slot carries
/// flag 0x05, and empty slots are 0xFF 0xFF.
/// </summary>
public readonly struct UnitSlot
{
    public const int SlotSize = 4;
    public const byte LiveFlag = 0x05;

    public byte X { get; }
    public byte Y { get; }
    public byte TypeCode { get; }
    public byte Flag { get; }

    public UnitSlot(byte x, byte y, byte typeCode, byte flag)
    {
        X = x; Y = y; TypeCode = typeCode; Flag = flag;
    }

    /// <summary>True when the slot holds no unit (both coordinates 0xFF).</summary>
    public bool IsEmpty => X == GameFacts.EmptySlot && Y == GameFacts.EmptySlot;
}

/// <summary>
/// Parses a <c>.UNT</c> file's 400-slot, 4-bytes-per-slot table out of the container payload and
/// exposes the occupied slots. Pure data layer — no process or UI dependency.
/// </summary>
public static class UnitTable
{
    public const int SlotCount = 400;
    public const int TableBytes = SlotCount * UnitSlot.SlotSize;   // 1600

    /// <summary>Reads all occupied slots from a whole <c>.UNT</c> file (7-byte container + payload).</summary>
    public static List<UnitSlot> ParseFile(byte[] file)
    {
        var container = SaveContainer.ParseValidated(file);
        return ParsePayload(container.Payload());
    }

    /// <summary>
    /// Reads all occupied slots from a raw 1600-byte payload. The <c>.UNT</c> table is a fixed
    /// 400-slot format, so a payload of any other length is rejected rather than partially parsed —
    /// a truncated or padded buffer means the layout no longer matches and must not look valid.
    /// </summary>
    public static List<UnitSlot> ParsePayload(byte[] payload)
    {
        if (payload.Length != TableBytes)
            throw new ArgumentException(
                $"A WOTL .UNT payload must be exactly {TableBytes} bytes (got {payload.Length}).", nameof(payload));

        var slots = new List<UnitSlot>();
        for (int i = 0; i < SlotCount; i++)
        {
            int o = i * UnitSlot.SlotSize;
            var slot = new UnitSlot(payload[o], payload[o + 1], payload[o + 2], payload[o + 3]);
            if (!slot.IsEmpty) slots.Add(slot);
        }
        return slots;
    }
}
