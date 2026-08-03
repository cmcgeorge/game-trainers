namespace DarkDesigns1Trainer.Game;

/// <summary>
/// The 43 monsters of Dark Designs I, transcribed from the unpacked EXE strings.
/// </summary>
public static class MonsterBook
{
    public sealed record Monster(string Name, string Notes);

    public static readonly Monster[] All =
    {
        new("Kobold",          "Weak humanoid"),
        new("Kobold Leader",   "Slightly stronger kobold"),
        new("Kobold Priest",   "Kobold spellcaster"),
        new("Orc Chief",       "Mid-tier humanoid"),
        new("Goblin",          "Weak humanoid"),
        new("Wolf",            "Animal"),
        new("Skeleton",        "Undead — vulnerable to Dispel Undead"),
        new("Zombie",          "Undead — vulnerable to Dispel Undead"),
        new("Ghost",           "Undead — may require magic to hit"),
        new("Mummy",           "Undead — mid-tier"),
        new("Lich",            "Undead spellcaster"),
        new("Lich Fighter",    "Undead melee fighter"),
        new("Pixie",           "Minor fey"),
        new("Bugbear",         "Goblinoid brute"),
        new("Lizard Man",      "Humanoid reptile"),
        new("Manticore",       "Monstrous beast"),
        new("Minotaur",        "Large humanoid bull"),
        new("Ogre",            "Large brute"),
        new("Ogre Mage",       "Ogre spellcaster"),
        new("Troll",           "Regenerates; dangerous in groups"),
        new("Evil Fighter",    "Humanoid enemy"),
        new("Evil Cleric",     "Enemy priest"),
        new("Evil Mage",       "Enemy wizard"),
        new("Gargoyle",        "Stone creature"),
        new("Iron Gargoyle",   "Tougher gargoyle"),
        new("Ettin",           "Two-headed giant"),
        new("Giant",           "Very large humanoid"),
        new("Golem",           "Constructed creature — unharmed by many attacks"),
        new("Basilisk",        "Petrifying gaze"),
        new("Medusa",          "Stone gaze — can petrify characters"),
        new("Evil Unicorn",    "Corrupted unicorn"),
        new("Fire Elemental",  "Extra-planar fire creature"),
        new("Air Elemental",   "Extra-planar air creature"),
        new("Water Elemental", "Extra-planar water creature"),
        new("Earth Elemental", "Extra-planar earth creature"),
        new("Quasit",          "Minor demon"),
        new("Hellhound",       "Fire-breathing dog"),
        new("Ice Demon",       "Cold demon"),
        new("Flame Devil",     "Fire devil — hit hard by Banishment"),
        new("Death Knight",    "Undead warrior — very dangerous"),
        new("3 Head Hydra",    "Multi-headed beast"),
        new("Demon Lord",      "Most powerful demon — hit hard by Banishment"),
        new("Chaos Avatar",    "Reflects spells back at the caster"),
    };
}
