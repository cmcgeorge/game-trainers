using System.IO;

namespace Roadwar2000Trainer.Game;

/// <summary>
/// A <c>.RWS</c> saved game on disk.
/// <para>
/// The format is the simplest one in this repository: 6,512 bytes that are a verbatim image of
/// the game's data-segment slab, with no header, no checksum and no length field. Editing one is
/// therefore exactly editing live memory with the game closed, which is why this class hands out
/// the same <see cref="GameSlab"/> the live editor uses.
/// </para>
/// <para>
/// Despite prompting for a diskette in drive A:, the PC build writes the file into the current
/// working directory -- normally the game folder itself. That is where saves are looked for.
/// </para>
/// </summary>
public sealed class SaveGame
{
    private readonly BufferTarget _buffer;

    private SaveGame(string path, byte[] bytes)
    {
        Path = path;
        _buffer = new BufferTarget(bytes);
        Slab = new GameSlab(_buffer);
        Slab.Refresh();
        Gang = new GangRecord(Slab);
    }

    public string Path { get; }

    public GameSlab Slab { get; }

    public GangRecord Gang { get; }

    /// <summary>True once <see cref="Save"/> has something to write.</summary>
    public bool IsDirty { get; private set; }

    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>The save's name as the game asks for it -- the file name without its extension.</summary>
    public string SaveName => System.IO.Path.GetFileNameWithoutExtension(Path);

    /// <summary>
    /// Opens a save. Returns null with a reason when the file is the wrong length or does not
    /// carry the structures every Roadwar slab has.
    /// </summary>
    public static SaveGame? Load(string path, out string error)
    {
        error = "";
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception ex) { error = ex.Message; return null; }

        if (bytes.Length != SaveFormat.SlabLength)
        {
            error = $"'{System.IO.Path.GetFileName(path)}' is {bytes.Length} bytes; " +
                    $"a Roadwar 2000 save is exactly {SaveFormat.SlabLength}.";
            return null;
        }

        if (!GameSlab.LooksLikeSlab(bytes))
        {
            error = $"'{System.IO.Path.GetFileName(path)}' is the right size but does not contain " +
                    "Roadwar 2000's vehicle tables, so it is not a save from this game.";
            return null;
        }

        return new SaveGame(path, bytes);
    }

    /// <summary>Every <c>.RWS</c> in a folder, newest first.</summary>
    public static IReadOnlyList<string> FindSaves(string folder)
    {
        try
        {
            var files = Directory.GetFiles(folder, "*.RWS");
            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
            return files;
        }
        catch (Exception) { return Array.Empty<string>(); }
    }

    /// <summary>Marks the buffer as changed; the view-models call this after every edit.</summary>
    public void MarkDirty() => IsDirty = true;

    /// <summary>
    /// Writes the buffer back, taking a one-shot <c>.bak</c> of the original first. The backup is
    /// only ever written once, so repeatedly saving never overwrites the pristine copy.
    /// </summary>
    public bool Save(out string error)
    {
        error = "";
        try
        {
            string backup = Path + ".bak";
            if (!File.Exists(backup)) File.Copy(Path, backup);
            File.WriteAllBytes(Path, _buffer.Bytes);
            IsDirty = false;
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }

    /// <summary>Writes the buffer to a new path without touching the original.</summary>
    public bool SaveAs(string path, out string error)
    {
        error = "";
        try { File.WriteAllBytes(path, _buffer.Bytes); return true; }
        catch (Exception ex) { error = ex.Message; return false; }
    }

    /// <summary>
    /// Compares against a slab taken from the running game, ignoring the three bytes the save
    /// routine itself rewrites. Used by the verifier and by the "does this file match the game?"
    /// check in the save editor.
    /// </summary>
    public int DifferencesFrom(byte[] liveSlab)
    {
        if (liveSlab.Length != SaveFormat.SlabLength) return SaveFormat.SlabLength;
        var ignore = new HashSet<int>(SaveFormat.VolatileOffsets);
        int n = 0;
        for (int i = 0; i < SaveFormat.SlabLength; i++)
            if (!ignore.Contains(i) && _buffer.Bytes[i] != liveSlab[i]) n++;
        return n;
    }
}
