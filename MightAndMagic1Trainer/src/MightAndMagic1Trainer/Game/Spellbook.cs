namespace MightAndMagic1Trainer.Game;

/// <summary>Which spell list a character draws from (decided by class).</summary>
public enum SpellSchool { None, Cleric, Sorcerer }

/// <summary>
/// One Might &amp; Magic 1 spell. A spell is identified in the cast menu by its
/// <see cref="Level"/> (1-7) and its <see cref="Number"/> within that level, which is
/// exactly what the trainer types: <c>{caster} c {Level} {Number} {ENTER}</c>.
///
/// Costs and descriptions are transcribed from the canonical MM1 spell list
/// (asimov.net mm1spell.txt, cross-checked against the manual). Some spells also
/// consume gems; <see cref="GemCost"/> is 0 when none are needed.
/// </summary>
public sealed record Spell(
    SpellSchool School, int Level, int Number, string Name, int SpCost, int GemCost, string Description)
{
    /// <summary>"L1 #3  Bless" — the in-menu coordinates plus name, for list display.</summary>
    public string Display => $"L{Level} #{Number}  {Name}";

    /// <summary>"2 SP" or "2 SP + 1 gem" — the resource cost, for display.</summary>
    public string CostText => GemCost <= 0
        ? $"{SpCost} SP"
        : $"{SpCost} SP + {GemCost} gem{(GemCost == 1 ? "" : "s")}";
}

/// <summary>
/// The complete MM1 spell tables and the class → school mapping. The trainer uses
/// these to offer a "pick a spell, I'll type it" UI: the caster's class selects the
/// school, the caster's known spell level (record offset 0x2F) gates which spells are
/// castable, and <see cref="Spell.Level"/>/<see cref="Spell.Number"/> become keystrokes.
/// </summary>
public static class Spellbook
{
    /// <summary>
    /// Maps a character class (record offset 0x14, 1..6) to the spell list it can cast.
    /// Knight (1) and Robber (6) cast nothing; Paladin (2) casts Cleric spells, Archer
    /// (3) casts Sorcerer spells, and the dedicated casters use their own lists.
    /// </summary>
    public static SpellSchool SchoolForClass(int characterClass) => characterClass switch
    {
        2 => SpellSchool.Cleric,    // Paladin
        3 => SpellSchool.Sorcerer,  // Archer
        4 => SpellSchool.Cleric,    // Cleric
        5 => SpellSchool.Sorcerer,  // Sorcerer
        _ => SpellSchool.None,      // Knight, Robber, (none)
    };

    public static string SchoolName(SpellSchool school) => school switch
    {
        SpellSchool.Cleric => "Cleric",
        SpellSchool.Sorcerer => "Sorcerer",
        _ => "none",
    };

    /// <summary>The spell list for a school, ordered by level then in-level number.</summary>
    public static IReadOnlyList<Spell> For(SpellSchool school) => school switch
    {
        SpellSchool.Cleric => Cleric,
        SpellSchool.Sorcerer => Sorcerer,
        _ => Array.Empty<Spell>(),
    };

    private static Spell C(int lvl, int num, string name, int sp, int gem, string desc)
        => new(SpellSchool.Cleric, lvl, num, name, sp, gem, desc);
    private static Spell S(int lvl, int num, string name, int sp, int gem, string desc)
        => new(SpellSchool.Sorcerer, lvl, num, name, sp, gem, desc);

