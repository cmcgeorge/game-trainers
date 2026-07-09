namespace MightAndMagic1Trainer.Game;

/// <summary>
/// Layout constants for the Might &amp; Magic 1 character record, reverse-engineered
/// from <c>ROSTER.DTA</c> and a Cheat Engine memory dump (MM.CEM).
///
/// A character record is 0x7F (127) meaningful bytes. In the on-disk roster the
/// records are packed end-to-end every 127 bytes. In the game's live memory they
/// are padded to a 128-byte (0x80) stride (confirmed by the memory dump), so the
/// trainer scans/walks memory using <see cref="MemoryStride"/>.
///
/// Field offsets are relative to the start of a record. Offsets marked "best-effort"
/// are confident in position/size but the human label may be imperfect — the raw
/// hex view is always authoritative.
/// </summary>
public static class RosterFormat
{
    /// <summary>Number of meaningful bytes in a record (on-disk record size).</summary>
    public const int RecordSize = 0x7F;        // 127

    /// <summary>Record-to-record stride in live process memory (padded).</summary>
    public const int MemoryStride = 0x80;      // 128

    /// <summary>Record-to-record stride on disk (packed).</summary>
    public const int FileStride = 0x7F;        // 127

    /// <summary>Maximum character slots the game tracks in the roster.</summary>
    public const int MaxSlots = 18;

    // --- Field offsets (relative to record start) -------------------------------
    public const int OffName = 0x00;           // 15 ASCII bytes, null padded
    public const int NameLength = 15;

    public const int OffSex = 0x10;            // 1 = Male, 2 = Female
    public const int OffAlignmentOrig = 0x11;  // alignment as created
    public const int OffAlignment = 0x12;       // current alignment
    public const int OffRace = 0x13;           // 1 = Human, ...
    public const int OffClass = 0x14;          // 1..6

    /// <summary>
    /// Seven primary attributes, each stored as two consecutive bytes:
    /// [normal (permanent), temp (active)]. The game uses the temp value; resting
    /// resets temp back to normal. Order: Intellect, Might, Personality, Endurance,
    /// Speed, Accuracy, Luck. (Matches ryz/MightAndMagic-SaveEditor.)
    /// </summary>
    public const int OffStats = 0x15;          // 7 * (normal,temp) = 14 bytes -> 0x15..0x22
    public const int StatCount = 7;

    public const int OffLevelCur = 0x23;       // current level (drainable)
    public const int OffLevelMax = 0x24;       // base level
    public const int OffAge = 0x25;            // age in years
    public const int OffTimesRested = 0x26;    // rest counter (rolls into age at 0xFF)

    public const int OffExperience = 0x27;     // 32-bit LE experience points

    public const int OffSpCur = 0x2B;          // 16-bit current spell points
    public const int OffSpMax = 0x2D;          // 16-bit maximum spell points
    public const int OffSpellLevel = 0x2F;     // highest spell level known (byte)

    public const int OffGems = 0x31;           // 16-bit gems

    public const int OffHpCur = 0x33;          // 16-bit current hit points
    public const int OffHpMod = 0x35;          // 16-bit modified (temporary) max HP
    public const int OffHpMax = 0x37;          // 16-bit true maximum hit points

    public const int OffGold = 0x39;           // 24-bit (3-byte) gold

    public const int OffArmorClassItems = 0x3C; // AC contributed by items
    public const int OffArmorClass = 0x3D;     // total armor class

    public const int OffFood = 0x3E;           // food (byte)
    public const int OffCondition = 0x3F;      // 0 = OK (Good)

    public const int OffEquipment = 0x40;      // 6 equipped item ids
    public const int OffBackpack = 0x46;       // 6 backpack item ids
    public const int OffEquipmentCharges = 0x4C; // 6 charge counts, one per equipped slot
    public const int OffBackpackCharges = 0x52;  // 6 charge counts, one per backpack slot
    public const int ItemSlotCount = 6;          // slots in each of equipment / backpack

    /// <summary>
    /// Eight elemental/effect resistances, stored like the attributes as two consecutive
    /// bytes each: [normal (permanent), temp (active)], in percent. Order matches
    /// ryz/MightAndMagic-SaveEditor: Magic, Fire, Cold, Electricity, Acid, Fear, Poison,
    /// Sleep. Every character innately has Fear 70/70 and Sleep 25/25 (observed across the
    /// sample roster and fresh characters) — the Fear pair doubles as the scan marker below.
    /// </summary>
    public const int OffResistances = 0x58;    // 8 * (normal,temp) = 16 bytes -> 0x58..0x67
    public const int ResistanceCount = 8;

