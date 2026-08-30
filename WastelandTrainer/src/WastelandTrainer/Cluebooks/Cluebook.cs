using WastelandTrainer.Game;

namespace WastelandTrainer.Cluebooks;

public sealed class CluebookOptions
{
    public bool IncludeAreas { get; init; } = true;
    public bool IncludeSkills { get; init; } = true;
    public bool IncludeItems { get; init; } = true;
    public bool IncludeWalkthrough { get; init; } = true;
    public bool IncludeStrategy { get; init; } = true;
}

public sealed class Cluebook
{
    public required CluebookOptions Options { get; init; }
    public required IReadOnlyList<MapArea> Areas { get; init; }

    public static Cluebook Build(CluebookOptions? options = null) =>
        new()
        {
            Options = options ?? new CluebookOptions(),
            Areas = MapBook.Areas,
        };
}
