namespace DarkDesigns1Trainer.Game;

/// <summary>
/// The 16 spells of Dark Designs I (8 wizard + 8 priest), transcribed from the unpacked EXE
/// strings and the game manual. Each spell costs spell points equal to its slot index + 1
/// (A=1 MP, B=2 MP, … H=8 MP).
/// </summary>
public static class SpellBook
{
    public enum School { Wizard, Priest }

    public sealed record Spell(int Slot, School School, string Name, int GoldCost, string Description);

    public static readonly Spell[] WizardSpells =
    {
        new(0, School.Wizard, "Magic Missile",  50, "Auto-hit bolt; ~short sword damage"),
        new(1, School.Wizard, "Speed",         100, "Raises target's Dexterity for combat"),
        new(2, School.Wizard, "Strength",      150, "Raises target's Strength for extra damage"),
        new(3, School.Wizard, "Stun",          200, "Target stands motionless for several rounds"),
        new(4, School.Wizard, "Lightning Bolt",250, "1-7 damage per caster level, single target"),
        new(5, School.Wizard, "Fireball",      300, "1-5 damage/level, entire enemy column"),
        new(6, School.Wizard, "Flame Strike",  350, "1-4 damage/level, all monsters"),
        new(7, School.Wizard, "Death Ray",     400, "Usually kills one monster outright"),
    };

    public static readonly Spell[] PriestSpells =
    {
        new(0, School.Priest, "Cure Light Wounds",  50, "Heals several Body points (level + Piety)"),
        new(1, School.Priest, "Dispel Undead",     100, "Destroys weaker undead creatures"),
        new(2, School.Priest, "Bless",             150, "25% of attacks warded off (combat duration)"),
        new(3, School.Priest, "Cure Serious Wounds",200, "Heals a few dozen Body points"),
        new(4, School.Priest, "Death's Door",      250, "Revives a KO'd character (0 Body, can act)"),
        new(5, School.Priest, "Banishment",        300, "Damage scales with target's evil alignment"),
        new(6, School.Priest, "Word of Recall",    350, "Teleports the party back to town"),
        new(7, School.Priest, "Cureall",           400, "Restores character to max Body points"),
    };

    public static string SpellLabel(Spell s) => $"{(char)('A' + s.Slot)}: {s.Name}  ({s.GoldCost}g)";
}
