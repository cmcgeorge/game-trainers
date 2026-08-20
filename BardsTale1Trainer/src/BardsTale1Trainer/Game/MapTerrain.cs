namespace BardsTale1Trainer.Game;

/// <summary>
/// What sits on one edge of a map square. Bard's Tale 1 records a wall on each side of a
/// square separately, so the two halves of a shared edge can disagree — a door on one side
/// and solid stone on the other is the game's one-way passage. The <c>OneWay…</c> kinds keep
/// that, with "forward" meaning the direction the map is <em>drawn</em> in: east through a
/// vertical edge, south through a horizontal one.
///
/// <para>Which way a one-way edge opens is read off the side that records the doorway — i.e.
/// on the reading that a step is gated by the square being left, not the one being entered.
/// That is inferred from the shape of the data and has not been confirmed against the running
/// game; if it turns out to be the other way round, every arrow is simply reversed.</para>
/// </summary>
public enum WallKind
{
    None,
    Wall,
    Door,
    SecretDoor,
    /// <summary>A door you can only walk through eastward (vertical edge) / southward (horizontal edge).</summary>
    OneWayDoor,
    /// <summary>A door you can only walk through westward (vertical edge) / northward (horizontal edge).</summary>
    OneWayDoorReversed,
    /// <summary>A secret door passable only eastward / southward.</summary>
    OneWaySecretDoor,
    /// <summary>A secret door passable only westward / northward.</summary>
    OneWaySecretDoorReversed,
}

/// <summary>
/// What occupies a square. Dungeon squares are always <see cref="Open"/> — what stops the
/// party there is a wall on an edge. City squares have no edge walls at all: a building is a
/// <see cref="Blocked"/> square, and the rest name the service that trades there so the
/// street map reads like the ones players drew by hand.
/// </summary>
public enum SquareFeature
{
    Open,
    /// <summary>A building or other obstruction — the party cannot stand here.</summary>
    Blocked,
    GateOpen,
    GateLocked,
    Temple,
    Tavern,
    Casino,
    Guild,
    Garths,
    Review,
    Roscoes,
    Bank,
    ThievesTemple,
}

/// <summary>
/// One decoded map square: its west and north walls plus what stands on it, and — only where
/// the square sits on the map's own east or south rim — that outer wall too. An interior east
/// or south edge is the neighbour's west or north edge, so it is left <see cref="WallKind.None"/>
/// rather than drawn twice. The grids come from <see cref="MapTerrainData"/>.
/// </summary>
public readonly record struct BoardSquare(
    WallKind West, WallKind North, SquareFeature Feature,
    WallKind East = WallKind.None, WallKind South = WallKind.None)
{
    /// <summary>True when the square itself is the barrier — a city building, not a walled edge.</summary>
    public bool IsBlocked => Feature == SquareFeature.Blocked;

    /// <summary>The two-or-three letter tag drawn on the square, or null for plain ground.</summary>
    public string? Label => Feature switch
    {
        SquareFeature.GateOpen => "GTE",
        SquareFeature.GateLocked => "LCK",
        SquareFeature.Temple => "TMP",
        SquareFeature.Tavern => "TAV",
        SquareFeature.Casino => "CAS",
        SquareFeature.Guild => "GLD",
        SquareFeature.Garths => "GAR",
        SquareFeature.Review => "REV",
        SquareFeature.Roscoes => "ROS",
        SquareFeature.Bank => "BNK",
        SquareFeature.ThievesTemple => "THV",
        _ => null,
    };

    /// <summary>Long name of whatever stands on the square, or null for plain ground.</summary>
    public string? Description => Feature switch
    {
        SquareFeature.Blocked => "building — impassable",
        SquareFeature.GateOpen => "city gate (open)",
        SquareFeature.GateLocked => "city gate (locked)",
        SquareFeature.Temple => "Temple",
        SquareFeature.Tavern => "Tavern",
        SquareFeature.Casino => "Casino",
        SquareFeature.Guild => "Adventurer's Guild",
        SquareFeature.Garths => "Garth's Equipment Shoppe",
        SquareFeature.Review => "Review Board",
        SquareFeature.Roscoes => "Roscoe's Energy Emporium",
        SquareFeature.Bank => "Bank",
        SquareFeature.ThievesTemple => "Thieves' temple",
        _ => null,
    };
}

/// <summary>Helpers for reasoning about a <see cref="WallKind"/> without listing every member.</summary>
public static class WallKinds
{
    /// <summary>True when something is drawn on this edge at all.</summary>
    public static bool IsDrawn(this WallKind kind) => kind != WallKind.None;

    /// <summary>True when the edge is a doorway of some sort rather than solid stone.</summary>
    public static bool IsDoorway(this WallKind kind) => kind is
        WallKind.Door or WallKind.SecretDoor or WallKind.OneWayDoor or
        WallKind.OneWayDoorReversed or WallKind.OneWaySecretDoor or WallKind.OneWaySecretDoorReversed;

    /// <summary>True when the doorway only opens one way.</summary>
    public static bool IsOneWay(this WallKind kind) => kind is
        WallKind.OneWayDoor or WallKind.OneWayDoorReversed or
        WallKind.OneWaySecretDoor or WallKind.OneWaySecretDoorReversed;

    /// <summary>True when the doorway is a secret one (including the one-way variants).</summary>
    public static bool IsSecret(this WallKind kind) => kind is
        WallKind.SecretDoor or WallKind.OneWaySecretDoor or WallKind.OneWaySecretDoorReversed;

    /// <summary>
    /// Plain-English name for the status line. The one-way names give the direction as drawn,
    /// which is east/south for a vertical/horizontal edge respectively.
    /// </summary>
    public static string? Describe(this WallKind kind) => kind switch
    {
        WallKind.Wall => "wall",
        WallKind.Door => "door",
        WallKind.SecretDoor => "secret door",
        WallKind.OneWayDoor => "one-way door, east/south only",
        WallKind.OneWayDoorReversed => "one-way door, west/north only",
        WallKind.OneWaySecretDoor => "one-way secret door, east/south only",
        WallKind.OneWaySecretDoorReversed => "one-way secret door, west/north only",
        _ => null,
    };

    /// <summary>
    /// For a one-way doorway, +1 when it opens the way the map is drawn (east through a
    /// vertical edge, south through a horizontal one) and -1 when it opens the other way.
    /// 0 for everything else.
    /// </summary>
    public static int OneWaySign(this WallKind kind) => kind switch
    {
        WallKind.OneWayDoor or WallKind.OneWaySecretDoor => 1,
        WallKind.OneWayDoorReversed or WallKind.OneWaySecretDoorReversed => -1,
        _ => 0,
    };
}
