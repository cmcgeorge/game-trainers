using System.Globalization;

namespace RailroadTycoonTrainer.ViewModels;

/// <summary>Parsing/formatting helpers for the numeric values the value scanner reads and writes.</summary>
public static class ScanValue
{
    /// <summary>
    /// Parses user-typed text as a signed value, accepting either decimal (<c>500</c>) or hex
    /// (<c>0x1F4</c> / <c>1F4h</c>). Returns false on empty or malformed input.
    /// </summary>
    public static bool TryParse(string? text, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        if (text.EndsWith("h", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(text.AsSpan(0, text.Length - 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Whether <paramref name="value"/> fits in the given scan width (so a write won't be truncated).</summary>
    public static bool FitsWidth(long value, ScanWidth width) => width switch
    {
        ScanWidth.Byte  => value is >= sbyte.MinValue and <= byte.MaxValue,
        ScanWidth.Int16 => value is >= short.MinValue and <= ushort.MaxValue,
        _               => value is >= int.MinValue and <= uint.MaxValue,
    };

    /// <summary>
    /// Folds a signed value into the unsigned representation the scanner stores, so a typed <c>-1</c>
    /// matches the <c>0xFF</c>/<c>0xFFFF</c>/<c>0xFFFFFFFF</c> the searcher decodes. Values already in
    /// the unsigned range pass through unchanged. Railroad Tycoon's cash is a signed 16-bit word that
    /// can go negative when a railroad is in the red, so folding matters for a debt-recovery scan.
    /// </summary>
    public static long Canonicalize(long value, ScanWidth width) => width switch
    {
        ScanWidth.Byte  => value & 0xFF,
        ScanWidth.Int16 => value & 0xFFFF,
        _               => value & 0xFFFFFFFFL,
    };
}
