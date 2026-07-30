using System.Text;

namespace PiratesTrainer.Game;

/// <summary>One settlement read out of the running game's city table.</summary>
/// <param name="Index">Position in the era's table.</param>
/// <param name="Name">Name as stored (twelve columns, trailing spaces trimmed).</param>
/// <param name="Nation">Owning power, decoded from record byte 3.</param>
/// <param name="Forts">Forts guarding the approach (record byte 4, low nibble).</param>
/// <param name="Soldiers">Garrison, record byte 5 x 10.</param>
/// <param name="Citizens">Population, (record byte 6 + 1) x 100.</param>
/// <param name="GoldThousands">Treasury in thousands of gold pieces (record byte 7).</param>
/// <param name="Address">Host address of the record's first byte, so a caller can pin a field.</param>
public sealed record LiveCity(
    int Index, string Name, string Nation, int Forts, int Soldiers,
    int Citizens, int GoldThousands, nuint Address)
{
    /// <summary>Treasury in gold pieces.</summary>
    public int Gold => GoldThousands * 1000;

    /// <summary>Host address of this record's gold byte, the useful thing to pin.</summary>
    public nuint GoldAddress => Address + 7;
}

/// <summary>The located data segment and everything read from it at location time.</summary>
public sealed class GameLocation
{
    /// <summary>Host address (inside the attached emulator) of the guest's <c>DGROUP:0000</c>.</summary>
    public nuint DgroupBase { get; }

    /// <summary>Gold read at location time.</summary>
    public int Gold { get; }

    /// <summary>
    /// Era <b>code</b> read at location time — 0, 2, 3, 4, 5 or 6, <em>not</em> a 0-5 index. Pass it
    /// through <see cref="PiratesLayout.EraIndexFromCode"/> before indexing <see cref="CityBook.ByEra"/>.
    /// </summary>
    public int EraCode { get; }

    /// <summary>Calendar year the game would display at location time.</summary>
    public int Year { get; }

    /// <summary>Month index 0-11 read at location time.</summary>
    public int Month { get; }

    /// <summary>The era's settlement table as it stands right now (empty if it did not validate).</summary>
    public IReadOnlyList<LiveCity> Cities { get; }

    public GameLocation(nuint dgroupBase, int gold, int eraCode, int year, int month, IReadOnlyList<LiveCity> cities)
    {
        DgroupBase = dgroupBase;
        Gold = gold;
        EraCode = eraCode;
        Year = year;
        Month = month;
        Cities = cities;
    }

    /// <summary>Host address of a DGROUP offset.</summary>
    public nuint AddressOf(int dgroupOffset) => DgroupBase + (nuint)dgroupOffset;

    /// <summary>Host address of the player's gold word.</summary>
    public nuint GoldAddress => AddressOf(PiratesLayout.GoldOffset);
}

/// <summary>
/// Auto-locates Pirates!'s data segment inside the attached emulator's memory and, from it, the player's
/// state — with <b>no value scan</b>.
///
/// DOSBox maps the DOS guest's conventional RAM verbatim into its own address space, so a guest linear
/// address <c>L</c> appears at <c>hostGuestBase + L</c>. <c>DISKP</c> is a flat real-mode image whose data
/// group is at a fixed paragraph within it, so every global sits at a constant DGROUP offset. Scanning
/// the host process for the title-screen copyright literal therefore yields a candidate
/// <c>DGROUP:0000 = hit − <see cref="PiratesLayout.AnchorOffset"/></c>.
///
/// A candidate is accepted only if <em>all three</em> anchor literals sit at their known offsets and the
/// derived era, year and settlement table all look sane. That matters more here than in a packed-EXE
/// target: <c>DISKP</c> is stored uncompressed and its disk images sit on the host filesystem, so a naive
/// single-string match could in principle land on a buffered copy rather than the live segment.
///
/// If nothing validates — the game is not running, or is still in the loader — <see cref="Locate"/>
/// returns null and the caller falls back to the value scanner, which does not care about layout at all.
/// </summary>
public sealed class GameLocator
{
    private readonly ProcessMemory _mem;

    public GameLocator(ProcessMemory mem) => _mem = mem;

