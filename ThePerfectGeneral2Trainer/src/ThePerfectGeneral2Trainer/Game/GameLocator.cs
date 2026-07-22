using System.Text;

namespace ThePerfectGeneral2Trainer.Game;

/// <summary>The result of auto-locating The Perfect General II's purchase state in guest RAM.</summary>
public sealed class LocatedState
{
    /// <summary>Address of the <c>D:\ICONS\MSGR.DAT</c> anchor string, or 0 if not found.</summary>
    public nuint AnchorAddress { get; init; }

    /// <summary>Address of the 16-byte purchased-unit count array, or 0.</summary>
    public nuint CountArrayAddress { get; init; }

    /// <summary>Address of the Buy Points Remaining scalar (Int16 LE), or 0.</summary>
    public nuint BuyPointsAddress { get; init; }

    /// <summary>Address of the Units Purchased scalar (Int16 LE), or 0.</summary>
    public nuint UnitsPurchasedAddress { get; init; }

    /// <summary>The 16 count-array bytes read at location time.</summary>
    public byte[] CountArray { get; init; } = Array.Empty<byte>();

    /// <summary>Buy Points Remaining value read at location time.</summary>
    public short BuyPoints { get; init; }

    /// <summary>Units Purchased value read at location time.</summary>
    public short UnitsPurchased { get; init; }

    /// <summary>True when the anchor string was found (the game is loaded in the emulator).</summary>
    public bool AnchorFound => AnchorAddress != 0;

    /// <summary>True when the purchase screen is active (count array validated against Units Purchased).</summary>
    public bool PurchaseScreenActive { get; init; }

    /// <summary>Whether coverage was incomplete (a per-region cap was hit during the scan).</summary>
    public bool Truncated { get; init; }
}

/// <summary>
/// Finds The Perfect General II's live purchase state in the emulator's guest RAM by scanning for a
/// constant ASCII file-path string the game loads into its DPMI heap, then deriving the count array,
/// Buy Points, and Units Purchased addresses at fixed offsets from that anchor. Addresses are
/// discovered every session — nothing is hard-coded (the repo-wide rule).
///
/// <para>The anchor <c>D:\ICONS\MSGR.DAT</c> was found exactly once in each of two full DOSBox-X
/// process dumps (~400 MB each) across two game states (purchase screen and round start). The
/// purchase count array sits 0x16E bytes before the anchor; Buy Points Remaining (Int16 LE) sits
/// 0x2E0 bytes before; Units Purchased (Int16 LE) sits 0x2E2 bytes before. All offsets were
/// byte-verified against the purchase-screen dump: count array <c>03 02 01 04 …</c> (sum 36),
/// Buy Points 39, Units Purchased 36 — matching <c>.data/memdump.md</c> exactly.</para>
///
/// <para>When the game is not on the purchase screen (e.g. during battle), the count-array area is
/// overwritten with DPMI far-pointer soup; the <see cref="LooksLikePurchaseScreen"/> validator
/// rejects that by checking each count byte is ≤ 100 and the array sum equals Units Purchased,
/// so the UI can tell the user to navigate to the purchase screen.</para>
/// </summary>
public static class GameLocator
{
    /// <summary>
    /// The anchor: the ASCII file-path string <c>D:\ICONS\MSGR.DAT</c> (17 bytes + NUL = 18). The game
    /// loads this into its DPMI heap at run time; it is constant across sessions and unique in the
    /// emulator process memory. <b>Confirmed</b> by byte-level search of both memory dumps.
    /// </summary>
    public static readonly byte[] Anchor = Encoding.ASCII.GetBytes("D:\\ICONS\\MSGR.DAT\0");

    /// <summary>Offset from the anchor to the purchased-unit count array (16 bytes, one per type).</summary>
    public const int AnchorToCountArray = -0x16E;

    /// <summary>Offset from the anchor to Buy Points Remaining (Int16 LE).</summary>
    public const int AnchorToBuyPoints = -0x2E0;

    /// <summary>Offset from the anchor to Units Purchased (Int16 LE).</summary>
    public const int AnchorToUnitsPurchased = -0x2E2;

    /// <summary>Maximum plausible per-type purchase count (rejects far-pointer bytes like 0xDF).</summary>
    private const int MaxCountPerType = 100;

    /// <summary>Maximum plausible Units Purchased total.</summary>
    private const int MaxUnitsPurchased = 1000;

    /// <summary>
    /// Scans the attached process for the anchor string and, if found, reads and validates the
    /// purchase state at the derived offsets. Returns a <see cref="LocatedState"/> whose
    /// <see cref="LocatedState.AnchorFound"/> is true when the game is loaded and whose
    /// <see cref="LocatedState.PurchaseScreenActive"/> is true when the purchase screen is current.
    /// </summary>
    public static LocatedState Locate(ProcessMemory mem, CancellationToken ct = default)
    {
        var scan = BytePatternScanner.Find(mem, Anchor, ct);
        if (scan.Addresses.Count == 0)
            return new LocatedState { Truncated = scan.Truncated };

        // Try all addresses; pick the first that looks like a purchase screen.
        // If none do, fall back to the first address so the UI can at least show
        // "anchor found but not on purchase screen".
        LocatedState? bestState = null;

        foreach (nuint anchor in scan.Addresses)
        {
            nuint countAddr   = unchecked((nuint)((nint)anchor + AnchorToCountArray));
            nuint buyPtsAddr  = unchecked((nuint)((nint)anchor + AnchorToBuyPoints));
            nuint unitsAddr   = unchecked((nuint)((nint)anchor + AnchorToUnitsPurchased));

            var counts = mem.Read(countAddr, PurchaseFormat.TypeCount);
            short buyPoints = ReadInt16(mem, buyPtsAddr);
            short unitsPurchased = ReadInt16(mem, unitsAddr);

            bool active = LooksLikePurchaseScreen(counts, unitsPurchased);

            var state = new LocatedState
            {
                AnchorAddress = anchor,
                CountArrayAddress = countAddr,
                BuyPointsAddress = buyPtsAddr,
                UnitsPurchasedAddress = unitsAddr,
                CountArray = counts,
                BuyPoints = buyPoints,
                UnitsPurchased = unitsPurchased,
                PurchaseScreenActive = active,
                Truncated = scan.Truncated,
            };

            if (active) return state;
            bestState ??= state;
        }

        return bestState!;
    }

    /// <summary>
    /// Validates that 16 bytes look like a live purchase count array: every byte ≤
    /// <see cref="MaxCountPerType"/>, at least one non-zero, and the sum equals the Units Purchased
    /// scalar. This rejects the DPMI far-pointer soup that overwrites the area when the game is not
    /// on the purchase screen.
    /// </summary>
    public static bool LooksLikePurchaseScreen(byte[] counts, short unitsPurchased)
    {
        if (counts.Length != PurchaseFormat.TypeCount) return false;
        if (unitsPurchased < 0 || unitsPurchased > MaxUnitsPurchased) return false;

        int sum = 0;
        bool anyNonZero = false;
        foreach (byte b in counts)
        {
            if (b > MaxCountPerType) return false;
            if (b != 0) anyNonZero = true;
            sum += b;
        }

        return anyNonZero && sum == unitsPurchased;
    }

    private static short ReadInt16(ProcessMemory mem, nuint address)
    {
        var buf = mem.Read(address, 2);
        if (buf.Length < 2) return 0;
        return (short)(buf[0] | (buf[1] << 8));
    }
}