    /// <summary>The slot-index byte the game stores in the record's final byte.</summary>
    public const int OffSlotIndex = 0x7E;

    // Scan-signature marker: two bytes that are constant across every observed record
    // (both the bundled sample and freshly-created characters). This is the Fear
    // resistance pair — every character is created with an innate 70/70 (0x46/0x46)
    // Fear resistance, which makes it a reliable signature byte pair. Used only by
    // LooksLikeRecord to disambiguate the roster from random memory.
    //
    // The trainer's own "Max resistances" command rewrites every resistance — Fear
    // included — to 100 (0x64), so LooksLikeRecord also accepts the maxed pair
    // (100/100). Without that, maxing resistances would make the very next roster
    // scan (re-attach, restart, re-scan) fail to find the still-valid records.
    //
    // NOTE: 0x70 was previously also treated as a constant marker (== 0x24), but that
    // byte lives in the record's undecoded tail and actually varies with game state:
    // it is 0x24 for the (progressed) sample characters yet 0x00 for freshly rolled
    // ones. Requiring it made the loader reject brand-new rosters, so it was dropped.
    public const int MarkerOffsetA = 0x62; public const byte MarkerByteA = 0x46; // two bytes
    public const byte MarkerByteMaxed = 100;   // trainer-maxed Fear (100/100); see above

    public static readonly string[] Stats =
        { "Intellect", "Might", "Personality", "Endurance", "Speed", "Accuracy", "Luck" };

    public static readonly string[] Resistances =
        { "Magic", "Fire", "Cold", "Electricity", "Acid", "Fear", "Poison", "Sleep" };

    /// <summary>Known character conditions; index = stored byte value (0 = OK).</summary>
    public static readonly string[] Conditions =
        { "OK", "Asleep", "Blinded", "Silenced", "Diseased", "Poisoned", "Paralyzed",
          "Unconscious", "Dead", "Stoned", "Eradicated" };

    public static string ConditionName(int c) =>
        c >= 0 && c < Conditions.Length ? Conditions[c] : $"Afflicted (0x{c:X2})";

    public static readonly string[] Classes =
        { "(none)", "Knight", "Paladin", "Archer", "Cleric", "Sorcerer", "Robber" };

    public static readonly string[] Sexes = { "(none)", "Male", "Female" };

    public static string ClassName(int c) => c >= 0 && c < Classes.Length ? Classes[c] : $"?({c})";
    public static string SexName(int s) => s >= 0 && s < Sexes.Length ? Sexes[s] : $"?({s})";

    /// <summary>
    /// Heuristic: does the 127-byte span starting at <paramref name="i"/> in
    /// <paramref name="buf"/> look like a valid character record? Used by the
    /// memory scanner to locate the roster regardless of where the OS mapped it.
    /// Combines several invariants observed across every sampled record.
    /// </summary>
    public static bool LooksLikeRecord(byte[] buf, int i)
    {
        if (i < 0 || i + RecordSize > buf.Length)
            return false;

        // Name: first byte must be an uppercase letter; the remaining bytes of the
        // 15-byte field must be name characters (A-Z, a-z, 0-9, space, ' - .) up to
        // the first NUL, then NUL padding only.
        byte first = buf[i + OffName];
        if (first < (byte)'A' || first > (byte)'Z')
            return false;

        bool sawNull = false;
        for (int n = 0; n < NameLength; n++)
        {
            byte b = buf[i + OffName + n];
            if (b == 0) { sawNull = true; continue; }
            if (sawNull) return false;                       // text after NUL = not a name
            bool ok = (b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z')
                      || (b >= '0' && b <= '9') || b == ' ' || b == '\'' || b == '-' || b == '.';
            if (!ok) return false;
        }

        // Class must be 1..6.
        int cls = buf[i + OffClass];
        if (cls < 1 || cls > 6) return false;

        // Sex must be 1..2.
        int sex = buf[i + OffSex];
        if (sex < 1 || sex > 2) return false;

        // Structural marker: the Fear resistance pair, stable across every sampled
        // record (innate 70/70) and after the trainer's own "Max resistances" (100/100).
        // It makes the scan signature far more specific and keeps false positives from
        // chaining into a fake roster run, while still recognising maxed-out parties.
        byte m0 = buf[i + MarkerOffsetA];
        byte m1 = buf[i + MarkerOffsetA + 1];
        bool markerOk = (m0 == MarkerByteA && m1 == MarkerByteA)
                        || (m0 == MarkerByteMaxed && m1 == MarkerByteMaxed);
        if (!markerOk)
            return false;

        return true;
    }
}
