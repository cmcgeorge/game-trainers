namespace Civilization3ConquestsTrainer.Game;

/// <summary>
/// The recovered memory layout of <c>Civ3Conquests.exe</c> (Steam / Civ III Complete, Conquests
/// v1.22), plus the pure predicates that decide whether a candidate address really is the thing we
/// think it is. No process access lives here, so <c>FormatCheck</c> can exercise all of it headlessly.
///
/// <para><b>Provenance.</b> The static-object addresses come from the <b>C3X</b> community patch
/// (github.com/maxpetul/C3X, by Flintlock/maxpetul), whose <c>civ_prog_objects.csv</c> carries a
/// separate address column per shipped build, and the struct field order comes from its
/// <c>Civ3Conquests.h</c>, which originates with <b>Antal1987</b>'s C3CPatchFramework. Every constant
/// below was then re-derived or re-checked against a running game — see
/// <c>docs/ReverseEngineering.md</c> for the confirmation log and the <c>[Confirmed]</c>/
/// <c>[Inferred]</c> split.</para>
///
/// <para><b>Addresses are stored as RVAs</b> and added to the module base discovered at run time.
/// The exe has no ASLR so the base is always 0x400000 in practice, but nothing here assumes it.</para>
/// </summary>
public static class Civ3Layout
{
    // --- static objects, as RVAs (C3X "define" rows, Steam column, minus the 0x400000 image base) ---

    /// <summary>[Confirmed] <c>leaders</c> — the inline <c>Leader[32]</c> array (VA 0xA75698).</summary>
    public const uint RvaLeaders = 0x675698;

    /// <summary>[Confirmed] <c>p_cities</c> — the city container (VA 0xA75668).</summary>
    public const uint RvaCities = 0x675668;

    /// <summary>[Confirmed] <c>p_units</c> — the unit container (VA 0xA75680).</summary>
    public const uint RvaUnits = 0x675680;

    /// <summary>[Confirmed] <c>p_bic_data</c> — the loaded rules/scenario database (VA 0x9E5D08).</summary>
    public const uint RvaBicData = 0x5E5D08;

    /// <summary>[Confirmed] <c>p_main_screen_form</c> (VA 0xA1AF00); holds the human player's civ id.</summary>
    public const uint RvaMainScreenForm = 0x61AF00;

    /// <summary>[Confirmed] <c>p_current_turn_no</c> (VA 0xA74EA4).</summary>
    public const uint RvaCurrentTurn = 0x674EA4;

    /// <summary>[Confirmed] <c>p_human_player_bits</c> — bit N set means civ N is human (VA 0xA74EB4).</summary>
    public const uint RvaHumanPlayerBits = 0x674EB4;

    /// <summary>[Confirmed] <c>p_player_bits</c> — bit N set means civ N is in the game (VA 0xA74EB8).</summary>
    public const uint RvaPlayerBits = 0x674EB8;

    /// <summary>[Confirmed] <c>p_debug_mode_bits</c> (VA 0xA74E78). Bits 2 and 3 are the two debug modes.</summary>
    public const uint RvaDebugModeBits = 0x674E78;

    /// <summary>[Confirmed] <c>p_game_difficulty</c> (VA 0xA74E7C); matches <c>Difficulty=</c> in conquests.ini.</summary>
    public const uint RvaGameDifficulty = 0x674E7C;

    /// <summary>[Confirmed] <c>p_preferences</c> (VA 0xA74E70); the in-memory twin of the ini bit string.</summary>
    public const uint RvaPreferences = 0x674E70;

    /// <summary>[Confirmed] <c>p_toggleable_rules</c> (VA 0xA74E74).</summary>
    public const uint RvaToggleableRules = 0x674E74;

    /// <summary>[Confirmed] <c>p_is_pbem_game</c> (VA 0xA74FAC). Writes are suppressed when set.</summary>
    public const uint RvaIsPbemGame = 0x674FAC;

    /// <summary>[Confirmed] <c>p_is_offline_mp_game</c> (VA 0xA75189). Writes are suppressed when set.</summary>
    public const uint RvaIsOfflineMpGame = 0x675189;

    // --- Base (the serialisation header every game object starts with) ---------------------------

    /// <summary>Four-character class tag at <c>Base + 0x08</c> — 'LEAD', 'CITY', 'UNIT', 'TILE', 'CULT'.</summary>
    public const int BaseClassNameOffset = 0x08;

