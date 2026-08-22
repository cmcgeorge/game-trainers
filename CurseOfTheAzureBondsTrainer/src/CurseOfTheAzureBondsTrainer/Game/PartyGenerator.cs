namespace CurseOfTheAzureBondsTrainer.Game;

/// <summary>How a generated character's six ability scores are rolled.</summary>
public enum RollStyle
{
    /// <summary>Three six-sided dice per ability — what the game's own create screen rolls.</summary>
    ThreeD6,

    /// <summary>Four dice keeping the best three — the usual house rule for a party meant to finish
    /// the game. Averages 12.2 per ability, and never rolls a 3.</summary>
    FourD6DropLowest,
}

/// <summary>
/// One generated level-1 character: everything the 422-byte record needs to describe a playable
/// party member, with no reference to where that record lives. <see cref="StampOnto"/> writes it
/// into a <see cref="CharacterRecord"/>; <see cref="WrittenRanges"/> lists exactly which byte
/// ranges that touches, so a live edit can poke those and nothing else.
/// </summary>
public sealed class RolledCharacter
{
    public required string Name { get; init; }
    /// <summary>The party job this character was rolled for.</summary>
    public required string Role { get; init; }

    public required int Race { get; init; }
    public required int Class { get; init; }
    public required int Alignment { get; init; }
    public required int Gender { get; init; }
    public required int Age { get; init; }

    /// <summary>The six ability scores in record order (STR, INT, WIS, DEX, CON, CHA).</summary>
    public required int[] Stats { get; init; }
    public required int StrengthPercent { get; init; }

    public required int Level { get; init; }
    public required int Movement { get; init; }
    public required int HpMax { get; init; }
    /// <summary>The hit die total before the Constitution bonus.</summary>
    public required int HpRolled { get; init; }

    public required int Thac0Base { get; init; }
    /// <summary>Effective THAC0: the base less the Strength bonus to hit.</summary>
    public required int Thac0 { get; init; }
    public required int ArmorClassBase { get; init; }
    /// <summary>Effective AC: unarmored 10 less the Dexterity adjustment.</summary>
    public required int ArmorClass { get; init; }

    public required int[] Saves { get; init; }
    /// <summary>The eight thief-skill percentages; all zero for a character with no thief level.</summary>
    public required int[] ThiefSkills { get; init; }
    /// <summary>The eight per-class level bytes: 1 in each class this character has, 0 elsewhere.</summary>
    public required int[] ClassLevels { get; init; }
    public required int[] ClericSlots { get; init; }
    public required int[] MageSlots { get; init; }
    /// <summary>One flag per spell, in <see cref="SpellBook.InRecordOrder"/> order.</summary>
    public required bool[] KnownSpells { get; init; }

    /// <summary>The single-class bytes this character combines.</summary>
    public required int[] SingleClasses { get; init; }

    // --- display ------------------------------------------------------------
    public string Title => $"{Name}  —  {CoabFormat.GenderName(Gender)} {CoabFormat.RaceName(Race)} {CoabFormat.ClassName(Class)}";

    public string StatsText
    {
        get
        {
            var parts = new List<string>(CoabFormat.StatCount);
            for (int i = 0; i < CoabFormat.StatCount; i++)
            {
                string v = i == 0 && Stats[0] == 18 && StrengthPercent > 0
                    ? $"18/{(StrengthPercent >= 100 ? "00" : StrengthPercent.ToString("D2"))}"
                    : Stats[i].ToString();
                parts.Add($"{CoabFormat.StatsShort[i]} {v}");
            }
            return string.Join(" · ", parts);
        }
    }

    public string CombatText =>
        $"HP {HpMax} · AC {ArmorClass} · THAC0 {Thac0} · saves {string.Join("/", Saves)} · " +
        $"{CoabFormat.AlignmentName(Alignment)} · age {Age}";

