namespace BardsTaleTrilogyTrainer.Game;

/// <summary>The seven casting schools of the trilogy, plus the two "no school" cases.</summary>
public enum SpellClass
{
    Conjurer = 0,
    Magician = 1,
    Sorcerer = 2,
    Wizard = 3,
    Archmage = 4,
    Chronomancer = 5,
    Geomancer = 6,

    /// <summary>
    /// A spell no school teaches. The game marks these with a <c>SpellDescription.m_level</c> of
    /// 0 and keeps them in the character's learnt-spell list; ZZGO and NUKE are the best known.
    /// </summary>
    AnyMagicUser = 7,

    None = -1,
}

/// <summary>
/// How the trilogy's casting schools map onto the game's class ids.
///
/// <para>This used to also carry a table of every spell with its code, school and level, taken
/// from a community list. That table was wrong for the remaster — the game keeps those in
/// serialized <c>SpellDescription</c> assets, and its schools and levels did not match. It has
/// been removed rather than corrected: <see cref="SpellCatalog"/> reads the real table out of
/// the running game, and <see cref="SpellId"/> carries the ids, which is everything the trainer
/// needs. What is left here is the school-to-class-id mapping, which is ordinary game structure
/// and is not guesswork.</para>
/// </summary>
public static class Spellbook
{
    /// <summary>The school a character class casts from, or <see cref="SpellClass.None"/>.</summary>
    public static SpellClass ArtForClass(int classId) => classId switch
    {
        6 => SpellClass.Conjurer,
        7 => SpellClass.Magician,
        8 => SpellClass.Sorcerer,
        9 => SpellClass.Wizard,
        10 => SpellClass.Archmage,
        11 => SpellClass.Chronomancer,
        12 => SpellClass.Geomancer,
        _ => SpellClass.None,
    };

    /// <summary>
    /// The class id that casts a school, which is also its index into the character's
    /// <c>m_spellLevel</c> array. Returns -1 for the two cases with no school of their own.
    /// </summary>
    public static int ClassIdFor(SpellClass cls) => cls switch
    {
        SpellClass.Conjurer => 6,
        SpellClass.Magician => 7,
        SpellClass.Sorcerer => 8,
        SpellClass.Wizard => 9,
        SpellClass.Archmage => 10,
        SpellClass.Chronomancer => 11,
        SpellClass.Geomancer => 12,
        _ => -1,
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

    /// <summary>The six bard songs of the first game.</summary>
    public static readonly string[] BardSongs =
    {
        "Falkentyne's Fury", "Seeker's Ballad", "Wayland's Watch",
        "Badh'r Kilnfest", "The Traveller's Tune", "Lucklaran",
    };
}
