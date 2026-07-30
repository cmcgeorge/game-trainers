using System.IO;
namespace SwordOfAragonTrainer.Game;

/// <summary>
/// One-shot backups for the files the trainer edits. The first time the trainer writes a save file it
/// copies the original to <c>&lt;name&gt;.bak</c>; later writes leave that copy alone, so a `.bak` is
/// always the state that existed before the trainer <b>first</b> touched that file — not a rolling
/// undo. If the game has saved over the letter since, the `.bak` is older than the current campaign,
/// which is why <see cref="EnsureFor"/> reports whether it actually created one and callers surface
/// that instead of promising recoverability unconditionally.
/// </summary>
public static class SaveBackup
{
    /// <summary>Extension appended for the backup copy.</summary>
    public const string BackupExtension = ".bak";

    /// <summary>The backup path the trainer would use for a save file.</summary>
    public static string PathFor(string savePath) => savePath + BackupExtension;

    /// <summary>
    /// Copies <paramref name="savePath"/> aside if it exists and no backup has been taken yet.
    /// Returns the backup path when one was created, otherwise null. Never throws for a missing
    /// source — a brand-new file simply has nothing to back up.
    /// </summary>
    public static string? EnsureFor(string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath) || !File.Exists(savePath)) return null;

        string backup = PathFor(savePath);
        if (File.Exists(backup)) return null;

        File.Copy(savePath, backup);
        return backup;
    }
}
