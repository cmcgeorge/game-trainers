using GameTrainers.Common.Mvvm;

namespace HillsfarTrainer.Game;

/// <summary>
/// Reads and writes Hillsfar's character files.
///
/// <para><c>&lt;name&gt;.HIL</c> (saved characters) and <c>*.PRE</c> (the four shipped pre-rolled
/// ones) are both a <b>raw dump of the 188-byte record</b> — no header, no checksum, no encryption.
/// So this is deliberately a very thin class: load the bytes, hand them to a
/// <see cref="CharacterRecord"/>, write them back. Anything the trainer does not understand is
/// carried through untouched, which is the whole reason offline editing is safe here.</para>
///
/// <para>A one-shot <c>.bak</c> is taken beside the file before the first write, so a mistake is
/// always recoverable.</para>
/// </summary>
public sealed class CharacterFile : ObservableObject
{
    /// <summary>Extension of a saved character.</summary>
    public const string SavedExtension = ".HIL";

    /// <summary>Extension of a shipped pre-rolled character.</summary>
    public const string PreRolledExtension = ".PRE";

    private readonly byte[] _bytes;

    private CharacterFile(string path, byte[] bytes)
    {
        Path = path;
        _bytes = bytes;
        Record = new CharacterRecord(bytes);
    }

    /// <summary>Full path of the file this was loaded from.</summary>
    public string Path { get; }

    /// <summary>The file name without its directory.</summary>
    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>A typed view over the loaded bytes.</summary>
    public CharacterRecord Record { get; }

    /// <summary>
    /// True when <see cref="Record"/> has been edited since the file was loaded or last saved.
    ///
    /// <para>This lives on the file rather than on the editor view-model on purpose: edits are applied
    /// in place to this instance's buffer and survive a selection change, so a single flag on the
    /// view-model would clear when the user clicked another file and leave the edits in memory with
    /// Save disabled — visible, unsaveable, and discarded by the next folder reload.</para>
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (!SetField(ref _isDirty, value)) return;
            // DisplayName is what the editor's list binds to, so it has to be raised too — otherwise
            // the unsaved-edit marker never appears and never clears.
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    private bool _isDirty;

    /// <summary>Marks the record as edited. Called by the editor after every mutation.</summary>
    public void MarkDirty() => IsDirty = true;

    /// <summary>True when the loaded bytes pass the record shape check.</summary>
    public bool LooksValid => CharacterFormat.LooksLikeRecord(_bytes);

    /// <summary>The file name, with a marker when it has unsaved edits — used by the editor's list.</summary>
    public string DisplayName => IsDirty ? FileName + " *" : FileName;

