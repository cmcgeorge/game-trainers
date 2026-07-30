using System.Text;

namespace PiratesTrainer.Game;

/// <summary>How firmly an offset in <see cref="PiratesLayout"/> was established.</summary>
public enum Evidence
{
    /// <summary>Proved by a dedicated routine in the disassembly that can only mean this field.</summary>
    Confirmed,

    /// <summary>Consistent with the disassembly but not pinned by a single unambiguous routine.</summary>
    Inferred,
}

/// <summary>
/// A value the trainer can pin straight from the located data segment: its DGROUP offset, its width and
/// how confident the reverse-engineering is.
/// </summary>
/// <param name="Label">Grid label; also the key <see cref="GameLocator"/>-driven code uses to find a pin.</param>
/// <param name="Offset">Offset from <c>DGROUP:0000</c>.</param>
/// <param name="Bytes">Field width in bytes (1 or 2).</param>
/// <param name="Evidence">How the offset was established.</param>
/// <param name="Note">One-line explanation shown as a tooltip / in the reference tab.</param>
public sealed record KnownValue(string Label, int Offset, int Bytes, Evidence Evidence, string Note);

/// <summary>
/// Fixed memory-layout facts for Sid Meier's Pirates! (MicroProse, 1987 — IBM version 432.02), recovered
/// by static reverse-engineering of the shipped <c>DISKP</c> program image. See
/// <c>docs/Pirates-ReverseEngineering.md</c> for the derivations.
///
/// The distribution in this repository's target directory is the DOS conversion of the original
/// self-booting release: <c>PIR.EXE</c> is a 1,983-byte shim that opens <c>DISK1</c>, <c>DISK2</c> and
/// <c>DISKS</c> as ordinary files, installs INT 80h/81h/82h handlers that service the game's raw sector
/// reads out of them, and then EXECs <c>DISKP</c> — the game proper, a 163,952-byte MZ image.
///
/// <c>DISKP</c> is a flat real-mode program whose data group sits at a fixed paragraph inside its own
/// load image (image paragraph 0x1124), so <b>every global has a constant DGROUP-relative offset</b>. The
/// absolute segment DOS picks at EXEC time varies, but it cancels out: find one string whose DGROUP
/// offset is known and <c>DGROUP:0000 = hit − anchorOffset</c>. Because the game runs under DOSBox, that
/// arithmetic happens against the emulator's address space, where the guest's conventional RAM is mapped
/// verbatim.
///
/// Everything here was derived statically; none of it has been confirmed against a running game on this
/// machine. That is why <see cref="GameLocator"/> validates three independent anchors before it trusts a
/// hit, why every pin carries its <see cref="Evidence"/>, and why the trainer always keeps the
/// build-independent value scanner available.
/// </summary>
public static class PiratesLayout
{
    // --- DGROUP string anchors -----------------------------------------------------------------
    // Three distinctive literals from the game's initialised data, at offsets read straight out of the
    // EXE image. Each appears exactly once in the 163,840-byte load image, so a hit on the first plus a
    // match on the other two cannot plausibly be a coincidence.

    /// <summary>Anchor literal — its bytes locate a candidate DGROUP base.</summary>
    public static readonly byte[] AnchorBytes = Encoding.ASCII.GetBytes("COPYRIGHT (C)  1987  MICROPROSE INC.");

    /// <summary>DGROUP offset of <see cref="AnchorBytes"/> (title-screen credits). <b>[Confirmed]</b>.</summary>
    public const int AnchorOffset = 0x0183;

    /// <summary>
    /// First validation literal — the eight-byte saved-game magic the program keeps as a compile-time
    /// reference copy. The image actually holds <c>"PIRATES!PIRATES!"</c> here, but only the first eight
    /// bytes are constant: the second copy sits at <see cref="SaveBlockOffset"/> and is overwritten by
    /// whatever the save disk holds as soon as a slot is read, so it must not be validated against.
    /// </summary>
    public static readonly byte[] ValidateBytes = Encoding.ASCII.GetBytes("PIRATES!");

    /// <summary>DGROUP offset of <see cref="ValidateBytes"/>. <b>[Confirmed]</b>.</summary>
    public const int ValidateOffset = 0x4128;

