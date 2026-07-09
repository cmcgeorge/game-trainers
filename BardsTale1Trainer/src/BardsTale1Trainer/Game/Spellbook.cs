namespace BardsTale1Trainer.Game;

/// <summary>The four magic arts. Values match the record's spell-level byte order
/// (offset 0x41..0x44) and the game's own spell-table order.</summary>
public enum SpellClass
{
    Magician = 0,
    Conjurer = 1,
    Sorcerer = 2,
    Wizard = 3,
    None = -1,
}

/// <summary>One castable spell: its art, spell level (1–7), the 4-letter code typed
/// in-game, and the full name. Codes and names were extracted verbatim from the
/// running BARD.EXE's data segment.</summary>
public sealed record Spell(SpellClass Class, int Level, string Code, string Name)
{
    public string Display => $"{Code} — {Name}";
}

/// <summary>All 79 Bard's Tale 1 spells, plus the class→art mapping.</summary>
public static class Spellbook
{
    public static readonly IReadOnlyList<Spell> All = Build();

    /// <summary>Spells of one art, in level order (the game's own ordering).</summary>
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
        SpellClass.Magician => "Magician",
        SpellClass.Conjurer => "Conjurer",
        SpellClass.Sorcerer => "Sorcerer",
        SpellClass.Wizard => "Wizard",
        _ => "(none)",
    };

    private static List<Spell> Build()
    {
        var list = new List<Spell>(79);

        void Add(SpellClass c, int level, params (string code, string name)[] spells)
        {
            foreach (var (code, name) in spells)
                list.Add(new Spell(c, level, code, name));
        }

        // Magician — mental & wind magic
        Add(SpellClass.Magician, 1, ("MIJA", "MIND JAB"), ("PHBL", "PHASE BLUR"), ("LOTR", "LOCATE TRAPS"), ("HYIM", "HYPNOTIC IMAGE"));
        Add(SpellClass.Magician, 2, ("DISB", "DISBELIEVE"), ("TADU", "TARGET-DUMMY"), ("MIFI", "MIND FIST"), ("FEAR", "WORD OF FEAR"));
        Add(SpellClass.Magician, 3, ("WIWO", "WIND WOLF"), ("VANI", "VANISHING SPELL"), ("SESI", "SECOND SIGHT"), ("CURS", "CURSE"));
        Add(SpellClass.Magician, 4, ("CAEY", "CAT EYES"), ("WIWA", "WIND WARRIOR"), ("INVI", "INVISIBILITY"));
        Add(SpellClass.Magician, 5, ("WIOG", "WIND OGRE"), ("DIIL", "DISRUPT ILL."), ("MIBL", "MIND BLADE"));
        Add(SpellClass.Magician, 6, ("WIDR", "WIND DRAGON"), ("MIWP", "MIND WARP"));
        Add(SpellClass.Magician, 7, ("WIGI", "WINDGIANT"), ("SOSI", "SORCERER SIGHT"));

        // Conjurer — creation & healing magic
        Add(SpellClass.Conjurer, 1, ("MAFL", "MAGE FLAME"), ("ARFI", "ARC FIRE"), ("SOSH", "SORCERER SHIELD"), ("TRZP", "TRAP ZAP"));
        Add(SpellClass.Conjurer, 2, ("FRFO", "FREEZE FOES"), ("MACO", "MAGIC COMPASS"), ("BASK", "BATTLESKILL"), ("WOHL", "WORD OF HEALING"));
        Add(SpellClass.Conjurer, 3, ("MAST", "MAGESTAR"), ("LERE", "LESSER REV."), ("LEVI", "LEVITATION"), ("WAST", "WARSTRIKE"));
        Add(SpellClass.Conjurer, 4, ("INWO", "INSTANT WOLF"), ("FLRE", "FLESH RESTORE"), ("POST", "POISON STRIKE"));
        Add(SpellClass.Conjurer, 5, ("GRRE", "GREATER REV."), ("WROV", "WRATH OF VAL."), ("SHSP", "SHOCK-SPHERE"));
        Add(SpellClass.Conjurer, 6, ("INOG", "INSTANT OGRE"), ("MALE", "MAJOR LEV."));
        Add(SpellClass.Conjurer, 7, ("FLAN", "FLESH ANEW"), ("APAR", "APPORT ARCANE"));

        // Sorcerer — illusion & enchantment magic
        Add(SpellClass.Sorcerer, 1, ("VOPL", "VORPAL PLATING"), ("AIAR", "AIR ARMOR"), ("STLI", "STEELIGHT SPELL"), ("SCSI", "SCRY SITE"));
        Add(SpellClass.Sorcerer, 2, ("HOWA", "HOLY WATER"), ("WIST", "WITHER STRIKE"), ("MAGA", "MAGE GAUNTLETS"), ("AREN", "AREA ENCHANT"));
        Add(SpellClass.Sorcerer, 3, ("MYSH", "MYSTIC SHIELD"), ("OGST", "OGRESTRENGTH"), ("MIMI", "MITHRIL MIGHT"), ("STFL", "STARFLARE"));
        Add(SpellClass.Sorcerer, 4, ("SPTO", "SPECTRE TOUCH"), ("DRBR", "DRAGON BREATH"), ("STSI", "STONELIGHT"));
        Add(SpellClass.Sorcerer, 5, ("ANMA", "ANTI-MAGIC"), ("ANSW", "ANIMATED SWORD"), ("STTO", "STONE TOUCH"));
        Add(SpellClass.Sorcerer, 6, ("PHDO", "PHASE DOOR"), ("YMCA", "MYSTICAL ARMOR"));
        Add(SpellClass.Sorcerer, 7, ("REST", "RESTORATION"), ("DEST", "DEATHSTRIKE"));

        // Wizard — summoning & necromancy
        Add(SpellClass.Wizard, 1, ("SUDE", "SUMMON DEAD"), ("REDE", "REPEL DEAD"));
        Add(SpellClass.Wizard, 2, ("LESU", "LESSER SUMMON"), ("DEBA", "DEMON BANE"));
        Add(SpellClass.Wizard, 3, ("SUPH", "SUMMON PHANTOM"), ("DISP", "DISPOSSESS"));
        Add(SpellClass.Wizard, 4, ("PRSU", "PRIME SUMMONING"), ("ANDE", "ANIMATE DEAD"));
        Add(SpellClass.Wizard, 5, ("SPBI", "SPELL BIND"), ("DMST", "DEMON STRIKE"));
        Add(SpellClass.Wizard, 6, ("SPSP", "SPELL SPIRIT"), ("BEDE", "BEYOND DEATH"));
        Add(SpellClass.Wizard, 7, ("GRSU", "GREATER SUMMON"));

        return list;
    }

    /// <summary>The six bard songs, in the game's play order (Play tune # 1–6).</summary>
    public static readonly string[] BardSongs =
    {
        "Falkens Fury", "Seekers Ballad", "Waylands Watch",
        "Badhr Kilnfest", "Traveller tune", "Lucklaran",
    };
}
