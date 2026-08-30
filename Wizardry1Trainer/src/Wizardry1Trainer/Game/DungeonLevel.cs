namespace Wizardry1Trainer.Game;

/// <summary>What occupies a dungeon square.</summary>
public enum CellKind
{
    /// <summary>Wall — impassable.</summary>
    Wall,
    /// <summary>Open floor — walkable.</summary>
    Floor,
}

/// <summary>A point of interest on a dungeon level.</summary>
public sealed record DungeonPoi(int X, int Y, string Name, string Description)
{
    /// <summary>"(x, y)" for display.</summary>
    public string Position => $"({X}, {Y})";
}

/// <summary>
/// One dungeon level of Wizardry 1: a 20×20 grid of wall/floor cells plus the points of
/// interest (stairs, elevator, items) that sit on it. Row 0 is the north edge, column 0
/// the west edge, matching the game's own coordinate system.
/// </summary>
public sealed record DungeonLevel(int Index, string Name, string Description, CellKind[,] Grid,
    IReadOnlyList<DungeonPoi> Pois)
{
    public int Width => GameFacts.MazeSize;
    public int Height => GameFacts.MazeSize;
}
