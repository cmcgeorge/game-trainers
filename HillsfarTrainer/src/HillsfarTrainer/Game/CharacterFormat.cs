namespace HillsfarTrainer.Game;

/// <summary>
/// One literal the locator matches, and the <c>DGROUP</c> offset it must sit at.
/// </summary>
/// <param name="DgroupOffset">Offset of the first byte within the game's data segment.</param>
/// <param name="Bytes">The raw bytes as they appear in memory.</param>
/// <param name="Description">What the literal is, for the UI.</param>
public readonly record struct Anchor(int DgroupOffset, byte[] Bytes, string Description);

/// <summary>
/// The reverse-engineered layout of Hillsfar's 188-byte character record, plus the literals used
/// to find the game's data segment.
///
/// <para>Every offset here is relative to the start of the record. The record lives at
/// <see cref="DgroupRecordOffset"/> inside the program's single data group, and the
/// <c>&lt;name&gt;.HIL</c> / <c>*.PRE</c> files on disk are a <b>raw dump of exactly these 188
/// bytes</b> — no header, no checksum, no encryption. That was established three ways: the loaded
/// record matched the file byte-for-byte; a near-total rewrite of the record in memory followed by
/// the game's own <i>Save your current Hillsfar character</i> produced a file identical to the
/// edited memory; and a file edited on disk loaded with every value showing on the character
/// sheet.</para>
///
/// <para>Fields carry a Confirmed/Inferred/Unknown status in <c>docs/ReverseEngineering.md</c>.
/// Everything this trainer <i>writes</i> is Confirmed — proved by writing a sentinel into the
/// running game and reading it back off the game's own screen. The Unknown bytes are deliberately
/// not exposed: they are round-tripped, never interpreted.</para>
/// </summary>
public static class CharacterFormat
{
    /// <summary>Length of the record, and of a <c>.HIL</c> / <c>.PRE</c> file.</summary>
    public const int RecordLength = 188;

    /// <summary>Where the working copy of the record lives inside the data segment.</summary>
    public const int DgroupRecordOffset = 0x094C;

    // --- record offsets -------------------------------------------------------

    /// <summary>A 32-bit value the game maintains during play. Purpose unknown; never written.</summary>
    public const int OffUnknownCounter = 0x00;

    /// <summary>Name: NUL-terminated, space-padded; the last byte is always NUL.</summary>
    public const int OffName = 0x04;

    /// <summary>Length of the name field in bytes, including its guaranteed trailing NUL.</summary>
    public const int NameFieldLength = 16;

    /// <summary>Longest name the field can hold (<see cref="NameFieldLength"/> less the terminator).</summary>
    public const int MaxNameLength = 15;

    /// <summary>Strength.</summary>
    public const int OffStrength = 0x14;

    /// <summary>Exceptional-strength percentile — the <c>(nn)</c> after an 18. Fighters only.</summary>
    public const int OffStrengthPercentile = 0x15;

    /// <summary>Intelligence.</summary>
    public const int OffIntelligence = 0x16;

    /// <summary>Wisdom.</summary>
    public const int OffWisdom = 0x17;

    /// <summary>Dexterity.</summary>
    public const int OffDexterity = 0x18;

    /// <summary>Constitution — also drives natural healing, see <see cref="GameFacts"/>.</summary>
    public const int OffConstitution = 0x19;

    /// <summary>Charisma.</summary>
    public const int OffCharisma = 0x1A;

    /// <summary>Alignment index, 0..8. See <see cref="AlignmentBook"/>.</summary>
    public const int OffAlignment = 0x1C;

    /// <summary>Age, written by the game as a 16-bit word.</summary>
    public const int OffAge = 0x1E;

    /// <summary>Current hit points.</summary>
    public const int OffHitPoints = 0x20;

    /// <summary>Maximum hit points.</summary>
    public const int OffHitPointsMax = 0x21;

    /// <summary>
    /// Class index — a character-creation menu index, converted to a mask by <see cref="ClassBook"/>.
    /// The game's own table covers 0..15; <see cref="ClassBook.MagicUserThiefIndex"/> is one past it,
    /// for the single legal combination the table has no slot for.
    /// </summary>
    public const int OffClassIndex = 0x24;

    /// <summary>Gold, 32-bit little-endian.</summary>
    public const int OffGold = 0x28;

    /// <summary>Gender: 0 male, 1 female.</summary>
    public const int OffGender = 0x2C;

    /// <summary>Race index, 0..5. See <see cref="RaceBook"/>.</summary>
    public const int OffRace = 0x2D;

