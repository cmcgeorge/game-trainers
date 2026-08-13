namespace PoolOfRadianceTrainer.Game;

/// <summary>How a generated character's six ability scores are rolled.</summary>
public enum RollStyle
{
    /// <summary>Three six-sided dice per ability — what the game's own create screen rolls (the
    /// model behind <see cref="RollOdds"/>). Averages 10.5 per ability.</summary>
    ThreeD6,

    /// <summary>Four dice keeping the best three — the usual house rule for a party meant to finish
    /// the game. Averages 12.2 per ability, and never rolls a 3.</summary>
    FourD6DropLowest,
}

/// <summary>
/// One generated level-1 character: everything the 285-byte record needs to describe a playable
/// party member, with no reference to where that record lives. <see cref="StampOnto"/> writes it
/// into a <see cref="CharacterRecord"/>; <see cref="WrittenRanges"/> lists exactly which byte
/// ranges that touches, so a live edit can poke those and nothing else.
/// </summary>
public sealed class RolledCharacter
{
    public required string Name { get; init; }
    /// <summary>The party job this character was rolled for ("Front-line fighter", "Healer", …).</summary>
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
    /// <summary>The hit die total before the Constitution bonus — what <c>0xB1</c> holds.</summary>
    public required int HpRolled { get; init; }

    public required int Thac0Base { get; init; }
    /// <summary>Effective THAC0: the base less the Strength bonus to hit.</summary>
    public required int Thac0 { get; init; }
    public required int ArmorClassBase { get; init; }
    /// <summary>Effective AC: unarmored 10 less the Dexterity adjustment. Carried armor is not
    /// counted — a generated character keeps whatever the record it replaces was holding, and the
    /// game recomputes this the moment anything is readied.</summary>
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

    /// <summary>The single-class bytes this character combines — <c>{2}</c> for a fighter,
    /// <c>{2, 5}</c> for a Fighter/Mage. These double as class-level byte indices.</summary>
    public required int[] SingleClasses { get; init; }

    // --- display ------------------------------------------------------------
    public string Title => $"{Name}  —  {PorFormat.GenderName(Gender)} {PorFormat.RaceName(Race)} {PorFormat.ClassName(Class)}";

    public string StatsText
    {
        get
        {
            var parts = new List<string>(PorFormat.StatCount);
            for (int i = 0; i < PorFormat.StatCount; i++)
            {
                string v = i == 0 && Stats[0] == 18 && StrengthPercent > 0
                    ? $"18/{(StrengthPercent >= 100 ? "00" : StrengthPercent.ToString("D2"))}"
                    : Stats[i].ToString();
                parts.Add($"{PorFormat.StatsShort[i]} {v}");
            }
            return string.Join(" · ", parts);
        }
    }

    public string CombatText =>
        $"HP {HpMax} · AC {ArmorClass} · THAC0 {Thac0} · saves {string.Join("/", Saves)} · " +
        $"{PorFormat.AlignmentName(Alignment)} · age {Age}";

    /// <summary>The spells this character starts knowing — null for a non-caster, so the UI can
    /// collapse the line rather than leaving a blank one.</summary>
    public string? SpellsText
    {
        get
        {
            var names = new List<string>();
            for (int i = 0; i < KnownSpells.Length && i < SpellBook.InRecordOrder.Count; i++)
                if (KnownSpells[i]) names.Add(SpellBook.InRecordOrder[i].Name);
            if (names.Count == 0) return null;
            string slots = "";
            if (ClericSlots[0] > 0) slots += $"; {ClericSlots[0]} cleric spell{(ClericSlots[0] == 1 ? "" : "s")}/day";
            if (MageSlots[0] > 0) slots += $"; {MageSlots[0]} mage spell{(MageSlots[0] == 1 ? "" : "s")}/day";
            return "Knows " + string.Join(", ", names) + slots + ".";
        }
    }

    public override string ToString() => Title;

