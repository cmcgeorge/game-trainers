namespace Questron2Trainer.Game;

/// <summary>Information about a single Questron II monster.</summary>
public sealed record MonsterInfo(int Id, string Name);

/// <summary>
/// The monsters of Questron II, extracted from START.EXE strings.
/// The manual states "over 60 different types of creatures inhabit Landor";
/// these ~40 were recovered from the EXE's string table.
/// </summary>
public static class MonsterBook
{
    public static readonly MonsterInfo[] Monsters =
    {
        new(0,  "Sovan Priest"),
        new(1,  "Gypsy Imp"),
        new(2,  "Beggar"),
        new(3,  "Brawn Warrior"),
        new(4,  "Wave Slapper"),
        new(5,  "Mutant Carp"),
        new(6,  "Hull Bore"),
        new(7,  "Spincer"),
        new(8,  "Snooper Slink"),
        new(9,  "Slasher Boar"),
        new(10, "Antisaur"),
        new(11, "Grub Snuffler"),
        new(12, "Ramdart"),
        new(13, "Swine Swallow"),
        new(14, "Boll Rot"),
        new(15, "Tangler"),
        new(16, "Hornet Cloud"),
        new(17, "Baboon"),
        new(18, "Ball Slime"),
        new(19, "Carrion Creeper"),
        new(20, "Jelly Nymph"),
        new(21, "Giant Cockroach"),
        new(22, "Stink Worm"),
        new(23, "Hurler"),
        new(24, "Ice Urchin"),
        new(25, "Cloud Creeper"),
        new(26, "Spiker"),
        new(27, "Venom Ant"),
        new(28, "Constrictor"),
        new(29, "Giant Mantray"),
        new(30, "Pincer"),
        new(31, "Jovine Pig"),
        new(32, "Blook Slake"),
        new(33, "Cannibal"),
        new(34, "Muck Grabber"),
        new(35, "Swamp Slither"),
        new(36, "Brine Flicker"),
        new(37, "Gilgore"),
        new(38, "Mind Scream"),
    };

    public static int Count => Monsters.Length;
}
