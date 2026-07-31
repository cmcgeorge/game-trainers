using System.Text;

namespace AlternateRealityTrainer.Game;

/// <summary>
/// The reverse-engineered layout of an <c>Alternate Reality: The City</c> character.
///
/// The game keeps the character in one 12,288-byte block that is written to disk verbatim as
/// <c>ARCCD</c><i>nn</i> and lives at a fixed offset inside the program's data segment
/// (<see cref="DgroupRecordOffset"/>). It is the working copy, not a save snapshot: writing an
/// attribute here changes the attribute the game uses.
///
/// Every offset below is taken from the game's own display templates. <c>CITY.EXE</c> stores each
/// message as a byte-coded template in which <c>0xB0</c>/<c>0xB1</c>/<c>0xB2</c>/<c>0xB3</c> mean
/// "print the u32 / u16 / u8 / string at DGROUP:<i>operand</i>" — so the program names the addresses
/// of its own status bar and inventory panel. Each was then read back from a live DOSBox session and
/// matched against the screen. See <c>docs/ReverseEngineering.md</c> in the game directory.
/// </summary>
public static class CharacterFormat
{
    /// <summary>Size of the character block (the whole <c>ARCCD</c><i>nn</i> file).</summary>
    public const int RecordSize = 0x3000;

    /// <summary>
    /// Bytes at the front of the record that hold every field the trainer reads, writes or
    /// validates. The rest of the block is worn-item records and padding, so both the poll loop and
    /// the structural scan only ever need this much.
    /// </summary>
    public const int LiveFieldsLength = 0xE4;

    /// <summary>Offset of the character block inside the game's data segment (DGROUP).</summary>
    public const int DgroupRecordOffset = 0x4EB1;

    /// <summary>
    /// Offset of the 64 x 64 city street map relative to DGROUP -- it sits <b>below</b> DGROUP:0000,
    /// in the data segment before it, which is why this is negative. Loaded verbatim from
    /// <c>CITY.EXE</c> (file offset <see cref="CityTerrain.CityExeOffset"/>) and never modified while
    /// the game runs. See <see cref="CityTerrain"/> for the byte layout.
    /// </summary>
    public const int DgroupTerrainOffset = -0x61B0;

    // --- identity ------------------------------------------------------------

    /// <summary>Name — NUL-padded ASCII, printed by the status bar with a width of 32.</summary>
    public const int OffName = 0x4C;

    /// <summary>Bytes reserved for <see cref="OffName"/>.</summary>
    public const int NameLength = 32;

    /// <summary>Longest name the trainer will write, leaving room for the NUL terminator.</summary>
    public const int MaxNameLength = 20;

    // --- calendar ------------------------------------------------------------

    public const int OffMinute = 0x26;   // u8
    public const int OffHour = 0x27;     // u8   "It is <hour>00 hours."
    public const int OffDay = 0x28;      // u8   "Hour <hour> of day <day>"
    public const int OffMonth = 0x29;    // u8   index into GameFacts.Months  [Inferred]
    public const int OffYear = 0x2A;     // u16  "In year <year> since abduction."

    // --- attributes ----------------------------------------------------------

    /// <summary>Offset of the first attribute record.</summary>
    public const int OffAttributes = 0x6E;

    /// <summary>Bytes between one attribute record and the next.</summary>
    public const int AttributeStride = 10;

    /// <summary>Number of attribute records.</summary>
    public const int AttributeCount = 7;

    /// <summary>
    /// Bytes 0..2 of an attribute record are <b>current</b>, <b>maximum</b> and <b>natural
    /// maximum</b>. They are equal in a healthy character — which is all the shipped saves show —
    /// but they are not the same field: a Wraith's touch was seen live taking the current value and
    /// the maximum to 0 while leaving the natural maximum at the character's rolled value, giving
    /// <c>0, 0, 9</c>. Neither of the first two can exceed the third.
    ///
    /// The status bar prints byte 0. The trainer writes all three, so an edit restores a drained
    /// attribute outright instead of being pulled back to the drained maximum.
    /// </summary>
    public const int AttributeCopies = 3;

