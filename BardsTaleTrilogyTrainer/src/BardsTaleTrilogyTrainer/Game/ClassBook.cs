namespace BardsTaleTrilogyTrainer.Game;

/// <summary>Broad role a class fills in the party.</summary>
public enum ClassRole
{
    Fighter,
    Stealth,
    Hybrid,
    Caster,
}

/// <summary>
/// One character class. <paramref name="Id"/> is the value of
/// <c>Character.Class</c> — and, for a casting class, also its index into the
/// character's <c>m_spellLevel</c> array, because the game indexes that array by
/// the class enum itself.
/// </summary>
public sealed record ClassInfo(
    int Id,
    string Name,
    ClassRole Role,
    SpellClass Art,
    bool AvailableAtCreation,
    int IntroducedIn,
    string Description)
{
    public bool IsCaster => Role == ClassRole.Caster;

    /// <summary>"BT1", "BT2" or "BT3" — the game the class first appears in.</summary>
    public string GameTag => $"BT{IntroducedIn}";

    public string Display => $"{Name} ({Role})";
}

/// <summary>Result of testing whether a character may change to a given class.</summary>
public sealed record ClassChangeCheck(bool Allowed, string Reason);

/// <summary>
/// One class-specific statistic to show for a character. <paramref name="Value"/>
/// is what the character actually has; <paramref name="Detail"/> says where the
/// number comes from and what it means.
/// </summary>
public sealed record ClassAbility(string Name, string Value, string Detail);

/// <summary>
/// The single source of truth for anything derived from a character's class: the
/// roster, the Review Board's class-change rules, and the meaning of each
/// class-specific score.
///
/// The class enum is **[Verified]** — read from <c>Character.Class</c> in the
/// installed game's IL2CPP metadata. So is the spell-level rule: the array is an
/// <c>int[16]</c> indexed by that same enum, and
/// <c>PlayerState_ReviewBoard::UpgradeMage</c> caps a school at
/// <c>Mathf.Min(7, (characterLevel + 1) / 2)</c>.
///
/// The change *requirements* and the meaning of each ability score come from the
/// game manual and community measurement rather than the binary, and are marked
/// as such in <c>docs/ReverseEngineering.md</c> §6.
/// </summary>
public static class ClassBook
{
    /// <summary>Highest playable class id (12 = Geomancer). 13–15 are Monster, Illusion and NPC.</summary>
    public const int MaxPlayableClassId = 12;

    /// <summary>[Verified] Highest spell level in any school — the cap in <c>UpgradeMage</c>.</summary>
    public const int MaxSpellLevel = 7;

    /// <summary>
    /// Special-ability scores are rolled against on a 0–255 scale in the games'
    /// own terms, where 255 is a certainty. The remaster prints the raw score on
    /// the character sheet and subtracts a per-map penalty when it rolls.
    /// </summary>
    public const int MaxAbilityScore = 255;

