using System.Text;

namespace DarkDesigns1Trainer.Game;

/// <summary>One square of a level, projected for drawing and for the location list.</summary>
public sealed record MapSquare(
    int X, int Y,
    WallKind North, WallKind East, WallKind South, WallKind West,
    int EventCode, SquareKind Kind, bool Visited, string RoomName)
{
    public string Coord => $"({X}, {Y})";

    /// <summary>True when nothing about this square is worth drawing beyond the empty grid.</summary>
    public bool IsBlank =>
        North == WallKind.Open && East == WallKind.Open &&
        South == WallKind.Open && West == WallKind.Open &&
        Kind == SquareKind.Plain && !Visited;
}

/// <summary>
/// One named place on a level: an event code, the room name and description the map file itself
/// carries for it, and every square that triggers it.
/// </summary>
public sealed record MapRoom(int Code, string Name, string Description, IReadOnlyList<MapSquare> Squares)
{
    public MapSquare First => Squares[0];
    public string Coord => First.Coord;
    public string Header => Squares.Count > 1 ? $"{Name}  ({Squares.Count} squares)" : Name;
}

/// <summary>
/// A decoded Dark Designs I level: the 12,648 bytes of a <c>DDMAP&lt;n&gt;.DAT</c> file, or the same
/// bytes read live out of the game's map buffer.
///
/// The level's room names and descriptions are the map file's own text — nothing here is curated,
/// so a modified or fan-made map describes itself correctly.
/// </summary>
public sealed class DungeonMap
{
    private readonly byte[] _bytes;
    private readonly int _base;

    /// <summary>The dungeon level this map is for (1–5), or 0 when it is not known.</summary>
    public int Level { get; }

    public DungeonMap(byte[] bytes, int offset = 0, int level = 0)
    {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        if (offset < 0 || offset + MapFormat.FileSize > bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(offset),
                $"A Dark Designs map needs {MapFormat.FileSize} bytes at the given offset.");
        _bytes = bytes;
        _base = offset;
        Level = level;
    }

    /// <summary>The raw bytes this view reads, for callers that want to write them back.</summary>
    public byte[] Bytes => _bytes;

    /// <summary>Offset of this map inside <see cref="Bytes"/>.</summary>
    public int Offset => _base;

    public string LevelName => MapBook.LevelName(Level);

    // --- walls ---------------------------------------------------------------
    /// <summary>The raw wall byte between (x, y) and its neighbour in <paramref name="direction"/>.</summary>
    public int WallValue(int x, int y, int direction) =>
        MapFormat.InBounds(x, y) && direction >= 0 && direction < MapFormat.Directions
            ? _bytes[_base + MapFormat.WallIndex(x, y, direction)]
            : MapFormat.WallSolid;

    public WallKind Wall(int x, int y, int direction) => MapFormat.Classify(WallValue(x, y, direction));

    /// <summary>True when the party could walk from (x, y) in <paramref name="direction"/>.</summary>
    public bool CanWalk(int x, int y, int direction) => MapFormat.IsPassable(WallValue(x, y, direction));

    // --- square contents -----------------------------------------------------
    private int ContentByte(int x, int y) =>
        MapFormat.InBounds(x, y) ? _bytes[_base + MapFormat.ContentIndex(x, y)] : 0;

    /// <summary>The square's event code, decoded as the game decodes it (see <see cref="MapFormat.DecodeEventCode"/>).</summary>
    public int EventCode(int x, int y) => MapFormat.DecodeEventCode(ContentByte(x, y));

    public bool IsVisited(int x, int y) => (ContentByte(x, y) & MapFormat.VisitedFlag) != 0;

    /// <summary>True when this was a chest or item square and the party has already emptied it.</summary>
    public bool IsEmptied(int x, int y) => MapFormat.IsEmptied(ContentByte(x, y));

    /// <summary>
    /// What the square does. An emptied chest or item reads as no event at all to the game, but is
    /// worth drawing differently from a plain square — it tells the player they have been here and
    /// taken it.
    /// </summary>
    public SquareKind Kind(int x, int y) =>
        IsEmptied(x, y) ? SquareKind.Emptied : MapFormat.KindOf(EventCode(x, y));

    /// <summary>
    /// Sets the mapped/visited bit on every square, which is what the game's own automap draws
    /// from. Returns how many squares were newly marked.
    /// </summary>
    public int RevealAll()
    {
        int changed = 0;
        for (int i = 0; i < MapFormat.ContentsLength; i++)
        {
            int at = _base + MapFormat.OffContents + i;
            if ((_bytes[at] & MapFormat.VisitedFlag) != 0) continue;
            _bytes[at] |= MapFormat.VisitedFlag;
            changed++;
        }
        return changed;
    }

