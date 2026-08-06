using System.IO;
using System.IO.Compression;

namespace TheQuestTrainer.Game;

/// <summary>
/// The world's own map picture, decoded, plus the scale that ties it to the outdoor tile grid.
/// </summary>
/// <param name="Image">The decoded picture.</param>
/// <param name="PixelsPerTile">How many pixels of it one outdoor tile occupies.</param>
/// <param name="Source">Where it was loaded from, for the status line.</param>
public sealed record WorldPicture(DecodedImage Image, double PixelsPerTile, string Source)
{
    /// <summary>Picture x of the centre of world-absolute tile column <paramref name="tileX"/>.</summary>
    public double PixelX(double tileX) => (tileX + 0.5) * PixelsPerTile;

    /// <inheritdoc cref="PixelX"/>
    public double PixelY(double tileY) => (tileY + 0.5) * PixelsPerTile;
}

/// <summary>
/// Finds and decodes the picture the game draws its own world map from.
///
/// The Quest keeps its art in <c>.pak</c> files that are ordinary zip archives, and a resource id is
/// the pack name, an underscore and the file's stem: the world object's <c>base_-WORLDMAP-</c> is
/// <c>worlds/base/-WORLDMAP-.dds</c> inside <c>data.pak</c>. An expansion's world is the same shape
/// inside its own pak under <c>expansions\</c>, so every pak in the install is searched rather than
/// just the main one.
///
/// <b>Nothing from the game is redistributed.</b> The picture is read out of the copy the player
/// already owns, found from the attached process's own executable path, exactly as the Dragon Wars
/// trainer reads that game's data files out of the folder it is pointed at. The trainer works
/// without it — the map tab falls back to a plain grid — so a missing or unreadable pak is a note,
/// never a failure.
/// </summary>
public static class WorldPictureLoader
{
    /// <summary>Where inside a pak a world's art lives.</summary>
    private const string WorldsFolder = "worlds/";

    /// <summary>Subfolder of the install holding expansion paks.</summary>
    private const string ExpansionsFolder = "expansions";

    /// <summary>
    /// Loads the picture named by <paramref name="pictureId"/> for the pack
    /// <paramref name="pack"/>, searching every pak in <paramref name="gameFolder"/>.
    /// </summary>
    /// <param name="gameFolder">The folder holding <c>TheQuest.exe</c> and <c>data.pak</c>.</param>
    /// <param name="pack">The world's resource pack, e.g. <c>base</c>.</param>
    /// <param name="pictureId">The world's picture id, e.g. <c>base_-WORLDMAP-</c>.</param>
    /// <param name="tilesPerSide">Outdoor tiles along the whole world, used to derive the scale.</param>
    /// <param name="detail">Always set: where it came from, or why there is nothing.</param>
    public static WorldPicture? Load(string? gameFolder, string? pack, string? pictureId,
                                     int tilesPerSide, out string detail)
    {
        detail = "";

        if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder))
        {
            detail = "The game folder is not known yet, so the world map picture was not loaded.";
            return null;
        }
        if (string.IsNullOrEmpty(pack) || string.IsNullOrEmpty(pictureId))
        {
            detail = "The world does not name a map picture.";
            return null;
        }

        string entry = EntryFor(pack, pictureId);

        foreach (string pak in Paks(gameFolder))
        {
            byte[]? bytes;
            try
            {
                using var zip = ZipFile.OpenRead(pak);
                var found = zip.GetEntry(entry);
                if (found is null) continue;

                using var stream = found.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                bytes = buffer.ToArray();
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                detail = $"Could not read {Path.GetFileName(pak)}: {e.Message}";
                continue;
            }

            var image = DdsImage.Decode(bytes, out string why);
            if (image is null)
            {
                detail = $"{entry} in {Path.GetFileName(pak)} could not be decoded: {why}";
                continue;
            }

            // The picture covers the whole outdoor grid, so its scale follows from the grid rather
            // than from a constant: Freymore is 14 cells of 21 tiles and its picture is 588 pixels,
            // i.e. two pixels a tile.
            double scale = tilesPerSide > 0 ? (double)image.Width / tilesPerSide : 1;
            detail = $"World map loaded from {Path.GetFileName(pak)} ({why} {scale:0.##} px/tile).";
            return new WorldPicture(image, scale, pak);
        }

        if (detail.Length == 0)
            detail = $"No {entry} in any pak under {gameFolder}.";
        return null;
    }

    /// <summary>
    /// The zip entry a resource id names. The id is the pack, an underscore and the stem, and the
    /// pack can itself contain underscores, so only the leading <c>pack_</c> is removed.
    /// </summary>
    public static string EntryFor(string pack, string pictureId)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(pictureId);

        string prefix = pack + "_";
        string stem = pictureId.StartsWith(prefix, StringComparison.Ordinal)
            ? pictureId[prefix.Length..]
            : pictureId;
        return $"{WorldsFolder}{pack}/{stem}.dds";
    }

    /// <summary>Every pak in the install, the main one first so it is not shadowed by an expansion.</summary>
    private static IEnumerable<string> Paks(string gameFolder)
    {
        string[] top;
        try { top = Directory.GetFiles(gameFolder, "*.pak"); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { yield break; }

        Array.Sort(top, StringComparer.OrdinalIgnoreCase);
        foreach (string pak in top) yield return pak;

        string expansions = Path.Combine(gameFolder, ExpansionsFolder);
        if (!Directory.Exists(expansions)) yield break;

        string[] more;
        try { more = Directory.GetFiles(expansions, "*.pak"); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { yield break; }

        Array.Sort(more, StringComparer.OrdinalIgnoreCase);
        foreach (string pak in more) yield return pak;
    }
}
