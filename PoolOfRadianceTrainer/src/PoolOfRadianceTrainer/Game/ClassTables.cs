namespace PoolOfRadianceTrainer.Game;

/// <summary>
/// The class and race numbers Pool of Radiance runs on — AD&amp;D 1st edition as the game implements
/// it — keyed by the record's own class and race bytes. This is the single source of truth for
/// anything derived from "what class is this character, at what level": hit dice, THAC0, saving
/// throws, thief skills, spells per day, the ability minimums the creation screen enforces, and
/// which races may take which classes and how far.
///
/// <para><b>What is anchored to a real record, and what isn't.</b> Three records decoded from a
/// running game and a real save pin the level-1 and level-5 rows exactly:</para>
/// <list type="bullet">
///   <item>a level-1 dwarf fighter — THAC0 base 20, saves 14/15/16/17/17, movement 12;</item>
///   <item>a level-1 elf Fighter/Mage — THAC0 base 20, saves 14/13/11/15/12 (the best of the fighter
///   and magic-user rows, category by category), 7 hit points;</item>
///   <item>a level-5 human fighter from a real saved game — THAC0 base <b>16</b>, saves
///   <b>11/12/13/13/14</b>, class-level byte 5, attack level 5.</item>
/// </list>
/// <para>The fighter's two anchors are four levels apart and both sit on the same straight line
/// (THAC0 = 21 − level), and the level-5 saving throws are the 1e fighter level-5/6 row to the
/// number. The cleric, magic-user and thief rows, and everything above level 5, follow the same
/// published tables but have no record of their own to check against — they are the game's rules
/// rather than measurements, and the trainer says so rather than implying otherwise.</para>
/// </summary>
public static class ClassTables
{
    /// <summary>The highest level any class reaches in this game (the thief's).</summary>
    public const int MaxLevel = 9;

    /// <summary>The four classes Pool of Radiance offers, as class bytes. These double as indices
    /// into the record's per-class level bytes (<see cref="PorFormat.OffClassLevels"/>).</summary>
    public static readonly int[] BaseClasses =
        { PorFormat.ClassCleric, PorFormat.ClassFighter, PorFormat.ClassMage, PorFormat.ClassThief };

    /// <summary>The single classes a class byte combines — <c>{2}</c> for a fighter, <c>{2, 5}</c>
    /// for a Fighter/Mage. Only the combinations the game itself offers are listed.</summary>
    public static int[] SingleClassesOf(int classByte) => classByte switch
    {
        PorFormat.ClassCleric => new[] { PorFormat.ClassCleric },
        PorFormat.ClassFighter => new[] { PorFormat.ClassFighter },
        PorFormat.ClassMage => new[] { PorFormat.ClassMage },
        PorFormat.ClassThief => new[] { PorFormat.ClassThief },
        PorFormat.ClassClericFighter => new[] { PorFormat.ClassCleric, PorFormat.ClassFighter },
        PorFormat.ClassClericFighterMage => new[] { PorFormat.ClassCleric, PorFormat.ClassFighter, PorFormat.ClassMage },
        PorFormat.ClassClericMage => new[] { PorFormat.ClassCleric, PorFormat.ClassMage },
        PorFormat.ClassClericThief => new[] { PorFormat.ClassCleric, PorFormat.ClassThief },
        PorFormat.ClassFighterMage => new[] { PorFormat.ClassFighter, PorFormat.ClassMage },
        PorFormat.ClassFighterThief => new[] { PorFormat.ClassFighter, PorFormat.ClassThief },
        PorFormat.ClassFighterMageThief => new[] { PorFormat.ClassFighter, PorFormat.ClassMage, PorFormat.ClassThief },
        PorFormat.ClassMageThief => new[] { PorFormat.ClassMage, PorFormat.ClassThief },
        _ => Array.Empty<int>(),
    };

    /// <summary>True if this class byte is one a Pool of Radiance character can hold (the engine's
    /// druid/paladin/ranger/monk values are not).</summary>
    public static bool IsPlayableClass(int classByte) => SingleClassesOf(classByte).Length > 0;

    /// <summary>This class's bit in the record's class bitmask at <see cref="PorFormat.OffClassMask"/>.</summary>
    public static int ClassBit(int singleClass) => singleClass switch
    {
        PorFormat.ClassMage => 0x01,
        PorFormat.ClassCleric => 0x02,
        PorFormat.ClassThief => 0x04,
        PorFormat.ClassFighter => 0x08,
        _ => 0,
    };

