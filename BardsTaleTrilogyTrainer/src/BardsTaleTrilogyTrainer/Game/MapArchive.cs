using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace BardsTaleTrilogyTrainer.Game;

/// <summary>
/// Reads the trilogy's map grids out of the installed game's <c>resources.assets</c>.
///
/// <para>Every map ships as a Unity <c>TextAsset</c> named <c>map_bt&lt;n&gt;_{city|dung}NN_…_asc</c>
/// holding a plain-text description of the grid. Rather than bundling that content — it is the
/// game's, not ours — the trainer opens the player's own installation, walks the serialised-file
/// object table to find the asset, and reads just that one blob.</para>
///
/// <para>Only the object table is parsed (Unity serialised-file format 17, as shipped with
/// Unity 2018.4): header, type table, then one entry per object giving its class id, offset and
/// size. Class id 49 is <c>TextAsset</c>, whose body is <c>m_Name</c> followed by the byte
/// array. Nothing else in the file is touched.</para>
/// </summary>
public sealed class MapArchive : IDisposable
{
    /// <summary>Unity class id for <c>TextAsset</c>.</summary>
    private const int TextAssetClassId = 49;

    /// <summary>
    /// Serialised-file layouts this reader has been verified against. The floor is 17, not 16:
    /// a type entry only carries <c>scriptTypeIndex</c> from 17 onwards, and an object entry
    /// stops carrying <c>isDestroyed</c>/<c>stripped</c> at the same point. Reading a version-16
    /// file with these layouts desynchronises after the first entry, which <c>Align4</c> cannot
    /// resynchronise. The shipped build is 17.
    /// </summary>
    private const int MinSupportedVersion = 17;
    private const int MaxSupportedVersion = 21;

    /// <summary>Guard against a corrupt length turning into a huge allocation.</summary>
    private const int MaxAssetBytes = 8 * 1024 * 1024;

    private readonly FileStream _stream;
    private readonly Dictionary<string, (long Offset, int Size)> _assets;
    private readonly Dictionary<string, MapGrid> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Path of the <c>resources.assets</c> this archive is reading.</summary>
    public string Path { get; }

    /// <summary>Names of every map asset found, in file order.</summary>
    public IReadOnlyCollection<string> MapAssets => _assets.Keys;

    private MapArchive(string path, FileStream stream, Dictionary<string, (long, int)> assets)
    {
        Path = path;
        _stream = stream;
        _assets = assets;
    }

    /// <summary>
    /// Opens the archive inside <paramref name="gameDirectory"/> (the folder holding
    /// <c>TheBardsTaleTrilogy.exe</c>). Returns null and sets <paramref name="error"/> when the
    /// file is missing or is not a layout this reader understands.
    /// </summary>
    public static MapArchive? TryOpen(string gameDirectory, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            error = "The game folder is not known yet — attach to the running game first.";
            return null;
        }

        string path = System.IO.Path.Combine(gameDirectory, "TheBardsTaleTrilogy_Data", "resources.assets");
        if (!File.Exists(path))
        {
            error = $"Could not find {path}.";
            return null;
        }

