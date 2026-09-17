namespace Space1889Trainer.Game;

/// <summary>
/// Layout of Space 1889's 20,198-byte game-state block — the memory <c>1889.COM</c> allocates once and
/// every program shares, and the exact contents of a <c>*.SAV</c> file (and of the shipped <c>DEF.S</c>).
/// <para>
/// Confidence markers: <b>[Confirmed]</b> = pinned against the running game (a poke read back on the
/// game's own screen, or a live diff of a known action); <b>[Static]</b> = unambiguous in the
/// disassembly but not exercised live; <b>[Inferred]</b> = a best reading.
/// </para>
/// </summary>
public static class StateFormat
{
    // --- the block ---------------------------------------------------------------

    /// <summary>Bytes in the block and in a save file. [Confirmed: M.EXE writes 0x4EE6 bytes; DEF.S matched RAM 20,195/20,198]</summary>
    public const int BlockLength = 0x4EE6;

    // --- character records -------------------------------------------------------

    /// <summary>Bytes per character record. [Confirmed: code indexes <c>slot * 0xFF</c>]</summary>
    public const int RecordSize = 0xFF;

    /// <summary>Adventurer slots (the party proper). [Confirmed]</summary>
    public const int CharacterSlots = 5;

    /// <summary>The sixth record is the party bank account, not a person. [Confirmed]</summary>
    public const int PartyAccountSlot = 5;

    /// <summary>The name the game gives the bank-account record. [Confirmed]</summary>
    public const string PartyAccountName = "PARTY ACCT.";

    /// <summary>Record +0x00: wealth in pennies, u32. [Confirmed: a poke read back on the PARTY screen]</summary>
    public const int WealthOffset = 0x00;

    /// <summary>Record +0x04: monthly income, u32, paid into the party account whenever day % 30 == 0. [Confirmed: day 31 paid exactly the sum]</summary>
    public const int IncomeOffset = 0x04;

    /// <summary>Record +0x08: PERSON'S WEIGHT in lb, u32 (character generator: 100 + 20 × STR). [Confirmed on the PARTY screen's second page]</summary>
    public const int BodyWeightOffset = 0x08;

    /// <summary>Record +0x0C: maximum health = STR + END, s8. [Confirmed: "HEALTH: a/b" tracked a poke]</summary>
    public const int MaxHealthOffset = 0x0C;

    /// <summary>Record +0x0D: current health, s8. [Confirmed]</summary>
    public const int HealthOffset = 0x0D;

    /// <summary>Record +0x0E: FATIGUE LEVEL, s8; equal to STR, AGI or END puts the character out of action. [Confirmed: poke read back]</summary>
    public const int FatigueOffset = 0x0E;

    /// <summary>Record +0x0F: MENTAL (insanity), s8; in a fight the character misbehaves when rand()%6 &lt; mental. [Confirmed display; effect Static]</summary>
    public const int MentalOffset = 0x0F;

    /// <summary>Record +0x10: 1 = dead / empty slot. Death also zeroes the rest of the record. [Static]</summary>
    public const int DeadOffset = 0x10;

    /// <summary>Record +0x11: owns a horse (0/1). [Static]</summary>
    public const int HorseOffset = 0x11;

    /// <summary>Record +0x12: portrait 0..4. [Static]</summary>
    public const int PortraitOffset = 0x12;

    /// <summary>Record +0x13: the six attribute bytes, STR AGI END INT CHA SOC. [Confirmed against the PARTY screen]</summary>
    public const int AttributesOffset = 0x13;

    /// <summary>Attribute count.</summary>
    public const int AttributeCount = 6;

    /// <summary>Record +0x19: the 24 skill bytes, four per attribute in the PARTY screen's order. [Confirmed for all five default characters]</summary>
    public const int SkillsOffset = 0x19;

    /// <summary>Skill count.</summary>
    public const int SkillCount = 24;

    /// <summary>Record +0x49: first career id (89CAREER.DAT id). [Static]</summary>
    public const int Career1IdOffset = 0x49;

