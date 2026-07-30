using System.IO;
namespace SwordOfAragonTrainer.Game;

/// <summary>
/// The four files that make up one saved game, identified by its save letter. Sword of Aragon writes
/// <c>ARAGON.HS?</c> (kingdom state), <c>ARAGON.HR?</c> (roster), <c>ARAGON.HI?</c> (chronicle) and
/// <c>ARAGON.HT?</c> (world grid) side by side in the save directory.
/// </summary>
public sealed class SaveSet
{
    /// <summary>Directory the four files live in.</summary>
    public string Directory { get; }

    /// <summary>Save letter, upper-case.</summary>
    public char Letter { get; }

    public SaveSet(string directory, char letter)
    {
        Directory = directory;
        Letter = char.ToUpperInvariant(letter);
    }

    public string KingdomPath => Path.Combine(Directory, GameFacts.KingdomFileName(Letter));
    public string RosterPath => Path.Combine(Directory, GameFacts.RosterFileName(Letter));
    public string ChroniclePath => Path.Combine(Directory, GameFacts.ChronicleFileName(Letter));
    public string MapPath => Path.Combine(Directory, GameFacts.MapFileName(Letter));

    /// <summary>True when at least the kingdom-state file is present.</summary>
    public bool Exists => File.Exists(KingdomPath);

    /// <summary>True when both editable files are present.</summary>
    public bool IsComplete => File.Exists(KingdomPath) && File.Exists(RosterPath);

    /// <summary>Label for the save-letter picker; includes the character's name when readable.</summary>
    public string Display
    {
        get
        {
            string suffix = IsComplete ? "" : "  (roster missing)";
            string who = DescribePlayer();
            return who.Length > 0 ? $"{Letter} — {who}{suffix}" : $"{Letter}{suffix}";
        }
    }

    /// <summary>
    /// Peeks at the roster's slot 0 for a "name, class, level" label — the same summary the game's own
    /// Old Game screen shows. Returns an empty string if the roster cannot be read.
    /// </summary>
    private string DescribePlayer()
    {
        try
        {
            if (!File.Exists(RosterPath)) return string.Empty;
            var roster = RosterFile.Load(RosterPath);
            var player = roster.Player;
            return $"{player.Name} the {player.TypeName}, level {player.Level}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Every save letter that has at least a kingdom-state file in <paramref name="directory"/>, in
    /// letter order. An unreadable or missing directory yields an empty list rather than throwing.
    /// </summary>
    public static IReadOnlyList<SaveSet> Discover(string directory)
    {
        var found = new List<SaveSet>();
        if (string.IsNullOrWhiteSpace(directory) || !System.IO.Directory.Exists(directory)) return found;

        foreach (char letter in GameFacts.SaveLetters)
        {
            var set = new SaveSet(directory, letter);
            if (set.Exists) found.Add(set);
        }
        return found;
    }

    /// <summary>
    /// Reads the Chronicle of Deeds as display text. The game stores <c>|</c> where a line breaks
    /// inside an entry and separates entries with two of them.
    /// </summary>
    public string ReadChronicle()
    {
        if (!File.Exists(ChroniclePath)) return "(no chronicle file for this save)";
        try
        {
            var bytes = File.ReadAllBytes(ChroniclePath);
            string text = System.Text.Encoding.Latin1.GetString(bytes);
            int eof = text.IndexOf((char)KingdomFile.EofMarker);
            if (eof >= 0) text = text[..eof];
            return text.Replace("|", Environment.NewLine).TrimEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "(chronicle could not be read: " + ex.Message + ")";
        }
    }
}
