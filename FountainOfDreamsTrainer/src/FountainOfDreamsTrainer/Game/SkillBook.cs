namespace FountainOfDreamsTrainer.Game;

/// <summary>
/// A Fountain of Dreams skill: its name, a short description, and the IQ needed to learn it.
/// Skill ids and names were compiled from the game manual and the KEH.EXE display strings.
/// The exact in-record skill encoding is variable-length packed data in the +0x24..+0x43
/// region; the trainer reads and writes skills through the live memory view.
/// </summary>
public sealed record SkillInfo(int Id, string Name, int MinIq, string Description = "")
{
    public string Requirement => MinIq > 0 ? $"Requires IQ {MinIq}." : "Learnable at any IQ.";

    public string FullDescription => string.Join("  ", new[]
    {
        Description,
        Requirement,
    }.Where(s => !string.IsNullOrEmpty(s)));
}

/// <summary>
/// Reference table of Fountain of Dreams skills. The game uses a similar skill system to
/// Wasteland (its engine predecessor), with skills gated by IQ thresholds. Skill names and
/// descriptions come from the game manual; the exact skill id mapping in the character record
/// is [Inferred] from the manual's skill listing order and the KEH.EXE display format strings.
/// </summary>
public static class SkillBook
{
    public static readonly IReadOnlyList<SkillInfo> Skills = new SkillInfo[]
    {
        new(1,  "Brawling",       0,  "More unarmed attacks per round in hand-to-hand combat."),
        new(2,  "Knife Fight",    0,  "Sharper accuracy and damage with knives at melee range."),
        new(3,  "Pistol",         0,  "Aim, load, and clear jams on pistols and sidearms."),
        new(4,  "Rifle",          0,  "Accurate single-fire with bolt and semi-auto rifles."),
        new(5,  "SMG",            10, "Control burst and auto fire on submachine guns."),
        new(6,  "Assault Rifle",  10, "Fire, load, and unjam assault rifles."),
        new(7,  "Shotgun",        10, "Shotgun proficiency for close-range stopping power."),
        new(8,  "Energy Weapon",  23, "Wield laser and ion energy weapons."),
        new(9,  "Athletics",      0,  "Running, jumping, and general physical fitness."),
        new(10, "Swim",           0,  "Cross deep water instead of being turned back."),
        new(11, "Climb",          0,  "Scale fences, walls, and cliffs that block the way."),
        new(12, "Perception",     10, "Spot concealed items, traps, and hidden passages."),
        new(13, "Stealth",        11, "Move silently and avoid detection by enemies."),
        new(14, "Picklock",       11, "Open locked doors and containers without the key."),
        new(15, "Sleight of Hand",12, "Pickpocket and sleight-of-hand tricks."),
        new(16, "Gamble",         11, "Better odds at games of chance."),
        new(17, "Medic",          15, "Field first aid that stabilises wounded party members."),
        new(18, "Doctor",         21, "Advanced medicine; heals serious wounds and cures afflictions."),
        new(19, "Science",        19, "Operate and repair technical equipment and electronics."),
        new(20, "Mechanics",      18, "Repair mechanical devices and vehicles."),
        new(21, "Survival",       10, "Find food and water in the Florida wilderness."),
        new(22, "Mutant Lore",    14, "Knowledge of mutations and their effects."),
        new(23, "Shrink",         16, "Psychiatric counselling; cures mental afflictions."),
        new(24, "Arcana",         17, "Knowledge of the island's hidden lore and rituals."),
    };

    private static readonly Dictionary<int, SkillInfo> ById = Skills.ToDictionary(s => s.Id);

    public static string SkillName(int id) =>
        ById.TryGetValue(id, out var s) ? s.Name : $"Skill #{id}";

    public static SkillInfo? Find(int id) => ById.TryGetValue(id, out var s) ? s : null;
}
