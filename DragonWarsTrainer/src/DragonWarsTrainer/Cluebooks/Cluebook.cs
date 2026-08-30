using DragonWarsTrainer.Game;

namespace DragonWarsTrainer.Cluebooks;

public sealed class CluebookOptions
{
    public bool IncludeAreas { get; init; } = true;
    public bool IncludeSpells { get; init; } = true;
    public bool IncludeSkills { get; init; } = true;
    public bool IncludeWalkthrough { get; init; } = true;
    public bool IncludeStrategy { get; init; } = true;
    public int MapCellSize { get; init; } = 14;
}

public sealed class Cluebook
{
    public required CluebookOptions Options { get; init; }
    public required IReadOnlyList<MapArea> Areas { get; init; }
    public required IReadOnlyList<SpellInfo> Spells { get; init; }
    public required IReadOnlyList<SkillInfo> Skills { get; init; }
    public required IReadOnlyList<WalkthroughSection> Walkthrough { get; init; }

    public static Cluebook Build(CluebookOptions? options = null) =>
        new()
        {
            Options = options ?? new CluebookOptions(),
            Areas = MapBook.Areas,
            Spells = SpellBook.Spells,
            Skills = SkillBook.Skills,
            Walkthrough = DragonWarsTrainer.Game.Walkthrough.Sections,
        };
}