    /// <summary>
    /// The thirteen playable classes. [Verified] against <c>Character.Class</c>:
    /// Warrior=0 … Geomancer=12 (the enum continues Monster=13, Illusion=14,
    /// NPC=15, MAX=16, which are not playable).
    /// </summary>
    public static readonly IReadOnlyList<ClassInfo> Classes = new[]
    {
        new ClassInfo(0, "Warrior", ClassRole.Fighter, SpellClass.None, true, 1,
            "The basic fighter, able to use nearly every weapon in the game. Gains an extra attack for every 4 levels after the 1st."),
        new ClassInfo(1, "Paladin", ClassRole.Fighter, SpellClass.None, true, 1,
            "A holy warrior. Gains multiple attacks like a Warrior, wields weapons others cannot, and has greatly increased resistance to evil magic."),
        new ClassInfo(2, "Rogue", ClassRole.Stealth, SpellClass.None, true, 1,
            "A professional thief with moderate combat ability. Hides in shadows, identifies items, and searches for and disarms traps — the only safe way to open a trapped chest."),
        new ClassInfo(3, "Bard", ClassRole.Hybrid, SpellClass.None, true, 1,
            "A warrior who turned to music. Uses most warrior weapons and plays magical tunes, one per experience level before his throat runs dry. Needs an instrument equipped."),
        new ClassInfo(4, "Hunter", ClassRole.Fighter, SpellClass.None, true, 1,
            "An assassin who strikes at vital areas. Has a chance — growing with experience — to score a critical hit and kill a foe outright."),
        new ClassInfo(5, "Monk", ClassRole.Fighter, SpellClass.None, true, 1,
            "A martial artist. Can use weapons but does far better without them at higher levels, and his armour class improves with every level gained."),
        new ClassInfo(6, "Conjurer", ClassRole.Caster, SpellClass.Conjurer, true, 1,
            "Creates and manipulates objects and light. One of the two magical schools open at character creation."),
        new ClassInfo(7, "Magician", ClassRole.Caster, SpellClass.Magician, true, 1,
            "Bestows effects on ordinary objects, increasing their capability or changing their form. The other school open at character creation."),
        new ClassInfo(8, "Sorcerer", ClassRole.Caster, SpellClass.Sorcerer, false, 1,
            "Illusion and perception. Not available at creation — a magic user must first have learned spell level 3 in one other school."),
        new ClassInfo(9, "Wizard", ClassRole.Caster, SpellClass.Wizard, false, 1,
            "Summons and binds supernatural creatures. Not available at creation — a magic user must first have learned spell level 3 in two other schools."),
        new ClassInfo(10, "Archmage", ClassRole.Caster, SpellClass.Archmage, false, 2,
            "The master of all four basic schools. The promotion itself can only be taken in BT2, even by a character who met the requirements in BT1."),
        new ClassInfo(11, "Chronomancer", ClassRole.Caster, SpellClass.Chronomancer, false, 3,
            "Bends time itself. Requires mastery of three magical schools — and gives up every spell learned in them on promotion."),
        new ClassInfo(12, "Geomancer", ClassRole.Caster, SpellClass.Geomancer, false, 3,
            "The fighter's road into magic, drawing power from the earth. Open only to fighting classes, and only once unlocked during BT3's story."),
    };

    /// <summary>The seven casting schools — exactly the indices of <c>m_spellLevel</c> the game uses.</summary>
    public static readonly IReadOnlyList<ClassInfo> CastingClasses =
        Classes.Where(c => c.IsCaster).ToArray();

    /// <summary>The four basic schools a Sorcerer, Wizard or Archmage promotion counts.</summary>
    public static readonly IReadOnlyList<ClassInfo> BasicArts =
        Classes.Where(c => c.Id >= 6 && c.Id <= 9).ToArray();

    public static ClassInfo? Find(int id) =>
        id >= 0 && id < Classes.Count ? Classes[id] : null;

    public static string ClassName(int id) =>
        Find(id)?.Name ?? CharacterFormat.ClassName(id);

    public static bool IsCaster(int id) => Find(id)?.IsCaster == true;

    /// <summary>
    /// [Verified] The spell level a character of this experience level may hold.
    /// <c>PlayerState_ReviewBoard::UpgradeMage</c> computes
    /// <c>Mathf.Min(7, (characterLevel + 1) / 2)</c>, so new spell levels arrive at
    /// character levels 1, 3, 5, 7, 9, 11 and 13.
    /// </summary>
    public static int SpellLevelForLevel(int characterLevel) =>
        characterLevel < 1 ? 0 : Math.Min(MaxSpellLevel, (characterLevel + 1) / 2);

    /// <summary>The character level at which a given spell level becomes available.</summary>
    public static int LevelForSpellLevel(int spellLevel) =>
        spellLevel < 1 ? 1 : 1 + (Math.Min(MaxSpellLevel, spellLevel) - 1) * 2;

    /// <summary>Melee attacks per round for a Warrior or Paladin: one more every 4 levels after the 1st.</summary>
    public static int MeleeAttacks(int level) =>
        1 + Math.Max(0, (Math.Max(1, level) - 1) / 4);

    /// <summary>
    /// A Monk's damage when fighting bare-handed, from the BT1 table published in
    /// the Bard's Tale Online character reference. Levels 5–6 and 7–8 share the
    /// same value in the original table; it is reproduced faithfully.
    /// </summary>
    public static int MonkUnarmedDamage(int level) => level switch
    {
        <= 2 => 4,
        <= 4 => 8,
        <= 6 => 16,
        <= 8 => 16,
        <= 12 => 32,
        <= 16 => 40,
        <= 24 => 48,
        <= 30 => 56,
        <= 39 => 80,
        <= 48 => 96,
        <= 55 => 128,
        <= 61 => 160,
        <= 63 => 192,
        _ => 234,
    };

