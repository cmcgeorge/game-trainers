using Space1889Trainer.Files;

namespace Space1889Trainer.Cluebooks;

/// <summary>Which sections a cluebook includes.</summary>
public sealed class CluebookOptions
{
    public bool IncludeControls { get; init; } = true;
    public bool IncludeWalkthrough { get; init; } = true;
    public bool IncludeSideQuests { get; init; } = true;
    public bool IncludeItems { get; init; } = true;
    public bool IncludeMaps { get; init; } = true;
}

/// <summary>What a cluebook is built from: the options, and the game folder's maps when one was found.</summary>
public sealed class Cluebook
{
    public required CluebookOptions Options { get; init; }

    /// <summary>The player's own map files, or null (the maps section then explains how to get it).</summary>
    public MapLibrary? Maps { get; init; }

    /// <summary>Builds a cluebook; <paramref name="gameFolder"/> is used for the maps when it holds the game.</summary>
    public static Cluebook Build(CluebookOptions? options = null, string? gameFolder = null) =>
        new()
        {
            Options = options ?? new CluebookOptions(),
            Maps = GameFolder.IsGameFolder(gameFolder) ? new MapLibrary(gameFolder!) : null,
        };
}
