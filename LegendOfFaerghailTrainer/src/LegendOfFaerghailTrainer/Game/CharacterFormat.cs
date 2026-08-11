namespace LegendOfFaerghailTrainer.Game;

/// <summary>
/// Byte layout of a Legend of Faerghail character record.
///
/// <para>The record is <b>410 bytes</b> (0x19A) — the stride the game itself multiplies a party
/// index by (<c>idx * 0x19a + [DS:0x30]</c> appears 1,337 times in the decompiled image). The same
/// record is used for the six party slots, the thirty-two roster slots, and the six party slots
/// inside a saved game.</para>
///
/// <para>Confidence markers: <b>[Confirmed]</b> means the offset was pinned against the running
/// game — either read out of a live record and matched to the number the character sheet printed,
/// or written with a sentinel and watched change on screen. <b>[Inferred]</b> means it is
/// consistent across every shipped record but was never proved on screen.</para>
/// </summary>
public static class CharacterFormat
{
    /// <summary>Size of one character record.</summary>
    public const int RecordSize = 410;

    /// <summary>Party slots the game keeps in memory.</summary>
    public const int PartySlots = 6;

    /// <summary>Roster ("saved characters") slots. The manual quotes a limit of 32.</summary>
    public const int RosterSlots = 32;

    // --- identity ---------------------------------------------------------------

    /// <summary>[Confirmed] 1 = slot occupied, 0 = empty. The game's own party loops test this byte.</summary>
    public const int OffOccupied = 0x00;

    /// <summary>[Confirmed] NUL-terminated name. The game shows at most 10 characters.</summary>
    public const int OffName = 0x01;
    public const int NameFieldLength = 14;
    public const int MaxNameLength = 10;

    /// <summary>[Confirmed] Experience level — the sheet's "Rnk". Sentinel 8 printed "Rnk 08".</summary>
    public const int OffLevel = 0x17;

    /// <summary>[Confirmed] 0 = female, 1 = male (cross-checked against Healer/Priestess wording).</summary>
    public const int OffSex = 0x18;

    /// <summary>[Confirmed] 0 = lawful, 1 = chaotic.</summary>
    public const int OffAlignment = 0x19;

    /// <summary>[Confirmed] Race index. Sentinel 3 turned a Human into a Halfling on screen.</summary>
    public const int OffRace = 0x1A;

    /// <summary>[Confirmed] Trade index; matches the tavern's Recruit list for all eleven roster entries.</summary>
    public const int OffClass = 0x1B;

    /// <summary>[Confirmed] Armour protection %, the figure printed in the party portrait box.</summary>
    public const int OffArmourPercent = 0x1E;

    /// <summary>[Confirmed] Health state, 0 = Good … 7 = Dead (a 0 HP character reads 7).</summary>
    public const int OffStatus = 0x1F;

    // --- pools ------------------------------------------------------------------

    /// <summary>[Confirmed] Maximum hit points, uint16 LE. Note the record stores max <i>before</i> current.</summary>
    public const int OffMaxHp = 0x20;

    /// <summary>[Confirmed] Current hit points, uint16 LE.</summary>
    public const int OffCurHp = 0x22;

    /// <summary>[Confirmed] Maximum magic points, byte. Sentinel 104/66 printed "Magic 0066 / 0104".</summary>
    public const int OffMaxMagic = 0x68;

    /// <summary>[Confirmed] Current magic points, byte.</summary>
    public const int OffCurMagic = 0x69;

    // --- abilities (percentages printed on sheet page 4) ------------------------
    // [Confirmed] twice: once against the shipped values, once against a written 0x0B..0x1C ramp.
    // The spacing really is irregular (2,1,3,2,3,2,2,2) — the nine displayed skills are single
    // bytes interleaved with fields that do not appear on any sheet page.