    /// <summary>The class bitmask a character of these classes carries at
    /// <see cref="PorFormat.OffClassMask"/> — 0x08 for a fighter, 0x09 for a Fighter/Mage, 0x0B for
    /// a Cleric/Fighter/Mage.</summary>
    public static int ClassMask(IEnumerable<int> singleClasses)
    {
        int mask = 0;
        foreach (int cls in singleClasses) mask |= ClassBit(cls);
        return mask;
    }

    /// <summary>The class bitmask for a class byte, straight from the byte itself.</summary>
    public static int ClassMaskFor(int classByte) => ClassMask(SingleClassesOf(classByte));

    /// <summary>Every class byte the game can create, in record order.</summary>
    public static readonly int[] PlayableClasses =
        Enumerable.Range(0, PorFormat.Classes.Length).Where(IsPlayableClass).ToArray();

    // --- per-class numbers ----------------------------------------------------
    /// <summary>The class's hit die (fighter d10, cleric d8, thief d6, magic-user d4).</summary>
    public static int HitDie(int singleClass) => singleClass switch
    {
        PorFormat.ClassFighter => 10,
        PorFormat.ClassCleric => 8,
        PorFormat.ClassThief => 6,
        PorFormat.ClassMage => 4,
        _ => 0,
    };

    /// <summary>
    /// The class's THAC0 at a level: the roll it needs to hit Armor Class 0, before the Strength
    /// bonus and equipment. The fighter's line is measured — 20 at level 1 and 16 at level 5, four
    /// levels and one point apart — the rest are the published attack matrices, which step every
    /// three levels for a cleric, four for a thief and five for a magic-user.
    /// </summary>
    public static int Thac0(int singleClass, int level)
    {
        level = Math.Clamp(level, 1, MaxLevel);
        return singleClass switch
        {
            PorFormat.ClassFighter => 21 - level,
            PorFormat.ClassCleric => level <= 3 ? 20 : level <= 6 ? 18 : 16,
            PorFormat.ClassThief => level <= 4 ? 21 : level <= 8 ? 19 : 17,
            PorFormat.ClassMage => level <= 5 ? 21 : 19,
            _ => 20,
        };
    }

    // Saving throws, in record order: paralyzation/poison/death, petrification/polymorph,
    // rod/staff/wand, breath weapon, spell. Each row covers the level band that starts at its key.
    private static readonly (int From, int[] Row)[] FighterSaves =
    {
        (1, new[] { 14, 15, 16, 17, 17 }),
        (3, new[] { 13, 14, 15, 16, 16 }),
        (5, new[] { 11, 12, 13, 13, 14 }),   // measured on a real level-5 fighter
        (7, new[] { 10, 11, 12, 12, 13 }),
        (9, new[] { 8, 9, 10, 9, 11 }),
    };

    private static readonly (int From, int[] Row)[] ClericSaves =
    {
        (1, new[] { 10, 13, 14, 16, 15 }),
        (4, new[] { 9, 12, 13, 15, 14 }),
        (7, new[] { 7, 10, 11, 13, 12 }),
    };

    private static readonly (int From, int[] Row)[] MageSaves =
    {
        (1, new[] { 14, 13, 11, 15, 12 }),
        (6, new[] { 13, 11, 9, 13, 10 }),
    };

    private static readonly (int From, int[] Row)[] ThiefSaves =
    {
        (1, new[] { 13, 12, 14, 16, 15 }),
        (5, new[] { 12, 11, 12, 15, 13 }),
        (9, new[] { 11, 10, 10, 14, 11 }),
    };

    /// <summary>The class's five saving throws at a level. A copy, so a caller can't edit the table.</summary>
    public static int[] Saves(int singleClass, int level)
    {
        level = Math.Clamp(level, 1, MaxLevel);
        var table = singleClass switch
        {
            PorFormat.ClassFighter => FighterSaves,
            PorFormat.ClassCleric => ClericSaves,
            PorFormat.ClassMage => MageSaves,
            PorFormat.ClassThief => ThiefSaves,
            _ => FighterSaves,
        };
        int[] row = table[0].Row;
        foreach (var (from, values) in table) if (level >= from) row = values;
        return (int[])row.Clone();
    }