    /// <summary>Record +0x4A: second career id (0 = none — ambiguous with ARMY (1), so check the name). [Static]</summary>
    public const int Career2IdOffset = 0x4A;

    /// <summary>Record +0x4B: 1-based inventory slot of the armour worn (0 = none). [Static]</summary>
    public const int ArmorSlotOffset = 0x4B;

    /// <summary>Record +0x4C: 1-based inventory slot of the weapon in hand (0 = FISTS). [Confirmed: status panel showed WEAPON: LIGHT REVOLVER]</summary>
    public const int WeaponSlotOffset = 0x4C;

    /// <summary>Record +0x4D: the inventory, <see cref="InventorySlots"/> × <see cref="InventoryEntrySize"/>. [Confirmed]</summary>
    public const int InventoryOffset = 0x4D;

    /// <summary>Inventory entries per character (the PARTY screen draws a 7 × 3 grid). [Confirmed]</summary>
    public const int InventorySlots = 21;

    /// <summary>Bytes per inventory entry: item id, loaded-ammo index, u16 ROUNDS, IN GUN. [Confirmed]</summary>
    public const int InventoryEntrySize = 5;

    /// <summary>Record +0xB6: first career name, 30 bytes. [Confirmed]</summary>
    public const int Career1NameOffset = 0xB6;

    /// <summary>Record +0xD4: second career name, 30 bytes. [Static]</summary>
    public const int Career2NameOffset = 0xD4;

    /// <summary>Width of a career-name field.</summary>
    public const int CareerNameLength = 30;

    /// <summary>Record +0xF2: the NUL-terminated name. [Confirmed]</summary>
    public const int NameOffset = 0xF2;

    /// <summary>Width of the name field, terminator included. [Confirmed: the sex byte follows at +0xFE]</summary>
    public const int NameLength = 12;

    /// <summary>Record +0xFE: sex, <c>'m'</c> (0x6D) or <c>'f'</c> (0x66). [Confirmed]</summary>
    public const int SexOffset = 0xFE;

    /// <summary>Block offset of <see cref="PartyAccountName"/> (slot 5 + <see cref="NameOffset"/>).</summary>
    public const int PartyAccountNameOffset = PartyAccountSlot * RecordSize + NameOffset;

    // --- party-level -----------------------------------------------------------------

    /// <summary>Party bank account in pennies, u32 (record 5 +0x00). [Confirmed: monthly income landed here]</summary>
    public const int PartyAccountOffset = PartyAccountSlot * RecordSize + WealthOffset;

    /// <summary>Marching order: five bytes, slot → character index; 0x5FA is the leader. [Static]</summary>
    public const int MarchingOrderOffset = 0x05FA;

    /// <summary>"In use" flags, 5 × 21 bytes: <c>0x5FF + character·21 + slot</c>. [Confirmed: set for the poked revolver]</summary>
    public const int InUseFlagsOffset = 0x05FF;

    /// <summary>Ground objects: six u16 counts (planets 1..6) followed by 203 × 11-byte records per planet. [Static]</summary>
    public const int GroundObjectsOffset = 0x0668;

    // --- location, clock --------------------------------------------------------------

    /// <summary>Current area record from A.SET/B.SET (dark-map flags, per-map positions, area name), 0x47 bytes. [Static]</summary>
    public const int AreaRecordOffset = 0x3ACA;

    /// <summary>Area name inside the area record, 15 bytes. [Static]</summary>
    public const int AreaNameOffset = 0x3B02;

    /// <summary>Planet name inside the planet record, 15 bytes. [Static]</summary>
    public const int PlanetNameOffset = 0x3B15;

    /// <summary>Party FOOD, u16, 0..32767 (two per living character per day). [Confirmed: FOOD: 1000 after a poke]</summary>
    public const int FoodOffset = 0x3B24;

    /// <summary>Largest FOOD the market lets you reach ("YOU CANNOT BUY ANY MORE FOOD").</summary>
    public const int MaxFood = 0x7FFF;

    /// <summary>DAY counter, u32. [Confirmed: tracked the DAY: line]</summary>
    public const int DayOffset = 0x3B2A;

