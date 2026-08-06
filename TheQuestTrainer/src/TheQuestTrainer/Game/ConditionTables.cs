namespace TheQuestTrainer.Game;

/// <summary>
/// The four adverse conditions the game itself names.
///
/// This is not a list the trainer invented. The function behind the character screen's condition
/// icons tests exactly these four and writes the wording below; there is no fifth icon, and the
/// remaining effect groups hold things a player would not want removed — racial modifiers,
/// equipment bonuses, resistances, and the drunkenness the taverns sell on purpose.
/// </summary>
public enum Condition
{
    /// <summary>Losing health every turn until cured.</summary>
    Poison,

    /// <summary>Carrying one of the game's diseases, and whatever it inflicts.</summary>
    Disease,

    /// <summary>Attack power reduced for a number of turns.</summary>
    Curse,

    /// <summary>Cannot attack or move.</summary>
    Paralysis,
}

/// <summary>
/// Names and wording for <see cref="Condition"/>, transcribed from the game's own condition
/// tooltips rather than paraphrased.
/// </summary>
public static class ConditionTables
{
    /// <summary>The four, in the order the character screen lists them.</summary>
    public static readonly IReadOnlyList<Condition> All = new[]
    {
        Condition.Poison, Condition.Disease, Condition.Curse, Condition.Paralysis,
    };

    /// <summary>How the game labels the character while the condition is on them.</summary>
    public static string Name(Condition condition) => condition switch
    {
        Condition.Poison => "Poisoned",
        Condition.Disease => "Diseased",
        Condition.Curse => "Cursed",
        _ => "Paralyzed",
    };

    /// <summary>The condition itself rather than the state — "poison" to <see cref="Name"/>'s "Poisoned".</summary>
    public static string Noun(Condition condition) => condition switch
    {
        Condition.Poison => "poison",
        Condition.Disease => "disease",
        Condition.Curse => "curse",
        _ => "paralysis",
    };

    /// <summary>The game's own one-line description of what the condition does.</summary>
    public static string Effect(Condition condition) => condition switch
    {
        Condition.Poison => "Loses health every turn until cured.",
        Condition.Disease => "Negative effects until cured; resting while seriously diseased is lethal.",
        Condition.Curse => "Attack power is reduced.",
        _ => "Cannot attack or move; trying to attack skips the turn.",
    };

    /// <summary>
    /// The effect kind a condition is filed under, or null for <see cref="Condition.Disease"/>,
    /// which is a list of its own rather than an effect group.
    /// </summary>
    public static int? EffectKind(Condition condition) => condition switch
    {
        Condition.Poison => ConditionLayout.KindPoison,
        Condition.Curse => ConditionLayout.KindCurse,
        Condition.Paralysis => ConditionLayout.KindParalysis,
        _ => null,
    };

    /// <summary>
    /// How a live one is described: the game prints poison as health per turn and both curse and
    /// paralysis as turns left, so the trainer does too.
    /// </summary>
    public static string Describe(Condition condition, int magnitude, int turns) => condition switch
    {
        Condition.Poison => $"{magnitude:N0} health per turn",
        Condition.Curse or Condition.Paralysis => turns == 1 ? "1 turn left" : $"{turns:N0} turns left",
        _ => "",
    };
}
