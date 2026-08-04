namespace Wizardry1Trainer.Game;

/// <summary>
/// The 50 spells of Wizardry 1 (21 mage + 29 priest). The SPELLSKN bitfield at character
/// record offset $8A packs one bit per spell (indices 0..49); the ordering is mage-first,
/// grouped by level, then alphabetical within each level (the game's internal spell-ID
/// order confirmed against the Pascal source and the Wizardry Wiki spell list).
/// </summary>
public static class SpellBook
{
    public sealed record SpellInfo(int Index, string Name, string School, int Level, string Effect);

    // --- mage spells (indices 0..20) ----------------------------------------
    // Level 1 (4 spells)
    private static readonly (string name, string effect)[] MageL1 =
    {
        ("Dumapic", "Shows the party's exact coordinates (x, y) and depth."),
        ("Halito", "Fireball dealing 1-8 fire damage to a single target."),
        ("Katino", "Puts one enemy group to sleep; sleeping targets take double damage."),
        ("Mogref", "Lowers the caster's armor class by 2 for one battle."),
    };
    // Level 2 (2 spells)
    private static readonly (string name, string effect)[] MageL2 =
    {
        ("Dilto", "Envelops an enemy group in darkness, raising their AC (easier to hit)."),
        ("Sopic", "Invisibility; lowers the caster's AC by 4 for one battle."),
    };
    // Level 3 (2 spells)
    private static readonly (string name, string effect)[] MageL3 =
    {
        ("Mahalito", "Firestorm dealing 4-24 damage to each member of an enemy group."),
        ("Molito", "Energy blast dealing 3-18 damage to each member of an enemy group."),
    };
    // Level 4 (3 spells)
    private static readonly (string name, string effect)[] MageL4 =
    {
        ("Dalto", "Blizzard dealing 6-36 freezing damage to each member of an enemy group."),
        ("Lahalito", "Inferno dealing 6-36 fire damage to each member of an enemy group."),
        ("Morlis", "Lesser fear; may cause an enemy group to flee in terror."),
    };
    // Level 5 (3 spells)
    private static readonly (string name, string effect)[] MageL5 =
    {
        ("Madalto", "Arctic frost dealing 8-64 freezing damage to each member of an enemy group."),
        ("Makanito", "Creates a vacuum that suffocates enemies with < 35-40 HP, killing them."),
        ("Mamorlis", "Greater fear; may cause all opponents to flee."),
    };
    // Level 6 (4 spells)
    private static readonly (string name, string effect)[] MageL6 =
    {
        ("Haman", "Lesser divine intervention; random powerful effect, costs one experience level."),
        ("Lakanito", "Suffocates all enemies in a group, killing them instantly."),
        ("Masopic", "Lowers the entire party's AC by 4 for one battle."),
        ("Zilwan", "Instantly destroys one undead monster."),
    };
    // Level 7 (3 spells)
    private static readonly (string name, string effect)[] MageL7 =
    {
        ("Mahaman", "Greater divine intervention; powerful random effect, costs one experience level."),
        ("Malor", "Teleports the party to a designated coordinate; errors can be fatal."),
        ("Tiltowait", "Ultimate attack; deals 10-100 damage to all enemies."),
    };