    /// <summary>Byte 3 of an attribute record: sub-point progress toward the next whole point. [Inferred]</summary>
    public const int AttributeFractionOffset = 3;

    /// <summary>Record offset of attribute <paramref name="index"/> (see <see cref="AttributeBook"/>).</summary>
    public static int AttributeOffset(int index)
    {
        if (index < 0 || index >= AttributeCount)
            throw new ArgumentOutOfRangeException(nameof(index));
        return OffAttributes + index * AttributeStride;
    }

    // --- progression ---------------------------------------------------------

    public const int OffLevel = 0xC1;          // u8   "Level :<n>"
    public const int OffExperience = 0xC2;     // u32  "Experience <n>"
    public const int OffNextLevelExp = 0xC6;   // u32  experience needed for the next level
    public const int OffHitPoints = 0xCA;      // u32  "Hit Points :<n>"
    public const int OffHitPointsMax = 0xCE;   // u32  maximum hit points

    // --- money and carried goods (the game's inventory panel, DGROUP:0x0400) --

    public const int OffGold = 0xD2;       // u16
    public const int OffSilver = 0xD4;     // u16
    public const int OffCopper = 0xD6;     // u16
    public const int OffGems = 0xD8;       // u16
    public const int OffJewelry = 0xDA;    // u16
    public const int OffFood = 0xDE;       // u8
    public const int OffWater = 0xDF;      // u8
    public const int OffCrystals = 0xE0;   // u8
    public const int OffKeys = 0xE1;       // u8
    public const int OffCompass = 0xE2;    // u8 (0/1)
    public const int OffWatch = 0xE3;      // u8 (0/1)

    // --- caps ----------------------------------------------------------------

    /// <summary>
    /// "Max" target for an attribute. The manual's range is 0..255 and the field is one byte, but a
    /// level-up adds +1 to every attribute, so 200 leaves plenty of headroom before a wrap.
    /// </summary>
    public const int MaxAttribute = 200;

    /// <summary>Hard ceiling the editor clamps an attribute to.</summary>
    public const int AttributeCeiling = 255;

    /// <summary>"Max" target for hit points (current and maximum).</summary>
    public const int MaxHitPoints = 9_999;

    /// <summary>"Max" target for each coin/valuables field (u16, so the ceiling is 65,535).</summary>
    public const int MaxCoins = 60_000;

    /// <summary>Hard ceiling for a u16 field.</summary>
    public const int CoinCeiling = ushort.MaxValue;

    /// <summary>"Max" target for the byte-sized supply counters.</summary>
    public const int MaxSupply = 99;

    /// <summary>Hard ceiling for a byte-sized supply counter.</summary>
    public const int SupplyCeiling = byte.MaxValue;

    /// <summary>Hard ceiling for level (the field is one byte).</summary>
    public const int LevelCeiling = 250;

    /// <summary>Hard ceiling for experience. The game sets the next-level threshold to twice the
    /// current experience, so this leaves room for that doubling inside a u32.</summary>
    public const uint ExperienceCeiling = 999_999_999;

    /// <summary>
    /// Hard ceiling for the hit-point fields. They are 32-bit, but the game prints them in a
    /// six-character column, so anything past a million is unreadable on screen anyway.
    /// </summary>
    public const uint HitPointCeiling = 999_999;

    // --- anchors used to find the record in a live process --------------------

    /// <summary>
    /// A literal from the game's data segment together with its DGROUP offset. Finding one of these
    /// in the emulator's memory pins DGROUP, and DGROUP pins the character record.
    /// </summary>
    public sealed record Anchor(string Name, byte[] Bytes, int DgroupOffset);

