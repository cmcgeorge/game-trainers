using System.IO;

namespace RedBaronTrainer.Game;

/// <summary>
/// Reads and writes the two Red Baron files this trainer edits offline: the realism panels
/// (<c>MREAL.PRF</c> for single missions, <c>CREAL.PRF</c> for careers) and <c>ROSTER.DAT</c>.
///
/// <para>Editing the files matters as much as editing memory: RB.EXE re-reads the realism panel from
/// disk each time the shell chains into it, so a change written here survives into the next sortie
/// even though the sim is a different process from the one the trainer was attached to.</para>
/// </summary>
public sealed class GameFolder
{
    public string Path { get; }

    public GameFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
    }

    /// <summary>True when <paramref name="path"/> holds a Red Baron installation.</summary>
    public static bool IsGameFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return false;
        foreach (var marker in GameFacts.GameFolderMarkers)
            if (!File.Exists(System.IO.Path.Combine(path, marker))) return false;
        return true;
    }

    /// <summary>Full path of a file inside the game folder.</summary>
    public string PathOf(string name) => System.IO.Path.Combine(Path, name);

    private static string RealismFileName(bool career) =>
        career ? GameFacts.CareerRealismFileName : GameFacts.MissionRealismFileName;

    // ---------------------------------------------------------------- realism panels

    /// <summary>Reads a 13-value realism panel, or null when the file is missing or malformed.</summary>
    public ushort[]? ReadRealism(bool career)
    {
        try
        {
            return RealismSettings.Decode(File.ReadAllBytes(PathOf(RealismFileName(career))));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes a realism panel, preserving anything past the 26 bytes we understand. Both shipped
    /// files are exactly 26 bytes, but rewriting only the prefix costs nothing and cannot truncate a
    /// variant that is longer.
    ///
    /// <para>The replacement goes through a temporary file and a move, so the game's own file is
    /// never in a truncated state: <c>File.WriteAllBytes</c> opens with <c>FileMode.Create</c>, and a
    /// failure between the truncate and the flush would leave the player with a preference file the
    /// game cannot read.</para>
    /// </summary>
    public void WriteRealism(bool career, IReadOnlyList<ushort> values)
    {
        string path = PathOf(RealismFileName(career));
        var block = RealismSettings.Encode(values);

        byte[] existing;
        try { existing = File.ReadAllBytes(path); }
        catch (FileNotFoundException) { existing = Array.Empty<byte>(); }
        catch (DirectoryNotFoundException) { existing = Array.Empty<byte>(); }

        if (existing.Length > block.Length)
        {
            var merged = (byte[])existing.Clone();
            block.CopyTo(merged, 0);
            block = merged;
        }

        string temp = path + ".tmp";
        try
        {
            File.WriteAllBytes(temp, block);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // A read-only or emulator-locked target fails at the Move, and leaving MREAL.PRF.tmp
            // lying in the player's game folder is not an acceptable souvenir of that.
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
            throw;
        }
    }

    // ------------------------------------------------------------------------- roster

    /// <summary>
    /// Reads <c>ROSTER.DAT</c> as its 8-byte header plus ten 90-byte records. Returns null when the
    /// file is absent or not the expected 908 bytes.
    /// </summary>
    public (byte[] Header, PilotRecord[] Pilots)? ReadRoster()
    {
        try
        {
            var bytes = File.ReadAllBytes(PathOf(GameFacts.RosterFileName));
            int expected = GameFacts.RosterFileHeaderSize + GameFacts.RosterSlots * GameFacts.PilotRecordSize;
            if (bytes.Length != expected) return null;

            var header = bytes[..GameFacts.RosterFileHeaderSize];
            var pilots = new PilotRecord[GameFacts.RosterSlots];
            for (int i = 0; i < pilots.Length; i++)
            {
                int off = GameFacts.RosterFileHeaderSize + i * GameFacts.PilotRecordSize;
                pilots[i] = new PilotRecord(bytes.AsSpan(off, GameFacts.PilotRecordSize));
            }
            return (header, pilots);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // ROSTER.DAT is deliberately read-only here. The trainer edits pilots in the shell's live memory,
    // where the game owns the write-back, rather than rewriting a 908-byte save behind its back; a
    // write path with no caller would be an untested way to destroy a player's careers.

    /// <summary>
    /// Copies a file to <c>&lt;name&gt;.bak</c> beside it if no backup exists yet. An existing backup
    /// is never overwritten, so the <c>.bak</c> always holds the file as it was before this trainer
    /// first touched it — not as it was at the start of the most recent session. That is the more
    /// useful guarantee, and it is why a second session's edits do not take a fresh copy.
    /// </summary>
    public void BackUpOnce(string name)
    {
        string source = PathOf(name);
        string backup = source + ".bak";
        if (!File.Exists(source) || File.Exists(backup)) return;
        File.Copy(source, backup);
    }
}