    // --- priest spells (indices 21..49) -------------------------------------
    // Level 1 (5 spells)
    private static readonly (string name, string effect)[] PriestL1 =
    {
        ("Badios", "Inflicts 1-8 HP damage to a single target."),
        ("Dios", "Heals 1-8 HP for a single target."),
        ("Kalki", "Lowers the entire party's AC by 1 for one battle."),
        ("Milwa", "Lesser light; illuminates the dungeon for a short time."),
        ("Porfic", "Lowers the caster's AC by 4 for one battle."),
    };
    // Level 2 (4 spells)
    private static readonly (string name, string effect)[] PriestL2 =
    {
        ("Calfo", "Identifies the trap type on a treasure chest (not always accurate)."),
        ("Manifo", "Paralyzes an enemy group; paralyzed targets take double damage."),
        ("Matu", "Lowers the entire party's AC by 2 for one battle."),
        ("Montino", "Silences an enemy group; silenced targets cannot cast spells."),
    };
    // Level 3 (4 spells)
    private static readonly (string name, string effect)[] PriestL3 =
    {
        ("Bamatu", "Lowers the entire party's AC by 4 for one battle."),
        ("Dialko", "Cures paralysis for a single party member."),
        ("Latumapic", "Permanently identifies all opponents until exiting the dungeon."),
        ("Lomilwa", "Greater light; illuminates the dungeon until exiting or entering a dark zone."),
    };
    // Level 4 (4 spells)
    private static readonly (string name, string effect)[] PriestL4 =
    {
        ("Badial", "Inflicts 2-16 HP damage to a single target."),
        ("Dial", "Heals 2-16 HP for a single target."),
        ("Latumofis", "Neutralizes poison status."),
        ("Maporfic", "Lowers the entire party's AC by 2 while in the dungeon."),
    };
    // Level 5 (6 spells)
    private static readonly (string name, string effect)[] PriestL5 =
    {
        ("Badi", "Death magic; kills a single target instantly."),
        ("Badialma", "Inflicts 3-24 HP damage to a single target."),
        ("Di", "Revives a fallen comrade with 1 HP (chance of failure = ashes)."),
        ("Dialma", "Heals 3-24 HP for a single target."),
        ("Kandi", "Gives the location of missing or dead party members."),
        ("Litokan", "Pillar of flame dealing 3-24 fire damage to each member of an enemy group."),
    };
    // Level 6 (4 spells)
    private static readonly (string name, string effect)[] PriestL6 =
    {
        ("Loktofeit", "Teleports the party out of the dungeon, forfeiting items and most gold."),
        ("Lorto", "Ripping blades dealing 6-36 damage to each member of an enemy group."),
        ("Mabadi", "Reduces a single target to 1-8 HP."),
        ("Madi", "Fully restores HP and removes all status ailments except death."),
    };
    // Level 7 (2 spells)
    private static readonly (string name, string effect)[] PriestL7 =
    {
        ("Kadorto", "Greater life magic; revives a comrade with full HP (failure = lost forever)."),
        ("Malikto", "Devastating force dealing 12-72 damage to all enemy groups."),
    };

    // --- build the full list ------------------------------------------------
    private static readonly (string name, string effect)[][] MageByLevel =
        { MageL1, MageL2, MageL3, MageL4, MageL5, MageL6, MageL7 };
    private static readonly (string name, string effect)[][] PriestByLevel =
        { PriestL1, PriestL2, PriestL3, PriestL4, PriestL5, PriestL6, PriestL7 };

    public static IReadOnlyList<SpellInfo> Spells { get; }
    public static IReadOnlyList<SpellInfo> MageSpells { get; }
    public static IReadOnlyList<SpellInfo> PriestSpells { get; }

    static SpellBook()
    {
        var all = new List<SpellInfo>();
        var mage = new List<SpellInfo>();
        var priest = new List<SpellInfo>();
        int idx = 0;

        for (int lvl = 0; lvl < 7; lvl++)
        {
            foreach (var (name, effect) in MageByLevel[lvl])
            {
                var info = new SpellInfo(idx++, name, "Mage", lvl + 1, effect);
                all.Add(info);
                mage.Add(info);
            }
        }
        for (int lvl = 0; lvl < 7; lvl++)
        {
            foreach (var (name, effect) in PriestByLevel[lvl])
            {
                var info = new SpellInfo(idx++, name, "Priest", lvl + 1, effect);
                all.Add(info);
                priest.Add(info);
            }
        }

        Spells = all;
        MageSpells = mage;
        PriestSpells = priest;
    }

    public static string SpellName(int index) =>
        index >= 0 && index < Spells.Count ? Spells[index].Name : $"?({index})";
}
