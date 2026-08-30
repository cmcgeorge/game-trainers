using BardsTale1Trainer.Game;

namespace BardsTale1Trainer.Cluebooks;

public sealed class CluebookOptions
{
    public bool IncludeMaps { get; init; } = true;
    public bool IncludeSpells { get; init; } = true;
    public bool IncludeClasses { get; init; } = true;
    public bool IncludeWalkthrough { get; init; } = true;
    public bool IncludeStrategy { get; init; } = true;
    public int MapCellSize { get; init; } = 18;
}

public sealed record DungeonGuide(GameMap Map, string[] KeyLocations);

public sealed class Cluebook
{
    public required CluebookOptions Options { get; init; }
    public required GameMap City { get; init; }
    public required IReadOnlyList<DungeonGuide> Dungeons { get; init; }

    public static Cluebook Build(CluebookOptions? options = null)
    {
        var maps = MapBook.Maps;
        return new Cluebook
        {
            Options = options ?? new CluebookOptions(),
            City = maps[0],
            Dungeons = maps.Skip(1).Select((map, index) => new DungeonGuide(map, LocationsFor(map, index))).ToArray(),
        };
    }

    private static string[] LocationsFor(GameMap map, int index) => map.Name switch
    {
        "Wine Cellar" => new[] { "Scarlet Bard entrance", "Stairway to the Sewers" },
        "Sewers — level 1" => new[] { "Wine Cellar stairs", "Sewer junctions", "Stairs to level 2" },
        "Sewers — level 2" => new[] { "Sewer level 1 stairs", "Stairs to level 3" },
        "Sewers — level 3" => new[] { "Deep sewer passages", "Return stairs" },
        "Catacombs — level 1" => new[] { "Temple entrance", "The dead god's name", "Stairs to level 2" },
        "Catacombs — level 2" => new[] { "Catacomb stairs", "Undead encounters", "Stairs to level 3" },
        "Catacombs — level 3" => new[] { "Deep catacombs", "The catacomb treasure", "Return stairs" },
        "Harkyn's Castle — level 1" => new[] { "Castle entrance", "Guarded stairs", "Castle corridors" },
        "Harkyn's Castle — level 2" => new[] { "Castle stairs", "Locked rooms", "Stairs to the upper level" },
        "Harkyn's Castle — level 3" => new[] { "Castle upper halls", "Harkyn's treasure", "Return stairs" },
        "Kylearan's Tower" => new[] { "Tower entrance", "Kylearan's encounters", "Tower treasure" },
        "Mangar's Tower — level 1" => new[] { "Tower entrance", "Stairs upward", "Mangar's defenses" },
        "Mangar's Tower — level 2" => new[] { "Tower stairs", "Teleporters and traps", "Stairs upward" },
        "Mangar's Tower — level 3" => new[] { "Tower maze", "Powerful guardians", "Stairs upward" },
        "Mangar's Tower — level 4" => new[] { "Upper tower stairs", "Final guardians", "Stairs to Mangar" },
        "Mangar's Tower — level 5" => new[] { "Mangar's sanctum", "Mangar the Dark", "The city key" },
        _ => new[] { $"Level {index + 1} stairs", "Dungeon passages", "Treasure rooms" },
    };
}
