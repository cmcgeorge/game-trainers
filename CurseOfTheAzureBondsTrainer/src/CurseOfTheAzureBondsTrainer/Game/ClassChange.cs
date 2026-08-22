namespace CurseOfTheAzureBondsTrainer.Game;

/// <summary>
/// What changing a character's class would do to its record: the new per-class levels and every
/// number that follows from them, plus anything the change would make questionable. Produced by
/// <see cref="ClassChange.Plan"/> so the user can read it before <see cref="ClassChange.Apply"/>
/// writes anything.
/// </summary>
public sealed class ClassChangePlan
{
    public required int FromClass { get; init; }
    public required int ToClass { get; init; }
    /// <summary>The single classes the new class byte combines.</summary>
    public required int[] SingleClasses { get; init; }
    /// <summary>The level each new class lands on, matching <see cref="SingleClasses"/>.</summary>
    public required int[] Levels { get; init; }
    /// <summary>The eight per-class level bytes as they will be written.</summary>
    public required int[] ClassLevels { get; init; }
    /// <summary>The highest of the new class levels — what the record's level byte holds.</summary>
    public required int Level { get; init; }

    /// <summary>The class bitmask the record carries alongside the class byte — see
    /// <see cref="CoabFormat.OffClassMask"/>.</summary>
    public required int ClassMask { get; init; }

    public required int Thac0Base { get; init; }
    public required int Thac0 { get; init; }
    public required int[] Saves { get; init; }
    public required int[] ThiefSkills { get; init; }
    public required int[] ClericSlots { get; init; }
    public required int[] MageSlots { get; init; }
    public required bool[] KnownSpells { get; init; }

    /// <summary>Things about this change that are wrong or that the game itself would not allow.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>Consequences worth knowing that aren't problems.</summary>
    public required IReadOnlyList<string> Notes { get; init; }

    /// <summary>True when the class byte isn't actually changing, so this is a repair.</summary>
    public bool IsSameClass => FromClass == ToClass;

    public string FromName => CoabFormat.ClassName(FromClass);
    public string ToName => CoabFormat.ClassName(ToClass);

    /// <summary>One line describing the levels the change produces.</summary>
    public string LevelText =>
        string.Join(" / ", SingleClasses.Select((c, i) => $"{CoabFormat.ClassLevelNames[c]} {Levels[i]}"));

    /// <summary>The whole plan as the status line and the confirm dialog show it.</summary>
    public string Summary
    {
        get
        {
            string head = IsSameClass
                ? $"Recompute {ToName} from class and level: {LevelText}"
                : $"{FromName} → {ToName}: {LevelText}";
            string numbers = $"THAC0 {Thac0} (base {Thac0Base}), saves {string.Join("/", Saves)}";
            var spells = new List<string>();
            if (ClericSlots.Sum() > 0)
                spells.Add($"cleric spells {string.Join("/", ClericSlots)}");
            if (MageSlots.Sum() > 0)
                spells.Add($"mage spells {string.Join("/", MageSlots)}");
            if (ThiefSkills.Any(s => s > 0))
                spells.Add($"thief skills {string.Join("/", ThiefSkills)}");
            return head + " · " + numbers + (spells.Count > 0 ? " · " + string.Join(" · ", spells) : "") + ".";
        }
    }
}

/// <summary>
/// Changes an existing character's class and brings every number that depends on it back into
/// agreement — the per-class levels, THAC0, saving throws, thief skills, spells known and spells
/// per day. The class byte on the Character tab can already be typed over on its own; what that
/// leaves behind is a character whose sheet says "Magic-User" while its saving throws, its THAC0
/// and its empty spell book still describe a fighter. This is that edit done properly.
///
/// <para><b>The character keeps its level and its hit points.</b> A level-5 fighter becomes a
/// level-5 magic-user, clamped to whatever that class can actually reach for that race, and keeps
/// the hit points it had. Experience, abilities, money, items and the game's own pointers are left
/// untouched; see <see cref="WrittenRanges"/> for the exact bytes this writes.</para>
/// </summary>
public static class ClassChange
{
    /// <summary>
    /// The record byte ranges <see cref="Apply"/> writes, in ascending order. Notably absent: hit
    /// points, experience, abilities, Armor Class, the level-drain bytes, the money counters and
    /// every pointer the game keeps its own bookkeeping in.
    /// </summary>
    public static readonly (int Offset, int Length)[] WrittenRanges =
    {
        (CoabFormat.OffMemorizedSpells, CoabFormat.MemorizedSpellsLen),
        (CoabFormat.OffThac0Base, 1),
        (CoabFormat.OffClass, 1),
        (CoabFormat.OffKnownSpells, CoabFormat.KnownSpellsLen),
        (CoabFormat.OffAttackLevel, 1),
        (CoabFormat.OffSaves, CoabFormat.SavesLen),
        (CoabFormat.OffMovementBase, 13),       // move base, level, drained levels/HP, undead level, thief skills
        (CoabFormat.OffClassLevels, CoabFormat.ClassLevelCount),
        (CoabFormat.OffGender, 1),
        (CoabFormat.OffAlignment, 1),
        (CoabFormat.OffAcBase, 1),
        (CoabFormat.OffExperience, 4),
        (CoabFormat.OffClassMask, 1),
        (CoabFormat.OffHpRolled, 1),
        (CoabFormat.OffClericSlots, CoabFormat.SpellSlotLevels),
        (CoabFormat.OffMageSlots, CoabFormat.SpellSlotLevels),
        (CoabFormat.OffThac0Cur, 1),
    };

