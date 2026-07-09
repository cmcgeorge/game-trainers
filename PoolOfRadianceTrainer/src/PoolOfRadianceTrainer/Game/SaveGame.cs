using System.IO;

namespace PoolOfRadianceTrainer.Game;

/// <summary>
/// One effect stored in a character's CHRDATAn.SPC file. On disk each effect is a 9-byte record:
/// a 5-byte payload (<c>[type][b1][b2][duration][b4]</c>, duration 0xFF = permanent) followed by a
/// 4-byte link pointer. The link is a stale runtime pointer rebuilt when the game loads the save,
/// so only the payload is meaningful and only the last record's link must be null.
/// </summary>
public sealed class EffectEntry
{
    public byte[] Payload { get; }   // exactly 5 bytes

    public EffectEntry(byte[] payload5)
    {
        Payload = new byte[5];
        Array.Copy(payload5, Payload, Math.Min(5, payload5.Length));
    }

    public static EffectEntry Permanent(byte type) => new(new byte[] { type, 0, 0, 0xFF, 0 });

    public byte Type => Payload[0];
    public byte Duration => Payload[3];
    public bool IsPermanent => Duration == 0xFF;

    public string Name => EffectBook.Name(Type);
    public string Hex => $"0x{Type:X2}";
    public string DurationText => IsPermanent ? "permanent" : $"duration {Duration}";
}

/// <summary>One character in a save: its record (CHRDATAn.SAV), effect list (CHRDATAn.SPC),
/// and carried items (CHRDATAn.ITM).</summary>
public sealed class SaveCharacter
{
    public int Index { get; init; }
    public required string SavPath { get; init; }
    public required string SpcPath { get; init; }
    public required string ItmPath { get; init; }
    /// <summary>The full raw .SAV file bytes (>= 285). Kept verbatim so the head-pointer write-back
    /// never truncates trailing bytes; <see cref="Record"/> is a parsed view of the first 285.</summary>
    public required byte[] SavBytes { get; init; }
    public required CharacterRecord Record { get; init; }
    public List<EffectEntry> Effects { get; } = new();
    /// <summary>Carried items, parsed from the CHRDATAn.ITM file (63-byte records).</summary>
    public List<ItemEntry> Items { get; } = new();

    public string Name => Record.Name;
}

/// <summary>
/// A loaded Gold Box save folder (e.g. C:\POOLRAD). Reads the party's CHRDATAn.SAV / .SPC files,
/// and writes effect changes back to the .SPC files (and the .SAV effects-head pointer at 0x7F).
/// Purely offline file editing — the game must be closed / reloaded for changes to take effect.
/// </summary>
public sealed class SaveGame
{
    // Placeholder non-null link written for non-final records. Its value is irrelevant (the game
    // rebuilds the pointers on load); only "non-zero vs zero" matters as a list terminator.
    private static readonly byte[] NonNullLink = { 0x08, 0x00, 0x01, 0x00 };
    private const int MaxParty = 8;

    public string Folder { get; }
    public IReadOnlyList<SaveCharacter> Characters { get; }

    private SaveGame(string folder, IReadOnlyList<SaveCharacter> characters)
    {
        Folder = folder;
        Characters = characters;
    }

