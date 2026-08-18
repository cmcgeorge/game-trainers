namespace BardsTaleTrilogyTrainer.Game;

/// <summary>The four magic arts in Bard's Tale I, plus the extended classes from BT2/BT3.</summary>
public enum SpellClass
{
    Conjurer = 0,
    Magician = 1,
    Sorcerer = 2,
    Wizard = 3,
    Archmage = 4,
    Chronomancer = 5,
    Geomancer = 6,
    AnyMagicUser = 7,
    None = -1,
}

/// <summary>One castable spell: its art, level, 4-letter code, and full name.</summary>
public sealed record Spell(SpellClass Class, int Level, string Code, string Name)
{
    public string Display => $"{Code} — {Name}";
}

/// <summary>
/// All spells across the Bard's Tale Trilogy, sourced from the community-maintained
/// list at bardstaleonline.com (created by Troy H. Cheek, ripped from the MS-DOS
/// executables). Includes the special "any magic user" spells ZZGO (Dream Spell)
/// and NUKE (Gotterdammerung) that the user specifically requested.
/// </summary>
public static class Spellbook
{
    public static readonly IReadOnlyList<Spell> All = Build();

    public static IEnumerable<Spell> For(SpellClass cls) => All.Where(s => s.Class == cls);

    /// <summary>The art a character class casts from, or None for non-casters.</summary>
    public static SpellClass ArtForClass(int classId) => classId switch
    {
        6 => SpellClass.Conjurer,
        7 => SpellClass.Magician,
        8 => SpellClass.Sorcerer,
        9 => SpellClass.Wizard,
        _ => SpellClass.None,
    };

    public static string ArtName(SpellClass cls) => cls switch
    {
        SpellClass.Conjurer => "Conjurer",
        SpellClass.Magician => "Magician",
        SpellClass.Sorcerer => "Sorcerer",
        SpellClass.Wizard => "Wizard",
        SpellClass.Archmage => "Archmage",
        SpellClass.Chronomancer => "Chronomancer",
        SpellClass.Geomancer => "Geomancer",
        SpellClass.AnyMagicUser => "Any Magic User",
        _ => "(none)",
    };

