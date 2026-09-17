namespace Space1889Trainer.Files;

/// <summary>A decoded 16-colour picture: one EGA colour index (0..15) per pixel, row-major.</summary>
public sealed class Picture
{
    public Picture(int width, int height, byte[] pixels)
    {
        if (pixels.Length != width * height) throw new ArgumentException("pixel count does not match the size", nameof(pixels));
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>EGA colour indices, <c>Pixels[y * Width + x]</c>.</summary>
    public byte[] Pixels { get; }
}

/// <summary>
/// The game's PIC format (M.EXE LOADPIC 1000:9D7C / READPIC 1000:9F87, unpacker 1000:9BC2):
/// <code>
/// +0  u16 height    +2 u16 width / 8    +4 u8[16] CGA dither map (unused on EGA/VGA)
/// +20 u16 unpacked size    +22 u16 packed size    +24 PackBits data, 4 bits per pixel, high nibble first
/// </code>
/// Archives (ALL.PIC, ALL.GND, ALL.PEO, TITLES.PIC, 0.PEO, 0.PLN) are a table of u32 offsets (−1 = empty)
/// followed by PIC records.
/// </summary>
public static class PicFile
{
    /// <summary>The default EGA palette as 0xRRGGBB; the game never reprograms it.</summary>
    public static readonly IReadOnlyList<int> EgaPalette = new[]
    {
        0x000000, 0x0000AA, 0x00AA00, 0x00AAAA, 0xAA0000, 0xAA00AA, 0xAA5500, 0xAAAAAA,
        0x555555, 0x5555FF, 0x55FF55, 0x55FFFF, 0xFF5555, 0xFF55FF, 0xFFFF55, 0xFFFFFF,
    };

    /// <summary>Header bytes before the packed data.</summary>
    public const int HeaderSize = 24;

    /// <summary>Decodes the PIC record at <paramref name="offset"/>, or returns null if it is malformed.</summary>
    public static Picture? Decode(byte[] data, int offset = 0)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (offset < 0 || offset > data.Length - HeaderSize) return null;
        int height = data[offset] | (data[offset + 1] << 8);
        int width = (data[offset + 2] | (data[offset + 3] << 8)) * 8;
        int unpacked = data[offset + 20] | (data[offset + 21] << 8);
        int packed = data[offset + 22] | (data[offset + 23] << 8);
        if (height <= 0 || width <= 0 || height > 1024 || width > 1024) return null;
        int start = offset + HeaderSize;
        if (packed > data.Length - start) return null;

        int rowBytes = width / 2;
        var raw = UnpackBits(data, start, packed, Math.Max(unpacked, rowBytes * height));
        if (raw.Length < rowBytes * height) return null;

        var pixels = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            int src = y * rowBytes, dst = y * width;
            for (int x = 0; x < rowBytes; x++)
            {
                byte b = raw[src + x];
                pixels[dst + x * 2] = (byte)(b >> 4);
                pixels[dst + x * 2 + 1] = (byte)(b & 0x0F);
            }
        }
        return new Picture(width, height, pixels);
    }

    /// <summary>PackBits: n &lt; 0x80 copies n+1 bytes; n &gt; 0x80 repeats the next byte 0x101−n times; 0x80 is a no-op.</summary>
    public static byte[] UnpackBits(byte[] src, int start, int length, int limit)
    {
        var output = new byte[limit];
        int o = 0, i = start, end = Math.Min(src.Length, start + length);
        while (i < end && o < limit)
        {
            int c = src[i++];
            if (c < 0x80)
            {
                int n = Math.Min(c + 1, Math.Min(end - i, limit - o));
                Array.Copy(src, i, output, o, n);
                i += c + 1;
                o += n;
            }
            else if (c > 0x80)
            {
                if (i >= end) break;
                byte b = src[i++];
                int n = Math.Min(0x101 - c, limit - o);
                output.AsSpan(o, n).Fill(b);
                o += n;
            }
        }
        if (o < limit) Array.Resize(ref output, o);
        return output;
    }

    /// <summary>Decodes member <paramref name="index"/> of an offset-table archive, or null.</summary>
    public static Picture? DecodeArchiveMember(byte[] archive, int index)
    {
        ArgumentNullException.ThrowIfNull(archive);
        if (index < 0 || index * 4 + 4 > archive.Length) return null;
        int offset = BitConverter.ToInt32(archive, index * 4);
        if (offset < 0 || offset >= archive.Length) return null;
        // The table ends where the first member begins; an index past it is not a member.
        int first = int.MaxValue;
        for (int i = 0; i * 4 + 4 <= archive.Length && i * 4 < first; i++)
        {
            int o = BitConverter.ToInt32(archive, i * 4);
            if (o >= 0) first = Math.Min(first, o);
        }
        return index * 4 < first ? Decode(archive, offset) : null;
    }
}
