namespace WastelandTrainer.Game;

/// <summary>A Wasteland skill: its in-record id, name, and the IQ needed to learn its first level.</summary>
public sealed record SkillInfo(int Id, string Name, int MinIq)
{
    public string Description => MinIq > 0 ? $"Requires IQ {MinIq}." : "";
}

/// <summary>
/// Reference table of Wasteland's 35 skills, keyed by the skill id stored in the packed skill list
/// at <see cref="CharacterFormat.OffSkills"/>. Ids and names were confirmed by decoding the four
/// dumped party members and matched against <c>.game\manual.txt</c>; the minimum-IQ groupings come
/// from the manual's skill listing. Reference only; the trainer edits skills by id.
/// </summary>
public static class SkillBook
{
    public static readonly IReadOnlyList<SkillInfo> Skills = new SkillInfo[]
    {
        new(1,  "Brawling",         0),
        new(2,  "Climb",            0),
        new(3,  "Clip Pistol",      0),
        new(4,  "Knife Fight",      0),
        new(5,  "Pugilism",         0),
        new(6,  "Rifle",            0),
        new(7,  "Swim",             0),
        new(8,  "Knife Throw",      0),
        new(9,  "Perception",       10),
        new(10, "Assault Rifle",    10),
        new(11, "AT Weapon",        10),
        new(12, "SMG",              10),
        new(13, "Acrobat",          10),
        new(14, "Gamble",           11),
        new(15, "Picklock",         11),
        new(16, "Silent Move",      11),
        new(17, "Combat Shooting",  12),
        new(18, "Confidence",       12),
        new(19, "Sleight of Hand",  12),
        new(20, "Demolition",       13),
        new(21, "Forgery",          13),
        new(22, "Alarm Disarm",     14),
        new(23, "Bureaucracy",      14),
        new(24, "Bomb Disarm",      15),
        new(25, "Medic",            15),
        new(26, "Safecrack",        16),
        new(27, "Cryptology",       17),
        new(28, "Metallurgy",       18),
        new(29, "Helicopter Pilot", 18),
        new(30, "Electronics",      19),
        new(31, "Toaster Repair",   20),
        new(32, "Doctor",           21),
        new(33, "Clone Tech",       22),
        new(34, "Energy Weapon",    23),
        new(35, "Cyborg Tech",      24),
    };

    private static readonly Dictionary<int, SkillInfo> ById =
        Skills.ToDictionary(s => s.Id);

    public static string SkillName(int id) =>
        ById.TryGetValue(id, out var s) ? s.Name : $"Skill #{id}";

    public static SkillInfo? Find(int id) => ById.TryGetValue(id, out var s) ? s : null;
}