    private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    /// <summary>
    /// The status-bar header. 39 bytes, unique in a live session, and the template it belongs to is
    /// the one whose operands gave up the attribute offsets in the first place.
    ///
    /// Note the <b>literal spaces</b>: unlike the validators below, this one is a fixed screen-layout
    /// run rather than a message, so the game stores it with real 0x20 bytes and not the 0x09 its
    /// text encoding uses for a space. Do not "fix" it to tabs — it would stop matching.
    /// </summary>
    public static readonly Anchor PrimaryAnchor =
        new("status bar header", Ascii("Stats STA   CHR   STR   INT   WIS   SKL"), 0x012A);

    /// <summary>
    /// Corroborating literals. Their distance from <see cref="PrimaryAnchor"/> is fixed by the
    /// executable, so requiring at least <see cref="MinValidators"/> of them to line up rejects a
    /// stray copy of the header sitting in a disk buffer. The game's text encoding uses tab (0x09)
    /// for the space character, which is why these read oddly.
    /// </summary>
    public static readonly Anchor[] Validators =
    {
        new("experience label", Ascii("Experience\t"), 0x0188),
        new("hit points label", Ascii("Hit\tPoints\t:"), 0x01AC),
        new("flamesword", Ascii("Magical\tFlamesword"), 0xC8DF),
    };

    /// <summary>How many of <see cref="Validators"/> must line up before a DGROUP candidate is accepted.</summary>
    public const int MinValidators = 2;

    // --- little-endian accessors ---------------------------------------------

    public static byte ReadU8(byte[] b, int off) => b[off];

    public static ushort ReadU16(byte[] b, int off) => (ushort)(b[off] | (b[off + 1] << 8));

