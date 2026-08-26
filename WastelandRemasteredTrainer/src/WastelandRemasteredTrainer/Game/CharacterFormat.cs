namespace WastelandRemasteredTrainer.Game;

/// <summary>
/// Field offsets for <c>Player</c> and <c>Party</c> in Wasteland Remastered, extracted from
/// the IL2CPP field-offset table in <c>GameAssembly.dll</c>.
///
/// <para>The remaster preserves the original Wasteland's character model — seven single-byte
/// attributes, packed skill and inventory arrays, 24-bit-style fields widened to int32 — but
/// wraps it in IL2CPP objects. Field offsets are from the object base and include the 16-byte
/// IL2CPP object header, so the first instance field (<c>m_name</c>) is at +0x10.</para>
///
/// <para>Offsets were extracted by following the <c>Il2CppMetadataRegistration</c> field-offset
/// table in the binary. The IL2CPP compiler reordered fields for optimal layout, so instance
/// fields do not follow declaration order. Const fields have offset 0 and are not stored.</para>
/// </summary>
public static class CharacterFormat
{
    // --- Player instance field offsets (from IL2CPP field-offset table) ----------
    /// <summary><c>m_name</c> — pointer to a managed <c>Il2CppString</c> (display name).</summary>
    public const int OffName = 0x10;

    /// <summary><c>m_uniqueId</c> — int64.</summary>
    public const int OffUniqueId = 0x18;

    /// <summary><c>CName</c> — pointer to a managed byte[] (character name bytes).</summary>
    public const int OffCName = 0x20;

    // --- attributes (one byte each, consecutive) --------------------------------
    /// <summary><c>Strength</c> — first of seven single-byte attributes.</summary>
    public const int OffStrength = 0x28;
    public const int OffIQ = 0x29;
    public const int OffLuck = 0x2A;
    public const int OffSpeed = 0x2B;
    public const int OffAgility = 0x2C;
    public const int OffDextermity = 0x2D;
    public const int OffCharisma = 0x2E;
    public const int AttributeCount = 7;

    /// <summary><c>Money</c> — int32.</summary>
    public const int OffMoney = 0x30;

    /// <summary><c>Sex</c> — byte (0 = Male, 1 = Female).</summary>
    public const int OffSex = 0x34;

    /// <summary><c>Nationality</c> — byte (0 US, 1 Russian, 2 Mexican, 3 Indian, 4 Chinese).</summary>
    public const int OffNationality = 0x35;

    /// <summary><c>AC</c> — byte (armor class).</summary>
    public const int OffAC = 0x36;

    /// <summary><c>MAXCON</c> — int32 (maximum constitution / hit points).</summary>
    public const int OffMaxCon = 0x38;

    /// <summary><c>CURRCON</c> — int32 (current constitution / hit points).</summary>
    public const int OffCurCon = 0x3C;

    /// <summary><c>WEAPON</c> — byte (equipped weapon id).</summary>
    public const int OffWeapon = 0x40;

    /// <summary><c>SKILLPOINTS</c> — byte (unspent skill points).</summary>
    public const int OffSkillPoints = 0x41;

    /// <summary><c>EXPERIENCE</c> — int32.</summary>
    public const int OffExperience = 0x44;

    /// <summary><c>LEVEL</c> — byte.</summary>
    public const int OffLevel = 0x48;

    /// <summary><c>ARMOR</c> — byte (equipped armor id).</summary>
    public const int OffArmor = 0x49;

    /// <summary><c>UNCCON</c> — int32 (unconscious CON threshold).</summary>
    public const int OffUncCon = 0x4C;

    /// <summary><c>DISEASE</c> — byte.</summary>
    public const int OffDisease = 0x50;

    // --- NPC flags (one byte each) ----------------------------------------------
    public const int OffNPC = 0x51;
    public const int OffNPCCom = 0x52;
    public const int OffNPCItem = 0x53;
    public const int OffNPCSkill = 0x54;
    public const int OffNPCAtt = 0x55;
    public const int OffNPCTrade = 0x56;
    public const int OffNPCGreed = 0x57;
    public const int OffNPCIMsg = 0x58;
    public const int OffNPCRecChr = 0x59;

