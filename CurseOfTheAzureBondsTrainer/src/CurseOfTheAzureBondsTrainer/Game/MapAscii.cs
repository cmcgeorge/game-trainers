namespace CurseOfTheAzureBondsTrainer.Game;

/// <summary>
/// Parses the ASCII map strings used in the strategy guide into <see cref="BoardSquare"/> grids.
///
/// Format (each row is a string, first 3 chars are the row-label prefix and are ignored):
///   rows[0]         = top boundary row  (all north walls for y=0)
///   rows[1 + y*2]   = interior row y    (cell content + east-wall separators)
///   rows[2 + y*2]   = boundary row y    (south walls of row y = north walls of row y+1)
///   rows[h*2]       = bottom boundary   (the map's outer south wall — the last boundary row)
///
/// Within a stripped row (49 chars for a 16-wide map, indices 0..48):
///   index x*3       = west separator of column x  (left boundary for x=0, interior for x>0)
///   index x*3+1..2  = 2-char cell content of column x
///   index x*3+3     = east separator of column x  (= west separator of column x+1)
///
/// Only the west and north edge of each square is stored, since an interior east/south edge is
/// simply the neighbour's west/north edge. The map's own outer east and south walls have no such
/// neighbour, so they are read from the last separator of each interior row (index width*3) and
/// from the bottom boundary row, and kept on the edge squares.
///
/// Wall chars: '#'=Wall  '+'=Door  '~'=SecretDoor  ' '=None
/// Floor chars: "##"=Stone (solid/unreachable)  "~~"=Water  else=Normal
/// </summary>
public static class MapAscii
{
    public static BoardSquare[,] Parse(string[] rows, int width = 16, int height = 16)
    {
        var board = new BoardSquare[width, height];
        string southBound = height * 2 < rows.Length ? Strip(rows[height * 2]) : "";

        for (int y = 0; y < height; y++)
        {
            string inter = Strip(rows[1 + y * 2]);
            string northBound = Strip(rows[y == 0 ? 0 : y * 2]);

            for (int x = 0; x < width; x++)
            {
                WallKind west  = Wall(inter, x * 3);
                WallKind north = SouthWall(northBound, x);
                FloorKind floor = Floor(inter, x);
                WallKind east  = x == width - 1  ? Wall(inter, width * 3)      : WallKind.None;
                WallKind south = y == height - 1 ? SouthWall(southBound, x)    : WallKind.None;
                board[x, y] = new BoardSquare(west, north, floor, east, south);
            }
        }

        return board;
    }

    // Strip the 3-char row-label prefix.
    static string Strip(string s) => s.Length >= 3 ? s[3..] : s;

    // Character at the given index represents a wall edge.
    static WallKind Wall(string row, int idx) =>
        idx < row.Length ? row[idx] switch {
            '#' => WallKind.Wall,
            '+' => WallKind.Door,
            '~' => WallKind.SecretDoor,
            _ => WallKind.None
        } : WallKind.None;

    // The south-wall of column x lives at indices 1+x*3 and 2+x*3 in a boundary row.
    // The wall is present when either char is '#'/'+' / '~'.
    static WallKind SouthWall(string row, int x)
    {
        int p = 1 + x * 3;
        char c1 = p     < row.Length ? row[p]     : ' ';
        char c2 = p + 1 < row.Length ? row[p + 1] : ' ';
        if (c1 == '#' || c2 == '#') return WallKind.Wall;
        if (c1 == '+' || c2 == '+') return WallKind.Door;
        if (c1 == '~' || c2 == '~') return WallKind.SecretDoor;
        return WallKind.None;
    }

    // Cell content at indices 1+x*3, 2+x*3.
    static FloorKind Floor(string row, int x)
    {
        int p = 1 + x * 3;
        char c1 = p     < row.Length ? row[p]     : ' ';
        char c2 = p + 1 < row.Length ? row[p + 1] : ' ';
        if (c1 == '#' && c2 == '#') return FloorKind.Stone;
        if (c1 == '~' && c2 == '~') return FloorKind.Water;
        return FloorKind.Normal;
    }
}
