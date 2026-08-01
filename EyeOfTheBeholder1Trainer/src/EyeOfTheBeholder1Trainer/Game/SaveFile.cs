using System.IO;

namespace EyeOfTheBeholder1Trainer.Game;

/// <summary>
/// Reader/writer for the Eye of the Beholder I save file (<c>EOBDATA.SAV</c>).
///
/// The file has no header: it begins with <see cref="CharacterFormat.MaxSlots"/> contiguous
/// character records (<see cref="CharacterFormat.RecordSize"/> bytes each), followed by game-state
/// data (explored map, monster positions, completed events). The trainer reads and edits the
/// character portion; the remainder is preserved byte-for-byte.
/// </summary>
public sealed class SaveFile
{
    private byte[] _data;

    /// <summary>The full file bytes (character records + game state).</summary>
    public byte[] Data => _data;

    /// <summary>Number of character records in the file (always <see cref="CharacterFormat.MaxSlots"/>).</summary>
    public int CharacterCount => CharacterFormat.MaxSlots;

    public SaveFile(byte[] data)
    {
        _data = data;
    }

    /// <summary>Loads a save file from disk.</summary>
    public static SaveFile Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return new SaveFile(bytes);
    }

    /// <summary>Saves the file back to disk (overwrites).</summary>
    public void Save(string path) => File.WriteAllBytes(path, _data);

    /// <summary>Returns a typed, mutable view over character slot <paramref name="index"/> (0..5).</summary>
    public CharacterRecord GetCharacter(int index)
    {
        if (index < 0 || index >= CharacterFormat.MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(index));
        return new CharacterRecord(_data, index * CharacterFormat.RecordSize);
    }

    /// <summary>Writes a character record back into the file buffer at slot <paramref name="index"/>.</summary>
    public void SetCharacter(int index, CharacterRecord record)
    {
        if (index < 0 || index >= CharacterFormat.MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(index));
        Array.Copy(record.Bytes, 0, _data, index * CharacterFormat.RecordSize, CharacterFormat.RecordSize);
    }

    /// <summary>Enumerates all occupied character slots in the file.</summary>
    public IEnumerable<(int Index, CharacterRecord Record)> GetOccupiedCharacters()
    {
        for (int i = 0; i < CharacterFormat.MaxSlots; i++)
        {
            var rec = GetCharacter(i);
            if (rec.IsOccupied)
                yield return (i, rec);
        }
    }

    /// <summary>True when the file is at least large enough to hold the full party.</summary>
    public bool IsValid => _data.Length >= CharacterFormat.PartySize;

    /// <summary>Creates a one-shot backup at <paramref name="path"/> + <c>.bak</c> if none exists.</summary>
    public void Backup(string path)
    {
        string bak = path + ".bak";
        if (!File.Exists(bak))
            File.Copy(path, bak);
    }
}
