using LegendOfGrimrock1Trainer.Game;

namespace LegendOfGrimrock1Trainer.Cluebooks;

public sealed class CluebookOptions
{
    public bool IncludeOverview { get; init; } = true;
    public bool IncludeDungeon { get; init; } = true;
    public bool IncludeCharacters { get; init; } = true;
    public bool IncludeSpells { get; init; } = true;
    public bool IncludeSkills { get; init; } = true;
    public bool IncludeEquipment { get; init; } = true;
    public bool IncludeBestiary { get; init; } = true;
    public bool IncludeWalkthrough { get; init; } = true;
    public bool IncludeStrategy { get; init; } = true;
}

public sealed record DungeonLevelInfo(int Number, string Name, string Description);

public sealed class Cluebook
{
    public required CluebookOptions Options { get; init; }
    public required IReadOnlyList<DungeonLevelInfo> Levels { get; init; }

    public static Cluebook Build(CluebookOptions? options = null) => new()
    {
        Options = options ?? new CluebookOptions(),
        Levels = GameTables.CampaignLevelNames
            .Select((name, index) => new DungeonLevelInfo(index + 1, name, LevelDescriptions[index]))
            .ToArray(),
    };

    private static readonly string[] LevelDescriptions =
    {
        "Tutorial level. Learn to sidestep snails and herders while you explore the first dungeon mechanisms.",
        "The first proper fights and pressure-plate puzzles. Keep food and torches in reserve.",
        "Skeleton territory. A mace user is especially useful here.",
        "The largest scripted level in the campaign. Expect puzzle-solving and careful observation.",
        "Open hallways and ranged enemies reward movement rather than standing still.",
        "Pits, traps, and false floors. Read the floor before committing the party.",
        "Ancient chambers guarded by Uggardians. Cold damage and Fire Shield are valuable.",
        "A treasure vault protected by locks and challenges.",
        "The first Goromorg temple. Break shields, then use concentrated damage.",
        "A deeper, harder Goromorg temple with more dangerous encounters.",
        "Undead appear in force. Keep the group supplied and ready to retreat.",
        "The prison and the Warden's domain. Bring every useful consumable.",
        "The cemetery and the final stretch of the descent.",
    };
}
