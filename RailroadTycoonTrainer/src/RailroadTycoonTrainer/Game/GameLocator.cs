namespace RailroadTycoonTrainer.Game;

/// <summary>The located data segment and the addresses derived from it.</summary>
public sealed class GameLocation
{
    /// <summary>Host address (in the attached emulator) of the guest <c>DGROUP:0000</c>.</summary>
    public nuint DgroupBase { get; }

    /// <summary>Cash value (in $1,000s) read at location time.</summary>
    public int CashThousands { get; }

    /// <summary>Current game year read at location time.</summary>
    public int Year { get; }

    public GameLocation(nuint dgroupBase, int cashThousands, int year)
    {
        DgroupBase = dgroupBase;
        CashThousands = cashThousands;
        Year = year;
    }

    /// <summary>Host address of the player's cash word (signed int16, units of $1,000).</summary>
    public nuint CashAddress => DgroupBase + (nuint)RtLayout.CashOffset;

    /// <summary>Host address of the current-year word (uint16).</summary>
    public nuint YearAddress => DgroupBase + (nuint)RtLayout.YearOffset;

    /// <summary>Dollars represented by <see cref="CashThousands"/>.</summary>
    public long CashDollars => RtLayout.ThousandsToDollars(CashThousands);
}

/// <summary>
/// Auto-locates Railroad Tycoon's data segment (DGROUP) inside the attached emulator's memory and, from
/// it, the player's cash — with <b>no value scan</b>. It scans the whole host process for the anchor
/// string (the guest's conventional RAM is mapped verbatim, so the string appears at
/// <c>hostBase + guestLinear</c>); each hit implies a candidate DGROUP base of
/// <c>hit − <see cref="RtLayout.AnchorOffset"/></c>, which is accepted only if a second independent
/// label string also sits at its known offset from that base. This is the "one click, no scanning"
/// path the user asked for. If nothing validates — a different EXE build, or the game isn't loaded —
/// <see cref="Locate"/> returns null and the caller falls back to the value scanner, which is immune to
/// build shifts.
/// </summary>
public sealed class GameLocator
{
    private readonly ProcessMemory _mem;

    public GameLocator(ProcessMemory mem) => _mem = mem;

    /// <summary>Finds the data segment and reads cash, or null if it can't be validated.</summary>
    public GameLocation? Locate(CancellationToken ct = default)
    {
        int windowLen = RtLayout.ValidationWindowBytes;
        // Reuse the shared region scanner (as WarOfTheLance/BattleTech do) rather than a local copy,
        // so a fix to the scan lands once in Common. The anchor appears once in decompressed DGROUP.
        foreach (var anchorHit in BytePatternScanner.Find(_mem, RtLayout.AnchorBytes, ct).Addresses)
        {
            if (anchorHit < (nuint)RtLayout.AnchorOffset) continue;
            nuint dgroupBase = anchorHit - (nuint)RtLayout.AnchorOffset;

            // Validate: both label strings must sit at their known DGROUP offsets from this base.
            byte[] window = _mem.Read(dgroupBase, windowLen);
            if (window.Length < windowLen || !RtLayout.ValidateSegment(window)) continue;

            // Strengthen the match: the year global (same DGROUP) must hold a plausible calendar year.
            byte[] yearBytes = _mem.Read(dgroupBase + (nuint)RtLayout.YearOffset, RtLayout.YearBytes);
            if (yearBytes.Length < RtLayout.YearBytes) continue;
            int year = yearBytes[0] | (yearBytes[1] << 8);
            if (!RtLayout.IsPlausibleYear(year)) continue;

            byte[] cashBytes = _mem.Read(dgroupBase + (nuint)RtLayout.CashOffset, RtLayout.CashBytes);
            if (cashBytes.Length < RtLayout.CashBytes) continue;
            short cash = (short)(cashBytes[0] | (cashBytes[1] << 8));
            return new GameLocation(dgroupBase, cash, year);
        }
        return null;
    }
}