    /// <summary>Number of squares the party has stood on.</summary>
    public int VisitedCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < MapFormat.ContentsLength; i++)
                if ((_bytes[_base + MapFormat.OffContents + i] & MapFormat.VisitedFlag) != 0) n++;
            return n;
        }
    }

    // --- description text ----------------------------------------------------
    /// <summary>
    /// The description lines the map file carries for an event code. The game prints lines
    /// <c>first .. first + count - 1</c> of the level's 127-line text block; each line is a
    /// length-prefixed string in a fixed 40-byte slot.
    /// </summary>
    public IReadOnlyList<string> TextFor(int eventCode)
    {
        if (eventCode <= 0 || eventCode >= MapFormat.EventCodeCount) return Array.Empty<string>();

        int first = _bytes[_base + MapFormat.OffTextFirst + eventCode];
        int count = _bytes[_base + MapFormat.OffTextCount + eventCode];
        if (count == 0) return Array.Empty<string>();

        var lines = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            int line = first + i;
            if (line < 0 || line >= MapFormat.TextLineCount) break;
            lines.Add(DecodeLine(_base + MapFormat.OffTextLines + line * MapFormat.TextLineSize));
        }
        return lines;
    }

    /// <summary>
    /// Decodes one 40-byte text slot: a length byte, then that many characters. <c>]</c> is the
    /// game's end-of-line marker and is dropped; <c>0x02</c> is its padding space and becomes an
    /// ordinary space, as does anything else unprintable, so a garbled slot cannot inject control
    /// characters into the UI. Leading and trailing padding is then trimmed, which leaves the
    /// interior spacing of a centred title intact.
    /// </summary>
    private string DecodeLine(int at)
    {
        int len = _bytes[at];
        if (len > MapFormat.TextLineSize - 1) len = MapFormat.TextLineSize - 1;

        var sb = new StringBuilder(len);
        for (int i = 0; i < len; i++)
        {
            byte c = _bytes[at + 1 + i];
            if (c == (byte)']') continue;                       // the game's newline marker
            sb.Append(c >= 0x20 && c < 0x7F ? (char)c : ' ');
        }
        return sb.ToString().Trim();
    }

    // --- projections ---------------------------------------------------------
    /// <summary>
    /// Every square that has something to draw: a wall, a door, a special square, or a visited mark.
    /// Blank interior squares are left out so the schematic stays light.
    /// </summary>
    public IReadOnlyList<MapSquare> DrawableSquares()
    {
        var names = RoomNames();
        var cells = new List<MapSquare>();
        for (int y = 0; y < MapFormat.GridSize; y++)
        {
            for (int x = 0; x < MapFormat.GridSize; x++)
            {
                var sq = Square(x, y, names);
                if (sq.IsBlank) continue;
                cells.Add(sq);
            }
        }
        return cells;
    }

    /// <summary>Projects a single square, resolving its room name from the level's own text.</summary>
    public MapSquare Square(int x, int y) => Square(x, y, RoomNames());

    private MapSquare Square(int x, int y, IReadOnlyDictionary<int, string> names)
    {
        int code = EventCode(x, y);
        return new MapSquare(
            x, y,
            Wall(x, y, MapFormat.North), Wall(x, y, MapFormat.East),
            Wall(x, y, MapFormat.South), Wall(x, y, MapFormat.West),
            code, MapFormat.KindOf(code), IsVisited(x, y),
            names.TryGetValue(code, out var n) ? n : "");
    }

    private Dictionary<int, string>? _roomNames;

    /// <summary>Event code → the room name on its first description line.</summary>
    private Dictionary<int, string> RoomNames()
    {
        if (_roomNames != null) return _roomNames;
        var map = new Dictionary<int, string>();
        for (int code = 1; code < MapFormat.FirstActionCode; code++)
        {
            var text = TextFor(code);
            if (text.Count == 0 || text[0].Length == 0) continue;
            map[code] = text[0];
        }
        return _roomNames = map;
    }

    /// <summary>
    /// The level's places, in event-code order: every described room plus every stairway, chest,
    /// item and edge square, each with the squares that trigger it.
    /// </summary>
    public IReadOnlyList<MapRoom> Rooms()
    {
        var squares = new Dictionary<int, List<MapSquare>>();
        var names = RoomNames();
        for (int x = 0; x < MapFormat.GridSize; x++)
        {
            for (int y = 0; y < MapFormat.GridSize; y++)
            {
                int code = EventCode(x, y);
                if (code == 0) continue;
                if (!squares.TryGetValue(code, out var list)) squares[code] = list = new List<MapSquare>();
                list.Add(Square(x, y, names));
            }
        }

        var rooms = new List<MapRoom>();
        foreach (int code in squares.Keys.OrderBy(c => c))
        {
            var kind = MapFormat.KindOf(code);
            string name = kind != SquareKind.Plain
                ? MapFormat.KindName(kind)
                : names.TryGetValue(code, out var n) ? n : $"Area {code}";
            string description = kind != SquareKind.Plain
                ? ""
                : string.Join(" ", TextFor(code).Skip(1));
            rooms.Add(new MapRoom(code, name, description, squares[code]));
        }
        return rooms;
    }

    /// <summary>The squares of one special kind, in scan order.</summary>
    public IReadOnlyList<MapSquare> SquaresOfKind(SquareKind kind)
    {
        var names = RoomNames();
        var found = new List<MapSquare>();
        for (int x = 0; x < MapFormat.GridSize; x++)
            for (int y = 0; y < MapFormat.GridSize; y++)
                if (Kind(x, y) == kind) found.Add(Square(x, y, names));
        return found;
    }
}