    /// <summary>Find a spell by its 4-letter code (case-insensitive).</summary>
    public static Spell? FindByCode(string code) =>
        All.FirstOrDefault(s => string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase));

    private static List<Spell> Build()
    {
        var list = new List<Spell>(140);

        void Add(SpellClass c, int level, params (string code, string name)[] spells)
        {
            foreach (var (code, name) in spells)
                list.Add(new Spell(c, level, code, name));
        }

        // Conjurer — creation & healing magic (BT1)
        Add(SpellClass.Conjurer, 1, ("MAFL", "Mage Flame"), ("ARFI", "Arc Fire"), ("SOSH", "Sorcerer Shield"), ("TRZP", "Trap Zap"));
        Add(SpellClass.Conjurer, 2, ("FRFO", "Freeze Foes"), ("MACO", "Kiel's Compass"), ("BASK", "Battleskill"), ("WOHL", "Word of Healing"));
        Add(SpellClass.Conjurer, 3, ("MAST", "Magestar"), ("LERE", "Lesser Revelation"), ("LEVI", "Levitation"), ("WAST", "Warstrike"));
        Add(SpellClass.Conjurer, 4, ("INWO", "Instant Wolf"), ("FLRE", "Flesh Restore"), ("POST", "Poison Strike"));
        Add(SpellClass.Conjurer, 5, ("GRRE", "Greater Revelation"), ("WROV", "Wrath of Valhalla"), ("SHSP", "Shock-Sphere"));
        Add(SpellClass.Conjurer, 6, ("INOG", "Instant Ogre"), ("MALE", "Major Levitation"));
        Add(SpellClass.Conjurer, 7, ("FLAN", "Flesh Anew"), ("APAR", "Apport Arcane"));

        // Magician — mental & wind magic (BT1)
        Add(SpellClass.Magician, 1, ("MIJA", "Mind Jab"), ("PHBL", "Phase Blur"), ("LOTR", "Locate Traps"), ("HYIM", "Hypnotic Image"));
        Add(SpellClass.Magician, 2, ("DISB", "Disbelieve"), ("TADU", "Target-Dummy"), ("MIFI", "Mind Fist"), ("FEAR", "Word of Fear"));
        Add(SpellClass.Magician, 3, ("WIWO", "Wind Wolf"), ("VANI", "Vanishing Spell"), ("SESI", "Second Sight"), ("CURS", "Curse"));
        Add(SpellClass.Magician, 4, ("CAEY", "Cat Eyes"), ("WIWA", "Wind Warrior"), ("INVI", "Invisibility"));
        Add(SpellClass.Magician, 5, ("WIOG", "Wind Ogre"), ("DIIL", "Disrupt Illusion"), ("MIBL", "Mind Blade"));
        Add(SpellClass.Magician, 6, ("WIDR", "Wind Dragon"), ("MIWP", "Mind Warp"));
        Add(SpellClass.Magician, 7, ("WIGI", "Wind Giant"), ("SOSI", "Sorcerer Sight"));

        // Sorcerer — enchantment & combat magic (BT1)
        Add(SpellClass.Sorcerer, 1, ("VOPL", "Vorpal Plating"), ("AIAR", "Air Armor"), ("STLI", "Steelight"), ("SCSI", "Scry Sight"));
        Add(SpellClass.Sorcerer, 2, ("HOWA", "Holy Water"), ("WIST", "Wither Strike"), ("MAGA", "Mage Gauntlets"), ("AREN", "Area Enchant"));
        Add(SpellClass.Sorcerer, 3, ("MYSH", "Mystic Shield"), ("OGST", "Ogre Strength"), ("MIMI", "Mithril Might"), ("STFL", "Starflare"));
        Add(SpellClass.Sorcerer, 4, ("SPTO", "Spectre Touch"), ("DRBR", "Dragon Breath"), ("STSI", "Stonelight"));
        Add(SpellClass.Sorcerer, 5, ("ANMA", "Anti-Magic"), ("ANSW", "Animated Sword"), ("STTO", "Stone Touch"));
        Add(SpellClass.Sorcerer, 6, ("PHDO", "Phase Door"), ("YMCA", "Mystical Armor"));
        Add(SpellClass.Sorcerer, 7, ("REST", "Restoration"), ("DEST", "Deathstrike"));

        // Wizard — summoning & necromancy (BT1)
        Add(SpellClass.Wizard, 1, ("SUDE", "Summon Dead"), ("REDE", "Repel Dead"));
        Add(SpellClass.Wizard, 2, ("LESU", "Lesser Summoning"), ("DEBA", "Demon Bane"));
        Add(SpellClass.Wizard, 3, ("SUPH", "Summon Phantom"), ("DISP", "Dispossess"));
        Add(SpellClass.Wizard, 4, ("PRSU", "Prime Summoning"), ("ANDE", "Animate Dead"));
        Add(SpellClass.Wizard, 5, ("SPBI", "Spell Bind"), ("DMST", "Demon Strike"));
        Add(SpellClass.Wizard, 6, ("SPSP", "Spell Spirit"), ("BEDE", "Beyond Death"));
        Add(SpellClass.Wizard, 7, ("GRSU", "Greater Summoning"));

        // Archmage (BT2)
        Add(SpellClass.Archmage, 1, ("HAFO", "Haltfoe"), ("MEME", "Melee Men"));
        Add(SpellClass.Archmage, 2, ("BASP", "Batchspell"));
        Add(SpellClass.Archmage, 3, ("CAMR", "Camaraderie"));
        Add(SpellClass.Archmage, 4, ("NILA", "Night Lance"));
        Add(SpellClass.Archmage, 5, ("HEAL", "Heal All"));
        Add(SpellClass.Archmage, 6, ("BRKR", "Kringle Bros."));
        Add(SpellClass.Archmage, 7, ("MAMA", "Mangar's Mallet"));

        // Chronomancer (BT3)
        Add(SpellClass.Chronomancer, 1, ("VITL", "Vitality"));
        Add(SpellClass.Chronomancer, 2, ("WIFI", "Witherfist"), ("COLD", "Frost Force"));
        Add(SpellClass.Chronomancer, 3, ("GOFI", "God Fire"), ("STUN", "Stun Force"));
        Add(SpellClass.Chronomancer, 4, ("LUCK", "Luck Chant"), ("FADE", "Far Death"));
        Add(SpellClass.Chronomancer, 5, ("WHAT", "Identify"), ("OLAY", "Youth"));
        Add(SpellClass.Chronomancer, 6, ("GRRO", "Grave Robber"), ("FOTA", "Force of Tarjan"));
        Add(SpellClass.Chronomancer, 7, ("SHSH", "Shadow Shield"), ("FAFI", "Fatal Fist"));

        // Geomancer (BT3)
        Add(SpellClass.Geomancer, 1, ("EADA", "Earth Dagger"), ("EASO", "Earth Song"), ("EAWA", "Earth Ward"));
        Add(SpellClass.Geomancer, 2, ("TREB", "Trebuchet"), ("EAEL", "Earth Elemental"), ("WAWA", "Wall Warp"));
        Add(SpellClass.Geomancer, 3, ("ROCK", "Petrify"), ("ROAL", "Roscoe's Alert"));
        Add(SpellClass.Geomancer, 4, ("SUSO", "Succor Song"), ("SAST", "Sandstorm"));
        Add(SpellClass.Geomancer, 5, ("SANT", "Sanctuary"), ("GLST", "Glacier Strike"));
        Add(SpellClass.Geomancer, 6, ("PATH", "Pathfinder"), ("MABA", "Magma Blast"));
        Add(SpellClass.Geomancer, 7, ("JOBO", "Jolt Bolt"), ("EAMA", "Earth Maw"));

        // Any Magic User — special cross-game spells (BT2/BT3)
        Add(SpellClass.AnyMagicUser, 0, ("GILL", "Gilles Gills"));
        Add(SpellClass.AnyMagicUser, 0, ("DIVA", "Divine Intervention"));
        Add(SpellClass.AnyMagicUser, 0, ("ZZGO", "Dream Spell"));
        Add(SpellClass.AnyMagicUser, 0, ("NUKE", "Gotterdammerung"));

        return list;
    }

    /// <summary>The six bard songs (BT1).</summary>
    public static readonly string[] BardSongs =
    {
        "Falkentyne's Fury", "Seeker's Ballad", "Wayland's Watch",
        "Badh'r Kilnfest", "The Traveller's Tune", "Lucklaran",
    };
}