    /// <summary>Tag dwords, little-endian, as they appear in memory.</summary>
    public const uint TagLead = 0x4441454C;   // "LEAD"
    public const uint TagCity = 0x59544943;   // "CITY"
    public const uint TagUnit = 0x54494E55;   // "UNIT"
    public const uint TagTile = 0x454C4954;   // "TILE"
    public const uint TagCult = 0x544C5543;   // "CULT"
    public const uint TagBic  = 0x20434942;   // "BIC "

    // --- Leader (stride confirmed live and by the game's own array-walk loop) ---------------------

    /// <summary>[Confirmed] <c>sizeof(Leader)</c>. The game's own loop does <c>add ebp,0x20E4</c>.</summary>
    public const int LeaderStride = 0x20E4;

    /// <summary>
    /// Smallest record size <see cref="ValidateLeader"/> can work with — it reads as far as the
    /// embedded culture object. A candidate stride below this cannot be a real <c>Leader</c>.
    /// </summary>
    public const int LeaderMinValidatableSize = LeaderCulture + CultureCivId + 4;

    public const int LeaderId = 0x1C;                 // [Confirmed] always equals the slot index
    public const int LeaderRaceId = 0x20;             // [Confirmed] indexes BIC.Races
    public const int LeaderCapitalId = 0x2C;          // [Inferred]
    public const int LeaderGoldenAgeEnd = 0x3C;       // [Inferred]
    public const int LeaderGoldDecrement = 0x44;      // [Confirmed] see DecodeGold
    public const int LeaderGoldEncoded = 0x48;        // [Confirmed]
    public const int LeaderAnarchyTurns = 0x9C;       // [Inferred]
    public const int LeaderGovernment = 0xA0;         // [Inferred]
    public const int LeaderTilesDiscovered = 0xA8;    // [Inferred]
    public const int LeaderEra = 0xF4;                // [Confirmed]
    public const int LeaderResearchBulbs = 0xF8;      // [Confirmed]
    public const int LeaderResearchId = 0xFC;         // [Inferred]
    public const int LeaderResearchTurns = 0x100;     // [Inferred]
    public const int LeaderFutureTechs = 0x104;       // [Inferred]
    public const int LeaderUnitCount = 0x18C;         // [Confirmed] cross-checked against the unit container
    public const int LeaderCitiesCount = 0x194;       // [Confirmed]
    public const int LeaderLuxurySlider = 0x1A4;      // [Confirmed]
    public const int LeaderScienceSlider = 0x1A8;     // [Confirmed]
    public const int LeaderGoldSlider = 0x1AC;        // [Confirmed]

    /// <summary>[Confirmed] Embedded <c>Culture</c> object; its own 'CULT' tag sits at +0x08 of it.</summary>
    public const int LeaderCulture = 0x181C;
    public const int CultureLevel = 0x1C;             // [Confirmed] relative to LeaderCulture
    public const int CultureTotalAccumulated = 0x20;  // [Confirmed]
    public const int CultureIncome = 0x24;            // [Confirmed]
    public const int CultureCivId = 0x28;             // [Confirmed] equals the owning leader's index

    // --- container (Cities / Units share this shape) ---------------------------------------------

    public const int ContainerItems = 0x04;           // [Confirmed] T_Item*
    public const int ContainerLastIndex = 0x10;       // [Confirmed] highest used slot, -1 when empty
    public const int ContainerCapacity = 0x14;        // [Confirmed]

    /// <summary>
    /// Ceiling on a container's slot count. Civ3's own limits are in the low thousands; this exists so
    /// a garbage header read out of the target cannot turn into a huge or overflowing allocation.
    /// </summary>
    public const int MaxContainerSlots = 100_000;

    /// <summary>Each item is <c>{ int, T_Body* }</c>, so the body pointer sits 4 bytes in.</summary>
    public const int ItemStride = 0x08;
    public const int ItemBodyPointer = 0x04;

    /// <summary>A body pointer points past the object's <c>Base</c>, so the tag is 0x14 behind it.</summary>
    public const int BodyToTag = 0x14;

    // --- Unit_Body --------------------------------------------------------------------------------

    public const int UnitId = 0x04;                   // [Confirmed] equals the slot index
    public const int UnitX = 0x08;                    // [Confirmed]
    public const int UnitY = 0x0C;                    // [Confirmed]
    public const int UnitCivId = 0x18;                // [Confirmed]
    public const int UnitRaceId = 0x1C;               // [Confirmed] agrees with the owner's RaceID
    public const int UnitTypeId = 0x24;               // [Confirmed] indexes BIC.UnitTypes
    public const int UnitExperience = 0x28;           // [Confirmed] 0 conscript … 3 elite
    public const int UnitStatus = 0x2C;               // [Inferred]

