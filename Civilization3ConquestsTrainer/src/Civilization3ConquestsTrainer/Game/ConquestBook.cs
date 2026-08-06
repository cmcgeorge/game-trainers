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

        new("Changing a unit's type works, but not its picture",
            "A unit stores its type as a single number indexing the loaded ruleset's unit table, and the " +
            "game looks up everything else through it — attack, defence, movement, maximum hit points, " +
            "abilities, and which orders the unit is offered — every time it needs them. So writing that " +
            "field really does change what the unit is, immediately and in full.\n\n" +
            "One thing does not follow: the artwork. Civ3 chooses a unit's animation when the unit is " +
            "created, building it from the type, the owner's era and its civilization and storing it in " +
            "the unit itself. A retyped unit therefore keeps the picture it was born with, even though it " +
            "fights as its new type. Damage is cleared when the type changes, because the maximum is " +
            "derived from the type and a unit carrying damage from a bigger type would otherwise be past " +
            "dead. The owner's internal per-type tallies are not corrected either — the game maintains " +
            "those when units are created and destroyed, so they drift by one per change.\n\n" +
            "This is not how the game upgrades a unit, and that is worth knowing: Civ3's own upgrade " +
            "spawns a brand-new unit of the new type, copies the name, veteran level and passengers " +
            "across, and destroys the original. The trainer cannot do that — creating a unit is a heap " +
            "allocation the game performs, not a value a trainer can write."),

        new("Armies: the trainer makes a leader, the game makes the army",
            "An army in Civ3 is an ordinary unit whose type carries the Army ability, and the ruleset " +
            "names which type that is. So the Type column can turn any unit into an army in one write — " +
            "and what you get is an empty shell, because what makes an army useful is the linkage " +
            "recording which units are inside it, which the game maintains and the trainer does not " +
            "imitate.\n\n" +
            "\"Make great leader\" takes the other route. It writes the ruleset's great-leader type onto " +
            "the selected unit, and the game does the rest: whether to offer the Build Army order is " +
            "decided by testing the Leader ability against the unit's current type, so a retyped unit " +
            "qualifies at once. Give it that order in the game and Civ3 consumes the leader and spawns a " +
            "real army through the same code path a leader won in battle would use. Then move units onto " +
            "the army's tile and load them with the game's own order. Everything after the one number the " +
            "trainer writes is the game's own work, which is exactly why this is the supported route.\n\n" +
            "The two type ids are read out of the loaded ruleset rather than hard-coded, and neither is " +
            "believed unless the type it names actually carries the matching ability — so a mod that " +
            "moved things around switches the feature off instead of acting on a wrong number."),

        new("Movement is spent, not remaining",
            "The same inversion applies to movement: the field counts points already used this turn, so " +
            "\"Refresh moves\" writes zero. Movement is stored in thirds, so a unit that has spent one " +
            "whole move reads 3 — the same scale the game uses to make roads cost a third of a point."),

        new("Worker progress counts up, and pools across a tile",
            "Job progress is the one unit field that reads the way you would expect: it counts worker-turns " +
            "already done, upward toward the job's cost, so a bigger number is closer to finished. The cost " +
            "is the loaded ruleset's figure for that job — Road 6, Irrigation 8, Mine 12 in the epic game — " +
            "multiplied by how awkward the tile is. The game adds up the progress of every unit standing on " +
            "the tile doing the same job, which is why several workers finish something together, and why " +
            "\"Finish worker jobs\" only has to write to one worker of a stack.\n\n" +
            "Banking work does not finish a job on the spot, and it is worth knowing why. The game tests " +
            "whether a job is complete only while a worker is actually putting a turn of work into it, and " +
            "that costs the worker its entire move — one tick per turn, so one check per turn. Banked work " +
            "therefore lands at the start of your next turn, and a job that was already due next turn " +
            "cannot get any shorter. To collect it immediately, tick \"Hold my units' moves at 0\" and " +
            "re-issue the worker's order: the returned movement buys a second tick this turn, and the check " +
            "runs again with the work already there.\n\n" +
            "Completing a job also clears the worker's banked work — the game zeroes the progress and the " +
            "job id on every unit standing there — so nothing carries into the next job. That is what " +
            "\"Keep worker jobs banked\" is for: it re-banks automatically on every poll, so with both " +
            "toggles on the whole loop is \"order it, order it again\", as many times in a turn as you like."),

        new("\"Instant worker jobs\" speeds up the AI too — but only while it is switched on",
            "That toggle rewrites the cost of every terrain job in the loaded ruleset, and a ruleset belongs " +
            "to the game rather than to a player — so every civ's workers get the same speed-up. It is the " +
            "same reason this trainer will not buff a unit type's defence to fake invincibility. It is safe " +
            "to use anyway because it is reversible: the original costs are remembered when you switch it on " +
            "and written back when you switch it off, detach, or close the trainer.\n\n" +
            "The timing matters, and it is better than it sounds. The game does not decide a job's cost when " +
            "the job starts — it re-reads the table every time a worker puts in a turn of work. AI workers do " +
            "that during the AI's turn, which runs after you end yours, so a toggle that is off at the moment " +
            "you end the turn does not reach them. Your own worker puts in a turn of work at the moment you " +
            "give it the order, which is while the toggle is still on; re-issuing a job adds to its progress " +
            "rather than resetting it, so telling a working unit to do the same job again is safe.\n\n" +
            "For an edge that is unambiguously yours alone, use \"Finish worker jobs\" instead: it writes to " +
            "your own units only, and needs no timing discipline at all. Save with the toggle off — Civ3 " +
            "saves carry a rules section, and no one has checked whether an edited cost rides along in it."),

        new("Sliders are tens of percent",
            "Luxury, science and tax are stored as 0–10 and must total 10. Your government also caps the " +
            "maximum any one slider can reach — Despotism allows less than Democracy — so the game may clamp " +
            "an edit that this trainer accepted."),

        new("The AI really does have thirty units on turn five",
            "On the higher difficulties the AI civilizations are handed a large free army before the game " +
            "even starts, and the Players tab reports it honestly. The loaded ruleset's own difficulty " +
            "table says what each level grants: Regent and below give nothing, Monarch gives 2 defensive " +
            "and 1 offensive unit, and Sid — the top level — gives every AI civ 12 defensive units, 6 " +
            "offensive ones, 2 extra settlers and 4 extra workers, plus 24 free unit support and a cost " +
            "factor of 4 against your 10. You get none of it. So an opening of three units against " +
            "twenty-eight is not a misread: it is the handicap you chose.\n\n" +
            "This was checked rather than assumed. Counting units per civ out of the game's own unit " +
            "container and comparing against each leader's separately stored unit count agreed exactly " +
            "for all thirteen civs — two unrelated structures in memory, so agreement is not " +
            "self-confirming."),

        new("Multiplayer is read-only",
            "Writes are suppressed when the game reports a PBEM or offline-multiplayer session, because " +
            "editing one side of a shared game desynchronises it."),
    };
}
