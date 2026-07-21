namespace ColonizationTrainer.Game;

/// <summary>
/// Little-endian read/write helpers over a byte buffer. Colonization saves are an x86 DOS format, so
/// every multi-byte field is little-endian. All accessors bounds-check and throw rather than reading
/// past the buffer, so a truncated or mis-sized save can never silently corrupt neighbouring bytes.
/// </summary>
public static class Bytes
{
    public static int U8(byte[] d, int off)
    {
        Guard(d, off, 1);
        return d[off];
    }

    public static int U16(byte[] d, int off)
    {
        Guard(d, off, 2);
        return d[off] | (d[off + 1] << 8);
    }

    public static short S16(byte[] d, int off) => unchecked((short)U16(d, off));

    public static long U32(byte[] d, int off)
    {
        Guard(d, off, 4);
        return (uint)(d[off] | (d[off + 1] << 8) | (d[off + 2] << 16) | (d[off + 3] << 24));
    }

    public static void WriteU8(byte[] d, int off, int value)
    {
        Guard(d, off, 1);
        d[off] = (byte)value;
    }

    public static void WriteU16(byte[] d, int off, int value)
    {
        Guard(d, off, 2);
        d[off] = (byte)(value & 0xFF);
        d[off + 1] = (byte)((value >> 8) & 0xFF);
    }

    public static void WriteS16(byte[] d, int off, short value) => WriteU16(d, off, (ushort)value);

    public static void WriteU32(byte[] d, int off, long value)
    {
        Guard(d, off, 4);
        ulong v = unchecked((ulong)value);
        d[off] = (byte)(v & 0xFF);
        d[off + 1] = (byte)((v >> 8) & 0xFF);
        d[off + 2] = (byte)((v >> 16) & 0xFF);
        d[off + 3] = (byte)((v >> 24) & 0xFF);
    }

    private static void Guard(byte[] d, int off, int width)
    {
        if (d == null) throw new ArgumentNullException(nameof(d));
        if (off < 0 || off > d.Length - width)
            throw new ArgumentOutOfRangeException(nameof(off),
                $"field at 0x{off:X} ({width} bytes) lies outside the {d.Length}-byte buffer.");
    }
}
