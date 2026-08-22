namespace CurseOfTheAzureBondsTrainer.Game;

/// <summary>
/// The class and race numbers Curse of the Azure Bonds runs on — AD&amp;D 1st edition as the game
/// implements it — keyed by the record's own class and race bytes. This is the single source of
/// truth for anything derived from "what class is this character, at what level": hit dice, THAC0,
/// saving throws, thief skills, spells per day, the ability minimums the creation screen enforces,
/// and which races may take which classes and how far.
///
/// <para>Curse of the Azure Bonds is the same Gold Box engine as Pool of Radiance but offers all
/// eight base classes (Cleric, Druid, Fighter, Paladin, Ranger, Magic-User, Thief, Monk) rather than
/// Pool's four, and reaches 5th-level spells rather than 3rd. The XP and spell-slot tables come from
/// the game's own Rule Book (<c>curseazure.pdf</c>); the THAC0 and saving-throw rows follow the
/// published AD&amp;D 1st-edition matrices. The sample party's level-5 characters pin several rows
/// exactly (see <c>test/FormatCheck</c>).</para>
/// </summary>
public static class ClassTables
{
    /// <summary>The highest level any class reaches in this game (the fighter's and thief's 12).</summary>
    public const int MaxLevel = 12;

    /// <summary>The eight base classes, as class bytes. These double as indices into the record's
    /// per-class level bytes (<see cref="CoabFormat.OffClassLevels"/>).</summary>
    public static readonly int[] BaseClasses =
    {
        CoabFormat.ClassCleric, CoabFormat.ClassDruid, CoabFormat.ClassFighter,
        CoabFormat.ClassPaladin, CoabFormat.ClassRanger, CoabFormat.ClassMage,
        CoabFormat.ClassThief, CoabFormat.ClassMonk,
    };

    /// <summary>The single classes a class byte combines — <c>{2}</c> for a fighter, <c>{2, 5}</c>
    /// for a Fighter/Mage. Only the combinations the game itself offers are listed.</summary>
    public static int[] SingleClassesOf(int classByte) => classByte switch
    {
        CoabFormat.ClassCleric => new[] { CoabFormat.ClassCleric },
        CoabFormat.ClassDruid => new[] { CoabFormat.ClassDruid },
        CoabFormat.ClassFighter => new[] { CoabFormat.ClassFighter },
        CoabFormat.ClassPaladin => new[] { CoabFormat.ClassPaladin },
        CoabFormat.ClassRanger => new[] { CoabFormat.ClassRanger },
        CoabFormat.ClassMage => new[] { CoabFormat.ClassMage },
        CoabFormat.ClassThief => new[] { CoabFormat.ClassThief },
        CoabFormat.ClassMonk => new[] { CoabFormat.ClassMonk },
        CoabFormat.ClassClericFighter => new[] { CoabFormat.ClassCleric, CoabFormat.ClassFighter },
        CoabFormat.ClassClericFighterMage => new[] { CoabFormat.ClassCleric, CoabFormat.ClassFighter, CoabFormat.ClassMage },
        CoabFormat.ClassClericRanger => new[] { CoabFormat.ClassCleric, CoabFormat.ClassRanger },
        CoabFormat.ClassClericMage => new[] { CoabFormat.ClassCleric, CoabFormat.ClassMage },
        CoabFormat.ClassClericThief => new[] { CoabFormat.ClassCleric, CoabFormat.ClassThief },
        CoabFormat.ClassFighterMage => new[] { CoabFormat.ClassFighter, CoabFormat.ClassMage },
        CoabFormat.ClassFighterThief => new[] { CoabFormat.ClassFighter, CoabFormat.ClassThief },
        CoabFormat.ClassFighterMageThief => new[] { CoabFormat.ClassFighter, CoabFormat.ClassMage, CoabFormat.ClassThief },
        CoabFormat.ClassMageThief => new[] { CoabFormat.ClassMage, CoabFormat.ClassThief },
        _ => Array.Empty<int>(),
    };

