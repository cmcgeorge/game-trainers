namespace MightAndMagic1Trainer.Game;

/// <summary>
/// What one side of a square presents to the party, ordered from what you can walk through to what
/// you cannot.
///
/// <para>This is the two maze planes read together: <see cref="EdgeKind"/> is the passability plane
/// alone, and the case it cannot express is the one that matters most —
/// <see cref="EdgeFace.Illusory"/>, where a wall is <i>drawn</i> and you may walk through it
/// anyway.</para>
///
/// <para>The order is load-bearing: where the two squares either side of an edge disagree,
/// <see cref="MazeMap.Stronger"/> keeps the higher value, so a plan draws the more solid of the two
/// and a player is never told a wall is a door.</para>
/// </summary>
public enum EdgeFace
{
    /// <summary>Nothing drawn and nothing in the way.</summary>
    None = 0,

    /// <summary>A wall is drawn and you can walk straight through it.</summary>
    Illusory = 1,

    /// <summary>Passable, and flagged by the game: secret, one-way, or a trigger.</summary>
    Special = 2,

    /// <summary>A door.</summary>
    Door = 3,

    /// <summary>Solid.</summary>
    Wall = 4,
}

/// <summary>What a maze is made of, counted once per edge rather than once per side.</summary>
/// <param name="Walls">Edges you cannot pass.</param>
/// <param name="Doors">Doors.</param>
/// <param name="Special">Passable edges the game has flagged.</param>
/// <param name="Illusory">Walls that are drawn but walkable.</param>
/// <param name="OneWay">Edges the two squares either side of disagree about.</param>
public sealed record WallCounts(int Walls, int Doors, int Special, int Illusory, int OneWay)
{
    /// <summary>"270 walls, 10 doors, 31 illusory walls" — the empty categories left out.</summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (Walls > 0) parts.Add($"{Walls} walls");
            if (Doors > 0) parts.Add($"{Doors} doors");
            if (Special > 0) parts.Add($"{Special} flagged edges");
            if (Illusory > 0) parts.Add($"{Illusory} illusory walls");
            if (OneWay > 0) parts.Add($"{OneWay} one-way");
            return parts.Count == 0 ? "nothing but open floor" : string.Join(", ", parts);
        }
    }
}

/// <summary>
/// What a decoded maze <em>means</em>, as opposed to how it is stored.
///
/// <para>These are queries over the two planes rather than more decoding, and they live beside the
/// maze rather than inside whatever draws it because two things now ask them: the cluebook's plans
/// and the trainer's own Map (drawn) tab. When the map tab and the cluebook disagree about how many
/// secret passages a place has, one of them is wrong — so they count in the same place.</para>
///
/// <para>The one rule worth stating twice: <b>an edge is counted once, not once per side.</b> Every
/// interior edge belongs to two squares, and the game is free to record it differently from each —
/// about 1.6% of them are, which is what a one-way door is.</para>
/// </summary>
public sealed partial class MazeMap
{
    /// <summary>
    /// Whether this is one of the twenty surface areas.
    ///
    /// <b>It changes what an illusory wall means.</b> Indoors, a drawn wall you can walk through is a
    /// secret passage and worth a coordinate. Outdoors it is terrain — scrub, trees, the edge of a
    /// wood — which is why a surface area has between 89 and 257 of them against a town's thirty.
    /// Anything that presents them to a player has to tell the two apart or it is teaching the reader
    /// to ignore a list that matters.
    /// </summary>
    public bool IsOutdoor => RawName.StartsWith("area", StringComparison.Ordinal);

    /// <summary>What is drawn along one side of square (<paramref name="x"/>, <paramref name="y"/>).</summary>
    /// <param name="dir">0 = west, 1 = north, 2 = east, 3 = south, as the maze records pack them.</param>
    public EdgeFace Face(int x, int y, int dir) => Edge(x, y, dir) switch
    {
        EdgeKind.Wall => EdgeFace.Wall,
        EdgeKind.Door => EdgeFace.Door,
        EdgeKind.Special => EdgeFace.Special,
        _ => IsIllusory(x, y, dir) ? EdgeFace.Illusory : EdgeFace.None,
    };

