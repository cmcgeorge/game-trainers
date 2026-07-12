using System.Text;

namespace WarOfTheLanceTrainer.Game;

/// <summary>
/// War of the Lance stores all of its on-screen strings in a "high-bit ASCII" form: every text
/// byte is the ASCII code with bit 7 set (so 'A' 0x41 -> 0xC1, space 0x20 -> 0xA0), and 0xFF
/// separates one string from the next. NAT.DAT, WL2.DAT and MENU.DAT are simply length-prefixed
/// runs of these separated strings. This codec is the single place that encoding is handled, so
/// signatures and dumps never hand-write raw bytes.
/// </summary>
public static class GameText
{
    public const byte Separator = 0xFF;

    /// <summary>
    /// Encodes ASCII to the game's high-bit form (no separator added). Throws on any character
    /// outside the 7-bit ASCII range, since the codec has no representation for it and a silent
    /// truncation would corrupt signatures and dumps.
    /// </summary>
    public static byte[] Encode(string text)
    {
        var bytes = new byte[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c > 0x7F)
                throw new ArgumentException("War of the Lance text is ASCII-only.", nameof(text));
            bytes[i] = (byte)(c | 0x80);
        }
        return bytes;
    }

    /// <summary>Decodes a high-bit run up to (not including) the next separator or buffer end.</summary>
    public static string DecodeWord(byte[] data, int start, out int consumed)
    {
        var sb = new StringBuilder();
        int i = start;
        for (; i < data.Length; i++)
        {
            byte b = data[i];
            if (b == Separator) { i++; break; }
            sb.Append((char)(b & 0x7F));
        }
        consumed = i - start;
        return sb.ToString();
    }

    /// <summary>Splits a payload of separator-delimited high-bit strings into a list.</summary>
    public static List<string> DecodeAll(byte[] payload)
    {
        var words = new List<string>();
        int i = 0;
        while (i < payload.Length)
        {
            string w = DecodeWord(payload, i, out int used);
            if (used == 0) { i++; continue; }
            words.Add(w);
            i += used;
        }
        return words;
    }
}