    /// <summary>The game's own month abbreviations, in table order.</summary>
    public static readonly IReadOnlyList<string> MonthNames = new[]
    {
        "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC",
    };

    /// <summary>
    /// The record separator between display strings. Note this cannot come from a C# string literal:
    /// <c>"\xff"</c> is a <em>greedy</em> hex escape that would swallow following letters, and
    /// <see cref="Encoding.ASCII"/> cannot represent a byte above 0x7F at all.
    /// </summary>
    public const byte StringSeparator = 0xFF;

    /// <summary>
    /// Second validation literal — the month-name table the date display indexes, built from
    /// <see cref="MonthNames"/> so it cannot be mangled by string escaping. In the program the records
    /// are separated by <c>0xFF</c> and a thirteenth "JAN" follows for wrap-around; validating the first
    /// twelve is a strict prefix of what is really there.
    /// </summary>
    /// <remarks>Must be declared after <see cref="MonthNames"/> — static initialisers run in order.</remarks>
    public static readonly byte[] MonthTableBytes = BuildMonthTable();

    /// <summary>DGROUP offset of <see cref="MonthTableBytes"/>. <b>[Confirmed]</b>.</summary>
    public const int MonthTableOffset = 0x31C9;

    private static byte[] BuildMonthTable()
    {
        var bytes = new List<byte>(MonthNames.Count * 4);
        for (int i = 0; i < MonthNames.Count; i++)
        {
            if (i > 0) bytes.Add(StringSeparator);
            bytes.AddRange(Encoding.ASCII.GetBytes(MonthNames[i]));
        }
        return bytes.ToArray();
    }

    /// <summary>How many bytes a validation window needs (through the end of the furthest literal).</summary>
    public static int ValidationWindowBytes => Math.Max(
        Math.Max(AnchorOffset + AnchorBytes.Length, MonthTableOffset + MonthTableBytes.Length),
        ValidateOffset + ValidateBytes.Length);

    // --- the saved-game block ------------------------------------------------------------------
    /// <summary>
    /// DGROUP offset where the game's persistent state lives. Disk-transfer descriptor 0x1F moves exactly
    /// <see cref="SaveBlockBytes"/> bytes between the save disk and this address, and the save-slot
    /// validity check compares its first eight bytes against the constant "PIRATES!" at
    /// <see cref="ValidateOffset"/>. <b>[Confirmed]</b>.
    ///
    /// Note this block is <em>not</em> the whole of the state the trainer touches. The calendar globals
    /// (<see cref="DayOfYearOffset"/>, <see cref="YearsElapsedOffset"/>, <see cref="MonthOffset"/>) and
    /// <see cref="PiratePointsOffset"/> sit well above it, around 0x9A00: they are live working copies
    /// that the load path <em>unpacks</em> out of the block (the saved-game list screen reads a slot's
    /// day counter from the table inside the block and writes it to <see cref="DayOfYearOffset"/>, then
    /// derives <see cref="YearsElapsedOffset"/> from it). So do not use this range as a proxy for
    /// "addresses the trainer owns".
    /// </summary>
    public const int SaveBlockOffset = 0x4130;

    /// <summary>Size of the saved-game block, from the same descriptor. <b>[Confirmed]</b>.</summary>
    public const int SaveBlockBytes = 0x0794;   // 1,940

    // --- the settlement table ------------------------------------------------------------------
    /// <summary>
    /// DGROUP offset of the era's settlement array. The game's city-pointer helper is literally
    /// <c>ptr = 0x4240 + index * 24</c>, and the era's 1,024-byte disk block loads here. <b>[Confirmed]</b>.
    /// </summary>
    public const int CityTableOffset = 0x4240;

    /// <summary>Bytes per settlement record. <b>[Confirmed]</b>.</summary>
    public const int CityRecordBytes = 24;

    /// <summary>Offset of the twelve-character name inside a settlement record. <b>[Confirmed]</b>.</summary>
    public const int CityNameOffset = 12;

    /// <summary>Length of the name field. <b>[Confirmed]</b>.</summary>
    public const int CityNameLength = 12;

