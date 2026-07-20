using System.IO;
using System.Text;

namespace WastelandTrainer.Game;

/// <summary>
/// A loaded Wasteland save file (GAME1 or GAME2) with its savegame block located and decoded. Holds
/// the whole file image plus the decrypted <see cref="Payload"/>; editing the payload (through
/// <see cref="Header"/> or the seven <see cref="Characters"/> records) and then calling
/// <see cref="Save"/> re-encrypts just that block back into a copy of the file, leaving every other
/// block byte-for-byte untouched.
///
/// <para>Wasteland writes two save files and, on load, uses whichever holds the higher serial
/// (<see cref="SaveFormat.Serial"/>). <see cref="Save"/> bumps the serial so the edited file is the
/// one the game reads next.</para>
/// </summary>
public sealed class SaveGame
{
    /// <summary>The full save-file image (all blocks), with only the edited savegame block changed on save.</summary>
    public byte[] FileBytes { get; }

    /// <summary>Byte offset of the savegame block's <c>"msqN"</c> tag within <see cref="FileBytes"/>.</summary>
    public int BlockOffset { get; }

    /// <summary>The block tag, <c>"msq0"</c> (GAME1) or <c>"msq1"</c> (GAME2).</summary>
    public string Tag { get; }

    /// <summary>The decoded, editable 0x800-byte savegame payload.</summary>
    public byte[] Payload { get; }

    /// <summary>Typed view over the party-state / position fields at the start of the payload.</summary>
    public SaveHeader Header { get; }

    /// <summary>The seven character records, each a live view over its slice of the payload's character area.</summary>
    public CharacterRecord[] Characters { get; }

    /// <summary>Path the save was loaded from (used as the default save-back target).</summary>
    public string Path { get; }

    private SaveGame(string path, byte[] fileBytes, int blockOffset, string tag, byte[] payload)
    {
        Path = path;
        FileBytes = fileBytes;
        BlockOffset = blockOffset;
        Tag = tag;
        Payload = payload;
        Header = new SaveHeader(payload);
        Characters = new CharacterRecord[CharacterFormat.MaxSlots];
        for (int i = 0; i < Characters.Length; i++)
            Characters[i] = new CharacterRecord(payload, SaveFormat.CharacterOffset(i));
    }

    /// <summary>Copies character record <paramref name="slot"/>'s bytes back into the payload buffer,
    /// so a record edited in isolation is committed before the block is re-encoded.</summary>
    public void CommitCharacter(int slot)
    {
        if (slot < 0 || slot >= Characters.Length) return;
        Array.Copy(Characters[slot].Bytes, 0, Payload, SaveFormat.CharacterOffset(slot), CharacterFormat.RecordSize);
    }

    /// <summary>Copies all seven character records back into the payload buffer.</summary>
    public void CommitAllCharacters()
    {
        for (int i = 0; i < Characters.Length; i++) CommitCharacter(i);
    }

    // --- loading -------------------------------------------------------------