    public const int OffNegotiating = 0x25;
    public const int OffAttack = 0x27;
    public const int OffDefence = 0x28;
    public const int OffConcentration = 0x2B;
    public const int OffPickPocketing = 0x2D;
    public const int OffStalking = 0x30;
    public const int OffTrapDetecting = 0x32;
    public const int OffTrapDisarming = 0x34;
    public const int OffLockPicking = 0x36;

    /// <summary>The nine ability byte offsets in the order the game prints them.</summary>
    public static readonly int[] AbilityOffsets =
    {
        OffNegotiating, OffAttack, OffDefence, OffConcentration, OffPickPocketing,
        OffStalking, OffTrapDetecting, OffTrapDisarming, OffLockPicking
    };

    /// <summary>Abilities are printed with "%3d%%"; the game's own training caps them at 100.</summary>
    public const int MaxAbility = 100;

    // --- attributes -------------------------------------------------------------
    // [Confirmed] against three live character sheets. Note the storage order is Con, Str, Dex,
    // Int, Wis while the sheet prints Str, Con, Dex, Int, Wis.

    public const int OffConstitution = 0x44;
    public const int OffStrength = 0x45;
    public const int OffDexterity = 0x46;
    public const int OffIntelligence = 0x47;
    public const int OffWisdom = 0x48;

    /// <summary>The five attribute offsets in record order.</summary>
    public static readonly int[] AttributeOffsets =
    {
        OffConstitution, OffStrength, OffDexterity, OffIntelligence, OffWisdom
    };

    /// <summary>
    /// Rolled attributes run 3..19 in the shipped records; the game's own bonus text
    /// ("Strength improves!") pushes them higher, so 25 is used as the editor ceiling.
    /// </summary>
    public const int MaxAttribute = 25;

    // --- load, purse, progress --------------------------------------------------

    /// <summary>[Confirmed] Maximum carried weight, uint16 LE, in <b>tenths of a pound</b> (5300 prints "0530").</summary>
    public const int OffMaxWeight = 0x64;

    /// <summary>[Confirmed] Current carried weight, uint16 LE, tenths of a pound (289 prints "0028").</summary>
    public const int OffCurWeight = 0x66;

    /// <summary>
    /// [Confirmed] Spell-list high-water mark: one past the highest occupied spell slot, i.e. how
    /// far the game scans when listing spells. See <see cref="OffItemCount"/> for how the
    /// distinction was established.
    /// </summary>
    public const int OffSpellCount = 0x6A;

    /// <summary>
    /// [Confirmed] Inventory high-water mark: one past the highest occupied inventory slot.
    ///
    /// <para>Every shipped record has its items packed from slot 0, so this byte reads exactly like
    /// a population count there — which is what it was first taken for. The game itself settled it:
    /// handed the Count's Amulet, a character carrying three items in slots 0–2 received it in slot
    /// <b>9</b> and this byte went to <b>10</b>, not 4. Editing a far slot without raising this byte
    /// would leave the new item outside the range the game scans, and therefore invisible.</para>
    /// </summary>
    public const int OffItemCount = 0x6B;

    /// <summary>[Confirmed] Experience, uint32 LE (printed with "%011ld").</summary>
    public const int OffExperience = 0x6C;

    /// <summary>[Confirmed] Rations, uint16 LE.</summary>
    public const int OffRations = 0x70;

    /// <summary>[Confirmed] Gold, uint32 LE (printed with "%05ld$").</summary>
    public const int OffGold = 0x72;

    /// <summary>
    /// [Unidentified] A second uint32 the game's own debug menu increases by 1,000,000 in the same
    /// breath as it adds 100,000 to <see cref="OffExperience"/>. It appears on no sheet page.
    /// Read and shown, never written.
    /// </summary>
    public const int OffUnknownCounter = 0x76;

    // --- languages --------------------------------------------------------------

