namespace DragonWarsTrainer.Game;

/// <summary>One reference spell: its casting school, Power cost, and effect.</summary>
public sealed record SpellInfo(string School, string Name, string Cost, string Effect);

/// <summary>
/// Transcribed spell reference for Dragon Wars, sourced from the <c>fraterrisus/dragonjars</c>
/// engine (its <c>Lists.java</c> spell tables) and the hitchhikerprod magic walkthrough. Grouped
/// by magic school for the References tab. Cost "var." means the caster may invest Power up to
/// their skill rank in that school. Reference only — this drives no memory writes.
/// </summary>
public static class SpellBook
{
    public static readonly IReadOnlyList<SpellInfo> Spells = new SpellInfo[]
    {
        // --- Low Magic ---
        new("Low Magic", "Mage Fire", "2", "1d8 fire damage to one target at 30'."),
        new("Low Magic", "Lesser Heal", "2", "Heals 1d4 HP to one target."),
        new("Low Magic", "Charm", "3", "Heals 1-4 HP and grants +1 AV for the battle."),
        new("Low Magic", "Luck", "3", "Grants +2 DV for the battle."),
        new("Low Magic", "Disarm", "4", "Disarms one target at 30' (if capable)."),
        new("Low Magic", "Mage Light", "var.", "Light source; lasts 3 hours per point invested."),

        // --- High Magic ---
        new("High Magic", "Healing", "3", "Heals 1d6 HP to one target."),
        new("High Magic", "Group Heal", "6", "Heals 1d6 HP to all party members."),
        new("High Magic", "Mystic Might", "4", "Grants +15 STR for the battle."),
        new("High Magic", "Sala's Swift", "8", "Grants +8 DEX for the battle."),
        new("High Magic", "Vorn's Guard", "6", "Grants +2 AC to the party for the battle."),
        new("High Magic", "Cloak Arcane", "var.", "+2 AC to the party; lasts 1 hour per point."),
        new("High Magic", "Fire Light", "var.", "1d6 damage per point to one target at 30'."),
        new("High Magic", "Ice Chill", "var.", "1d4 damage per point to one target at 50'."),
        new("High Magic", "Elvar's Fire", "6", "2d6 damage to an enemy group at 30'."),
        new("High Magic", "Poog's Vortex", "11", "4d6 damage to an enemy group at 20'."),
        new("High Magic", "Big Chill", "15", "4d6 damage to all active enemies at 30'."),
        new("High Magic", "Reveal Glamour", "2", "Dispels illusions in the area at 40'."),
        new("High Magic", "Sense Traps", "var.", "Discovers/ignores traps; 2 hours per point."),
        new("High Magic", "Dazzle", "3", "One target misses their next turn (30')."),
        new("High Magic", "Cowardice", "8", "Causes an enemy group at 60' to flee."),
        new("High Magic", "Air Summon", "var.", "Summons an Air Element (12 HP, 1d10)."),
        new("High Magic", "Water Summon", "var.", "Summons a Water Element (25 HP, 1d20)."),
        new("High Magic", "Earth Summon", "var.", "Summons an Earth Element (15 HP, 1d20)."),
        new("High Magic", "Fire Summon", "var.", "Summons a Fire Element (35 HP, 2d20)."),

        // --- Druid Magic ---
        new("Druid Magic", "Greater Healing", "4", "Heals 1d6 HP to one target."),
        new("Druid Magic", "Cure All", "6", "Heals 1d8 HP to all party members."),
        new("Druid Magic", "Scare", "4", "+2 AV to the party; scares Underworld fairies (20')."),
        new("Druid Magic", "Death Curse", "6", "3d6 damage to one target at 40'."),
        new("Druid Magic", "Fire Blast", "12", "4d6 fire damage to an enemy group at 30'."),
        new("Druid Magic", "Insect Plague", "4", "-2 AV and -2 DV to an enemy group at 60'."),
        new("Druid Magic", "Whirl Wind", "4", "Pushes an enemy group back 30 feet (40')."),
        new("Druid Magic", "Brambles", "5", "An enemy group at 60' misses their next turn."),
        new("Druid Magic", "Create Wall", "5", "Repairs the Yellow Mud Toad temple."),
        new("Druid Magic", "Soften Stone", "6", "Dissolves stone blockades to bypass obstacles."),
        new("Druid Magic", "Beast Call", "var.", "Summons a Beast (13 HP, 1d12)."),
        new("Druid Magic", "Wood Spirit", "var.", "Summons a Wood Spirit (19 HP, 1d12)."),
        new("Druid Magic", "Invoke Spirit", "var.", "Summons a Spirit (13 HP, 3d10)."),

        // --- Sun Magic ---
        new("Sun Magic", "Sun Light", "3", "Heals 1d6 HP to one target."),
        new("Sun Magic", "Heal", "4", "Heals 1d8 HP to one target."),
        new("Sun Magic", "Major Healing", "6", "Heals 1d6 HP to all party members."),
        new("Sun Magic", "Holy Aim", "5", "+2 AV to the party for combat."),
        new("Sun Magic", "Battle Power", "8", "+10 STR to the party for combat."),
        new("Sun Magic", "Mithras' Bless", "5", "+3 DV to the party for combat."),
        new("Sun Magic", "Armor of Light", "6", "+2 DV to one target for combat."),
        new("Sun Magic", "Sun Stroke", "var.", "1d8 damage per point to one target at 20'."),
        new("Sun Magic", "Rage of Mithras", "var.", "1d6 damage per point to one target at 70'."),
        new("Sun Magic", "Exorcism", "5", "6d6 damage to undead in a group at 50'."),
        new("Sun Magic", "Inferno", "var.", "1d4 damage per point to all enemies at 40'."),
        new("Sun Magic", "Wrath of Mithras", "var.", "1d4 damage per point to a group at 90'."),
        new("Sun Magic", "Fire Storm", "20", "6d6 damage to all active enemies at 60'."),
        new("Sun Magic", "Charger", "8", "Adds 1 charge to a magic item in inventory."),
        new("Sun Magic", "Disarm Trap", "var.", "Ignores traps; lasts 2 hours per point."),
        new("Sun Magic", "Guidance", "var.", "Enables the compass UI; 3 hours per point."),
        new("Sun Magic", "Radiance", "var.", "Illuminates surroundings; 2 hours per point."),
        new("Sun Magic", "Column of Fire", "5", "An enemy group at 40' can't advance for a turn."),
        new("Sun Magic", "Summon Salamander", "var.", "Summons a Salamander (23 HP, 1d10)."),

        // --- Miscellaneous ---
        new("Miscellaneous", "Zak's Speed", "10", "+15 DEX to the party for combat."),
        new("Miscellaneous", "Kill Ray", "15", "10d8 damage to one target at 50'."),
    };
}
