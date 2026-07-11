namespace DragonWarsTrainer.Game;

/// <summary>What sits on one edge of a square: nothing, a solid wall, a door, or a fence.</summary>
public enum WallKind
{
    None,
    Wall,
    Door,
    Fence,
}

/// <summary>The floor terrain of a square as far as the automap distinguishes it.</summary>
public enum FloorKind
{
    Normal,
    Water,
    Abyss,
    Stone,
}

/// <summary>One decoded map square: its west/north walls and its floor terrain.</summary>
public readonly record struct BoardSquare(WallKind West, WallKind North, FloorKind Floor);

/// <summary>
/// A single Dragon Wars board decoded into per-square terrain (walls + floor). The board lives in
/// the archive as the "primary" chunk <c>0x46 + boardId</c>; after Huffman decompression it opens
/// with a 4-byte header (xMax, yMax, flags) followed by five high-bit-terminated arrays
/// (texture / wall / roof / floor / deco), a title word, then <c>xMax * yMax</c> 3-byte squares.
/// Each square packs the west wall (bits 0-3), the north wall (bits 4-7) and a floor index
/// (bits 12-13). Wall and floor indices are resolved through the texture array into the game's
/// texture ids, then classified the same way the in-game automap does. Origin is bottom-left with
/// Y increasing upward (west = left edge, north = top edge). Verified against the shipped data.
/// </summary>
public sealed class BoardMap
{
    // --- texture id classes (from Lists in fraterrisus/dragonjars) ------------
    private const int TexBase = 0x6e;
    private const int WallBlueStone = 0x6e;
    private const int WallBlueDoor = 0x73;
    private const int WallFence = 0x7a;
    private const int WallGreyStone = 0x7d;
    private const int WallGreyDoor = 0x7e;
    private const int FloorWater = 0x75;
    private const int FloorStone = 0x7c;
    private const int FloorAbyss = 0x85;

    private readonly BoardSquare[] _squares;

    private BoardMap(int width, int height, BoardSquare[] squares)
    {
        Width = width;
        Height = height;
        _squares = squares;
    }

    /// <summary>Board width in squares.</summary>
    public int Width { get; }

    /// <summary>Board height in squares.</summary>
    public int Height { get; }

    /// <summary>The square at (<paramref name="x"/>, <paramref name="y"/>), origin bottom-left.</summary>
    public BoardSquare Square(int x, int y) => _squares[y * Width + x];

    /// <summary>
    /// Parses the decompressed primary chunk of a board into terrain, or null if the bytes do not
    /// describe a well-formed board.
    /// </summary>
    public static BoardMap? TryParse(byte[] chunk)
    {
        if (chunk is null || chunk.Length < 5) return null;

        int xMax = chunk[0];
        int yMax = chunk[1];
        if (xMax == 0 || yMax == 0) return null;

        const int ptrTexture = 4;
        if (!SkipArray(chunk, ptrTexture, 1, out int ptrWall)) return null;
        if (!SkipArray(chunk, ptrWall, 2, out int ptrRoof)) return null;
        if (!SkipArray(chunk, ptrRoof, 1, out int ptrFloor)) return null;
        if (!SkipArray(chunk, ptrFloor, 1, out int ptrDeco)) return null;
        if (!SkipArray(chunk, ptrDeco, 1, out int ptrTitle)) return null;

        int ptrSquares = ptrTitle + 2;
        long need = (long)ptrSquares + (long)xMax * yMax * 3;
        if (need > chunk.Length) return null;

        int At(int off) => off >= 0 && off < chunk.Length ? chunk[off] : -1;

        int Texture(int i)
        {
            int b = At(ptrTexture + i);
            return b < 0 ? -1 : 0x7f & b;
        }

        WallKind ResolveWall(int index)
        {
            if (index == 0) return WallKind.None;
            int wb = At(ptrWall + 2 * (index - 1));
            if (wb < 0) return WallKind.None;
            int w = 0x7f & wb;
            int id;
            if (w > TexBase)
            {
                id = w;
            }
            else
            {
                int t = Texture(w);
                if (t < 0) return WallKind.None;
                id = t + TexBase;
            }
            return id switch
            {
                WallBlueDoor or WallGreyDoor => WallKind.Door,
                WallFence => WallKind.Fence,
                WallBlueStone or WallGreyStone => WallKind.Wall,
                _ => WallKind.None,
            };
        }

        FloorKind ResolveFloor(int index)
        {
            int fb = At(ptrFloor + index);
            if (fb < 0) return FloorKind.Normal;
            int t = Texture(0x7f & fb);
            if (t < 0) return FloorKind.Normal;
            int id = t + TexBase;
            return id switch
            {
                FloorWater => FloorKind.Water,
                FloorAbyss => FloorKind.Abyss,
                FloorStone => FloorKind.Stone,
                _ => FloorKind.Normal,
            };
        }

        var squares = new BoardSquare[xMax * yMax];
        for (int y = 0; y < yMax; y++)
        {
            for (int x = 0; x < xMax; x++)
            {
                int off = ptrSquares + ((yMax - (y + 1)) * xMax + x) * 3;
                int raw = chunk[off] | (chunk[off + 1] << 8) | (chunk[off + 2] << 16);
                var west = ResolveWall(raw & 0xf);
                var north = ResolveWall((raw >> 4) & 0xf);
                var floor = ResolveFloor((raw >> 12) & 0x3);
                squares[y * xMax + x] = new BoardSquare(west, north, floor);
            }
        }
        return new BoardMap(xMax, yMax, squares);
    }

    /// <summary>
    /// Advances past a high-bit-terminated array: walk in <paramref name="width"/>-byte strides
    /// until a byte with bit 0x80 set is consumed. Returns false if it would run off the buffer.
    /// </summary>
    private static bool SkipArray(byte[] b, int ptr, int width, out int end)
    {
        end = ptr;
        while (end < b.Length)
        {
            byte x = b[end];
            end += width;
            if ((x & 0x80) != 0) return true;
        }
        return false;
    }
}