    public static uint ReadU32(byte[] b, int off) =>
        (uint)(b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24));

    public static void WriteU8(byte[] b, int off, byte v) => b[off] = v;

    public static void WriteU16(byte[] b, int off, ushort v)
    {
        b[off] = (byte)v;
        b[off + 1] = (byte)(v >> 8);
    }

    public static void WriteU32(byte[] b, int off, uint v)
    {
        b[off] = (byte)v;
        b[off + 1] = (byte)(v >> 8);
        b[off + 2] = (byte)(v >> 16);
        b[off + 3] = (byte)(v >> 24);
    }

    /// <summary>Reads the NUL-terminated ASCII name at <see cref="OffName"/>.</summary>
    public static string ReadName(byte[] b, int baseOffset = 0)
    {
        int start = baseOffset + OffName;
        int len = 0;
        while (len < NameLength && b[start + len] != 0) len++;
        return Encoding.ASCII.GetString(b, start, len).TrimEnd();
    }

    /// <summary>
    /// Characters a name may contain. <see cref="IsWritableName"/> and
    /// <see cref="LooksLikeRecord"/> share this so the editor can never write a name the locator
    /// would then refuse to recognise — the two rules drifting apart is exactly how a trainer loses
    /// the character it has just renamed.
    /// </summary>
    public static bool IsNameCharacter(char c) =>
        c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or ' ' or '\'' or '-' or '.';

    /// <summary>True when a name may begin with this character.</summary>
    public static bool IsNameFirstCharacter(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    /// <summary>Shortest name the locator will still recognise.</summary>
    public const int MinNameLength = 2;

    /// <summary>
    /// True when <paramref name="name"/> would still leave a record that
    /// <see cref="LooksLikeRecord"/> recognises: at least <see cref="MinNameLength"/> characters
    /// survive sanitising, the first is a letter, and every character is one a name may contain.
    /// Writing a name that fails this would make the trainer unable to find the character again.
    /// </summary>
    public static bool IsWritableName(string? name)
    {
        string clean = Sanitise(name);
        if (clean.Length < MinNameLength) return false;
        if (!IsNameFirstCharacter(clean[0])) return false;
        foreach (char c in clean)
            if (!IsNameCharacter(c)) return false;
        return true;
    }

    /// <summary>
    /// Writes <paramref name="name"/> at <see cref="OffName"/>, truncated to
    /// <see cref="MaxNameLength"/> and NUL-padded across the whole field.
    /// </summary>
    public static void WriteName(byte[] b, string name, int baseOffset = 0)
    {
        int start = baseOffset + OffName;
        Array.Clear(b, start, NameLength);
        var bytes = Encoding.ASCII.GetBytes(Sanitise(name));
        Array.Copy(bytes, 0, b, start, bytes.Length);
    }

    /// <summary>
    /// The exact text <see cref="WriteName"/> would store: non-printable and non-ASCII characters
    /// replaced by spaces, truncated to <see cref="MaxNameLength"/>, trailing blanks removed.
    /// </summary>
    private static string Sanitise(string? name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var clean = new StringBuilder(MaxNameLength);
        foreach (char c in name)
        {
            if (clean.Length >= MaxNameLength) break;
            clean.Append(c is >= ' ' and < (char)127 ? c : ' ');
        }
        return clean.ToString().TrimEnd();
    }

    /// <summary>
    /// True when the window at <paramref name="off"/> looks like a live character record.
    ///
    /// The shape is specific: a name of at least <see cref="MinNameLength"/> characters that starts
    /// with a letter and contains only <see cref="IsNameCharacter"/> ones; seven attribute records
    /// whose current and maximum sit at or below the natural maximum (see
    /// <see cref="AttributeCopies"/> — they are <b>not</b> required to be equal, and zero is allowed,
    /// because a Wraith drains them); a plausible level; hit points within their maximum; and a
    /// next-level threshold at or above the experience.
    ///
    /// It is strong enough to confirm a record the anchors already found. It is <b>not</b> strong
    /// enough to go hunting with on its own, which is why the structural scan in
    /// <c>GameLocator</c> is opt-in.
    /// </summary>
    public static bool LooksLikeRecord(byte[] b, int off)
    {
        if (off < 0 || off > b.Length - LiveFieldsLength) return false;

        // Name: at least two characters, starting with a letter, drawn from what a name can contain,
        // and terminated inside the field. One printable byte is not a name — that alone let a run of
        // unrelated heap data pass when the structural fallback was pointed at the wrong process.
        int nameLen = 0;
        while (nameLen < NameLength && b[off + OffName + nameLen] != 0)
        {
            if (!IsNameCharacter((char)b[off + OffName + nameLen])) return false;
            nameLen++;
        }
        if (nameLen < MinNameLength || nameLen >= NameLength) return false;
        if (!IsNameFirstCharacter((char)b[off + OffName])) return false;

        // Seven attribute records: current, maximum, natural maximum, then a fraction byte.
        //
        // The first three are equal in a healthy character, which is all that was visible until a
        // Wraith drained one live: its touch takes the current value *and* the maximum down but
        // leaves the natural maximum, so the record reads 0, 0, 9. Requiring all three to agree made
        // exactly the character whose owner most wants a trainer impossible to find. What actually
        // holds is that neither the current value nor the maximum can exceed the natural maximum.
        //
        // A zero attribute is likewise not rejected. An all-zero window is still thrown out — by the
        // name and hit-point checks around this loop.
        for (int i = 0; i < AttributeCount; i++)
        {
            int a = off + AttributeOffset(i);
            if (b[a] > b[a + 1] || b[a + 1] > b[a + 2]) return false;
        }

        if (b[off + OffLevel] > LevelCeiling) return false;

        uint hp = ReadU32(b, off + OffHitPoints);
        uint hpMax = ReadU32(b, off + OffHitPointsMax);
        if (hpMax == 0 || hpMax > HitPointCeiling) return false;
        if (hp > hpMax) return false;

        // The game always keeps the next-level threshold at or above the current experience — it
        // recomputes it to twice the experience on every level-up. A window where the threshold is
        // *below* the experience is not a character record.
        uint experience = ReadU32(b, off + OffExperience);
        uint nextLevel = ReadU32(b, off + OffNextLevelExp);
        if (experience > ExperienceCeiling || nextLevel > ExperienceCeiling) return false;
        return nextLevel >= experience;
    }
}