    /// <summary>The spells this character starts knowing — null for a non-caster.</summary>
    public string? SpellsText
    {
        get
        {
            var names = new List<string>();
            for (int i = 0; i < KnownSpells.Length && i < SpellBook.InRecordOrder.Count; i++)
                if (KnownSpells[i]) names.Add(SpellBook.InRecordOrder[i].Name);
            if (names.Count == 0) return null;
            string slots = "";
            if (ClericSlots.Sum() > 0) slots += $"; {ClericSlots[0]} cleric spell{(ClericSlots[0] == 1 ? "" : "s")}/day";
            if (MageSlots.Sum() > 0) slots += $"; {MageSlots[0]} mage spell{(MageSlots[0] == 1 ? "" : "s")}/day";
            return "Knows " + string.Join(", ", names) + slots + ".";
        }
    }

    public override string ToString() => Title;

    // --- writing into a record ----------------------------------------------
    /// <summary>
    /// The record byte ranges <see cref="StampOnto"/> writes, in ascending order. Everything else
    /// in the 422 bytes is deliberately left alone: the money counters, the carried-item count and
    /// list pointer, the equipped-item pointers, the effects pointer, encumbrance, the party
    /// linked-list pointer and the combat-icon bytes all belong to the record's current owner or to
    /// the running game, and a generated character inherits them rather than corrupting them.
    /// </summary>
    public static readonly (int Offset, int Length)[] WrittenRanges =
    {
        (CoabFormat.OffNameLength, 1),                                       // name length
        (CoabFormat.OffName, CoabFormat.NameMaxLength),                      // name
        (CoabFormat.OffStats, CoabFormat.StatCount * CoabFormat.StatStride + 2), // six pairs + STR% pair
        (CoabFormat.OffMemorizedSpells, CoabFormat.MemorizedSpellsLen),
        (CoabFormat.OffThac0Base, 1),
        (CoabFormat.OffRace, 2),                                             // race + class
        (CoabFormat.OffAge, 2),
        (CoabFormat.OffHpMax, 1),
        (CoabFormat.OffKnownSpells, CoabFormat.KnownSpellsLen),
        (CoabFormat.OffAttackLevel, 1),
        (CoabFormat.OffSaves, CoabFormat.SavesLen),
        (CoabFormat.OffMovementBase, 13),                                    // move, level, drained, undead, thief skills
        (CoabFormat.OffClassLevels, CoabFormat.ClassLevelCount),
        (CoabFormat.OffGender, 1),
        (CoabFormat.OffAlignment, 1),
        (CoabFormat.OffAcBase, 1),
        (CoabFormat.OffExperience, 4),
        (CoabFormat.OffClassMask, 1),
        (CoabFormat.OffHpRolled, 1),
        (CoabFormat.OffClericSlots, CoabFormat.SpellSlotLevels),
        (CoabFormat.OffMageSlots, CoabFormat.SpellSlotLevels),
        (CoabFormat.OffStatus, 1),
        (CoabFormat.OffThac0Cur, 1),
        (CoabFormat.OffAcCur, 1),
        (CoabFormat.OffHpCur, 1),
        (CoabFormat.OffMovementCur, 1),
    };

