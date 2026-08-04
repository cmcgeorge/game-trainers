namespace Civilization3ConquestsTrainer.Game;

/// <summary>One of the nine scenarios Conquests ships with.</summary>
public sealed record Conquest(string Name, string Era, string Setting, string Victory, string Note);

/// <summary>
/// One behaviour note for the References tab.
///
/// A record rather than a tuple on purpose: WPF resolves binding paths through properties only, and
/// <c>ValueTuple</c>'s <c>Item1</c>/<c>Item2</c> are public <i>fields</i>, so binding to a tuple
/// silently renders nothing.
/// </summary>
public sealed record BehaviourNote(string Topic, string Body);

/// <summary>
/// Static reference data for the References tab: the nine shipped conquests, and the handful of
/// game rules a player needs while editing.
///
/// Unlike <see cref="GameTables"/> this is <i>not</i> read from memory. A conquest's identity is
/// packaging, not program state — the game holds only whichever single scenario is loaded — so the
/// roster is curated here from the shipped <c>Conquests\</c> folder and the game manual.
/// </summary>
public static class ConquestBook
{
    /// <summary>
    /// The nine conquests, in the order the game numbers them in <c>Conquests\</c>. Taken from the
    /// shipped <c>.biq</c> filenames rather than from memory, because the fifth one is commonly
    /// misremembered — it is Mesoamerica, not an industrial-era scenario.
    /// </summary>
    public static IReadOnlyList<Conquest> All { get; } = new[]
    {
        new Conquest("Mesopotamia", "4000–1000 BC",
            "The Fertile Crescent — Sumer, Babylon, Egypt, the Hittites, Assyria and Persia",
            "Victory points, held to the turn limit",
            "The shortest conquest and the usual first one. Land is tight and rivers decide everything, so early city placement matters more than any later decision."),

        new Conquest("Rise of Rome", "280 BC – AD 100",
            "The Republic against Carthage, Greece, Egypt, Gaul, Germania, Parthia and Britannia",
            "Victory points, or outright conquest",
            "Legions and Carthaginian war elephants are the two units that decide the early game; Rome starts strong and has to convert that lead before the others consolidate."),

        new Conquest("Fall of Rome", "AD 350–600",
            "The Western and Eastern Empires against the Huns, Vandals, Franks, Goths and Sassanids",
            "The Empires win by holding their provinces; the invaders win by taking them",
            "The most asymmetric of the nine. As Rome you defend a long, thin, over-extended border with too few units — trading space for time is usually correct."),

        new Conquest("Middle Ages", "AD 1000–1500",
            "England, France, the Holy Roman Empire, Spain, Byzantium, the Arabs, Russia, the Turks and the Vikings",
            "Victory points at the time limit",
            "Culture does more work here than anywhere else: cathedrals and universities move borders that armies could not."),

        new Conquest("Mesoamerica", "AD 500 – 1500",
            "The Maya, Aztecs, Inca, Toltecs, Zapotecs and their neighbours in Central and South America",
            "Victory points, held to the turn limit",
            "No horses, no iron-age cavalry, and jungle everywhere — a completely different unit mix from the European conquests, and the one most people underestimate."),

        new Conquest("Age of Discovery", "AD 1492 – 1780",
            "Spain, Portugal, England, France and the Netherlands colonising the New World",
            "Victory points from colonies and the wealth shipped home",
            "Naval movement and coastal city sites dominate; a landlocked capital is close to a losing position."),

        new Conquest("Sengoku – Sword of the Shogun", "AD 1467 – 1600",
            "The Japanese warring-states period, fought between the daimyo clans",
            "Unify Japan — hold the victory-point provinces",
            "A tight land map with unique samurai units and almost no room for peaceful expansion. Expect to be at war from turn one."),

        new Conquest("Napoleonic Europe", "AD 1795 – 1815",
            "France against Britain, Austria, Prussia, Russia, Spain and the Ottomans",
            "France wins by dominating Europe; the coalition wins by containing it",
            "Large stacks and artillery. France's problem is not winning battles but winning them fast enough, before the coalition's combined economy tells."),

        new Conquest("WWII in the Pacific", "AD 1941 – 1945",
            "Japan against the United States, Britain, Australia, China and the Netherlands",
            "Japan wins by holding its perimeter to the turn limit; the Allies win by breaking it",
            "Carriers, submarines and island airbases — almost every decisive unit is naval or air, and land combat is mostly about taking and holding single-tile islands."),
    };

    /// <summary>What the game does with a treasury edit, and the other behaviour notes the UI links to.</summary>
    public static IReadOnlyList<BehaviourNote> Notes { get; } = new BehaviourNote[]
    {
        new("Treasury is obfuscated",
            "Civ3 never stores your gold as a single number. It keeps two fields whose sum is the treasury, " +
            "seeded differently for each civ every game. That is why a Cheat-Engine exact-value scan for the " +
            "number on your top bar finds nothing, and why this trainer locates the player structurally instead. " +
            "Edits write only the encoded half; the game's own key is left alone."),

        new("Gold per turn is not stored",
            "Net income is recomputed from your cities every turn rather than kept in a field, so there is " +
            "nothing to poke. Edit the treasury (and freeze it) instead."),

        new("Freeze exists because the game recomputes",
            "Civ3 rewrites unit movement and damage at the turn boundary and adjusts the treasury by income. " +
            "A one-shot poke survives until the next turn; ticking Freeze re-applies it every poll."),

        new("Unit damage, not hit points",
            "The unit record stores hit points *lost*, and the maximum comes from the unit type plus its " +
            "veteran level. \"Full heal\" therefore writes zero damage rather than a hit-point total."),

        new("Freezing a unit heals it — it cannot make it invincible",
            "Civ3 fights a whole battle inside one call: every round, the kill, and the score update " +
            "happen before the game hands control back. The trainer polls between frames, so there is " +
            "no moment during combat when it could step in. Freeze restores a unit that survived; it " +
            "cannot save one that lost. Nor is there a per-unit hit-point ceiling to raise — the game " +
            "computes maximum hit points from the unit type and veteran level rather than storing " +
            "them. Promoting to Elite is the one per-unit durability lever that exists, and it is " +
            "worth roughly one extra hit point over a Regular."),

        new("Movement is spent, not remaining",
            "The same inversion applies to movement: the field counts points already used this turn, so " +
            "\"Refresh moves\" writes zero."),

        new("Sliders are tens of percent",
            "Luxury, science and tax are stored as 0–10 and must total 10. Your government also caps the " +
            "maximum any one slider can reach — Despotism allows less than Democracy — so the game may clamp " +
            "an edit that this trainer accepted."),

        new("Multiplayer is read-only",
            "Writes are suppressed when the game reports a PBEM or offline-multiplayer session, because " +
            "editing one side of a shared game desynchronises it."),
    };
}
