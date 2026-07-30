using System.IO;

namespace BeachHead2000Trainer.Game;

/// <summary>
/// Outcome of a bulk level-file operation. <paramref name="Total"/> is how many files the operation
/// was attempted on, <paramref name="Succeeded"/> how many were actually written,
/// <paramref name="BackedUp"/> how many <c>.bak</c> copies were newly created (0 for a restore, and
/// 0 on a repeat run because backups are one-shot), and <paramref name="Errors"/> a per-file message
/// for anything that failed — the batch keeps going so one locked or read-only file doesn't abort
/// the rest.
/// </summary>
public sealed record LevelBatchResult(
    int Total, int Succeeded, int BackedUp, IReadOnlyList<string> Errors)
{
    /// <summary>Nothing to do — no matching files were found.</summary>
    public static readonly LevelBatchResult Empty = new(0, 0, 0, Array.Empty<string>());

    /// <summary>True when at least one file could not be written.</summary>
    public bool HasErrors => Errors.Count > 0;
}

/// <summary>
/// Bulk operations across the whole shipped level set (<c>Level_00</c>…<c>Level_60</c>) in one
/// directory: give every level max ammo in a single click, and put the originals back afterwards.
/// Each write is preceded by a one-shot <see cref="LevelBackup"/> copy, so
/// <see cref="RestoreAll"/> always has the pristine shipped level to return to. Editing goes
/// through <see cref="LevelFile"/>, which rewrites only the <c>Ammo</c> line and leaves comments,
/// <c>Object</c>/<c>ObjectInc</c> blocks, and unknown lines untouched.
/// </summary>
public static class LevelBatch
{
    /// <summary>
    /// Backs up and then sets every level file in <paramref name="directory"/> to the game's max
    /// ammo (999 bullets / 99 projectiles / 99 missiles).
    /// </summary>
    public static LevelBatchResult MaxAmmoAll(string? directory) =>
        SetAmmoAll(directory, GameFacts.MaxBullets, GameFacts.MaxProjectiles, GameFacts.MaxMissiles);

    /// <summary>
    /// Backs up and then rewrites the <c>Ammo</c> line of every level file in
    /// <paramref name="directory"/>. A file whose backup could not be taken is left untouched —
    /// the copy happens first, so a failure there skips the write rather than losing the original.
    /// </summary>
    public static LevelBatchResult SetAmmoAll(string? directory, int bullets, int projectiles, int missiles)
    {
        var files = LevelBackup.EnumerateLevelFiles(directory);
        if (files.Count == 0) return LevelBatchResult.Empty;

        var errors = new List<string>();
        int written = 0, backedUp = 0;

        foreach (string path in files)
        {
            try
            {
                if (LevelBackup.EnsureFor(path) != null) backedUp++;

                var level = LevelFile.Load(path);
                level.Bullets = bullets;
                level.Projectiles = projectiles;
                level.Missiles = missiles;
                level.Save();
                written++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        return new LevelBatchResult(files.Count, written, backedUp, errors);
    }

    /// <summary>
    /// Copies every <c>.bak</c> in <paramref name="directory"/> back over its level file. The
    /// backups are kept, so a restore can be repeated (and a later edit still has an original to
    /// fall back to).
    /// </summary>
    public static LevelBatchResult RestoreAll(string? directory)
    {
        var files = LevelBackup.EnumerateBackups(directory);
        if (files.Count == 0) return LevelBatchResult.Empty;

        var errors = new List<string>();
        int restored = 0;

        foreach (string path in files)
        {
            try
            {
                if (LevelBackup.RestoreFor(path)) restored++;
            }
            catch (Exception ex)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        return new LevelBatchResult(files.Count, restored, 0, errors);
    }
}
