namespace MoriaTrainer.Game;

/// <summary>One mage spell or priest prayer (Confirmed from <c>spells.doc</c>, <c>faq</c> Q9/Q10, and manual).</summary>
public sealed record SpellInfo(
    int Index,           // a=1..z, A=27.., etc.; the letter never changes once assigned
    string Letter,       // in-game letter for this spell
    string Name,
    int MinLevel,        // first character level it can be learned at
    int ManaCost,        // base mana cost
    string Realm,        // "Mage" or "Priest"
    string Book,         // which of the four books it appears in
    string Effect,
    string Damage = "")  // for offensive spells, the dice/multiplier
{
    public bool IsOffensive => !string.IsNullOrEmpty(Damage);
}

/// <summary>
/// The 31 mage spells + 31 priest prayers (Confirmed from <c>spells.doc</c> + the FAQ damage table).
/// Letters are stable across the game (a 5.x feature: a spell's letter never changes even if you
/// don't yet know the earlier spells in the book).
/// </summary>
public static class SpellBook
{
    public static readonly IReadOnlyList<SpellInfo> MageSpells = new[]
    {
        new SpellInfo( 1, "a", "Magic Missile",       1,  1, "Mage", "Beginners' Magician's Handbook", "Bolt of force.",                         "2d6"),
        new SpellInfo( 2, "b", "Detect Monsters",     1,  1, "Mage", "Beginners' Magician's Handbook", "Reveals nearby monsters on the map."),
        new SpellInfo( 3, "c", "Phase Door",          1,  2, "Mage", "Beginners' Magician's Handbook", "Short-range teleport."),
        new SpellInfo( 4, "d", "Light Area",          1,  2, "Mage", "Beginners' Magician's Handbook", "Lights the current room/corridor."),
        new SpellInfo( 5, "e", "Detect Treasure",     3,  3, "Mage", "Beginners' Magician's Handbook", "Reveals nearby treasure."),
        new SpellInfo( 6, "f", "Detect Invisible",    3,  3, "Mage", "Beginners' Magician's Handbook", "Reveals invisible creatures."),
        new SpellInfo( 7, "g", "Detect Traps/Doors",  5,  4, "Mage", "Beginners' Magician's Handbook", "Reveals traps, secret doors, and stairs."),
        new SpellInfo( 8, "h", "Stinking Cloud",      5,  4, "Mage", "Beginners' Magician's Handbook", "Poison cloud at a target square.",        "12"),
        new SpellInfo( 9, "i", "Confuse Monster",     7,  6, "Mage", "Trowel-Wielders' Guide",         "Confuses one monster."),
        new SpellInfo(10, "j", "Lightning Bolt",      7,  6, "Mage", "Trowel-Wielders' Guide",         "Bolt of lightning.",                      "4d8"),
        new SpellInfo(11, "k", "Sleep I",             7,  6, "Mage", "Trowel-Wielders' Guide",         "Sleeps one monster in a direction."),
        new SpellInfo(12, "l", "Frost Bolt",          9,  8, "Mage", "Trowel-Wielders' Guide",         "Bolt of cold.",                            "6d8"),
        new SpellInfo(13, "m", "Acid Ball",          11, 10, "Mage", "Trowel-Wielders' Guide",         "Ball of acid.",                            "60"),
        new SpellInfo(14, "n", "Fire Bolt",          13, 11, "Mage", "Trowel-Wielders' Guide",         "Bolt of fire.",                            "9d8"),
        new SpellInfo(15, "o", "Lightning Ball",     15, 14, "Mage", "Trowel-Wielders' Guide",         "Ball of lightning.",                       "32"),
        new SpellInfo(16, "p", "Frost Ball",         17, 16, "Mage", "Trowel-Wielders' Guide",         "Ball of cold.",                            "48"),
        new SpellInfo(17, "q", "Fire Ball",          19, 18, "Mage", "Higher Magicians' Handbook",     "Ball of fire.",                            "72"),
        new SpellInfo(18, "r", "Recharge Item I",    21, 20, "Mage", "Higher Magicians' Handbook",     "Recharges a wand/staff (weaker)."),
        new SpellInfo(19, "s", "Sleep II",           23, 22, "Mage", "Higher Magicians' Handbook",     "Sleeps all monsters adjacent to player."),
        new SpellInfo(20, "t", "Haste Self",         25, 24, "Mage", "Higher Magicians' Handbook",     "+1 speed for a duration."),
        new SpellInfo(21, "u", "Fire Ball (II)",     27, 26, "Mage", "Higher Magicians' Handbook",     "Larger fire ball."),
        new SpellInfo(22, "v", "Recharge Item II",   29, 28, "Mage", "Higher Magicians' Handbook",     "Recharges a wand/staff (more reliable)."),
        new SpellInfo(23, "w", "Word of Destruction",31, 30, "Mage", "Higher Magicians' Handbook",     "Obliterates everything within 15 spaces. Balrog teleports away instead of dying."),
        new SpellInfo(24, "x", "Sleep III",          33, 32, "Mage", "Mages' Companion",              "Sleeps all monsters in line of sight (incl. invisible)."),
        new SpellInfo(25, "y", "Genocide",           35, 34, "Mage", "Mages' Companion",              "Removes all of one creature type from the level (drains you)."),
        new SpellInfo(26, "z", "Detect Enchantment", 37, 36, "Mage", "Mages' Companion",              "Reveals enchanted items on the level."),
        new SpellInfo(27, "A", "Teleport Other",     39, 38, "Mage", "Mages' Companion",              "Teleports a monster away."),
        new SpellInfo(28, "B", "Haste Self (II)",    41, 40, "Mage", "Mages' Companion",              "Longer +1 speed."),
        new SpellInfo(29, "C", "Drain Life",         43, 42, "Mage", "Mages' Companion",              "Direct damage bolt.",                      "75"),
        new SpellInfo(30, "D", "Mass Genocide",      45, 44, "Mage", "Mages' Companion",              "Removes all monsters in line of sight (drains you more)."),
        new SpellInfo(31, "E", "Word of Recall",     47, 46, "Mage", "Mages' Companion",              "Teleports to town from dungeon, or to deepest level from town."),
    };