    /// <summary>[Confirmed] Hit points <i>lost</i>, not remaining — "full heal" writes 0 here.</summary>
    public const int UnitDamage = 0x30;

    /// <summary>[Confirmed] Movement <i>used</i> this turn — "refresh moves" writes 0 here.</summary>
    public const int UnitMoves = 0x34;

    /// <summary>How many bytes of a unit record <see cref="ValidateUnit"/> needs.</summary>
    public const int UnitRecordProbeBytes = 0x40;

    // --- City_Body --------------------------------------------------------------------------------
    // Only the anchor-bracketed prefix is exposed. Past +0x54 the C3X header's own field_XX anchors
    // drift by 0x18, so population, corruption, the incomes, the build queue and the city name are at
    // offsets nobody has pinned and are not surfaced at all. See docs/ReverseEngineering.md §4.4.
    //
    // The prefix itself is now Confirmed against a live game with 32 cities across 13 civs: every
    // record validated, and the per-civ tally taken from CityCivId matched each leader's own
    // Cities_Count exactly — two independent structures agreeing. The food and shield stores were
    // additionally round-tripped (the trainer wrote them and the game held the values).

    public const int CityId = 0x04;                   // [Confirmed] equals the slot index, 32/32
    public const int CityX = 0x08;                    // [Confirmed] int16, all within the map bounds
    public const int CityY = 0x0A;                    // [Confirmed] int16
    public const int CityCivId = 0x0C;                // [Confirmed] int8, tallies to Leader.Cities_Count
    public const int CityImprovementsMaintenance = 0x10;   // [Inferred] not surfaced
    public const int CityStatus = 0x14;               // [Inferred] not surfaced
    public const int CityStoredFood = 0x24;           // [Confirmed] write round-trip
    public const int CityStoredProduction = 0x28;     // [Confirmed] write round-trip
    public const int CityOrderId = 0x30;              // [Inferred] not surfaced
    public const int CityOrderType = 0x34;            // [Inferred] not surfaced
    public const int CityFlipImmunityTurns = 0x3C;    // [Inferred] not surfaced
    public const int CityCulturalLevel = 0x40;        // [Inferred] plausible, not cross-checked
    public const int CityDraftCount = 0x50;           // [Inferred] not surfaced

    /// <summary>Last City_Body offset the header's anchors still agree on. Nothing past this is exposed.</summary>
    public const int CityTrustedPrefixEnd = 0x54;

    // --- Map (embedded at the tail of BIC) ---------------------------------------------------------

    public const int BicMap = 0x3E64;                 // [Confirmed] Map lives inside BIC
    public const int MapTileCount = 0x40;             // [Confirmed] == Width * Height / 2
    public const int MapTiles = 0x148;                // [Confirmed] Tile**
    public const int MapHeight = 0x154;               // [Confirmed]
    public const int MapWidth = 0x168;                // [Confirmed]
    public const int MapSeed = 0x1EC;                 // [Confirmed] matches WorldSeed in conquests.ini
    public const int MapFlags = 0x1F0;                // [Inferred] bit 0 = wraps east-west

    // --- Tile ---------------------------------------------------------------------------------------

    public const int TileTagOffset = 0x44;            // [Confirmed] 'TILE'

    /// <summary>
    /// [Inferred] Fog-of-war state. Deliberately <b>not</b> written by "reveal map": the community
    /// patch's own visibility test ORs together <c>FOWStatus</c>, <c>V3</c> and <c>Visibility</c> and
    /// leaves this one alone, so it appears to track something other than "this civ has seen it".
    /// </summary>
    public const int TileFogOfWar = 0x58;

    public const int TileFowStatus = 0x5C;            // [Inferred] per-civ bitmask
    public const int TileVisibility3 = 0x60;          // [Inferred] per-civ bitmask
    public const int TileVisibility = 0x64;           // [Inferred] per-civ bitmask

    /// <summary>The three per-civ visibility bitmasks "reveal map" sets together.</summary>
    public static readonly int[] TileVisibilityMasks = { TileFowStatus, TileVisibility3, TileVisibility };

    // --- BIC tables ----------------------------------------------------------------------------------