    /// <summary>
    /// Most settlements an era's table holds. The table starts at <see cref="CityTableOffset"/> and the
    /// convoy routes begin 0x3E0 bytes later, so 0x3E0 / 24 records fit; the largest shipped era uses 41.
    /// </summary>
    public const int MaxCities = 0x3E0 / CityRecordBytes;   // 41

    /// <summary>DGROUP offset of the convoy route rows (16 Silver Train slots then 16 Treasure Fleet). <b>[Confirmed]</b>.</summary>
    public const int FleetRouteOffset = CityTableOffset + 0x3E0;

    // --- player state --------------------------------------------------------------------------
    /// <summary>
    /// The player's purse: an <b>unsigned</b> 16-bit word. The game has one routine that adds to it
    /// (saturating at 0xFFFF on carry) and one that subtracts, the latter printing "Not enough gold."
    /// when it would borrow — which is what fixes this offset beyond doubt. <b>[Confirmed]</b>.
    /// </summary>
    public const int GoldOffset = 0x4847;

    /// <summary>The largest value the gold word can hold.</summary>
    public const int MaxGold = 0xFFFF;

    /// <summary>
    /// Crew of the active party — the word at <b>+3</b> in the 32-byte party record that begins at
    /// <see cref="PartyTableOffset"/> (byte 0 is the record's in-use flag, and <see cref="GoldOffset"/>
    /// is the word at +7 of the same record). It is the share divisor when plunder is divided.
    /// <b>[Inferred]</b>: the divide-plunder arithmetic reads it as the head count, but no routine names
    /// it outright.
    /// </summary>
    public const int CrewOffset = 0x4843;

    /// <summary>
    /// Start of the four 32-byte party records ("Divide Party" / "Join Parties" splits your force).
    /// Record <c>n</c> begins at <c>0x4840 + 32n</c>; byte 0 is its in-use flag. <b>[Inferred]</b>.
    /// </summary>
    public const int PartyTableOffset = 0x4840;

    /// <summary>Bytes per party record. <b>[Inferred]</b>.</summary>
    public const int PartyRecordBytes = 32;

    /// <summary>Number of party records. <b>[Inferred]</b> from the four the setup code clears.</summary>
    public const int PartyCount = 4;

    /// <summary>
    /// Accumulated personal wealth, a 16-bit word in <b>tens</b> of gold pieces — the retirement screen
    /// prints the raw word and then appends a literal '0'. Each month the estate adds half the land byte
    /// to it. <b>[Confirmed]</b>.
    /// </summary>
    public const int WealthOffset = 0x4742;

    /// <summary>Multiplier from the stored wealth word to gold pieces.</summary>
    public const int WealthPerUnit = 10;

    /// <summary>
    /// Land grants, one byte in units of <see cref="AcresPerUnit"/> acres — the retirement screen
    /// multiplies it by 50 before printing " acres of land." <b>[Confirmed]</b>.
    /// </summary>
    public const int LandOffset = 0x4745;

    /// <summary>Acres represented by one unit of the land byte.</summary>
    public const int AcresPerUnit = 50;

    /// <summary>Rank/title index (Ensign..Marquis), used to pick the title string. <b>[Inferred]</b>.</summary>
    public const int RankOffset = 0x473D;

    /// <summary>Index into the era's settlement table for the port you are at. <b>[Inferred]</b>.</summary>
    public const int CurrentCityOffset = 0x4759;

    /// <summary>
    /// Era code. The displayed year is <c>1560 + 20 * era + yearsElapsed</c>, which is exactly what the
    /// date routine computes — so the six selectable periods store <b>0, 2, 3, 4, 5, 6</b>, not 0-5. The
    /// menu handler makes that explicit: it takes the 1-based menu choice and maps only choice 1 down to
    /// 0, passing 2-6 through. Code 1 (which would be 1580) is arithmetically valid but never offered.
    /// <b>[Confirmed]</b>.
    /// </summary>
    public const int EraOffset = 0x475A;

    /// <summary>Era codes of the six selectable periods, in menu order (1560, 1600, 1620, 1640, 1660, 1680).</summary>
    public static readonly IReadOnlyList<int> EraCodes = new[] { 0, 2, 3, 4, 5, 6 };

    /// <summary>
    /// Era code the game defaults to when the player declines to pick a period: 5, i.e. 1660, "The
    /// Buccaneer Heroes". <b>[Confirmed]</b> — the handler stores a literal 5 on that branch.
    /// </summary>
    public const int DefaultEraCode = 5;

