namespace SwordOfAragonTrainer.Game;

/// <summary>
/// Microsoft Binary Format single-precision conversion. QuickBASIC 3.0 predates Microsoft's move to
/// IEEE 754, so every <c>SINGLE</c> the game stores — the player's gold above all — uses MBF:
///
/// <code>
/// bytes: [m0][m1][m2][exp]                      (little-endian, in file and in RAM)
/// value = (-1)^sign * (1 + mantissa / 2^23) * 2^(exp - 129)
///         sign     = bit 7 of m2
///         mantissa = ((m2 &amp; 0x7F) &lt;&lt; 16) | (m1 &lt;&lt; 8) | m0
///         exp == 0 =&gt; the value is zero
/// </code>
///
/// Two consequences the trainer relies on:
/// <list type="number">
/// <item>An IEEE float scan can never find these values — the bit patterns differ.</item>
/// <item>For positive values MBF *is* monotonic when read as an unsigned little-endian 32-bit
/// integer (the exponent occupies the most significant byte), so an ordinary unknown-value Int32
/// scan narrowed by Increased/Decreased does track gold correctly.</item>
/// </list>
/// </summary>
public static class Mbf
{
    /// <summary>Largest magnitude an MBF single can hold (exponent 255, full mantissa).</summary>
    public static readonly double MaxMagnitude = (2.0 - Math.Pow(2, -23)) * Math.Pow(2, 126);

    /// <summary>Decodes four little-endian bytes at <paramref name="offset"/> as an MBF single.</summary>
    public static double ToDouble(ReadOnlySpan<byte> bytes, int offset = 0)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), "MBF single needs 4 bytes.");

        int m0 = bytes[offset], m1 = bytes[offset + 1], m2 = bytes[offset + 2], exp = bytes[offset + 3];
        if (exp == 0) return 0.0;

        int mantissa = ((m2 & 0x7F) << 16) | (m1 << 8) | m0;
        double magnitude = (1.0 + mantissa / 8388608.0) * Math.Pow(2, exp - 129);
        return (m2 & 0x80) != 0 ? -magnitude : magnitude;
    }

    /// <summary>Decodes an unsigned little-endian 32-bit word that holds an MBF single.</summary>
    public static double FromRaw(uint raw)
    {
        Span<byte> b = stackalloc byte[4];
        b[0] = (byte)raw;
        b[1] = (byte)(raw >> 8);
        b[2] = (byte)(raw >> 16);
        b[3] = (byte)(raw >> 24);
        return ToDouble(b);
    }

    /// <summary>
    /// Encodes <paramref name="value"/> as an MBF single into four bytes at
    /// <paramref name="offset"/>. Values too large for the format saturate at
    /// <see cref="MaxMagnitude"/>; values too small (or zero, or non-finite) encode as MBF zero,
    /// which is all four bytes clear.
    /// </summary>
    public static void Write(Span<byte> destination, double value, int offset = 0)
    {
        if (offset < 0 || offset + 4 > destination.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), "MBF single needs 4 bytes.");

        var target = destination.Slice(offset, 4);
        target.Clear();
        if (double.IsNaN(value) || double.IsInfinity(value) || value == 0.0) return;

        bool negative = value < 0;
        double magnitude = Math.Abs(value);
        if (magnitude > MaxMagnitude) magnitude = MaxMagnitude;

        // Normalise to [1, 2) and derive the biased exponent.
        int exponent = (int)Math.Floor(Math.Log2(magnitude));
        double fraction = magnitude / Math.Pow(2, exponent);
        // Log2 rounding can leave the fraction a hair outside [1, 2); nudge it back.
        while (fraction >= 2.0) { fraction /= 2.0; exponent++; }
        while (fraction < 1.0) { fraction *= 2.0; exponent--; }

        int mantissa = (int)Math.Round((fraction - 1.0) * 8388608.0);
        if (mantissa > 0x7FFFFF) { mantissa = 0; exponent++; }   // rounded up to the next power of two

        int biased = exponent + 129;
        if (biased <= 0) return;                                  // underflow -> MBF zero
        if (biased > 255) { biased = 255; mantissa = 0x7FFFFF; }  // saturate

        target[0] = (byte)mantissa;
        target[1] = (byte)(mantissa >> 8);
        target[2] = (byte)(((mantissa >> 16) & 0x7F) | (negative ? 0x80 : 0));
        target[3] = (byte)biased;
    }

    /// <summary>Encodes <paramref name="value"/> and returns it as four bytes.</summary>
    public static byte[] GetBytes(double value)
    {
        var buf = new byte[4];
        Write(buf, value);
        return buf;
    }
}