    /// <summary>The saving throws a character of these classes gets: the best (lowest) of each
    /// class's row, category by category — which is what the sample party's Fighter/Mage carries.</summary>
    public static int[] SavesFor(IReadOnlyList<int> singleClasses, IReadOnlyList<int> levels)
    {
        var best = new int[PorFormat.SavesLen];
        for (int i = 0; i < best.Length; i++) best[i] = int.MaxValue;
        for (int c = 0; c < singleClasses.Count; c++)
        {
            var row = Saves(singleClasses[c], levels[c]);
            for (int i = 0; i < best.Length; i++) best[i] = Math.Min(best[i], row[i]);
        }
        return best;
    }

    // Thief skills by level, in record order: pick pockets, open locks, find/remove traps, move
    // silently, hide in shadows, hear noise, climb walls, read languages. The AD&D 1e thief table;
    // racial and Dexterity adjustments are added on top (see RacialThiefAdjust/DexterityThiefAdjust).
    private static readonly int[][] ThiefSkillTable =
    {
        new[] { 30, 25, 20, 15, 10, 10, 85,  0 },   // level 1
        new[] { 35, 29, 25, 21, 15, 10, 86,  0 },
        new[] { 40, 33, 30, 27, 20, 15, 87,  0 },
        new[] { 45, 37, 35, 33, 25, 15, 88, 20 },
        new[] { 50, 42, 40, 40, 31, 20, 90, 25 },
        new[] { 55, 47, 45, 47, 37, 20, 92, 30 },
        new[] { 60, 52, 50, 55, 43, 25, 94, 35 },
        new[] { 65, 57, 55, 62, 49, 25, 96, 40 },
        new[] { 70, 62, 60, 70, 56, 30, 98, 45 },   // level 9, the thief's cap
    };

    /// <summary>The eight base thief-skill percentages at a level, before racial and Dexterity
    /// adjustments.</summary>
    public static int[] ThiefSkillBase(int level) =>
        (int[])ThiefSkillTable[Math.Clamp(level, 1, MaxLevel) - 1].Clone();

    /// <summary>A thief's eight skill percentages at a level, with the racial and Dexterity
    /// adjustments applied and clamped to a percentage the game can hold.</summary>
    public static int[] ThiefSkills(int level, int race, int dexterity)
    {
        var skills = ThiefSkillBase(level);
        int[] racial = RacialThiefAdjust(race);
        int[] dex = DexterityThiefAdjust(dexterity);
        for (int i = 0; i < skills.Length; i++)
            skills[i] = Math.Clamp(skills[i] + racial[i] + (i < dex.Length ? dex[i] : 0), 0, 95);
        return skills;
    }

    // Spells per day by level (the 1st, 2nd and 3rd-level slots), before the cleric's Wisdom bonus.
    //
    // Solved from the game's own saved characters rather than taken on trust — an earlier version of
    // this trainer carried the Rule Book table as printed in ClassRaceBook.LevelProgression, whose
    // cleric column was a level out and whose magic-user column was wrong at levels 5 and 6. Four
    // clerics at four different level/Wisdom combinations and three magic-users pin these rows
    // exactly (all are checked in test/FormatCheck):
    //
    //   Brother Sean  cleric 1, WIS 17 -> 3/0/0   = 1/0/0 + (2,0,0), the level-2 and 3 bonus spells
    //   Bakshi        cleric 1, WIS 17 -> 3/0/0     suppressed because a level-1 cleric can't cast them
    //   Dirten        cleric 5, WIS 16 -> 5/5/1   = 3/3/1 + (2,2,0)
    //   Alfred        cleric 6, WIS 18 -> 5/5/3   = 3/3/2 + (2,2,1)
    //   Darkstar      mage 1           -> 1/0/0   (a magic-user gets no bonus spells from Intelligence)
    //   Tarry, Carry  mage 6           -> 4/2/2
    private static readonly int[][] ClericSlotTable =
    {
        new[] { 1, 0, 0 }, new[] { 2, 0, 0 }, new[] { 2, 1, 0 },
        new[] { 3, 2, 0 }, new[] { 3, 3, 1 }, new[] { 3, 3, 2 },
    };

    private static readonly int[][] MageSlotTable =
    {
        new[] { 1, 0, 0 }, new[] { 2, 0, 0 }, new[] { 2, 1, 0 },
        new[] { 3, 2, 0 }, new[] { 4, 2, 1 }, new[] { 4, 2, 2 },
    };

