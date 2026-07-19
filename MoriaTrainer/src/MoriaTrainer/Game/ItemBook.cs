namespace MoriaTrainer.Game;

/// <summary>An item-category entry from UMoria's <c>object_list[420]</c>.</summary>
public sealed record ItemInfo(
    int Tval,
    string Category,
    string DisplayChar,
    string Examples,
    string Notes)
{
    public string TvalHex => $"0x{Tval:X2}";
}

/// <summary>
/// The item-type reference (Confirmed from <c>constant.h</c>'s TV_* constants and the manual §9).
/// Maps the <c>tval</c> byte stored on each <c>inven_type</c> to a human-readable category.
/// </summary>
public static class ItemBook
{
    public static readonly IReadOnlyList<ItemInfo> Items = new[]
    {
        new ItemInfo( 1, "Shovel",            "/", "Shovel, Shovel (+1)", "Digging tool. Wield to dig through veins/loose rock."),
        new ItemInfo( 2, "Pick",              "\\", "Pick, Pick (+2)",    "Digging tool. Better than a shovel; can be a weapon."),
        new ItemInfo( 5, "Sword",             "|", "Dagger, Short Sword, Long Sword, Katana, Two-Handed Sword", "Edged weapon. Light swords get extra blows at high STR/DEX."),
        new ItemInfo( 6, "Hafted Weapon",     "|", "Mace, Morning Star, War Hammer, Flail", "Blunt weapon. Priest-friendly (priests cannot use edged)."),
        new ItemInfo( 7, "Polearm",           "|", "Spear, Trident, Glaive, Halberd", "Two-handed reach weapon."),
        new ItemInfo( 8, "Bow",               "}", "Short Bow, Long Bow, Composite Bow, Light Crossbow, Heavy Crossbow, Sling", "Wield to multiply missile damage. Sling×2, short bow×2, long bow×3, composite bow×4, light XB×3, heavy XB×4."),
        new ItemInfo(10, "Sling Ammo",        "{", "Rounded Pebble, Iron Shot", "Used with a sling (×2 multiplier)."),
        new ItemInfo(11, "Bow Ammo",          "{", "Arrow, Bolt", "Used with the matching bow/crossbow for big multiplier."),
        new ItemInfo(15, "Boots",             "]", "Soft Boots, Hard Boots, Boots of Speed, Boots of Free Action", "Foot slot. Boots of Speed are a permanent +speed item."),
        new ItemInfo(16, "Cloak",             "]", "Cloak, Cloak of Protection, Cloak of Invisibility", "Back slot."),
        new ItemInfo(17, "Helmet/Crown",      "]", "Iron Crown, Crown of the Magi, Crown of Might, Metal Cap, Iron Helm", "Head slot. Crowns have powerful ego abilities."),
        new ItemInfo(17, "Spike",             ",", "Iron Spike", "Used to jam doors (j<dir> / S<dir>). Can be wielded as a joke weapon."),
        new ItemInfo(20, "Shield",            "]", "Small Shield, Large Shield, Shield of Resistance", "Off-hand armor slot."),
        new ItemInfo(21, "Clothing",          "(", "Robe", "Soft body armor; favored by mages (no to-hit penalty)."),
        new ItemInfo(22, "Soft Armor",         "(", "Soft Leather, Studded Leather, Rhinu-Hide", "Light body armor; no to-hit penalty."),
        new ItemInfo(23, "Hard Armor",         "(", "Chain Mail, Plate Mail, Mithril Plate", "Heavy body armor; shows a (-#) to-hit penalty."),
        new ItemInfo(25, "Scroll",            "?", "Identify, Word-of-Recall, Recharge, Mass Genocide, Rune of Protection, Destruction", "Read with 'r'. Some titles can be read without triggering (identify, recharge)."),
        new ItemInfo(26, "Potion",            "!", "Cure Light/Serious/Critical Wounds, Healing, Gain Stat, Restore Mana, Invulnerability", "Quaff with 'q'. Multiple effects on some (cure light also cures blindness)."),
        new ItemInfo(30, "Flask of Oil",      "~", "Flask of Oil", "Refills a lamp (F command). Thrown oil flasks do fire damage."),
        new ItemInfo(35, "Ring",              "=", "Ring of Strength, Ring of Speed, Ring of Searching, Ring of Free Action", "Two ring slots (left and right hand). Ring of Speed is endgame gear (level 50)."),
        new ItemInfo(40, "Amulet",            ",", "Amulet of the Magi, Amulet of Wisdom, Amulet of Charisma", "Neck slot. Amulet of the Magi: free action, see invisible, +search, +3 AC."),
        new ItemInfo(45, "Chest",             "&", "Empty Chest, Chest (locked), Chest (trapped)", "Always search before opening. Bashing may destroy contents but doesn't disarm."),
        new ItemInfo(65, "Wand",              "-", "Wand of Magic Missile, Lightning Ball, Frost Ball, Fire Ball, Drain Life, Clone Monster, Wall Building", "Aimed (a<dir> / z<dir>). Uses Magic-Device skill. Recharge scrolls refill them."),
        new ItemInfo(70, "Staff",             "_", "Staff of Cure Light Wounds, Lightning, Dispel Evil, Speed, Mass Polymorph, Destruction, Healing", "Used (u / Z). Area-effect; harder to use than wands but more powerful."),
        new ItemInfo(80, "Food",              ",", "Ration, Slime Mold, Piece of Elvish Waybread, Potion of Slime Mold Juice", "Eaten with E. Food counter hits 0 = starvation death."),
        new ItemInfo(90, "Magic Book",        ")", "Beginners' Magician's Handbook, Trowel-Wielders' Guide, Higher Magicians' Handbook, Mages' Companion", "Mage spells (b / P). Carry the book of any spell you know."),
        new ItemInfo(91, "Prayer Book",       "p", "Beginners' Hymnal, Chants/Blessings, Exorcism/Dispel, Holy Words", "Priest prayers (p / P). Carry the book of any prayer you know."),
    };