    /// <summary>Loads and decodes the savegame block from the file at <paramref name="path"/>.</summary>
    public static SaveGame Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (!TryLocate(bytes, out int offset, out string tag, out byte[] payload))
            throw new InvalidDataException(
                "No decodable Wasteland savegame block found. Pick a GAME1 or GAME2 file from a Wasteland save directory.");
        return new SaveGame(path, bytes, offset, tag, payload);
    }

    /// <summary>Loads from an in-memory image (used by FormatCheck against captured files).</summary>
    public static SaveGame FromBytes(string path, byte[] bytes)
    {
        if (!TryLocate(bytes, out int offset, out string tag, out byte[] payload))
            throw new InvalidDataException("No decodable Wasteland savegame block found.");
        return new SaveGame(path, (byte[])bytes.Clone(), offset, tag, payload);
    }

    /// <summary>
    /// Scans <paramref name="bytes"/> for the savegame block: an <c>"msqN"</c> tag whose following
    /// 0x800 bytes decode with a valid rotating-XOR checksum <i>and</i> look like party state (a sane
    /// member count and at least the first member a well-formed character record). The many per-map
    /// blocks share the tag but are variable-length and fail a fixed-0x800 decode, so this reliably
    /// picks the one savegame block. The documented offset is preferred when several candidates match.
    /// </summary>
    public static bool TryLocate(byte[] bytes, out int offset, out string tag, out byte[] payload)
    {
        offset = -1; tag = ""; payload = Array.Empty<byte>();
        int best = -1; string bestTag = ""; byte[]? bestPayload = null;

        for (int i = 0; i + SaveFormat.ReservedBlockSize <= bytes.Length; i++)
        {
            if (bytes[i] != (byte)'m' || bytes[i + 1] != (byte)'s' || bytes[i + 2] != (byte)'q') continue;
            byte t = bytes[i + 3];
            if (t != (byte)'0' && t != (byte)'1') continue;

            byte[] decoded;
            try { decoded = RotatingXor.Decode(bytes, i + SaveFormat.TagSize, SaveFormat.PayloadSize); }
            catch { continue; }                       // checksum failed => not the savegame block
            if (!LooksLikeSaveState(decoded)) continue;

            string candidateTag = "msq" + (char)t;
            // Prefer the documented shipped offset; otherwise keep the last (savegame block trails the maps).
            bool documented = i == SaveFormat.ShippedBlockOffset[t == (byte)'0' ? 0 : 1];
            if (documented) { offset = i; tag = candidateTag; payload = decoded; return true; }
            best = i; bestTag = candidateTag; bestPayload = decoded;
        }

        if (bestPayload == null) return false;
        offset = best; tag = bestTag; payload = bestPayload;
        return true;
    }

    /// <summary>Plausibility gate that separates the decoded savegame block from a map block that merely
    /// decoded cleanly: a sane current/total member count and — when the save has members — the first
    /// character record passing the shared occupancy test.</summary>
    private static bool LooksLikeSaveState(byte[] payload)
    {
        int members = payload[SaveFormat.CurrentMembers];
        int total = payload[SaveFormat.TotalMembers];
        if (members > CharacterFormat.MaxSlots) return false;
        if (total > CharacterFormat.MaxSlots * SaveFormat.GroupCount) return false;
        if (members == 0) return true;   // a valid empty-party save (rare, but legal)
        return CharacterRecord.IsValidRecord(payload, SaveFormat.CharacterArea);
    }

    // --- saving --------------------------------------------------------------

    /// <summary>Re-encodes the (committed) payload into a copy of the file image, returning the new bytes
    /// without touching disk. The tag and the zero reserved tail are preserved; only the seed + ciphertext
    /// are rewritten.</summary>
    public byte[] BuildFileBytes()
    {
        CommitAllCharacters();
        var outBytes = (byte[])FileBytes.Clone();
        RotatingXor.EncodeInto(Payload, outBytes, BlockOffset + SaveFormat.TagSize);
        return outBytes;
    }

    /// <summary>
    /// Commits every edit, bumps the serial so the game loads this file next, and writes it back. A
    /// one-time <c>.bak</c> copy of the original file is made first (never overwritten) so the pre-edit
    /// save is always recoverable. Pass a <paramref name="path"/> to save elsewhere.
    /// </summary>
    public void Save(string? path = null)
    {
        Header.BumpSerial();
        byte[] outBytes = BuildFileBytes();
        Array.Copy(outBytes, 0, FileBytes, 0, outBytes.Length);   // keep this instance in sync with disk

        string target = path ?? Path;
        string backup = target + ".bak";
        if (File.Exists(target) && !File.Exists(backup))
            File.Copy(target, backup);
        File.WriteAllBytes(target, outBytes);
    }

    // --- helpers -------------------------------------------------------------

    /// <summary>True when <paramref name="path"/>'s file name looks like a Wasteland save (GAME1/GAME2).</summary>
    public static bool LooksLikeSaveFileName(string path)
    {
        string name = System.IO.Path.GetFileName(path);
        return name.Equals("game1", StringComparison.OrdinalIgnoreCase)
            || name.Equals("game2", StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(Tag).Append(" @ ").Append(BlockOffset)
          .Append("  members=").Append(Header.CurrentMembers)
          .Append("  map=").Append(Header.CurrentMap)
          .Append("  pos=(").Append(Header.PartyX).Append(',').Append(Header.PartyY).Append(')');
        return sb.ToString();
    }
}