    /// <summary>The player's family name, nine characters inside the display-string table. <b>[Confirmed]</b>.</summary>
    public const int PlayerNameOffset = 0x104B;

    /// <summary>Length of the family-name field.</summary>
    public const int PlayerNameLength = 9;

    // --- the game clock ------------------------------------------------------------------------
    // The monthly tick reads as: day-of-year++ ; if it reaches 360, years++ and subtract 360 ;
    // month = day-of-year / 30.  So the calendar is a flat 360-day year of twelve 30-day months.

    /// <summary>Day within the current year, 0..359. <b>[Confirmed]</b>.</summary>
    public const int DayOfYearOffset = 0x9A9F;

    /// <summary>Whole years elapsed since the era's start year. <b>[Confirmed]</b>.</summary>
    public const int YearsElapsedOffset = 0x9A9D;

    /// <summary>Current month 0..11, derived by the tick as day-of-year / 30. <b>[Confirmed]</b>.</summary>
    public const int MonthOffset = 0x9A2B;

    /// <summary>Days in the game's year (twelve 30-day months). <b>[Confirmed]</b>.</summary>
    public const int DaysPerYear = 360;

    /// <summary>Days in a game month. <b>[Confirmed]</b>.</summary>
    public const int DaysPerMonth = 30;

    /// <summary>Years each era advances the base date. <b>[Confirmed]</b>.</summary>
    public const int YearsPerEra = 20;

    /// <summary>Start year of era 0. <b>[Confirmed]</b>.</summary>
    public const int BaseYear = 1560;

    /// <summary>Hall-of-Fame score out of 100, shown on the retirement screen. <b>[Inferred]</b>.</summary>
    public const int PiratePointsOffset = 0x9A27;

    /// <summary>Index of the settlement the Treasure Fleet is currently at, refreshed from its route. <b>[Confirmed]</b>.</summary>
    public const int TreasureFleetCityOffset = 0x473F;

    /// <summary>The Treasure Fleet's current route slot 0..15. <b>[Confirmed]</b>.</summary>
    public const int TreasureFleetSlotOffset = 0x475B;

    // --- the convoy clock ----------------------------------------------------------------------
    // The setup code derives each convoy's route slot straight from the calendar:
    //     slot = dayOfYear / 15 − bias + 2 * (era & 1) ;  if (slot < 0) slot += 24
    // 15 days is half a month, so dayOfYear/15 is a half-month index 0..23. A bias of 18 puts slot 0 at
    // half-month 18 — day 270, the first half of October — for the Treasure Fleet, and a bias of 6 puts
    // it at April for the Silver Train. The odd-era term shifts both convoys one month earlier, which is
    // exactly why 1620 (code 3) and 1660 (code 5) run to September/March in the manual's chart.

    /// <summary>Half-month slots in a year — the length of each convoy's route row.</summary>
    public const int HalfMonthsPerYear = 24;

    /// <summary>Days in a half-month slot.</summary>
    public const int DaysPerHalfMonth = 15;

    /// <summary>Slot bias for the Treasure Fleet: slot 0 falls in the first half of October.</summary>
    public const int TreasureFleetSlotBias = 18;

    /// <summary>Slot bias for the Silver Train: slot 0 falls in the first half of April.</summary>
    public const int SilverTrainSlotBias = 6;

    /// <summary>Route-table slots each convoy row holds (the other eight of the 24 are "gone to Spain").</summary>
    public const int RouteSlots = 16;

    // --- derived helpers (pure; unit-tested) ---------------------------------------------------
    /// <summary>The calendar year the game would print for an era code and an elapsed-years count.</summary>
    public static int DisplayYear(int eraCode, int yearsElapsed) =>
        BaseYear + YearsPerEra * eraCode + yearsElapsed;

    /// <summary>Position 0-5 of an era code in the six selectable periods, or -1 if it is not one of them.</summary>
    public static int EraIndexFromCode(int eraCode)
    {
        for (int i = 0; i < EraCodes.Count; i++)
            if (EraCodes[i] == eraCode) return i;
        return -1;
    }

