namespace SwordOfAragonTrainer.Game;

/// <summary>
/// Fixed facts about Sword of Aragon (SSI, 1989) recovered from the shipped executables and save
/// files. See <c>docs/RE.md</c> for how each one was established; every value here is in the
/// Confirmed set.
/// </summary>
public static class GameFacts
{
    /// <summary>Executable the front end runs (copy protection, character creation).</summary>
    public const string FrontEndExe = "SWORD.EXE";

    /// <summary>Executable that owns the world map and the kingdom state.</summary>
    public const string WorldMapExe = "ARAGON.EXE";

    /// <summary>Executable that owns tactical battles.</summary>
    public const string BattleExe = "HEXWAR.EXE";

    /// <summary>Save letters the game offers (Z is reserved as its scratch slot).</summary>
    public const string SaveLetters = "ABCDEFGHIJKLMNOPQRSTUVWXY";

    /// <summary>Kingdom-state file for a save letter, e.g. <c>ARAGON.HSA</c>.</summary>
    public static string KingdomFileName(char letter) => $"ARAGON.HS{char.ToUpperInvariant(letter)}";

    /// <summary>Roster file for a save letter, e.g. <c>ARAGON.HRA</c>.</summary>
    public static string RosterFileName(char letter) => $"ARAGON.HR{char.ToUpperInvariant(letter)}";

    /// <summary>Chronicle-of-deeds file for a save letter, e.g. <c>ARAGON.HIA</c>.</summary>
    public static string ChronicleFileName(char letter) => $"ARAGON.HI{char.ToUpperInvariant(letter)}";

    /// <summary>World-map grid file for a save letter (not edited by this trainer).</summary>
    public static string MapFileName(char letter) => $"ARAGON.HT{char.ToUpperInvariant(letter)}";

    // --- world -----------------------------------------------------------------
    /// <summary>Width and height of both the world map and every tactical map, in hexes.</summary>
    public const int MapSize = 24;

    /// <summary>The game opens in April 871 QJ; year fields are stored as an offset from this.</summary>
    public const int BaseYear = 871;

    /// <summary>Months are stored 0-based (0 = January), so the shipped start value is 3 = April.</summary>
    public const int StartMonth = 3;

    /// <summary>Highest score the game will award — the City Status screen prints "(500)".</summary>
    public const int MaxScore = 500;

    /// <summary>Highest tax rate the game accepts ("You must use a RATE from 0 to 80 percent.").</summary>
    public const int MaxTaxRate = 80;

    /// <summary>Stacking allowance per hex, in size points.</summary>
    public const int StackingLimit = 200;

    /// <summary>A tactical battle cannot run past this turn.</summary>
    public const int MaxBattleTurns = 23;

    /// <summary>The earliest turn on which Quit is offered.</summary>
    public const int EarliestQuitTurn = 7;

    /// <summary>
    /// Ceiling the trainer applies to edited gold/income figures. The game stores them as
    /// QuickBASIC single-precision (24-bit mantissa), so beyond ~16.7 million an integer no longer
    /// round-trips; this stays comfortably inside that and inside the game's own display width.
    /// </summary>
    public const double MaxWealth = 9_999_999;

    /// <summary>Month names as the game spells them.</summary>
    public static readonly string[] Months =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    };

    /// <summary>Months in which unsupplied units suffer attrition outside a friendly city.</summary>
    public static readonly int[] AttritionMonths = { 11, 0, 1 };   // December, January, February

    /// <summary>Renders a stored (yearOffset, month) pair the way the game dates its events.</summary>
    public static string FormatDate(int yearOffset, int month)
    {
        string name = month >= 0 && month < Months.Length ? Months[month] : $"month {month}";
        return $"{name} {BaseYear + yearOffset} QJ";
    }
}