    // --- writing into a record ----------------------------------------------
    /// <summary>
    /// The record byte ranges <see cref="StampOnto"/> writes, in ascending order. Everything else
    /// in the 285 bytes is deliberately left alone: the money counters, the carried-item count and
    /// list pointer, the equipped-item pointers, the effects pointer, encumbrance, the party
    /// linked-list pointer and the combat-icon bytes all belong to the record's current owner or to
    /// the running game, and a generated character inherits them rather than corrupting them.
    /// </summary>
    public static readonly (int Offset, int Length)[] WrittenRanges =
    {
        (PorFormat.OffNameLength, 0x17),        // name length + name + six abilities + STR%
        (PorFormat.OffMemorizedSpells, PorFormat.MemorizedSpellsLen),
        (PorFormat.OffThac0Base, 1),
        (PorFormat.OffRace, 2),                 // race + class
        (PorFormat.OffAge, 2),
        (PorFormat.OffHpMax, 1),
        (PorFormat.OffKnownSpells, PorFormat.KnownSpellsLen),
        (PorFormat.OffAttackLevel, 1),
        (PorFormat.OffSaves, PorFormat.SavesLen),
        (PorFormat.OffMovementBase, 13),        // move base, level, drained levels/HP, undead level, thief skills
        (PorFormat.OffClassLevels, PorFormat.ClassLevelCount),
        (PorFormat.OffGender, 1),
        (PorFormat.OffAlignment, 1),
        (PorFormat.OffAcBase, 1),
        (PorFormat.OffExperience, 4),
        (PorFormat.OffClassMask, 8),            // class bitmask + HP rolled + cleric slots + mage slots
        (PorFormat.OffStatus, 1),
        (PorFormat.OffThac0Cur, 2),             // THAC0 current + AC current
        (PorFormat.OffHpCur, 2),                // HP current + movement current
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
        for (int i = 0; i < PorFormat.StatCount; i++) record.SetStat(i, Stats[i]);
        record.StrengthPercent = StrengthPercent;

        // A fresh character has memorized nothing — and leaving the previous occupant's memorized
        // spells behind would hand a fighter a cleric's prayers.
        Array.Clear(record.Bytes, PorFormat.OffMemorizedSpells, PorFormat.MemorizedSpellsLen);

        record.Thac0Base = Thac0Base;
        record.Race = Race;
        record.Class = Class;
        record.Age = Age;
        record.HpMax = HpMax;

        for (int i = 0; i < PorFormat.KnownSpellsLen; i++)
            record.Bytes[PorFormat.OffKnownSpells + i] = i < KnownSpells.Length && KnownSpells[i] ? (byte)1 : (byte)0;

        record.Bytes[PorFormat.OffAttackLevel] = (byte)Level;
        for (int i = 0; i < PorFormat.SavesLen; i++) record.SetSave(i, Saves[i]);

        record.Bytes[PorFormat.OffMovementBase] = (byte)Movement;
        record.Bytes[PorFormat.OffLevelHighest] = (byte)Level;
        record.Bytes[PorFormat.OffDrainedLevels] = 0;      // whoever held this slot may have met undead
        record.Bytes[PorFormat.OffDrainedHp] = 0;
        record.Bytes[PorFormat.OffUndeadLevel] = 0;
        for (int i = 0; i < PorFormat.ThiefSkillsLen; i++) record.SetThiefSkill(i, ThiefSkills[i]);

        for (int i = 0; i < PorFormat.ClassLevelCount; i++) record.SetClassLevel(i, ClassLevels[i]);
        record.Gender = Gender;
        record.Alignment = Alignment;
        record.ArmorClassBase = ArmorClassBase;
        record.Experience = 0;
        // The record says what class this character is in three places; the class byte alone is not
        // enough (see PorFormat.OffClassMask).
        record.Bytes[PorFormat.OffClassMask] = (byte)ClassTables.ClassMask(SingleClasses);
        record.HpRolled = HpRolled;
        for (int i = 0; i < 3; i++)
        {
            record.Bytes[PorFormat.OffClericSlots + i] = (byte)ClericSlots[i];
            record.Bytes[PorFormat.OffMageSlots + i] = (byte)MageSlots[i];
        }

        record.Status = 0;                                  // Okay — the slot may have held a corpse
        record.Thac0 = Thac0;
        record.ArmorClass = ArmorClass;
        record.HpCurrent = HpMax;
        record.Bytes[PorFormat.OffMovementCur] = (byte)Movement;
    }
}