    /// <summary>Era code stored for the period at position <paramref name="index"/> (0-5), or -1.</summary>
    public static int EraCodeFromIndex(int index) =>
        index >= 0 && index < EraCodes.Count ? EraCodes[index] : -1;

    /// <summary>Month index 0..11 for a day-of-year, the way the monthly tick derives it.</summary>
    public static int MonthFromDayOfYear(int dayOfYear) =>
        Math.Clamp(dayOfYear, 0, DaysPerYear - 1) / DaysPerMonth;

    /// <summary>
    /// The convoy's route slot for a day of the year, reproducing the game's own arithmetic. Returns a
    /// value in 0..23; anything at or above <see cref="RouteSlots"/> means the convoy is not on the
    /// Spanish Main at that date.
    /// </summary>
    public static int ConvoySlot(int dayOfYear, int eraCode, int bias)
    {
        int slot = Math.Clamp(dayOfYear, 0, DaysPerYear - 1) / DaysPerHalfMonth - bias + 2 * (eraCode & 1);
        if (slot < 0) slot += HalfMonthsPerYear;
        return slot % HalfMonthsPerYear;
    }

    /// <summary>The Treasure Fleet's route slot for a day of the year.</summary>
    public static int TreasureFleetSlot(int dayOfYear, int eraCode) =>
        ConvoySlot(dayOfYear, eraCode, TreasureFleetSlotBias);

    /// <summary>The Silver Train's route slot for a day of the year.</summary>
    public static int SilverTrainSlot(int dayOfYear, int eraCode) =>
        ConvoySlot(dayOfYear, eraCode, SilverTrainSlotBias);

    /// <summary>
    /// Month index 0..11 in which a convoy route slot falls, inverting <see cref="ConvoySlot"/>. This is
    /// what turns a decoded route row into the manual's "city - month - early/late" chart.
    /// </summary>
    public static int MonthForSlot(int slot, int eraCode, int bias) =>
        ((slot + bias - 2 * (eraCode & 1)) % HalfMonthsPerYear + HalfMonthsPerYear) % HalfMonthsPerYear / 2;

    /// <summary>Whether a route slot falls in the first half of its month.</summary>
    public static bool IsEarlyHalf(int slot, int eraCode, int bias) =>
        ((slot + bias - 2 * (eraCode & 1)) % HalfMonthsPerYear + HalfMonthsPerYear) % HalfMonthsPerYear % 2 == 0;

    /// <summary>Gold pieces represented by a stored wealth word.</summary>
    public static int WealthToGold(int stored) => stored * WealthPerUnit;

    /// <summary>Acres represented by a stored land byte.</summary>
    public static int LandToAcres(int stored) => stored * AcresPerUnit;

    /// <summary>DGROUP offset of settlement <paramref name="index"/>, matching the game's own helper.</summary>
    public static int CityOffset(int index) => CityTableOffset + index * CityRecordBytes;

    /// <summary>Whether a year could plausibly be in a Pirates! campaign — used to strengthen validation.</summary>
    public static bool IsPlausibleYear(int year) => year is >= BaseYear and <= 1800;

    /// <summary>Whether an era byte is one of the six selectable time periods (codes 0, 2, 3, 4, 5, 6).</summary>
    public static bool IsPlausibleEra(int eraCode) => EraIndexFromCode(eraCode) >= 0;

    // --- pure validation helpers ---------------------------------------------------------------
    /// <summary>
    /// Whether the bytes at a candidate base carry all three anchor literals at their known DGROUP
    /// offsets — i.e. this really is the game's data segment. <paramref name="window"/> must start at the
    /// candidate base and cover at least <see cref="ValidationWindowBytes"/> bytes.
    /// </summary>
    public static bool ValidateSegment(ReadOnlySpan<byte> window)
    {
        if (!MatchAt(window, AnchorOffset, AnchorBytes)) return false;
        if (!MatchAt(window, MonthTableOffset, MonthTableBytes)) return false;
        if (!MatchAt(window, ValidateOffset, ValidateBytes)) return false;
        return true;
    }

