using System.IO;

namespace DarkDesigns1Trainer.Game;

/// <summary>
/// Reads and writes the <c>DDCHARS.DAT</c> character file (1,224 bytes = 144-byte header +
/// 15 × 72-byte records). The header's first 16 bytes are decoded — the four party roster slots
/// (read-only) and the party position (editable through <see cref="Position"/>); its remaining
/// 128 bytes, and every byte of a record the trainer does not expose, are round-tripped
/// unchanged. A one-shot <c>.bak</c> backup is taken before the first write.
///
/// Edit with the game closed: Dark Designs rewrites this file from its in-memory party on
/// <c>(Q)uit and save</c>, so offline edits made while it is running are discarded.
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

    // --- header: the saved party position ------------------------------------
    /// <summary>
    /// The party's saved position — the same level / X / Y / facing the game keeps live while you
    /// play, written into the header on <c>(Q)uit and save</c> and read back out of it on load.
    /// Editing it here therefore teleports the party on the next run, level included: the game
    /// loads whichever <c>DDMAP</c> the header names, so the map always matches the level.
    /// </summary>
    public PartyPosition Position
    {
        get => PartyPosition.FromBytes(Header, CharacterFormat.HdrOffPosition);
        set
        {
            value.WriteTo(Header, CharacterFormat.HdrOffPosition);
            _modified = true;
        }
    }

    /// <summary>
    /// Which roster slot party position <paramref name="position"/> (1–4) holds, or 0 for an empty
    /// position. Read-only: the party is assembled in the game's own town menu, and rewriting these
    /// without also rebuilding the working copies would desynchronise the two.
    /// </summary>
    public int PartySlot(int position)
    {
        if (position < 1 || position > CharacterFormat.PartySize) return 0;
        int at = CharacterFormat.HdrOffPartySlots + (position - 1) * 2;
        return Header[at] | (Header[at + 1] << 8);
    }

    /// <summary>Writes all characters back to the file, taking a .bak on the first write.</summary>
    public void Save()
    {
        if (!_modified) return;
        BackupIfNeeded();

        // The header is held as a copy, so put it back before writing — untouched bytes round-trip
        // byte-for-byte, and an edited position reaches the file.
        Array.Copy(Header, 0, _data, 0, CharacterFormat.HeaderSize);

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
