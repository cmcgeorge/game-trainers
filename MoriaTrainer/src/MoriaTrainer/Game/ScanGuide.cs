namespace MoriaTrainer.Game;

/// <summary>
/// One guided value-scan recipe for a UMoria character field. Each recipe tells the user what to do
/// in-game, picks the right <see cref="ScanWidth"/>, and gives the typical value range so the UI can
/// pre-fill the scan box with a sensible default. Recipes are derived from the struct layout in
/// <see cref="PlayerFormat"/> (Confirmed for the front of <c>misc</c>) and the stat encoding.
/// </summary>
public sealed record ScanRecipe(
    string Field,
    string DisplayName,
    ScanWidth Width,
    string HowToRead,
    long TypicalMin,
    long TypicalMax,
    long SuggestedDefault,
    string Notes)
{
    public string Range => TypicalMin == TypicalMax
        ? TypicalMin.ToString()
        : $"{TypicalMin}..{TypicalMax}";
}

/// <summary>
/// The guided scans the trainer ships for UMoria 5.5.2. Each maps a visible on-screen number to a
/// memory field, with the scan width that matches the C type. The trainer's Character tab lists
/// these as one-click "guided scan" buttons that pre-fill the value scanner with the right width
/// and a hint.
/// </summary>
public static class ScanGuide
{
    public static readonly IReadOnlyList<ScanRecipe> Recipes = new[]
    {
        new ScanRecipe("chp",      "Current HP",        ScanWidth.Int32,
            "Read your current HP from the status line (e.g. '30/30').",
            1, 9999, 30,
            "Stored as int32 in misc.chp. Pin and freeze to be invincible."),
        new ScanRecipe("maxhp",    "Max HP",            ScanWidth.Int32,
            "Read your max HP from the status line (e.g. '30/30').",
            1, 9999, 30,
            "Stored as int32 in misc.maxhp. Editing raises your HP ceiling."),
        new ScanRecipe("cmana",    "Current Mana",      ScanWidth.Int32,
            "Read your current mana from the status line (e.g. '10/10').",
            0, 9999, 10,
            "Stored as int32 in misc.cmana. Mages/priests only; warriors have 0."),
        new ScanRecipe("mhp",      "Max Mana",          ScanWidth.Int32,
            "Read your max mana from the status line (e.g. '10/10').",
            0, 9999, 10,
            "Stored as int32 in misc.mhp. Mages/priests only."),
        new ScanRecipe("au",       "Gold",              ScanWidth.Int32,
            "Read your gold from the status line (e.g. 'Au 250').",
            0, 9999999, 250,
            "Stored as int32 in misc.au. The most common cheat target."),
        new ScanRecipe("exp",      "Experience",        ScanWidth.Int32,
            "Read your experience from the 'C' character description.",
            0, 9999999, 100,
            "Stored as int32 in misc.exp. Editing doesn't auto-grant levels."),
        new ScanRecipe("lev",      "Character Level",   ScanWidth.Int16,
            "Read your level from the status line (e.g. 'LEV 5').",
            1, PlayerFormat.MaxLevel, 5,
            "Stored as int16 in misc.lev. Editing the level doesn't recalc HP/mana."),
        new ScanRecipe("str",      "Strength",          ScanWidth.Byte,
            "Read STR from 'C'. If ≤18, scan that byte; if 18/xx, scan the /xx byte (the byte after 18).",
            3, 100, 18,
            "Stats 3..18 are one byte; 18/01..18/100 use two bytes (18, then /xx). Pin and freeze to keep a drained stat."),
        new ScanRecipe("int",      "Intelligence",      ScanWidth.Byte,
            "Read INT from 'C'. Same encoding as STR.",
            3, 100, 18, "Mage's prime stat."),
        new ScanRecipe("wis",      "Wisdom",            ScanWidth.Byte,
            "Read WIS from 'C'. Same encoding as STR.",
            3, 100, 18, "Priest's prime stat."),
        new ScanRecipe("dex",      "Dexterity",         ScanWidth.Byte,
            "Read DEX from 'C'. Same encoding as STR.",
            3, 100, 18, "Affects blows/turn, to-hit, dodging, disarming."),
        new ScanRecipe("con",      "Constitution",      ScanWidth.Byte,
            "Read CON from 'C'. Same encoding as STR.",
            3, 100, 18, "Affects HP/level and poison resistance."),
        new ScanRecipe("chr",      "Charisma",          ScanWidth.Byte,
            "Read CHR from 'C'. Same encoding as STR.",
            3, 100, 18, "Affects store prices."),
        new ScanRecipe("food",     "Food Counter",      ScanWidth.Int16,
            "Read the food counter indirectly: 'Weak' when low, 'Full' when high. Scan after eating (high) and after going hungry (low).",
            0, 15000, 5000,
            "Stored as int16 in misc.food. 0 = starvation death. Freeze high to never need food."),
    };

    public static ScanRecipe? ByField(string field) => Recipes.FirstOrDefault(r => r.Field == field);
}
