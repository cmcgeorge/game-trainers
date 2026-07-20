using System.IO;
using System.Text.Json;

namespace WastelandTrainer.Game;

/// <summary>
/// User-captured teleport destinations, persisted between runs. Only the Ranger Center start is
/// confirmed against the shipped save (see <see cref="MapBook.TeleportTargets"/>); every other spot is
/// captured by the player from a save they made <b>standing there</b>, so the map id and X/Y are exactly
/// what the game reads on load — the one reliable way to grow the "Jump to" list without guessing at
/// unconfirmed interior grids. The list is stored as JSON under
/// <c>%APPDATA%\WastelandTrainer\teleports.json</c>.
/// </summary>
public static class TeleportBookmarks
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Full path of the bookmarks file (created on first save).</summary>
    public static string FilePath
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WastelandTrainer");
            return Path.Combine(dir, "teleports.json");
        }
    }

    /// <summary>Loads the saved bookmarks, or an empty list when none exist or the file is unreadable.</summary>
    public static List<TeleportTarget> Load()
    {
        try
        {
            string path = FilePath;
            if (!File.Exists(path)) return new List<TeleportTarget>();
            var list = JsonSerializer.Deserialize<List<TeleportTarget>>(File.ReadAllText(path));
            return list ?? new List<TeleportTarget>();
        }
        catch
        {
            return new List<TeleportTarget>();
        }
    }

    /// <summary>Writes the bookmarks back to disk, creating the folder if needed.</summary>
    public static void Save(IEnumerable<TeleportTarget> bookmarks)
    {
        string path = FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(bookmarks, JsonOptions));
    }
}