    /// <summary>A Monk's armour class improves by 1 for every level gained after the 1st.</summary>
    public static int MonkArmorClassBonus(int level) => Math.Max(0, Math.Max(1, level) - 1);

    /// <summary>Renders an ability score as the percentage the game rolls against.</summary>
    public static string ScoreAsPercent(int score) =>
        $"{Math.Clamp(score, 0, MaxAbilityScore) * 100 / MaxAbilityScore}%";

    /// <summary>Formats a live ability score together with what it means as a chance.</summary>
    public static string ScoreText(int score) =>
        $"{score} of {MaxAbilityScore} ({ScoreAsPercent(score)})";

    /// <summary>
    /// The best the class-specific scores can honestly be set to.
    ///
    /// <para>The four scores the game rolls against — disarm, hide in shadows,
    /// identify and critical hit — go to <see cref="MaxAbilityScore"/>, a
    /// certainty before the remaster subtracts its per-map penalty. The Bard's
    /// remaining tunes refill to the character's level, which is the manual's rule
    /// (as many tunes as experience levels before his throat runs dry).</para>
    ///
    /// <para>Attacks per round and songs known are deliberately left alone: the
    /// first is a count the game loops over in combat rather than a chance, and
    /// the second is how many of the six songs the Bard has learned. Neither means
    /// anything at 255.</para>
    /// </summary>
    public static ClassScores MaxAbilityScores(ClassScores current, int level) => current with
    {
        DisarmTrapBonus = MaxAbilityScore,
        HideInShadowsBonus = MaxAbilityScore,
        IdentifyBonus = MaxAbilityScore,
        CriticalHit = MaxAbilityScore,
        SongsRemaining = Math.Max(1, level),
    };

    // --- class change ------------------------------------------------------------

    /// <summary>
    /// Tests the Review Board's rules for moving <paramref name="fromClass"/> to
    /// <paramref name="toClass"/>. <paramref name="spellLevels"/> is the character's
    /// <c>m_spellLevel</c> array, indexed by class id.
    ///
    /// The rules are the manual's: a magic user who leaves a school may never
    /// return to it; Sorcerer needs spell level 3 in one other school, Wizard in
    /// two, Archmage in all four; the Chronomancer needs three schools mastered;
    /// the Geomancer is open only to fighters. Fighting classes carry no
    /// documented prerequisite.
    /// </summary>
    public static ClassChangeCheck CanChangeTo(int fromClass, int toClass, int level, IReadOnlyList<int> spellLevels)
    {
        var target = Find(toClass);
        if (target == null)
            return new ClassChangeCheck(false, $"Class id {toClass} is not a playable class.");

        var source = Find(fromClass);
        if (toClass == fromClass)
            return new ClassChangeCheck(false, $"Already a {target.Name}.");

        // "Magic users leaving any class cannot return to it" — a non-zero level in
        // the target school means the character has already been that class.
        int held = SchoolLevel(spellLevels, toClass);
        if (target.IsCaster && held > 0)
            return new ClassChangeCheck(false,
                $"{target.Name} spell level is already {held} — a magic user who leaves a school may not return to it.");

        int atThree = BasicArts.Count(c => SchoolLevel(spellLevels, c.Id) >= 3);
        int mastered = BasicArts.Count(c => SchoolLevel(spellLevels, c.Id) >= MaxSpellLevel);

        return toClass switch
        {
            8 when atThree < 1 => new ClassChangeCheck(false,
                "Sorcerer requires spell level 3 or higher in one other magical school (none reached 3)."),
            9 when atThree < 2 => new ClassChangeCheck(false,
                $"Wizard requires spell level 3 or higher in two other magical schools (only {atThree} reached 3)."),
            10 when atThree < 4 => new ClassChangeCheck(false,
                $"Archmage requires spell level 3 or higher in all four basic schools (only {atThree} reached 3)."),
            10 => new ClassChangeCheck(true,
                "Archmage requirements met. The promotion itself is only offered in BT2, even to a character who qualified in BT1."),
            11 when mastered < 3 => new ClassChangeCheck(false,
                $"Chronomancer requires three magical schools mastered to spell level {MaxSpellLevel} (only {mastered} mastered)."),
            11 => new ClassChangeCheck(true,
                "Chronomancer requirements met. The promotion is offered in BT3 and gives up the spells of the schools it came from."),
            12 when source is not { Role: ClassRole.Fighter } => new ClassChangeCheck(false,
                $"Geomancer is open only to fighting classes; {source?.Name ?? "this character"} is not one."),
            12 => new ClassChangeCheck(true,
                "Geomancer requirements met. The class must also be unlocked during BT3's story before the game offers it."),
            _ => new ClassChangeCheck(true, ChangeSummary(target, level)),
        };
    }

