namespace PoolOfRadianceTrainer.Game;

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
    /// <see cref="PorFormat.OffClassMask"/>. A class change that leaves this behind would tell the
    /// engine the character is still whatever it used to be.</summary>
    public required int ClassMask { get; init; }

    public required int Thac0Base { get; init; }
    public required int Thac0 { get; init; }
    public required int[] Saves { get; init; }
    public required int[] ThiefSkills { get; init; }
    public required int[] ClericSlots { get; init; }
    public required int[] MageSlots { get; init; }
    public required bool[] KnownSpells { get; init; }

    /// <summary>Things about this change that are wrong or that the game itself would not allow —
    /// an illegal race/class pairing, a level the class cannot reach, an ability below the class
    /// minimum, experience that doesn't support the level.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>Consequences worth knowing that aren't problems — what is kept, what is cleared.</summary>
    public required IReadOnlyList<string> Notes { get; init; }

    /// <summary>True when the class byte isn't actually changing, so this is a repair: it recomputes
    /// the derived numbers from class and level and leaves the class alone.</summary>
    public bool IsSameClass => FromClass == ToClass;

    public string FromName => PorFormat.ClassName(FromClass);
    public string ToName => PorFormat.ClassName(ToClass);

    /// <summary>One line describing the levels the change produces, e.g.
    /// "Cleric 5 / Fighter 5 / Mage 5".</summary>
    public string LevelText =>
        string.Join(" / ", SingleClasses.Select((c, i) => $"{PorFormat.ClassLevelNames[c]} {Levels[i]}"));

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
            if (ClericSlots[0] + ClericSlots[1] + ClericSlots[2] > 0)
                spells.Add($"cleric spells {string.Join("/", ClericSlots)}");
            if (MageSlots[0] + MageSlots[1] + MageSlots[2] > 0)
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
/// the hit points it had — which no legitimately-played character could have, and is exactly the
/// point of a trainer. Experience, abilities, money, items and the game's own pointers are left
/// untouched; see <see cref="WrittenRanges"/> for the exact bytes this writes.</para>
/// </summary>
public static class ClassChange
{
    /// <summary>
    /// The record byte ranges <see cref="Apply"/> writes, in ascending order. Notably absent: hit
    /// points, experience, abilities, Armor Class (which comes from Dexterity and armour, not
    /// class), the level-drain bytes, the money counters and every pointer the game keeps its own
    /// bookkeeping in.
    /// </summary>
    public static readonly (int Offset, int Length)[] WrittenRanges =
    {
        (PorFormat.OffMemorizedSpells, PorFormat.MemorizedSpellsLen),
        (PorFormat.OffThac0Base, 1),
        (PorFormat.OffClass, 1),
        (PorFormat.OffKnownSpells, PorFormat.KnownSpellsLen),
        (PorFormat.OffAttackLevel, 1),
        (PorFormat.OffSaves, PorFormat.SavesLen),
        (PorFormat.OffLevelHighest, 1),
        (PorFormat.OffThiefSkills, PorFormat.ThiefSkillsLen),
        (PorFormat.OffClassLevels, PorFormat.ClassLevelCount),
        (PorFormat.OffClassMask, 1),
        (PorFormat.OffClericSlots, 6),          // cleric slots + mage slots, adjacent
        (PorFormat.OffThac0Cur, 1),
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
                "Not a class a Pool of Radiance character can hold.");

        var warnings = new List<string>();
        var notes = new List<string>();

        int race = record.Race;
        int current = Math.Max(1, record.EffectiveLevel);

        // Keep the level, but no class can be carried past where the game stops it: the training
        // hall's cap, or the race's own lower limit.
        var levels = new int[classes.Length];
        for (int i = 0; i < classes.Length; i++)
        {
            int cls = classes[i];
            int cap = ClassTables.LevelCap(race, cls);
            if (cap == 0)
            {
                // The race can't take this class at all; fall back to the class's own cap so the
                // rest of the plan is still computable, and say so.
                cap = ClassTables.TrainingCap(cls);
                warnings.Add($"{PorFormat.RaceName(race)}s cannot be {PorFormat.ClassLevelNames[cls]}s in " +
                             "this game — the game would never create this character, and its own screens " +
                             "may not agree with the numbers written here.");
            }
            levels[i] = Math.Clamp(current, 1, cap);
            if (levels[i] < current)
                warnings.Add($"{PorFormat.ClassLevelNames[cls]} is capped at {levels[i]} for a " +
                             $"{PorFormat.RaceName(race)} — the character drops from level {current} in that class.");
        }

        int highest = levels.Max();
        var classLevels = new int[PorFormat.ClassLevelCount];
        for (int i = 0; i < classes.Length; i++) classLevels[classes[i]] = levels[i];

        // THAC0: the new class's base, and a current that keeps whatever the character's equipment
        // and Strength were already worth (old base − old current), since neither of those changes.
        int thac0Base = Enumerable.Range(0, classes.Length).Min(i => ClassTables.Thac0(classes[i], levels[i]));
        int equipmentDelta = record.Thac0Base - record.Thac0;
        int thac0 = Math.Clamp(thac0Base - equipmentDelta, 1, 30);

        var saves = ClassTables.SavesFor(classes, levels);

