namespace WastelandRemasteredTrainer.Game;

/// <summary>
/// Static facts about Wasteland Remastered that the trainer depends on.
///
/// <para>The game is a Unity IL2CPP 64-bit build (Unity 2018.4.0f1). The process name is
/// "Wasteland Remastered" and the IL2CPP native module is <c>GameAssembly.dll</c>.
/// All game types live in the global namespace (empty string).</para>
///
/// <para>Unlike the Bard's Tale Trilogy trainer, no metadata-usage slot RVAs are known for
/// this build, so the class locator uses the module sweep approach as its primary strategy.</para>
/// </summary>
public static class GameFacts
{
    /// <summary>Process executable name (without path or extension).</summary>
    public const string ProcessName = "Wasteland Remastered";

    /// <summary>The IL2CPP native module that holds all compiled game logic.</summary>
    public const string GameModuleName = "GameAssembly.dll";

    /// <summary>Namespace of the game's own types (global namespace).</summary>
    public const string GameNamespace = "";

    /// <summary>IL2CPP type name of a party member.</summary>
    public const string PlayerTypeName = "Player";

    /// <summary>IL2CPP type name of the party singleton.</summary>
    public const string PartyTypeName = "Party";

    /// <summary>Maximum party slots.</summary>
    public const int PartySlots = 7;

    /// <summary>
    /// How many entries of <c>Party.players</c> to walk. Deliberately larger than
    /// <see cref="PartySlots"/>: the list is clamped to this rather than rejected, so a roster
    /// that legitimately holds more than the seven marching slots still yields its members
    /// instead of reporting an empty party.
    /// </summary>
    public const int MaxPartyListEntries = 32;

    /// <summary>Skill slots in the packed skill array per character.</summary>
    public const int SkillSlots = 30;

    /// <summary>Inventory slots in the packed item array per character.</summary>
    public const int ItemSlots = 30;

    /// <summary>Maximum character level the trainer will write.</summary>
    public const int MaxLevel = 99;

    /// <summary>Attribute range (generous upper bound).</summary>
    public const int MinAttribute = 1;
    public const int MaxAttribute = 99;

    /// <summary>Maximum CON (hit points) the trainer will write.</summary>
    public const int MaxCon = 5000;

    /// <summary>Maximum money the trainer will write.</summary>
    public const int MaxMoney = 999999;

    /// <summary>Maximum experience the trainer will write.</summary>
    public const int MaxExperience = 999999;

    /// <summary>Maximum skill level.</summary>
    public const int MaxSkillLevel = 10;

    /// <summary>Maximum skill points.</summary>
    public const int MaxSkillPoints = 99;

    /// <summary>Maximum ammo per inventory slot (7-bit field).</summary>
    public const int MaxAmmo = 99;

    /// <summary>Build the offsets were extracted from.</summary>
    public const string ConfirmedBuild = "Unity 2018.4.0f1 (Steam, Wasteland Remastered)";

    /// <summary>Where a Steam install usually lives.</summary>
    public static readonly string[] LikelyGameDirectories =
    {
        @"C:\Program Files (x86)\Steam\steamapps\common\Wasteland Remastered",
        @"C:\Program Files\Steam\steamapps\common\Wasteland Remastered",
        @"D:\Steam\steamapps\common\Wasteland Remastered",
        @"D:\SteamLibrary\steamapps\common\Wasteland Remastered",
    };
}