/// <summary>
/// Rolls a ready-to-play Pool of Radiance party: good-aligned level-1 characters in race/class
/// combinations the game itself allows, with abilities dealt to the class that needs them and every
/// derived number (hit points, AC, THAC0, saving throws, thief skills, spells) filled in to match.
///
/// <para>The composition follows the party the game's own Rule Book and this trainer's strategy
/// guide recommend — front-line fighters, a healer, a scout, a magic-user — so a generated party is
/// viable rather than merely legal. Each slot draws from several race/class picks, so two runs give
/// different parties that are equally playable.</para>
///
/// <para><b>Provenance.</b> Everything here is either the game's own data or AD&amp;D 1st edition as
/// Pool of Radiance implements it, and the parts that could be checked against real records were:
/// the bundled sample party's level-1 dwarf fighter carries THAC0 base 20, saving throws
/// 14/15/16/17/17 and movement 12, and its level-1 elf Fighter/Mage carries THAC0 base 20, saves
/// 14/13/11/15/12 (the best of the fighter and magic-user rows, category by category), 7 hit points
/// — the average of a maximised d10 and d4 — and four known magic-user level-1 spells. Its dwarf
/// takes no racial bonus to its saving throws, so none is applied here either.</para>
/// </summary>
public static class PartyGenerator
{
    /// <summary>The most characters a Pool of Radiance party can hold.</summary>
    public const int MaxParty = 6;

    /// <summary>Generated characters are level 1 — the game's training halls do the rest.</summary>
    public const int StartingLevel = 1;

    /// <summary>Unencumbered movement, in squares. Both sample-party records carry 12.</summary>
    public const int BaseMovement = 12;

    /// <summary>The unarmored Armor Class the record's "base" AC byte holds (both sample records,
    /// and the real level-5 fighter save this trainer was tested against, store 10).</summary>
    public const int UnarmoredAc = 10;

    // --- generation ----------------------------------------------------------

