namespace BardsTaleTrilogyTrainer.Game;

/// <summary>
/// Layout of <c>BardsTale.Character</c> in the remaster, taken from the game's own IL2CPP
/// metadata: the field names come from <c>global-metadata.dat</c> and the offsets from the
/// field-offset table in <c>GameAssembly.dll</c>.
///
/// <para>The three offsets the Cheat Engine community had published for this game —
/// experience at +0x50, current hit points at +0x84 and current spell points at +0x8C —
/// come out of that table unchanged, which is what confirms the extraction is aligned.</para>
///
/// <para>Offsets are from the object base and so include the 16-byte IL2CPP header. Note that
/// experience and gold are <c>long</c>, not <c>int</c>, and that the remaster keeps a single
/// set of attributes rather than the original's current/maximum pairs.</para>
/// </summary>
public static class CharacterFormat
{
    /// <summary>Total size of a <c>Character</c> object, from the type's size table.</summary>
    public const int ObjectSize = 0x108;

    /// <summary>Bytes the locator reads when validating a candidate character.</summary>
    public const int ProbeSize = ObjectSize;

    // --- identity ---------------------------------------------------------------
    /// <summary><c>m_name</c> — pointer to a managed string.</summary>
    public const int OffName = 0x28;

    /// <summary><c>m_gender</c> (0 = Male, 1 = Female, 2 = It).</summary>
    public const int OffGender = 0x30;

    /// <summary><c>m_race</c> (0 = Human … 6 = Gnome).</summary>
    public const int OffRace = 0x34;

    /// <summary><c>m_class</c> (0 = Warrior … 12 = Geomancer, 13+ = monster/illusion/NPC).</summary>
    public const int OffClass = 0x38;

    /// <summary><c>m_initialClass</c> — the class the character started as.</summary>
    public const int OffInitialClass = 0x100;

    /// <summary><c>m_monsterType</c> — non-zero for summons and illusions.</summary>
    public const int OffMonsterType = 0x3C;

    // --- progression ------------------------------------------------------------
    /// <summary><c>m_experience</c> — <b>int64</b>.</summary>
    public const int OffExperience = 0x50;

    /// <summary><c>m_gold</c> — per-character carried gold, <b>int64</b>.</summary>
    public const int OffGold = 0x70;

    /// <summary><c>m_level</c>.</summary>
    public const int OffLevel = 0x7C;

    /// <summary><c>m_realLevel</c> — level before any drain.</summary>
    public const int OffRealLevel = 0xA8;

    /// <summary><c>m_levelDrain</c>.</summary>
    public const int OffLevelDrain = 0xAC;

    /// <summary><c>m_nmbrOfBattles</c>.</summary>
    public const int OffBattles = 0xB0;

    // --- attributes (one set; the remaster has no separate maxima) ---------------
    /// <summary><c>m_strength</c>; intelligence, dexterity, constitution and luck follow at +4 each.</summary>
    public const int OffStrength = 0x58;
    public const int OffIntelligence = 0x5C;
    public const int OffDexterity = 0x60;
    public const int OffConstitution = 0x64;
    public const int OffLuck = 0x68;
    public const int StatCount = 5;

    // --- vitals -----------------------------------------------------------------
    /// <summary><c>m_maxHitpoints</c>.</summary>
    public const int OffHpMax = 0x80;

    /// <summary><c>m_hitpoints</c>. [Confirmed] independently by the community's CE scripts.</summary>
    public const int OffHpCur = 0x84;

    /// <summary><c>m_maxSpellpoints</c>.</summary>
    public const int OffSpMax = 0x88;

    /// <summary><c>m_spellpoints</c>. [Confirmed] independently by the community's CE scripts.</summary>
    public const int OffSpCur = 0x8C;

    /// <summary><c>m_condition</c> — 0 = Okay, 1 = Poisoned … 8 = Drained.</summary>
    public const int OffCondition = 0xA0;

    // --- class perks ------------------------------------------------------------
    public const int OffAttacks = 0x90;
    public const int OffDisarmTrapBonus = 0xB4;
    public const int OffIdentifyBonus = 0xB8;
    public const int OffHideInShadowsBonus = 0xBC;
    public const int OffCriticalHit = 0xC0;
    public const int OffSongsRemaining = 0xC4;
    public const int OffSongsKnown = 0xC8;

    // --- spells and items -------------------------------------------------------
    /// <summary>
    /// <c>m_spellLevel</c> — pointer to an <c>int[16]</c> indexed by <em>class id</em>, so the
    /// caster classes live at indices 6–12. The constructor allocates it with length 16.
    /// </summary>
    public const int OffSpellLevels = 0xD0;