    /// <summary>
    /// Writes this character over <paramref name="record"/>, touching only
    /// <see cref="WrittenRanges"/>. The record keeps its money, its items and its place in the
    /// game's own linked lists, so the result is the same character sheet with a new person on it.
    /// </summary>
    public void StampOnto(CharacterRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        record.Name = Name;
        for (int i = 0; i < CoabFormat.StatCount; i++) record.SetStat(i, Stats[i]);
        record.StrengthPercent = StrengthPercent;

        Array.Clear(record.Bytes, CoabFormat.OffMemorizedSpells, CoabFormat.MemorizedSpellsLen);

        record.Thac0Base = Thac0Base;
        record.Race = Race;
        record.Class = Class;
        record.Age = Age;
        record.HpMax = HpMax;

        for (int i = 0; i < CoabFormat.KnownSpellsLen; i++)
            record.Bytes[CoabFormat.OffKnownSpells + i] = i < KnownSpells.Length && KnownSpells[i] ? (byte)1 : (byte)0;

        record.Bytes[CoabFormat.OffAttackLevel] = (byte)Level;
        for (int i = 0; i < CoabFormat.SavesLen; i++) record.SetSave(i, Saves[i]);

        record.Bytes[CoabFormat.OffMovementBase] = (byte)Movement;
        record.Bytes[CoabFormat.OffLevelHighest] = (byte)Level;
        record.Bytes[CoabFormat.OffDrainedLevels] = 0;
        record.Bytes[CoabFormat.OffDrainedHp] = 0;
        record.Bytes[CoabFormat.OffUndeadLevel] = 0;
        for (int i = 0; i < CoabFormat.ThiefSkillsLen; i++) record.SetThiefSkill(i, ThiefSkills[i]);

        for (int i = 0; i < CoabFormat.ClassLevelCount; i++) record.SetClassLevel(i, ClassLevels[i]);
        record.Gender = Gender;
        record.Alignment = Alignment;
        record.ArmorClassBase = ArmorClassBase;
        record.Experience = 0;
        record.Bytes[CoabFormat.OffClassMask] = (byte)ClassTables.ClassMask(SingleClasses);
        record.HpRolled = HpRolled;
        for (int i = 0; i < CoabFormat.SpellSlotLevels; i++)
        {
            record.Bytes[CoabFormat.OffClericSlots + i] = (byte)ClericSlots[i];
            record.Bytes[CoabFormat.OffMageSlots + i] = (byte)MageSlots[i];
        }

        record.Status = 0;
        record.Thac0 = Thac0;
        record.ArmorClass = ArmorClass;
        record.HpCurrent = HpMax;
        record.Bytes[CoabFormat.OffMovementCur] = (byte)Movement;
    }
}

/// <summary>
/// Rolls a ready-to-play Curse of the Azure Bonds party: good-aligned level-1 characters in
/// race/class combinations the game itself allows, with abilities dealt to the class that needs them
/// and every derived number (hit points, AC, THAC0, saving throws, thief skills, spells) filled in
/// to match.
///
/// <para>The composition follows the party the game's own Rule Book recommends — front-line
/// fighters, a healer, a scout, a magic-user — and takes advantage of Curse's wider class selection
/// (Paladin, Ranger) to offer more varied parties than the sister Pool of Radiance generator.</para>
/// </summary>
public static class PartyGenerator
{
    /// <summary>The most characters a Curse party can hold.</summary>
    public const int MaxParty = 6;

    /// <summary>Generated characters are level 1 — the game's training halls do the rest.</summary>
    public const int StartingLevel = 1;

    /// <summary>Unencumbered movement, in squares.</summary>
    public const int BaseMovement = 12;

    /// <summary>The unarmored Armor Class the record's "base" AC byte holds.</summary>
    public const int UnarmoredAc = 10;

    // --- generation ----------------------------------------------------------

    /// <summary>
    /// Rolls a party of <paramref name="count"/> characters (1..<see cref="MaxParty"/>), in
    /// marching order — melee at the front, the magic-user at the back.
    /// </summary>
    public static IReadOnlyList<RolledCharacter> Generate(Random rng, int count = MaxParty,
                                                          RollStyle style = RollStyle.FourD6DropLowest)
    {
        ArgumentNullException.ThrowIfNull(rng);
        count = Math.Clamp(count, 1, MaxParty);

        var slots = Roster.OrderBy(s => s.Priority).Take(count).OrderBy(s => s.March).ToList();

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var party = new List<RolledCharacter>(slots.Count);
        foreach (var slot in slots)
        {
            var pick = slot.Picks[rng.Next(slot.Picks.Length)];
            party.Add(Roll(rng, pick, slot.Role, style, usedNames));
        }
        return party;
    }