    /// <summary>Experience, 32-bit little-endian.</summary>
    public const int OffExperience = 0x2E;

    /// <summary>First of three thief-skill percentages (Inferred).</summary>
    public const int OffThiefSkills = 0x32;

    /// <summary>How many thief-skill bytes follow <see cref="OffThiefSkills"/>.</summary>
    public const int ThiefSkillCount = 3;

    /// <summary>
    /// Class bitmask — the byte the game actually tests (45 <c>test byte</c> sites). Bit 0 Thief,
    /// bit 1 Fighter, bit 2 Magic-User, bit 3 Cleric, and the mask is stored in <b>both</b> nibbles.
    /// </summary>
    public const int OffClassMask = 0x35;

    /// <summary>Hour of day, 1..24. 24 is midnight and displays as "am".</summary>
    public const int OffHour = 0x44;

    /// <summary>Day counter, 16-bit; the clock bumps it when the hour reaches 24.</summary>
    public const int OffDay = 0x3E;

    /// <summary>Real-world <c>time_t</c> of the last clock tick, 32-bit.</summary>
    public const int OffTickTime = 0x40;

    /// <summary>Flag bits (Inferred). Bit 0 is set for both shipped thieves, who carry picks.</summary>
    public const int OffFlags = 0x45;

    /// <summary>First byte of the lock-pick block. See <see cref="LockPickSet"/>.</summary>
    public const int OffLockPicks = 0x46;

    /// <summary>Knock rings carried, 0..<see cref="MaxConsumable"/>.</summary>
    public const int OffKnockRings = 0x86;

    /// <summary>Healing potions carried, 0..<see cref="MaxConsumable"/>.</summary>
    public const int OffHealingPotions = 0x87;

    /// <summary>First of the 18 per-hour countdown timers the clock decrements.</summary>
    public const int OffHourTimers = 0x89;

    /// <summary>How many per-hour timers there are — the clock tick walks exactly this many.</summary>
    public const int HourTimerCount = 18;

    /// <summary>Archery-range level, capped by the game at <see cref="MaxArcheryLevel"/>.</summary>
    public const int OffArcheryLevel = 0x9F;

    /// <summary>Hours remaining until the next natural heal; the game resets it to 24.</summary>
    public const int OffHealCountdown = 0xAB;

    /// <summary>Cleric level. The four class levels run <c>0xB7..0xBA</c> in descending mask order.</summary>
    public const int OffLevelCleric = 0xB7;

    /// <summary>Magic-User level.</summary>
    public const int OffLevelMagicUser = 0xB8;

    /// <summary>Fighter level.</summary>
    public const int OffLevelFighter = 0xB9;

    /// <summary>Thief level.</summary>
    public const int OffLevelThief = 0xBA;

    // --- limits ---------------------------------------------------------------

    /// <summary>Lowest ability score the game rolls.</summary>
    public const int MinAbility = 3;

    /// <summary>Highest ability score the game rolls — the manual's stated range is 3..19.</summary>
    public const int MaxAbility = 19;

    /// <summary>Highest exceptional-strength percentile.</summary>
    public const int MaxStrengthPercentile = 100;

    /// <summary>Both consumable counters are capped at 99 by the game's own purchase code.</summary>
    public const int MaxConsumable = 99;

    /// <summary>The game's own cap on the archery-range level (<c>cmp al,0x0F</c> before the <c>inc</c>).</summary>
    public const int MaxArcheryLevel = 15;

    /// <summary>Largest value a single-byte field can hold.</summary>
    public const int MaxByte = 255;

    /// <summary>Largest value the 32-bit gold and experience fields can hold.</summary>
    public const uint MaxDword = uint.MaxValue;

    /// <summary>Hours in the game's day; hour 24 is midnight and wraps to 1.</summary>
    public const int HoursPerDay = 24;

    // --- locator anchors ------------------------------------------------------

    /// <summary>
    /// The primary anchor: the 69-byte startup banner at <c>DGROUP:0x0D1A</c>. It is plain ASCII
    /// (unlike most of the game's text, which is digraph-compressed) and a live sweep of a running
    /// 16 MB DOSBox guest found exactly one occurrence.
    /// </summary>
    public static readonly Anchor PrimaryAnchor = new(
        0x0D1A,
        "WARNING: DO NOT RUN MEMORY RESIDENT PROGRAMS WHILE PLAYING HILLSFAR!!"u8.ToArray(),
        "startup warning banner at DGROUP:0x0D1A");