    public static readonly IReadOnlyList<SpellInfo> PriestPrayers = new[]
    {
        new SpellInfo( 1, "a", "Detect Evil",         1,  2, "Priest", "Beginners' Hymnal",  "Reveals evil creatures nearby."),
        new SpellInfo( 2, "b", "Cure Light Wounds",   1,  2, "Priest", "Beginners' Hymnal",  "Heals a little; cures blindness."),
        new SpellInfo( 3, "c", "Bless",               1,  2, "Priest", "Beginners' Hymnal",  "+2 AC, +5 to-hit for a short time."),
        new SpellInfo( 4, "d", "Remove Fear",         1,  2, "Priest", "Beginners' Hymnal",  "Cures fear."),
        new SpellInfo( 5, "e", "Call Light",          3,  3, "Priest", "Beginners' Hymnal",  "Lights the area."),
        new SpellInfo( 6, "f", "Detect Traps/Doors",  3,  3, "Priest", "Beginners' Hymnal",  "Reveals traps, secret doors, stairs."),
        new SpellInfo( 7, "g", "Slow Poison",         3,  3, "Priest", "Beginners' Hymnal",  "Halves poison damage rate."),
        new SpellInfo( 8, "h", "Blind Creature",      5,  4, "Priest", "Beginners' Hymnal",  "Blinds a monster (it wanders confused)."),
        new SpellInfo( 9, "i", "Portal",              7,  6, "Priest", "Chants/Blessings",   "Medium-range teleport."),
        new SpellInfo(10, "j", "Cure Serious Wounds", 7,  6, "Priest", "Chants/Blessings",   "Heals more; cures blindness/confusion."),
        new SpellInfo(11, "k", "Chant",               9,  8, "Priest", "Chants/Blessings",   "Double-duration Bless."),
        new SpellInfo(12, "l", "Sanctuary",          11, 10, "Priest", "Chants/Blessings",   "Sleeps all monsters adjacent to player."),
        new SpellInfo(13, "m", "Create Food",        13, 11, "Priest", "Chants/Blessings",   "Creates food. Never starve."),
        new SpellInfo(14, "n", "Remove Curse",       15, 14, "Priest", "Chants/Blessings",   "Removes a curse from a worn/wielded item."),
        new SpellInfo(15, "o", "Resist Heat/Cold",   17, 16, "Priest", "Chants/Blessings",   "Temporary resist fire and cold."),
        new SpellInfo(16, "p", "Neutralize Poison",  19, 18, "Priest", "Exorcism/Dispel",    "Cures poison."),
        new SpellInfo(17, "q", "Protection from Evil",21, 20, "Priest", "Exorcism/Dispel",   "Evil creatures ≤ your level cannot attack you."),
        new SpellInfo(18, "r", "Cure Critical Wounds",23, 22, "Priest", "Exorcism/Dispel",  "Big heal; cures blindness/confusion/poison/stun."),
        new SpellInfo(19, "s", "Earthquake",         25, 24, "Priest", "Exorcism/Dispel",    "Random wall collapses nearby; can injure creatures."),
        new SpellInfo(20, "t", "Turn Undead",        27, 26, "Priest", "Exorcism/Dispel",    "Undead ≤ your level flee."),
        new SpellInfo(21, "u", "Prayer",             29, 28, "Priest", "Exorcism/Dispel",    "Quadruple-duration Bless."),
        new SpellInfo(22, "v", "Dispel Undead",      31, 30, "Priest", "Exorcism/Dispel",    "1..(3× your level) damage to all undead in LOS."),
        new SpellInfo(23, "w", "Heal",               33, 32, "Priest", "Exorcism/Dispel",    "Very large heal."),
        new SpellInfo(24, "x", "Dispel Evil",        35, 34, "Priest", "Exorcism/Dispel",    "1..(3× your level) damage to all evil in LOS."),
        new SpellInfo(25, "y", "Glyph of Warding",   37, 36, "Priest", "Holy Words",         "Draws a glyph monsters cannot enter (small break chance)."),
        new SpellInfo(26, "z", "Holy Word",          39, 38, "Priest", "Holy Words",         "Full heal; cures poison/fear; dispels evil (1..4× level). In 5.5.1+ restores stats and 3 turns invulnerable."),
        new SpellInfo(27, "A", "Restore Mana",       41, 40, "Priest", "Holy Words",         "Refills mana to full."),
        new SpellInfo(28, "B", "Restore Life",       43, 42, "Priest", "Holy Words",         "Restores drained experience."),
        new SpellInfo(29, "C", "Resurrection",       45, 44, "Priest", "Holy Words",         "Resurrects a dead companion (single-player: no use)."),
        new SpellInfo(30, "D", "Word of Recall",     47, 46, "Priest", "Holy Words",         "Teleports to town/dungeon as the scroll."),
        new SpellInfo(31, "E", "Invulnerability",    49, 48, "Priest", "Holy Words",         "A few turns of invulnerability."),
    };

    public static IEnumerable<SpellInfo> All => MageSpells.Concat(PriestPrayers);
}