    public static readonly IReadOnlyList<Spell> Cleric = new[]
    {
        // Level 1
        C(1, 1, "Awaken",                  1, 0, "Awakens sleeping party members."),
        C(1, 2, "Bless",                   1, 0, "Increases the party's fighting accuracy."),
        C(1, 3, "Blind",                   1, 0, "Blinds a monster."),
        C(1, 4, "First Aid",               1, 0, "Heals a character up to 8 HP."),
        C(1, 5, "Light",                   1, 0, "Lights up dark areas; stacks if cast repeatedly."),
        C(1, 6, "Power Cure",              2, 1, "Heals 1-10 HP per experience level of the caster."),
        C(1, 7, "Protection From Fear",    1, 0, "Protects the party from fear."),
        C(1, 8, "Turn Undead",             1, 0, "Destroys some or all undead monsters."),
        // Level 2
        C(2, 1, "Cure Wounds",             2, 0, "Heals a character up to 15 HP."),
        C(2, 2, "Heroism",                 3, 1, "Temporarily grants a character 6 HP and +2 experience levels."),
        C(2, 3, "Pain",                    2, 0, "Inflicts 2-12 HP of damage on a monster."),
        C(2, 4, "Protection From Cold",    2, 0, "Increases the party's cold resistance."),
        C(2, 5, "Protection From Fire",    2, 0, "Increases the party's fire resistance."),
        C(2, 6, "Protection From Poison",  2, 0, "Increases the party's poison resistance."),
        C(2, 7, "Silence",                 2, 0, "Prevents a monster from casting spells."),
        C(2, 8, "Suggestion",              2, 0, "Stops a monster from attacking."),
        // Level 3
        C(3, 1, "Create Food",             4, 1, "Creates 6 units of food."),
        C(3, 2, "Cure Blindness",          3, 0, "Cures blindness."),
        C(3, 3, "Cure Paralysis",          3, 0, "Cures paralysis."),
        C(3, 4, "Lasting Light",           3, 0, "Lights up areas with a stronger, longer-lasting glow."),
        C(3, 5, "Produce Flame",           3, 0, "Torches a monster for 3-18 HP."),
        C(3, 6, "Produce Frost",           3, 0, "Freezes a monster for 3-18 HP."),
        C(3, 7, "Remove Quest",            3, 0, "Releases the party from a quest."),
        C(3, 8, "Walk On Water",           4, 1, "Creates a floating path so the party can cross water."),
        // Level 4
        C(4, 1, "Cure Disease",            4, 0, "Cures disease."),
        C(4, 2, "Neutralize Poison",       4, 0, "Cures poison."),
        C(4, 3, "Protection From Acid",    4, 0, "Increases the party's acid resistance."),
        C(4, 4, "Protection From Electricity", 4, 0, "Increases the party's electrical resistance."),
        C(4, 5, "Restore Alignment",       6, 2, "Restores a character's original alignment."),
        C(4, 6, "Summon Lightning",        4, 0, "Zaps up to 3 monsters for 4-32 HP."),
        C(4, 7, "Super Heroism",           6, 2, "Temporarily grants a character 10 HP and +3 experience levels."),
        C(4, 8, "Surface",                 6, 2, "Transports the party to the surface."),
        // Level 5
        C(5, 1, "Deadly Swarm",            5, 0, "Sends killer insects at a monster for 2-20 HP."),
        C(5, 2, "Dispell Magic",           5, 0, "Cancels magic spells."),
        C(5, 3, "Paralyze",                5, 0, "Paralyzes monsters."),
        C(5, 4, "Remove Condition",        8, 3, "Removes undesirable conditions from a character."),
        C(5, 5, "Restore Energy",          8, 3, "Restores 1-5 drained experience levels."),
        // Level 6
        C(6, 1, "Moon Ray",                9, 4, "Heals each party member 3-30 HP and removes 3-30 HP from monsters."),
        C(6, 2, "Raise Dead",              9, 4, "Raises a dead character (may fail)."),
        C(6, 3, "Rejuvenate",              9, 4, "Makes a character 1-10 years younger."),
        C(6, 4, "Stone To Flesh",          9, 4, "Reanimates a stoned character."),
        C(6, 5, "Town Portal",             9, 4, "Transports the party to a town."),
        // Level 7
        C(7, 1, "Divine Intervention",    15, 10, "Restores all characters' HP and heals their conditions."),
        C(7, 2, "Holy Word",              10, 5, "Destroys all undead."),
        C(7, 3, "Protection From Elements", 10, 5, "Protects the party from fear, cold, fire and other elements."),
        C(7, 4, "Resurrection",           10, 5, "Restores an eradicated character but adds 10 years and -1 to stats."),
        C(7, 5, "Sun Ray",                10, 5, "Burns a monster for 50-100 HP."),
    };

