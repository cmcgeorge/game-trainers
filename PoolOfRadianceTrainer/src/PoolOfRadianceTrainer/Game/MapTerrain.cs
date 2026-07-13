namespace PoolOfRadianceTrainer.Game;

/// <summary>What sits on one edge of a map square: nothing, a solid wall, or a door.</summary>
public enum WallKind
{
    None,
    Wall,
    Door,
}

/// <summary>The floor terrain of a square as far as the schematic distinguishes it.</summary>
public enum FloorKind
{
    Normal,
    Water,
    Stone,
}

/// <summary>
/// One decoded map square: its west/north walls and its floor terrain. Mirrors the Dragon Wars
/// trainer's <c>BoardSquare</c> so the Maps schematic can draw walls/water when a terrain decoder
/// is available. Pool of Radiance's Gold Box level geometry is not yet decoded, so the Maps tab
/// currently renders the grid + keyed locations + live position without terrain overlay.
/// </summary>
public readonly record struct BoardSquare(WallKind West, WallKind North, FloorKind Floor);