    /// <summary>
    /// Rolls a party of <paramref name="count"/> characters (1..<see cref="MaxParty"/>), in
    /// marching order — melee at the front, the magic-user at the back. A short party keeps the
    /// roles that matter most: four characters are always a fighter, a cleric, a magic-user and a
    /// thief. Deterministic for a given <paramref name="rng"/> seed.
    /// </summary>
    public static IReadOnlyList<RolledCharacter> Generate(Random rng, int count = MaxParty,
                                                          RollStyle style = RollStyle.FourD6DropLowest)
    {
        ArgumentNullException.ThrowIfNull(rng);
        count = Math.Clamp(count, 1, MaxParty);

        // Drop the lowest-priority slots for a short party, then put the survivors back into
        // marching order so the front rank is still the front rank.
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
        int gender = rng.Next(2);   // 0 male, 1 female

        // Abilities: roll six, then deal the best to the abilities this character's classes live on.
        var rolls = new int[PorFormat.StatCount];
        for (int i = 0; i < rolls.Length; i++) rolls[i] = RollAbility(rng, style);
        Array.Sort(rolls);
        Array.Reverse(rolls);

        var stats = new int[PorFormat.StatCount];
        var order = StatPriority(classes);
        for (int i = 0; i < order.Length; i++) stats[order[i]] = rolls[i];

        // Racial adjustments, then the class requirements. Dealing the best rolls to the prime
        // requisites nearly always satisfies the minimums on its own; the raise is there so a bad
        // roll produces a legal character rather than a rejected one.
        int[] racialAdjust = ClassTables.StatAdjust(pick.Race);
        for (int i = 0; i < stats.Length; i++)
            stats[i] = Math.Clamp(stats[i] + racialAdjust[i], 3, 18);
        foreach (int cls in classes)
            foreach (var (stat, min) in ClassTables.Minimums(cls))
                if (stats[stat] < min) stats[stat] = min;

        // Exceptional Strength is the fighter's alone, and only at Strength 18. A female fighter
        // rolls 18/01-18/50 (see the strategy guide's party-creation notes).
        int strPercent = 0;
        if (stats[StatStr] == 18 && classes.Contains(PorFormat.ClassFighter))
            strPercent = gender == 0 ? rng.Next(1, 101) : rng.Next(1, 51);

        // Hit points: every class's die at maximum, averaged over a multiclass, plus the
        // Constitution bonus — the "max your starting HP in Modify Character" the strategy guide
        // recommends, since starting HP can never be improved later.
        int dice = classes.Sum(ClassTables.HitDie);
        int rolled = Math.Max(1, dice / classes.Length);
        bool warrior = classes.Contains(PorFormat.ClassFighter);
        int hp = Math.Max(1, rolled + ClassTables.ConstitutionHpBonus(stats[StatCon], warrior));

        int thac0Base = classes.Min(c => ClassTables.Thac0(c, StartingLevel));
        int ac = UnarmoredAc - ClassTables.DexterityAcBonus(stats[StatDex]);

        // A multiclass saves as its best class in every category.
        var levels = classes.Select(_ => StartingLevel).ToArray();
        var saves = ClassTables.SavesFor(classes, levels);

        var classLevels = new int[PorFormat.ClassLevelCount];
        foreach (int cls in classes) classLevels[cls] = StartingLevel;   // class bytes double as level indices

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
            ThiefSkills = classes.Contains(PorFormat.ClassThief)
                ? ClassTables.ThiefSkills(StartingLevel, pick.Race, stats[StatDex])
                : new int[PorFormat.ThiefSkillsLen],
            ClassLevels = classLevels,
            ClericSlots = classes.Contains(PorFormat.ClassCleric)
                ? ClassTables.ClericSlots(StartingLevel, stats[StatWis])
                : new int[3],
            MageSlots = classes.Contains(PorFormat.ClassMage) ? ClassTables.MageSlots(StartingLevel) : new int[3],
            KnownSpells = RollKnownSpells(rng, classes),
            SingleClasses = (int[])classes.Clone(),   // the roster's own array must stay untouched
        };
    }

    // --- the roster ----------------------------------------------------------
    private const int StatStr = ClassTables.StatStr, StatInt = ClassTables.StatInt, StatWis = ClassTables.StatWis,
                      StatDex = ClassTables.StatDex, StatCon = ClassTables.StatCon, StatCha = ClassTables.StatCha;

    /// <summary>One race/class combination the game allows at creation.</summary>
    private readonly record struct Pick(int Race, int ClassByte, int[] Classes);

    /// <summary>A job in the party: where it marches, how early it survives a short party, and the
    /// race/class combinations that can fill it.</summary>
    private sealed record Slot(int March, int Priority, string Role, Pick[] Picks);

    private static Pick P(int race, int classByte) => new(race, classByte, ClassTables.SingleClassesOf(classByte));

    /// <summary>
    /// The party the Rule Book and <c>docs/strategy-guide.md</c> §2 recommend: two front-line
    /// fighters, two clerics, a thief and a magic-user. Priority 1-4 keeps a fighter, a cleric, a
    /// magic-user and a thief in any party of four or more.
    /// </summary>
    private static readonly Slot[] Roster =
    {
        new(0, 1, "Front-line fighter", new[]
        {
            P(PorFormat.RaceHuman, PorFormat.ClassFighter),
            P(PorFormat.RaceDwarf, PorFormat.ClassFighter),
            P(PorFormat.RaceHalfElf, PorFormat.ClassFighter),
            P(PorFormat.RaceElf, PorFormat.ClassFighter),
        }),
        new(1, 5, "Second front-liner", new[]
        {
            P(PorFormat.RaceDwarf, PorFormat.ClassFighter),
            P(PorFormat.RaceHuman, PorFormat.ClassFighter),
            P(PorFormat.RaceDwarf, PorFormat.ClassFighterThief),
            P(PorFormat.RaceHalfElf, PorFormat.ClassClericFighter),
        }),
        new(2, 2, "Healer", new[]
        {
            P(PorFormat.RaceHuman, PorFormat.ClassCleric),
            P(PorFormat.RaceHalfElf, PorFormat.ClassCleric),
            P(PorFormat.RaceHalfElf, PorFormat.ClassClericFighter),
        }),
        new(3, 4, "Scout / trap-finder", new[]
        {
            P(PorFormat.RaceHuman, PorFormat.ClassThief),
            P(PorFormat.RaceHalfling, PorFormat.ClassFighterThief),
            P(PorFormat.RaceElf, PorFormat.ClassFighterThief),
            P(PorFormat.RaceGnome, PorFormat.ClassFighterThief),
        }),
        new(4, 6, "Support caster", new[]
        {
            P(PorFormat.RaceHuman, PorFormat.ClassCleric),
            P(PorFormat.RaceHalfElf, PorFormat.ClassClericFighterMage),
            P(PorFormat.RaceHalfElf, PorFormat.ClassClericMage),
            P(PorFormat.RaceElf, PorFormat.ClassFighterMage),
        }),
        new(5, 3, "Magic-user", new[]
        {
            P(PorFormat.RaceHuman, PorFormat.ClassMage),
            P(PorFormat.RaceElf, PorFormat.ClassFighterMage),
            P(PorFormat.RaceHalfElf, PorFormat.ClassMage),
            P(PorFormat.RaceElf, PorFormat.ClassMage),
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

    /// <summary>The order each class wants its rolls dealt in — its prime requisite first, then what
    /// keeps it alive. Generation policy rather than a rule of the game, which is why it lives here
    /// and not in <see cref="ClassTables"/>.</summary>
    private static readonly Dictionary<int, int[]> StatOrder = new()
    {
        [PorFormat.ClassFighter] = new[] { StatStr, StatCon, StatDex, StatWis, StatInt, StatCha },
        [PorFormat.ClassCleric] = new[] { StatWis, StatCon, StatStr, StatDex, StatCha, StatInt },
        [PorFormat.ClassMage] = new[] { StatInt, StatDex, StatCon, StatWis, StatCha, StatStr },
        [PorFormat.ClassThief] = new[] { StatDex, StatCon, StatStr, StatInt, StatWis, StatCha },
    };

    /// <summary>The order to deal the sorted rolls in: each class's own priorities interleaved, so a
    /// Fighter/Mage puts its best roll in Strength and its second in Intelligence.</summary>
    private static int[] StatPriority(int[] classes)
    {
        var order = new List<int>(PorFormat.StatCount);
        for (int rank = 0; rank < PorFormat.StatCount; rank++)
            foreach (int cls in classes)
            {
                int stat = StatOrder[cls][rank];
                if (!order.Contains(stat)) order.Add(stat);
            }
        return order.ToArray();
    }

    /// <summary>
    /// The spells a new caster starts knowing, as flags in <see cref="SpellBook.InRecordOrder"/>
    /// order. A magic-user gets Sleep and Magic Missile — the two spells the strategy guide calls
    /// the early game — plus two more of its level-1 list, matching the four the sample party's
    /// elf Fighter/Mage carries. A cleric prays for its spells rather than learning them, so every
    /// level-1 cleric spell is flagged.
    /// </summary>
    private static bool[] RollKnownSpells(Random rng, int[] classes)
    {
        var known = new bool[PorFormat.KnownSpellsLen];

        if (classes.Contains(PorFormat.ClassCleric))
            for (int i = 0; i < SpellBook.InRecordOrder.Count; i++)
                if (SpellBook.InRecordOrder[i] is { School: "Cleric", Level: 1 }) known[i] = true;

        if (classes.Contains(PorFormat.ClassMage))
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
    /// <summary>A good alignment. Thieves cannot be lawful good (strategy guide §2), so a character
    /// with a thief level draws from neutral and chaotic good only.</summary>
    private static int PickAlignment(Random rng, int[] classes)
    {
        int[] good = classes.Contains(PorFormat.ClassThief)
            ? new[] { AlignmentNeutralGood, AlignmentChaoticGood }
            : new[] { AlignmentLawfulGood, AlignmentNeutralGood, AlignmentChaoticGood };
        return good[rng.Next(good.Length)];
    }

    private const int AlignmentLawfulGood = 0, AlignmentNeutralGood = 3, AlignmentChaoticGood = 6;

    /// <summary>The alignment bytes a generated character can carry — the three good ones.</summary>
    public static readonly int[] GoodAlignments = { AlignmentLawfulGood, AlignmentNeutralGood, AlignmentChaoticGood };

    private static string PickName(Random rng, int gender, HashSet<string> used)
    {
        var pool = gender == 0 ? MaleNames : FemaleNames;
        for (int attempt = 0; attempt < pool.Length * 2; attempt++)
        {
            string name = pool[rng.Next(pool.Length)];
            if (used.Add(name)) return name;
        }
        // Every name in the pool is taken (impossible for a party of six, but a party is not the
        // only caller a future edit might add): fall back to a numbered one rather than a duplicate.
        for (int n = 2; ; n++)
        {
            string name = Truncate(pool[0], PorFormat.NameMaxLength - 2) + " " + n;
            if (used.Add(name)) return name;
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    // Names are stored uppercase because that is how the game writes and shows them, and all fit
    // the record's 15-character name field.
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
