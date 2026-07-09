namespace DragonWarsTrainer.Game;

/// <summary>One reference skill: its roster index, name, and what it does.</summary>
public sealed record SkillInfo(int Index, string Name, string Description);

/// <summary>
/// Descriptions for the 27 Dragon Wars skills (roster offsets 0x20..0x3A), transcribed from the
/// hitchhikerprod walkthrough. Names mirror <see cref="RosterFormat.SkillNames"/>; this adds the
/// reference blurb shown on the References tab.
/// </summary>
public static class SkillBook
{
    private static readonly string[] Descriptions =
    {
        "Identify magical objects, decode glyphs, and operate the travel Nexus teleporters.",
        "Navigate caves and survive underground environmental hazards.",
        "Tactical advantages and survival checks inside heavily forested terrain.",
        "Checked when scaling cliffs or navigating treacherous mountain ridges.",
        "Discovers urban secrets, rumors, and historical flavor inside cities.",
        "Halt bleeding and heal wounded party members after combat.",
        "Ascend steep walls, escape from holes, and cross structural gaps.",
        "Modifies damage and hit chance when attacking unarmed.",
        "Avoid random encounters, sneak past guards, or escape prisons.",
        "Disarm trapped doors and open locked chests.",
        "Steal gold, keys, or quest items from NPCs.",
        "Prevents drowning when crossing bays, canals, or rivers.",
        "Follow trails or find hidden paths in the wilderness.",
        "Navigate the complex Lansk government systems.",
        "Unlocks casting from the Druid spell library.",
        "Unlocks casting from the High Magic spell library.",
        "Unlocks casting from the Low Magic spell library.",
        "Easter egg — checked in minor scripts, but cannot be bought.",
        "Unlocks casting from the Sun Magic spell library.",
        "Weapon proficiency for axes (battleaxes, war axes, picks).",
        "Weapon proficiency for flails (spiked, runed).",
        "Weapon proficiency for blunt weapons (maces, hammers, staves).",
        "Weapon proficiency for bladed weapons (daggers, swords).",
        "Weapon proficiency for heavy two-handed weapons.",
        "Ranged proficiency for traditional bows.",
        "Ranged proficiency for mechanical crossbows.",
        "Ranged proficiency for thrown spears, daggers, or tridents.",
    };

    public static readonly IReadOnlyList<SkillInfo> Skills = BuildSkills();

    private static IReadOnlyList<SkillInfo> BuildSkills()
    {
        var list = new List<SkillInfo>(RosterFormat.SkillCount);
        for (int i = 0; i < RosterFormat.SkillCount; i++)
        {
            string desc = i < Descriptions.Length ? Descriptions[i] : "";
            list.Add(new SkillInfo(i, RosterFormat.SkillNames[i], desc));
        }
        return list;
    }
}
