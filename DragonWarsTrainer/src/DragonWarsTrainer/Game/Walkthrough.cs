namespace DragonWarsTrainer.Game;

/// <summary>A titled strategy/walkthrough section.</summary>
public sealed record WalkthroughSection(string Title, string Body);

/// <summary>
/// A condensed in-app strategy reference for Dragon Wars, transcribed from the hitchhikerprod
/// walkthrough. Shown on the References tab; reference only.
/// </summary>
public static class Walkthrough
{
    public static readonly IReadOnlyList<WalkthroughSection> Sections = new WalkthroughSection[]
    {
        new("The goal",
            "You are prisoners dumped in Purgatory by the tyrant Drake of Phoebus. Escape, then work " +
            "across Dilmun to defeat the necromancer Namtar: free Irkalla, reforge the Sword of Freedom, " +
            "win the Dragon Gem, destroy Namtar's army in the frozen wastes of Nisir, slay Namtar, and " +
            "throw his Dead Body into The Pit in the Magan Underworld."),

        new("Party build & skills",
            "Create a balanced 4-character party. Train fighters in Swords or Axes and give them Low " +
            "Magic 1 early for the cheap 30' Mage Fire attack. Make sure someone has Bandage (post-combat " +
            "healing) and Swim (needed to survive deep water and to leave Purgatory). One character should " +
            "develop Arcane Lore (Nexus teleporters) and Lockpick (chests). Spread the four Lore skills " +
            "(Town/Cave/Forest/Mountain) — they are checked constantly for paths and clues."),

        new("Combat & mechanics",
            "Most melee deals full Stun and half Health damage, so enemies drop unconscious before dying. " +
            "Lower AC is better. Power (mana) recharges only at pools/rest; variable-cost spells scale with " +
            "your school rank. Disarm and ranged spells (Mage Fire, Fire Light) open fights safely. Keep a " +
            "healer topped up and bandage the wounded between battles."),

        new("Escaping Purgatory",
            "Accumulate a few levels in the Arena (talk to the Arena Master at (19,26) for Citizenship " +
            "Papers). Recharge Power free at the pool (23,2). Two exits: the Morgue at (31,10) — climb into " +
            "the body bags — sets the friendly Slave Camp flag (0x40); or the Hole in the Wall bay at (25,8) " +
            "if you can Swim. Recruit Ulrik at Phoebus's Tavern (25,27)."),

        new("Early Dilmun",
            "Visit the free heal/recharge pool at (14,1) on the overworld. Cross Guard Bridge #1 to the Isle " +
            "of the Sun; wrestle Enkidu for Druid Magic 2; buy Sun Magic and solve the Mystic Wood. Collect " +
            "the Stone Arms/Head/Hand and repair the statue in the City of the Yellow Mud Toad."),

        new("Freeport",
            "Reached from the overworld at (43,23). Ryan's Armor Shop (5,14) sells Large Shields cheap. " +
            "Recruit Halifax at the Brews Brothers Tavern (14,7). The Order of the Sword (14,8) grants the " +
            "Stone Hands and the Spell Staff. Avoid the fake Sword of Freedom at (3,4) — picking it up " +
            "incinerates the character who grabs it."),

        new("Endgame",
            "Obtain the Dragon Gem from the Lansk dragon and the Silver Key from Nergal to free Irkalla. " +
            "Reforge the Sword of Freedom. Take the Dragon Gem into Nisir to break Namtar's army, then kill " +
            "Namtar. Carry his Dead Body to the Magan Underworld and hurl it into The Pit to win."),
    };
}
