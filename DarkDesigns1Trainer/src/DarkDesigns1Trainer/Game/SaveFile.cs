using System.IO;

namespace DarkDesigns1Trainer.Game;

/// <summary>
/// Reads and writes the <c>DDCHARS.DAT</c> character file (1,224 bytes = 144-byte header +
/// 20 × 54-byte records). The header is round-tripped without interpretation; only the
/// character records are exposed for editing. A one-shot <c>.bak</c> backup is taken before
/// the first write.
/// </summary>
public sealed class SaveFile : IDisposable
{
    private readonly string _path;
    private byte[] _data;
    private bool _modified;
    private bool _backedUp;

    public byte[] Header { get; }
    public List<CharacterRecord> Characters { get; } = new();

    public SaveFile(string path)
    {
        _path = path;
        _data = File.ReadAllBytes(path);
        if (_data.Length != CharacterFormat.FileSize)
            throw new FormatException(
                $"{path} is {_data.Length} bytes; expected {CharacterFormat.FileSize} ({CharacterFormat.HeaderSize} header + {CharacterFormat.MaxSlots} × {CharacterFormat.RecordSize} records).");

        Header = new byte[CharacterFormat.HeaderSize];
        Array.Copy(_data, 0, Header, 0, CharacterFormat.HeaderSize);

        for (int i = 0; i < CharacterFormat.MaxSlots; i++)
        {
            var rec = new CharacterRecord(_data, CharacterFormat.HeaderSize + i * CharacterFormat.RecordSize);
            Characters.Add(rec);
        }
    }

    public IEnumerable<CharacterRecord> OccupiedCharacters =>
        Characters.Where(c => c.IsOccupied);

    /// <summary>Writes all characters back to the file, taking a .bak on the first write.</summary>
    public void Save()
    {
        if (!_modified) return;
        BackupIfNeeded();

        for (int i = 0; i < CharacterFormat.MaxSlots; i++)
        {
            Array.Copy(Characters[i].Bytes, 0, _data,
                CharacterFormat.HeaderSize + i * CharacterFormat.RecordSize,
                CharacterFormat.RecordSize);
        }
        File.WriteAllBytes(_path, _data);
        _modified = false;
    }

    /// <summary>Marks a character as modified so the next Save() writes it.</summary>
    public void MarkModified() => _modified = true;

    private void BackupIfNeeded()
    {
        if (_backedUp) return;
        string bak = _path + ".bak";
        if (!File.Exists(bak))
            File.Copy(_path, bak, overwrite: false);
        _backedUp = true;
    }

    public void Dispose()
    {
    }
}