    /// <summary>True if this class byte is one a Curse character can hold.</summary>
    public static bool IsPlayableClass(int classByte) => SingleClassesOf(classByte).Length > 0;

    /// <summary>This class's bit in the record's class bitmask at <see cref="CoabFormat.OffClassMask"/>.
    /// The four Pool of Radiance classes use the same bits as that game; the four Curse-only classes
    /// use the upper nibble.</summary>
    public static int ClassBit(int singleClass) => singleClass switch
    {
        CoabFormat.ClassMage => 0x01,
        CoabFormat.ClassCleric => 0x02,
        CoabFormat.ClassThief => 0x04,
        CoabFormat.ClassFighter => 0x08,
        CoabFormat.ClassPaladin => 0x10,
        CoabFormat.ClassRanger => 0x20,
        CoabFormat.ClassDruid => 0x40,
        CoabFormat.ClassMonk => 0x80,
        _ => 0,
    };

    /// <summary>The class bitmask a character of these classes carries.</summary>
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
        Enumerable.Range(0, CoabFormat.Classes.Length).Where(IsPlayableClass).ToArray();

    // --- per-class numbers ----------------------------------------------------
    /// <summary>The class's hit die (fighter/paladin/ranger d10, cleric/ranger d8, thief/monk d6,
    /// druid/mage d4).</summary>
    public static int HitDie(int singleClass) => singleClass switch
    {
        CoabFormat.ClassFighter => 10,
        CoabFormat.ClassPaladin => 10,
        CoabFormat.ClassRanger => 8,
        CoabFormat.ClassCleric => 8,
        CoabFormat.ClassThief => 6,
        CoabFormat.ClassMonk => 6,
        CoabFormat.ClassDruid => 4,
        CoabFormat.ClassMage => 4,
        _ => 0,
    };

    /// <summary>
    /// The class's THAC0 at a level: the roll it needs to hit Armor Class 0, before the Strength
    /// bonus and equipment. The fighter's line is measured on the sample party — 20 at level 1 and
    /// 16 at level 5 — the rest follow the published attack matrices.
    /// </summary>
    public static int Thac0(int singleClass, int level)
    {
        level = Math.Clamp(level, 1, MaxLevel);
        return singleClass switch
        {
            CoabFormat.ClassFighter => Math.Max(1, 21 - level),
            CoabFormat.ClassPaladin => Math.Max(1, 21 - level),
            CoabFormat.ClassRanger => Math.Max(1, 21 - level),
            CoabFormat.ClassCleric => level <= 3 ? 20 : level <= 6 ? 18 : level <= 9 ? 16 : 14,
            CoabFormat.ClassDruid => level <= 3 ? 20 : level <= 6 ? 18 : level <= 9 ? 16 : 14,
            CoabFormat.ClassThief => level <= 4 ? 21 : level <= 8 ? 19 : level <= 12 ? 17 : 15,
            CoabFormat.ClassMonk => level <= 4 ? 21 : level <= 8 ? 19 : level <= 12 ? 17 : 15,
            CoabFormat.ClassMage => level <= 5 ? 21 : level <= 10 ? 19 : 17,
            _ => 20,
        };
    }

    // Saving throws, in record order: paralyzation/poison/death, petrification/polymorph,
    // rod/staff/wand, breath weapon, spell. Each row covers the level band that starts at its key.
    private static readonly (int From, int[] Row)[] FighterSaves =
    {
        (1, new[] { 14, 15, 16, 17, 17 }),
        (3, new[] { 13, 14, 15, 16, 16 }),
        (5, new[] { 11, 12, 13, 13, 14 }),
        (7, new[] { 10, 11, 12, 12, 13 }),
        (9, new[] { 8, 9, 10, 9, 11 }),
        (11, new[] { 7, 8, 9, 8, 10 }),
    };

