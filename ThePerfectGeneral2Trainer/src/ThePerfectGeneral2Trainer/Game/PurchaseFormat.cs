namespace ThePerfectGeneral2Trainer.Game;

/// <summary>One decoded entry of the purchase count array: a unit type and how many were bought.</summary>
public readonly record struct PurchaseCount(int Index, string Type, int Count);

/// <summary>
/// Models the <b>Confirmed</b> purchased-unit count array (see <c>.docs/ReverseEngineering.md</c> §3):
/// 16 contiguous bytes, one per unit type, in purchase-screen order, each a 0–255 tally of how many of
/// that type the player has bought. The <c>GameLocator</c> auto-locator derives the count array address
/// at a fixed offset from the <c>D:\ICONS\MSGR.DAT</c> anchor string and feeds the live bytes through
/// <see cref="Decode"/> to populate the Purchase tab (see <c>.docs/ReverseEngineering.md</c> §6).
///
/// <para>The order here is the <b>purchase-screen order</b>, which differs from the
/// <see cref="UnitReference"/> stat-table order only in where MINE sits (first here, 14th there).</para>
/// </summary>
public static class PurchaseFormat
{
    /// <summary>Number of bytes in the count array (one per unit type).</summary>
    public const int TypeCount = 16;

    /// <summary>Unit types in purchase-screen / count-array order.</summary>
    public static readonly IReadOnlyList<string> TypeOrder = Array.AsReadOnly(new[]
    {
        "Mine",
        "Infantry",
        "Machine Gun",
        "Engineer",
        "Bazooka",
        "Armored Car w/MG",
        "Armored Car",
        "Light Tank",
        "Medium Tank",
        "Heavy Tank",
        "Mobile Artillery",
        "Light Artillery",
        "Heavy Artillery",
        "Plane",
        "Fortification",
        "Elephant Tank",
    });

    /// <summary>
    /// Decodes a 16-byte count array into labelled per-type counts. Throws if the block is not exactly
    /// <see cref="TypeCount"/> bytes so a mis-sized read can never silently mis-align the labels.
    /// </summary>
    public static IReadOnlyList<PurchaseCount> Decode(ReadOnlySpan<byte> block)
    {
        if (block.Length != TypeCount)
            throw new ArgumentException($"Count array must be exactly {TypeCount} bytes.", nameof(block));

        var list = new List<PurchaseCount>(TypeCount);
        for (int i = 0; i < TypeCount; i++)
            list.Add(new PurchaseCount(i, TypeOrder[i], block[i]));
        return list;
    }

    /// <summary>Sums the count array — the "Units Purchased" figure the purchase screen shows.</summary>
    public static int TotalUnits(ReadOnlySpan<byte> block)
    {
        if (block.Length != TypeCount)
            throw new ArgumentException($"Count array must be exactly {TypeCount} bytes.", nameof(block));

        int sum = 0;
        for (int i = 0; i < TypeCount; i++) sum += block[i];
        return sum;
    }
}
