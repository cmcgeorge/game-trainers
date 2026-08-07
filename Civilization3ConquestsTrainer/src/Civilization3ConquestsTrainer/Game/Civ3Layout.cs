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

    /// <summary>
    /// [Confirmed] Government type. Promoted from Inferred by the game's own worker-rate routine at
    /// <c>0x5C1D10</c>, which does <c>mov ecx,[eax + 0xA75738]</c> with <c>eax = CivID × 0x20E4</c> —
    /// and <c>0xA75738 − 0xA75698 = 0xA0</c> exactly. It reads this to apply the despotism work penalty.
    /// </summary>
    public const int LeaderGovernment = 0xA0;
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

    /// <summary>
    /// [Confirmed] How far a <c>Unit_Body</c> sits into its <c>Unit</c>. The game's own routines take a
    /// <c>Unit*</c> and address body fields through it, so every offset transcribed from the
    /// instruction stream arrives 0x1C larger than the one this file records —
    /// <c>get_worker_remaining_turns_to_complete</c> spells the conversion out with
    /// <c>lea esi,[eax-0x1C]</c>.
    /// </summary>
    public const int BodyOffsetInUnit = 0x1C;

    // --- Unit_Body --------------------------------------------------------------------------------

    public const int UnitId = 0x04;                   // [Confirmed] equals the slot index
    public const int UnitX = 0x08;                    // [Confirmed]
    public const int UnitY = 0x0C;                    // [Confirmed]
    public const int UnitCivId = 0x18;                // [Confirmed]
    public const int UnitRaceId = 0x1C;               // [Confirmed] agrees with the owner's RaceID

    /// <summary>
    /// [Confirmed] Which unit type this unit is — an index into <c>BIC.UnitTypes</c>, and the field the
    /// Units tab's <i>Type</i> column writes.
    ///
    /// <para>Read out of the game's own code rather than inferred: <c>Unit_has_ability</c>
    /// (<c>0x5CB430</c>) does <c>mov eax,[esi+0x40]</c> — <c>Unit+0x40</c> is <c>Unit_Body+0x24</c> —
    /// then indexes <c>BIC.UnitTypes</c> at stride <c>0x138</c> and tests the ability bitfield. So a
    /// unit's abilities are resolved from <b>whatever this field says right now</b>, which is what makes
    /// retyping a unit change what it can do rather than only what it is called. The same is true of its
    /// actions (<c>Unit_can_perform_action</c> @ <c>0x5D0670</c>) and of its maximum hit points
    /// (<c>Unit_get_max_hp</c> @ <c>0x5CD180</c>).</para>
    ///
    /// <para><b>What a write here does not reach.</b> The unit's on-map artwork is chosen when the unit
    /// is <i>spawned</i> — <c>Leader_spawn_unit</c> (<c>0x575900</c>) builds an animation name from the
    /// type, the owner's era and its race and loads it into the unit at <c>Unit_Body+0x260</c> — so a
    /// retyped unit is expected to keep the sprite it was born with until the game rebuilds the object.
    /// Nor are the owner's incremental tallies (per-type counts at <c>Leader+0x15F0</c>, armies at
    /// <c>Leader+0x188</c>) corrected, because the game maintains those at spawn and despawn.
    /// See <c>docs/ReverseEngineering.md</c> §4.8.</para>
    /// </summary>
    public const int UnitTypeId = 0x24;
    public const int UnitExperience = 0x28;           // [Confirmed] 0 conscript … 3 elite

    /// <summary>
    /// [Confirmed] Per-turn status bits. The one that matters here is
    /// <see cref="UnitStatusUsedAttack"/>: it is what stops a unit attacking twice in one turn.
    ///
    /// <para>Read out of the game's own code at three sites, which between them set the bit, test it and
    /// clear it. <c>Fighter_fight</c> (<c>0x4AC060</c>) does <c>or dword [eax+0x48],4</c> at
    /// <c>0x4AC355</c> on the attacker as a battle begins. <c>Unit_can_move_to_adjacent_tile</c>
    /// (<c>0x5C4620</c>) does <c>test byte [esi+0x48],4</c> at <c>0x5C4748</c> and, when the bit is set
    /// and <c>Unit_has_ability(UTA_Blitz)</c> comes back false, returns <c>AMV_REQUIRES_BLITZ</c> — the
    /// refusal the player sees as a unit that will not attack again. (<c>Unit+0x48</c> is
    /// <c>Unit_Body+0x2C</c>: the body sits <c>0x1C</c> past the object.)</para>
    ///
    /// <para><b>The game clears this bit itself, every turn.</b> <c>Unit_begin_turn</c>
    /// (<c>0x5D65B0</c>) ends with one four-instruction sequence at <c>0x5D6D39</c> —
    /// <c>mov eax,[esi+0x48]</c> / <c>mov dword [esi+0x50],0</c> / <c>and al,0xB8</c> /
    /// <c>mov [esi+0x48],eax</c> — which zeroes <see cref="UnitMoves"/> and knocks out bits
    /// <c>0x01|0x02|0x04|0x40</c> together. So "attack again" writes nothing the game does not write for
    /// itself at every turn boundary; it only writes it sooner. That same sequence also re-confirms
    /// <see cref="UnitMoves"/> at <c>+0x34</c> from a second, unrelated routine.</para>
    /// </summary>
    public const int UnitStatus = 0x2C;

    /// <summary>
    /// <c>USF_USED_ATTACK</c> — set on a unit that has already attacked this turn, and the only bit of
    /// <see cref="UnitStatus"/> this trainer writes.
    ///
    /// <para>The game's own new-turn clear is wider (<c>0x47</c>: this bit plus <c>0x01</c>
    /// <c>SKIPPED_FULL_TURN_WITH_DAMAGE</c>, <c>0x02</c>, and <c>0x40</c>
    /// <c>USED_DEFENSIVE_BOMBARD</c>), and that width is deliberately <b>not</b> copied. Bit
    /// <c>0x01</c> feeds the healing test at the top of <c>Unit_begin_turn</c>, so clearing it in the
    /// middle of a turn would quietly change how much a damaged unit recovers — a side effect nobody
    /// asked for. Clearing one bit is the whole feature.</para>
    /// </summary>
    public const int UnitStatusUsedAttack = 0x04;

    /// <summary>Whether a unit's status word says it has already attacked this turn.</summary>
    public static bool HasUsedAttack(int status) => (status & UnitStatusUsedAttack) != 0;

    /// <summary>The same status word with the "already attacked" bit knocked out, and nothing else changed.</summary>
    public static int ClearUsedAttack(int status) => status & ~UnitStatusUsedAttack;

    /// <summary>[Confirmed] Hit points <i>lost</i>, not remaining — "full heal" writes 0 here.</summary>
    public const int UnitDamage = 0x30;

    /// <summary>
    /// [Confirmed] Movement <i>used</i> this turn — "refresh moves" writes 0 here.
    ///
    /// <para>Confirmed twice over: alongside the other unit fields in §4.3, and again by
    /// <c>Unit_begin_turn</c>'s <c>mov dword [esi+0x50],0</c> at <c>0x5D6D3C</c> (<c>Unit+0x50</c> is
    /// <c>Unit_Body+0x34</c>), which is the game zeroing spent movement at the start of a unit's turn.</para>
    /// </summary>
    public const int UnitMoves = 0x34;

    /// <summary>
    /// [Confirmed] Worker-turns <i>already put into</i> the current job — it counts <b>up</b> toward the
    /// job's cost, so raising it finishes the job sooner and zeroing it starts the work over.
    ///
    /// <para>Confirmed out of the game's own code rather than inferred. <c>get_worker_remaining_turns_to_complete</c>
    /// (<c>0x5D5520</c>) computes the total cost as <c>Worker_Job.TurnToComplete × a terrain factor</c>
    /// and then, at <c>0x5D563D</c>, does <c>mov ebp,[esi+0x54]</c> / <c>sub ebx,ebp</c> — subtracting
    /// this field from that total. (<c>Unit+0x54</c> is <c>Unit_Body+0x38</c>: the body pointer sits
    /// <c>0x1C</c> past the object, which the same routine confirms with <c>lea esi,[eax-0x1C]</c>.)</para>
    ///
    /// <para><b>Progress pools across a stack.</b> That loop walks every unit standing on the tile and
    /// subtracts the <c>Job_Value</c> of each one whose <see cref="UnitJobId"/> matches, which is why
    /// several workers on one tile finish a job together — and why writing this on any one of them is
    /// enough to finish it for the whole stack.</para>
    /// </summary>
    public const int UnitJobValue = 0x38;

    /// <summary>
    /// [Confirmed] Which job the unit is performing, or <c>-1</c> when it is idle — the
    /// <c>enum Worker_Jobs</c> ordinal (0 mine, 1 irrigate, 2 fortress, 3 road, 4 railroad …), which
    /// indexes the loaded ruleset's own job table. The routine above compares it against the job it was
    /// asked about before pooling a unit's progress. Idle units read <c>-1</c> here and <c>0</c> in
    /// <see cref="UnitJobValue"/>, confirmed live across 28 units of 11 civs.
    ///
    /// <para>The trainer reads this and never writes it: starting a job is more than setting a number
    /// (the game also sets unit state, the tile's overlays and the animation), so a poked job id would
    /// describe work the game never actually began.</para>
    /// </summary>
    public const int UnitJobId = 0x3C;

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

    /// <summary>[Confirmed] <c>WorkerJobCount</c> — 13 in the epic game, but a mod may ship its own.</summary>
    public const int BicWorkerJobCount = 0x8B8;

    /// <summary>
    /// [Confirmed] <c>Worker_Job*</c> — the per-job cost table for whichever ruleset is loaded.
    ///
    /// <para>Read straight out of the game's code, not derived: <c>get_worker_remaining_turns_to_complete</c>
    /// loads it with <c>mov esi,[0x9E9B24]</c>, and <c>0x9E9B24 − 0x9E5D08</c> (<c>p_bic_data</c>) is
    /// <c>0x3E1C</c>. The arithmetic over the community header agrees, and so does the
    /// <see cref="BicMap"/> anchor <c>0x48</c> further on.</para>
    /// </summary>
    public const int BicWorkerJobs = 0x3E1C;

    /// <summary>[Confirmed] <c>sizeof(Worker_Job)</c>. The game indexes the table by <c>29 × id</c>
    /// scaled by 4 (<c>lea</c> chain at <c>0x5D5565</c>), which is <c>0x74</c>.</summary>
    public const int WorkerJobStride = 0x74;

    public const int WorkerJobName = 0x04;            // [Confirmed] char[32] — "Mine", "Irrigation", "Road" …

    /// <summary>
    /// [Confirmed] Base cost of the job in worker-turns, read by the game as
    /// <c>mov ebx,[esi + id×0x74 + 0x44]</c> and then multiplied by a terrain factor to give the real
    /// cost. Epic-game values: Road 6, Irrigation 8, Mine 12, Railroad 12, Fortress 16, Clear Damage 24.
    /// A worker contributes roughly two of these per turn, which is what makes a road on open ground the
    /// familiar three turns.
    /// </summary>
    public const int WorkerJobTurnToComplete = 0x44;

    /// <summary>How many bytes of a <c>Worker_Job</c> record <see cref="ValidateWorkerJob"/> needs.</summary>
    public const int WorkerJobRecordProbeBytes = WorkerJobTurnToComplete + 4;

    /// <summary>
    /// [Confirmed] <c>sizeof(UnitType)</c>. Read straight out of the game's code: both
    /// <c>Unit_has_ability</c> (<c>0x5CB430</c>) and <c>Unit_upgrade</c> (<c>0x5CF2E0</c>) index the
    /// table with <c>imul ecx,ecx,0x138</c> or the equivalent <c>lea</c> chain.
    /// </summary>
    public const int UnitTypeStride = 0x138;
    public const int UnitTypeName = 0x08;             // [Confirmed] char[32]
    public const int UnitTypeCost = 0x54;             // [Confirmed]
    public const int UnitTypeDefence = 0x58;          // [Confirmed]

    /// <summary>
    /// [Confirmed] The <c>ID</c> field inside a <c>UnitType</c> record — not to be confused with
    /// <see cref="UnitTypeId"/>, which is where a <i>unit</i> stores its type.
    ///
    /// <para><b>It is not the row index, and it is not unique.</b> In the epic ruleset the two coincide,
    /// which is what once made <c>Table[i].ID == i</c> look like a stride proof for this table. A
    /// conquest disproves it: Mesopotamia's 31 types read 0, 1, 2, 6, 7, 8 … 195 here — the ids the epic
    /// unit list gives those units — and repeat several of them, its two <i>Galley</i> rows both reading
    /// 29. What the game indexes by is the row position instead: <c>General.BuildArmyUnitID</c> holds 12
    /// there, which is row 12 <i>Army</i> and not the row whose <c>ID</c> is 12, and a barbarian on the
    /// map carries type 23, which is row 23 <i>Fighter</i>. So the field is documented here but nothing
    /// reads it: the stride is proved by <see cref="ValidateUnitType"/> instead, and the id the trainer
    /// hands out is the row index the game itself means.</para>
    /// </summary>
    public const int UnitTypeRecordId = 0x5C;
    public const int UnitTypeAttack = 0x60;           // [Confirmed]
    public const int UnitTypeMovement = 0x70;         // [Confirmed]

    /// <summary>How many bytes of a <c>UnitType</c> record <see cref="ValidateUnitType"/> needs.</summary>
    public const int UnitTypeRecordProbeBytes = UnitTypeMovement + 4;

    /// <summary>
    /// [Confirmed] The unit type's ability bitfield, indexed by <c>enum UnitTypeAbilities</c> — the
    /// thing that makes a type an army or a great leader rather than an ordinary unit.
    ///
    /// <para>The game's own accessor (<c>UnitType_has_ability</c> @ <c>0x5F4750</c>) is four
    /// instructions: for an ability index below 32 it does <c>test [eax+0x88], 1&lt;&lt;n</c>, and for
    /// 32 and above it subtracts 32 and tests <c>[eax+0x130]</c> instead. Both offsets are therefore
    /// the game's own, and the second doubles as a confirmation of the <c>0x138</c> stride — the
    /// overflow word is the second-to-last field in the record.</para>
    /// </summary>
    public const int UnitTypeAbilities = 0x88;

    /// <summary>
    /// [Inferred] Land (0), sea (1) or air (2) — used to keep the <i>Type</i> column from offering to
    /// turn a Trireme into a Warrior while it is sitting in the ocean.
    ///
    /// <para>This is the one field here that was not read out of the instruction stream. It is
    /// <b>bracketed with no slack</b> between two anchors that were: the community header's own
    /// <c>field_98</c> at <c>+0x98</c>, and <c>Standard_Actions</c> at <c>+0xA8</c>, which
    /// <c>Unit_can_perform_action</c> (<c>0x5D0670</c>) proves by indexing the four action words from
    /// there. Exactly four fields fit that gap and this is the header's first. Even so it is checked at
    /// run time rather than trusted — see <see cref="IsPlausibleUnitClass"/> and
    /// <c>GameTables.UnitClassesUsable</c>, which fall back to offering every type rather than
    /// filtering on a field that might not be this one.</para>
    /// </summary>
    public const int UnitTypeClass = 0x9C;

    /// <summary>Land units. Air and sea are 1 and 2; see <see cref="UnitTypeClass"/>.</summary>
    public const int UnitClassLand = 0;
    public const int UnitClassSea = 1;
    public const int UnitClassAir = 2;

    /// <summary>
    /// [Confirmed] The <c>Army</c> ability's bit index. <c>Unit_upgrade</c> pushes <c>0x12</c> at
    /// <c>0x5CF4B7</c> to decide whether the unit it has just created should have the old unit's
    /// passengers loaded into it as an army.
    /// </summary>
    public const int UnitAbilityArmy = 0x12;

    /// <summary>
    /// [Confirmed] The <c>Leader</c> ability's bit index — the one that puts <i>Build Army</i> on a
    /// unit's action list. The gate inside <c>Unit_can_perform_action</c> (<c>0x5D0956</c>) tests
    /// exactly this, on the unit's <i>current</i> type, which is what makes the great-leader route work
    /// without a code patch.
    /// </summary>
    public const int UnitAbilityLeader = 0x13;

    /// <summary>Whether a unit type carries an ability, given its <see cref="UnitTypeAbilities"/> word.</summary>
    /// <remarks>
    /// Only the first 32 abilities live in that word; the game keeps 32 and above in a second word at
    /// <c>UnitType+0x130</c>. Nothing here needs one, so an out-of-range index answers "no" rather than
    /// silently shifting by 32 and reading a different ability's bit.
    /// </remarks>
    public static bool UnitTypeHasAbility(int abilities, int abilityBit)
        => abilityBit is >= 0 and < 32 && (abilities & (1 << abilityBit)) != 0;

    /// <summary>Whether a unit type's class field holds one of the three domains the game defines.</summary>
    public static bool IsPlausibleUnitClass(int unitClass)
        => unitClass is >= UnitClassLand and <= UnitClassAir;

    // --- BIC.General ---------------------------------------------------------------------------------
    // The embedded rules block. Its position is fixed by the same arithmetic the worker-job table rests
    // on, and confirmed at +0xD0 (FoodPerCitizen, reading 2 in a live epic game) among four others.

    /// <summary>[Confirmed] The embedded <c>General</c> block — the loaded ruleset's global settings.</summary>
    public const int BicGeneral = 0x3CDC;

    /// <summary>
    /// [Inferred] <c>General.BattleCreatedUnitID</c> — which unit type the ruleset uses for a great
    /// leader. Its neighbour <see cref="GeneralBuildArmyUnitId"/> is confirmed from the instruction
    /// stream, which brackets this one immediately below it; and <c>GameTables</c> only believes the id
    /// it reads here if that type actually carries <see cref="UnitAbilityLeader"/>, so the offset and
    /// its meaning have to agree before anything uses them.
    /// </summary>
    public const int GeneralBattleCreatedUnitId = 0xA8;

    /// <summary>
    /// [Confirmed] <c>General.BuildArmyUnitID</c> — which unit type the ruleset uses for an army.
    ///
    /// <para><c>Unit_form_army</c> (<c>0x5CB5B0</c>) reads it as an absolute address:
    /// <c>mov edi,[0x9E9A90]</c>, and <c>0x9E9A90 − 0x9E5D08</c> (<c>p_bic_data</c>) is
    /// <c>0x3D88 = BicGeneral + 0xAC</c>. It then passes that type straight to
    /// <c>Leader_spawn_unit</c>, so this is literally the type of the army the game builds.</para>
    /// </summary>
    public const int GeneralBuildArmyUnitId = 0xAC;

    /// <summary>Where <c>BattleCreatedUnitID</c> sits relative to <c>BIC</c> itself.</summary>
    public const int BicGreatLeaderUnitType = BicGeneral + GeneralBattleCreatedUnitId;

    /// <summary>Where <c>BuildArmyUnitID</c> sits relative to <c>BIC</c> itself — VA <c>0x9E9A90</c>.</summary>
    public const int BicArmyUnitType = BicGeneral + GeneralBuildArmyUnitId;

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

    /// <summary>
    /// Whether a <c>Worker_Job</c> record looks real. Unlike <c>Race</c> this table has <b>no <c>ID</c>
    /// field</b>, so the usual <c>Table[i].ID == i</c> stride proof is unavailable and the record has to
    /// vouch for itself: a printable, non-empty name and a cost inside
    /// <see cref="GameFacts.MaxWorkerJobTurnToComplete"/>. Thirteen consecutive records satisfying that
    /// at a stride of <c>0x74</c> is what stands in for the missing index.
    /// </summary>
    public static bool ValidateWorkerJob(ReadOnlySpan<byte> record)
    {
        if (record.Length < WorkerJobRecordProbeBytes) return false;

        int cost = BitConverter.ToInt32(record.Slice(WorkerJobTurnToComplete, 4));
        if (cost < 0 || cost > GameFacts.MaxWorkerJobTurnToComplete) return false;

        return IsName(record.Slice(WorkerJobName, 32));
    }

    /// <summary>
    /// Whether a <c>UnitType</c> record looks real.
    ///
    /// <para>This table <i>has</i> an <c>ID</c> field, but it is not the row index (see
    /// <see cref="UnitTypeRecordId"/> — a conquest's ids skip and repeat), so it proves nothing about a
    /// stride and the record has to vouch for itself exactly as a <c>Worker_Job</c> does: a printable,
    /// non-empty name and stats inside bounds arbitrary memory does not clear. Thirty-one consecutive
    /// records satisfying that at a spacing of <c>0x138</c> is what stands in for the missing index.</para>
    /// </summary>
    public static bool ValidateUnitType(ReadOnlySpan<byte> record)
    {
        if (record.Length < UnitTypeRecordProbeBytes) return false;

        // Deliberately loose, and for the same reason as MaxWorkerJobTurnToComplete: the ruleset is the
        // modder's to write, so these only have to be tight enough that random memory fails them. Zero
        // is legal throughout — a Settler attacks and defends at 0, and a barbarian costs nothing. The
        // ID field is deliberately left out: it is not the row index, and its range is not ours to assume.
        int cost = BitConverter.ToInt32(record.Slice(UnitTypeCost, 4));
        int attack = BitConverter.ToInt32(record.Slice(UnitTypeAttack, 4));
        int defence = BitConverter.ToInt32(record.Slice(UnitTypeDefence, 4));
        int movement = BitConverter.ToInt32(record.Slice(UnitTypeMovement, 4));
        if (cost is < 0 or > 1_000_000) return false;
        if (attack is < 0 or > 10_000 || defence is < 0 or > 10_000 || movement is < 0 or > 10_000) return false;

        return IsName(record.Slice(UnitTypeName, 32));
    }

    /// <summary>
    /// Whether a fixed-width character field holds a <i>name</i> rather than merely bytes: printable
    /// ASCII up to the terminator, at least one character of it, and a terminator inside the field.
    /// </summary>
    private static bool IsName(ReadOnlySpan<byte> field)
    {
        int length = 0;
        while (length < field.Length && field[length] != 0)
        {
            if (field[length] is < 0x20 or > 0x7E) return false;
            length++;
        }
        return length > 0 && length < field.Length;
    }

    /// <summary>
    /// What to write into <see cref="UnitJobValue"/> so a job of the given base cost completes.
    ///
    /// <para>The game's real threshold is <c>TurnToComplete × a terrain factor</c> it derives from the
    /// tile the worker is standing on, and the trainer does not decode that factor — so this multiplies
    /// by <see cref="GameFacts.WorkerJobTerrainFactorCeiling"/> to clear the worst terrain rather than
    /// writing some huge round number. Enough to finish the job, small enough that it stays a plausible
    /// count of worker-turns instead of a value the game's own arithmetic has to survive.</para>
    /// </summary>
    public static int WorkerJobWorkToFinish(int turnToComplete)
        => Math.Clamp(turnToComplete, 1, GameFacts.MaxWorkerJobTurnToComplete)
           * GameFacts.WorkerJobTerrainFactorCeiling;

    /// <summary>Whether a <c>Map</c> header is self-consistent: Civ3's staggered grid holds W*H/2 tiles.</summary>
    public static bool ValidateMap(int width, int height, int tileCount)
        => width is > 0 and <= 1024 && height is > 0 and <= 1024 && tileCount == width * height / 2;
}
