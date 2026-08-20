namespace BardsTaleTrilogyTrainer.Game;

/// <summary>
/// Static facts about The Bard's Tale Trilogy remaster that the trainer depends on.
///
/// <para>The class-pointer RVAs below are the metadata-usage slots inside
/// <c>GameAssembly.dll</c> where the IL2CPP runtime caches each type's <c>Il2CppClass*</c>.
/// They were read out of the shipped binary (see <c>docs/ReverseEngineering.md</c>) and are
/// build-specific, so every use is validated by comparing the class's name and namespace
/// before anything is read through it; <see cref="Il2CppClassLocator"/> falls back to
/// scanning the module when a slot does not check out.</para>
/// </summary>
public static class GameFacts
{
    /// <summary>Process executable name (without path).</summary>
    public const string ProcessName = "TheBardsTaleTrilogy";

    /// <summary>The IL2CPP native module that holds all compiled game logic.</summary>
    public const string GameModuleName = "GameAssembly.dll";

    /// <summary>Namespace of the game's own types inside <c>Assembly-CSharp</c>.</summary>
    public const string GameNamespace = "BardsTale";

    /// <summary>Maximum party slots (<c>Party.MaxSlots</c>).</summary>
    public const int PartySlots = 7;

    /// <summary>Per-character inventory slots (<c>Character.InventorySize</c>).</summary>
    public const int CharacterInventorySize = 16;

    /// <summary>Shared party inventory slots (<c>Party.InventorySize</c>).</summary>
    public const int PartyInventorySize = 40;

    /// <summary>Maximum character level the trainer will write.</summary>
    public const int MaxLevel = 99;

    /// <summary>Attribute range (generous upper bound for buffs and equipment bonuses).</summary>
    public const int MinAttribute = 1;
    public const int MaxAttribute = 100;

    // --- IL2CPP class slots (RVAs inside GameAssembly.dll) -----------------------
    /// <summary>Slot holding <c>Il2CppClass*</c> for <c>BardsTale.Party</c>.</summary>
    public const int PartyClassRva = 0xE44900;

    /// <summary>Slot holding <c>Il2CppClass*</c> for <c>BardsTale.Player</c>.</summary>
    public const int PlayerClassRva = 0xE44BF8;

    /// <summary>Slot holding <c>Il2CppClass*</c> for <c>BardsTale.GlobalMaps</c>.</summary>
    public const int GlobalMapsClassRva = 0xE44D50;

    /// <summary>Slot holding <c>Il2CppClass*</c> for <c>BardsTale.TeleportTarget</c>.</summary>
    public const int TeleportTargetClassRva = 0xE46478;

    /// <summary>Slot holding <c>Il2CppClass*</c> for <c>BardsTale.Automap</c>.</summary>
    public const int AutomapClassRva = 0xE44D38;

    /// <summary>
    /// Slot holding <c>Il2CppClass*</c> for <c>BardsTale.GlobalSpells</c> — the singleton that
    /// owns the spell table, and so the trainer's source for every spell's code, school and level.
    /// </summary>
    public const int GlobalSpellsClassRva = 0xE44C18;

    /// <summary>Build the RVAs above were read from (Unity player version string).</summary>
    public const string ConfirmedBuild = "Unity 2018.4.0.11993000 (Steam, app 843260)";

    /// <summary>Where a Steam install usually lives, used when the game is not running.</summary>
    public static readonly string[] LikelyGameDirectories =
    {
        @"C:\Program Files (x86)\Steam\steamapps\common\The Bard's Tale Trilogy",
        @"C:\Program Files\Steam\steamapps\common\The Bard's Tale Trilogy",
        @"C:\GOG Games\The Bard's Tale Trilogy",
    };
}
