namespace BardsTale1Trainer.Game;

/// <summary>One character class for the reference tab.</summary>
public sealed record ClassInfo(int Id, string Name, string Tag, string Description);

/// <summary>One race for the reference tab.</summary>
public sealed record RaceInfo(int Id, string Name, string Description);

/// <summary>
/// Reference text for the ten classes and seven races. Ids match the record's
/// class/race bytes (and the game's own table order). Descriptions are summarised
/// from the Bard's Tale manual.
/// </summary>
public static class ClassBook
{
    public static readonly IReadOnlyList<ClassInfo> Classes = new[]
    {
        new ClassInfo(0, "Warrior", "fighter",
            "The basic fighter. Can use most weapons; gains an extra attack every 4 levels."),
        new ClassInfo(1, "Paladin", "fighter",
            "An honourable fighter with improved saving throws; can wield some of the best weapons in the game."),
        new ClassInfo(2, "Rogue", "stealth",
            "Hides in shadows and — critically — identifies and disarms trapped chests. Weak in a stand-up fight."),
        new ClassInfo(3, "Bard", "hybrid",
            "A fighter who plays magic songs (6 tunes) with bardic instruments; needs a drink now and then to keep singing."),
        new ClassInfo(4, "Hunter", "fighter",
            "An assassin-type fighter whose attacks can instantly kill (critical hit) as he levels."),
        new ClassInfo(5, "Monk", "fighter",
            "A martial artist who fights better bare-handed and unarmoured as he advances. Excellent AC over time."),
        new ClassInfo(6, "Conjurer", "caster",
            "Creation & healing magic (MAGE FLAME, WORD OF HEALING, FLESH ANEW). One of the two starting mage classes."),
        new ClassInfo(7, "Magician", "caster",
            "Mental & wind magic (MIND JAB, WIND OGRE…). The other starting mage class."),
        new ClassInfo(8, "Sorcerer", "caster",
            "Illusion and perception magic (MYSTIC SHIELD, PHASE DOOR…). Reached by changing class after mastering ~3 spell levels."),
        new ClassInfo(9, "Wizard", "caster",
            "Summoning and binding of supernatural creatures (SUMMON PHANTOM, GREATER SUMMON). The final art."),
    };

    public static readonly IReadOnlyList<RaceInfo> Races = new[]
    {
        new RaceInfo(0, "Human", "No bonuses, no penalties — the all-rounder."),
        new RaceInfo(1, "Elf", "High IQ; favoured for spellcasters. Slightly fragile."),
        new RaceInfo(2, "Dwarf", "High strength and constitution — a natural front-line fighter."),
        new RaceInfo(3, "Hobbit", "Very high dexterity and luck; the classic rogue."),
        new RaceInfo(4, "Half-Elf", "A compromise between human flexibility and elven IQ."),
        new RaceInfo(5, "Half-Orc", "Strong and tough but dim; good warrior material."),
        new RaceInfo(6, "Gnome", "High IQ and luck; an excellent conjurer or magician."),
    };
}
