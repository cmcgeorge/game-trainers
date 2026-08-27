using System.IO;
using System.IO.Compression;

namespace TheQuestTrainer.Adventures;

/// <summary>
/// One adventure the installed game can play: the pak it lives in, and enough of its header to name
/// it in a list without decoding the whole world.
/// </summary>
/// <param name="PakPath">The <c>.pak</c> the world came out of.</param>
/// <param name="EntryName">The entry inside that zip, e.g. <c>pdbs/TheQuestBase.pdb</c>.</param>
/// <param name="Name">The world's own name, e.g. <c>Freymore</c>.</param>
/// <param name="Pack">The resource pack prefix, e.g. <c>base</c>.</param>
/// <param name="Database">The database name, e.g. <c>TheQuestBase</c>.</param>
/// <param name="FormatVersion">The serialization version the header declares.</param>
/// <param name="IsExpansion">Whether the pak came from the <c>expansions</c> folder.</param>
public sealed record AdventureSource(string PakPath, string EntryName, string Name, string Pack,
                                     string Database, int FormatVersion, bool IsExpansion)
{
    /// <summary>"Freymore — the base game" / "Islands of Ice and Fire — isle.pak".</summary>
    public string Display =>
        IsExpansion ? $"{Name} — {Path.GetFileName(PakPath)}" : $"{Name} — the base game";
}

/// <summary>
/// Finds the adventures in a Quest installation.
///
/// The game keeps its content in <c>.pak</c> files that are ordinary zip archives, and every
/// playable world is one Palm database under <c>pdbs/</c> inside one of them: the base game's
/// <c>data.pak</c> carries <c>TheQuestBase</c>, and each downloadable adventure is a pak of its own
/// under <c>expansions\</c> carrying its own world. This is the same shape
/// <see cref="Game.WorldPictureLoader"/> already relies on for the world map picture.
///
/// <b>Nothing from the game is redistributed or modified.</b> The paks are read out of the copy the
/// player already owns and are only ever opened for reading.
///
/// Not every <c>ThQW</c> database is an adventure: <c>data.pak</c> also ships <c>TheQuestRes</c> and
/// <c>TheQuestSound</c>, which are art and audio in the same container. A world is told apart by its
/// grid prefix — the string an outdoor map id is built from, <c>base_s</c> for Freymore. The two
/// resource databases have none, because they have no maps to lay on a grid.
/// </summary>
public static class AdventureCatalog
{
    /// <summary>Where inside a pak the world databases live.</summary>
    private const string DatabaseFolder = "pdbs/";

    /// <summary>Subfolder of the install holding the downloadable adventures.</summary>
    public const string ExpansionsFolder = "expansions";

    /// <summary>
    /// Every adventure in <paramref name="gameFolder"/>, the base game first.
    /// </summary>
    /// <param name="gameFolder">The folder holding <c>TheQuest.exe</c> and <c>data.pak</c>.</param>
    /// <param name="detail">Always set: what was found, or why nothing was.</param>
    public static IReadOnlyList<AdventureSource> Find(string? gameFolder, out string detail)
    {
        detail = "";
        var found = new List<AdventureSource>();

        if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder))
        {
            detail = "No game folder, so no adventures were looked for.";
            return found;
        }

        var problems = new List<string>();
        foreach ((string pak, bool isExpansion) in Paks(gameFolder))
        {
            try
            {
                using var zip = ZipFile.OpenRead(pak);
                foreach (var entry in zip.Entries)
                {
                    if (!entry.FullName.StartsWith(DatabaseFolder, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!entry.FullName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)) continue;

                    var source = Describe(pak, entry, isExpansion);
                    if (source is not null) found.Add(source);
                }
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                problems.Add($"{Path.GetFileName(pak)}: {e.Message}");
            }
        }

        detail = found.Count == 0
            ? $"No adventures were found under {gameFolder}."
            : $"{found.Count} adventure{(found.Count == 1 ? "" : "s")} found.";
        if (problems.Count > 0) detail += " Could not read " + string.Join("; ", problems) + ".";

        return found;
    }

    /// <summary>
    /// Decodes one adventure in full.
    /// </summary>
    /// <param name="source">One of the entries <see cref="Find"/> returned.</param>
    /// <param name="why">Set when the return is null.</param>
    public static Adventure? Load(AdventureSource source, out string why)
    {
        ArgumentNullException.ThrowIfNull(source);

        byte[]? bytes = Extract(source.PakPath, source.EntryName, out why);
        if (bytes is null) return null;

        var database = PalmDatabase.Parse(bytes, out why);
        if (database is null) return null;

        return AdventureReader.Read(database, $"{Path.GetFileName(source.PakPath)}!{source.EntryName}", out why);
    }

    /// <summary>Reads one entry's header far enough to name it, or null when it is not a world.</summary>
    private static AdventureSource? Describe(string pak, ZipArchiveEntry entry, bool isExpansion)
    {
        byte[] bytes;
        try
        {
            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }
        catch (Exception e) when (e is IOException or InvalidDataException)
        {
            return null;
        }

        var database = PalmDatabase.Parse(bytes, out _);
        if (database is null || !database.IsQuestWorld) return null;

        // Only the header: naming an adventure must not cost a whole world, because this runs on the
        // UI thread while the trainer is attaching.
        if (AdventureReader.Describe(database, out _) is not { } header) return null;
        if (header.GridPrefix.Length == 0) return null;

        return new AdventureSource(pak, entry.FullName, header.Name, header.Pack,
                                   database.Name, header.Version, isExpansion);
    }

    private static byte[]? Extract(string pak, string entryName, out string why)
    {
        why = "";
        try
        {
            using var zip = ZipFile.OpenRead(pak);
            var entry = zip.GetEntry(entryName);
            if (entry is null)
            {
                why = $"{entryName} is no longer in {Path.GetFileName(pak)}.";
                return null;
            }

            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            why = $"could not read {Path.GetFileName(pak)}: {e.Message}";
            return null;
        }
    }

    /// <summary>Every pak in the install, the main one first so the base game heads the list.</summary>
    private static IEnumerable<(string Path, bool IsExpansion)> Paks(string gameFolder)
    {
        foreach (string pak in Sorted(gameFolder)) yield return (pak, false);

        string expansions = Path.Combine(gameFolder, ExpansionsFolder);
        if (!Directory.Exists(expansions)) yield break;

        foreach (string pak in Sorted(expansions)) yield return (pak, true);
    }

    private static string[] Sorted(string folder)
    {
        string[] paks;
        try { paks = Directory.GetFiles(folder, "*.pak"); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }

        Array.Sort(paks, StringComparer.OrdinalIgnoreCase);
        return paks;
    }
}
