namespace ImperialismIITrainer.Game;

/// <summary>
/// Fixed memory-layout facts for locating the human player's nation object in the running
/// <c>Imperialism II.exe</c> (GOG / June 1999 build), recovered by live reverse-engineering.
///
/// The game is a native MFC exe with a fixed image base and no ASLR, so its static globals sit at
/// constant addresses every launch, and every field's offset within a class is a compile-time constant.
/// The only thing that varies per session is the <i>heap address</i> of the nation object — and a
/// static global holds a live pointer to it. So the locator chain is:
/// <code>treasury = *( *(playerNationGlobal) + TreasuryOffset )</code>
/// which resolves automatically each session with no value scanning.
///
/// These constants are specific to this build; a different patch would shift them, which is why the
/// locator <b>validates</b> what it finds (vtable in .rdata + plausible treasury + sane warehouse) and
/// the trainer falls back to the value scanner if validation fails.
/// </summary>
public static class NationLayout
{
    // --- module ranges (image base 0x400000, no ASLR); from the PE section table ---------------
    /// <summary>.rdata (read-only data) runtime range — C++ vtables live here.</summary>
    public const uint RdataStart = 0x006E9000;
    public const uint RdataEnd   = 0x00744000;
    /// <summary>.data/.bss (static globals) runtime range — scanned for the nation pointer.</summary>
    public const uint DataStart  = 0x00744000;
    public const uint DataEnd    = 0x0076B000;

    /// <summary>Static globals observed holding a pointer to the human player's nation object.</summary>
    public static readonly uint[] PlayerNationGlobals = { 0x00760650, 0x007606A8 };

    // --- field offsets within the nation object (TCountry / TGreatPower) ------------------------
    /// <summary>Signed 32-bit treasury (goes negative in debt).</summary>
    public const int TreasuryOffset = 0x130;
    /// <summary>First mapped warehouse commodity slot (int16 each).</summary>
    public const int WarehouseBase = 0xDD4;
    /// <summary>How many bytes of the object header the locator reads to validate/resolve.</summary>
    public const int HeaderBytes = 0xE10;

    /// <summary>
    /// Confirmed warehouse commodity slots (offset from object base; each a signed int16). Matched
    /// against a live game: the raw block and the refined block each lined up with the manual's order.
    /// The two near-duplicate pairs (Tin/Copper, both 12; Wool/Paper both 10) are ordered by that same
    /// evidence — treat the labels as best-effort, the offsets as exact.
    /// </summary>
    public static readonly (string Name, int Offset)[] WarehouseSlots =
    {
        ("Wool",      0xDD4), ("Timber",    0xDD6), ("Tin",       0xDD8),
        ("Copper",    0xDDA), ("Iron Ore",  0xDDC),
        ("Fabric",    0xDEA), ("Lumber",    0xDEC), ("Paper",     0xDEE),
        ("Bronze",    0xDF0), ("Cast Iron", 0xDF2),
    };

    // --- pure validation helpers (unit-tested; no process access) ------------------------------
    public static bool IsPlausibleTreasury(long v) => v is >= -100_000_000 and <= 2_000_000_000;

    public static bool LooksLikeVtable(uint ptr) => ptr >= RdataStart && ptr < RdataEnd;

    public static bool LooksLikeHeapPointer(uint p) => p is >= 0x01000000 and < 0x7F000000 && (p & 3) == 0;

    /// <summary>
    /// Whether an object header looks like a nation object: its vtable points into .rdata, the treasury
    /// field is a plausible amount, and the warehouse window is all non-negative small int16s.
    /// </summary>
    public static bool ValidateHeader(ReadOnlySpan<byte> hdr)
    {
        if (hdr.Length < HeaderBytes) return false;
        if (!LooksLikeVtable(BitConverter.ToUInt32(hdr.Slice(0, 4)))) return false;
        if (!IsPlausibleTreasury(BitConverter.ToInt32(hdr.Slice(TreasuryOffset, 4)))) return false;

        int small = 0;
        for (int i = 0; i < 24; i++)
        {
            short s = BitConverter.ToInt16(hdr.Slice(WarehouseBase + i * 2, 2));
            if (s < 0) return false;             // a warehouse stockpile is never negative
            if (s is > 0 and <= 30000) small++;
        }
        return small >= 5;                        // several stocked goods → looks like a real warehouse
    }
}