    /// <summary>Length of the <c>m_spellLevel</c> array (one slot per class id).</summary>
    public const int SpellLevelSlots = 16;

    /// <summary>Highest spell-class level the game grants.</summary>
    public const int MaxSpellLevel = 7;

    /// <summary>
    /// <c>m_learntSpells</c> — <c>List&lt;Spell&gt;</c> holding spells the character was taught
    /// outright rather than earning through a school level.
    ///
    /// <para><c>Character.KnowsSpell</c> tests this list first and returns true on a hit, before
    /// it looks at <see cref="OffSpellLevels"/> at all. Spells whose
    /// <c>SpellDescription.m_level</c> is 0 — the cross-game ones such as ZZGO and NUKE, and the
    /// chapter quest spells — can never satisfy the school-level test, so this list is the only
    /// place they can live.</para>
    /// </summary>
    public const int OffLearntSpells = 0xD8;

    /// <summary>
    /// <c>m_recentSpells</c> — the cast-again shortcut list. Not spell knowledge; listed so it
    /// is not mistaken for <see cref="OffLearntSpells"/>, which it sits far away from.
    /// </summary>
    public const int OffRecentSpells = 0x10;

    /// <summary><c>m_inventory</c> — a <c>BardsTale.Inventory</c> object.</summary>
    public const int OffInventory = 0xE0;

    /// <summary><c>m_statusEffects</c>.</summary>
    public const int OffStatusEffects = 0xF0;

    // --- BardsTale.Party --------------------------------------------------------
    /// <summary>Static <c>Instance</c>, first field of the static block.</summary>
    public const int PartyInstanceStatic = 0x00;

    /// <summary><c>m_slots</c> — the UI slot widgets.</summary>
    public const int PartySlotsField = 0x38;

    /// <summary>
    /// <c>m_members</c> — a <c>PartyMember[]</c>. Each element is the slot's UI wrapper, not the
    /// character itself; the character hangs off it at <see cref="PartyMemberCharacter"/>.
    /// </summary>
    public const int PartyMembers = 0x40;

    /// <summary><c>PartyMember.m_character</c> — the <c>Character</c> in that slot.</summary>
    public const int PartyMemberCharacter = 0x10;

    /// <summary><c>m_inventory</c> — the shared party pack.</summary>
    public const int PartyInventory = 0x60;

    /// <summary>
    /// <c>m_gold</c> — party purse, <b>int64</b>. The community's CE table wrote a dword here;
    /// this is the same field, at the same offset, with its real width.
    /// </summary>
    public const int PartyGold = 0x68;

    // --- BardsTale.GlobalSpells -------------------------------------------------
    /// <summary>Static <c>Instance</c>, first field of the static block.</summary>
    public const int GlobalSpellsInstanceStatic = 0x00;

    /// <summary><c>m_spells</c> — every <c>SpellDescription</c> the game loaded, in table order.</summary>
    public const int GlobalSpellsSpells = 0x18;

    /// <summary>
    /// <c>m_spellsByEnum</c> — the same descriptions indexed by <see cref="SpellId"/>.
    /// <c>GlobalSpells.GetSpell</c> is nothing but a bounds-checked read from this array.
    /// </summary>
    public const int GlobalSpellsByEnum = 0x20;

    // --- BardsTale.SpellDescription (instance size 0xB8) ------------------------
    /// <summary><c>m_code</c> — the four-letter code the game shows, e.g. <c>ZZGO</c>.</summary>
    public const int SpellDescriptionCode = 0x10;

    /// <summary><c>m_spell</c> — which <see cref="SpellId"/> this describes.</summary>
    public const int SpellDescriptionSpell = 0x18;

    /// <summary><c>m_class</c> — the casting school, as a class id.</summary>
    public const int SpellDescriptionClass = 0x20;

    /// <summary>
    /// <c>m_level</c> — the school level that grants it. <b>Zero means no school ever grants
    /// it</b>, which is what marks the cross-game and quest spells.
    /// </summary>
    public const int SpellDescriptionLevel = 0x24;

    /// <summary><c>m_cost</c> — spell points per cast.</summary>
    public const int SpellDescriptionCost = 0x28;

    /// <summary><c>m_combat</c> — castable in combat.</summary>
    public const int SpellDescriptionCombat = 0x38;

    /// <summary><c>m_nonCombat</c> — castable outside combat.</summary>
    public const int SpellDescriptionNonCombat = 0x39;

    /// <summary><c>m_bt1Spell</c>; <c>m_bt2Spell</c> and <c>m_bt3Spell</c> follow at +1 each.</summary>
    public const int SpellDescriptionBt1 = 0x41;

    // --- BardsTale.Inventory ----------------------------------------------------
    /// <summary><c>Inventory</c> holds a single <c>Item[]</c> reference.</summary>
    public const int InventoryItems = 0x10;

