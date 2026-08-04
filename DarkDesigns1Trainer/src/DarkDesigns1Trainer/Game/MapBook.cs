using System.IO;

namespace DarkDesigns1Trainer.Game;

/// <summary>One of the five dungeon levels, for the level picker.</summary>
public sealed record MapLevel(int Number, string Name)
{
    public string Header => $"{Number}. {Name}";
}

/// <summary>
/// The five Dark Designs I levels and how to read their <c>DDMAP&lt;n&gt;.DAT</c> files.
///
/// The level names are the game's own, printed under the compass by the routine at <c>0x25C4</c>,
/// which switches on the level global and prints one of five 16-character strings — so level 1 is
/// the top of the castle and 5 the bottom of the dungeon, and going *down* stairs increases it.
/// </summary>
public static class MapBook
{
    /// <summary>The five levels, from the top of the castle down.</summary>
    public static readonly IReadOnlyList<MapLevel> Levels = BuildLevels();

    private static MapLevel[] BuildLevels()
    {
        var levels = new MapLevel[GameFacts.MapCount];
        for (int i = 0; i < levels.Length; i++)
            levels[i] = new MapLevel(i + 1, GameFacts.LevelNames[i]);
        return levels;
    }

    /// <summary>Name of a level number, or a description of where the party is if it is not on one.</summary>
    public static string LevelName(int level)
    {
        if (level == MapFormat.TownLevel) return "Town";
        if (level >= MapFormat.MinLevel && level <= MapFormat.MaxLevel)
            return GameFacts.LevelNames[level - 1];
        return $"Level {level}";
    }

    /// <summary>File name of a level's map, e.g. <c>DDMAP3.DAT</c>.</summary>
    public static string MapFileName(int level) => string.Format(GameFacts.MapFilePattern, level);

    /// <summary>
    /// Reads every <c>DDMAP&lt;n&gt;.DAT</c> in <paramref name="folder"/> that is the right size and
    /// passes <see cref="MapFormat.LooksLikeMap"/>. Returns level number → map. A folder that holds
    /// none is reported through <paramref name="error"/> rather than throwing, since the caller is a
    /// folder picker.
    /// </summary>
    public static bool TryLoadFromFolder(string folder, out Dictionary<int, DungeonMap> maps, out string error)
    {
        maps = new Dictionary<int, DungeonMap>();
        error = "";

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            error = $"{folder} is not a folder.";
            return false;
        }

        var problems = new List<string>();
        for (int level = MapFormat.MinLevel; level <= MapFormat.MaxLevel; level++)
        {
            string path = Path.Combine(folder, MapFileName(level));
            if (!File.Exists(path)) continue;
            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length != MapFormat.FileSize)
                {
                    problems.Add($"{MapFileName(level)} is {bytes.Length} bytes, expected {MapFormat.FileSize}");
                    continue;
                }
                if (!MapFormat.LooksLikeMap(bytes, 0))
                {
                    problems.Add($"{MapFileName(level)} does not decode as a level");
                    continue;
                }
                maps[level] = new DungeonMap(bytes, 0, level);
            }
            catch (Exception ex)
            {
                problems.Add($"{MapFileName(level)}: {ex.Message}");
            }
        }

        if (maps.Count > 0) return true;

        error = problems.Count > 0
            ? "No usable maps: " + string.Join("; ", problems)
            : $"No {string.Format(GameFacts.MapFilePattern, "1–5")} files in {folder}.";
        return false;
    }
}
