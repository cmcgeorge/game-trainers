namespace ColonizationTrainer.Game;

/// <summary>A colonist profession/expert: its index and name plus what it does well.</summary>
public sealed record Profession(int Id, string Name, string Specialty);

/// <summary>
/// The 28 professions in index order (a colonist's <c>profession</c> byte indexes this list).
/// Verified against the viceroy <c>profession_list</c> and <c>.games/PEDIA.TXT</c> (<c>@JOB0…</c>).
/// An expert doubles output in its field (food experts add a flat +2).
/// </summary>
public static class ProfessionBook
{
    public static readonly IReadOnlyList<Profession> Professions = new[]
    {
        new Profession(0,  "Expert Farmer",         "×2 food on the tile (+2)"),
        new Profession(1,  "Master Sugar Planter",  "×2 sugar (learn from natives)"),
        new Profession(2,  "Master Tobacco Planter","×2 tobacco (learn from natives)"),
        new Profession(3,  "Master Cotton Planter", "×2 cotton (learn from natives)"),
        new Profession(4,  "Master Fur Trapper",    "×2 furs (learn from natives)"),
        new Profession(5,  "Expert Lumberjack",     "×2 lumber"),
        new Profession(6,  "Expert Ore Miner",      "×2 ore"),
        new Profession(7,  "Expert Silver Miner",   "×2 silver"),
        new Profession(8,  "Expert Fisherman",      "×2 fish (+2)"),
        new Profession(9,  "Master Distiller",      "×2 rum"),
        new Profession(10, "Master Tobacconist",    "×2 cigars"),
        new Profession(11, "Master Weaver",         "×2 cloth"),
        new Profession(12, "Master Fur Trader",     "×2 coats"),
        new Profession(13, "Expert Carpenter",      "×2 hammers (construction)"),
        new Profession(14, "Expert Blacksmith",     "×2 tools"),
        new Profession(15, "Master Gunsmith",       "×2 muskets"),
        new Profession(16, "Firebrand Preacher",    "×2 crosses"),
        new Profession(17, "Elder Statesman",       "×2 Liberty Bells"),
        new Profession(18, "(Student)",             "In training at a school"),
        new Profession(19, "Free Colonist",         "Baseline — any job (~3/turn)"),
        new Profession(20, "Hardy Pioneer",         "×2 pioneer work"),
        new Profession(21, "Veteran Soldier",       "Combat bonus"),
        new Profession(22, "Seasoned Scout",        "Better exploration / rumors"),
        new Profession(23, "Veteran Dragoon",       "Combat bonus (mounted)"),
        new Profession(24, "Jesuit Missionary",     "×2 missionary effect"),
        new Profession(25, "Indentured Servant",    "Below baseline; learns slowly"),
        new Profession(26, "Petty Criminal",        "Worst worker; can't learn from natives"),
        new Profession(27, "Indian Convert",        "Good outdoors, poor at manufacturing"),
    };
}
