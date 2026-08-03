namespace DarkDesigns1Trainer.Game;

/// <summary>
/// Confirmed facts about Dark Designs I, used by the trainer's UI and locator.
/// </summary>
public static class GameFacts
{
    public const string GameTitle = "Dark Designs I: Grelminar's Staff";
    public const string Author = "John Carmack";
    public const int Year = 1990;
    public const string Publisher = "Softdisk / Big Blue Disk";

    public const string ExeName = "DARKDES.EXE";
    public const string CharsFile = "DDCHARS.DAT";
    public const string MapFilePattern = "DDMAP{0}.DAT";
    public const int MapCount = 5;

    public static readonly string[] LevelNames =
        { "Top Castle Level", "Mid Castle Level", "Ground Level", "Dungeon Level 1", "Dungeon Level 2" };

    public const int MaxPartySize = 4;
    public const int MaxRosterSize = 20;

    /// <summary>The title string used as the locator anchor (plain ASCII, 34 bytes).</summary>
    public const string AnchorString = "Dark Designs I : Grelminar's Staff";

    /// <summary>Additional validator strings that should appear near the anchor in the data segment.</summary>
    public static readonly string[] ValidatorStrings =
    {
        "# NAME          BODY  STATUS MAGIC CLAU",
        "DDCHARS.DAT",
        "DDMAP1.DAT",
    };

    public const int AttributeMin = 3;
    public const int AttributeMax = 18;
}
