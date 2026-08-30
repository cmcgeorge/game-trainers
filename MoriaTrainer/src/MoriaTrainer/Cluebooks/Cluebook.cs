using MoriaTrainer.Game;

namespace MoriaTrainer.Cluebooks;

public sealed class CluebookOptions
{
    public bool IncludeLevels { get; init; } = true;
    public bool IncludeRacesAndClasses { get; init; } = true;
    public bool IncludeSpells { get; init; } = true;
    public bool IncludeItems { get; init; } = true;
    public bool IncludeBestiary { get; init; } = true;
    public bool IncludeWalkthrough { get; init; } = true;
    public bool IncludeStrategy { get; init; } = true;
}

public sealed class Cluebook
{
    public required CluebookOptions Options { get; init; }
    public required IReadOnlyList<LevelInfo> Levels { get; init; }

    public static Cluebook Build(CluebookOptions? options = null) =>
        new()
        {
            Options = options ?? new CluebookOptions(),
            Levels = LevelBook.Levels,
        };
}
