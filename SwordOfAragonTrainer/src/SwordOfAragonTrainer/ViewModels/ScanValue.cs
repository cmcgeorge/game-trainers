using System.Globalization;

namespace SwordOfAragonTrainer.ViewModels;

/// <summary>Parsing and range helpers for the numbers the live tab reads and writes.</summary>
public static class ScanValue
{
    /// <summary>
    /// Parses user-typed text as a signed integer, accepting decimal (<c>100</c>) or hex
    /// (<c>0x64</c> / <c>64h</c>). Returns false on empty or malformed input.
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

    /// <summary>Parses user-typed text as a decimal number (for MBF gold values).</summary>
    public static bool TryParseDouble(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Whether a value fits the given width, so a write cannot be silently truncated.</summary>
    public static bool FitsWidth(long value, ScanWidth width) => width switch
    {
        ScanWidth.Byte => value is >= sbyte.MinValue and <= byte.MaxValue,
        ScanWidth.Int16 => value is >= short.MinValue and <= ushort.MaxValue,
        _ => value is >= int.MinValue and <= uint.MaxValue,
    };

    /// <summary>
    /// Folds a signed value into the unsigned representation <see cref="MemorySearcher"/> stores, so a
    /// typed <c>-1</c> matches the <c>0xFFFF</c> it decodes.
    /// </summary>
    public static long Canonicalize(long value, ScanWidth width) => width switch
    {
        ScanWidth.Byte => value & 0xFF,
        ScanWidth.Int16 => value & 0xFFFF,
        _ => value & 0xFFFFFFFFL,
    };
}
