using System.IO;

namespace WarOfTheLanceTrainer.Game;

/// <summary>
/// The 7-byte container header shared by every War of the Lance data file
/// (NAT.DAT, WL.DAT, WL2.DAT, SCEN.DAT, WL.UNT, WL.MAP, MENU.DAT, Q1/Q2.DAT, P*.BIN, MAP*.BIN):
///
/// <code>
///   +0  u8   magic        always 0xFD
///   +1  u16  checksum     little-endian; differs per file, identical across a WL/SCEN set
///   +3  u16  tag          little-endian; 0x0000 for most files, an ASCII pair for MAP*.BIN
///   +5  u16  payloadLen   little-endian; file length == payloadLen + 7 (verified for all files)
///   +7  ..   payload
/// </code>
///
/// The length invariant (payload + 7 == file size) was confirmed against every shipped file, so
/// it doubles as a cheap "is this really a WOTL container?" gate.
/// </summary>
public readonly struct SaveContainer
{
    public const byte Magic = 0xFD;
    public const int HeaderSize = 7;

    public byte MagicByte { get; }
    public ushort Checksum { get; }
    public ushort Tag { get; }
    public int PayloadLength { get; }

    /// <summary>The raw file bytes this header was parsed from (payload starts at <see cref="HeaderSize"/>).</summary>
    public byte[] Raw { get; }

    private SaveContainer(byte magic, ushort checksum, ushort tag, int payloadLen, byte[] raw)
    {
        MagicByte = magic;
        Checksum = checksum;
        Tag = tag;
        PayloadLength = payloadLen;
        Raw = raw;
    }

    /// <summary>True when the magic byte and the payload-length invariant both hold.</summary>
    public bool IsValid => MagicByte == Magic && HeaderSize + PayloadLength == Raw.Length;

    /// <summary>The payload bytes (a defensive copy).</summary>
    public byte[] Payload()
    {
        var slice = new byte[PayloadLength];
        Array.Copy(Raw, HeaderSize, slice, 0, PayloadLength);
        return slice;
    }

    /// <summary>Parses a container header out of a whole data file's bytes.</summary>
    public static SaveContainer Parse(byte[] file)
    {
        if (file == null) throw new ArgumentNullException(nameof(file));
        if (file.Length < HeaderSize)
            throw new ArgumentException("File is too small to hold a WOTL container header.", nameof(file));

        byte magic = file[0];
        ushort checksum = (ushort)(file[1] | (file[2] << 8));
        ushort tag = (ushort)(file[3] | (file[4] << 8));
        int payloadLen = file[5] | (file[6] << 8);
        return new SaveContainer(magic, checksum, tag, payloadLen, file);
    }

    /// <summary>Parses and throws when the header does not validate.</summary>
    public static SaveContainer ParseValidated(byte[] file)
    {
        var c = Parse(file);
        if (!c.IsValid)
            throw new InvalidDataException(
                $"Not a valid WOTL container (magic=0x{c.MagicByte:X2}, payloadLen={c.PayloadLength}, fileLen={file.Length}).");
        return c;
    }
}