    private static RolledCharacter Roll(Random rng, Pick pick, string role, RollStyle style, HashSet<string> usedNames)
    {
        var classes = pick.Classes;
        int gender = rng.Next(2);

        var rolls = new int[CoabFormat.StatCount];
        for (int i = 0; i < rolls.Length; i++) rolls[i] = RollAbility(rng, style);
        Array.Sort(rolls);
        Array.Reverse(rolls);

        var stats = new int[CoabFormat.StatCount];
        var order = StatPriority(classes);
        for (int i = 0; i < order.Length; i++) stats[order[i]] = rolls[i];

        int[] racialAdjust = ClassTables.StatAdjust(pick.Race);
        for (int i = 0; i < stats.Length; i++)
            stats[i] = Math.Clamp(stats[i] + racialAdjust[i], 3, 18);
        foreach (int cls in classes)
            foreach (var (stat, min) in ClassTables.Minimums(cls))
                if (stats[stat] < min) stats[stat] = min;

        int strPercent = 0;
        bool isWarriorClass = classes.Contains(CoabFormat.ClassFighter) ||
                              classes.Contains(CoabFormat.ClassPaladin) ||
                              classes.Contains(CoabFormat.ClassRanger);
        if (stats[StatStr] == 18 && isWarriorClass)
            strPercent = gender == 0 ? rng.Next(1, 101) : rng.Next(1, 51);

        int dice = classes.Sum(ClassTables.HitDie);
        int rolled = Math.Max(1, dice / classes.Length);
        bool warrior = classes.Contains(CoabFormat.ClassFighter) ||
                       classes.Contains(CoabFormat.ClassPaladin) ||
                       classes.Contains(CoabFormat.ClassRanger);
        int hp = Math.Max(1, rolled + ClassTables.ConstitutionHpBonus(stats[StatCon], warrior));

        int thac0Base = classes.Min(c => ClassTables.Thac0(c, StartingLevel));
        int ac = UnarmoredAc - ClassTables.DexterityAcBonus(stats[StatDex]);

        var levels = classes.Select(_ => StartingLevel).ToArray();
        var saves = ClassTables.SavesFor(classes, levels);

        var classLevels = new int[CoabFormat.ClassLevelCount];
        foreach (int cls in classes) classLevels[cls] = StartingLevel;

        return new RolledCharacter
        {
            Name = PickName(rng, gender, usedNames),
            Role = role,
            Race = pick.Race,
            Class = pick.ClassByte,
            Alignment = PickAlignment(rng, classes),
            Gender = gender,
            Age = RollAge(rng, pick.Race),
            Stats = stats,
            StrengthPercent = strPercent,
            Level = StartingLevel,
            Movement = BaseMovement,
            HpMax = hp,
            HpRolled = rolled,
            Thac0Base = thac0Base,
            Thac0 = thac0Base - ClassTables.StrengthToHitBonus(stats[StatStr], strPercent),
            ArmorClassBase = UnarmoredAc,
            ArmorClass = ac,
            Saves = saves,
            ThiefSkills = classes.Contains(CoabFormat.ClassThief)
                ? ClassTables.ThiefSkills(StartingLevel, pick.Race, stats[StatDex])
                : new int[CoabFormat.ThiefSkillsLen],
            ClassLevels = classLevels,
            ClericSlots = classes.Contains(CoabFormat.ClassCleric)
                ? ClassTables.ClericSlots(StartingLevel, stats[StatWis])
                : new int[CoabFormat.SpellSlotLevels],
            MageSlots = classes.Contains(CoabFormat.ClassMage)
                ? ClassTables.MageSlots(StartingLevel)
                : new int[CoabFormat.SpellSlotLevels],
            KnownSpells = RollKnownSpells(rng, classes),
            SingleClasses = (int[])classes.Clone(),
        };
    }

    // --- the roster ----------------------------------------------------------
    private const int StatStr = ClassTables.StatStr, StatInt = ClassTables.StatInt, StatWis = ClassTables.StatWis,
                      StatDex = ClassTables.StatDex, StatCon = ClassTables.StatCon, StatCha = ClassTables.StatCha;

    private readonly record struct Pick(int Race, int ClassByte, int[] Classes);

    private sealed record Slot(int March, int Priority, string Role, Pick[] Picks);

    private static Pick P(int race, int classByte) => new(race, classByte, ClassTables.SingleClassesOf(classByte));