        FileStream? stream = null;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var assets = IndexTextAssets(stream);
            if (assets.Count == 0)
            {
                error = "No map assets found in resources.assets — is this the expected game build?";
                stream.Dispose();
                return null;
            }
            return new MapArchive(path, stream, assets);
        }
        catch (Exception ex)
        {
            stream?.Dispose();
            error = $"Could not read resources.assets: {ex.Message}";
            return null;
        }
    }

    /// <summary>Decodes one map by asset name (<see cref="GameMapInfo.Asset"/>), with caching.</summary>
    public MapGrid? TryGetMap(string assetName, out string error)
    {
        error = "";
        if (_cache.TryGetValue(assetName, out var cached)) return cached;
        if (!_assets.TryGetValue(assetName, out var entry))
        {
            error = $"'{assetName}' is not in this installation's resources.assets.";
            return null;
        }

        try
        {
            var bytes = new byte[entry.Size];
            _stream.Position = entry.Offset;
            _stream.ReadExactly(bytes, 0, bytes.Length);
            var grid = MapFileParser.Parse(Encoding.UTF8.GetString(bytes));
            _cache[assetName] = grid;
            return grid;
        }
        catch (Exception ex)
        {
            error = $"Could not decode '{assetName}': {ex.Message}";
            return null;
        }
    }

    public void Dispose() => _stream.Dispose();

    // --- serialised-file walk ---------------------------------------------------
    /// <summary>
    /// Parses the header and object table and returns the file offset/length of every
    /// <c>map_*</c> TextAsset's payload (the bytes after <c>m_Name</c>).
    /// </summary>
    private static Dictionary<string, (long Offset, int Size)> IndexTextAssets(FileStream stream)
    {
        var head = new byte[20];
        stream.Position = 0;
        stream.ReadExactly(head, 0, head.Length);

        // The four header words are big-endian even on little-endian targets.
        int metadataSize = BinaryPrimitives.ReadInt32BigEndian(head.AsSpan(0));
        long fileSize = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(4));
        int version = BinaryPrimitives.ReadInt32BigEndian(head.AsSpan(8));
        long dataOffset = BinaryPrimitives.ReadUInt32BigEndian(head.AsSpan(12));

        if (version < MinSupportedVersion || version > MaxSupportedVersion)
            throw new NotSupportedException(
                $"serialised-file version {version} (this reader handles {MinSupportedVersion}-{MaxSupportedVersion}).");
        if (dataOffset <= 20 || dataOffset > stream.Length)
            throw new InvalidDataException($"implausible data offset {dataOffset}.");
        if (metadataSize <= 0) throw new InvalidDataException("empty metadata block.");

        // Everything before dataOffset is header + metadata; read it once and walk in memory.
        int prefix = (int)Math.Min(dataOffset, int.MaxValue);
        var meta = new byte[prefix];
        stream.Position = 0;
        stream.ReadExactly(meta, 0, prefix);

        var r = new Cursor(meta, 20);
        r.SkipCString();                        // unity version, e.g. "2018.4.0f1"
        r.SkipInt32();                          // target platform
        bool typeTree = r.ReadByte() != 0;
        if (typeTree)
            throw new NotSupportedException("this build embeds type trees, which this reader skips.");

        int typeCount = r.ReadInt32();
        if (typeCount < 0 || typeCount > 1 << 16) throw new InvalidDataException("bad type count.");
        var classIds = new int[typeCount];
        for (int i = 0; i < typeCount; i++)
        {
            classIds[i] = r.ReadInt32();
            r.Skip(1);                          // isStrippedType
            r.Skip(2);                          // scriptTypeIndex
            if (classIds[i] == 114) r.Skip(16); // MonoBehaviour: script hash
            r.Skip(16);                         // type hash
        }

        int objectCount = r.ReadInt32();
        if (objectCount < 0 || objectCount > 1 << 22) throw new InvalidDataException("bad object count.");

        var result = new Dictionary<string, (long, int)>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < objectCount; i++)
        {
            r.Align4();
            r.Skip(8);                          // pathID
            int byteStart = r.ReadInt32();
            int byteSize = r.ReadInt32();
            int typeIndex = r.ReadInt32();
            if (typeIndex < 0 || typeIndex >= typeCount) continue;
            if (classIds[typeIndex] != TextAssetClassId) continue;
            if (byteStart < 0 || byteSize <= 0) continue;

            long start = dataOffset + byteStart;
            if (start + byteSize > fileSize || start + byteSize > stream.Length) continue;

            // TextAsset body: m_Name (length-prefixed, padded to 4) then the byte array.
            if (!TryReadTextAssetHeader(stream, start, byteSize, out string name, out long payload, out int length))
                continue;
            if (!name.StartsWith("map_", StringComparison.OrdinalIgnoreCase)) continue;
            result[name] = (payload, length);
        }
        return result;
    }

    /// <summary>
    /// Reads a TextAsset's <c>m_Name</c> and the offset/length of its payload without pulling
    /// the whole (possibly multi-megabyte) blob into memory.
    /// </summary>
    private static bool TryReadTextAssetHeader(FileStream stream, long start, int byteSize,
        out string name, out long payloadOffset, out int payloadLength)
    {
        name = "";
        payloadOffset = 0;
        payloadLength = 0;

        // A name plus its length words: 256 bytes is far more than any asset name here needs.
        int probe = (int)Math.Min(byteSize, 256);
        var buf = new byte[probe];
        stream.Position = start;
        int read = stream.Read(buf, 0, probe);
        if (read < 8) return false;

        int nameLength = BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(0));
        if (nameLength < 0 || nameLength > probe - 8) return false;
        name = Encoding.UTF8.GetString(buf, 4, nameLength);

        int after = 4 + nameLength;
        after = (after + 3) & ~3;               // Unity pads strings to a 4-byte boundary
        if (after + 4 > read) return false;

        payloadLength = BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(after));
        if (payloadLength <= 0 || payloadLength > MaxAssetBytes) return false;
        if (after + 4 + payloadLength > byteSize) return false;

        payloadOffset = start + after + 4;
        return true;
    }

    /// <summary>Little-endian cursor over the metadata block, with Unity's 4-byte alignment.</summary>
    private sealed class Cursor
    {
        private readonly byte[] _d;
        private int _p;

        public Cursor(byte[] data, int position) { _d = data; _p = position; }

        public void Skip(int n) => Advance(n);
        public void SkipInt32() => Advance(4);
        public void Align4() => _p = (_p + 3) & ~3;

        public byte ReadByte()
        {
            int p = _p;
            Advance(1);
            return _d[p];
        }

        public int ReadInt32()
        {
            int p = _p;
            Advance(4);
            return BinaryPrimitives.ReadInt32LittleEndian(_d.AsSpan(p));
        }

        public void SkipCString()
        {
            int end = Array.IndexOf(_d, (byte)0, _p);
            if (end < 0) throw new InvalidDataException("unterminated string in metadata.");
            _p = end + 1;
        }

        private void Advance(int n)
        {
            if (n < 0 || _p > _d.Length - n)
                throw new InvalidDataException("metadata block ended early.");
            _p += n;
        }
    }
}