    /// <summary>Current planet, s8: 0 SPACE, 1 EARTH, 2 LUNA, 3 MERCURY, 4 MARS, 5 VENUS, 6 SHIP. [Confirmed: 1 in London]</summary>
    public const int PlanetOffset = 0x3B32;

    /// <summary>Area within the planet (0 = the planet's surface map), s8. [Confirmed: 1 = London]</summary>
    public const int AreaOffset = 0x3B33;

    /// <summary>Map within the area (0 = the area's main map), s8. [Confirmed: 3 = the museum]</summary>
    public const int MapOffset = 0x3B34;

    /// <summary>Planet to return to after leaving a boarded ship, s8. [Static]</summary>
    public const int ReturnPlanetOffset = 0x3B35;

    // --- NPC tables ---------------------------------------------------------------------

    /// <summary>1000-bit "NPC removed/killed" set. [Static]</summary>
    public const int NpcRemovedBitsOffset = 0x3B36;

    /// <summary>Per-NPC dialogue progress nibbles (1000 ids). [Static]</summary>
    public const int NpcProgressOffset = 0x3CBC;

    /// <summary>Per-NPC hit points, 1000 × s8 (default 10). [Static]</summary>
    public const int NpcHitPointsOffset = 0x3EB0;

    /// <summary>NPC ids the tables cover.</summary>
    public const int NpcIdCount = 1000;

    // --- position ----------------------------------------------------------------------

    /// <summary>Party row × 2 on the current map, s16. [Confirmed: +6 through a door; the view follows a poke]</summary>
    public const int RowOffset = 0x4298;

    /// <summary>Party column × 2 on the current map, s16. [Confirmed: ±2 per step; a poke moved the party]</summary>
    public const int ColumnOffset = 0x429A;

    /// <summary>Current map lit (1) / dark with no light (0), u16. [Static]</summary>
    public const int LitOffset = 0x429C;

    /// <summary>Row × 2 before the last step, s16. [Confirmed: the trail value]</summary>
    public const int PreviousRowOffset = 0x429E;

    /// <summary>Column × 2 before the last step, s16. [Confirmed]</summary>
    public const int PreviousColumnOffset = 0x42A0;

    // --- ether flyer -----------------------------------------------------------------------

    /// <summary>Owns a built ether flyer (0/1). [Static]</summary>
    public const int HasFlyerOffset = 0x42A2;

    /// <summary>AMMONIA fuel loaded (0/1). [Static]</summary>
    public const int AmmoniaLoadedOffset = 0x42A3;

    /// <summary>GLOW CRYSTAL fuel loaded (0/1). [Static]</summary>
    public const int GlowCrystalsLoadedOffset = 0x42A4;

    /// <summary>A space fight / boarding is in progress and S.EXE will resume it (0/1). [Static]</summary>
    public const int CombatResumeOffset = 0x42A5;

    /// <summary>The player's flyer, 10 bytes: hull, lift, propeller, power, engine, engine×2, armour, top gun, low gun, altitude cap. [Static]</summary>
    public const int FlyerOffset = 0x42B1;

    /// <summary>Bytes in a ship record.</summary>
    public const int FlyerRecordSize = 10;

    /// <summary>Flyer record field offsets.</summary>
    public const int FlyerHull = 0, FlyerLift = 1, FlyerPropeller = 2, FlyerPower = 3, FlyerEngine = 4,
                     FlyerEngineShadow = 5, FlyerArmor = 6, FlyerTopGun = 7, FlyerLowGun = 8, FlyerAltitudeCap = 9;

    /// <summary>Value of the current flyer (trade-in credit), u32. [Static]</summary>
    public const int FlyerValueOffset = 0x42BB;

    /// <summary>The current enemy ship, same 10-byte layout. [Static]</summary>
    public const int EnemyShipOffset = 0x42BF;

    /// <summary>Player component-damage bits, u16 (non-zero adds £100 to repairs). [Static]</summary>
    public const int FlyerDamageBitsOffset = 0x42C9;