    /// <summary>Finds the data segment and reads the headline state, or null if nothing validates.</summary>
    public GameLocation? Locate(CancellationToken ct = default)
    {
        int windowLen = PiratesLayout.ValidationWindowBytes;

        foreach (var anchorHit in BytePatternScanner.Find(_mem, PiratesLayout.AnchorBytes, ct).Addresses)
        {
            ct.ThrowIfCancellationRequested();
            if (anchorHit < (nuint)PiratesLayout.AnchorOffset) continue;
            nuint dgroupBase = anchorHit - (nuint)PiratesLayout.AnchorOffset;

            // 1. All three static literals must sit at their known DGROUP offsets.
            byte[] window = _mem.Read(dgroupBase, windowLen);
            if (window.Length < windowLen || !PiratesLayout.ValidateSegment(window)) continue;

            // 2. The era byte must name one of the six time periods, and the clock must be in range.
            if (!TryReadByte(dgroupBase, PiratesLayout.EraOffset, out int eraCode)) continue;
            if (!PiratesLayout.IsPlausibleEra(eraCode)) continue;
            if (!TryReadWord(dgroupBase, PiratesLayout.YearsElapsedOffset, out int yearsElapsed)) continue;
            if (!TryReadWord(dgroupBase, PiratesLayout.DayOfYearOffset, out int dayOfYear)) continue;
            if (dayOfYear >= PiratesLayout.DaysPerYear) continue;
            int year = PiratesLayout.DisplayYear(eraCode, yearsElapsed);
            if (!PiratesLayout.IsPlausibleYear(year)) continue;

            // 3. The settlement table must decode — the strongest signal that this is the live segment
            //    and not some buffered copy of the program image, because the table is loaded from disk
            //    at run time and only exists once the player has picked an era.
            var cities = ReadCities(dgroupBase);
            if (cities.Count == 0) continue;

            if (!TryReadWord(dgroupBase, PiratesLayout.MonthOffset, out int month)) continue;
            if (!TryReadWord(dgroupBase, PiratesLayout.GoldOffset, out int gold)) continue;

            return new GameLocation(dgroupBase, gold, eraCode, year, month, cities);
        }
        return null;
    }

    /// <summary>
    /// Reads the era's settlement table from a candidate base, stopping at the first record that does not
    /// look like one. Returns an empty list if even the first record fails, which is what disqualifies a
    /// candidate base.
    /// </summary>
    public IReadOnlyList<LiveCity> ReadCities(nuint dgroupBase)
    {
        var list = new List<LiveCity>();
        byte[] table = _mem.Read(dgroupBase + (nuint)PiratesLayout.CityTableOffset,
                                 PiratesLayout.MaxCities * PiratesLayout.CityRecordBytes);
        for (int i = 0; i < PiratesLayout.MaxCities; i++)
        {
            int at = i * PiratesLayout.CityRecordBytes;
            if (at + PiratesLayout.CityRecordBytes > table.Length) break;
            var record = table.AsSpan(at, PiratesLayout.CityRecordBytes);
            if (!PiratesLayout.LooksLikeCityRecord(record)) break;
            list.Add(new LiveCity(
                Index: i,
                Name: PiratesLayout.CityName(record),
                Nation: NationName(record[3]),
                Forts: record[4] & 0x0F,
                Soldiers: record[5] * 10,
                Citizens: (record[6] + 1) * 100,
                GoldThousands: record[7],
                Address: dgroupBase + (nuint)(PiratesLayout.CityTableOffset + at)));
        }
        return list;
    }

    /// <summary>Reads the player's family name out of the display-string table.</summary>
    public string ReadPlayerName(nuint dgroupBase)
    {
        byte[] raw = _mem.Read(dgroupBase + (nuint)PiratesLayout.PlayerNameOffset, PiratesLayout.PlayerNameLength);
        if (raw.Length < PiratesLayout.PlayerNameLength) return string.Empty;
        var sb = new StringBuilder(raw.Length);
        foreach (byte b in raw)
        {
            if (b is < 32 or > 126) break;
            sb.Append((char)b);
        }
        return sb.ToString().Trim();
    }

    /// <summary>The four nations, in the order the settlement record's nation byte uses.</summary>
    public static string NationName(int nation) => nation switch
    {
        0 => "Spanish",
        1 => "English",
        2 => "French",
        3 => "Dutch",
        _ => "?",
    };

    private bool TryReadByte(nuint baseAddr, int offset, out int value)
    {
        value = 0;
        byte[] b = _mem.Read(baseAddr + (nuint)offset, 1);
        if (b.Length < 1) return false;
        value = b[0];
        return true;
    }

    private bool TryReadWord(nuint baseAddr, int offset, out int value)
    {
        value = 0;
        byte[] b = _mem.Read(baseAddr + (nuint)offset, 2);
        if (b.Length < 2) return false;
        value = b[0] | (b[1] << 8);
        return true;
    }
}
