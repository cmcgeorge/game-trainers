using System.Text;

namespace DarklandsTrainer.Game;

/// <summary>A leading character record decoded from a Darklands save (read-only).</summary>
public sealed class SaveCharacter
{
    public string Name { get; init; } = "";
    public string Nickname { get; init; } = "";

    /// <summary>The six current primary attributes, in <see cref="AttributeBook"/> order.</summary>
    public IReadOnlyList<byte> Attributes { get; init; } = Array.Empty<byte>();

    /// <summary>The six maximum primary attributes.</summary>
    public IReadOnlyList<byte> MaxAttributes { get; init; } = Array.Empty<byte>();
}

/// <summary>The header fields plus the leading character record decoded from a save.</summary>
public sealed class SaveHeader
{
    public string Location { get; init; } = "";
    public string Label { get; init; } = "";
    public int PartyCount { get; init; }

    /// <summary>Non-empty portrait codes, one per filled party slot (e.g. F60, F01, A00, C00).</summary>
    public IReadOnlyList<string> Portraits { get; init; } = Array.Empty<string>();

    public SaveCharacter FirstCharacter { get; init; } = new();
}

/// <summary>
/// Read-only reader for the Darklands save format, keyed off the offsets confirmed in the shipped
/// character-generation template <c>SAVES\DEFAULT</c> (see <c>.docs/ReverseEngineering.md</c> §3). It is
/// deliberately read-only: the offsets are derived from a single sample, so the trainer never writes
/// them back to a save on disk — live edits go through the value scanner instead. The
/// <c>FormatCheck</c> harness exercises this reader against a synthetic fixture built from the DEFAULT
/// bytes so the parser can't silently drift.
/// </summary>
public static class SaveFile
{
    /// <summary>Size of the DEFAULT template save in bytes.</summary>
    public const int DefaultSize = 26349;

    public const int LocationOffset = 0x000;
    public const int LabelOffset = 0x015;
    public const int PartyCountOffset = 0x0F1;
    public const int PortraitBlockOffset = 0x0FD;
    public const int PortraitStride = 4;
    public const int MaxPartySlots = 4;

    public const int NameOffset = 0x1AE;
    public const int NicknameOffset = 0x1C7;
    public const int AttributesCurrentOffset = 0x1E6;
    public const int AttributesMaxOffset = 0x1ED;

    /// <summary>Bytes per attribute block: six primaries followed by a 0x63/99 cap byte.</summary>
    public const int AttributeBlockLength = AttributeBook.PrimaryCount + 1;

    /// <summary>Decodes the header and the leading character record. Throws on a buffer too short to hold them.</summary>
    public static SaveHeader ParseHeader(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        int need = AttributesMaxOffset + AttributeBlockLength;
        if (data.Length < need)
            throw new ArgumentException($"Save buffer is {data.Length} bytes; need at least {need}.", nameof(data));

        int partyCount = data[PartyCountOffset];
        if (partyCount > MaxPartySlots)
            throw new ArgumentException($"Party count {partyCount} exceeds {MaxPartySlots} slots.", nameof(data));

        var portraits = new List<string>(partyCount);
        for (int slot = 0; slot < partyCount; slot++)
        {
            string code = ReadAsciiZ(data, PortraitBlockOffset + slot * PortraitStride, PortraitStride);
            if (!string.IsNullOrEmpty(code) && code != "0")
                portraits.Add(code);
        }

        return new SaveHeader
        {
            Location = ReadAsciiZ(data, LocationOffset, 20),
            Label = ReadAsciiZ(data, LabelOffset, 20),
            PartyCount = partyCount,
            Portraits = portraits,
            FirstCharacter = new SaveCharacter
            {
                Name = ReadAsciiZ(data, NameOffset, 24),
                Nickname = ReadAsciiZ(data, NicknameOffset, 16),
                Attributes = ReadAttributeBlock(data, AttributesCurrentOffset),
                MaxAttributes = ReadAttributeBlock(data, AttributesMaxOffset),
            },
        };
    }

    /// <summary>Reads a NUL-terminated ASCII string of at most <paramref name="maxLength"/> bytes.</summary>
    public static string ReadAsciiZ(byte[] data, int offset, int maxLength)
    {
        if (offset < 0 || offset >= data.Length) return "";
        int end = offset;
        int limit = Math.Min(data.Length, offset + maxLength);
        while (end < limit && data[end] != 0) end++;
        return Encoding.ASCII.GetString(data, offset, end - offset);
    }

    /// <summary>Reads the six primary attribute bytes starting at <paramref name="offset"/>.</summary>
    public static byte[] ReadAttributeBlock(byte[] data, int offset)
    {
        var attrs = new byte[AttributeBook.PrimaryCount];
        for (int i = 0; i < attrs.Length; i++)
            attrs[i] = (offset + i < data.Length) ? data[offset + i] : (byte)0;
        return attrs;
    }
}