    /// <summary>
    /// Corroborating literals, each checked live for uniqueness. Note that two of these are
    /// <i>not</i> readable text: the class-table entry and the digraph table's first row are raw
    /// bytes sliced out of the unpacked image. Most of the game's strings are compressed, so
    /// matching on decoded text would find nothing.
    /// </summary>
    public static readonly Anchor[] Validators =
    {
        new(0x0E1D, "Put the Hillsfar Program Disk in the drive"u8.ToArray(),
            "disk prompt at DGROUP:0x0E1D"),
        new(0x3DD8, "FTR/MU/TH\0"u8.ToArray(),
            "class-name table entry at DGROUP:0x3DD8"),
        new(0xAAA4, " eotahnrsiuldygc"u8.ToArray(),
            "text-codec digraph table at DGROUP:0xAAA4"),
        new(0x91AC, "HILCHAGUYPRE"u8.ToArray(),
            "character-file extension table at DGROUP:0x91AC"),
    };

    /// <summary>
    /// How many of <see cref="Validators"/> must line up before a candidate is accepted — so at
    /// minimum a three-of-five match counting the primary anchor.
    /// </summary>
    public const int MinValidators = 2;

    // --- shape check ----------------------------------------------------------

    /// <summary>
    /// Highest ability score the shape check will still accept.
    ///
    /// <para>Above <see cref="MaxAbility"/> on purpose. The camp menu can <i>Transfer a character</i>
    /// in from <c>Pool of Radiance</c>, where magic items push scores past 19, and rejecting such a
    /// record outright would leave the trainer refusing to recognise a character that is plainly on
    /// screen. The editor still clamps its own writes to <see cref="MaxAbility"/>; this is only how
    /// much slack the <i>recognition</i> test allows.</para>
    /// </summary>
    public const int MaxPlausibleAbility = 25;

    /// <summary>
    /// True when a 188-byte window looks like a real character record.
    ///
    /// <para>This is the last line of defence behind the anchors, and it is deliberately loose
    /// about the things the game itself is loose about. It requires a printable name that does not
    /// start with a space, ability scores inside a plausible range, a maximum hit-point count
    /// above zero with the current count not exceeding it, an hour inside 1..24, a legal class mask
    /// with matching nibbles, and a race and alignment inside their tables.</para>
    ///
    /// <para>The name test only demands a printable non-space first byte rather than a letter,
    /// because <see cref="CharacterRecord.Name"/> accepts any printable ASCII: a stricter test here
    /// would let the trainer rename a character to something it then refused to find again.</para>
    /// </summary>
    public static bool LooksLikeRecord(ReadOnlySpan<byte> rec)
    {
        if (rec.Length < RecordLength) return false;

        // Name: printable non-space first byte — or NUL, because CharacterRecord.Name accepts an
        // empty name and writes the terminator straight into byte 0. The two have to agree, or the
        // trainer could rename a character into something it then refused to recognise. Allowing NUL
        // costs nothing: an all-zero window still fails on hit points, abilities, hour and mask below.
        byte first = rec[OffName];
        if (first != 0 && (first <= 0x20 || first > 0x7E)) return false;
        if (rec[OffName + NameFieldLength - 1] != 0) return false;
        for (int i = 0; i < NameFieldLength; i++)
        {
            byte b = rec[OffName + i];
            if (b != 0 && (b < 0x20 || b > 0x7E)) return false;
        }

        // Abilities. Strength's percentile is a percentage, not an ability score.
        foreach (int off in new[]
                 {
                     OffStrength, OffIntelligence, OffWisdom,
                     OffDexterity, OffConstitution, OffCharisma,
                 })
        {
            if (rec[off] < MinAbility || rec[off] > MaxPlausibleAbility) return false;
        }
        if (rec[OffStrengthPercentile] > MaxStrengthPercentile) return false;

        // Hit points.
        if (rec[OffHitPointsMax] == 0) return false;
        if (rec[OffHitPoints] > rec[OffHitPointsMax]) return false;

        // Clock, race, alignment.
        if (rec[OffHour] < 1 || rec[OffHour] > HoursPerDay) return false;
        if (rec[OffRace] >= RaceBook.Races.Count) return false;
        if (rec[OffGender] > 1) return false;
        if (rec[OffAlignment] >= AlignmentBook.Alignments.Count) return false;

        // Class mask: the game stores it in both nibbles, and only certain masks are legal.
        byte mask = rec[OffClassMask];
        if ((mask & 0x0F) != (mask >> 4)) return false;
        if (!ClassBook.IsLegalMask(mask & 0x0F)) return false;

        return true;
    }
}
