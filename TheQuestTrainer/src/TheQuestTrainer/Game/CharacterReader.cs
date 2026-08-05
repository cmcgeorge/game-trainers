using TheQuestTrainer.Memory;

namespace TheQuestTrainer.Game;

/// <summary>
/// Everything the UI shows, read in one pass so nothing on screen is half a tick older than
/// anything else. Arrays are indexed by the game's own ids, so slot 0 is present and unused.
/// </summary>
public sealed record CharacterSnapshot
{
    /// <summary>Address the record was read from.</summary>
    public required uint Record { get; init; }

    /// <summary>Character name.</summary>
    public required string Name { get; init; }

    /// <summary>Portrait resource id, e.g. <c>bres_head00_racederth</c>.</summary>
    public required string PortraitId { get; init; }

    /// <summary>Race id, 0..5.</summary>
    public required uint RaceId { get; init; }

    /// <summary>Race name for <see cref="RaceId"/>.</summary>
    public string RaceName => GameTables.RaceName(RaceId);

    /// <summary>Character level.</summary>
    public required int Level { get; init; }

    /// <summary>Total experience.</summary>
    public required long Experience { get; init; }

    /// <summary>The threshold the game has cached for the next level.</summary>
    public required long ExperienceForNextLevel { get; init; }

    /// <summary>Current health. The maximum is derived and is not in the record.</summary>
    public required int Health { get; init; }

    /// <summary>Current mana. The maximum is derived and is not in the record.</summary>
    public required int Mana { get; init; }

    /// <summary>Gold.</summary>
    public required long Gold { get; init; }

    /// <summary>Fame, -100..+100.</summary>
    public required int Fame { get; init; }

    /// <summary>Reputation word for <see cref="Fame"/>.</summary>
    public string FameBand => GameTables.FameBand(Fame);

    /// <summary>Outstanding crime.</summary>
    public required long Crime { get; init; }

    /// <summary>Unspent attribute points.</summary>
    public required int AttributePoints { get; init; }

    /// <summary>Unspent skill points.</summary>
    public required int SkillPoints { get; init; }

    /// <summary>Base attribute values indexed by attribute id; index 0 is the unused slot.</summary>
    public required IReadOnlyList<int> Attributes { get; init; }

    /// <summary>Base skill values indexed by skill id; index 0 is the unused slot.</summary>
    public required IReadOnlyList<int> Skills { get; init; }

    /// <summary>The values the character was created with, same indexing.</summary>
    public required IReadOnlyList<int> StartingSkills { get; init; }

    /// <summary>The record's own per-level experience table, read rather than assumed.</summary>
    public required IReadOnlyList<uint> ExperienceTable { get; init; }

    /// <summary>
    /// Experience the current level started at — the previous row of the table, or 0 at level 1.
    /// Used to show progress towards the next level.
    /// </summary>
    public long ExperienceForThisLevel => ThresholdForLevel(Level);

    /// <summary>
    /// The table's own threshold for reaching <paramref name="level"/>: entry <c>level - 2</c>,
    /// or 0 for level 1, or -1 when the level is outside the table.
    /// </summary>
    public long ThresholdForLevel(int level)
    {
        if (level <= 1) return 0;
        int index = level - 2;
        return index < ExperienceTable.Count ? ExperienceTable[index] : -1;
    }
}

/// <summary>Reads a validated character record into a <see cref="CharacterSnapshot"/>.</summary>
public static class CharacterReader
{
    /// <summary>
    /// Snapshots the record at <paramref name="record"/>. Returns null when the record could not be
    /// read whole — the caller treats that as "the game went away" rather than showing stale data.
    /// </summary>
    public static CharacterSnapshot? Read(IMemorySource source, uint record)
    {
        ArgumentNullException.ThrowIfNull(source);

        var buffer = new byte[QuestLayout.RecordBytes];
        if (source.Read(record, buffer, buffer.Length) != buffer.Length) return null;

        // A malformed or unreadable name means this is not a character record any more — the heap
        // block was reused, or a spilled buffer went away between the locate and this read. Falling
        // back to "" would show an empty name and claim the read succeeded, which is worse than
        // saying nothing: the caller treats null as "the game went away" and stops.
        string? name = StdString.Read(source, buffer, (int)QuestLayout.Name);
        if (name is null) return null;

        var attributes = new int[GameFacts.AttributeSlots];
        for (int id = 0; id < attributes.Length; id++)
            attributes[id] = BitConverter.ToUInt16(buffer, (int)QuestLayout.BaseAttributes + id * 2);

        var skills = new int[GameFacts.SkillSlots];
        var starting = new int[GameFacts.SkillSlots];
        for (int id = 0; id < skills.Length; id++)
        {
            skills[id] = BitConverter.ToUInt16(buffer, (int)QuestLayout.BaseSkills + id * 2);
            starting[id] = BitConverter.ToUInt16(buffer, (int)QuestLayout.StartingSkills + id * 2);
        }

        var table = new uint[GameFacts.ExperienceTableEntries];
        for (int i = 0; i < table.Length; i++)
            table[i] = BitConverter.ToUInt32(buffer, (int)QuestLayout.ExperienceTable + i * 4);

        return new CharacterSnapshot
        {
            Record = record,
            Name = name,
            PortraitId = StdString.Read(source, buffer, (int)QuestLayout.PortraitId) ?? "",
            RaceId = BitConverter.ToUInt32(buffer, (int)QuestLayout.Race),
            Level = BitConverter.ToUInt16(buffer, (int)QuestLayout.Level),
            Experience = BitConverter.ToUInt32(buffer, (int)QuestLayout.Experience),
            ExperienceForNextLevel = BitConverter.ToUInt32(buffer, (int)QuestLayout.ExperienceForNextLevel),
            Health = BitConverter.ToUInt16(buffer, (int)QuestLayout.Health),
            Mana = BitConverter.ToUInt16(buffer, (int)QuestLayout.Mana),
            Gold = BitConverter.ToUInt32(buffer, (int)QuestLayout.Gold),
            Fame = BitConverter.ToInt16(buffer, (int)QuestLayout.Fame),
            Crime = BitConverter.ToUInt32(buffer, (int)QuestLayout.Crime),
            AttributePoints = BitConverter.ToUInt16(buffer, (int)QuestLayout.AttributePoints),
            SkillPoints = BitConverter.ToUInt16(buffer, (int)QuestLayout.SkillPoints),
            Attributes = attributes,
            Skills = skills,
            StartingSkills = starting,
            ExperienceTable = table,
        };
    }
}