    /// <summary>
    /// Works out what changing <paramref name="record"/> to <paramref name="newClassByte"/> would
    /// do, without touching the record. The character keeps its current level (clamped to what the
    /// new class and race can reach) and its hit points.
    /// </summary>
    public static ClassChangePlan Plan(CharacterRecord record, int newClassByte)
    {
        ArgumentNullException.ThrowIfNull(record);
        var classes = ClassTables.SingleClassesOf(newClassByte);
        if (classes.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(newClassByte), newClassByte,
                "Not a class a Curse of the Azure Bonds character can hold.");

        var warnings = new List<string>();
        var notes = new List<string>();

        int race = record.Race;
        int current = Math.Max(1, record.EffectiveLevel);

        // Keep the level, but no class can be carried past where the game stops it.
        var levels = new int[classes.Length];
        for (int i = 0; i < classes.Length; i++)
        {
            int cls = classes[i];
            int cap = ClassTables.LevelCap(race, cls);
            if (cap == 0)
            {
                cap = ClassTables.TrainingCap(cls);
                warnings.Add($"{CoabFormat.RaceName(race)}s cannot be {CoabFormat.ClassLevelNames[cls]}s in " +
                             "this game — the game would never create this character, and its own screens " +
                             "may not agree with the numbers written here.");
            }
            levels[i] = Math.Clamp(current, 1, cap);
            if (levels[i] < current)
                warnings.Add($"{CoabFormat.ClassLevelNames[cls]} is capped at {levels[i]} for a " +
                             $"{CoabFormat.RaceName(race)} — the character drops from level {current} in that class.");
        }

        int highest = levels.Max();
        var classLevels = new int[CoabFormat.ClassLevelCount];
        for (int i = 0; i < classes.Length; i++) classLevels[classes[i]] = levels[i];

        // THAC0: the new class's base, and a current that keeps whatever the character's equipment
        // and Strength were already worth.
        int thac0Base = Enumerable.Range(0, classes.Length).Min(i => ClassTables.Thac0(classes[i], levels[i]));
        int equipmentDelta = record.Thac0Base - record.Thac0;
        int thac0 = Math.Clamp(thac0Base - equipmentDelta, 1, 30);

        var saves = ClassTables.SavesFor(classes, levels);

        int thiefIndex = Array.IndexOf(classes, CoabFormat.ClassThief);
        var thiefSkills = thiefIndex >= 0
            ? ClassTables.ThiefSkills(levels[thiefIndex], race, record.Dexterity)
            : new int[CoabFormat.ThiefSkillsLen];

        int clericIndex = Array.IndexOf(classes, CoabFormat.ClassCleric);
        var clericSlots = clericIndex >= 0
            ? ClassTables.ClericSlots(levels[clericIndex], record.Wisdom)
            : new int[CoabFormat.SpellSlotLevels];

        int mageIndex = Array.IndexOf(classes, CoabFormat.ClassMage);
        var mageSlots = mageIndex >= 0 ? ClassTables.MageSlots(levels[mageIndex]) : new int[CoabFormat.SpellSlotLevels];

        var known = KnownSpellsFor(classes, levels);

        // Ability minimums the creation screen enforces.
        foreach (int cls in classes)
            foreach (var (stat, min) in ClassTables.Minimums(cls))
                if (record.GetStat(stat) < min)
                    warnings.Add($"{CoabFormat.Stats[stat]} {record.GetStat(stat)} is below the {min} a " +
                                 $"{CoabFormat.ClassLevelNames[cls]} needs — raise it on this tab first.");

        // Experience against the new class's own table.
        for (int i = 0; i < classes.Length; i++)
        {
            long need = ClassTables.XpForLevel(classes[i], levels[i]);
            if (need > 0 && record.Experience < need)
                warnings.Add($"{record.Experience:N0} experience is short of the {need:N0} a level-{levels[i]} " +
                             $"{CoabFormat.ClassLevelNames[classes[i]]} needs — the training hall will not " +
                             "level this character until it catches up.");
            else if (need >= 0)
            {
                int supported = ClassTables.LevelForXp(classes[i], record.Experience);
                if (supported > levels[i])
                    notes.Add($"Its experience would already support {CoabFormat.ClassLevelNames[classes[i]]} " +
                              $"level {supported} — train at the hall to collect it.");
            }
        }

        if (thiefIndex >= 0 && record.Alignment == CoabFormat.AlignmentLawfulGood)
            warnings.Add("A thief cannot be lawful good — change the alignment on this tab.");

        notes.Add($"Hit points stay at {record.HpCurrent}/{record.HpMax}: they are the character's, not the class's.");
        notes.Add("Experience, abilities, Armor Class, money and carried items are left alone.");
        if (record.Bytes.Skip(CoabFormat.OffMemorizedSpells).Take(CoabFormat.MemorizedSpellsLen).Any(b => b != 0))
            notes.Add("Memorized spells are cleared — the new class may not be able to cast them. Rest to memorize again.");
        if (known.Any(k => k))
            notes.Add("The new class knows every spell of the levels it can cast; what it may memorize is " +
                      "limited by its spells per day.");
        else if (ClassTables.SingleClassesOf(record.Class).Any(c => c is CoabFormat.ClassCleric or CoabFormat.ClassMage))
            notes.Add("The spell book is cleared — the new class casts nothing.");
        notes.Add("Readied equipment is not re-checked: gear the new class cannot use keeps working until " +
                  "you unready it in the game, and Armor Class recomputes then.");

        return new ClassChangePlan
        {
            FromClass = record.Class,
            ToClass = newClassByte,
            SingleClasses = classes,
            Levels = levels,
            ClassLevels = classLevels,
            ClassMask = ClassTables.ClassMask(classes),
            Level = highest,
            Thac0Base = thac0Base,
            Thac0 = thac0,
            Saves = saves,
            ThiefSkills = thiefSkills,
            ClericSlots = clericSlots,
            MageSlots = mageSlots,
            KnownSpells = known,
            Warnings = warnings,
            Notes = notes,
        };
    }

