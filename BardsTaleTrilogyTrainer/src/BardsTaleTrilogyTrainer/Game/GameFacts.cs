namespace BardsTaleTrilogyTrainer.Game;

/// <summary>
/// Static facts about The Bard's Tale Trilogy remaster that the trainer depends on.
/// Sourced from online research, CE scripts, and community resources.
/// </summary>
public static class GameFacts
{
    /// <summary>Process executable name (without path).</summary>
    public const string ProcessName = "TheBardsTaleTrilogy";

    /// <summary>The IL2CPP native module that holds all compiled game logic.</summary>
    public const string GameModuleName = "GameAssembly.dll";

    /// <summary>Maximum party size (slot 0 = special/summon, 1–6 = members, matching the original).</summary>
    public const int PartySlots = 7;

    /// <summary>Maximum character level (the remaster supports levels well beyond the original's 40).</summary>
    public const int MaxLevel = 99;

    /// <summary>Attribute range (generous upper bound to accommodate temporary buffs and equipment bonuses).</summary>
    public const int MinAttribute = 1;
    public const int MaxAttribute = 100;

    /// <summary>RVA of the global game-state pointer inside GameAssembly.dll.
    /// [Confirmed] for game version 4.28 (CE script, August 2019).</summary>
    public const int GlobalPointerRva = 0xE40338;

    /// <summary>Offset from the game-state object to the party/economy sub-object.
    /// [Confirmed] for v4.28: <c>mov rdx,[rcx+000000B8]</c> in the gold script.</summary>
    public const int GameStatePartyOffset = 0xB8;

    /// <summary>Offset of gold on the party/economy object.
    /// [Confirmed] for v4.28: <c>mov [rdi+68],rax</c> in the gold script.</summary>
    public const int PartyGoldOffset = 0x68;

    /// <summary>Version string the CE table was last confirmed against.</summary>
    public const string ConfirmedVersion = "4.28–4.34";
}
