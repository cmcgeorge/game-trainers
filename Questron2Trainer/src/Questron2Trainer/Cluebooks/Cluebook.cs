using Questron2Trainer.Game;

namespace Questron2Trainer.Cluebooks;

public sealed class CluebookOptions
{
    public bool IncludeMaps { get; init; } = true;
    public bool IncludeSpells { get; init; } = true;
    public bool IncludeEquipment { get; init; } = true;
    public bool IncludeWalkthrough { get; init; } = true;
    public bool IncludeStrategy { get; init; } = true;
    public int MapCellSize { get; init; } = 20;
}

public sealed class Cluebook
{
    public required CluebookOptions Options { get; init; }
    public required IReadOnlyList<AreaLevel> Areas { get; init; }

    public static Cluebook Build(CluebookOptions? options = null) => new()
    {
        Options = options ?? new CluebookOptions(),
        Areas = AreaData.Areas,
    };
}
