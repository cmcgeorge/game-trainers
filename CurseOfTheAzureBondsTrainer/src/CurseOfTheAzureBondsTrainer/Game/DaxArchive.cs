using System.IO;

namespace CurseOfTheAzureBondsTrainer.Game;

/// <summary>One member of a <c>.DAX</c> archive: its block id and unpacked bytes.</summary>
public sealed record DaxBlock(int Id, byte[] Data);

/// <summary>
/// Reader for the game's <c>.DAX</c> resource archives.
///
/// <para>A <c>.DAX</c> file is a small container:</para>
/// <code>
/// UInt16  headerLength                 // bytes of block entries that follow
/// entry[headerLength / 9]:
///     byte    id                       // block id
///     UInt32  offset                   // from the end of the header
///     UInt16  unpackedSize
///     UInt16  packedSize
/// byte[]  packed block data
/// </code>
/// <para><c>2 + headerLength + Σ packedSize</c> accounts for every archive in the CURSE folder
/// exactly, which is what confirms the field order. Blocks are PackBits-style RLE: read a lead byte
/// <c>n</c>; if <c>n &lt; 0x80</c> copy the next <c>n + 1</c> bytes verbatim, otherwise repeat the
/// next single byte <c>256 - n</c> times. Under that variant every one of the game's 16
/// <c>GEO*.DAX</c> geometry blocks lands on its declared unpacked size exactly, and every
/// <c>MON*CHA.DAX</c> block unpacks to precisely one 422-byte character record.</para>
///
/// <para>The trainer reads these at runtime rather than embedding their contents, so the level
/// geometry it matches against the running game is the geometry of <i>your</i> install.</para>
/// </summary>
public static class DaxArchive
{
    private const int EntrySize = 9;

    /// <summary>Reads and unpacks every block of an archive. Blocks whose packed data is truncated
    /// or which do not unpack to their declared size are skipped rather than returned half-decoded,
    /// so a caller never has to re-check the length.</summary>
    public static IReadOnlyList<DaxBlock> Read(string path)
    {
        byte[] b;
        try { b = File.ReadAllBytes(path); }
        catch { return Array.Empty<DaxBlock>(); }
        return Parse(b);
    }

    /// <summary>Parses archive bytes already in hand (the file form of <see cref="Read"/>).</summary>
    public static IReadOnlyList<DaxBlock> Parse(byte[] b)
    {
        var blocks = new List<DaxBlock>();
        if (b.Length < 2) return blocks;

        int headerLength = b[0] | (b[1] << 8);
        int count = headerLength / EntrySize;
        int body = 2 + headerLength;
        if (body > b.Length) return blocks;

        for (int i = 0; i < count; i++)
        {
            int o = 2 + i * EntrySize;
            if (o + EntrySize > b.Length) break;

            int id = b[o];
            long offset = (uint)(b[o + 1] | (b[o + 2] << 8) | (b[o + 3] << 16) | (b[o + 4] << 24));
            int unpacked = b[o + 5] | (b[o + 6] << 8);
            int packed = b[o + 7] | (b[o + 8] << 8);

            long start = body + offset;
            if (start < 0 || start + packed > b.Length || unpacked <= 0) continue;

            byte[] data = Unpack(b, (int)start, packed, unpacked);
            if (data.Length == unpacked) blocks.Add(new DaxBlock(id, data));
        }
        return blocks;
    }

    /// <summary>PackBits-style RLE decode of one block.</summary>
    private static byte[] Unpack(byte[] src, int start, int packed, int unpacked)
    {
        var outBuf = new byte[unpacked];
        int o = 0, i = start, end = start + packed;

        while (i < end && o < unpacked)
        {
            int n = src[i++];
            if (n < 0x80)
            {
                int run = n + 1;
                if (i + run > end) run = end - i;
                if (run <= 0) break;
                if (o + run > unpacked) run = unpacked - o;
                Array.Copy(src, i, outBuf, o, run);
                i += n + 1;
                o += run;
            }
            else
            {
                if (i >= end) break;
                byte v = src[i++];
                int run = Math.Min(256 - n, unpacked - o);
                for (int k = 0; k < run; k++) outBuf[o + k] = v;
                o += run;
            }
        }
        // A short decode means the block was truncated; report it by length rather than padding.
        return o == unpacked ? outBuf : outBuf[..o];
    }

    // --- level geometry ------------------------------------------------------

    /// <summary>Unpacked size of a <c>GEO*.DAX</c> level-geometry block: a UInt16 length then four
    /// 256-byte planes.</summary>
    public const int GeoBlockSize = 1026;

    /// <summary>Offset of the wall planes inside a geometry block, and their combined length. Planes
    /// 0 and 1 hold the four wall-index nibbles per square; the game loads them into RAM unchanged,
    /// which is what makes a level identifiable in a running process.</summary>
    public const int GeoWallOffset = 2;
    public const int GeoWallLength = 512;

    /// <summary>The wall planes of a geometry block, or null if the block isn't one.</summary>
    public static byte[]? WallPlanes(DaxBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (block.Data.Length < GeoWallOffset + GeoWallLength) return null;
        return block.Data[GeoWallOffset..(GeoWallOffset + GeoWallLength)];
    }

    /// <summary>Every <c>GEO*.DAX</c> level in a game folder, tagged <c>GEO&lt;n&gt;:&lt;block&gt;</c>
    /// to match <see cref="MapArea.Geo"/>.</summary>
    public static IReadOnlyList<(string Geo, byte[] Walls)> ReadLevels(string gameFolder)
    {
        var levels = new List<(string, byte[])>();
        if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder)) return levels;

        string[] files;
        try { files = Directory.GetFiles(gameFolder, "GEO?.DAX"); }
        catch { return levels; }

        foreach (string f in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            string stem = Path.GetFileNameWithoutExtension(f).ToUpperInvariant();
            foreach (var block in Read(f))
            {
                byte[]? walls = WallPlanes(block);
                if (walls != null) levels.Add(($"{stem}:{block.Id}", walls));
            }
        }
        return levels;
    }
}
