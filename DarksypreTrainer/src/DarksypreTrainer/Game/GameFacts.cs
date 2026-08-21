namespace DarksypreTrainer.Game;

/// <summary>
/// Confirmed constants for DarkSpyre (Event Horizon Software, 1990).
/// Sources, in order of authority: the game's own shipped data files (<c>CR.DAT</c>,
/// <c>OBJ.DAT</c>, <c>darkspyre.txt</c>), a live DOSBox session, then the Cheatbook
/// walkthrough. Values taken from the files are marked [File]; values only stated in the
/// manual or walkthrough are marked [Manual].
/// </summary>
internal static class GameFacts
{
    /// <summary>Game title.</summary>
    public const string GameTitle = "DarkSpyre";

    /// <summary>Developer.</summary>
    public const string Developer = "Event Horizon Software";

    /// <summary>Release year.</summary>
    public const int ReleaseYear = 1990;

    /// <summary>Maximum attribute value (manual states "can never exceed 20"). [Manual]</summary>
    public const int MaxAttribute = 20;

    /// <summary>
    /// Maximum spell points the game itself will hand out (the manual states "spell points can
    /// never exceed 100"). Not a clamp: a higher maximum poked into the character record is
    /// honoured by the engine. [Manual]
    /// </summary>
    public const int MaxSpellPoints = 100;

    /// <summary>Total levels in the game. [Manual]</summary>
    public const int TotalLevels = 50;

    /// <summary>Required levels to complete the game. [Manual]</summary>
    public const int RequiredLevels = 39;

    /// <summary>Number of character attributes. [Manual]</summary>
    public const int AttributeCount = 6;

    /// <summary>Number of weapon proficiency types. [Manual]</summary>
    public const int WeaponTypeCount = 7;

    /// <summary>Number of weapon proficiency levels. [Manual]</summary>
    public const int WeaponProficiencyLevels = 10;

    /// <summary>Number of magic skill classes. [Manual]</summary>
    public const int MagicClassCount = 6;

    /// <summary>Number of magic proficiency levels. [Manual]</summary>
    public const int MagicProficiencyLevels = 7;

    /// <summary>Number of armor protection levels. [Manual]</summary>
    public const int ArmorProtectionLevels = 15;

    /// <summary>Number of armor condition levels. [Manual]</summary>
    public const int ArmorConditionLevels = 7;

    /// <summary>Number of runes of power (the 5 special ones needed to complete the game). [Manual]</summary>
    public const int PowerRuneCount = 5;

    /// <summary>Total number of runes in the game. [Manual]</summary>
    public const int TotalRunes = 25;

    /// <summary>HP formula: Strength + Endurance + random. [Manual]</summary>
    public const string HpFormula = "Strength + Endurance + Random";

    /// <summary>SP formula: Talent + Power + random. [Manual]</summary>
    public const string SpFormula = "Talent + Power + Random";

    /// <summary>Creature types shipped in <c>CR.DAT</c>, excluding the player. [File]</summary>
    public const int CreatureCount = 35;

    /// <summary>Objects in the <c>OBJ.DAT</c> table. [File]</summary>
    public const int ObjectCount = 162;

    /// <summary>Bytes per object record in <c>OBJ.DAT</c>, ahead of the name table. [File]</summary>
    public const int ObjectRecordSize = 57;

    /// <summary>Emulator process name hints for auto-selection.</summary>
    public static readonly string[] EmulatorHints =
        { "dosbox", "dosbox-x", "dosbox-staging", "scummvm", "boxer" };
}