    /// <summary>A cleric's spells a day at a level, including the Wisdom bonus — which is granted
    /// only for spell levels the cleric is already high enough to cast, so a level-1 cleric with
    /// Wisdom 18 gets its two extra first-level spells and nothing else.</summary>
    public static int[] ClericSlots(int level, int wisdom)
    {
        var slots = (int[])ClericSlotTable[Math.Clamp(level, 1, ClericSlotTable.Length) - 1].Clone();
        var bonus = WisdomBonusSpells(wisdom);
        int castable = MaxSpellLevel(level);
        for (int i = 0; i < slots.Length; i++)
            if (i < castable) slots[i] += bonus[i];
        return slots;
    }

    /// <summary>A magic-user's spells a day at a level.</summary>
    public static int[] MageSlots(int level) =>
        (int[])MageSlotTable[Math.Clamp(level, 1, MageSlotTable.Length) - 1].Clone();

    /// <summary>The highest spell level this caster can cast — 1 at levels 1-2, 2 at 3-4, 3 at 5+,
    /// which is what both slot tables above step at.</summary>
    public static int MaxSpellLevel(int casterLevel) => Math.Clamp((casterLevel + 1) / 2, 1, 3);

    /// <summary>The ability minimums the creation screen enforces for a class, as (stat index, minimum).</summary>
    public static (int Stat, int Min)[] Minimums(int singleClass) => singleClass switch
    {
        PorFormat.ClassFighter => new[] { (StatStr, 9), (StatCon, 7) },
        PorFormat.ClassCleric => new[] { (StatWis, 9) },
        PorFormat.ClassMage => new[] { (StatInt, 9) },
        PorFormat.ClassThief => new[] { (StatDex, 9) },
        _ => Array.Empty<(int, int)>(),
    };

    /// <summary>Ability indices into the record's six scores, for the tables above.</summary>
    public const int StatStr = 0, StatInt = 1, StatWis = 2, StatDex = 3, StatCon = 4, StatCha = 5;

    // --- level caps and race legality ----------------------------------------
    /// <summary>Where the game's own training halls stop: Fighter 8, Thief 9, Cleric 6, Mage 6.
    /// Beyond these the XP table in <see cref="ClassRaceBook.XpTable"/> has no next level.</summary>
    public static int TrainingCap(int singleClass) => singleClass switch
    {
        PorFormat.ClassFighter => 8,
        PorFormat.ClassThief => 9,
        PorFormat.ClassCleric => 6,
        PorFormat.ClassMage => 6,
        _ => 1,
    };

    // Racial level limits (AD&D 1e). A race missing from a row cannot take that class at all;
    // int.MaxValue means the race has no limit of its own and only the training cap applies.
    private static readonly Dictionary<int, Dictionary<int, int>> RacialCaps = new()
    {
        [PorFormat.RaceHuman] = new()
        {
            [PorFormat.ClassCleric] = int.MaxValue, [PorFormat.ClassFighter] = int.MaxValue,
            [PorFormat.ClassMage] = int.MaxValue, [PorFormat.ClassThief] = int.MaxValue,
        },
        [PorFormat.RaceElf] = new()
        {
            [PorFormat.ClassFighter] = 7, [PorFormat.ClassMage] = 11, [PorFormat.ClassThief] = int.MaxValue,
        },
        [PorFormat.RaceHalfElf] = new()
        {
            [PorFormat.ClassCleric] = 5, [PorFormat.ClassFighter] = 8,
            [PorFormat.ClassMage] = 8, [PorFormat.ClassThief] = int.MaxValue,
        },
        [PorFormat.RaceDwarf] = new()
        {
            [PorFormat.ClassFighter] = 9, [PorFormat.ClassThief] = int.MaxValue,
        },
        [PorFormat.RaceGnome] = new()
        {
            [PorFormat.ClassFighter] = 6, [PorFormat.ClassThief] = int.MaxValue,
        },
        [PorFormat.RaceHalfling] = new()
        {
            [PorFormat.ClassFighter] = 6, [PorFormat.ClassThief] = int.MaxValue,
        },
        // Not a race the creation screen offers — the engine simply has a value for it.
        [PorFormat.RaceHalfOrc] = new()
        {
            [PorFormat.ClassCleric] = 4, [PorFormat.ClassFighter] = 10, [PorFormat.ClassThief] = 8,
        },
    };

    /// <summary>True if this race may take this single class at all.</summary>
    public static bool CanTake(int race, int singleClass) =>
        RacialCaps.TryGetValue(race, out var caps) && caps.ContainsKey(singleClass);

