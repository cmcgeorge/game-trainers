using TheQuestTrainer.Memory;

namespace TheQuestTrainer.Game;

/// <summary>One active effect, as it sits in the heap.</summary>
public readonly record struct ActiveEffect(uint Address, int Magnitude, int Duration, byte Source)
{
    /// <summary>Whether the game's own cure would take this one away.</summary>
    public bool IsCurable => ConditionLayout.IsCurable(Source);
}

/// <summary>
/// One effect group, read whole: the two pointers that bound it and the effects between them.
/// </summary>
public sealed record EffectGroup
{
    /// <summary>Group index, 1..<see cref="ConditionLayout.LastEffectGroup"/>.</summary>
    public required int Index { get; init; }

    /// <summary>Address of the group's <c>begin</c> pointer, inside the character record.</summary>
    public required uint Slot { get; init; }

    /// <summary>First element of the pointer array, or 0 when the vector never allocated.</summary>
    public required uint Begin { get; init; }

    /// <summary>One past the last element.</summary>
    public required uint End { get; init; }

    /// <summary>The effects, in the game's own order.</summary>
    public required IReadOnlyList<ActiveEffect> Effects { get; init; }

    /// <summary>Sum of the magnitudes — the game's own "how poisoned are you" arithmetic.</summary>
    public int TotalMagnitude => Effects.Sum(e => e.Magnitude);

    /// <summary>The longest time left, which is how the game words a curse or a paralysis.</summary>
    public int LongestDuration => Effects.Count == 0 ? 0 : Effects.Max(e => e.Duration);

    /// <summary>How many of them a cure would remove.</summary>
    public int Curable => Effects.Count(e => e.IsCurable);
}

/// <summary>Something wrong with the character right now.</summary>
public sealed record Affliction
{
    /// <summary>Which of the game's four it is.</summary>
    public required Condition Condition { get; init; }

    /// <summary>How the game words the severity, e.g. <c>2 health per turn</c> or <c>14 turns left</c>.</summary>
    public required string Detail { get; init; }

    /// <summary>Entries behind it: effects in the group, or 1 for a single disease.</summary>
    public required int Entries { get; init; }

    /// <summary>How many of those entries the trainer's cure would remove.</summary>
    public required int Curable { get; init; }

    /// <summary>Label the game would use.</summary>
    public string Name => ConditionTables.Name(Condition);

    /// <summary>Name and severity on one line.</summary>
    public string Label => Detail.Length == 0 ? Name : $"{Name} — {Detail}";
}

/// <summary>Every adverse condition on the character, read in one pass.</summary>
public sealed record ConditionSnapshot
{
    /// <summary>Address of the character record it was read from.</summary>
    public required uint Record { get; init; }

    /// <summary>What is wrong, in the order the character screen lists it. Empty when nothing is.</summary>
    public required IReadOnlyList<Affliction> Afflictions { get; init; }

    /// <summary>Names of the diseases the character is carrying.</summary>
    public required IReadOnlyList<string> Diseases { get; init; }

    /// <summary>Whether anything at all is wrong.</summary>
    public bool Any => Afflictions.Count > 0;

    /// <summary>Whether a cure has anything to do.</summary>
    public bool AnyCurable => Afflictions.Any(a => a.Curable > 0);

    /// <summary>
    /// One line per affliction, or the game's own word for a clean bill of health.
    ///
    /// An affliction with nothing curable behind it says so rather than being hidden: the trainer
    /// removes exactly what the game's own cures remove, and an effect from a worn item or from the
    /// character's race is neither.
    /// </summary>
    public string Summary
    {
        get
        {
            if (Afflictions.Count == 0) return "None.";
            return string.Join("\n", Afflictions.Select(a =>
                a.Curable > 0 ? a.Label : $"{a.Label} (not something a cure removes)"));
        }
    }
}

/// <summary>
/// Reads the character's conditions out of a validated record.
///
/// Nothing here is a flag to test. Poison, curse and paralysis are vectors of heap-allocated effect
/// objects, and which vector holds which is decided by a table inside the record — so this reads the
/// table first and then follows it, exactly as the game's own cure does. Disease is a vector of
/// pointers to shared type objects, and the names come from those.
/// </summary>
public static class ConditionReader
{
    /// <summary>
    /// Snapshots every condition on the record at <paramref name="record"/>. Returns null when the
    /// structures do not read back as what they should be — a group index outside the array, a pair
    /// of pointers that is not a vector, an effect that cannot be read. The caller shows nothing and
    /// refuses to cure, rather than acting on whatever the bytes happened to be.
    /// </summary>
    public static ConditionSnapshot? Read(IMemorySource source, uint record)
    {
        ArgumentNullException.ThrowIfNull(source);

        var afflictions = new List<Affliction>();
        var diseases = ReadDiseases(source, record);
        if (diseases is null) return null;

        foreach (var condition in ConditionTables.All)
        {
            if (condition == Condition.Disease)
            {
                foreach (string name in diseases)
                    afflictions.Add(new Affliction
                    {
                        Condition = Condition.Disease,
                        Detail = name,
                        Entries = 1,
                        Curable = 1,
                    });
                continue;
            }

            var group = ReadGroupOf(source, record, condition);
            if (group is null) return null;
            if (group.Effects.Count == 0) continue;

            // Poison is "on" when the total is positive, not when the list is non-empty: that is the
            // game's own test, and it is what makes an effect that heals as much as it harms read as
            // no poison at all rather than as a poison of zero.
            if (condition == Condition.Poison && group.TotalMagnitude <= 0) continue;

            afflictions.Add(new Affliction
            {
                Condition = condition,
                Detail = ConditionTables.Describe(condition, group.TotalMagnitude, group.LongestDuration),
                Entries = group.Effects.Count,
                Curable = group.Curable,
            });
        }

        return new ConditionSnapshot
        {
            Record = record,
            Afflictions = afflictions,
            Diseases = diseases,
        };
    }