    private static int SchoolLevel(IReadOnlyList<int> spellLevels, int classId) =>
        classId >= 0 && classId < spellLevels.Count ? spellLevels[classId] : 0;

    private static string ChangeSummary(ClassInfo target, int level)
    {
        if (!target.IsCaster)
            return $"May become a {target.Name}.";
        int spellLevel = SpellLevelForLevel(level);
        return $"May become a {target.Name} — a level-{Math.Max(1, level)} character holds {target.Name} spell level {spellLevel}.";
    }

    // --- class-specific abilities --------------------------------------------------

    /// <summary>
    /// The statistics worth showing for this character, read from the fields the
    /// game itself keeps. <paramref name="scores"/> carries the live values;
    /// each entry's <c>Detail</c> explains what the number does and where the
    /// explanation comes from.
    /// </summary>
    public static IReadOnlyList<ClassAbility> AbilitiesFor(
        int classId, int level, int dexterity, ClassScores scores, IReadOnlyList<int> spellLevels,
        SpellCatalog? catalog = null)
    {
        var list = new List<ClassAbility>();
        var info = Find(classId);
        if (info == null)
        {
            list.Add(new ClassAbility("Class", CharacterFormat.ClassName(classId),
                $"The class field holds {classId}; the playable classes are 0–{MaxPlayableClassId}."));
            return list;
        }

        level = Math.Max(1, level);

        switch (classId)
        {
            case 0: // Warrior
                AddAttacks(list, scores, level, "One extra attack for every 4 levels of experience after the 1st (manual).");
                break;

            case 1: // Paladin
                AddAttacks(list, scores, level, "Paladins gain multiple attacks at higher levels like a Warrior (manual).");
                list.Add(new ClassAbility("Resistance to evil magic", "greatly increased",
                    "The manual states a greatly increased resistance; the game does not keep it as a number."));
                break;

            case 2: // Rogue
                list.Add(new ClassAbility("Disarm traps", ScoreText(scores.DisarmTrapBonus),
                    "m_disarmTrapBonus. Grows +3–11 per level-up; about 175 gives a ~95% chance in every location."));
                list.Add(new ClassAbility("Hide in shadows", ScoreText(scores.HideInShadowsBonus),
                    "m_hideInShadowsBonus. Grows +3–11 per level-up, on the same 0–255 scale."));
                list.Add(new ClassAbility("Identify items", ScoreText(scores.IdentifyBonus),
                    "m_identifyBonus — the Rogue's chance to identify an unknown item."));
                break;

            case 3: // Bard
                list.Add(new ClassAbility("Songs known", $"{scores.SongsKnown}",
                    "m_songsKnown. A true Bard has six songs on his lips (manual); BT2 and BT3 carry their own song lists."));
                list.Add(new ClassAbility("Tunes before a drink", $"{scores.SongsRemaining} (of {level} at full)",
                    "m_songsRemaining. A Bard can play as many tunes as experience levels before his throat gets dry (manual); a drink refills it."));
                break;

            case 4: // Hunter
                list.Add(new ClassAbility("Critical hit", ScoreText(scores.CriticalHit),
                    "m_criticalHit. Starts at 0 and rises by 1–32 at each level-up, so criticals land reliably from about level 16."));
                list.Add(new ClassAbility("Critical hit (Construction Set formula)", HunterCriticalPercent(level, dexterity),
                    "What the Construction Set manual predicts for this level and Dexterity: 1–3% per level, plus 1% per point of Dexterity over 14 per level, to a maximum of 99%."));
                list.Add(new ClassAbility("Dungeon penalty", "applies",
                    "The remaster subtracts a flat, per-map penalty from the score, so even 255 does not always land deep in a dungeon."));
                break;

            case 5: // Monk
                list.Add(new ClassAbility("Unarmed damage", $"{MonkUnarmedDamage(level)}",
                    "From the BT1 table (levels 1–2: 4, 3–4: 8, 5–8: 16, 9–12: 32 … level 64: 234). A Monk out-damages his own weapons from about level 3."));
                list.Add(new ClassAbility("Armour class bonus", $"{MonkArmorClassBonus(level)} better",
                    "A Monk's armour class improves by 1 for each level of experience past the 1st. The game computes AC from equipment at read time, so there is no AC field to edit."));
                break;

            default:
                AddCasterAbilities(list, info, level, spellLevels, catalog ?? SpellCatalog.Empty);
                break;
        }

        return list;
    }