    /// <summary>
    /// The party the Rule Book recommends: two front-line fighters, two clerics, a thief and a
    /// magic-user. Curse's wider class menu adds Paladin and Ranger picks to the front line.
    /// </summary>
    private static readonly Slot[] Roster =
    {
        new(0, 1, "Front-line fighter", new[]
        {
            P(CoabFormat.RaceHuman, CoabFormat.ClassFighter),
            P(CoabFormat.RaceDwarf, CoabFormat.ClassFighter),
            P(CoabFormat.RaceHuman, CoabFormat.ClassPaladin),
            P(CoabFormat.RaceHuman, CoabFormat.ClassRanger),
        }),
        new(1, 5, "Second front-liner", new[]
        {
            P(CoabFormat.RaceDwarf, CoabFormat.ClassFighter),
            P(CoabFormat.RaceHuman, CoabFormat.ClassFighter),
            P(CoabFormat.RaceDwarf, CoabFormat.ClassFighterThief),
            P(CoabFormat.RaceHalfElf, CoabFormat.ClassClericFighter),
        }),
        new(2, 2, "Healer", new[]
        {
            P(CoabFormat.RaceHuman, CoabFormat.ClassCleric),
            P(CoabFormat.RaceHalfElf, CoabFormat.ClassCleric),
            P(CoabFormat.RaceHalfElf, CoabFormat.ClassClericFighter),
        }),
        new(3, 4, "Scout / trap-finder", new[]
        {
            P(CoabFormat.RaceHuman, CoabFormat.ClassThief),
            P(CoabFormat.RaceHalfling, CoabFormat.ClassFighterThief),
            P(CoabFormat.RaceElf, CoabFormat.ClassFighterThief),
            P(CoabFormat.RaceGnome, CoabFormat.ClassFighterThief),
        }),
        new(4, 6, "Support caster", new[]
        {
            P(CoabFormat.RaceHuman, CoabFormat.ClassCleric),
            P(CoabFormat.RaceHalfElf, CoabFormat.ClassClericFighterMage),
            P(CoabFormat.RaceHalfElf, CoabFormat.ClassClericMage),
            P(CoabFormat.RaceElf, CoabFormat.ClassFighterMage),
        }),
        new(5, 3, "Magic-user", new[]
        {
            P(CoabFormat.RaceHuman, CoabFormat.ClassMage),
            P(CoabFormat.RaceElf, CoabFormat.ClassFighterMage),
            P(CoabFormat.RaceHalfElf, CoabFormat.ClassMage),
            P(CoabFormat.RaceElf, CoabFormat.ClassMage),
        }),
    };

    // --- rolls ---------------------------------------------------------------
    private static int RollAge(Random rng, int race)
    {
        var (min, max) = ClassTables.StartingAge(race);
        return rng.Next(min, max + 1);
    }

    private static int RollAbility(Random rng, RollStyle style)
    {
        if (style == RollStyle.ThreeD6) return rng.Next(1, 7) + rng.Next(1, 7) + rng.Next(1, 7);
        Span<int> d = stackalloc int[4];
        int total = 0, lowest = 7;
        for (int i = 0; i < 4; i++) { d[i] = rng.Next(1, 7); total += d[i]; if (d[i] < lowest) lowest = d[i]; }
        return total - lowest;
    }

    private static readonly Dictionary<int, int[]> StatOrder = new()
    {
        [CoabFormat.ClassFighter] = new[] { StatStr, StatCon, StatDex, StatWis, StatInt, StatCha },
        [CoabFormat.ClassPaladin] = new[] { StatStr, StatCha, StatCon, StatWis, StatDex, StatInt },
        [CoabFormat.ClassRanger] = new[] { StatStr, StatDex, StatCon, StatWis, StatInt, StatCha },
        [CoabFormat.ClassCleric] = new[] { StatWis, StatCon, StatStr, StatDex, StatCha, StatInt },
        [CoabFormat.ClassDruid] = new[] { StatWis, StatCha, StatCon, StatDex, StatStr, StatInt },
        [CoabFormat.ClassMage] = new[] { StatInt, StatDex, StatCon, StatWis, StatCha, StatStr },
        [CoabFormat.ClassThief] = new[] { StatDex, StatCon, StatStr, StatInt, StatWis, StatCha },
        [CoabFormat.ClassMonk] = new[] { StatDex, StatWis, StatStr, StatCon, StatInt, StatCha },
    };