    /// <summary>How far this race can take this class: the lower of its racial limit and the game's
    /// training cap. 0 if the race cannot take the class.</summary>
    public static int LevelCap(int race, int singleClass)
    {
        if (!RacialCaps.TryGetValue(race, out var caps) || !caps.TryGetValue(singleClass, out int racial))
            return 0;
        return Math.Min(racial, TrainingCap(singleClass));
    }

    /// <summary>The lowest cap across a multiclass — the level at which the whole combination stops.</summary>
    public static int LevelCapFor(int race, IReadOnlyList<int> singleClasses)
    {
        int cap = int.MaxValue;
        foreach (int cls in singleClasses) cap = Math.Min(cap, LevelCap(race, cls));
        return cap == int.MaxValue ? 0 : cap;
    }

    // The multiclass combinations each race may take, as class bytes. Humans are single-class only;
    // the half-elf has the widest menu (and is the only race that can be a Cleric/Fighter/Mage).
    private static readonly Dictionary<int, int[]> RaceMulticlass = new()
    {
        [PorFormat.RaceHuman] = Array.Empty<int>(),
        [PorFormat.RaceElf] = new[]
        {
            PorFormat.ClassFighterMage, PorFormat.ClassFighterThief,
            PorFormat.ClassMageThief, PorFormat.ClassFighterMageThief,
        },
        [PorFormat.RaceHalfElf] = new[]
        {
            PorFormat.ClassClericFighter, PorFormat.ClassClericMage, PorFormat.ClassClericFighterMage,
            PorFormat.ClassFighterMage, PorFormat.ClassFighterThief,
            PorFormat.ClassMageThief, PorFormat.ClassFighterMageThief,
        },
        [PorFormat.RaceDwarf] = new[] { PorFormat.ClassFighterThief },
        [PorFormat.RaceGnome] = new[] { PorFormat.ClassFighterThief },
        [PorFormat.RaceHalfling] = new[] { PorFormat.ClassFighterThief },
        [PorFormat.RaceHalfOrc] = new[]
        {
            PorFormat.ClassClericFighter, PorFormat.ClassClericThief, PorFormat.ClassFighterThief,
        },
    };

    /// <summary>Every class byte this race may hold, single and multi, in record order.</summary>
    public static IReadOnlyList<int> LegalClasses(int race)
    {
        var singles = BaseClasses.Where(c => CanTake(race, c));
        var multi = RaceMulticlass.TryGetValue(race, out var m) ? m : Array.Empty<int>();
        return singles.Concat(multi).Distinct().OrderBy(c => c).ToList();
    }

    /// <summary>True if this race may hold this exact class byte.</summary>
    public static bool IsLegal(int race, int classByte) => LegalClasses(race).Contains(classByte);

    // --- ability adjustments --------------------------------------------------
    /// <summary>Hit points per die from Constitution. Only warriors benefit above +2.</summary>
    public static int ConstitutionHpBonus(int con, bool warrior) => con switch
    {
        <= 3 => -2,
        <= 6 => -1,
        <= 14 => 0,
        15 => 1,
        16 => 2,
        17 => warrior ? 3 : 2,
        _ => warrior ? 4 : 2,
    };

    /// <summary>The Dexterity defensive adjustment subtracted from Armor Class. Confirmed outright
    /// by the sister game's item-less sample party, whose every AC is 10 minus this.</summary>
    public static int DexterityAcBonus(int dex) => dex switch
    {
        <= 3 => -4,
        4 => -3,
        5 => -2,
        6 => -1,
        <= 14 => 0,
        15 => 1,
        16 => 2,
        17 => 3,
        _ => 4,
    };

    /// <summary>The Strength bonus to hit, subtracted from THAC0 — including the exceptional-strength
    /// rows in <see cref="ClassRaceBook.ExceptionalStrength"/>. Measured on the sample party's
    /// Strength-17 fighter, whose current THAC0 is one better than his base.</summary>
    public static int StrengthToHitBonus(int str, int strPercent) => str switch
    {
        <= 3 => -3,
        <= 5 => -2,
        <= 7 => -1,
        <= 16 => 0,
        17 => 1,
        _ => strPercent switch { 0 or <= 50 => 1, <= 99 => 2, _ => 3 },
    };