    /// <summary>
    /// [Confirmed] Eight language bytes; non-zero = the character speaks it. Writing 1 across the
    /// range made all eight lines appear on sheet page 5. The shipped records store 2, not 1 —
    /// and the Half-Orc's Orc-tongue byte and the Dwarf's Dwarven-tongue byte are exactly the ones
    /// set, which is what pinned the order.
    /// </summary>
    public const int OffLanguages = 0x7A;
    public const int LanguageCount = 8;

    // --- inventory --------------------------------------------------------------

    /// <summary>[Confirmed] First inventory slot.</summary>
    public const int OffInventory = 0x82;

    /// <summary>Bytes per inventory slot: id, equipped flag, unknown, condition %.</summary>
    public const int InventoryEntrySize = 4;

    /// <summary>
    /// Slots between <see cref="OffInventory"/> and <see cref="OffSpells"/>. 0x142 - 0x82 = 0xC0 =
    /// 48 x 4, so the array fills the gap exactly. Slots beyond the first few were never observed
    /// in use, but a quest item handed to a three-item character landed in slot 9, which rules out
    /// a short packed array.
    /// </summary>
    public const int InventorySlots = (OffSpells - OffInventory) / InventoryEntrySize;

    public const int InvId = 0;
    public const int InvEquipped = 1;
    public const int InvUnknown = 2;
    public const int InvCondition = 3;

    // --- spells -----------------------------------------------------------------

    /// <summary>[Confirmed] First spell slot; Merlin's "Burning hands 8/8, Light 4/4" reads 01 08 02 04.</summary>
    public const int OffSpells = 0x142;

    /// <summary>Bytes per spell slot: id, uses remaining.</summary>
    public const int SpellEntrySize = 2;

    /// <summary>Spell slots to the end of the record: (410 - 0x142) / 2 = 44.</summary>
    public const int SpellSlots = (RecordSize - OffSpells) / SpellEntrySize;

    public const int SpellId = 0;
    public const int SpellUses = 1;

    // --- constants used by validation -------------------------------------------

    /// <summary>Sentinel the game leaves in every live record at 0x10/0x11 and 0x16.</summary>
    public const int OffSentinelWord = 0x10;
    public const int OffSentinelByte = 0x16;

    // --- editor ceilings --------------------------------------------------------
    // The trainer's own limits, named here rather than repeated as literals so a freeze can compare
    // against the value that will actually be stored (a freeze whose target exceeds the ceiling
    // would never converge and would re-write the record on every tick, for ever).
    //
    // Two rules these have to obey, both learned the hard way:
    //   1. A ceiling must never be *below* what the record can already hold, or ticking a freeze -
    //      a control that says "keep this where it is" - would silently destroy the excess.
    //   2. A ceiling must never be *above* what IsValidRecord accepts, or an edit inside the
    //      editor's own range writes a record the locator then refuses, which force-detaches the
    //      poll loop and makes the next Attach fail outright.

    /// <summary>Hit points fit a uint16; this matches the validator's ceiling exactly.</summary>
    public const int MaxHitPoints = 9999;

    /// <summary>
    /// Gold is a full uint32 in the record and the validator does not constrain it, so the editor
    /// does not either. (The sheet's `%05ld$` is a minimum field width, not a maximum — reading it
    /// as a cap is what made an earlier version of this constant destructive.)
    /// </summary>
    public const long MaxGold = uint.MaxValue;

    /// <summary>Rations are a uint16 and unconstrained by the validator.</summary>
    public const int MaxRations = ushort.MaxValue;

    /// <summary>
    /// Maximum load in pounds. The field is a uint16 of tenths, so 6553 lb would fit — but
    /// <see cref="CharacterRecord.IsValidRecord"/> rejects anything above 30000 tenths, and a
    /// record the validator refuses is one the trainer can no longer attach to. Rule 2 above.
    /// </summary>
    public const int MaxLoadPounds = MaxPlausibleLoadTenths / 10;

    /// <summary>The validator's ceiling on the stored load words, in tenths of a pound.</summary>
    public const int MaxPlausibleLoadTenths = 30000;
}
