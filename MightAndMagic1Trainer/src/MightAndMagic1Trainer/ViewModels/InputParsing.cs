using System.Globalization;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>
/// Parsing for the small numeric fields the view models read from the UI (the X/Y search
/// pattern and the typed-teleport destination). Accepts decimal or <c>0x…</c> hex and
/// succeeds only when the value lands in <c>[0, maxInclusive]</c> — callers pass the upper
/// bound that fits their domain (0xFF for a raw byte, 15 for a per-area map cell).
/// </summary>
internal static class InputParsing
{
    public static bool TryParseByte(string? s, int maxInclusive, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        int parsed;
        bool ok = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed)
            : int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
        if (!ok || parsed < 0 || parsed > maxInclusive) return false;
        value = parsed;
        return true;
    }
}
