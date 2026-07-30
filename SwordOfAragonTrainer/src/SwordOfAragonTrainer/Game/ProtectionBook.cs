namespace SwordOfAragonTrainer.Game;

/// <summary>One city's row of the startup copy-protection answer table.</summary>
public sealed record ProtectionAnswer(string City, string Location, string Resources, string Economy, string Ruler)
{
    /// <summary>The answer for a named field, or empty if the field name is not one of the four.</summary>
    public string ForField(string field) => field.Trim().ToUpperInvariant() switch
    {
        "LOCATION" => Location,
        "RESOURCES" => Resources,
        "ECONOMY" => Economy,
        "RULER" => Ruler,
        _ => string.Empty,
    };
}

/// <summary>
/// The startup copy protection and its complete answer key.
///
/// <c>SWORD.EXE</c> asks: <i>"Using the Sword of Aragon poster, determine the name of this fortress by
/// matching the screen and poster icons. From the city description area in the Duke's Notebook enter
/// the first word of the summary information for that city."</i> — then prompts
/// <c>First word of: &lt;FIELD&gt;</c> where FIELD is one of LOCATION, RESOURCES, ECONOMY or RULER.
///
/// The table below is the game's own answer key, read out of <c>SWORD.EXE</c> at file offsets
/// 0x7250–0x7444 and cross-checked row by row against the matching Notebook entry in the rule book.
/// The seven wilderness regions that also have Notebook entries are not in the table and are never
/// asked about.
///
/// You do not need the poster: the prompt names the field, at least one retry is granted
/// (<c>ERROR: wrong word--try again</c> precedes <c>--too bad!</c>), and
/// <see cref="CandidatesFor"/> returns the whole candidate set for a field — never more than 13 words.
/// </summary>
public static class ProtectionBook
{
    /// <summary>The four field names the prompt can ask for, in table order.</summary>
    public static readonly string[] Fields = { "LOCATION", "RESOURCES", "ECONOMY", "RULER" };

    /// <summary>The answer key, in the game's own city order.</summary>
    public static readonly IReadOnlyList<ProtectionAnswer> Answers = new[]
    {
        new ProtectionAnswer("Aladda",   "NORTHWEST",    "LUMBER",    "FARMING",     "YOU"),
        new ProtectionAnswer("Marinia",  "NORTHWEST",    "RIVER",     "TRAPPING",    "GARDWELL"),
        new ProtectionAnswer("Brocada",  "NORTH",        "GALATION",  "FISHING",     "PETROV"),
        new ProtectionAnswer("Sur Nova", "FOOTHILLS",    "FOREST",    "LOGGING",     "UNKNOWN"),
        new ProtectionAnswer("Paritan",  "NORTH",        "HARBOR",    "SMUGGLING",   "PITLAG"),
        new ProtectionAnswer("Nuralia",  "NORTH",        "RICH",      "AGRICULTURE", "WILFREED"),
        new ProtectionAnswer("Tentula",  "SOUTHEAST",    "LAKE",      "FISHING",     "TANTALA"),
        new ProtectionAnswer("Zarnix",   "JUSTINID",     "MINERALS",  "UNKNOWN",     "GNARDIX"),
        new ProtectionAnswer("Lucedia",  "SOUTHEAST",    "GOOD",      "FARMING",     "COUNCIL"),
        new ProtectionAnswer("Pudawala", "EAST",         "DALATION",  "FISHING",     "EL-IKHOM"),
        new ProtectionAnswer("Sothold",  "NORTHEAST",    "EXCELLENT", "FARMING",     "STRUMBERG"),
        new ProtectionAnswer("Estallah", "NORTHEAST",    "DALATION",  "COMMERCE",    "LANDRATOZ"),
        new ProtectionAnswer("Tetrada",  "NORTHEASTERN", "BORDER",    "COMMERCE",    "LUCINIAN"),
    };

    /// <summary>The prompt text the game shows, for reference in the UI.</summary>
    public const string PromptText =
        "Using the Sword of Aragon poster, determine the name of this fortress by matching the screen " +
        "and poster icons. From the city description area in the Duke's Notebook enter the first word " +
        "of the summary information for that city.";

    /// <summary>
    /// Every distinct answer that can be correct for a field, in the order the cities appear — so
    /// trying them top to bottom needs at most 13 attempts and usually far fewer.
    /// </summary>
    public static IReadOnlyList<string> CandidatesFor(string field)
    {
        var seen = new List<string>();
        foreach (var answer in Answers)
        {
            string word = answer.ForField(field);
            if (word.Length > 0 && !seen.Contains(word, StringComparer.Ordinal)) seen.Add(word);
        }
        return seen;
    }

    /// <summary>The row for a city name (case-insensitive, spaces ignored), or null.</summary>
    public static ProtectionAnswer? ForCity(string city)
    {
        string key = city.Replace(" ", string.Empty);
        return Answers.FirstOrDefault(a =>
            string.Equals(a.City.Replace(" ", string.Empty), key, StringComparison.OrdinalIgnoreCase));
    }
}
