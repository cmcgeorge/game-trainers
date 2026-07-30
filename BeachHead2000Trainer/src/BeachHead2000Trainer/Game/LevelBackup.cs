using System.Globalization;
using System.IO;

namespace BeachHead2000Trainer.Game;

/// <summary>
/// One-shot backups for the level files the editor rewrites. The first time the trainer writes a
/// <c>Level_nn</c> file it copies the original to <c>Level_nn.bak</c>; later writes leave that copy
/// alone, so a <c>.bak</c> is always the state that existed before the trainer <b>first</b> touched
/// that file — the pristine shipped level, not a rolling undo. <see cref="EnsureFor"/> reports
/// whether it actually created a backup so callers can say how many are new rather than promising
/// recoverability unconditionally.
/// </summary>
public static class LevelBackup
{
    /// <summary>Extension appended for the backup copy.</summary>
    public const string BackupExtension = ".bak";

    /// <summary>The backup path the trainer would use for a level file.</summary>
    public static string PathFor(string levelPath) => levelPath + BackupExtension;

    /// <summary>The shipped file name for a level index (<c>Level_00</c> … <c>Level_60</c>).</summary>
    public static string FileNameFor(int index) =>
        string.Format(CultureInfo.InvariantCulture, GameFacts.LevelFilePattern, index);

    /// <summary>
    /// The <c>Level_00</c>…<c>Level_60</c> files that actually exist in <paramref name="directory"/>,
    /// in level order. Enumerating by the shipped name pattern (rather than globbing) keeps the bulk
    /// operations off unrelated files — including the <c>.bak</c> copies themselves.
    /// </summary>
    public static IReadOnlyList<string> EnumerateLevelFiles(string? directory)
    {
        var found = new List<string>();
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return found;

        for (int i = GameFacts.FirstLevel; i <= GameFacts.LastLevel; i++)
        {
            string path = Path.Combine(directory, FileNameFor(i));
            if (File.Exists(path)) found.Add(path);
        }
        return found;
    }

    /// <summary>
    /// The level files in <paramref name="directory"/> that have a backup beside them, in level
    /// order. Returns the <i>level</i> paths (not the <c>.bak</c> paths) so a backup can be restored
    /// even when the level file itself has since been deleted.
    /// </summary>
    public static IReadOnlyList<string> EnumerateBackups(string? directory)
    {
        var found = new List<string>();
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return found;

        for (int i = GameFacts.FirstLevel; i <= GameFacts.LastLevel; i++)
        {
            string path = Path.Combine(directory, FileNameFor(i));
            if (File.Exists(PathFor(path))) found.Add(path);
        }
        return found;
    }

    /// <summary>
    /// Copies <paramref name="levelPath"/> aside if it exists and no backup has been taken yet.
    /// Returns the backup path when one was created, otherwise null. Never throws for a missing
    /// source — a file that isn't there has nothing to back up.
    /// </summary>
    public static string? EnsureFor(string levelPath)
    {
        if (string.IsNullOrWhiteSpace(levelPath) || !File.Exists(levelPath)) return null;

        string backup = PathFor(levelPath);
        if (File.Exists(backup)) return null;

        File.Copy(levelPath, backup);
        return backup;
    }

    /// <summary>
    /// Copies the backup back over <paramref name="levelPath"/>, overwriting the trainer's edits.
    /// The <c>.bak</c> is kept so a restore can be repeated. Returns false when no backup exists.
    /// </summary>
    public static bool RestoreFor(string levelPath)
    {
        string backup = PathFor(levelPath);
        if (!File.Exists(backup)) return false;

        File.Copy(backup, levelPath, overwrite: true);
        return true;
    }
}
