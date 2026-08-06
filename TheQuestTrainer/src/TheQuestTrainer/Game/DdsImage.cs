namespace TheQuestTrainer.Game;

/// <summary>A decoded picture as 32-bit BGRA rows, top row first — what WPF wants to be handed.</summary>
/// <param name="Width">Pixels across.</param>
/// <param name="Height">Pixels down.</param>
/// <param name="Bgra">Width × Height × 4 bytes, blue, green, red, alpha.</param>
public sealed record DecodedImage(int Width, int Height, byte[] Bgra)
{
    /// <summary>Bytes per row.</summary>
    public int Stride => Width * 4;

    /// <summary>The pixel at (<paramref name="x"/>, <paramref name="y"/>) as 0xAARRGGBB, or 0 outside.</summary>
    public uint Pixel(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
        int at = (y * Width + x) * 4;
        return (uint)(Bgra[at + 3] << 24 | Bgra[at + 2] << 16 | Bgra[at + 1] << 8 | Bgra[at]);
    }
}

/// <summary>
/// Just enough DDS to read The Quest's world map.
///
/// The game ships its art as DXT1 (BC1) — the world map is one 588×588 surface with no mipmaps —
/// and WPF cannot open DDS at all, so the four-by-four blocks are unpacked here into plain BGRA.
/// BC1 is eight bytes per block: two RGB565 endpoints and sixteen two-bit indices into a palette
/// interpolated between them. Which interpolation depends on the order of the endpoints, and that
/// is also how the format encodes transparency, so the two cases are not interchangeable.
///
/// Nothing else about DDS is implemented, deliberately: an unsupported header is a null and a
/// message, not a partial decode of bytes that mean something else.
/// </summary>
public static class DdsImage
{
    private const int Magic = 0x20534444;        // "DDS "
    private const int HeaderBytes = 128;         // magic + the 124-byte header
    private const uint FourCcDxt1 = 0x31545844;  // "DXT1"

    /// <summary>Largest surface this will decode, so a malformed header cannot ask for a gigabyte.</summary>
    public const int MaxSide = 8192;

    /// <summary>
    /// Decodes a DXT1 DDS, or returns null and says why. Mipmaps after the first surface are
    /// ignored — the trainer only ever wants the full-size one.
    /// </summary>
    public static DecodedImage? Decode(byte[] dds, out string detail)
    {
        ArgumentNullException.ThrowIfNull(dds);

        if (dds.Length < HeaderBytes || BitConverter.ToInt32(dds, 0) != Magic)
        {
            detail = "not a DDS file.";
            return null;
        }

        int height = BitConverter.ToInt32(dds, 12);
        int width = BitConverter.ToInt32(dds, 16);
        uint fourCc = BitConverter.ToUInt32(dds, 84);

        if (width <= 0 || height <= 0 || width > MaxSide || height > MaxSide)
        {
            detail = $"implausible dimensions ({width}×{height}).";
            return null;
        }

        if (fourCc != FourCcDxt1)
        {
            detail = $"compressed as {FourCcName(fourCc)}, which this reader does not decode.";
            return null;
        }

        int blocksX = (width + 3) / 4;
        int blocksY = (height + 3) / 4;
        long need = (long)HeaderBytes + (long)blocksX * blocksY * 8;
        if (dds.Length < need)
        {
            detail = $"truncated: {dds.Length} bytes for a {width}×{height} surface needing {need}.";
            return null;
        }

        var bgra = new byte[width * height * 4];
        var palette = new byte[4 * 4];
        int at = HeaderBytes;

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++, at += 8)
            {
                ushort c0 = BitConverter.ToUInt16(dds, at);
                ushort c1 = BitConverter.ToUInt16(dds, at + 2);
                uint bits = BitConverter.ToUInt32(dds, at + 4);
                BuildPalette(c0, c1, palette);

                for (int j = 0; j < 4; j++)
                {
                    int y = by * 4 + j;
                    if (y >= height) break;
                    for (int i = 0; i < 4; i++)
                    {
                        int x = bx * 4 + i;
                        if (x >= width) break;
                        int index = (int)(bits >> (2 * (j * 4 + i))) & 3;
                        Array.Copy(palette, index * 4, bgra, (y * width + x) * 4, 4);
                    }
                }
            }
        }

        detail = $"{width}×{height} DXT1.";
        return new DecodedImage(width, height, bgra);
    }

    /// <summary>
    /// Fills the four BGRA palette entries of one block.
    ///
    /// The endpoints' order is the format's flag: with <paramref name="c0"/> the greater the block is
    /// opaque and the two middle colours are one-third and two-thirds along, and otherwise there is
    /// one midpoint and the fourth entry is transparent black.
    /// </summary>
    private static void BuildPalette(ushort c0, ushort c1, byte[] palette)
    {
        Unpack565(c0, palette, 0);
        Unpack565(c1, palette, 4);

        if (c0 > c1)
        {
            for (int k = 0; k < 3; k++)
            {
                palette[8 + k] = (byte)((2 * palette[k] + palette[4 + k]) / 3);
                palette[12 + k] = (byte)((palette[k] + 2 * palette[4 + k]) / 3);
            }
            palette[11] = 0xFF;
            palette[15] = 0xFF;
        }
        else
        {
            for (int k = 0; k < 3; k++)
            {
                palette[8 + k] = (byte)((palette[k] + palette[4 + k]) / 2);
                palette[12 + k] = 0;
            }
            palette[11] = 0xFF;
            palette[15] = 0x00;     // the one transparent entry BC1 can express
        }
    }

    /// <summary>Expands an RGB565 word into BGRA, replicating the high bits so white stays white.</summary>
    private static void Unpack565(ushort c, byte[] into, int at)
    {
        int r = (c >> 11) & 0x1F, g = (c >> 5) & 0x3F, b = c & 0x1F;
        into[at] = (byte)(b << 3 | b >> 2);
        into[at + 1] = (byte)(g << 2 | g >> 4);
        into[at + 2] = (byte)(r << 3 | r >> 2);
        into[at + 3] = 0xFF;
    }

    private static string FourCcName(uint fourCc)
    {
        Span<char> chars = stackalloc char[4];
        for (int i = 0; i < 4; i++)
        {
            char c = (char)((fourCc >> (8 * i)) & 0xFF);
            chars[i] = c is >= ' ' and < (char)0x7F ? c : '?';
        }
        return new string(chars);
    }
}
