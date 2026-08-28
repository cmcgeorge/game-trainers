namespace MightAndMagic1Trainer.Game;

/// <summary>One rule of the engine, and how well it is known.</summary>
/// <param name="Title">What the rule governs.</param>
/// <param name="Text">The rule, in prose.</param>
/// <param name="Confidence">"Confirmed", "Inferred" or "Uncertain", matching <c>docs/</c>.</param>
/// <param name="Source">The routine or table it was read out of.</param>
public sealed record GameRule(string Title, string Text, string Confidence, string Source);

/// <summary>The hit die a class rolls on each level-up.</summary>
public sealed record HitDieRow(string ClassName, int Die)
{
    public string DieText => $"d{Die}";
}

/// <summary>
/// The mechanics behind the numbers on a character sheet, as read out of <c>Mm.exe</c> and written
/// up in <c>docs/formulas.md</c> — read-only reference data, like <see cref="ClassBook"/> and
/// <see cref="MonsterBook"/>.
///
/// <para><b>Why this is worth a chapter of its own.</b> Every other reference in the trainer says
/// what a thing <i>is</i>; this says what the game <i>does</i> with it. A player deciding whether
/// Endurance is worth re-rolling for wants the hit-point rule, not the attribute's name, and a
/// player who has watched the same monster kill the same character twice wants to know that a
/// natural 1 always misses and a natural 20 always hits.</para>
///
/// <para>Each entry carries its own confidence and the routine it came from, because these were
/// recovered by disassembly rather than published: the shapes are confirmed, several of the exact
/// constants are read straight off a decompile and are worth a live spot-check.</para>
/// </summary>
public static class RulesBook
{
    /// <summary>
    /// The per-class hit die, from the 6-byte table at <c>DS:0x1374</c>. Indexed the way the record's
    /// class byte is, so the order is the same as <see cref="ClassBook.Classes"/>.
    /// </summary>
    public static readonly IReadOnlyList<HitDieRow> HitDice = new[]
    {
        new HitDieRow("Knight", 12),
        new HitDieRow("Paladin", 10),
        new HitDieRow("Archer", 10),
        new HitDieRow("Cleric", 8),
        new HitDieRow("Sorcerer", 6),
        new HitDieRow("Robber", 8),
    };

    /// <summary>
    /// The Endurance thresholds and the hit points each is worth per level, highest first.
    ///
    /// A pair here is read as "at least this Endurance, this bonus". The negative rows are why a
    /// character rolled with Endurance below 9 loses hit points every level they train, which is the
    /// single most expensive mistake available at character creation.
    /// </summary>
    public static readonly IReadOnlyList<(int MinEndurance, int Bonus)> EnduranceBonuses = new[]
    {
        (40, 10), (35, 9), (30, 8), (27, 7), (24, 6), (21, 5), (19, 4), (17, 3), (15, 2), (13, 1),
        (9, 0), (7, -1), (5, -2), (0, -3),
    };

    /// <summary>Hit points a character of this Endurance gains or loses on top of the class die, per level.</summary>
    public static int EnduranceBonus(int endurance)
    {
        foreach (var (min, bonus) in EnduranceBonuses)
            if (endurance >= min) return bonus;
        return EnduranceBonuses[^1].Bonus;
    }

    /// <summary>The rules themselves, in the order a reader meets them: levelling, then fighting, then luck.</summary>
    public static readonly IReadOnlyList<GameRule> Rules = new[]
    {
        new GameRule(
            "Experience does not level you up — training does",
            "The Training Centre compares your stored experience against the threshold for your next " +
            "level and charges you for the advance. Until you pay, the experience just sits there. " +
            "The maximum level is 200.",
            "Confirmed", "FUN_1000_2a3a"),

        new GameRule(
            "Two experience curves, split by class",
            "Knight, Cleric and Robber advance on one table; Paladin, Archer and Sorcerer on a more " +
            "expensive one. Both double the requirement every level up to level 8 and then add a " +
            "fixed amount per level, so the early levels come fast and the cost flattens into a long " +
            "straight climb rather than running away from you.",
            "Confirmed (the shape and the split; the exact constants are read off the decompile)",
            "FUN_1000_2a3a, tables at DS:0x15B5"),

        new GameRule(
            "Hit points per level = the class die plus an Endurance bonus",
            "Every level gained rolls the class's hit die once and adds the Endurance bonus below, " +
            "with a floor of 1. Endurance is therefore worth more than any other attribute over a " +
            "whole game: the difference between Endurance 13 and Endurance 40 is nine hit points a " +
            "level, every level, and below 9 the bonus goes negative.",
            "Confirmed", "the level-up routine at 1000:1bf7"),

        new GameRule(
            "To hit: a d20, with a natural 1 and a natural 20 decided in advance",
            "Each attack rolls a d20. A 20 always hits and a 1 always misses; anything else hits when " +
            "the roll plus your to-hit bonus reaches a threshold derived from the target's armour " +
            "class. Accuracy feeds the bonus on roughly the same scale Endurance feeds hit points — " +
            "about +1 to +7 from Accuracy 13 up to 40, and a penalty below 13.",
            "Confirmed", "roll_damage at 1000:c005, character setup at 1000:a3c2"),

        new GameRule(
            "Damage is rolled per landed hit, not per attack",
            "A hit deals the weapon's die plus a flat bonus made of the weapon's own bonus and a Might " +
            "table on the same scale as Accuracy's. Misses add nothing, and the running total " +
            "saturates at 255.",
            "Confirmed", "roll_damage at 1000:c005"),

        new GameRule(
            "Extra attacks are a fighter's reward for level 8",
            "Knights, Paladins and Archers get 1 + (level ÷ 8) attacks a round once they reach level " +
            "8. Everyone else attacks once, however high they train.",
            "Confirmed", "1000:a3c2"),

        new GameRule(
            "A monster's damage and attacks are stored, not computed",
            "The inspect screen prints a monster's maximum damage and number of attacks straight out " +
            "of its record, which is why the bestiary in this book can quote them exactly.",
            "Confirmed", "1000:d544"),

        new GameRule(
            "The dice are a shift register, and they are predictable",
            "Every roll comes from a 32-bit LFSR (feedback from bits 27 and 30) sampled down to the " +
            "range asked for, with a retry when the sample overshoots. It is fully deterministic: " +
            "given the current state, every upcoming roll is known — which is exactly what the " +
            "trainer's Roll Predictor tab does with it.",
            "Confirmed", "rand(n) at 1000:451b, state at DS:0x3BCE"),
    };
}
