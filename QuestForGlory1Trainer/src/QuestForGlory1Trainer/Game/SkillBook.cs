namespace QuestForGlory1Trainer.Game;

/// <summary>One character stat / skill entry for display in the Reference tab.</summary>
public sealed class SkillInfo
{
    /// <summary>Property index within the SCI0 Ego object's property array.</summary>
    public int Id { get; }

    /// <summary>Stat name as shown in the in-game stats screen.</summary>
    public string Name { get; }

    /// <summary>Valid numeric range and brief note on what the stat governs.</summary>
    public string Notes { get; }

    /// <summary>Whether this stat is available to all classes (false = class-specific).</summary>
    public bool AllClasses { get; }

    public SkillInfo(int id, string name, string notes, bool allClasses = true)
    {
        Id = id;
        Name = name;
        Notes = notes;
        AllClasses = allClasses;
    }
}

/// <summary>Reference table of all character stats in Quest for Glory I.</summary>
public static class SkillBook
{
    public static readonly IReadOnlyList<SkillInfo> Stats = new[]
    {
        new SkillInfo( 0, "Strength",      "1–200. Melee damage; maximum carry weight."),
        new SkillInfo( 1, "Intelligence",  "1–200. Spell learning speed; puzzle hints."),
        new SkillInfo( 2, "Agility",       "1–200. Hit chance; dodge; Pick Locks."),
        new SkillInfo( 3, "Vitality",      "1–200. Max Health and Stamina pools."),
        new SkillInfo( 4, "Luck",          "1–200. Random event outcomes; treasure quality."),
        new SkillInfo( 5, "Weapon Use",    "0–200. Melee accuracy; improves with combat practice."),
        new SkillInfo( 6, "Parry",         "0–200. Chance to block incoming attacks."),
        new SkillInfo( 7, "Dodge",         "0–200. Chance to avoid missile/area attacks."),
        new SkillInfo( 8, "Stealth",       "0–200. Moving silently past creatures and NPCs."),
        new SkillInfo( 9, "Pick Locks",    "0–200. Opens locked doors and chests. Thief emphasis.", allClasses: false),
        new SkillInfo(10, "Throwing",      "0–200. Accuracy and range with thrown weapons."),
        new SkillInfo(11, "Climbing",      "0–200. Scaling walls, trees, and cliff faces."),
        new SkillInfo(12, "Magic",         "0–200. Mana pool size; spell casting accuracy. Magic-user emphasis.", allClasses: false),
        new SkillInfo(13, "Experience",    "0–∞. Accumulated XP; no direct cap."),
        new SkillInfo(14, "Health (cur)",  "0–max. Current HP. Restore via potions, rest, or Erana's Peace."),
        new SkillInfo(15, "Stamina (cur)", "0–max. Current stamina. Depletes in combat and travel."),
        new SkillInfo(16, "Mana (cur)",    "0–max. Current magic points. Depletes when casting spells."),
    };
}
