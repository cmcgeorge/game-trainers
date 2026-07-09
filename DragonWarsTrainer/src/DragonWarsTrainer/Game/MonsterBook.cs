namespace DragonWarsTrainer.Game;

/// <summary>One bestiary entry with its core stats and attack notes.</summary>
public sealed record MonsterInfo(string Name, int Str, int Dex, int Int, int Spr,
    string Hp, string Av, string Speed, int Xp, string Notes)
{
    public string Attributes => $"STR {Str}  DEX {Dex}  INT {Int}  SPR {Spr}";
}

/// <summary>
/// Selected Dragon Wars bestiary entries, transcribed from the hitchhikerprod Purgatory/Freeport
/// maps. A monster's DEX drives its base AV/DV (base = DEX / 4); the AV column is the extra flat
/// modifier. Reference only.
/// </summary>
public static class MonsterBook
{
    public static readonly IReadOnlyList<MonsterInfo> Monsters = new MonsterInfo[]
    {
        new("Bandits", 10, 16, 9, 9, "3-24", "+0", "20'", 80, "1d8 stun; can flee."),
        new("Big Dogs", 17, 12, 3, 10, "11-20", "+0", "30'", 80, "2d6 stun."),
        new("Born Losers", 3, 10, 3, 5, "2-8", "+0", "10'", 60, "2d4 stun."),
        new("Cannibals", 8, 15, 4, 1, "1-10", "+0", "30'", 30, "2d4 stun, 2d6 physical."),
        new("Drunks", 16, 9, 3, 15, "9-34", "+1", "10'", 90, "1d4 stun."),
        new("Fanatics", 12, 10, 3, 15, "4-18", "+0", "20'", 80, "2d4 stun."),
        new("Giant Spiders", 22, 24, 1, 6, "16-34", "+0", "50'", 110, "2d8 physical; cannot be disarmed."),
        new("King's Guard", 12, 16, 8, 10, "6-21", "+0", "10'", 100, "3d6 & 3d8 physical; carries gold."),
        new("Pikemen", 16, 12, 10, 10, "11-32", "+0", "10'", 90, "3d4 physical; can flee; carries gold."),
        new("Wolves", 9, 16, 5, 6, "7-27", "+1", "40'", 80, "2d6 stun."),
        new("Gladiators", 15, 23, 15, 16, "7-28", "+1", "20'", 130, "3d6 physical (Arena bosses)."),
        new("Humbaba", 66, 18, 5, 20, "60-110", "+1", "20'", 1000, "3d10 physical (Purgatory quest boss)."),
        new("Adventurers", 20, 30, 20, 2, "40-75", "+2", "50'", 140, "8d8 physical (Freeport)."),
        new("Goblins", 10, 16, 6, 10, "5-17", "+3", "30'", 90, "4d6 physical."),
        new("Murk Trees", 0, 16, 0, 0, "23-95", "+4", "50'", 150, "7d8 physical."),
    };
}
