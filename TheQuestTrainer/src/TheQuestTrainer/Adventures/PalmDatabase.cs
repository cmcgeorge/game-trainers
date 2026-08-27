using System.Buffers.Binary;
using System.IO;

namespace TheQuestTrainer.Adventures;

/// <summary>One record of a Palm database: its id, and where its bytes are.</summary>
/// <param name="Index">Position in the record list, zero-based.</param>
/// <param name="UniqueId">The record's 24-bit id. The world references records by this, not by index.</param>
/// <param name="Offset">Byte offset of the record in the file.</param>
/// <param name="Length">Length in bytes, derived from the next record's offset.</param>
public readonly record struct PalmRecord(int Index, int UniqueId, int Offset, int Length);

/// <summary>
/// A Palm OS database (<c>.pdb</c>), which is what The Quest still keeps its worlds in — the game
/// began on Palm OS in 2005 and the Windows re-release never changed the container.
///
/// The header is the documented Palm layout: a 32-byte name, big-endian dates and counts, a
/// four-character type and creator, then one 8-byte entry per record giving its file offset and
/// 24-bit unique id. A record's length is the gap to the next one, so the list has to be read whole
/// before any record can be.
///
/// Nothing here is Quest-specific except the two four-character codes: a world database is type
/// <c>ThQW</c>, creator <c>ThQu</c>. Records are padded to a multiple of four bytes, which is why a
/// parser that consumes every field can still find up to three bytes left over.
/// </summary>
public sealed class PalmDatabase
{
    /// <summary>Offset of the record list, immediately after the 78-byte header.</summary>
    private const int RecordListOffset = 78;

    /// <summary>Bytes per record-list entry: a big-endian offset, then an attribute byte and a 24-bit id.</summary>
    private const int RecordEntryBytes = 8;

    /// <summary>The type code every Quest world database carries.</summary>
    public const string WorldType = "ThQW";

    /// <summary>The creator code every Quest database carries.</summary>
    public const string QuestCreator = "ThQu";

    private readonly byte[] _bytes;

    private PalmDatabase(byte[] bytes, string name, string type, string creator, IReadOnlyList<PalmRecord> records)
    {
        _bytes = bytes;
        Name = name;
        Type = type;
        Creator = creator;
        Records = records;
    }

    /// <summary>The database name from the header, e.g. <c>TheQuestBase</c>.</summary>
    public string Name { get; }

    /// <summary>The four-character type code, e.g. <c>ThQW</c>.</summary>
    public string Type { get; }

    /// <summary>The four-character creator code, e.g. <c>ThQu</c>.</summary>
    public string Creator { get; }

    /// <summary>Every record, in file order.</summary>
    public IReadOnlyList<PalmRecord> Records { get; }

    /// <summary>Whether this is a Quest world database rather than art, sound or something else.</summary>
    public bool IsQuestWorld => Type == WorldType && Creator == QuestCreator;

    /// <summary>The bytes of one record.</summary>
    public ReadOnlySpan<byte> Bytes(in PalmRecord record) => _bytes.AsSpan(record.Offset, record.Length);

    /// <summary>The first byte of a record — its class tag — or <c>-1</c> when the record is empty.</summary>
    public int TagOf(in PalmRecord record) => record.Length > 0 ? _bytes[record.Offset] : -1;

    /// <summary>An archive positioned at the start of <paramref name="record"/>.</summary>
    public RecordArchive Open(in PalmRecord record) => new(Bytes(record));

    /// <summary>
    /// Parses <paramref name="bytes"/> as a Palm database.
    /// </summary>
    /// <param name="bytes">The whole file.</param>
    /// <param name="why">Always set: what went wrong, or the empty string on success.</param>
    /// <returns>The database, or null when the bytes are not one.</returns>
    public static PalmDatabase? Parse(byte[] bytes, out string why)
    {
        why = "";
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length < RecordListOffset)
        {
            why = "the file is shorter than a Palm database header";
            return null;
        }

        string name = Latin1(bytes, 0, 32);
        string type = Latin1(bytes, 60, 4);
        string creator = Latin1(bytes, 64, 4);
        int count = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(76, 2));

        int listEnd = RecordListOffset + count * RecordEntryBytes;
        if (listEnd > bytes.Length)
        {
            why = $"the record list claims {count} records, which does not fit in {bytes.Length} bytes";
            return null;
        }

        var offsets = new int[count];
        var ids = new int[count];
        for (int i = 0; i < count; i++)
        {
            int at = RecordListOffset + i * RecordEntryBytes;
            offsets[i] = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at, 4));
            ids[i] = (int)(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at + 4, 4)) & 0x00FF_FFFF);

            if (offsets[i] < listEnd || offsets[i] > bytes.Length)
            {
                why = $"record {i} starts at {offsets[i]}, outside the file";
                return null;
            }
            if (i > 0 && offsets[i] < offsets[i - 1])
            {
                why = $"record {i} starts before record {i - 1}";
                return null;
            }
        }

        var records = new PalmRecord[count];
        for (int i = 0; i < count; i++)
        {
            int end = i + 1 < count ? offsets[i + 1] : bytes.Length;
            records[i] = new PalmRecord(i, ids[i], offsets[i], end - offsets[i]);
        }

        return new PalmDatabase(bytes, name, type, creator, records);
    }

    /// <summary>Reads a file and parses it. A missing or unreadable file is a reason, not an exception.</summary>
    public static PalmDatabase? Open(string path, out string why)
    {
        try
        {
            return Parse(File.ReadAllBytes(path), out why);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            why = $"could not read {Path.GetFileName(path)}: {e.Message}";
            return null;
        }
    }

    /// <summary>A NUL-terminated Latin-1 field of fixed width.</summary>
    private static string Latin1(byte[] bytes, int at, int width)
    {
        var span = bytes.AsSpan(at, width);
        int end = span.IndexOf((byte)0);
        return System.Text.Encoding.Latin1.GetString(end < 0 ? span : span[..end]);
    }
}
