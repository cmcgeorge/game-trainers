using CurseOfTheAzureBondsTrainer.Game;

namespace CurseOfTheAzureBondsTrainer.Cluebooks;

public sealed class CluebookOptions
{
    public bool IncludeMaps { get; init; } = true;
    public bool IncludeSpells { get; init; } = true;
    public bool IncludeClasses { get; init; } = true;
    public bool IncludeWalkthrough { get; init; } = true;
    public bool IncludeStrategy { get; init; } = true;
    public int MapCellSize { get; init; } = 24;
}

public sealed class Cluebook
{
    public required CluebookOptions Options { get; init; }
    public required IReadOnlyList<MapArea> Areas { get; init; }
    public required IReadOnlyList<SpellInfo> Spells { get; init; }
    public required IReadOnlyList<ClassInfo> Classes { get; init; }
    public required IReadOnlyList<RaceInfo> Races { get; init; }
    public required IReadOnlyList<WalkthroughSection> Walkthrough { get; init; }

    public static Cluebook Build(CluebookOptions? options = null) =>
        new()
        {
            Options = options ?? new CluebookOptions(),
            Areas = MapBook.Areas,
            Spells = SpellBook.All,
            Classes = ClassRaceBook.Classes,
            Races = ClassRaceBook.Races,
            Walkthrough = Game.Walkthrough.Sections,
        };
}
