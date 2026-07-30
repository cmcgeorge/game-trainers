using System.Globalization;

namespace SwordOfAragonTrainer.Game;

/// <summary>
/// Field access for one comma-separated line of an <c>ARAGON.HS&lt;letter&gt;</c> save. QuickBASIC's
/// <c>WRITE #</c> emits bare commas with no padding and quotes only string fields, so splitting on
/// commas is exact. Every helper leaves fields it was not asked to change byte-identical, which is
/// what lets the trainer edit a handful of numbers in a 286-line file without disturbing the rest.
/// </summary>
internal static class CsvRow
{
    /// <summary>Splits a line into its raw fields.</summary>
    public static string[] Split(string line) => line.Split(',');

    /// <summary>Number of fields in a line.</summary>
    public static int Count(string line) => Split(line).Length;

    /// <summary>Strips the surrounding quotes QuickBASIC puts around string fields.</summary>
    public static string Unquote(string field)
    {
        string text = field.Trim();
        return text.Length >= 2 && text[0] == '"' && text[^1] == '"' ? text[1..^1] : text;
    }

    /// <summary>Reads a field as a double, or <paramref name="fallback"/> if absent/unparseable.</summary>
    public static double GetDouble(string line, int index, double fallback = 0)
    {
        var fields = Split(line);
        if (index < 0 || index >= fields.Length) return fallback;
        return double.TryParse(fields[index].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                               out double value)
            ? value
            : fallback;
    }

    /// <summary>Reads a field as an int (truncating any fractional part), or <paramref name="fallback"/>.</summary>
    public static int GetInt(string line, int index, int fallback = 0)
    {
        var fields = Split(line);
        if (index < 0 || index >= fields.Length) return fallback;
        string text = fields[index].Trim();
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            return value;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
            ? (int)d
            : fallback;
    }

    /// <summary>Reads a field as an unquoted string, or empty if absent.</summary>
    public static string GetString(string line, int index)
    {
        var fields = Split(line);
        return index >= 0 && index < fields.Length ? Unquote(fields[index]) : string.Empty;
    }

    /// <summary>
    /// Returns <paramref name="line"/> with field <paramref name="index"/> replaced. A request for a
    /// field the line does not have is ignored rather than padded — the game wrote the line, so its
    /// arity is authoritative.
    /// </summary>
    public static string SetInt(string line, int index, int value)
        => SetRaw(line, index, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Returns <paramref name="line"/> with field <paramref name="index"/> replaced by a number
    /// formatted the way QuickBASIC writes singles: no thousands separators, no exponent, and at most
    /// four decimals (the game's own values never carry more).
    ///
    /// A non-finite value is written as <c>0</c>. <c>Math.Clamp</c> passes NaN straight through (both
    /// of its comparisons are false for NaN), so without this guard a NaN typed into a bound text box
    /// would reach the file as the literal text <c>NaN</c> — which QuickBASIC's <c>INPUT #</c> cannot
    /// read back as a number.
    /// </summary>
    public static string SetDouble(string line, int index, double value)
    {
        if (!double.IsFinite(value)) value = 0;
        return SetRaw(line, index, value.ToString("0.####", CultureInfo.InvariantCulture));
    }

    private static string SetRaw(string line, int index, string text)
    {
        var fields = Split(line);
        if (index < 0 || index >= fields.Length) return line;
        fields[index] = text;
        return string.Join(',', fields);
    }
}
