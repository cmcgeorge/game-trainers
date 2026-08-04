namespace DarkDesigns1Trainer.Game;

/// <summary>
/// Where the party is: the dungeon level, the square, and which way it faces.
///
/// The game holds these as four <c>uint16</c> in a row at <c>DGROUP:0x1320</c>, and its
/// <c>DDCHARS.DAT</c> loader reads them straight out of the save header at offsets <c>0x08</c>–
/// <c>0x0F</c> (its own reads are 8, 2, 2, 2, 2 and 128 bytes, which is how the header's field
/// boundaries were found). The saver writes them back the same way, so the same four values
/// describe the live party and the saved one.
/// </summary>
public readonly record struct PartyPosition(int Level, int X, int Y, int Facing)
{
    /// <summary>True when the party is in the castle rather than back in town.</summary>
    public bool InDungeon => Level >= MapFormat.MinLevel && Level <= MapFormat.MaxLevel;

    public string FacingName => MapFormat.FacingName(Facing);

    public string LevelName => MapBook.LevelName(Level);

    public string Coord => $"({X}, {Y})";

    public string Describe() => InDungeon
        ? $"{LevelName} — X {X} · Y {Y} facing {FacingName}"
        : "In town — a position only means something inside Grelminar's castle.";

    /// <summary>True when all four fields are inside the ranges the game itself enforces.</summary>
    public bool IsPlausible => MapFormat.IsPlausiblePosition(Level, X, Y, Facing);

    /// <summary>Encodes the block back into the four <c>uint16</c> the game reads.</summary>
    public byte[] ToBytes()
    {
        var b = new byte[MapFormat.PositionBlockSize];
        WriteTo(b, 0);
        return b;
    }

    /// <summary>Writes the four <c>uint16</c> into an existing buffer.</summary>
    public void WriteTo(byte[] b, int offset)
    {
        if (b == null) throw new ArgumentNullException(nameof(b));
        if (offset < 0 || offset + MapFormat.PositionBlockSize > b.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        Write(b, offset + MapFormat.PosOffLevel, Level);
        Write(b, offset + MapFormat.PosOffX, X);
        Write(b, offset + MapFormat.PosOffY, Y);
        Write(b, offset + MapFormat.PosOffFacing, Facing);

        static void Write(byte[] buf, int at, int value)
        {
            buf[at] = (byte)(value & 0xFF);
            buf[at + 1] = (byte)((value >> 8) & 0xFF);
        }
    }

    /// <summary>Decodes the four <c>uint16</c> at <paramref name="offset"/>.</summary>
    public static PartyPosition FromBytes(byte[] b, int offset = 0)
    {
        if (b == null) throw new ArgumentNullException(nameof(b));
        if (offset < 0 || offset + MapFormat.PositionBlockSize > b.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        return new PartyPosition(
            Read(b, offset + MapFormat.PosOffLevel),
            Read(b, offset + MapFormat.PosOffX),
            Read(b, offset + MapFormat.PosOffY),
            Read(b, offset + MapFormat.PosOffFacing));

        static int Read(byte[] buf, int at) => buf[at] | (buf[at + 1] << 8);
    }

    /// <summary>The same position with every field clamped into range (level clamped to the castle).</summary>
    public PartyPosition Clamped() => new(
        Math.Clamp(Level, MapFormat.MinLevel, MapFormat.MaxLevel),
        Math.Clamp(X, 0, MapFormat.GridSize - 1),
        Math.Clamp(Y, 0, MapFormat.GridSize - 1),
        Math.Clamp(Facing, 0, MapFormat.Directions - 1));
}