    private static void AddAttacks(List<ClassAbility> list, ClassScores scores, int level, string detail)
    {
        string expected = $"{MeleeAttacks(level)} expected";
        list.Add(new ClassAbility("Melee attacks", $"{scores.Attacks} per round ({expected})",
            $"m_nmbrOfAttacks. {detail}"));
    }

    private static void AddCasterAbilities(
        List<ClassAbility> list, ClassInfo info, int level, IReadOnlyList<int> spellLevels,
        SpellCatalog catalog)
    {
        list.Add(new ClassAbility("Magical school", Spellbook.ArtName(info.Art),
            $"{info.Name} casts from the {Spellbook.ArtName(info.Art)} school — index {info.Id} of the character's m_spellLevel array."));

        int held = SchoolLevel(spellLevels, info.Id);
        int allowed = SpellLevelForLevel(level);
        list.Add(new ClassAbility("Spell level", $"{held} of {MaxSpellLevel}",
            $"m_spellLevel[{info.Id}]. New spell levels arrive at character levels 1, 3, 5, 7, 9, 11 and 13 — a level-{level} character may hold spell level {allowed}."));
        list.Add(new ClassAbility("Spells known", SpellsKnownText(catalog, info, held),
            $"Every {Spellbook.ArtName(info.Art)} spell up to level {held}, counted from the game's own spell table. Spells granted individually — quest, bought, or by script — are held in m_learntSpells instead and are not counted here."));

        int atThree = BasicArts.Count(c => SchoolLevel(spellLevels, c.Id) >= 3);
        int mastered = BasicArts.Count(c => SchoolLevel(spellLevels, c.Id) >= MaxSpellLevel);
        list.Add(new ClassAbility("Basic schools at level 3+", $"{atThree} of 4",
            "Sorcerer needs one, Wizard two, Archmage all four."));
        list.Add(new ClassAbility("Basic schools mastered", $"{mastered} of 4",
            $"Mastery is spell level {MaxSpellLevel}. The Chronomancer needs three."));
    }

    private static string HunterCriticalPercent(int level, int dexterity)
    {
        int dexBonus = Math.Max(0, dexterity - 14);
        int low = Math.Min(99, level * (1 + dexBonus));
        int high = Math.Min(99, level * (3 + dexBonus));
        return low == high ? $"{low}%" : $"{low}–{high}%";
    }

    /// <summary>
    /// How many of a school's spells the character holds, out of everything that school teaches.
    /// The counts come from the running game's spell table, because a spell's school and level
    /// are serialized asset data rather than anything in the executable; without the game
    /// attached there is no honest number to show.
    /// </summary>
    private static string SpellsKnownText(SpellCatalog catalog, ClassInfo info, int heldLevel)
    {
        if (!catalog.IsLive) return "— (attach and locate to count)";
        int total = catalog.ForSchool(info.Id).Count();
        int held = catalog.ForSchool(info.Id).Count(s => s.Level <= heldLevel);
        return $"{held} of {total}";
    }
}

/// <summary>
/// The live class-specific scores read off one character. Every member is a real
/// <c>Character</c> field; see <see cref="CharacterFormat"/> for the offsets.
/// </summary>
public readonly record struct ClassScores(
    int Attacks,
    int DisarmTrapBonus,
    int IdentifyBonus,
    int HideInShadowsBonus,
    int CriticalHit,
    int SongsRemaining,
    int SongsKnown);