    private static readonly (int From, int[] Row)[] ClericSaves =
    {
        (1, new[] { 10, 13, 14, 16, 15 }),
        (4, new[] { 9, 12, 13, 15, 14 }),
        (7, new[] { 7, 10, 11, 13, 12 }),
        (10, new[] { 6, 9, 10, 12, 11 }),
    };

    private static readonly (int From, int[] Row)[] MageSaves =
    {
        (1, new[] { 14, 13, 11, 15, 12 }),
        (6, new[] { 13, 11, 9, 13, 10 }),
        (11, new[] { 12, 10, 8, 12, 9 }),
    };

    private static readonly (int From, int[] Row)[] ThiefSaves =
    {
        (1, new[] { 13, 12, 14, 16, 15 }),
        (5, new[] { 12, 11, 12, 15, 13 }),
        (9, new[] { 11, 10, 10, 14, 11 }),
        (13, new[] { 10, 9, 9, 13, 10 }),
    };

    // Paladin and Ranger save as fighters.
    private static (int From, int[] Row)[] PaladinSaves => FighterSaves;
    private static (int From, int[] Row)[] RangerSaves => FighterSaves;
    // Druid saves as cleric.
    private static (int From, int[] Row)[] DruidSaves => ClericSaves;
    // Monk saves as thief.
    private static (int From, int[] Row)[] MonkSaves => ThiefSaves;

    /// <summary>The class's five saving throws at a level. A copy, so a caller can't edit the table.</summary>
    public static int[] Saves(int singleClass, int level)
    {
        level = Math.Clamp(level, 1, MaxLevel);
        var table = singleClass switch
        {
            CoabFormat.ClassFighter => FighterSaves,
            CoabFormat.ClassPaladin => PaladinSaves,
            CoabFormat.ClassRanger => RangerSaves,
            CoabFormat.ClassCleric => ClericSaves,
            CoabFormat.ClassDruid => DruidSaves,
            CoabFormat.ClassThief => ThiefSaves,
            CoabFormat.ClassMonk => MonkSaves,
            CoabFormat.ClassMage => MageSaves,
            _ => FighterSaves,
        };
        int[] row = table[0].Row;
        foreach (var (from, values) in table) if (level >= from) row = values;
        return (int[])row.Clone();
    }

    /// <summary>The saving throws a character of these classes gets: the best (lowest) of each
    /// class's row, category by category.</summary>
    public static int[] SavesFor(IReadOnlyList<int> singleClasses, IReadOnlyList<int> levels)
    {
        var best = new int[CoabFormat.SavesLen];
        for (int i = 0; i < best.Length; i++) best[i] = int.MaxValue;
        for (int c = 0; c < singleClasses.Count; c++)
        {
            var row = Saves(singleClasses[c], levels[c]);
            for (int i = 0; i < best.Length; i++) best[i] = Math.Min(best[i], row[i]);
        }
        return best;
    }

    // Thief skills by level, in record order: pick pockets, open locks, find/remove traps, move
    // silently, hide in shadows, hear noise, climb walls, read languages.
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
        new[] { 70, 62, 60, 70, 56, 30, 98, 45 },
        new[] { 72, 64, 63, 75, 60, 32, 99, 48 },
        new[] { 74, 66, 66, 80, 64, 35, 99, 52 },
        new[] { 76, 68, 69, 85, 68, 38, 99, 55 },   // level 12, the thief's cap
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

    // Spells per day by level (spell levels 1-5), before the cleric's Wisdom bonus.
    // From the Rule Book's class tables; clerics add the Wisdom bonus on top.
    private static readonly int[][] ClericSlotTable =
    {
        new[] { 1, 0, 0, 0, 0 }, new[] { 2, 0, 0, 0, 0 }, new[] { 2, 1, 0, 0, 0 },
        new[] { 3, 2, 0, 0, 0 }, new[] { 3, 3, 1, 0, 0 }, new[] { 3, 3, 2, 0, 0 },
        new[] { 3, 3, 2, 1, 0 }, new[] { 3, 3, 3, 2, 0 }, new[] { 4, 4, 3, 2, 1 },
        new[] { 4, 4, 3, 3, 2 }, new[] { 4, 4, 3, 3, 2 }, new[] { 4, 4, 3, 3, 2 },
    };

