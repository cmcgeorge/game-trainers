using Wizardry1Trainer.Game;

namespace Wizardry1Trainer.Cluebooks;

/// <summary>What to include in the cluebook.</summary>
public sealed class CluebookOptions
{
    public bool IncludeMaps { get; init; } = true;
    public bool IncludeSpells { get; init; } = true;
    public bool IncludeClasses { get; init; } = true;
    public bool IncludeWalkthrough { get; init; } = true;
    public bool IncludeStrategy { get; init; } = true;
    public int MapCellSize { get; init; } = 20;
}

/// <summary>The assembled cluebook data.</summary>
public sealed class Cluebook
{
    public required CluebookOptions Options { get; init; }
    public required IReadOnlyList<DungeonLevel> Levels { get; init; }

    public static Cluebook Build(CluebookOptions? options = null) =>
        new()
        {
            Options = options ?? new CluebookOptions(),
            Levels = DungeonData.Levels,
        };
}