    /// <summary>Ego-weapon modifiers (Confirmed from <c>faq</c> Q8 and manual §8.2).</summary>
    public static readonly IReadOnlyList<(string Code, string Name, string Effect)> EgoWeapons = new[]
    {
        ("HA", "Holy Avenger",  "+(1-4) STR, +(1-4) AC, slay evil/undead, sustain stat, see invisible. +1 STR sustains STR; +2 INT; +3 WIS; +4 CON."),
        ("DF", "Defender",      "stealth, regenerate, free action, see invisible, feather fall, RF/RC/RL/RA, +(6-10) AC."),
        ("SA", "Slay Animal",   "×2 damage vs. animals."),
        ("SD", "Slay Dragon",   "×4 damage vs. dragons."),
        ("SE", "Slay Evil",     "×2 damage vs. evil."),
        ("SU", "Slay Undead",   "×3 damage vs. undead; see invisible."),
        ("FT", "Flame Tongue",  "×1.5 damage vs. fire-vulnerable creatures."),
        ("FB", "Frost Brand",   "×1.5 damage vs. cold-vulnerable creatures."),
    };

    /// <summary>Crown types (Confirmed from <c>faq</c> Q8 and manual §8.5).</summary>
    public static readonly IReadOnlyList<(string Name, string Effect)> Crowns = new[]
    {
        ("Crown of the Magi",      "+(1-3) INT, RF/RC/RA/RL."),
        ("Crown of Lordliness",    "+(1-3) WIS, CHR."),
        ("Crown of Might",         "+(1-3) STR, DEX, CON; free action."),
        ("Crown of Seeing",        "see invisible; +(10-25) searching."),
        ("Crown of Regeneration",  "1.5× HP/mana regen (uses food faster)."),
        ("Crown of Beauty",        "+(1-3) CHR (otherwise useless)."),
    };

    /// <summary>Item-flag resistances/abilities (Confirmed from <c>constant.h</c> TR_* bits).</summary>
    public static readonly IReadOnlyList<(string Code, string Effect)> WearableFlags = new[]
    {
        ("RA", "Resist Acid (1/3 damage; armor isn't corroded)."),
        ("RC", "Resist Cold (1/3 damage)."),
        ("RF", "Resist Fire (1/3 damage)."),
        ("RL", "Resist Lightning (1/3 damage)."),
        ("R",  "Resistance (RA + RC + RF + RL)."),
        ("FA", "Free Action (immune to slow/paralyze from monsters)."),
        ("SI", "See Invisible."),
        ("PL", "Permanent Light (acts as a light source)."),
        ("TL", "Temporary Light (torch/lamp fuel counter)."),
        ("SR", "Slow Regeneration."),
        ("RG", "Regeneration (1.5× HP/mana regen; uses food faster)."),
        ("FF", "Feather Fall (1/2 trap door fall damage)."),
        ("BB", "Berserk Strength (bonus STR; penalty INT/CHR)."),
        ("HA", "Holy Avenger (see Ego Weapons)."),
        ("DF", "Defender (see Ego Weapons)."),
        ("ST", "Sustain (keeps a drained stat from dropping further until restored)."),
    };

    public static ItemInfo? ByTval(int tval) => Items.FirstOrDefault(i => i.Tval == tval);
}