    private static readonly int[][] MageSlotTable =
    {
        new[] { 1, 0, 0, 0, 0 }, new[] { 2, 0, 0, 0, 0 }, new[] { 2, 1, 0, 0, 0 },
        new[] { 3, 2, 0, 0, 0 }, new[] { 4, 2, 1, 0, 0 }, new[] { 4, 2, 2, 0, 0 },
        new[] { 4, 3, 2, 1, 0 }, new[] { 4, 3, 3, 2, 0 }, new[] { 4, 4, 3, 2, 1 },
        new[] { 4, 4, 4, 2, 2 }, new[] { 4, 4, 4, 3, 3 }, new[] { 4, 4, 4, 3, 3 },
    };

    // Druids get their own spell progression (from the Rule Book).
    private static readonly int[][] DruidSlotTable =
    {
        new[] { 1, 0, 0, 0, 0 }, new[] { 2, 0, 0, 0, 0 }, new[] { 2, 1, 0, 0, 0 },
        new[] { 3, 2, 0, 0, 0 }, new[] { 3, 3, 1, 0, 0 }, new[] { 3, 3, 2, 0, 0 },
        new[] { 3, 3, 3, 1, 0 }, new[] { 3, 3, 3, 2, 0 }, new[] { 4, 4, 3, 2, 1 },
        new[] { 4, 4, 4, 2, 2 }, new[] { 4, 4, 4, 3, 3 }, new[] { 4, 4, 4, 3, 3 },
    };

    /// <summary>A cleric's spells a day at a level, including the Wisdom bonus — which is granted
    /// only for spell levels the cleric is already high enough to cast.</summary>
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

    /// <summary>A druid's spells a day at a level.</summary>
    public static int[] DruidSlots(int level) =>
        (int[])DruidSlotTable[Math.Clamp(level, 1, DruidSlotTable.Length) - 1].Clone();

    /// <summary>The highest spell level this caster can cast — 1 at levels 1-2, 2 at 3-4, 3 at 5-6,
    /// 4 at 7-8, 5 at 9+.</summary>
    public static int MaxSpellLevel(int casterLevel) => Math.Clamp((casterLevel + 1) / 2, 1, 5);

    /// <summary>The ability minimums the creation screen enforces for a class, as (stat index, minimum).</summary>
    public static (int Stat, int Min)[] Minimums(int singleClass) => singleClass switch
    {
        CoabFormat.ClassFighter => new[] { (StatStr, 9), (StatCon, 7) },
        CoabFormat.ClassPaladin => new[] { (StatStr, 12), (StatCon, 9), (StatWis, 13), (StatCha, 17) },
        CoabFormat.ClassRanger => new[] { (StatStr, 13), (StatDex, 14), (StatCon, 14), (StatWis, 14) },
        CoabFormat.ClassCleric => new[] { (StatWis, 9) },
        CoabFormat.ClassDruid => new[] { (StatWis, 12), (StatCha, 15) },
        CoabFormat.ClassMage => new[] { (StatInt, 9) },
        CoabFormat.ClassThief => new[] { (StatDex, 9) },
        CoabFormat.ClassMonk => new[] { (StatStr, 9), (StatWis, 9), (StatDex, 9) },
        _ => Array.Empty<(int, int)>(),
    };

    /// <summary>Ability indices into the record's six scores, for the tables above.</summary>
    public const int StatStr = 0, StatInt = 1, StatWis = 2, StatDex = 3, StatCon = 4, StatCha = 5;