    /// <summary>Player HULL HITS, u16. [Static]</summary>
    public const int FlyerHullHitsOffset = 0x42CB;

    /// <summary>Player "W" damage, u16. [Static]</summary>
    public const int FlyerWDamageOffset = 0x42CD;

    /// <summary>Bridge stations → character index (−1 = empty): captain, helmsman, trimsman, top gun, low gun. [Static]</summary>
    public const int BridgeCrewOffset = 0x42D5;

    /// <summary>Story flags, u16 (see <see cref="StoryFlagBook"/>). [Static]</summary>
    public const int StoryFlagsOffset = 0x42E0;

    /// <summary>Map-modification log: 512 × 6-byte entries and a u16 count at 0x4EE4. [Static]</summary>
    public const int MapChangeLogOffset = 0x42E4;

    /// <summary>Entries in the map-modification log, u16. [Static]</summary>
    public const int MapChangeCountOffset = 0x4EE4;

    // --- helpers ---------------------------------------------------------------------------

    /// <summary>Block offset of a character record.</summary>
    public static int RecordOffset(int slot) => slot * RecordSize;

    /// <summary>Block offset of an inventory entry.</summary>
    public static int InventoryEntryOffset(int slot, int entry) => RecordOffset(slot) + InventoryOffset + entry * InventoryEntrySize;

    /// <summary>Block offset of an inventory entry's "in use" flag.</summary>
    public static int InUseFlagOffset(int slot, int entry) => InUseFlagsOffset + slot * InventorySlots + entry;

    /// <summary>
    /// The "HEALTH: a/b" threshold: max − ⌈(STR+END)/2⌉. The character is out of action once current health
    /// falls to it. [Confirmed: a poke to max 12 changed the display to /7 for STR 5 END 4]
    /// </summary>
    public static int UnconsciousThreshold(int maxHealth, int strength, int endurance)
    {
        int sum = strength + endurance;
        return maxHealth - (sum / 2 + (sum & 1));
    }

    // --- structural check -------------------------------------------------------------------

    /// <summary>
    /// Does this buffer look like a live or saved game-state block? Checks only what never changes in play
    /// and what no trainer edit can break: the bank account's name, that every adventurer slot is either
    /// empty or carries a terminated printable name with an <c>m</c>/<c>f</c> sex byte, that at least one is
    /// occupied, and that the planet byte is in range. Attribute and skill values are deliberately not
    /// range-checked, so a maxed-out party still validates.
    /// </summary>
    public static bool LooksLikeState(byte[] block)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (block.Length != BlockLength) return false;

        for (int i = 0; i < PartyAccountName.Length; i++)
            if (block[PartyAccountNameOffset + i] != PartyAccountName[i]) return false;
        if (block[PartyAccountNameOffset + PartyAccountName.Length] != 0) return false;

        // M.EXE uses 10 transiently while it reloads a planet; 9 is the shared pub/inn pseudo-planet.
        sbyte planet = (sbyte)block[PlanetOffset];
        if (planet < 0 || planet > 10) return false;

        int occupied = 0;
        for (int slot = 0; slot < CharacterSlots; slot++)
        {
            int rec = RecordOffset(slot);
            if (IsEmptySlot(block, rec)) continue;
            if (!HasValidName(block, rec)) return false;
            byte sex = block[rec + SexOffset];
            if (sex != (byte)'m' && sex != (byte)'f') return false;
            occupied++;
        }
        return occupied > 0;
    }

    /// <summary>An unused (or dead — death zeroes the record) slot has no name.</summary>
    public static bool IsEmptySlot(byte[] block, int recordOffset) => block[recordOffset + NameOffset] == 0;

    /// <summary>1..11 printable ASCII characters, NUL-terminated inside the 12-byte field.</summary>
    public static bool HasValidName(byte[] block, int recordOffset)
    {
        int start = recordOffset + NameOffset;
        for (int i = 0; i < NameLength; i++)
        {
            byte b = block[start + i];
            if (b == 0) return i > 0;
            if (b < 0x20 || b > 0x7E) return false;
        }
        return false;
    }
}
