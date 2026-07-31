namespace AirborneRangerTrainer.Game;

/// <summary>One entry of the game's rank table.</summary>
/// <param name="Index">Index stored in the roster record's binary tail.</param>
/// <param name="Mnemonic">The three-character mnemonic the game prints.</param>
/// <param name="Name">The full rank name, from the game's promotion messages.</param>
public readonly record struct RankInfo(int Index, string Mnemonic, string Name);

/// <summary>
/// The rank ladder, read out of the literal at <c>DGROUP:0xBB64</c> —
/// <c>"PFC CPL SGT SSG PSG SGM 2LT 1LT CPT MAJ LTC COL     KIA POW "</c>, four characters per entry.
/// The full names come from the game's own promotion messages at <c>DGROUP:0xD149</c>
/// (<c>Corporal.</c> … <c>Colonel.</c>).
///
/// <para>Indices 12–14 are not promotions: 12 is the blank the game uses for an empty slot, and
/// 13/14 record a ranger who was killed or captured.</para>
/// </summary>
public static class RankBook
{
    /// <summary>Every rank slot, including the blank and the two casualty markers.</summary>
    public static readonly IReadOnlyList<RankInfo> All = new[]
    {
        new RankInfo(0,  "PFC", "Private First Class"),
        new RankInfo(1,  "CPL", "Corporal"),
        new RankInfo(2,  "SGT", "Sergeant"),
        new RankInfo(3,  "SSG", "Staff Sergeant"),
        new RankInfo(4,  "PSG", "Platoon Sergeant"),
        new RankInfo(5,  "SGM", "Sergeant Major"),
        new RankInfo(6,  "2LT", "Second Lieutenant"),
        new RankInfo(7,  "1LT", "First Lieutenant"),
        new RankInfo(8,  "CPT", "Captain"),
        new RankInfo(9,  "MAJ", "Major"),
        new RankInfo(10, "LTC", "Lieutenant Colonel"),
        new RankInfo(11, "COL", "Colonel"),
        new RankInfo(12, "   ", "(empty slot)"),
        new RankInfo(13, "KIA", "Killed in action"),
        new RankInfo(14, "POW", "Prisoner of war"),
    };

    /// <summary>Number of entries in the table.</summary>
    public static int Count => All.Count;

    /// <summary>Highest index that is a promotion rather than a marker.</summary>
    public const int HighestPromotion = 11;

    /// <summary>The mnemonic for <paramref name="index"/>, or three spaces if out of range.</summary>
    public static string Mnemonic(int index) =>
        index >= 0 && index < All.Count ? All[index].Mnemonic : "   ";

    /// <summary>The full name for <paramref name="index"/>, or a placeholder if out of range.</summary>
    public static string Name(int index) =>
        index >= 0 && index < All.Count ? All[index].Name : $"(rank {index})";
}
