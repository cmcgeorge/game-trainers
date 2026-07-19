namespace MoriaTrainer.Game;

/// <summary>One of the eight UMoria character races (Confirmed from <c>moria1.txt</c> §2.4).</summary>
public sealed record RaceInfo(
    int Id,
    string Name,
    string AllowedClasses,
    string KeyAdjustments,
    bool Infravision,
    string Notes)
{
    public bool CanBe(int classId) => AllowedClasses switch
    {
        "any" => true,
        "any except Paladin" => classId != ClassBook.ClassPaladin,
        _ => AllowedClasses.Split(',').Contains(classId.ToString()),
    };
}

/// <summary>The eight playable races (Confirmed from the manual + <c>constant.h</c> race ids).</summary>
public static class RaceBook
{
    public const int RaceHuman      = 0;
    public const int RaceHalfElf    = 1;
    public const int RaceElf        = 2;
    public const int RaceHalfling   = 3;
    public const int RaceGnome      = 4;
    public const int RaceDwarf      = 5;
    public const int RaceHalfOrc    = 6;
    public const int RaceHalfTroll  = 7;

    public static readonly IReadOnlyList<RaceInfo> Races = new[]
    {
        new RaceInfo(RaceHuman,     "Human",     "any",
            "no adjustments; levels fastest",
            false, "The baseline race. Good at everything, great at nothing."),
        new RaceInfo(RaceHalfElf,   "Half-Elf",  "any",
            "+INT/DEX, -STR; +search/disarm/stealth/magic",
            true,  "A balanced race that can take any class."),
        new RaceInfo(RaceElf,       "Elf",       "any except Paladin",
            "+INT/WIS/DEX, -STR/CON; better magic than Half-Elf",
            true,  "Fragile but magical. No Paladins."),
        new RaceInfo(RaceHalfling,  "Halfling",  "0,1,3",   // Warrior, Mage, Rogue
            "+DEX, big -STR; great save, search, stealth",
            true,  "Excellent rogues. No bashing."),
        new RaceInfo(RaceGnome,     "Gnome",     "0,1,2,3", // Warrior, Mage, Priest, Rogue
            "+INT, -STR; great saving throw",
            true,  "Practical jokers. Good mages."),
        new RaceInfo(RaceDwarf,     "Dwarf",     "0,2",     // Warrior, Priest
            "+CON/STR, -DEX/CHR; great fighting; resists poison",
            true,  "The most forgiving warrior race."),
        new RaceInfo(RaceHalfOrc,   "Half-Orc",  "0,2,3",   // Warrior, Priest, Rogue
            "+STR/CON, -CHR; great fighting",
            true,  "Brutal melee; ugly disposition."),
        new RaceInfo(RaceHalfTroll, "Half-Troll","0",       // Warrior
            "+STR/CON huge, -INT/WIS/CHR; very strong, very dumb",
            true,  "The tank. Warrior only."),
    };

    public static RaceInfo? ById(int id) => id >= 0 && id < Races.Count ? Races[id] : null;
}
