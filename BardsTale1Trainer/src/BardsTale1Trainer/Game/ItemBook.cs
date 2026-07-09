namespace BardsTale1Trainer.Game;

/// <summary>
/// The game's 126-entry item name table, extracted verbatim from the running
/// BARD.EXE's data segment (DS:0x808, "Torch\0Lamp\0…"). Inventory slot words use a
/// 1-based index into this list (0 = empty slot); bit 15 of the word is the
/// "equipped" flag.
/// </summary>
public static class ItemBook
{
    /// <summary>Item names in game order; index 0 is item id 1 ("Torch").</summary>
    public static readonly string[] ItemNames =
    {
        "Torch",          // 1
        "Lamp",           // 2
        "Broadsword",     // 3
        "Short Sword",    // 4
        "Dagger",         // 5
        "War Axe",        // 6
        "Halbard",        // 7
        "Mace",           // 8
        "Staff",          // 9
        "Buckler",        // 10
        "Tower Shield",   // 11
        "Leather Armor",  // 12
        "Chain Mail",     // 13
        "Scale Armor",    // 14
        "Plate Armor",    // 15
        "Robes",          // 16
        "Helm",           // 17
        "Leather Glvs.",  // 18
        "Gauntlets",      // 19
        "Mandolin",       // 20
        "Harp",           // 21
        "Flute",          // 22
        "Mthr Sword",     // 23
        "Mthr Shield",    // 24
        "Mthr Chain",     // 25
        "Mthr Scale",     // 26
        "Samurai Fgn",    // 27
        "Bracers [6]",    // 28
        "Bardsword",      // 29
        "Fire Horn",      // 30
        "Lightwand",      // 31
        "Mthr Dagger",    // 32
        "Mthr Helm",      // 33
        "Mthr Gloves",    // 34
        "Mthr Axe",       // 35
        "Mthr Mace",      // 36
        "Mthr Plate",     // 37
        "Ogre Fgn",       // 38
        "Lak's Lyre",     // 39
        "Shield Ring",    // 40
        "Dork Ring",      // 41
        "Fin's Flute",    // 42
        "Kael's Axe",     // 43
        "Blood Axe",      // 44
        "Dayblade",       // 45
        "Shield Staff",   // 46
        "Elf Cloak",      // 47
        "Hawkblade",      // 48
        "Admt Sword",     // 49
        "Admt Shield",    // 50
        "Admt Dagger",    // 51
        "Admt Helm",      // 52
        "Admt Gloves",    // 53
        "Admt Mace",      // 54
        "Broom",          // 55
        "Pureblade",      // 56
        "Exorwand",       // 57
        "Ali's Carpet",   // 58
        "Magic Mouth",    // 59
        "Luckshield",     // 60
        "Giant Fgn",      // 61
        "Admt Chain",     // 62
        "Admt Scale",     // 63
        "Admt Plate",     // 64
        "Bracers [4]",    // 65
        "Arcshield",      // 66
        "Pure Shield",    // 67
        "Mage Staff",     // 68
        "War Staff",      // 69
        "Thief Dagger",   // 70
        "Soul Mace",      // 71
        "Wither Staff",   // 72
        "Sorcerstaff",    // 73
        "Sword of Pak",   // 74
        "Heal Harp",      // 75
        "Galt's Flute",   // 76
        "Frost Horn",     // 77
        "Dmnd Sword",     // 78
        "Dmnd Shield",    // 79
        "Dmnd Dagger",    // 80
        "Dmnd Helm",      // 81
        "Golem Fgn",      // 82
        "Titan Fgn",      // 83
        "Conjurstaff",    // 84
        "Arc's Hammer",   // 85
        "Staff of Lor",   // 86
        "Powerstaff",     // 87
        "Mournblade",     // 88
        "Dragonshield",   // 89
        "Dmnd Plate",     // 90
        "Wargloves",      // 91
        "Lorehelm",       // 92
        "Dragonwand",     // 93
        "Kiels Compass",  // 94
        "Speedboots",     // 95
        "Flame Horn",     // 96
        "Truthdrum",      // 97
        "Spiritdrum",     // 98
        "Pipes of Pan",   // 99
        "Ring of Power",  // 100
        "Deathring",      // 101
        "Ybarrashield",   // 102
        "Spectre Mace",   // 103
        "Dag Stone",      // 104
        "Arc's Eye",      // 105
        "Ogrewand",       // 106
        "Spirithelm",     // 107
        "Dragon Fgn",     // 108
        "Mage Fgn",       // 109
        "Troll Ring",     // 110
        "Troll Staff",    // 111
        "Onyx Key",       // 112
        "Crystal Sword",  // 113
        "Stoneblade",     // 114
        "Travelhelm",     // 115
        "Death Dagger",   // 116
        "Mongo Fgn",      // 117
        "Lich Fgn",       // 118
        "Eye",            // 119
        "Master Key",     // 120
        "WizWand",        // 121
        "Silvr Square",   // 122
        "Silvr Circle",   // 123
        "Silvr Triang",   // 124
        "Thor Fgn",       // 125
        "Old Man Fgn",    // 126
    };

    public const int MaxItemId = 126;

    /// <summary>Friendly name for a 1-based item id; 0 is the empty slot.</summary>
    public static string ItemName(int id) =>
        id == 0 ? "(empty)"
        : id >= 1 && id <= ItemNames.Length ? ItemNames[id - 1]
        : $"? (id {id})";

    /// <summary>One row of the slot-editor dropdown: id + display name.</summary>
    public sealed record ItemChoice(int Id, string Name)
    {
        public string Display => Id == 0 ? "0 — (empty)" : $"{Id} — {Name}";
    }

    /// <summary>id 0 (empty) followed by all 126 items, shared by every slot dropdown.</summary>
    public static readonly IReadOnlyList<ItemChoice> Choices = BuildChoices();

    private static List<ItemChoice> BuildChoices()
    {
        var list = new List<ItemChoice>(ItemNames.Length + 1) { new(0, "(empty)") };
        for (int i = 0; i < ItemNames.Length; i++)
            list.Add(new ItemChoice(i + 1, ItemNames[i]));
        return list;
    }

    /// <summary>Rough category for the reference tab, inferred from the name.</summary>
    public static string CategoryOf(int id)
    {
        if (id < 1 || id > ItemNames.Length) return "Other";
        string n = ItemNames[id - 1];
        if (n.Contains("Fgn")) return "Figurines";
        if (n.Contains("Sword") || n.Contains("Dagger") || n.Contains("Axe") || n.Contains("Mace")
            || n.Contains("Halbard") || n.Contains("blade") || n.Contains("Blade") || n.Contains("Hammer"))
            return "Weapons";
        if (n.Contains("Staff") || n.Contains("wand") || n.Contains("Wand")) return "Staves & wands";
        if (n.Contains("Shield") || n.Contains("Buckler")) return "Shields";
        if (n.Contains("Armor") || n.Contains("Mail") || n.Contains("Chain") || n.Contains("Scale")
            || n.Contains("Plate") || n.Contains("Robes") || n.Contains("Cloak") || n.Contains("Bracers"))
            return "Armor";
        if (n.Contains("Helm")) return "Helmets";
        if (n.Contains("Glvs") || n.Contains("Gloves") || n.Contains("Gauntlets")) return "Gloves";
        if (n.Contains("Ring")) return "Rings";
        if (n.Contains("Harp") || n.Contains("Flute") || n.Contains("Mandolin") || n.Contains("Horn")
            || n.Contains("Lyre") || n.Contains("drum") || n.Contains("Pipes"))
            return "Instruments";
        return "Other";
    }
}
