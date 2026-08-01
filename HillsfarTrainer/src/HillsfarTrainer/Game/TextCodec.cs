using System.Text;

namespace HillsfarTrainer.Game;

/// <summary>
/// Hillsfar's digraph text codec.
///
/// <para>Most of the game's text is not plain ASCII: bytes below <c>0x80</c> are literal characters
/// (with <c>0x0D</c> a line break and <c>0x00</c> a terminator) and <b>every byte at or above
/// <c>0x80</c> expands to exactly two characters</b>. The table is 144 bytes at
/// <c>DGROUP:0xAAA4</c> — sixteen "first" characters followed by sixteen groups of eight "second"
/// characters — and the index arithmetic is:</para>
///
/// <code>
/// i      = b - 0x80
/// first  = T[ i >> 3 ]
/// second = T[ 16 + (i >> 3) * 8 + (i &amp; 7) ]
/// </code>
///
/// <para>The layout was solved against fifteen expansions recovered from words whose plaintext is
/// known from the class and building tables, and it reproduces all fifteen exactly.</para>
///
/// <para>The trainer carries the shipped table as a constant so it can decode text without a running
/// game, and can also read the table out of an attached process — see
/// <see cref="FromMemory"/> — which is how it would notice a different build.</para>
/// </summary>
public sealed class TextCodec
{
    /// <summary>Where the table lives in the data segment.</summary>
    public const int DgroupTableOffset = 0xAAA4;

    /// <summary>Length of the table: sixteen first characters plus sixteen groups of eight.</summary>
    public const int TableLength = 16 + 16 * 8;

    /// <summary>Lowest byte value that expands to a digraph.</summary>
    public const byte FirstDigraphByte = 0x80;

    /// <summary>
    /// The table as it ships in v1.2, read byte-for-byte out of the unpacked image.
    ///
    /// <para>Written as bytes rather than as a string literal on purpose: the very last entry — the
    /// second character of code <c>0xFF</c> — is <c>0x80</c>, which is not ASCII and could not
    /// survive an <c>Encoding.ASCII</c> round-trip. It is almost certainly an unused slot (a digraph
    /// marker where a character should be), but it is carried verbatim so this constant compares
    /// equal to the table read out of a live game.</para>
    /// </summary>
    public static readonly byte[] ShippedTable =
    {
        0x20, 0x65, 0x6F, 0x74, 0x61, 0x68, 0x6E, 0x72, 0x73, 0x69, 0x75, 0x6C, 0x64, 0x79, 0x67, 0x63,
        //  ^ the sixteen first characters: " eotahnrsiuldygc"
        0x74, 0x20, 0x61, 0x68, 0x73, 0x79, 0x62, 0x6F,   //  0  ' ' -> "t ahsybo"
        0x20, 0x72, 0x6E, 0x61, 0x73, 0x64, 0x65, 0x74,   //  1  'e' -> " rnasdet"
        0x75, 0x20, 0x6E, 0x72, 0x6F, 0x66, 0x74, 0x77,   //  2  'o' -> "u nroftw"
        0x68, 0x20, 0x6F, 0x65, 0x69, 0x61, 0x72, 0x74,   //  3  't' -> "h oeiart"
        0x72, 0x6E, 0x74, 0x20, 0x6C, 0x73, 0x76, 0x63,   //  4  'a' -> "rnt lsvc"
        0x65, 0x61, 0x69, 0x20, 0x6F, 0x74, 0x72, 0x21,   //  5  'h' -> "eai otr!"
        0x20, 0x67, 0x64, 0x6F, 0x65, 0x27, 0x74, 0x6B,   //  6  'n' -> " gdoe'tk"
        0x65, 0x20, 0x6F, 0x61, 0x69, 0x74, 0x73, 0x79,   //  7  'r' -> "e oaitsy"
        0x20, 0x74, 0x65, 0x68, 0x73, 0x6F, 0x69, 0x2E,   //  8  's' -> " tehsoi."
        0x6E, 0x73, 0x74, 0x6C, 0x63, 0x67, 0x6D, 0x64,   //  9  'i' -> "nstlcgmd"
        0x20, 0x72, 0x74, 0x6E, 0x6C, 0x73, 0x67, 0x61,   // 10  'u' -> " rtnlsga"
        0x6C, 0x65, 0x20, 0x64, 0x6F, 0x61, 0x79, 0x69,   // 11  'l' -> "le doayi"
        0x20, 0x65, 0x6F, 0x69, 0x2E, 0x72, 0x73, 0x61,   // 12  'd' -> " eoi.rsa"
        0x6F, 0x20, 0x2E, 0x2C, 0x21, 0x74, 0x73, 0x69,   // 13  'y' -> "o .,!tsi"
        0x20, 0x68, 0x6F, 0x65, 0x75, 0x61, 0x69, 0x72,   // 14  'g' -> " hoeuair"
        0x6B, 0x65, 0x6F, 0x61, 0x68, 0x74, 0x72, 0x80,   // 15  'c' -> "keoahtr" + 0x80
    };

    private readonly byte[] _table;

    /// <summary>Builds a codec over a 144-byte table.</summary>
    public TextCodec(byte[] table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Length != TableLength)
            throw new ArgumentException($"the table is exactly {TableLength} bytes", nameof(table));
        _table = table;
    }

    /// <summary>A codec using the shipped v1.2 table.</summary>
    public static TextCodec Shipped { get; } = new(ShippedTable);

    /// <summary>
    /// A codec using the table read out of an attached game, or the shipped one when the read fails.
    /// Reading it live means a build with a different table still decodes correctly.
    /// </summary>
    public static TextCodec FromMemory(byte[]? liveTable) =>
        liveTable is { Length: TableLength } ? new TextCodec(liveTable) : Shipped;

    /// <summary>The two characters a digraph byte expands to.</summary>
    public string Expand(byte b)
    {
        if (b < FirstDigraphByte) return ((char)b).ToString();
        int i = b - FirstDigraphByte;
        int group = i >> 3, slot = i & 7;
        return string.Concat((char)_table[group], (char)_table[16 + group * 8 + slot]);
    }

    /// <summary>
    /// Decodes a run of game text, stopping at the first NUL. <c>0x0D</c> becomes a newline; any
    /// other control byte is rendered as <c>&lt;XX&gt;</c> so nothing is silently dropped.
    /// </summary>
    public string Decode(ReadOnlySpan<byte> raw)
    {
        var sb = new StringBuilder(raw.Length * 2);
        foreach (byte b in raw)
        {
            if (b == 0) break;
            if (b >= FirstDigraphByte) sb.Append(Expand(b));
            else if (b == 0x0D) sb.Append('\n');
            else if (b >= 0x20 && b <= 0x7E) sb.Append((char)b);
            else sb.Append('<').Append(b.ToString("X2")).Append('>');
        }
        return sb.ToString();
    }
}
