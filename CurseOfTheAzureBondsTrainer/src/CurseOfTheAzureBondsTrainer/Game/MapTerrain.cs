namespace CurseOfTheAzureBondsTrainer.Game;

/// <summary>What sits on one edge of a map square: nothing, a solid wall, a door, or a secret (illusory) passage.</summary>
public enum WallKind
{
    None,
    Wall,
    Door,
    SecretDoor,
}

/// <summary>
/// What the schematic paints on a square. Every Curse level is an indoor 16x16 grid, so these are
/// all derived from the level's own geometry: <see cref="Stone"/> is a square the party can never
/// reach (sealed on all four sides, or cut off from the level's main walkable region) and
/// <see cref="Water"/> is available for a level that needs it.
/// </summary>
public enum FloorKind
{
    Normal,
    Water,
    Stone,
}

/// <summary>
/// One decoded map square: its west/north walls and its floor terrain, plus the map's outer east
/// and south walls where the square sits on that edge (interior east/south edges are the neighbour's
/// west/north edge, so they are left <see cref="WallKind.None"/> to avoid drawing every line twice).
/// The data comes from the game's own level geometry — see <see cref="MapTerrainData"/>.
/// </summary>
public readonly record struct BoardSquare(
    WallKind West, WallKind North, FloorKind Floor,
    WallKind East = WallKind.None, WallKind South = WallKind.None);