    /// <summary>The order to deal the sorted rolls in: each class's own priorities interleaved.</summary>
    private static int[] StatPriority(int[] classes)
    {
        var order = new List<int>(CoabFormat.StatCount);
        for (int rank = 0; rank < CoabFormat.StatCount; rank++)
            foreach (int cls in classes)
            {
                int stat = StatOrder[cls][rank];
                if (!order.Contains(stat)) order.Add(stat);
            }
        return order.ToArray();
    }

    /// <summary>
    /// The spells a new caster starts knowing. A magic-user gets Sleep and Magic Missile plus two
    /// more level-1 picks. A cleric prays for its spells rather than learning them, so every
    /// level-1 cleric spell is flagged.
    /// </summary>
    private static bool[] RollKnownSpells(Random rng, int[] classes)
    {
        var known = new bool[CoabFormat.KnownSpellsLen];

        if (classes.Contains(CoabFormat.ClassCleric))
            for (int i = 0; i < SpellBook.InRecordOrder.Count; i++)
                if (SpellBook.InRecordOrder[i] is { School: "Cleric", Level: 1 }) known[i] = true;

        if (classes.Contains(CoabFormat.ClassMage))
        {
            foreach (string name in new[] { "Sleep", "Magic Missile" })
            {
                int idx = SpellBook.RecordIndexOf("Mage", name);
                if (idx >= 0) known[idx] = true;
            }
            var rest = Enumerable.Range(0, SpellBook.InRecordOrder.Count)
                .Where(i => !known[i] && SpellBook.InRecordOrder[i] is { School: "Mage", Level: 1 })
                .OrderBy(_ => rng.Next())
                .Take(2);
            foreach (int i in rest) known[i] = true;
        }
        return known;
    }

    // --- alignment and names --------------------------------------------------
    /// <summary>A good alignment. Thieves cannot be lawful good, so a character with a thief level
    /// draws from neutral and chaotic good only.</summary>
    private static int PickAlignment(Random rng, int[] classes)
    {
        int[] good = classes.Contains(CoabFormat.ClassThief)
            ? new[] { CoabFormat.AlignmentNeutralGood, CoabFormat.AlignmentChaoticGood }
            : new[] { CoabFormat.AlignmentLawfulGood, CoabFormat.AlignmentNeutralGood, CoabFormat.AlignmentChaoticGood };
        return good[rng.Next(good.Length)];
    }

    /// <summary>The alignment bytes a generated character can carry — the three good ones.</summary>
    public static readonly int[] GoodAlignments =
        { CoabFormat.AlignmentLawfulGood, CoabFormat.AlignmentNeutralGood, CoabFormat.AlignmentChaoticGood };

    private static string PickName(Random rng, int gender, HashSet<string> used)
    {
        var pool = gender == 0 ? MaleNames : FemaleNames;
        for (int attempt = 0; attempt < pool.Length * 2; attempt++)
        {
            string name = pool[rng.Next(pool.Length)];
            if (used.Add(name)) return name;
        }
        for (int n = 2; ; n++)
        {
            string name = Truncate(pool[0], CoabFormat.NameMaxLength - 2) + " " + n;
            if (used.Add(name)) return name;
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private static readonly string[] MaleNames =
    {
        "ALARIC", "BRANNOR", "CEDRIC", "DORAN", "FENRIK", "GARETH", "HALDRIC", "IVARR",
        "JORAN", "KELDOR", "LOMWYN", "MERRIC", "NOLAN", "ORRIN", "PERRIN", "QUINLAN",
        "ROLAND", "SEVRIN", "THORVALD", "ULRIC", "VARDEN", "WULFGAR", "YORICK", "ZAMEK",
    };

    private static readonly string[] FemaleNames =
    {
        "ALYSSA", "BRIANNE", "CERIDWEN", "DELWYN", "ELSPETH", "FIONNA", "GWENNA", "HELENE",
        "ISOLDE", "JESSAMY", "KATRIEL", "LIRIEL", "MERIDA", "NAIRA", "ODELIA", "PERRINA",
        "ROWENA", "SERAFINA", "TALWYN", "URSULA", "VERENA", "WYNNE", "YSOLDE", "ZARA",
    };
}