    /// <summary>Writes a plan into the record, touching only <see cref="WrittenRanges"/>.</summary>
    public static void Apply(CharacterRecord record, ClassChangePlan plan)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(plan);

        Array.Clear(record.Bytes, CoabFormat.OffMemorizedSpells, CoabFormat.MemorizedSpellsLen);

        record.Thac0Base = plan.Thac0Base;
        record.Class = plan.ToClass;

        for (int i = 0; i < CoabFormat.KnownSpellsLen; i++)
            record.Bytes[CoabFormat.OffKnownSpells + i] =
                i < plan.KnownSpells.Length && plan.KnownSpells[i] ? (byte)1 : (byte)0;

        record.Bytes[CoabFormat.OffAttackLevel] = (byte)plan.Level;
        for (int i = 0; i < CoabFormat.SavesLen; i++) record.SetSave(i, plan.Saves[i]);
        record.Bytes[CoabFormat.OffLevelHighest] = (byte)plan.Level;
        for (int i = 0; i < CoabFormat.ThiefSkillsLen; i++) record.SetThiefSkill(i, plan.ThiefSkills[i]);
        for (int i = 0; i < CoabFormat.ClassLevelCount; i++) record.SetClassLevel(i, plan.ClassLevels[i]);
        record.Bytes[CoabFormat.OffClassMask] = (byte)plan.ClassMask;
        for (int i = 0; i < CoabFormat.SpellSlotLevels; i++)
        {
            record.Bytes[CoabFormat.OffClericSlots + i] = (byte)plan.ClericSlots[i];
            record.Bytes[CoabFormat.OffMageSlots + i] = (byte)plan.MageSlots[i];
        }
        record.Thac0 = plan.Thac0;
    }

    /// <summary>
    /// The spells a character of these classes and levels knows: every spell of every level it can
    /// cast. Generous for a magic-user — which is what a trainer is for.
    /// </summary>
    private static bool[] KnownSpellsFor(IReadOnlyList<int> classes, IReadOnlyList<int> levels)
    {
        var known = new bool[CoabFormat.KnownSpellsLen];
        for (int c = 0; c < classes.Count; c++)
        {
            string school = classes[c] switch
            {
                CoabFormat.ClassCleric => "Cleric",
                CoabFormat.ClassMage => "Mage",
                CoabFormat.ClassDruid => "Druid",
                _ => "",
            };
            if (school.Length == 0) continue;

            int max = ClassTables.MaxSpellLevel(levels[c]);
            for (int i = 0; i < SpellBook.InRecordOrder.Count && i < known.Length; i++)
                if (SpellBook.InRecordOrder[i].School == school && SpellBook.InRecordOrder[i].Level <= max)
                    known[i] = true;
        }
        return known;
    }
}