    /// <summary>
    /// Reads the effect group holding <paramref name="condition"/>, or null when the record's own
    /// kind table does not point at a group this build has.
    /// </summary>
    public static EffectGroup? ReadGroupOf(IMemorySource source, uint record, Condition condition)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (ConditionTables.EffectKind(condition) is not { } kind) return null;
        if (!TryReadUInt32(source, ConditionLayout.EffectGroupSlot(record, kind), out uint group)) return null;
        if (!ConditionLayout.IsEffectGroup(group)) return null;

        return ReadGroup(source, record, (int)group);
    }

    /// <summary>
    /// Reads effect group <paramref name="index"/>. Returns null when the two pointers are not a
    /// plausible vector or an element cannot be followed.
    /// </summary>
    public static EffectGroup? ReadGroup(IMemorySource source, uint record, int index)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!ConditionLayout.IsEffectGroup(index)) return null;

        uint slot = ConditionLayout.EffectGroupBegin(record, index);
        if (!TryReadUInt32(source, slot, out uint begin)) return null;
        if (!TryReadUInt32(source, slot + 4, out uint end)) return null;

        int count = VectorLength(begin, end, ConditionLayout.MaxEffectsPerGroup);
        if (count < 0) return null;

        var effects = new List<ActiveEffect>(count);
        if (count > 0)
        {
            var pointers = new byte[count * 4];
            if (source.Read(begin, pointers, pointers.Length) != pointers.Length) return null;

            var effect = new byte[ConditionLayout.EffectBytes];
            for (int i = 0; i < count; i++)
            {
                uint address = BitConverter.ToUInt32(pointers, i * 4);
                if (address == 0) return null;
                if (source.Read(address, effect, effect.Length) != effect.Length) return null;

                effects.Add(new ActiveEffect(
                    address,
                    BitConverter.ToInt16(effect, (int)ConditionLayout.EffectMagnitude),
                    BitConverter.ToInt32(effect, (int)ConditionLayout.EffectDuration),
                    effect[(int)ConditionLayout.EffectSource]));
            }
        }

        return new EffectGroup
        {
            Index = index,
            Slot = slot,
            Begin = begin,
            End = end,
            Effects = effects,
        };
    }

    /// <summary>
    /// Names of the diseases the character carries, or null when the vector does not read as one.
    ///
    /// A disease whose type object cannot be read is *not* skipped — the vector is being trusted
    /// enough to be written to, so an element that leads nowhere means it should not be.
    /// </summary>
    public static IReadOnlyList<string>? ReadDiseases(IMemorySource source, uint record)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!TryReadUInt32(source, record + ConditionLayout.DiseasesBegin, out uint begin)) return null;
        if (!TryReadUInt32(source, record + ConditionLayout.DiseasesEnd, out uint end)) return null;

        int count = VectorLength(begin, end, ConditionLayout.MaxDiseases);
        if (count < 0) return null;
        if (count == 0) return Array.Empty<string>();

        var pointers = new byte[count * 4];
        if (source.Read(begin, pointers, pointers.Length) != pointers.Length) return null;

        var names = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            uint type = BitConverter.ToUInt32(pointers, i * 4);
            if (type == 0) return null;
            if (!TryReadUInt32(source, type + ConditionLayout.DiseaseTypeName, out uint name)) return null;

            string? text = ItemTypeReader.ReadText(source, name);
            if (text is null) return null;
            names.Add(text);
        }

        return names;
    }

    /// <summary>
    /// Elements in a <c>std::vector</c> of dwords, or -1 when the two pointers are not a plausible
    /// one — misordered, misaligned, or longer than <paramref name="max"/>.
    /// </summary>
    internal static int VectorLength(uint begin, uint end, int max)
    {
        if (begin == 0 && end == 0) return 0;
        if (begin == 0 || end < begin) return -1;
        uint bytes = end - begin;
        if (bytes % 4 != 0) return -1;
        uint count = bytes / 4;
        return count > max ? -1 : (int)count;
    }

    private static bool TryReadUInt32(IMemorySource source, uint address, out uint value)
    {
        var word = new byte[4];
        if (source.Read(address, word, 4) != 4) { value = 0; return false; }
        value = BitConverter.ToUInt32(word, 0);
        return true;
    }
}