    public const int BicUnitTypeCount = 0x8A8;        // [Confirmed]
    public const int BicRacesCount = 0x8AC;           // [Confirmed]
    public const int BicRaces = 0x3CC8;               // [Confirmed] Race*
    public const int BicUnitTypes = 0x3CD8;           // [Confirmed] UnitType*

    public const int RaceStride = 0x974;              // [Confirmed] brute-forced against Race[i].ID == i
    public const int RaceLeaderName = 0x1C;           // [Confirmed] char[32]
    public const int RaceAdjective = 0x74;            // [Confirmed] char[40]
    public const int RaceCountryName = 0x9C;          // [Confirmed] char[40]
    public const int RaceAggression = 0x918;          // [Inferred]
    public const int RaceId = 0x91C;                  // [Confirmed]

    public const int UnitTypeStride = 0x138;          // [Confirmed] brute-forced against UnitType[i].ID == i
    public const int UnitTypeName = 0x08;             // [Confirmed] char[32]
    public const int UnitTypeCost = 0x54;             // [Confirmed]
    public const int UnitTypeDefence = 0x58;          // [Confirmed]

    /// <summary>[Confirmed] The <c>ID</c> field inside a <c>UnitType</c> record — not to be confused
    /// with <see cref="UnitTypeId"/>, which is where a <i>unit</i> stores its type.</summary>
    public const int UnitTypeRecordId = 0x5C;
    public const int UnitTypeAttack = 0x60;           // [Confirmed]
    public const int UnitTypeMovement = 0x70;         // [Confirmed]

    public const int MainScreenPlayerCivId = 0x4DBC;  // [Confirmed]

    // === gold codec ==================================================================================

    /// <summary>
    /// Civ3 stores a player's treasury split across two fields that sum to it, seeded differently per
    /// civ each game, so the number on screen never appears in RAM. Any value scan for the displayed
    /// treasury therefore finds nothing — which is precisely why this trainer leads with the
    /// structural locator instead of a scanner.
    /// </summary>
    public static long DecodeGold(int decrement, int encoded) => (long)decrement + encoded;

    /// <summary>
    /// Produces the value to write into <c>Gold_Encoded</c> so the treasury reads
    /// <paramref name="desired"/>. <c>Gold_Decrement</c> is never written: it is the game's key, and
    /// rewriting it would desynchronise every other read of the same treasury.
    /// </summary>
    public static bool TryEncodeGold(long desired, int decrement, out int encoded)
    {
        encoded = 0;
        // Range-check the input before subtracting rather than relying on the difference landing
        // outside int32: `desired - decrement` can itself overflow int64 and wrap back into range.
        if (!IsPlausibleTreasury(desired)) return false;
        long value = desired - decrement;
        if (value < int.MinValue || value > int.MaxValue) return false;
        encoded = (int)value;
        return true;
    }

    // === pure validation predicates ===================================================================

    /// <summary>A treasury within the range Civ3 can actually reach without having been tampered with.</summary>
    public static bool IsPlausibleTreasury(long gold) => gold is >= -100_000_000 and <= 2_000_000_000;

    /// <summary>Slider values are tens of percent and the three always total <see cref="GameFacts.SliderTotal"/>.</summary>
    public static bool IsPlausibleSliderSet(int luxury, int science, int gold)
        => luxury >= 0 && science >= 0 && gold >= 0
           && luxury <= GameFacts.SliderTotal && science <= GameFacts.SliderTotal && gold <= GameFacts.SliderTotal
           && luxury + science + gold == GameFacts.SliderTotal;

    /// <summary>A civ slot index, including the barbarians at 0.</summary>
    public static bool IsValidCivId(int civId) => civId is >= 0 and < GameFacts.MaxPlayers;

    /// <summary>Whether bit <paramref name="civId"/> is set in one of the player bitmasks.</summary>
    public static bool IsBitSet(uint mask, int civId)
        => IsValidCivId(civId) && (mask & (1u << civId)) != 0;

    /// <summary>A heap pointer in a 32-bit user-mode process: aligned, not null, below the 2 GB line.</summary>
    public static bool LooksLikeHeapPointer(uint p) => p is >= 0x00010000 and < 0x7F000000 && (p & 3) == 0;

