namespace PoolOfRadianceTrainer.Game;

/// <summary>What sits on one edge of a map square: nothing, a solid wall, a door, or a secret (illusory) passage.</summary>
public enum WallKind
{
    None,
    Wall,
    Door,
    SecretDoor,
}

/// <summary>
/// What the schematic paints on a square. The first three are the indoor kinds derived from the
/// level geometry; the rest are the overland terrain types of the wilderness map, which has no
/// walls at all — see <see cref="WildernessMap"/>.
/// </summary>
public enum FloorKind
{
    Normal,
    Water,
    Stone,

    // --- wilderness (overland Moonsea map) ---
    /// <summary>Off-map, or a square the transcribed overland map does not cover.</summary>
    Unknown,
    Plains,
    Swamp,
    Forest,
    Hills,
    Mountains,
    River,
    DeepWater,
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
