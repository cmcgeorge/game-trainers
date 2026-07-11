using System.IO;

namespace DragonWarsTrainer.Game;

/// <summary>
/// Reads the Dragon Wars data archive (<c>DATA1</c> + <c>DATA2</c>) and hands back decompressed
/// chunks by id. Each file opens with a 0x300-byte table of 384 little-endian word sizes; a size of
/// 0 or with the 0x8000 bit set marks an absent chunk, otherwise chunks are laid out back-to-back
/// starting at offset 0x300. A chunk is looked up in DATA1 first, then DATA2. Layout and lookup
/// order match the <c>fraterrisus/dragonjars</c> <c>ChunkTable</c>.
/// </summary>
public sealed class DataArchive
{
    private const int TableSize = 0x300;
    private const int ChunkCount = TableSize / 2;

    private readonly byte[] _data1;
    private readonly byte[] _data2;
    private readonly (int Start, int Size)?[] _t1;
    private readonly (int Start, int Size)?[] _t2;

    private DataArchive(byte[] data1, byte[] data2)
    {
        _data1 = data1;
        _data2 = data2;
        _t1 = BuildTable(data1);
        _t2 = BuildTable(data2);
    }

    /// <summary>Number of chunk slots in the table.</summary>
    public static int Count => ChunkCount;

    /// <summary>
    /// Attempts to load the archive from a folder containing <c>DATA1</c> and <c>DATA2</c>.
    /// </summary>
    public static bool TryLoadFromFolder(string folder, out DataArchive? archive, out string error)
    {
        archive = null;
        error = "";
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            error = "Folder not found.";
            return false;
        }
        string p1 = Path.Combine(folder, "DATA1");
        string p2 = Path.Combine(folder, "DATA2");
        if (!File.Exists(p1) || !File.Exists(p2))
        {
            error = "That folder has no DATA1 / DATA2 files.";
            return false;
        }
        try
        {
            var d1 = File.ReadAllBytes(p1);
            var d2 = File.ReadAllBytes(p2);
            if (d1.Length < TableSize || d2.Length < TableSize)
            {
                error = "DATA1 / DATA2 are too small to be valid.";
                return false;
            }
            archive = new DataArchive(d1, d2);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Could not read the data files: {ex.Message}";
            return false;
        }
    }

    /// <summary>Returns the decompressed bytes of chunk <paramref name="id"/>, or null if absent.</summary>
    public byte[]? GetChunk(int id)
    {
        var raw = GetRawChunk(id);
        if (raw is null) return null;
        try { return HuffmanDecoder.Decode(raw); }
        catch (InvalidDataException) { return null; }
    }

    private byte[]? GetRawChunk(int id)
    {
        if (id < 0 || id >= ChunkCount) return null;
        if (_t1[id] is { } a && a.Start != 0) return Slice(_data1, a);
        if (_t2[id] is { } b && b.Start != 0) return Slice(_data2, b);
        return null;
    }

    private static byte[]? Slice(byte[] data, (int Start, int Size) e)
    {
        if (e.Start + e.Size > data.Length) return null;
        var buf = new byte[e.Size];
        Array.Copy(data, e.Start, buf, 0, e.Size);
        return buf;
    }

    private static (int Start, int Size)?[] BuildTable(byte[] data)
    {
        var table = new (int Start, int Size)?[ChunkCount];
        int next = TableSize;
        for (int i = 0; i < ChunkCount; i++)
        {
            int size = data[i * 2] | (data[i * 2 + 1] << 8);
            if (size == 0 || (size & 0x8000) != 0)
            {
                table[i] = null;
            }
            else
            {
                table[i] = (next, size);
                next += size;
            }
        }
        return table;
    }
}