        int thiefIndex = Array.IndexOf(classes, PorFormat.ClassThief);
        var thiefSkills = thiefIndex >= 0
            ? ClassTables.ThiefSkills(levels[thiefIndex], race, record.Dexterity)
            : new int[PorFormat.ThiefSkillsLen];

        int clericIndex = Array.IndexOf(classes, PorFormat.ClassCleric);
        var clericSlots = clericIndex >= 0
            ? ClassTables.ClericSlots(levels[clericIndex], record.Wisdom)
            : new int[3];

        int mageIndex = Array.IndexOf(classes, PorFormat.ClassMage);
        var mageSlots = mageIndex >= 0 ? ClassTables.MageSlots(levels[mageIndex]) : new int[3];

        var known = KnownSpellsFor(classes, levels);

        // Ability minimums the creation screen enforces. Not fatal — the record will hold whatever
        // is written — but the character is one the game could not have made.
        foreach (int cls in classes)
            foreach (var (stat, min) in ClassTables.Minimums(cls))
                if (record.GetStat(stat) < min)
                    warnings.Add($"{PorFormat.Stats[stat]} {record.GetStat(stat)} is below the {min} a " +
                                 $"{PorFormat.ClassLevelNames[cls]} needs — raise it on this tab first.");

        // Experience against the new class's own table. The record holds one total, and for a
        // multiclass that total is already the divided share, so each class reads the same number.
        for (int i = 0; i < classes.Length; i++)
        {
            long need = ClassTables.XpForLevel(classes[i], levels[i]);
            if (need > 0 && record.Experience < need)
                warnings.Add($"{record.Experience:N0} experience is short of the {need:N0} a level-{levels[i]} " +
                             $"{PorFormat.ClassLevelNames[classes[i]]} needs — the training hall will not " +
                             "level this character until it catches up.");
            else if (need >= 0)
            {
                int supported = ClassTables.LevelForXp(classes[i], record.Experience);
                if (supported > levels[i])
                    notes.Add($"Its experience would already support {PorFormat.ClassLevelNames[classes[i]]} " +
                              $"level {supported} — train at the hall to collect it.");
            }
        }

        if (thiefIndex >= 0 && record.Alignment == AlignmentLawfulGood)
            warnings.Add("A thief cannot be lawful good — change the alignment on this tab.");

        notes.Add($"Hit points stay at {record.HpCurrent}/{record.HpMax}: they are the character's, not the class's.");
        notes.Add("Experience, abilities, Armor Class, money and carried items are left alone.");
        if (record.Bytes.Skip(PorFormat.OffMemorizedSpells).Take(PorFormat.MemorizedSpellsLen).Any(b => b != 0))
            notes.Add("Memorized spells are cleared — the new class may not be able to cast them. Rest to memorize again.");
        if (known.Any(k => k))
            notes.Add("The new class knows every spell of the levels it can cast; what it may memorize is " +
                      "limited by its spells per day.");
        else if (ClassTables.SingleClassesOf(record.Class).Any(c => c is PorFormat.ClassCleric or PorFormat.ClassMage))
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

        // Whatever was memorized belonged to the old class.
        Array.Clear(record.Bytes, PorFormat.OffMemorizedSpells, PorFormat.MemorizedSpellsLen);

        record.Thac0Base = plan.Thac0Base;
        record.Class = plan.ToClass;

        for (int i = 0; i < PorFormat.KnownSpellsLen; i++)
            record.Bytes[PorFormat.OffKnownSpells + i] =
                i < plan.KnownSpells.Length && plan.KnownSpells[i] ? (byte)1 : (byte)0;

        record.Bytes[PorFormat.OffAttackLevel] = (byte)plan.Level;
        for (int i = 0; i < PorFormat.SavesLen; i++) record.SetSave(i, plan.Saves[i]);
        record.Bytes[PorFormat.OffLevelHighest] = (byte)plan.Level;
        for (int i = 0; i < PorFormat.ThiefSkillsLen; i++) record.SetThiefSkill(i, plan.ThiefSkills[i]);
        for (int i = 0; i < PorFormat.ClassLevelCount; i++) record.SetClassLevel(i, plan.ClassLevels[i]);
        // The class byte is not the only place the record says what this character is.
        record.Bytes[PorFormat.OffClassMask] = (byte)plan.ClassMask;
        for (int i = 0; i < 3; i++)
        {
            record.Bytes[PorFormat.OffClericSlots + i] = (byte)plan.ClericSlots[i];
            record.Bytes[PorFormat.OffMageSlots + i] = (byte)plan.MageSlots[i];
        }
        record.Thac0 = plan.Thac0;
    }

    /// <summary>
    /// The spells a character of these classes and levels knows: every spell of every level it can
    /// cast. That is simply correct for a cleric, which prays for its spells rather than learning
    /// them, and generous for a magic-user — which is what a trainer is for. What can actually be
    /// memorized is still limited by spells per day.
    /// </summary>
    private static bool[] KnownSpellsFor(IReadOnlyList<int> classes, IReadOnlyList<int> levels)
    {
        var known = new bool[PorFormat.KnownSpellsLen];
        for (int c = 0; c < classes.Count; c++)
        {
            string school = classes[c] switch
            {
                PorFormat.ClassCleric => "Cleric",
                PorFormat.ClassMage => "Mage",
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

    private const int AlignmentLawfulGood = 0;
}