    public static readonly IReadOnlyList<Spell> Sorcerer = new[]
    {
        // Level 1
        S(1, 1, "Awaken",          1, 0, "Awakens characters."),
        S(1, 2, "Detect Magic",    1, 0, "Detects magic items and auras."),
        S(1, 3, "Energy Blast",    2, 1, "Zaps a monster for 1-4 HP per caster level."),
        S(1, 4, "Flame Arrow",     1, 0, "Zaps a monster for 1-6 HP of fire damage."),
        S(1, 5, "Leather Skin",    1, 0, "Toughens all characters' skin (improves armor class)."),
        S(1, 6, "Light",           1, 0, "Lights up dark areas (as the Cleric Light spell)."),
        S(1, 7, "Location",        1, 0, "Reports the party's current location."),
        S(1, 8, "Sleep",           1, 0, "Lulls monsters to sleep."),
        // Level 2
        S(2, 1, "Electric Arrow",  2, 0, "Electrocutes a monster for 2-12 HP."),
        S(2, 2, "Hypnotize",       2, 0, "Prevents a monster from attacking."),
        S(2, 3, "Identify Monster", 3, 1, "Identifies a monster."),
        S(2, 4, "Jump",            2, 0, "The party jumps 2 squares forward."),
        S(2, 5, "Levitate",        2, 0, "Raises the party above the ground."),
        S(2, 6, "Power",           2, 0, "Raises a character's Might by 1-4 points."),
        S(2, 7, "Quickness",       2, 0, "Raises a character's Speed by 1-4 points."),
        S(2, 8, "Scare",           2, 0, "Puts fear into a monster."),
        // Level 3
        S(3, 1, "Fire Ball",       4, 1, "Zaps a group of monsters for 1-6 HP per caster level."),
        S(3, 2, "Fly",             3, 0, "The party flies in outdoor areas."),
        S(3, 3, "Invisibility",    4, 1, "The party becomes invisible."),
        S(3, 4, "Lightning Bolt",  4, 1, "Zaps monsters for 1-6 HP per caster level."),
        S(3, 5, "Make Room",       3, 0, "Lets 5 characters engage in hand-to-hand combat."),
        S(3, 6, "Slow",            3, 0, "Slows monsters."),
        S(3, 7, "Weaken",          4, 1, "Weakens all monsters by 2 HP and 1 AC."),
        S(3, 8, "Web",             3, 0, "Wraps 1-5 monsters and prevents them from attacking."),
        // Level 4
        S(4, 1, "Acid Arrow",      4, 0, "Zaps a monster for 3-30 HP."),
        S(4, 2, "Cold Beam",       4, 0, "Zaps a monster for 4-40 HP."),
        S(4, 3, "Feeble Mind",     6, 2, "Erases a monster's mind."),
        S(4, 4, "Freeze",          4, 0, "Freezes a monster and stops its attacks."),
        S(4, 5, "Guard Dog",       4, 0, "Prevents surprise attacks."),
        S(4, 6, "Psychic Protection", 6, 2, "Prevents mind-influencing attacks."),
        S(4, 7, "Shield",          6, 2, "Shields the party from missiles."),
        S(4, 8, "Time Distortion", 6, 2, "Allows the party to retreat from combat."),
        // Level 5
        S(5, 1, "Acid Rain",       5, 0, "Zaps monsters for 5-50 HP."),
        S(5, 2, "Dispell Magic",   5, 0, "Cancels all magic spells."),
        S(5, 3, "Finger Of Death", 8, 3, "Kills a monster."),
        S(5, 4, "Shelter",         8, 3, "Grants a free day without encounters."),
        S(5, 5, "Teleport",        8, 3, "Moves the party 9 squares in a random direction."),
        // Level 6
        S(6, 1, "Dancing Sword",   9, 4, "Zaps monsters for 1-30 HP."),
        S(6, 2, "Disintegration",  9, 4, "Destroys a monster."),
        S(6, 3, "Etherealize",     9, 4, "Moves the party through barriers."),
        S(6, 4, "Protection From Magic", 9, 4, "Protects the party from magic."),
        S(6, 5, "Recharge Item",   9, 4, "Recharges an item by 1-4 charges (the item may be destroyed)."),
        // Level 7
        S(7, 1, "Astral Spell",    10, 5, "Transports the party to the astral plane."),
        // Duplication's 105 SP / 100 gems is the canonical game cost, not a typo — leave as-is.
        S(7, 2, "Duplication",     105, 100, "Duplicates any item."),
        S(7, 3, "Meteor Shower",   10, 5, "Zaps monsters for 1-120 HP."),
        S(7, 4, "Power Shield",    10, 5, "Reduces damage to the party by half."),
        S(7, 5, "Prismatic Light", 10, 5, "Has an unpredictable effect on monsters."),
    };
}