    // --- BardsTale.Item ---------------------------------------------------------
    /// <summary><c>m_itemDesc</c> — the shared description this item instantiates.</summary>
    public const int ItemDescription = 0x10;

    /// <summary><c>m_equipped</c>.</summary>
    public const int ItemEquipped = 0x20;

    /// <summary>
    /// <c>m_charges</c>. <c>Character.UseItemCharge</c> returns immediately when this is zero
    /// instead of decrementing, so a zeroed item is never used up — which is what the game
    /// means by an item with no charge count in its description.
    /// </summary>
    public const int ItemCharges = 0x24;

    /// <summary><c>m_name</c> on <c>ItemDescription</c>.</summary>
    public const int ItemDescriptionName = 0x10;

    // --- enums ------------------------------------------------------------------
    public static readonly string[] Stats = { "Strength", "IQ", "Dexterity", "Constitution", "Luck" };

    public static readonly string[] Classes =
    {
        "Warrior", "Paladin", "Rogue", "Bard", "Hunter", "Monk",
        "Conjurer", "Magician", "Sorcerer", "Wizard",
        "Archmage", "Chronomancer", "Geomancer",
        "Monster", "Illusion", "NPC",
    };

    public static readonly string[] Races =
    {
        "Human", "Elf", "Dwarf", "Hobbit", "Half-Elf", "Half-Orc", "Gnome"
    };

    public static readonly string[] Genders = { "Male", "Female", "It" };

    public static readonly string[] Conditions =
    {
        "Okay", "Poisoned", "Old", "Dead", "Stoned",
        "Paralyzed", "Possessed", "Insane", "Drained",
    };

    /// <summary>Class ids that have a spell-level slot, in <c>m_spellLevel</c> index order.</summary>
    public static readonly (int ClassId, string Name)[] CasterClasses =
    {
        (6, "Conjurer"), (7, "Magician"), (8, "Sorcerer"), (9, "Wizard"),
        (10, "Archmage"), (11, "Chronomancer"), (12, "Geomancer"),
    };

    public static string ClassName(int c) => c >= 0 && c < Classes.Length ? Classes[c] : $"?({c})";
    public static string RaceName(int r) => r >= 0 && r < Races.Length ? Races[r] : $"?({r})";
    public static string GenderName(int g) => g >= 0 && g < Genders.Length ? Genders[g] : $"?({g})";
    public static string ConditionName(int c) => c >= 0 && c < Conditions.Length ? Conditions[c] : $"?({c})";

    /// <summary>
    /// Plausibility check on a candidate <c>Character</c> object, used as a sanity gate when
    /// the roster is reached by scanning rather than through <c>Party.Instance</c>.
    /// </summary>
    public static bool LooksLikeCharacter(ReadOnlySpan<byte> buf)
    {
        if (buf.Length < ObjectSize) return false;

        long xp = ReadI64(buf, OffExperience);
        if (xp < 0 || xp > 100_000_000_000L) return false;

        long gold = ReadI64(buf, OffGold);
        if (gold < 0 || gold > 100_000_000_000L) return false;

        int race = ReadI32(buf, OffRace);
        if (race < 0 || race >= Races.Length) return false;

        int cls = ReadI32(buf, OffClass);
        if (cls < 0 || cls >= Classes.Length) return false;

        int gender = ReadI32(buf, OffGender);
        if (gender < 0 || gender >= Genders.Length) return false;

        int level = ReadI32(buf, OffLevel);
        if (level < 0 || level > 1000) return false;

        int hpMax = ReadI32(buf, OffHpMax);
        int hp = ReadI32(buf, OffHpCur);
        if (hpMax <= 0 || hpMax > 100_000) return false;
        if (hp < 0 || hp > hpMax) return false;

        int spMax = ReadI32(buf, OffSpMax);
        int sp = ReadI32(buf, OffSpCur);
        if (spMax < 0 || spMax > 100_000) return false;
        if (sp < 0 || sp > spMax) return false;

        int condition = ReadI32(buf, OffCondition);
        if (condition < 0 || condition >= Conditions.Length) return false;

        for (int i = 0; i < StatCount; i++)
        {
            int v = ReadI32(buf, OffStrength + i * 4);
            if (v < 0 || v > 1000) return false;
        }

        return true;
    }

    public static int ReadI32(ReadOnlySpan<byte> buf, int off) =>
        buf[off] | (buf[off + 1] << 8) | (buf[off + 2] << 16) | (buf[off + 3] << 24);

    public static long ReadI64(ReadOnlySpan<byte> buf, int off) =>
        (uint)ReadI32(buf, off) | ((long)ReadI32(buf, off + 4) << 32);
}