    /// <summary>True if the folder contains at least one CHRDATAn.SAV record.</summary>
    public static bool LooksLikeSaveFolder(string folder) =>
        !string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder) &&
        Enumerable.Range(1, MaxParty).Any(i => File.Exists(Path.Combine(folder, $"CHRDATA{i}.SAV")));

    public static SaveGame Load(string folder)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);
        var chars = new List<SaveCharacter>();
        for (int i = 1; i <= MaxParty; i++)
        {
            string sav = Path.Combine(folder, $"CHRDATA{i}.SAV");
            if (!File.Exists(sav)) continue;
            byte[] bytes = File.ReadAllBytes(sav);
            if (bytes.Length < PorFormat.RecordSize) continue;

            var sc = new SaveCharacter
            {
                Index = i,
                SavPath = sav,
                SpcPath = Path.Combine(folder, $"CHRDATA{i}.SPC"),
                ItmPath = Path.Combine(folder, $"CHRDATA{i}.ITM"),
                SavBytes = bytes,
                Record = new CharacterRecord(bytes),
            };
            if (File.Exists(sc.SpcPath))
            {
                byte[] spc = File.ReadAllBytes(sc.SpcPath);
                for (int o = 0; o + 9 <= spc.Length; o += 9)
                    sc.Effects.Add(new EffectEntry(spc[o..(o + 5)]));
            }
            if (File.Exists(sc.ItmPath))
            {
                byte[] itm = File.ReadAllBytes(sc.ItmPath);
                for (int o = 0; o + ItemEntry.RecordSize <= itm.Length; o += ItemEntry.RecordSize)
                    sc.Items.Add(new ItemEntry(itm, o));
            }
            chars.Add(sc);
        }
        if (chars.Count == 0) throw new FileNotFoundException("No CHRDATAn.SAV character files found in the folder.");
        return new SaveGame(folder, chars);
    }

    /// <summary>Copies the save-relevant files to a timestamped sub-folder; returns its path.</summary>
    public string Backup()
    {
        string dir = Path.Combine(Folder, $"_trainer-backup-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(dir);
        foreach (string pattern in new[] { "CHRDATA*.*", "SAVGAM*.DAT" })
            foreach (string f in Directory.GetFiles(Folder, pattern))
                File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), overwrite: true);
        return dir;
    }

    /// <summary>Adds an effect type to a character if not already present. Returns true if added.</summary>
    public static bool AddEffect(SaveCharacter c, byte type)
    {
        if (c.Effects.Any(e => e.Type == type)) return false;
        c.Effects.Add(EffectEntry.Permanent(type));
        return true;
    }

    /// <summary>Persists a character's effects to its .SPC file and updates the .SAV head pointer (0x7F).</summary>
    public static void Write(SaveCharacter c)
    {
        int n = c.Effects.Count;
        if (n == 0)
        {
            // Retract the .SAV head pointer BEFORE deleting the .SPC data file: an interruption
            // between the two can then only leave a harmless orphaned .SPC, never a head pointing
            // at a file that no longer exists.
            SetHead(c, present: false);
            if (File.Exists(c.SpcPath)) File.Delete(c.SpcPath);
            return;
        }

        var buf = new byte[n * 9];
        for (int i = 0; i < n; i++)
        {
            int b = i * 9;
            Array.Copy(c.Effects[i].Payload, 0, buf, b, 5);
            byte[] link = (i == n - 1) ? new byte[4] : NonNullLink;   // last record terminates with a null link
            Array.Copy(link, 0, buf, b + 5, 4);
        }
        // Write the effect data first, then point the head at it.
        WriteAtomic(c.SpcPath, buf);
        SetHead(c, present: true);
    }

    // --- items ---------------------------------------------------------------

    /// <summary>Fully identifies every carried item on a character (reveals all name parts by
    /// clearing each item's hidden-names flag). Persists to the .ITM file. Returns the number of
    /// items newly identified.</summary>
    public static int IdentifyAll(SaveCharacter c)
    {
        int changed = 0;
        foreach (var it in c.Items) if (it.Identify()) changed++;
        if (changed > 0) WriteItems(c);
        return changed;
    }

    /// <summary>Persists a character's carried items to its .ITM file (a flat array of 63-byte
    /// records). Does not touch the .SAV item count — the number of records is unchanged.</summary>
    public static void WriteItems(SaveCharacter c)
    {
        int n = c.Items.Count;
        if (n == 0)
        {
            if (File.Exists(c.ItmPath)) File.Delete(c.ItmPath);
            return;
        }
        var buf = new byte[n * ItemEntry.RecordSize];
        for (int i = 0; i < n; i++)
            Array.Copy(c.Items[i].Raw, 0, buf, i * ItemEntry.RecordSize, ItemEntry.RecordSize);
        WriteAtomic(c.ItmPath, buf);
    }

    /// <summary>Copies <paramref name="src"/>'s entire carried inventory onto <paramref name="dst"/>,
    /// overwriting dst's items. The 63-byte item records carry the persisted state (including the
    /// equipped/identified flags); the .SAV item-count byte is mirrored too, and the runtime item
    /// pointers are rebuilt by the game on load. Returns the number of items copied.</summary>
    public static int DuplicateInventory(SaveCharacter src, SaveCharacter dst)
    {
        if (ReferenceEquals(src, dst)) return 0;
        dst.Items.Clear();
        foreach (var it in src.Items) dst.Items.Add(it.Clone());
        WriteItems(dst);
        // Copy the source's item-count byte verbatim rather than deriving it from dst.Items.Count.
        // The two can legitimately differ in a real save — the bundled sample party's Darkstar stores
        // count 4 with only 3 .ITM records and loads fine — so mirroring the source keeps dst's item
        // subsystem byte-identical to a known-good character instead of forcing an unverified pairing.
        dst.SavBytes[PorFormat.OffNumberOfItems] = src.SavBytes[PorFormat.OffNumberOfItems];
        // Point the item-list head at "present" when there are items (a placeholder the game
        // rebuilds on load, like the effects head) so a previously item-less character reliably
        // picks up the copied inventory.
        byte[] head = dst.Items.Count > 0 ? NonNullLink : new byte[4];
        Array.Copy(head, 0, dst.SavBytes, PorFormat.OffItemsPtr, 4);
        WriteAtomic(dst.SavPath, dst.SavBytes);
        return dst.Items.Count;
    }

    private static void SetHead(SaveCharacter c, bool present)
    {
        // Patch the effects-head pointer in the full raw .SAV bytes, preserving everything else
        // (including any bytes past the 285-byte record) so a write-back never truncates the file.
        byte[] head = present ? NonNullLink : new byte[4];
        Array.Copy(head, 0, c.SavBytes, PorFormat.OffEffectsPtr, 4);
        WriteAtomic(c.SavPath, c.SavBytes);
    }

    /// <summary>Writes bytes via a same-directory temp file plus an atomic replace, so a failed or
    /// partial write can never leave the real save file truncated or half-written.</summary>
    private static void WriteAtomic(string path, byte[] data)
    {
        string tmp = path + ".tmp";
        File.WriteAllBytes(tmp, data);
        File.Move(tmp, path, overwrite: true);
    }
}