    /// <summary><c>RANK</c> — pointer to a managed byte[] (rank string bytes).</summary>
    public const int OffRank = 0x60;

    /// <summary><c>WLSWON</c> — int32 (Wasteland skills won).</summary>
    public const int OffWlsWon = 0x68;

    /// <summary><c>WLSVER</c> — byte (version).</summary>
    public const int OffWlsVer = 0x69;

    /// <summary><c>SKILLS</c> — pointer to a managed byte[] (packed skillId+level pairs).</summary>
    public const int OffSkills = 0x70;

    /// <summary><c>ITEMS</c> — pointer to a managed byte[] (packed itemId+ammo/qty pairs).</summary>
    public const int OffItems = 0x78;

    /// <summary><c>SE_Name</c> — pointer to a managed string (saved editor name).</summary>
    public const int OffSEName = 0x80;

    /// <summary><c>SE_Rank</c> — pointer to a managed string (saved editor rank).</summary>
    public const int OffSERank = 0x88;

    /// <summary><c>m_hardwiredCameo</c> — int32.</summary>
    public const int OffHardwiredCameo = 0x90;

    /// <summary><c>SE_Items</c> — pointer to a List (saved editor items).</summary>
    public const int OffSEItems = 0x98;

    /// <summary><c>SE_Skills</c> — pointer to a List (saved editor skills).</summary>
    public const int OffSESkills = 0xA0;

    /// <summary><c>m_fireType</c> — int32.</summary>
    public const int OffFireType = 0xA8;

    /// <summary><c>m_clipSize</c> — int32.</summary>
    public const int OffClipSize = 0xAC;

    /// <summary>Approximate size of the Player object's instance fields.</summary>
    public const int ObjectSize = 0xB0;

    /// <summary>Bytes the locator reads when validating a candidate Player.</summary>
    public const int ProbeSize = 0x60;

    // --- Party field offsets ----------------------------------------------------
    /// <summary>Static <c>m_instance</c> — first field of the static block.</summary>
    public const int PartyInstanceStatic = 0x00;

    /// <summary><c>players</c> — <c>List&lt;Player&gt;</c> reference (instance field at +0x10).</summary>
    public const int PartyPlayers = 0x10;

    // --- packed skill/inventory format (same as original Wasteland) --------------
    /// <summary>Each skill/inventory slot is 2 bytes: (id, level/qty).</summary>
    public const int SlotSize = 2;

    /// <summary>Skills array: 30 slots × 2 bytes, 0x00-terminated.</summary>
    public const int SkillBlockBytes = GameFacts.SkillSlots * SlotSize;

    /// <summary>Items array: 30 slots × 2 bytes.</summary>
    public const int ItemBlockBytes = GameFacts.ItemSlots * SlotSize;

    /// <summary>Bit 7 of an inventory quantity byte flags a jammed weapon.</summary>
    public const int InventoryJammedFlag = 0x80;

    /// <summary>Low 7 bits of an inventory quantity byte are the ammo/charge count.</summary>
    public const int InventoryCountMask = 0x7F;

    /// <summary>The ammo/charge count carried in an inventory quantity byte, jam bit removed.</summary>
    public static int AmmoOf(int quantityByte) => quantityByte & InventoryCountMask;

    /// <summary>True when an inventory quantity byte has the jammed-weapon bit set.</summary>
    public static bool IsJammed(int quantityByte) => (quantityByte & InventoryJammedFlag) != 0;

    /// <summary>Builds an inventory quantity byte from a count and a jam flag.</summary>
    public static byte PackQuantity(int ammo, bool jammed) =>
        (byte)((ammo & InventoryCountMask) | (jammed ? InventoryJammedFlag : 0));

    // --- lookup tables ----------------------------------------------------------
    /// <summary>Attribute abbreviations in record order (STR, IQ, LCK, SPD, AGL, DEX, CHR).</summary>
    public static readonly string[] AttributeNames =
        { "STR", "IQ", "LCK", "SPD", "AGL", "DEX", "CHR" };

