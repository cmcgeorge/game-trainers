namespace DarklandsTrainer.Game;

/// <summary>
/// Miscellaneous <b>Confirmed</b> game-knowledge constants and tables surfaced on the Reference tab:
/// the three-tier currency and its conversion ratios, and the party Fame ladder. All are read out of the
/// executable's string tables and the shipped docs (see <c>.docs/ReverseEngineering.md</c> §2.6–§2.7).
/// </summary>
public static class GameFacts
{
    /// <summary>Silver groschen in one gold florin.</summary>
    public const int GroschenPerFlorin = 20;

    /// <summary>Copper pfennigs in one silver groschen.</summary>
    public const int PfennigPerGroschen = 12;

    /// <summary>Copper pfennigs in one gold florin (20 × 12).</summary>
    public const int PfennigPerFlorin = GroschenPerFlorin * PfennigPerGroschen;

    /// <summary>Reduces a raw pfennig total to florins / groschen / pfennigs, matching the in-game purse display.</summary>
    public static (int Florins, int Groschen, int Pfennigs) SplitPfennigs(int totalPfennigs)
    {
        if (totalPfennigs < 0) totalPfennigs = 0;
        int florins = totalPfennigs / PfennigPerFlorin;
        int remainder = totalPfennigs % PfennigPerFlorin;
        int groschen = remainder / PfennigPerGroschen;
        int pfennigs = remainder % PfennigPerGroschen;
        return (florins, groschen, pfennigs);
    }

    /// <summary>The party Fame ladder, lowest to highest, as spelled in the executable.</summary>
    public static readonly IReadOnlyList<string> FameTiers = Array.AsReadOnly(new[]
    {
        "Unknown", "Barely Known", "Slight Reputation", "Modest Reputation", "Good Reputation",
        "Slight Heroes", "Modest Heroes", "Great Heroes", "Famous Heroes", "Storied Heroes",
        "Legendary Heroes",
    });

    /// <summary>The three coin denominations, most valuable first.</summary>
    public static readonly IReadOnlyList<CurrencyInfo> Currency = Array.AsReadOnly(new CurrencyInfo[]
    {
        new("Florin",   "fl", "Gold. 1 fl = 20 gr = 240 pf."),
        new("Groschen", "gr", "Silver. 1 gr = 12 pf."),
        new("Pfennig",  "pf", "Copper. The smallest coin."),
    });
}

/// <summary>One coin denomination for the Reference tab.</summary>
public readonly record struct CurrencyInfo(string Name, string Abbreviation, string Notes);
