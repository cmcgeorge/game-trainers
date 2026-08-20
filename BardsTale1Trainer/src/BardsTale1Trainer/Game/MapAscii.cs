namespace BardsTale1Trainer.Game;

/// <summary>
/// Parses the ASCII map grids in <see cref="MapTerrainData"/> into <see cref="BoardSquare"/>
/// arrays. Row 0 is the map's north edge and column 0 its west edge.
///
/// <para><b>Dungeon format</b> (<see cref="Parse"/>) — one line per wall row interleaved with
/// one line per square row, so the picture reads like graph paper:</para>
/// <code>
///   rows[0]        top boundary  — the north walls of row 0
///   rows[1 + y*2]  square row y  — wall glyph, then a 2-char square body, repeating
///   rows[2 + y*2]  the boundary below row y = the north walls of row y+1
///   rows[h*2]      bottom boundary — the map's outer south wall
/// </code>
/// <para>Within a row: index <c>x*3</c> is the west edge of column x (the map's west rim for
/// x = 0), <c>x*3+1..2</c> the square itself, and <c>width*3</c> the outer east rim. A
/// boundary row puts <c>'+'</c> on the corners and the horizontal edge glyph in the two
/// characters between them.</para>
///
/// <para><b>Edge glyphs.</b> Vertical (in a square row): <c>' '</c> open, <c>'#'</c> wall,
/// <c>'+'</c> door, <c>'~'</c> secret door, <c>'&gt;'</c>/<c>'&lt;'</c> a door that only opens
/// eastward/westward, <c>')'</c>/<c>'('</c> the same for a secret door. Horizontal (in a
/// boundary row): <c>' '</c>, <c>'#'</c>, <c>'+'</c>, <c>'~'</c> as above, <c>'v'</c>/<c>'^'</c>
/// a door that only opens southward/northward, <c>'V'</c>/<c>'A'</c> the same for a secret
/// door. One-way edges are the game's own asymmetric walls — a door recorded on one square's
/// face and solid stone on the neighbour's. Which way such an edge opens is taken from the side
/// that records the doorway, on the reading that the game gates a step on the square being
/// <em>left</em>; that is inferred from the data, not confirmed against the running game.</para>
///
/// <para><b>Wrap-around rims.</b> Every dungeon level wraps, so the north and south rims are the
/// same physical edge, as are the west and east ones. Each rim therefore carries the merged
/// glyph, and the two always agree — a mismatch would mean a row or column was dropped, which
/// is what the harness checks.</para>
///
/// <para><b>City format</b> (<see cref="ParseCity"/>) — Skara Brae records no edge walls at
/// all, so its grid is simply one line per row with two characters per square: <c>"  "</c>
/// street, <c>"##"</c> building, <c>"GL"</c>/<c>"GO"</c> a locked/open city gate, and a
/// two-letter tag for a service (see <see cref="Feature"/>).</para>
/// </summary>
public static class MapAscii
{
    /// <summary>Parses a dungeon grid — walls on the edges, nothing on the squares.</summary>
    public static BoardSquare[,] Parse(string[] rows, int width = 22, int height = 22)
    {
        var board = new BoardSquare[width, height];
        string southBound = Row(rows, height * 2);

        for (int y = 0; y < height; y++)
        {
            string squares = Row(rows, 1 + y * 2);
            string northBound = Row(rows, y * 2);

            for (int x = 0; x < width; x++)
            {
                board[x, y] = new BoardSquare(
                    West: Vertical(squares, x * 3),
                    North: Horizontal(northBound, x),
                    Feature: SquareFeature.Open,
                    East: x == width - 1 ? Vertical(squares, width * 3) : WallKind.None,
                    South: y == height - 1 ? Horizontal(southBound, x) : WallKind.None);
            }
        }

        return board;
    }

    /// <summary>
    /// Parses a city grid — two characters per square, no wall rows. What stops the party in
    /// Skara Brae is a whole square being a building, so that is all there is to record.
    /// </summary>
    public static BoardSquare[,] ParseCity(string[] rows, int width = 30, int height = 30)
    {
        var board = new BoardSquare[width, height];
        for (int y = 0; y < height; y++)
        {
            string row = Row(rows, y);
            for (int x = 0; x < width; x++)
                board[x, y] = new BoardSquare(WallKind.None, WallKind.None, Feature(row, x * 2));
        }
        return board;
    }

    private static string Row(string[] rows, int index) => index >= 0 && index < rows.Length ? rows[index] : "";

    private static char At(string row, int index) => index >= 0 && index < row.Length ? row[index] : ' ';

    /// <summary>A wall glyph in a square row: the edge between the square west of it and east of it.</summary>
    private static WallKind Vertical(string row, int index) => At(row, index) switch
    {
        '#' => WallKind.Wall,
        '+' => WallKind.Door,
        '~' => WallKind.SecretDoor,
        '>' => WallKind.OneWayDoor,
        '<' => WallKind.OneWayDoorReversed,
        ')' => WallKind.OneWaySecretDoor,
        '(' => WallKind.OneWaySecretDoorReversed,
        _ => WallKind.None,
    };

    /// <summary>
    /// A wall glyph in a boundary row. The edge for column x spans the two characters at
    /// <c>1 + x*3</c>; either of them carrying the glyph counts, so the data stays readable
    /// whether it is written <c>"##"</c> or <c>"# "</c>.
    /// </summary>
    private static WallKind Horizontal(string row, int x)
    {
        int p = 1 + x * 3;
        return Pick(At(row, p), At(row, p + 1));
    }

    private static WallKind Pick(char a, char b)
    {
        var first = Glyph(a);
        return first != WallKind.None ? first : Glyph(b);
    }

    private static WallKind Glyph(char c) => c switch
    {
        '#' => WallKind.Wall,
        '+' => WallKind.Door,
        '~' => WallKind.SecretDoor,
        'v' => WallKind.OneWayDoor,
        '^' => WallKind.OneWayDoorReversed,
        'V' => WallKind.OneWaySecretDoor,
        'A' => WallKind.OneWaySecretDoorReversed,
        _ => WallKind.None,
    };

    /// <summary>The two-character body of a city square.</summary>
    private static SquareFeature Feature(string row, int index) =>
        $"{At(row, index)}{At(row, index + 1)}" switch
        {
            "##" => SquareFeature.Blocked,
            "GL" => SquareFeature.GateLocked,
            "GO" => SquareFeature.GateOpen,
            "TP" => SquareFeature.Temple,
            "TV" => SquareFeature.Tavern,
            "CA" => SquareFeature.Casino,
            "GU" => SquareFeature.Guild,
            "GA" => SquareFeature.Garths,
            "RV" => SquareFeature.Review,
            "RO" => SquareFeature.Roscoes,
            "BK" => SquareFeature.Bank,
            "TH" => SquareFeature.ThievesTemple,
            _ => SquareFeature.Open,
        };
}
