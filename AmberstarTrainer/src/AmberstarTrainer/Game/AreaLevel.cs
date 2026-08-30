namespace AmberstarTrainer.Game;

public enum AreaCellKind
{
    Wall,
    Floor,
    Water,
    Mountain,
    Forest,
    Desert,
}

public sealed record AreaPoi(int X, int Y, string Name, string Description)
{
    public string Position => $"({X}, {Y})";
}

public sealed record AreaLevel(int Index, string Name, string Description, AreaCellKind[,] Grid,
    IReadOnlyList<AreaPoi> Pois)
{
    public int Width => Grid.GetLength(0);
    public int Height => Grid.GetLength(1);
}