    // --- level caps and race legality ----------------------------------------
    /// <summary>Where the game's own training halls stop, from the Rule Book's class caps.</summary>
    public static int TrainingCap(int singleClass) => singleClass switch
    {
        CoabFormat.ClassFighter => 12,
        CoabFormat.ClassPaladin => 11,
        CoabFormat.ClassRanger => 11,
        CoabFormat.ClassCleric => 10,
        CoabFormat.ClassDruid => 11,
        CoabFormat.ClassMage => 11,
        CoabFormat.ClassThief => 12,
        CoabFormat.ClassMonk => 12,
        _ => 1,
    };

    // Racial level limits (AD&D 1e as Curse implements them). A race missing from a row cannot take
    // that class at all; int.MaxValue means the race has no limit of its own and only the training
    // cap applies. From the Rule Book's racial level limit table.
    private static readonly Dictionary<int, Dictionary<int, int>> RacialCaps = new()
    {
        [CoabFormat.RaceHuman] = new()
        {
            [CoabFormat.ClassCleric] = int.MaxValue, [CoabFormat.ClassFighter] = int.MaxValue,
            [CoabFormat.ClassPaladin] = int.MaxValue, [CoabFormat.ClassRanger] = int.MaxValue,
            [CoabFormat.ClassMage] = int.MaxValue, [CoabFormat.ClassThief] = int.MaxValue,
            [CoabFormat.ClassMonk] = int.MaxValue, [CoabFormat.ClassDruid] = int.MaxValue,
        },
        [CoabFormat.RaceElf] = new()
        {
            [CoabFormat.ClassFighter] = 7, [CoabFormat.ClassMage] = 11,
            [CoabFormat.ClassThief] = int.MaxValue,
        },
        [CoabFormat.RaceHalfElf] = new()
        {
            [CoabFormat.ClassCleric] = 5, [CoabFormat.ClassFighter] = 8,
            [CoabFormat.ClassRanger] = 8, [CoabFormat.ClassMage] = 8,
            [CoabFormat.ClassThief] = int.MaxValue, [CoabFormat.ClassDruid] = int.MaxValue,
        },
        [CoabFormat.RaceDwarf] = new()
        {
            [CoabFormat.ClassFighter] = 9, [CoabFormat.ClassThief] = int.MaxValue,
        },
        [CoabFormat.RaceGnome] = new()
        {
            [CoabFormat.ClassFighter] = 6, [CoabFormat.ClassThief] = int.MaxValue,
        },
        [CoabFormat.RaceHalfling] = new()
        {
            [CoabFormat.ClassFighter] = 6, [CoabFormat.ClassThief] = int.MaxValue,
        },
        [CoabFormat.RaceHalfOrc] = new()
        {
            [CoabFormat.ClassCleric] = 4, [CoabFormat.ClassFighter] = 10,
            [CoabFormat.ClassThief] = 8,
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

    // The multiclass combinations each race may take, as class bytes. Humans are single-class only.
    private static readonly Dictionary<int, int[]> RaceMulticlass = new()
    {
        [CoabFormat.RaceHuman] = Array.Empty<int>(),
        [CoabFormat.RaceElf] = new[]
        {
            CoabFormat.ClassFighterMage, CoabFormat.ClassFighterThief,
            CoabFormat.ClassMageThief, CoabFormat.ClassFighterMageThief,
        },
        [CoabFormat.RaceHalfElf] = new[]
        {
            CoabFormat.ClassClericFighter, CoabFormat.ClassClericMage, CoabFormat.ClassClericFighterMage,
            CoabFormat.ClassFighterMage, CoabFormat.ClassFighterThief,
            CoabFormat.ClassMageThief, CoabFormat.ClassFighterMageThief, CoabFormat.ClassClericRanger,
            CoabFormat.ClassClericThief,
        },
        [CoabFormat.RaceDwarf] = new[] { CoabFormat.ClassFighterThief },
        [CoabFormat.RaceGnome] = new[] { CoabFormat.ClassFighterThief },
        [CoabFormat.RaceHalfling] = new[] { CoabFormat.ClassFighterThief },
        [CoabFormat.RaceHalfOrc] = new[]
        {
            CoabFormat.ClassClericFighter, CoabFormat.ClassClericThief, CoabFormat.ClassFighterThief,
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

    /// <summary>The Dexterity defensive adjustment subtracted from Armor Class.</summary>
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

    /// <summary>The Strength bonus to hit, subtracted from THAC0 — including exceptional-strength
    /// rows.</summary>
    public static int StrengthToHitBonus(int str, int strPercent) => str switch
    {
        <= 3 => -3,
        <= 5 => -2,
        <= 7 => -1,
        <= 16 => 0,
        17 => 1,
        _ => strPercent switch { 0 or <= 50 => 1, <= 99 => 2, _ => 3 },
    };

    /// <summary>The extra cleric spells a day Wisdom grants, as 1st-5th-level slots. Only granted
    /// for spell levels the cleric can already cast.</summary>
    public static int[] WisdomBonusSpells(int wis) => wis switch
    {
        <= 12 => new[] { 0, 0, 0, 0, 0 },
        13 => new[] { 1, 0, 0, 0, 0 },
        14 => new[] { 2, 0, 0, 0, 0 },
        15 => new[] { 2, 1, 0, 0, 0 },
        16 => new[] { 2, 2, 0, 0, 0 },
        17 => new[] { 2, 2, 1, 0, 0 },
        _ => new[] { 2, 2, 1, 1, 0 },
    };

    /// <summary>The race's ability adjustments, in record order.</summary>
    public static int[] StatAdjust(int race) => race switch
    {
        CoabFormat.RaceDwarf => new[] { 0, 0, 0, 0, 1, 0 },
        CoabFormat.RaceElf => new[] { 0, 0, 0, 1, 0, 0 },
        CoabFormat.RaceHalfling => new[] { 0, 0, 0, 1, 0, 0 },
        _ => new[] { 0, 0, 0, 0, 0, 0 },
    };

    /// <summary>The race's thief-skill adjustments, in record order.</summary>
    public static int[] RacialThiefAdjust(int race) => race switch
    {
        CoabFormat.RaceDwarf => new[] { 0, 10, 15, 0, 0, 0, -10, -5 },
        CoabFormat.RaceElf => new[] { 5, -5, 0, 5, 10, 5, 0, 0 },
        CoabFormat.RaceGnome => new[] { 0, 5, 10, 5, 5, 10, -15, 0 },
        CoabFormat.RaceHalfElf => new[] { 10, 0, 0, 0, 5, 0, 0, 0 },
        CoabFormat.RaceHalfling => new[] { 5, 5, 5, 10, 15, 5, -15, -5 },
        CoabFormat.RaceHalfOrc => new[] { -5, 5, 5, 0, 0, 5, 0, -10 },
        _ => new[] { 0, 0, 0, 0, 0, 0, 0, 0 },
    };

    /// <summary>The Dexterity adjustment to the first five thief skills.</summary>
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

    /// <summary>A plausible starting age for the race.</summary>
    public static (int Min, int Max) StartingAge(int race) => race switch
    {
        CoabFormat.RaceDwarf => (40, 60),
        CoabFormat.RaceElf => (130, 190),
        CoabFormat.RaceGnome => (65, 90),
        CoabFormat.RaceHalfElf => (22, 35),
        CoabFormat.RaceHalfling => (25, 40),
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
            CoabFormat.ClassCleric => row.Cleric,
            CoabFormat.ClassFighter => row.Fighter,
            CoabFormat.ClassPaladin => row.Paladin,
            CoabFormat.ClassRanger => row.Ranger,
            CoabFormat.ClassMage => row.Mage,
            CoabFormat.ClassThief => row.Thief,
            // Druids and Monks are not in the Rule Book's XP table directly; druids use the cleric
            // progression and monks use the thief progression (both confirmed by the game's own
            // class table notes).
            CoabFormat.ClassDruid => row.Cleric,
            CoabFormat.ClassMonk => row.Thief,
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
