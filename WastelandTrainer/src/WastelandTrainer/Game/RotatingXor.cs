using System.IO;

namespace WastelandTrainer.Game;

/// <summary>
/// Wasteland's rotating-XOR stream cipher, used to obfuscate the fixed-size blocks inside a
/// GAME1 / GAME2 save file (the savegame block this trainer edits, and the per-map blocks it does
/// not). The two bytes immediately after a block's <c>"msqN"</c> tag seed the cipher and also encode
/// a running checksum used to verify a clean decode.
///
/// <para><b>Codec.</b> Let <c>x1</c>, <c>x2</c> be the two seed bytes. The running key starts at
/// <c>x1 ^ x2</c> and advances by <c>0x1F</c> (mod 256) after every byte. Each ciphertext byte is
/// XORed with the current key to recover the plaintext. A checksum accumulator starts at 0 and has
/// every decoded byte subtracted from it (mod 65536); after the whole block it must equal
/// <c>x1 | (x2 &lt;&lt; 8)</c>, otherwise the block did not decode cleanly.</para>
///
/// <para><b>Losslessness.</b> Because the seed's low/high bytes ARE that end-checksum, re-encoding
/// an unchanged plaintext reproduces the original seed and ciphertext byte-for-byte; changing the
/// plaintext simply yields the new seed the game will accept. This is what makes save editing safe:
/// an untouched block round-trips exactly. Derived from the file format documented by the
/// open-source <c>kayahr/wastelib</c> project and verified against the shipped saves in FormatCheck.</para>
/// </summary>
public static class RotatingXor
{
    /// <summary>Amount the rolling key advances (mod 256) after each processed byte.</summary>
    public const int KeyStep = 0x1F;

    /// <summary>Bytes the seed/checksum header occupies (the two bytes before the ciphertext).</summary>
    public const int SeedSize = 2;

    /// <summary>
    /// Decodes <paramref name="size"/> plaintext bytes from <paramref name="data"/> starting at
    /// <paramref name="offset"/>, where <c>data[offset]</c>/<c>data[offset+1]</c> are the two seed
    /// bytes and the ciphertext follows. Throws <see cref="InvalidDataException"/> when the trailing
    /// checksum does not match (the block is not a clean rotating-XOR payload of that length).
    /// </summary>
    public static byte[] Decode(byte[] data, int offset, int size)
    {
        if (offset < 0 || size < 0 || data.Length - offset < SeedSize + size)
            throw new ArgumentOutOfRangeException(nameof(size), "Block does not fit inside the buffer.");

        int x1 = data[offset];
        int x2 = data[offset + 1];
        int key = x1 ^ x2;
        int endChecksum = x1 | (x2 << 8);
        int checksum = 0;

        var result = new byte[size];
        int p = offset + SeedSize;
        for (int i = 0; i < size; i++)
        {
            int plain = data[p++] ^ key;
            result[i] = (byte)plain;
            checksum = (checksum - plain) & 0xFFFF;
            key = (key + KeyStep) & 0xFF;
        }

        if (checksum != endChecksum)
            throw new InvalidDataException(
                $"Rotating-XOR checksum mismatch (got 0x{checksum:X4}, expected 0x{endChecksum:X4}).");
        return result;
    }

    /// <summary>
    /// Encodes <paramref name="plaintext"/> into a <c><see cref="SeedSize"/> + plaintext.Length</c>-byte
    /// block: two seed bytes followed by the ciphertext. The seed is derived from the plaintext so the
    /// block decodes cleanly and, for unchanged input, reproduces the original bytes exactly.
    /// </summary>
    public static byte[] Encode(byte[] plaintext)
    {
        int sum = 0;
        foreach (byte b in plaintext) sum = (sum + b) & 0xFFFF;
        int endChecksum = (-sum) & 0xFFFF;   // the decoder subtracts every byte from 0

        int x1 = endChecksum & 0xFF;
        int x2 = (endChecksum >> 8) & 0xFF;
        int key = x1 ^ x2;

        var outBuf = new byte[SeedSize + plaintext.Length];
        outBuf[0] = (byte)x1;
        outBuf[1] = (byte)x2;
        for (int i = 0; i < plaintext.Length; i++)
        {
            outBuf[SeedSize + i] = (byte)(plaintext[i] ^ key);
            key = (key + KeyStep) & 0xFF;
        }
        return outBuf;
    }

    /// <summary>
    /// Encodes <paramref name="plaintext"/> in place over <paramref name="destination"/> starting at
    /// <paramref name="offset"/> (writing the two seed bytes then the ciphertext), the inverse of
    /// <see cref="Decode"/>. Used to write an edited payload back into a copy of the save file.
    /// </summary>
    public static void EncodeInto(byte[] plaintext, byte[] destination, int offset)
    {
        var block = Encode(plaintext);
        Array.Copy(block, 0, destination, offset, block.Length);
    }
}
