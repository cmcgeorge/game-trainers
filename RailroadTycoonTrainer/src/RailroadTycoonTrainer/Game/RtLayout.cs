using System.Text;

namespace RailroadTycoonTrainer.Game;

/// <summary>
/// Fixed memory-layout facts for locating the player's live game state inside Railroad Tycoon
/// (MicroProse, 1990 — our copy is <c>GAME.EXE</c> v455.00, 197,182 bytes, EXEPACK-packed), recovered
/// by live reverse-engineering under DOSBox-X (see <c>docs/RailroadTycoon-ReverseEngineering.md</c>).
///
/// Railroad Tycoon is a real-mode DOS program the trainer reaches through the emulator: the DOS guest's
/// conventional RAM is mapped verbatim somewhere inside the DOSBox host process, so a guest linear
/// address <c>L</c> appears in host memory at <c>hostGuestBase + L</c>. The game is a large-model
/// Microsoft-C build whose <c>DS</c> equals its data group (DGROUP); every global therefore sits at a
/// <em>fixed DGROUP-relative offset</em> for this EXE build. Because those offsets are constant, the
/// player's cash is reachable without any per-value scan: find DGROUP once (by anchoring on a static
/// string whose DGROUP offset is known) and read <c>DGROUP + <see cref="CashOffset"/></c>.
///
/// The absolute DS <i>segment</i> value varies between runs (DOS load address depends on resident
/// drivers), but it cancels out of the arithmetic: the anchor string and the cash sit at the same
/// constant DGROUP offsets whatever DS happens to be, so <c>hostBase = anchorHit − anchorOffset</c>
/// yields the host address of <c>DGROUP:0000</c> directly. Everything here is specific to this build,
/// which is why the locator <b>validates</b> a second independent string before trusting a hit and the
/// trainer keeps a value scanner as the build-independent fallback.
/// </summary>
public static class RtLayout
{
    // --- the cash global (the headline value) --------------------------------------------------
    /// <summary>
    /// DGROUP offset of the player's treasury. It is a signed 16-bit little-endian word in
    /// <b>units of $1,000</b> — the game appends "000" on screen, so a stored 1000 shows as
    /// "$1,000,000". <b>[Confirmed]</b> live: writing distinct values here drove the on-screen cash
    /// panel (VGA, Eastern US 1830, Investor). Note this build (GAME.EXE v455.00) places cash at 0x957A;
    /// an independent disassembly of a different build reported 0x95AA — a build shift the value scanner
    /// is immune to. A static disassembly of our own binary could not isolate cash (money is never
    /// printf-formatted and is not a discrete field in the save), so the live write-test is the
    /// authority here.
    /// </summary>
    public const int CashOffset = 0x957A;

    /// <summary>Bytes of the cash field (a 16-bit word).</summary>
    public const int CashBytes = 2;

    /// <summary>
    /// The game clamps cash to <c>0x7530</c> = 30000 = $30,000,000 during its own accounting. Freezing
    /// at or below this avoids a clamp/overflow fight with the fiscal tick; the raw field is a signed
    /// word so it will <i>hold</i> a larger poke at rest, but the next accounting pass may snap it back.
    /// </summary>
    public const int CashCapThousands = 0x7530;   // 30000 → $30,000,000

    /// <summary>A comfortable "max cash" the trainer offers: the game's own $30M ceiling.</summary>
    public const int MaxCashThousands = CashCapThousands;

    // --- the game-clock year ------------------------------------------------------------------
    /// <summary>
    /// DGROUP offset of the current calendar year (an unsigned 16-bit word, e.g. 1830). <b>[Confirmed]</b>
    /// both ways: read live as 0x0726 = 1830 at game start, and identified independently in a static
    /// disassembly as the year global (the era gates at 1800/1830/1865 test it). Freezing it stops the
    /// calendar — the difficulty's year limit can't end the game — while the seasons keep turning.
    /// </summary>
    public const int YearOffset = 0x96C0;

    /// <summary>Bytes of the year field (a 16-bit word).</summary>
    public const int YearBytes = 2;

    /// <summary>A plausible in-game year — used to strengthen the locator's validation.</summary>
    public static bool IsPlausibleYear(int year) => year is >= 1800 and <= 2100;

    // --- DGROUP string anchors (fixed offsets in this build's initialised data) -----------------
    // Two distinctive financial-report label literals. Anchoring on one and validating the other means
    // a stray match can't masquerade as the live segment. The on-disk EXE is EXEPACK-compressed, so
    // these plaintext strings exist only once in memory — in the decompressed guest DGROUP — which
    // makes even a single hit reliable; the second string is belt-and-suspenders. [Confirmed] offsets.

    /// <summary>Anchor literal — its bytes locate a candidate DGROUP base.</summary>
    public static readonly byte[] AnchorBytes = Encoding.ASCII.GetBytes("Outstanding Loans: ");
    /// <summary>DGROUP offset of <see cref="AnchorBytes"/>.</summary>
    public const int AnchorOffset = 0x24A8;

    /// <summary>Validation literal — must sit at its known offset from the candidate base.</summary>
    public static readonly byte[] ValidateBytes = Encoding.ASCII.GetBytes("Stockholders Equity: ");
    /// <summary>DGROUP offset of <see cref="ValidateBytes"/>.</summary>
    public const int ValidateOffset = 0x24BC;

    // --- conversions ---------------------------------------------------------------------------
    /// <summary>Dollars shown for a stored thousands value.</summary>
    public static long ThousandsToDollars(long thousands) => thousands * 1000L;

    /// <summary>Stored thousands value for a dollar amount (rounded down to the nearest $1,000).</summary>
    public static int DollarsToThousands(long dollars) => (int)(dollars / 1000L);

    // --- pure validation helpers (unit-tested; no process access) ------------------------------
    /// <summary>
    /// Whether the bytes at a candidate base carry both label strings at their known offsets — i.e. this
    /// really is the game's data segment. <paramref name="window"/> must start at the candidate DGROUP
    /// base and cover at least <see cref="ValidateOffset"/> + the validation string.
    /// </summary>
    public static bool ValidateSegment(ReadOnlySpan<byte> window)
    {
        if (!MatchAt(window, AnchorOffset, AnchorBytes)) return false;
        if (!MatchAt(window, ValidateOffset, ValidateBytes)) return false;
        return true;
    }

    /// <summary>How many bytes a validation window needs (through the end of the validation string).</summary>
    public static int ValidationWindowBytes => ValidateOffset + ValidateBytes.Length;

    private static bool MatchAt(ReadOnlySpan<byte> window, int offset, byte[] needle)
    {
        if (offset < 0 || offset + needle.Length > window.Length) return false;
        return window.Slice(offset, needle.Length).SequenceEqual(needle);
    }
}
