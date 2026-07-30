using System.IO;
namespace SwordOfAragonTrainer.Game;

/// <summary>
/// A loaded <c>ARAGON.HR&lt;letter&gt;</c> roster: 80 fixed 100-byte records held as one buffer and
/// edited in place through <see cref="RosterRecord"/> views. Because the whole file is round-tripped
/// verbatim, every byte the trainer does not deliberately change is preserved — including the
/// offsets whose meaning is still unproven.
/// </summary>
public sealed class RosterFile
{
    private readonly byte[] _data;

    /// <summary>Path the roster was read from; <see cref="Save"/> writes back here by default.</summary>
    public string SourcePath { get; }

    private RosterFile(byte[] data, string path)
    {
        _data = data;
        SourcePath = path;
        Records = Enumerable.Range(0, RosterFormat.SlotCount)
            .Select(slot => new RosterRecord(data, slot))
            .ToArray();
    }

    /// <summary>All 80 slots, in file order: 0–19 characters, 20–79 units.</summary>
    public IReadOnlyList<RosterRecord> Records { get; }

    /// <summary>The player's own character (slot 0).</summary>
    public RosterRecord Player => Records[RosterFormat.PlayerSlot];

    /// <summary>
    /// The player's class code, which drives the purchase discounts. Read from slot 0 — the game
    /// always keeps the player's own character there.
    /// </summary>
    public int PlayerClassCode => Player.TypeCode;

    /// <summary>Occupied character slots, in slot order.</summary>
    public IEnumerable<RosterRecord> Characters =>
        Records.Take(RosterFormat.CharacterSlots).Where(r => r.IsOccupied);

    /// <summary>Occupied unit slots, in slot order.</summary>
    public IEnumerable<RosterRecord> Units =>
        Records.Skip(RosterFormat.FirstUnitSlot).Where(r => r.IsOccupied);

    /// <summary>
    /// Reads a roster file. Throws <see cref="InvalidDataException"/> if the file is not exactly
    /// <see cref="RosterFormat.FileSize"/> bytes or if slot 0 does not hold a recognisable character —
    /// the read-validate-write guard that stops the trainer editing something that is not a roster.
    /// </summary>
    public static RosterFile Load(string path) => Create(File.ReadAllBytes(path), path);

    /// <summary>
    /// Wraps a copy of <paramref name="bytes"/> (used by the verification harness). The caller's array
    /// is not modified; inspect the result through <see cref="ToArray"/>. Validated exactly as
    /// <see cref="Load"/> is.
    /// </summary>
    public static RosterFile FromBytes(byte[] bytes, string path = "") =>
        Create((byte[])bytes.Clone(), path);

    // Takes ownership of `owned`; both entry points funnel through here so a file and an in-memory
    // buffer are validated identically.
    private static RosterFile Create(byte[] owned, string path)
    {
        string name = string.IsNullOrEmpty(path) ? "roster" : Path.GetFileName(path);
        if (owned.Length != RosterFormat.FileSize)
            throw new InvalidDataException(
                $"'{name}' is {owned.Length} bytes; a Sword of Aragon roster is " +
                $"exactly {RosterFormat.FileSize}.");

        var roster = new RosterFile(owned, path);
        int playerType = roster.Player.TypeCode;
        if (playerType < UnitBook.FirstCharacterCode || UnitBook.Type(playerType) == null)
            throw new InvalidDataException(
                $"'{name}' does not look like a roster: slot 0 should hold the player's character " +
                $"(a type code of {UnitBook.FirstCharacterCode}–{UnitBook.Types.Count}) " +
                $"but holds {playerType}.");

        return roster;
    }

    /// <summary>Recomputes the derived cost/size fields of every occupied slot.</summary>
    public void RecomputeAllDerived()
    {
        int playerClass = PlayerClassCode;
        foreach (var record in Records)
            if (record.IsOccupied) record.RecomputeDerived(playerClass);
    }

    /// <summary>
    /// Writes the buffer back. A one-off <c>.bak</c> copy is taken first if none exists yet; the path
    /// of a backup actually created is returned, or null when one was already present.
    /// </summary>
    public string? Save(string? path = null)
    {
        string target = path ?? SourcePath;
        string? backup = SaveBackup.EnsureFor(target);
        File.WriteAllBytes(target, _data);
        return backup;
    }

    /// <summary>A copy of the raw bytes, for tests and diffing.</summary>
    public byte[] ToArray() => (byte[])_data.Clone();
}