    /// <summary>
    /// Loads a character file. Throws when the file is not exactly
    /// <see cref="CharacterFormat.RecordLength"/> bytes — a wrong length means it is not one of these
    /// files, and guessing would be worse than failing.
    /// </summary>
    public static CharacterFile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length != CharacterFormat.RecordLength)
            throw new InvalidDataException(
                $"'{System.IO.Path.GetFileName(path)}' is {bytes.Length} bytes; a Hillsfar character "
                + $"file is exactly {CharacterFormat.RecordLength}.");
        return new CharacterFile(path, bytes);
    }

    /// <summary>
    /// Every character file in a directory, saved characters first. Files that are the wrong length
    /// or fail the shape check are skipped rather than reported — a game directory may hold all
    /// sorts of things.
    /// </summary>
    public static IReadOnlyList<CharacterFile> LoadDirectory(string directory)
    {
        var result = new List<CharacterFile>();
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return result;

        foreach (var ext in new[] { SavedExtension, PreRolledExtension })
        {
            // Materialise the enumeration inside the try. Directory.EnumerateFiles is lazy, so a
            // directory-level failure surfaces from the first MoveNext — i.e. from the foreach
            // header — and a try inside the loop body would never see it.
            List<string> paths;
            try
            {
                paths = Directory.EnumerateFiles(directory, "*" + ext).ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException
                                          or ArgumentException)
            {
                continue;   // unreadable or malformed directory — nothing to offer for this extension
            }

            foreach (var path in paths)
            {
                try
                {
                    var f = Load(path);
                    if (f.LooksValid) result.Add(f);
                }
                catch (Exception e) when (e is IOException or InvalidDataException
                                              or UnauthorizedAccessException)
                {
                    // Not a character file, or not readable. Skip it.
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Writes the record back, taking a one-shot <c>.bak</c> first (see <see cref="BackupOnce"/>).
    /// </summary>
    public void Save()
    {
        // Guard as SaveAs does. Backing up unconditionally meant that if the file had been deleted or
        // renamed since it was loaded, BackupOnce threw and the edits could never be written — even
        // though the write itself would have succeeded and recreated the file.
        if (File.Exists(Path)) BackupOnce(Path);
        File.WriteAllBytes(Path, _bytes);
        IsDirty = false;
    }

    /// <summary>
    /// Writes the record to another path, backing that file up first if it already exists.
    ///
    /// <para>The backup is not optional here. The obvious use of this method is exporting a live
    /// character, and <see cref="SuggestFileName"/> deliberately reproduces the stem the <i>game</i>
    /// uses — so the target is very often the player's own save rather than a new file.</para>
    /// </summary>
    /// <returns>True when an existing file was overwritten (and therefore backed up).</returns>
    public bool SaveAs(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        bool existed = File.Exists(path);
        if (existed) BackupOnce(path);
        File.WriteAllBytes(path, _bytes);
        return existed;
    }

    /// <summary>
    /// Copies <paramref name="path"/> to <c><paramref name="path"/>.bak</c> unless a backup is
    /// already there, so the first edit is always recoverable and later ones cannot bury the
    /// original.
    ///
    /// <para>The copy goes to a temporary name and is only moved into place once it is complete.
    /// Treating mere <i>existence</i> of the <c>.bak</c> as proof of a good backup would be wrong: a
    /// copy that failed part-way (disk full, a dropped network path) commonly leaves a truncated
    /// destination behind, and the next attempt would then skip the backup and overwrite the
    /// original with only that truncated file to fall back on.</para>
    /// </summary>
    public static void BackupOnce(string path)
    {
        var backup = path + ".bak";
        if (File.Exists(backup)) return;

        var staging = backup + ".tmp";
        try
        {
            File.Copy(path, staging, overwrite: true);
            if (new FileInfo(staging).Length != new FileInfo(path).Length)
                throw new IOException($"backup of '{System.IO.Path.GetFileName(path)}' was truncated");
            // Another instance may have created the backup since the check above. That is a good
            // outcome, not a failure: keep theirs, drop ours, and let the save proceed.
            if (File.Exists(backup)) File.Delete(staging);
            else File.Move(staging, backup);
        }
        catch
        {
            try { if (File.Exists(staging)) File.Delete(staging); } catch { /* best effort */ }
            throw;
        }
    }

    /// <summary>
    /// Builds a character file in memory from an arbitrary 188-byte record — used to write a live
    /// character out to disk. The bytes are copied, so the caller's buffer is not aliased.
    /// </summary>
    public static CharacterFile FromRecord(string path, ReadOnlySpan<byte> record)
    {
        if (record.Length != CharacterFormat.RecordLength)
            throw new ArgumentException(
                $"a record is exactly {CharacterFormat.RecordLength} bytes", nameof(record));
        return new CharacterFile(path, record.ToArray());
    }

    /// <summary>
    /// A DOS 8.3 filename for a character name: up to eight characters, whitespace dropped,
    /// uppercased, anything outside <c>A-Z 0-9 _</c> discarded, plus <c>.HIL</c>.
    ///
    /// <para>This approximates the game's own choice rather than reproducing it exactly. The game
    /// builds its filename from the <i>raw</i> leading bytes of the name field and ignores the NUL
    /// terminator — which is why overwriting a long name with a short one in memory and then saving
    /// produced <c>ZZTOPOPH.HIL</c> — whereas this works from the decoded name, so the two diverge
    /// for a name with interior spaces. <see cref="CharacterRecord.Name"/> clears the whole field
    /// precisely so the game's version stays predictable.</para>
    ///
    /// <para>The character filter is a safety requirement, not tidiness:
    /// <see cref="CharacterRecord.Name"/> accepts any printable ASCII, so an unfiltered stem could
    /// contain a path separator — and a name beginning with one would make
    /// <see cref="System.IO.Path.Combine"/> discard the chosen folder entirely and write to the root
    /// of the drive. Reserved DOS device names are rejected for the same reason.</para>
    /// </summary>
    public static string SuggestFileName(string characterName)
    {
        var chars = (characterName ?? string.Empty)
            .Select(char.ToUpperInvariant)
            .Where(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
            .Take(8)
            .ToArray();
        var stem = new string(chars);

        // Reserved DOS device names cannot be used as filenames even with an extension.
        if (stem.Length == 0 || ReservedNames.Contains(stem)) stem = "CHARACTR";
        return stem + SavedExtension;
    }

    /// <summary>DOS device names that may never be used as a filename stem.</summary>
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };
}