    public static readonly string[] Genders = { "Male", "Female" };
    public static string GenderName(int v) => v >= 0 && v < Genders.Length ? Genders[v] : $"?({v})";

    public static readonly string[] Nationalities =
        { "U.S.", "Russian", "Mexican", "Indian", "Chinese" };
    public static string NationalityName(int v) =>
        v >= 0 && v < Nationalities.Length ? Nationalities[v] : $"?({v})";

    // --- static field offsets for other key types --------------------------------
    /// <summary>Wasteland singleton <c>m_instance</c> — first static field.</summary>
    public const int WastelandInstanceStatic = 0x00;

    /// <summary>Wasteland instance <c>m_partyManager</c> — at +0x98.</summary>
    public const int WastelandPartyManager = 0x98;

    /// <summary>PartyManager singleton <c>m_instance</c> — first static field.</summary>
    public const int PartyManagerInstanceStatic = 0x00;

    /// <summary>PartyManager instance <c>m_saveData</c> — at +0x28.</summary>
    public const int PartyManagerSaveData = 0x28;

    // --- CoreSave offsets (save data) -------------------------------------------
    public const int CoreSaveMapX = 0x10;
    public const int CoreSaveMapY = 0x11;
    public const int CoreSaveNumberInParty = 0x18;
    public const int CoreSaveCurrentMap = 0x1A;
    public const int CoreSaveClock = 0x20;

    /// <summary>
    /// How far below zero a character's CON may go and still be a real character. Wasteland
    /// takes a wounded ranger through negative CON on the way to dead, so the floor has to sit
    /// well below zero — a scan that insisted on CON >= 0 would reject exactly the characters
    /// someone opens a trainer to rescue.
    /// </summary>
    public const int MinPlausibleCon = -10_000;

    /// <summary>
    /// Plausibility check on a candidate <c>Player</c> object. This is a <b>shape</b> test for
    /// the structural fallback scan only — it answers "could this block of bytes be a character
    /// record", not "is this character in good standing".
    ///
    /// <para>Never use it to filter objects already known to be Players (say, entries of
    /// <c>Party.players</c>): a heuristic applied to a confirmed object can only lose real
    /// characters. <see cref="Il2Cpp.IsInstanceOf"/> is the right test there.</para>
    ///
    /// <para>The ceilings are deliberately loose. Attributes and money are checked against
    /// generous multiples of the trainer's own limits rather than the limits themselves,
    /// because a character the trainer has already edited must still be found on the next
    /// scan.</para>
    /// </summary>
    public static bool LooksLikePlayer(ReadOnlySpan<byte> buf)
    {
        if (buf.Length < ProbeSize) return false;

        int maxCon = ReadI32(buf, OffMaxCon);
        if (maxCon <= 0 || maxCon > 100_000) return false;

        // Current CON may be negative (dying) but never above the maximum.
        int curCon = ReadI32(buf, OffCurCon);
        if (curCon < MinPlausibleCon || curCon > maxCon) return false;

        int level = buf[OffLevel];
        if (level > GameFacts.MaxLevel) return false;

        int money = ReadI32(buf, OffMoney);
        if (money < 0 || money > GameFacts.MaxMoney * 10) return false;

        int exp = ReadI32(buf, OffExperience);
        if (exp < 0 || exp > GameFacts.MaxExperience * 100) return false;

        // Every attribute is a byte in 1..99: the game's own range tops out far below that,
        // and the trainer clamps its own writes to the same ceiling, so a maxed-out character
        // still passes. A zero attribute means this is not a character record.
        for (int i = 0; i < AttributeCount; i++)
        {
            int v = buf[OffStrength + i];
            if (v == 0 || v > GameFacts.MaxAttribute) return false;
        }

        int sex = buf[OffSex];
        if (sex > 1) return false;

        return true;
    }

    public static int ReadI32(ReadOnlySpan<byte> buf, int off) =>
        buf[off] | (buf[off + 1] << 8) | (buf[off + 2] << 16) | (buf[off + 3] << 24);
}
