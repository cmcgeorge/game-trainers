using System.IO;

namespace Space1889Trainer.Game;

/// <summary>
/// A saved game on disk. <c>GAME → SAVE</c> writes the whole 20,198-byte state block to <c>NAME.SAV</c> in the
/// game folder; the shipped <c>DEF.S</c> (the default party's new game) is the same format. The editor edits a
/// copy in memory and writes it back, backing the original up once as <c>NAME.SAV.bak</c> before the first write.
/// </summary>
public sealed class SaveGame
{
    private (DateTime WrittenUtc, long Length) _diskStamp;

    private SaveGame(string path, byte[] bytes)
    {
        Path = path;
        Target = new BufferTarget(bytes);
        State = new GameState(Target);
        State.Refresh();
        _diskStamp = DiskStamp(path);
    }

    public string Path { get; }

    public string FileName => System.IO.Path.GetFileName(Path);

    public BufferTarget Target { get; }

    public GameState State { get; }

    /// <summary>Saves in <paramref name="folder"/>: every <c>*.SAV</c>, plus <c>DEF.S</c> last.</summary>
    public static IReadOnlyList<string> FindSaves(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<string>();
        var list = Directory.EnumerateFiles(folder)
            .Where(f => f.EndsWith(".SAV", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var def = Directory.EnumerateFiles(folder).FirstOrDefault(f =>
            System.IO.Path.GetFileName(f).Equals("DEF.S", StringComparison.OrdinalIgnoreCase));
        if (def is not null) list.Add(def);
        return list;
    }

    /// <summary>Loads a save, or returns null with the reason.</summary>
    public static SaveGame? Load(string path, out string error)
    {
        error = "";
        if (System.IO.Path.GetFileName(path).Equals("START.OBJ", StringComparison.OrdinalIgnoreCase))
        {
            error = "START.OBJ only holds the starting ground objects, not a whole game; open a NAME.SAV (or DEF.S).";
            return null;
        }
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = $"Could not read {path}: {ex.Message}";
            return null;
        }

        if (bytes.Length != StateFormat.BlockLength)
        {
            error = $"{System.IO.Path.GetFileName(path)} is {bytes.Length:N0} bytes; a Space 1889 save is exactly {StateFormat.BlockLength:N0}.";
            return null;
        }
        if (!StateFormat.LooksLikeState(bytes))
        {
            error = $"{System.IO.Path.GetFileName(path)} is the right size but does not look like a Space 1889 save (no PARTY ACCT. record).";
            return null;
        }
        return new SaveGame(path, bytes);
    }

    /// <summary>The backup path written before the first save.</summary>
    public string BackupPath => Path + ".bak";

    /// <summary>
    /// True when the file on disk is no longer the copy that was opened (or last saved) — typically because the game
    /// saved over it in the meantime. Saving then would replace newer progress with this older copy plus the edits.
    /// </summary>
    public bool ChangedOnDisk => DiskStamp(Path) != _diskStamp;

    private static (DateTime, long) DiskStamp(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? (info.LastWriteTimeUtc, info.Length) : (DateTime.MinValue, -1);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (DateTime.MinValue, -1);
        }
    }

    /// <summary>
    /// Writes the edited block back. The first time a given file is written the original is copied to
    /// <see cref="BackupPath"/> (an existing backup is never overwritten, so it stays the pre-trainer original).
    /// </summary>
    public bool Save(out string error)
    {
        error = "";
        string temp = Path + ".tmp";
        try
        {
            if (File.Exists(Path) && !File.Exists(BackupPath)) File.Copy(Path, BackupPath);
            File.WriteAllBytes(temp, Target.Bytes);
            File.Move(temp, Path, overwrite: true);
            _diskStamp = DiskStamp(Path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch (Exception cleanupEx) when (cleanupEx is IOException or UnauthorizedAccessException) { }
            error = $"Could not write {Path}: {ex.Message}";
            return false;
        }
    }
}
