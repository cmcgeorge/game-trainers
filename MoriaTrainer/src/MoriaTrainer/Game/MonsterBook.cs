namespace MoriaTrainer.Game;

/// <summary>
/// One creature in UMoria's <c>c_list[279]</c>. The trainer ships a curated subset of the roster
/// (the early monsters a new player meets, the spoiler monsters a deep-delve player wants to look
/// up, and the Balrog). The live <c>c_list</c> table has 279 entries (Confirmed from the source);
/// a future revision that locates the COFF image base can render all 279 with their live stats.
/// </summary>
public sealed record CreatureInfo(
    int Id,
    string Name,
    string Symbol,    // display character
    int Level,        // first dungeon depth the creature appears on
    int ArmorClass,
    string HitDice,   // e.g. "1d6" or "8d8"
    string Attacks,   // e.g. "claw:1d4, claw:1d4, bite:2d6"
    int Exp,
    string Speed,     // "normal", "fast (+1)", etc.
    string Flags,     // comma-separated: evil, undead, dragon, invisible, breathes fire, ...
    string Recall)    // the recall-paragraph text shown in-game
{
    public bool IsBalrog => Name.Contains("Balrog", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The 279-creature roster is far too large to ship verbatim; this book holds the creatures a
/// player actually looks up: the early-game roster (depths 1-5), the dangerous mid-game casters
/// and dragons, and the endgame uniques including the Balrog. Each entry's recall text mirrors
/// what the in-game <c>/</c> and <c>l</c> commands print from <c>c_recall</c>.
/// </summary>
public static class MonsterBook
{
    public const int BalrogId = 23; // the canonical c_list index for the Balrog of Moria (Confirmed)

    public static readonly IReadOnlyList<CreatureInfo> Creatures = new[]
    {
        // --- early game (town + dungeon levels 1-3) ----------------------------
        new CreatureInfo( 1, "Mouse",          "r", 1,  7, "1d1", "bite:1",        0,  "normal", "animal", "Harmless. Often the first thing you meet."),
        new CreatureInfo( 2, "Kobold",         "k", 1,  6, "1d1", "club:1d3",      2,  "normal", "evil",  "Weak humanoid. Carries a little treasure."),
        new CreatureInfo( 3, "Centipede",      "c", 1,  5, "1d1", "sting:1",       1,  "normal", "animal", "Poisonous sting; weak but can poison a low-CON character."),
        new CreatureInfo( 4, "Jackal",         "h", 1,  6, "1d1", "bite:1d2",      1,  "normal", "animal", "Comes in packs."),
        new CreatureInfo( 5, "Street Urchin",  "p", 0, 10, "1d1", "hit:1",         0,  "normal", "town",  "Town-only. Mobs you for money. No XP for killing in town."),
        new CreatureInfo( 6, "Sneaky Rogue",   "p", 0,  7, "1d4", "hit:1d4",       0,  "normal", "town, evil", "Town-only. Pickpockets items from your inventory."),
        new CreatureInfo( 7, "Blue Jelly",     "J", 2,  9, "1d4", "touch:1",       3,  "normal", "immobile, cold", "Stays put. Drains a little."),
        new CreatureInfo( 8, "Giant Centipede","c", 2,  5, "1d4", "sting:1d3",     4,  "normal", "animal, poison", "Bigger centipede; poison is more dangerous."),
        new CreatureInfo( 9, "Skeleton",       "s", 2,  6, "1d2", "hit:1, hit:1",  4,  "normal", "undead", "First undead. Holy weapons/slay-undead help."),
        new CreatureInfo(10, "Orc",            "o", 3,  6, "1d4", "weapon:1d6",   10,  "normal", "evil",  "First real melee threat. Carries decent gold."),

        // --- mid game (levels 4-15) -------------------------------------------
        new CreatureInfo(11, "Giant Ant",      "a", 4,  5, "1d4", "bite:1d4, sting:1",   12, "normal", "animal, poison", "Can be dangerous in groups."),
        new CreatureInfo(12, "Yellow Mushroom","m", 4,  9, "1d4", "spore:1",        5,  "normal", "immobile, hallucinatory", "Causes hallucination. Kill at range."),
        new CreatureInfo(13, "Cave Spider",    "S", 5,  4, "1d6", "bite:1d3, bite:1d3", 18, "fast",   "animal, poison", "Fast and poisonous. Watch your HP."),
        new CreatureInfo(14, "Ogre",           "O", 5,  4, "4d4", "club:2d4",     120,  "normal", "evil", "First big bruiser. Bring a real weapon."),
        new CreatureInfo(15, "Giant Tick",     "t", 6,  4, "1d4", "bite:1d3, blood:1d2", 25, "normal", "animal, drains HP", "Drains HP to heal itself. Kill fast."),
        new CreatureInfo(16, "Wraith",         "W", 7,  4, "2d4", "touch:1d6",    150,  "normal", "undead, evil, drains XP", "Drains experience on hit. Slay-undead essential."),
        new CreatureInfo(17, "Giant Skeleton", "s", 8,  4, "3d4", "hit:1d6, hit:1d6", 90, "normal", "undead", "Bigger skeleton. Slow but hits hard."),
        new CreatureInfo(18, "Vampire",        "V", 9,  4, "3d4", "bite:1d6, hit:1d6", 220, "normal", "undead, evil, drains XP, regenerate", "Drains XP and regenerates. Slay-undead + see-invisible."),
        new CreatureInfo(19, "Young Dragon",   "d", 10, 4, "4d4", "claw:2d4, bite:3d4", 600, "normal", "dragon", "First dragon. Slay-dragon weapon recommended."),

        // --- dragons (the major threats) --------------------------------------
        new CreatureInfo(20, "White Dragon",   "d", 14, 4, "5d4", "claw:2d4, bite:3d4, breath:cold",  1200, "normal", "dragon, breathes cold, resists cold",    "Breathes cold. Resist cold item advised."),
        new CreatureInfo(21, "Blue Dragon",    "d", 18, 4, "6d4", "claw:3d4, bite:3d4, breath:lightning", 1800, "normal", "dragon, breathes lightning, resists lightning", "Lightning breath destroys items. Resist lightning."),
        new CreatureInfo(22, "Red Dragon",     "d", 22, 4, "7d4", "claw:3d4, bite:4d4, breath:fire",   2400, "normal", "dragon, breathes fire, resists fire",    "Fire breath is very hot. Resist fire essential."),
        new CreatureInfo(29, "Black Dragon",   "d", 26, 4, "8d4", "claw:4d4, bite:5d4, breath:acid",   3000, "normal", "dragon, breathes acid, resists acid",    "Acid breath corrodes armor. Body armor soaks some."),
        new CreatureInfo(24, "Green Dragon",   "d", 30, 4, "8d4", "claw:4d4, bite:5d4, breath:poison", 3600, "normal", "dragon, breathes poison gas, resists poison", "No resistance to poison gas. Cure poison ready."),
        new CreatureInfo(25, "Ancient Multi-Hued Dragon", "D", 40, 2, "12d8", "claw:5d4, bite:6d4, breath:all", 12000, "fast", "dragon, breathes all elements, resists all", "The deadliest random dragon. Pillar-dance at speed 3."),

        // --- liches (the caster threat) ---------------------------------------
        new CreatureInfo(26, "Lich",           "L", 25, 2, "4d4", "touch:1d6, cast spells", 1500, "normal", "undead, evil, casts spells, drains mana", "Casts bolt/ball spells. Free action + see invisible."),
        new CreatureInfo(27, "Archlich",       "L", 35, 2, "6d4", "touch:1d6, cast spells", 4000, "normal", "undead, evil, casts spells, drains mana", "More HP and nastier spells than a lich."),
        new CreatureInfo(28, "Emperor Lich",   "L", 45, 2, "8d4", "touch:1d6, cast spells, drain charges", 9000, "fast", "undead, evil, casts spells, drains mana & charges", "1520+ HP. Drains wands for 40HP/charge. See emperor-lich tactic in the strategy guide."),

        // --- the Balrog and endgame -------------------------------------------
        new CreatureInfo(BalrogId, "Balrog of Moria", "B", 49, 2, "10d10", "flaming sword:8d8, whip:6d6, cast spells, breath:fire", 50000, "fast", "unique, evil, breathes fire, resists fire, immune to sleep/poly/confuse/genocide/destruction",
            "The endgame boss. Spawns on most levels from 49 onward. Cannot be bashed, polymorphed, slept, confused, genocided, or destroyed by Word of Destruction (it teleports to another level and heals). Kill with weapons and direct-damage spells (frost/fire/lightning ball). Pillar-dance at speed >= 3."),
    };

    public static CreatureInfo? ById(int id) => Creatures.FirstOrDefault(c => c.Id == id);
    public static CreatureInfo? Balrog => ById(BalrogId);
}
