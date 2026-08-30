namespace AlternateRealityTrainer.Cluebooks;

public sealed class CluebookOptions
{
    public bool IncludeCityMap { get; init; } = true;
    public bool IncludeAttributes { get; init; } = true;
    public bool IncludePotions { get; init; } = true;
    public bool IncludeSurvival { get; init; } = true;
    public bool IncludeStrategy { get; init; } = true;
    public int MapCellSize { get; init; } = 12;
}

public sealed class Cluebook
{
    public required CluebookOptions Options { get; init; }

    public static Cluebook Build(CluebookOptions? options = null) =>
        new()
        {
            Options = options ?? new CluebookOptions(),
        };
}
