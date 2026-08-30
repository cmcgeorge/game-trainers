namespace EyeOfTheBeholder1Trainer.Game;

public enum CellKind
{
    Wall,
    Floor,
}

public sealed record DungeonPoi(int X, int Y, string Name, string Description)
{
    public string Position => $"({X}, {Y})";
}

public sealed record DungeonLevel(int Index, string Name, string Description, CellKind[,] Grid,
    IReadOnlyList<DungeonPoi> Pois)
{
    public int Width => GameFacts.LevelGridSize;
    public int Height => GameFacts.LevelGridSize;
}
