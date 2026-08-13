namespace CurseOfTheAzureBondsTrainer.Game;

/// <summary>One bestiary entry, decoded from the game's own monster archive. Damage is not part of
/// the character record — it comes from whatever weapon the creature is carrying — so it is left
/// out rather than guessed at.</summary>
public sealed record MonsterInfo(
    string Name, int Xp, int Hp, int Ac, int Thac0, int Level, int Movement, string Notes);

/// <summary>
/// The bestiary, decoded from the game's own monster archives rather than transcribed from a guide.
/// Each <c>MON&lt;n&gt;CHA.DAX</c> block unpacks to a complete 422-byte character record in exactly
/// the format the trainer edits (see <see cref="CoabFormat"/>), so every number below is the game's
/// own: the creature's Armor Class, its hit points, and the experience it pays for the kill.
///
/// <para>That one offset table decodes all 81 monster records to self-consistent values is itself
/// part of what pins the layout down — current hit points equal maximum hit points in all 81,
/// status reads "okay" in all 81, and current movement equals base movement in all 81. See
/// <c>docs/reverse-engineering.md</c> §3. The trainer does not depend on this table; the combat
/// panel edits monster records live. It is reference.</para>
///
/// <para>THAC0 is the record's <i>base</i> value. Eleven records — the named NPCs, and the armed
/// humans the engine equips as it builds an encounter — carry an uncomputed current-THAC0 byte on
/// disk, and the Armor Class 10 those same records list is the unarmored baseline their gear is
/// applied to. Everything else is what you actually meet.</para>
///
/// <para><see cref="MonsterInfo.Notes"/> names the modules a creature appears in, which is also how
/// the areas in <see cref="MapBook"/> were identified.</para>
/// </summary>
public static class MonsterBook
{
    public static readonly IReadOnlyList<MonsterInfo> All = new MonsterInfo[]
    {
        new("DRACOLICH", 13200, 66, -6, 7, 17, 24, "Wilderness, Dracandros"),
        new("BEHOLDER", 12900, 75, 0, 7, 17, 3, "Zhentil Keep"),
        new("BIT O' MOANDER", 11500, 140, 0, 7, 20, 6, "Yulash / Moander"),
        new("TYRANTHRAXUS", 5850, 100, -7, 1, 15, 15, "Myth Drannor"),
        new("DARK ELF LORD", 4900, 108, 0, 10, 12, 12, "Zhentil Keep, Dracandros"),
        new("BLACK DRAGON", 4250, 48, 3, 12, 12, 24, "Wilderness, Dracandros"),
        new("DRACANDROS", 2850, 32, 10, 16, 11, 6, "Dracandros"),
        new("MOGION", 2850, 60, 10, 14, 10, 12, "Yulash / Moander"),
        new("GIANT SLUG", 2000, 60, 5, 9, 12, 6, "Yulash / Moander"),
        new("ZHENTRIM CLERIC", 2000, 48, 2, 18, 9, 12, "Yulash / Moander"),
        new("ZHENTRIM MAGE", 2000, 25, 8, 19, 9, 12, "Yulash / Moander"),
        new("EFREETI", 1950, 55, 2, 10, 10, 24, "Dracandros"),
        new("ETTIN", 1950, 70, 3, 10, 10, 12, "Wilderness"),
        new("SHAMBLING MOUND", 1800, 65, 0, 10, 11, 6, "Yulash / Moander"),
        new("NEO-OTYUGH", 1500, 72, 0, 9, 12, 6, "Tilverton"),
        new("HIGH PRIEST", 1350, 60, 10, 14, 10, 12, "Zhentil Keep, Myth Drannor"),
        new("ZHENTRIM FGHTR", 1350, 58, 10, 12, 9, 12, "Yulash / Moander"),
        new("RAKSHASA", 925, 35, -4, 13, 7, 15, "Zhentil Keep, Myth Drannor"),
        new("WYVERN", 925, 42, 3, 12, 9, 24, "Dracandros"),
        new("PRIEST OF BANE", 900, 48, 10, 16, 8, 12, "Myth Drannor"),
        new("HOODED MEDUSA", 850, 42, 0, 13, 6, 9, "Zhentil Keep"),
        new("SALAMANDER", 825, 42, 3, 15, 8, 9, "Dracandros"),
        new("ZHENTIL MAGE", 825, 19, 8, 19, 7, 12, "Zhentil Keep"),
        new("THRI-KREEN", 800, 33, 5, 13, 7, 18, "Myth Drannor"),
        new("OTYUGH", 700, 40, 3, 12, 8, 6, "Tilverton, Zhentil Keep"),
        new("PHASE SPIDER", 700, 35, 5, 13, 6, 6, "Myth Drannor"),
        new("DARK ELF CLERIC", 650, 42, 4, 16, 6, 12, "Dracandros"),
        new("DARK ELF MAGE", 650, 20, 4, 16, 6, 12, "Dracandros"),
        new("ZHENTIL CLERIC", 625, 40, 2, 18, 6, 12, "Zhentil Keep"),
        new("DRAGONBAIT", 550, 50, 5, 16, 7, 12, "Yulash / Moander"),
        new("MANTICORE", 525, 33, 4, 14, 7, 6, "Zhentil Keep"),
        new("TROLL", 525, 36, 4, 13, 7, 12, "Tilverton"),
        new("DISPLACER BEAST", 475, 35, 2, 13, 6, 15, "Wilderness"),
        new("LG VEGEPYGMY", 425, 30, 4, 14, 6, 12, "Yulash / Moander"),
        new("MINOTAUR", 400, 33, 1, 13, 7, 12, "Zhentil Keep"),
        new("ANHKHEG", 390, 40, 2, 12, 8, 12, "Dracandros"),
        new("GRIFFON", 375, 42, 3, 13, 7, 12, "Zhentil Keep"),
        new("RED PLUME", 375, 40, 10, 14, 7, 12, "Yulash / Moander"),
        new("ZHENTIL FIGHTER", 375, 40, 10, 14, 7, 12, "Zhentil Keep"),
        new("AKABAR BEL AKAS", 350, 15, 10, 20, 5, 12, "Dracandros"),
        new("ALIAS", 350, 48, 7, 16, 6, 12, "Yulash / Moander"),
        new("CULTIST", 350, 24, 4, 19, 5, 12, "Yulash / Moander"),
        new("FIRE KNIFE", 350, 26, 1, 19, 6, 12, "Wilderness, Tilverton"),
        new("MARGOYLE", 350, 30, 2, 13, 6, 12, "Myth Drannor"),
        new("THIEF", 350, 24, 2, 19, 6, 12, "Tilverton"),
        new("GIANT SPIDER", 315, 24, 4, 15, 5, 12, "Myth Drannor"),
        new("HELL HOUND", 250, 35, 4, 13, 7, 12, "Myth Drannor"),
        new("DK ELF FIGHTER", 225, 38, 4, 16, 5, 12, "Dracandros"),
        new("FIGHTER", 225, 35, 10, 16, 5, 12, "Wilderness, Myth Drannor"),
        new("KNIGHT", 225, 54, 4, 16, 6, 12, "Tilverton"),
        new("LOOTER", 225, 20, 2, 19, 5, 12, "Yulash / Moander"),
        new("OWL BEAR", 225, 27, 5, 15, 6, 12, "Dracandros"),
        new("RED PLUME", 225, 35, 5, 16, 5, 12, "Wilderness"),
        new("ZHENTIL FIGHTER", 225, 35, 5, 16, 5, 12, "Wilderness"),
        new("MAGE", 205, 12, 8, 20, 4, 12, "Tilverton"),
        new("CULTIST", 150, 18, 4, 20, 3, 12, "Wilderness"),
        new("BUGBEAR", 135, 24, 5, 16, 3, 9, "Wilderness"),
        new("SM VEGEPYGMY", 120, 15, 4, 16, 3, 12, "Yulash / Moander"),
        new("OGRE", 90, 21, 5, 15, 5, 9, "Zhentil Keep"),
        new("WORG", 90, 44, 5, 15, 4, 18, "Wilderness"),
        new("CENTAUR", 85, 28, 4, 15, 4, 18, "Wilderness"),
        new("ROYAL GUARD", 85, 21, 4, 18, 3, 12, "Tilverton"),
        new("CROCODILE", 60, 16, 5, 16, 4, 15, "Wilderness, Tilverton"),
        new("HIPPOGRIFF", 60, 25, 5, 16, 3, 18, "Wilderness"),
        new("LIZARD MAN", 50, 15, 5, 16, 2, 6, "Wilderness"),
        new("BAR PATRON", 35, 16, 5, 19, 2, 12, "Tilverton"),
        new("FIGHTING DOG", 35, 12, 5, 16, 3, 12, "Tilverton"),
        new("MONKEY", 20, 6, 5, 18, 2, 12, "Tilverton"),
        new("CYNTHIA", 0, 40, 2, 1, 9, 12, "Wilderness"),
        new("GRENDEL", 0, 60, 0, 1, 9, 12, "Wilderness"),
        new("RUSTLE", 0, 98, 0, 1, 9, 12, "Wilderness"),
    };

    /// <summary>Filter the bestiary by a case-insensitive substring of the name or notes.</summary>
    public static IEnumerable<MonsterInfo> Search(string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return All;
        term = term.Trim();
        return All.Where(m =>
            m.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            m.Notes.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
