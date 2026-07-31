using System.Text;

namespace AirborneRangerTrainer.Game;

/// <summary>
/// The layout of <c>ROSTER.DAT</c>, the game's career file.
///
/// <para>495 bytes: a six-byte header, six 81-byte ranger records, and a three-byte trailer. Each
/// record is two CRLF-ish text lines (each terminated by <c>0D FF</c>) followed by a ten-byte
/// binary tail. Line 1 carries the rank mnemonic, the name and a six-digit score; line 2 carries
/// the decoration ribbons. The tail repeats the rank as an index and the decorations as a bitmask.
/// </para>
///
/// <para>The field widths are pinned by the game's own blank-ranger template at <c>DGROUP:0x9F7E</c>
/// — <c>"    PFC                    000000"</c> — and the rank and decoration tables are the
/// literals at <c>DGROUP:0xBB64</c> and <c>DGROUP:0xBBA6</c>. The whole decode was checked against
/// the game's <b>Assign a Veteran Ranger</b> screen. See <c>docs/ReverseEngineering.md</c> §4.</para>
/// </summary>
public static class RosterFormat
{
    /// <summary>Bytes before the first record.</summary>
    public const int HeaderLength = 6;

    /// <summary>Bytes per ranger record.</summary>
    public const int RecordLength = 81;

    /// <summary>Ranger slots in the file.</summary>
    public const int RecordCount = 6;

    /// <summary>Bytes after the last record.</summary>
    public const int TrailerLength = 3;

    /// <summary>Total size of a well-formed <c>ROSTER.DAT</c>.</summary>
    public const int FileLength = HeaderLength + RecordCount * RecordLength + TrailerLength;

    /// <summary>The file the game reads and writes.</summary>
    public const string FileName = "ROSTER.DAT";

    // --- record layout -------------------------------------------------------

    /// <summary>Characters in the name/rank/score line.</summary>
    public const int LineLength = 33;

    /// <summary>Characters in the decorations line.</summary>
    public const int DecorationLineLength = 34;

    /// <summary>Record offset of the rank/name/score line.</summary>
    public const int OffLine1 = 0;

    /// <summary>Record offset of the decorations line.</summary>
    public const int OffLine2 = OffLine1 + LineLength + 2;

    /// <summary>Record offset of the ten-byte binary tail.</summary>
    public const int OffTail = OffLine2 + DecorationLineLength + 2;

    /// <summary>Bytes in the binary tail.</summary>
    public const int TailLength = 10;

    /// <summary>Line-1 column where the three-character rank mnemonic starts.</summary>
    public const int LineRankColumn = 4;

    /// <summary>Line-1 column where the name starts.</summary>
    public const int LineNameColumn = 8;

    /// <summary>Characters reserved for the name.</summary>
    public const int NameLength = 19;

    /// <summary>Line-1 column where the six-digit score starts.</summary>
    public const int LineScoreColumn = LineNameColumn + NameLength;

    /// <summary>Digits in the stored score.</summary>
    public const int ScoreDigits = 6;

    /// <summary>Largest score the six-digit field can hold.</summary>
    public const int MaxScore = 999_999;

    /// <summary>Tail offset of the rank index.</summary>
    public const int TailRankIndex = 1;

    /// <summary>Tail offset of the decoration bitmask.</summary>
    public const int TailDecorations = 2;

    /// <summary>Each text line ends with these two bytes.</summary>
    public static readonly byte[] LineTerminator = { 0x0D, 0xFF };

    /// <summary>Byte offset of record <paramref name="slot"/>.</summary>
    public static int RecordOffset(int slot)
    {
        if (slot < 0 || slot >= RecordCount) throw new ArgumentOutOfRangeException(nameof(slot));
        return HeaderLength + slot * RecordLength;
    }

    /// <summary>
    /// True when <paramref name="bytes"/> is a roster file the editor is willing to touch: the right
    /// length, and every record's two line terminators exactly where they belong. Anything else is
    /// refused rather than rewritten — a mis-parsed save is worse than no save editor.
    /// </summary>
    public static bool LooksLikeRoster(byte[]? bytes)
    {
        if (bytes == null || bytes.Length != FileLength) return false;
        for (int slot = 0; slot < RecordCount; slot++)
        {
            int b = RecordOffset(slot);
            if (bytes[b + OffLine1 + LineLength] != 0x0D || bytes[b + OffLine1 + LineLength + 1] != 0xFF) return false;
            if (bytes[b + OffLine2 + DecorationLineLength] != 0x0D || bytes[b + OffLine2 + DecorationLineLength + 1] != 0xFF) return false;
        }
        return true;
    }

    /// <summary>Reads <paramref name="length"/> ASCII characters at <paramref name="offset"/>.</summary>
    public static string ReadAscii(byte[] bytes, int offset, int length) =>
        Encoding.ASCII.GetString(bytes, offset, length);

    /// <summary>
    /// Writes <paramref name="text"/> into a fixed-width ASCII field, space-padded and truncated.
    /// Characters the game cannot render become spaces.
    /// </summary>
    public static void WriteAscii(byte[] bytes, int offset, int length, string? text)
    {
        for (int i = 0; i < length; i++)
        {
            char c = text != null && i < text.Length ? text[i] : ' ';
            bytes[offset + i] = (byte)(c is >= ' ' and < (char)127 ? c : ' ');
        }
    }

    /// <summary>
    /// The exact text <see cref="WriteAscii"/> would store for a ranger name: unrenderable
    /// characters replaced by spaces, truncated to <see cref="NameLength"/>, blanks trimmed from
    /// <b>both</b> ends.
    ///
    /// <para>Trimming both ends is required, not cosmetic. The reader trims both ends too, and a
    /// setter that compares its sanitised input against the stored name would otherwise never
    /// converge on a leading space: <c>" Bob"</c> would be written, read back as <c>"Bob"</c>, and a
    /// subsequent attempt to type <c>"Bob"</c> would look like a no-op — leaving the file holding a
    /// name the game prints one column across with no way to correct it.</para>
    /// </summary>
    public static string SanitiseName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var clean = new StringBuilder(NameLength);
        foreach (char c in name)
        {
            if (clean.Length >= NameLength) break;
            clean.Append(c is >= ' ' and < (char)127 ? c : ' ');
        }
        return clean.ToString().Trim();
    }
}