    /// <summary>The extra cleric spells a day Wisdom grants, as 1st/2nd/3rd-level slots. Measured
    /// against four real clerics — see <see cref="ClericSlots"/>, which suppresses the rows for
    /// spell levels the cleric cannot yet cast. (Wisdom 18 also grants a 4th-level spell in the
    /// tabletop rules; no class reaches 4th-level spells in this game.)</summary>
    public static int[] WisdomBonusSpells(int wis) => wis switch
    {
        <= 12 => new[] { 0, 0, 0 },
        13 => new[] { 1, 0, 0 },
        14 => new[] { 2, 0, 0 },
        15 => new[] { 2, 1, 0 },
        16 => new[] { 2, 2, 0 },
        _ => new[] { 2, 2, 1 },
    };

    /// <summary>The race's ability adjustments, in record order — the ones the game's own race table
    /// documents (dwarves +1 Constitution, elves and halflings +1 Dexterity).</summary>
    public static int[] StatAdjust(int race) => race switch
    {
        PorFormat.RaceDwarf => new[] { 0, 0, 0, 0, 1, 0 },
        PorFormat.RaceElf => new[] { 0, 0, 0, 1, 0, 0 },
        PorFormat.RaceHalfling => new[] { 0, 0, 0, 1, 0, 0 },
        _ => new[] { 0, 0, 0, 0, 0, 0 },
    };

    /// <summary>The race's thief-skill adjustments, in record order.</summary>
    public static int[] RacialThiefAdjust(int race) => race switch
    {
        PorFormat.RaceDwarf => new[] { 0, 10, 15, 0, 0, 0, -10, -5 },
        PorFormat.RaceElf => new[] { 5, -5, 0, 5, 10, 5, 0, 0 },
        PorFormat.RaceGnome => new[] { 0, 5, 10, 5, 5, 10, -15, 0 },
        PorFormat.RaceHalfElf => new[] { 10, 0, 0, 0, 5, 0, 0, 0 },
        PorFormat.RaceHalfling => new[] { 5, 5, 5, 10, 15, 5, -15, -5 },
        PorFormat.RaceHalfOrc => new[] { -5, 5, 5, 0, 0, 5, 0, -10 },
        _ => new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
    };

    /// <summary>The Dexterity adjustment to the first five thief skills (pick pockets … hide in
    /// shadows); the last three take none.</summary>
    public static int[] DexterityThiefAdjust(int dex) => Math.Clamp(dex, 3, 18) switch
    {
        <= 9 => new[] { -15, -10, -10, -20, -10 },
        10 => new[] { -10, -5, -10, -15, -5 },
        11 => new[] { -5, 0, -5, -10, 0 },
        12 => new[] { 0, 0, 0, -5, 0 },
        <= 15 => new[] { 0, 0, 0, 0, 0 },
        16 => new[] { 0, 5, 0, 0, 0 },
        17 => new[] { 5, 10, 0, 5, 5 },
        _ => new[] { 10, 15, 5, 10, 10 },
    };

    /// <summary>A plausible starting age for the race — the sample party's dwarf is 52 and its elf
    /// 180, and the real saved fighter is a 19-year-old human.</summary>
    public static (int Min, int Max) StartingAge(int race) => race switch
    {
        PorFormat.RaceDwarf => (40, 60),
        PorFormat.RaceElf => (130, 190),
        PorFormat.RaceGnome => (65, 90),
        PorFormat.RaceHalfElf => (22, 35),
        PorFormat.RaceHalfling => (25, 40),
        _ => (18, 24),
    };

    /// <summary>The experience this class needs to reach a level, from the game's own XP table; 0 for
    /// level 1, and -1 if the class cannot reach that level at all.</summary>
    public static long XpForLevel(int singleClass, int level)
    {
        var row = ClassRaceBook.XpTable.FirstOrDefault(r => r.Level == level);
        if (row == null) return -1;
        int xp = singleClass switch
        {
            PorFormat.ClassCleric => row.Cleric,
            PorFormat.ClassFighter => row.Fighter,
            PorFormat.ClassMage => row.Mage,
            PorFormat.ClassThief => row.Thief,
            _ => 0,
        };
        return level == 1 ? 0 : xp == 0 ? -1 : xp;
    }

    /// <summary>The level this class's own XP table says a character with <paramref name="xp"/>
    /// experience has reached, capped at the training hall.</summary>
    public static int LevelForXp(int singleClass, long xp)
    {
        int level = 1;
        for (int l = 2; l <= TrainingCap(singleClass); l++)
        {
            long need = XpForLevel(singleClass, l);
            if (need < 0 || xp < need) break;
            level = l;
        }
        return level;
    }
}