    /// <summary>
    /// The edge on column boundary <paramref name="vx"/> (0..<see cref="Size"/>) of row
    /// <paramref name="y"/>, as the squares west and east of it each see it.
    /// </summary>
    public (EdgeFace West, EdgeFace East) VerticalEdge(int vx, int y)
    {
        var west = vx > 0 ? Face(vx - 1, y, 2) : (EdgeFace?)null;      // east side of the square to the west
        var east = vx < Size ? Face(vx, y, 0) : (EdgeFace?)null;       // west side of the square to the east
        return (west ?? east ?? EdgeFace.None, east ?? west ?? EdgeFace.None);
    }

    /// <summary>
    /// The edge on row boundary <paramref name="hy"/> (0..<see cref="Size"/>) of column
    /// <paramref name="x"/>, as the squares south and north of it each see it.
    /// </summary>
    public (EdgeFace South, EdgeFace North) HorizontalEdge(int hy, int x)
    {
        var south = hy > 0 ? Face(x, hy - 1, 1) : (EdgeFace?)null;     // north side of the square below
        var north = hy < Size ? Face(x, hy, 3) : (EdgeFace?)null;      // south side of the square above
        return (south ?? north ?? EdgeFace.None, north ?? south ?? EdgeFace.None);
    }

    /// <summary>The more solid of two views of one edge — what has to be drawn when they differ.</summary>
    public static EdgeFace Stronger(EdgeFace a, EdgeFace b) => a > b ? a : b;

    /// <summary>Counts what the maze is made of, one count per edge.</summary>
    public WallCounts Counts()
    {
        int walls = 0, doors = 0, special = 0, illusory = 0, oneWay = 0;

        Walk((a, b, _, _, _) =>
        {
            if (a != b) oneWay++;
            switch (Stronger(a, b))
            {
                case EdgeFace.Wall: walls++; break;
                case EdgeFace.Door: doors++; break;
                case EdgeFace.Special: special++; break;
                case EdgeFace.Illusory: illusory++; break;
            }
        });

        return new WallCounts(walls, doors, special, illusory, oneWay);
    }

    /// <summary>
    /// Every edge with a wall drawn on it that you can walk straight through, once per edge.
    ///
    /// <para>Each is named by a square and the direction to walk out of it, so that following the
    /// list never needs a map. Where an edge has two squares the east or north one is named, which is
    /// arbitrary but consistent — the passage works from both sides.</para>
    ///
    /// <para><b>Outdoors these are terrain, not secrets</b>; see <see cref="IsOutdoor"/>.</para>
    /// </summary>
    public IReadOnlyList<(int X, int Y, int Dir)> SecretPassages()
    {
        var found = new List<(int, int, int)>();

        Walk((a, b, boundary, along, vertical) =>
        {
            if (Stronger(a, b) != EdgeFace.Illusory) return;

            if (vertical) found.Add(boundary < Size ? (boundary, along, 0) : (Size - 1, along, 2));
            else found.Add(boundary < Size ? (along, boundary, 3) : (along, Size - 1, 1));
        });

        return found;
    }

    /// <summary>The ways out of one square that go through a drawn wall.</summary>
    public IReadOnlyList<int> WalkThroughSides(int x, int y)
    {
        var ways = new List<int>();
        for (int dir = 0; dir < 4; dir++)
            if (Face(x, y, dir) == EdgeFace.Illusory) ways.Add(dir);
        return ways;
    }

    /// <summary>"west", "north", "east", "south" for the direction a maze record packs at that index.</summary>
    public static string DirectionName(int dir) => dir switch
    {
        0 => "west", 1 => "north", 2 => "east", _ => "south",
    };

    /// <summary>
    /// Visits every edge of the maze exactly once, giving both squares' view of it, the boundary it
    /// lies on (0..<see cref="Size"/>), the row or column it lies along, and which way it runs.
    /// </summary>
    private void Walk(Action<EdgeFace, EdgeFace, int, int, bool> edge)
    {
        for (int y = 0; y < Size; y++)
        {
            for (int vx = 0; vx <= Size; vx++)
            {
                var (west, east) = VerticalEdge(vx, y);
                edge(west, east, vx, y, true);
            }
        }

        for (int hy = 0; hy <= Size; hy++)
        {
            for (int x = 0; x < Size; x++)
            {
                var (south, north) = HorizontalEdge(hy, x);
                edge(south, north, hy, x, false);
            }
        }
    }
}