    /// <summary>
    /// Whether a <c>Leader</c> record looks real. <paramref name="record"/> must start at the leader's
    /// base and be at least <see cref="LeaderStride"/> bytes. <paramref name="slot"/> is the index the
    /// record is expected to occupy — <c>ID == index</c> is the load-bearing check, because the only
    /// stride that satisfies it for all 32 slots is the true one.
    /// </summary>
    public static bool ValidateLeader(ReadOnlySpan<byte> record, int slot, uint rdataStart, uint rdataEnd)
    {
        if (record.Length < LeaderMinValidatableSize) return false;
        if (BitConverter.ToUInt32(record.Slice(BaseClassNameOffset, 4)) != TagLead) return false;
        if (BitConverter.ToInt32(record.Slice(LeaderId, 4)) != slot) return false;

        uint vtable = BitConverter.ToUInt32(record[..4]);
        if (vtable < rdataStart || vtable >= rdataEnd) return false;

        int race = BitConverter.ToInt32(record.Slice(LeaderRaceId, 4));
        if (race < -1 || race >= GameFacts.MaxPlayers) return false;

        if (!IsPlausibleSliderSet(
                BitConverter.ToInt32(record.Slice(LeaderLuxurySlider, 4)),
                BitConverter.ToInt32(record.Slice(LeaderScienceSlider, 4)),
                BitConverter.ToInt32(record.Slice(LeaderGoldSlider, 4)))) return false;

        int era = BitConverter.ToInt32(record.Slice(LeaderEra, 4));
        if (era is < 0 or > 15) return false;

        int cities = BitConverter.ToInt32(record.Slice(LeaderCitiesCount, 4));
        int units = BitConverter.ToInt32(record.Slice(LeaderUnitCount, 4));
        if (cities is < 0 or > 10_000 || units is < 0 or > 100_000) return false;

        if (BitConverter.ToUInt32(record.Slice(LeaderCulture + BaseClassNameOffset, 4)) != TagCult) return false;
        if (BitConverter.ToInt32(record.Slice(LeaderCulture + CultureCivId, 4)) != slot) return false;

        long gold = DecodeGold(
            BitConverter.ToInt32(record.Slice(LeaderGoldDecrement, 4)),
            BitConverter.ToInt32(record.Slice(LeaderGoldEncoded, 4)));
        return IsPlausibleTreasury(gold);
    }

    /// <summary>Whether a <c>Unit_Body</c> looks real, given the map bounds it must sit inside.</summary>
    public static bool ValidateUnit(ReadOnlySpan<byte> body, int slot, int mapWidth, int mapHeight)
    {
        if (body.Length < 0x40) return false;
        if (BitConverter.ToInt32(body.Slice(UnitId, 4)) != slot) return false;
        if (!IsValidCivId(BitConverter.ToInt32(body.Slice(UnitCivId, 4)))) return false;

        int x = BitConverter.ToInt32(body.Slice(UnitX, 4));
        int y = BitConverter.ToInt32(body.Slice(UnitY, 4));
        if (x < 0 || y < 0 || (mapWidth > 0 && x >= mapWidth) || (mapHeight > 0 && y >= mapHeight)) return false;

        if (BitConverter.ToInt32(body.Slice(UnitDamage, 4)) < 0) return false;
        int exp = BitConverter.ToInt32(body.Slice(UnitExperience, 4));
        return exp is >= 0 and <= 15;
    }

    /// <summary>Whether a <c>City_Body</c> prefix looks real, given the map bounds.</summary>
    public static bool ValidateCity(ReadOnlySpan<byte> body, int slot, int mapWidth, int mapHeight)
    {
        if (body.Length < CityTrustedPrefixEnd) return false;
        if (BitConverter.ToInt32(body.Slice(CityId, 4)) != slot) return false;
        if (!IsValidCivId(body[CityCivId])) return false;

        short x = BitConverter.ToInt16(body.Slice(CityX, 2));
        short y = BitConverter.ToInt16(body.Slice(CityY, 2));
        if (x < 0 || y < 0 || (mapWidth > 0 && x >= mapWidth) || (mapHeight > 0 && y >= mapHeight)) return false;

        if (BitConverter.ToInt32(body.Slice(CityStoredFood, 4)) < 0) return false;
        if (BitConverter.ToInt32(body.Slice(CityStoredProduction, 4)) < 0) return false;
        return BitConverter.ToInt32(body.Slice(CityCulturalLevel, 4)) is >= 0 and <= 100;
    }

    /// <summary>Whether a <c>Map</c> header is self-consistent: Civ3's staggered grid holds W*H/2 tiles.</summary>
    public static bool ValidateMap(int width, int height, int tileCount)
        => width is > 0 and <= 1024 && height is > 0 and <= 1024 && tileCount == width * height / 2;
}