    /// <summary>
    /// Whether a 24-byte span looks like a live settlement record: a name of printable upper-case ASCII
    /// right-padded with spaces, and a nation byte in range. A <em>single</em> internal space is legal —
    /// "SANTA MARTA", "FLORIDA CHNL", "RIO DE HACHA" — but a run of two is not, which is what stops a
    /// mostly-blank block of RAM from passing as a name once the trailing pad has been trimmed. Used to
    /// sanity-check the located settlement table before the trainer shows it; a mis-located base produces
    /// garbage names immediately.
    /// </summary>
    public static bool LooksLikeCityRecord(ReadOnlySpan<byte> record)
    {
        if (record.Length < CityRecordBytes) return false;
        if (record[3] > 3) return false;                       // nation 0..3

        var name = record.Slice(CityNameOffset, CityNameLength);
        int end = name.Length;
        while (end > 0 && name[end - 1] == ' ') end--;          // trim the trailing pad
        if (end == 0) return false;                             // an all-blank name is not a settlement
        if (name[0] == ' ') return false;                       // real names never start with a space

        bool sawLetter = false;
        for (int i = 0; i < end; i++)
        {
            byte b = name[i];
            bool ok = b is (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'0' and <= (byte)'9')
                          or (byte)' ' or (byte)'.' or (byte)'\'' or (byte)'-';
            if (!ok) return false;
            if (b == ' ' && name[i - 1] == ' ') return false;   // no double spaces inside a name
            if (b is >= (byte)'A' and <= (byte)'Z') sawLetter = true;
        }
        return sawLetter;
    }

    /// <summary>Decodes the twelve-character name out of a settlement record.</summary>
    public static string CityName(ReadOnlySpan<byte> record) =>
        record.Length < CityRecordBytes
            ? string.Empty
            : Encoding.ASCII.GetString(record.Slice(CityNameOffset, CityNameLength)).TrimEnd();

    private static bool MatchAt(ReadOnlySpan<byte> window, int offset, byte[] needle)
    {
        if (offset < 0 || offset + needle.Length > window.Length) return false;
        return window.Slice(offset, needle.Length).SequenceEqual(needle);
    }

    // --- the pin set the auto-locate offers ----------------------------------------------------
    /// <summary>
    /// Everything auto-locate can pin, in the order it adds them. Gold leads because it is both the
    /// headline cheat and the best-evidenced offset.
    /// </summary>
    public static readonly IReadOnlyList<KnownValue> KnownValues = new[]
    {
        new KnownValue("Gold", GoldOffset, 2, Evidence.Confirmed,
            "Your purse, an unsigned 16-bit word (0..65535). Fixed by the game's add-gold / spend-gold pair."),
        new KnownValue("Crew", CrewOffset, 2, Evidence.Inferred,
            "Crew of the active party — the word at +3 in its 32-byte record, and the divisor when plunder is shared."),
        new KnownValue("Wealth (x10 gold)", WealthOffset, 2, Evidence.Confirmed,
            "Accumulated personal wealth in tens of gold pieces; the retirement screen prints it then appends a '0'."),
        new KnownValue("Land (x50 acres)", LandOffset, 1, Evidence.Confirmed,
            "Land grants in units of 50 acres. Each month half of this is added to Wealth."),
        new KnownValue("Day of year (0-359)", DayOfYearOffset, 2, Evidence.Confirmed,
            "The game clock. Freeze it to stop ageing out of your career; the year rolls at 360."),
        new KnownValue("Years elapsed", YearsElapsedOffset, 2, Evidence.Confirmed,
            "Whole years since the era began. Displayed year = 1560 + 20 x era + this."),
        new KnownValue("Month (0-11)", MonthOffset, 2, Evidence.Confirmed,
            "Derived each tick as day-of-year / 30, so freeze the day rather than this to stop the calendar."),
        new KnownValue("Era code", EraOffset, 1, Evidence.Confirmed,
            "Which time period is in play: 0=1560, 2=1600, 3=1620, 4=1640, 5=1660, 6=1680. Read-only in practice — changing it desynchronises the settlement table."),
        new KnownValue("Rank", RankOffset, 1, Evidence.Inferred,
            "Title index used to pick Ensign / Captain / Major / Colonel / Admiral / Baron / Count / Marquis."),
        new KnownValue("Pirate points (/100)", PiratePointsOffset, 2, Evidence.Inferred,
            "Hall-of-Fame score shown when you retire."),
    };
}
