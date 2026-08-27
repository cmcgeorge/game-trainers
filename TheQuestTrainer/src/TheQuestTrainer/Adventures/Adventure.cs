using TheQuestTrainer.Game;

namespace TheQuestTrainer.Adventures;

/// <summary>An effect a spell, item or NPC type carries: a source id and the numbers behind it.</summary>
/// <param name="SourceId">What the effect comes from — a spell id, an ability id, a disease id.</param>
public sealed record AdventureEffect(string SourceId, ushort A, ushort B, ushort C, ushort D,
                                     byte E, byte F, byte G, byte H)
{
    /// <summary>Whether the effect names anything at all. Empty ones are padding in the data.</summary>
    public bool IsNamed => SourceId.Length > 0;
}

/// <summary>
/// One map of the world: an outdoor grid cell or a standalone interior.
///
/// This is the offline twin of <see cref="WorldMap"/>, which reads the same fields out of the
/// running game. The two agree by construction — the flag word and the id are the same bytes — and
/// the harness checks one against the other.
/// </summary>
public sealed record AdventureMap
{
    /// <summary>The database record holding this map's tiles.</summary>
    public required int RecordId { get; init; }

    /// <summary>The internal id, e.g. <c>base_s0804</c> or <c>base_house7</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The name the game shows, e.g. <c>Port of Mithria</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The map's flag word — see the <c>Flag*</c> constants on <see cref="MapLayout"/>.</summary>
    public required ushort Flags { get; init; }

    /// <summary>One-based column of the outdoor grid, or null for an interior.</summary>
    public required int? Column { get; init; }

    /// <summary>One-based row of the outdoor grid, or null for an interior.</summary>
    public required int? Row { get; init; }

    /// <summary>Ids of the map objects placed on this map, in the order the map lists them.</summary>
    public IReadOnlyList<string> ObjectIds { get; init; } = [];

    /// <summary>
    /// Whether the map has a placement record at all.
    ///
    /// This is not the same as <see cref="ObjectIds"/> being empty: a placement entry only carries an
    /// id when the thing it places has one, so a map can be full of nameless scenery and still name
    /// nothing. A map with no placement record is genuinely bare.
    /// </summary>
    public bool HasPlacements { get; init; }

    /// <summary>
    /// Side length in tiles. The engine derives it from the flag word rather than storing it: an
    /// outdoor cell is 21×21, an interior 35×35. <c>FUN_004BF830</c> does exactly this.
    /// </summary>
    public int Tiles => (Flags & MapLayout.FlagOffsetByBorder) != 0 ? MapLayout.GridMapTiles : 35;

    /// <summary>Whether the map is a cell of the outdoor grid.</summary>
    public bool IsOutdoorCell => Column is not null && Row is not null;

    /// <summary>Where the map's north-west corner sits in world-absolute tiles, for a grid map.</summary>
    public int? OriginX => Column is { } c ? MapLayout.CellOriginTile(c) : null;

    /// <inheritdoc cref="OriginX"/>
    public int? OriginY => Row is { } r ? MapLayout.CellOriginTile(r) : null;

    /// <summary>"8, 4" for an outdoor cell, a dash for an interior.</summary>
    public string CellLabel => IsOutdoorCell ? $"{Column}, {Row}" : "—";

    /// <summary>"21×21", for the gazetteer.</summary>
    public string SizeLabel => $"{Tiles}×{Tiles}";

    /// <summary>What the flag word says, in the game's own terms.</summary>
    public string Notes
    {
        get
        {
            var parts = new List<string>();
            if ((Flags & MapLayout.FlagTeleportDenied) != 0) parts.Add("Teleport magic denied");
            if ((Flags & MapLayout.FlagMarkDenied) != 0) parts.Add("Mark denied");
            if ((Flags & MapLayout.FlagRecallTarget) != 0) parts.Add("Recall target");
            return string.Join(" · ", parts);
        }
    }
}

/// <summary>One quest, exactly as the game's own quest log holds it.</summary>
public sealed record AdventureQuest(string Id, string Name, string Description);

/// <summary>
/// One item type. The numeric fields are the same ones the item panel prints, and the offsets they
/// were written from are the ones <c>docs/ReverseEngineering.md</c> §15.3 already documents for the
/// live object — the serializer and the object are the same fields in the same order.
/// </summary>
public sealed record AdventureItem
{
    /// <summary>The internal id, e.g. <c>base_weap_steelpoleaxe</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The displayed name.</summary>
    public required string Name { get; init; }

    /// <summary>The description shown when the item is read or examined. Often empty.</summary>
    public required string Description { get; init; }

    /// <summary>The spell an item casts, by id. Empty for an ordinary item.</summary>
    public required string SpellId { get; init; }

    /// <summary>Value in gold.</summary>
    public required uint Value { get; init; }

    /// <summary>Weight in hundredths, as the panel prints it.</summary>
    public required ushort Weight { get; init; }

    /// <summary>Damage range. Both zero for anything that is not a weapon.</summary>
    public required ushort DamageMin { get; init; }

    /// <inheritdoc cref="DamageMin"/>
    public required ushort DamageMax { get; init; }

    /// <summary>Armour value.</summary>
    public required ushort Armour { get; init; }

    /// <summary>How much enchantment the item can hold.</summary>
    public required ushort EnchantStorage { get; init; }

    /// <summary>Full condition, i.e. the denominator of the item panel's <c>Condition: %u/%u</c>.</summary>
    public required ushort MaxCondition { get; init; }

    /// <summary>Category 1..15 — see <see cref="ItemTables"/>.</summary>
    public required byte Category { get; init; }

    /// <summary>Sub-type within the category.</summary>
    public required byte Subtype { get; init; }

    /// <summary>Required alignment: 1 good, 2 evil, 0 either.</summary>
    public required byte Alignment { get; init; }

    /// <summary>Flag bits; bit 1 marks a category-1 weapon as light.</summary>
    public required byte Flags { get; init; }

    /// <summary>The effects the type carries in its own right.</summary>
    public IReadOnlyList<AdventureEffect> Effects { get; init; } = [];

    /// <summary>The game's own word for the category, or the bare number when it is out of range.</summary>
    public string CategoryName => ItemTables.CategoryName(Category);

    /// <summary>The game's own word for the sub-type.</summary>
    public string SubtypeName => ItemTables.SubtypeName(Category, Subtype);
}

/// <summary>One spell, with the text the spellbook shows.</summary>
public sealed record AdventureSpell
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// <summary>Mana cost, casting difficulty and duration, in the order the record stores them.</summary>
    public required ushort Cost { get; init; }

    /// <inheritdoc cref="Cost"/>
    public required ushort Difficulty { get; init; }

    /// <inheritdoc cref="Cost"/>
    public required ushort Duration { get; init; }

    /// <summary>The effects the spell applies.</summary>
    public IReadOnlyList<AdventureEffect> Effects { get; init; } = [];
}

/// <summary>One monster type, with the singular and plural names the combat log uses.</summary>
public sealed record AdventureMonster
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string PluralName { get; init; }

    /// <summary>The ten words the record holds for the creature's numbers, in stored order.</summary>
    public required IReadOnlyList<ushort> Stats { get; init; }

    /// <summary>Hit points.</summary>
    public required int Health { get; init; }
}

/// <summary>One NPC type: the template a placed NPC points at for its abilities and numbers.</summary>
public sealed record AdventureNpcType
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    /// <summary>The ten words the record holds for the type's numbers, in stored order.</summary>
    public required IReadOnlyList<ushort> Stats { get; init; }

    /// <summary>The effects the type carries.</summary>
    public IReadOnlyList<AdventureEffect> Effects { get; init; } = [];
}

/// <summary>
/// One thing the player may say back, and the id that choice carries.
/// </summary>
/// <param name="Text">The wording of the option, e.g. <c>All right. What do I have to do?</c>.</param>
/// <param name="Symbol">The id the choice names, when it names one.</param>
public sealed record DialogChoice(string Text, string Symbol);

/// <summary>
/// What a character says in reply to one topic, the ids the reply touches, and what the player may
/// say back.
///
/// A reply stores up to five ids and up to four follow-up options; the game's own reader
/// (<c>FUN_00513540</c>) reads them in that order around the line of speech. <b>What the reply does
/// with an id is not in the record</b> — it keeps the id and a number, and the number's meaning was
/// not established — so <see cref="Symbols"/> says "this reply is about that thing", nothing more.
/// </summary>
public sealed record DialogReply
{
    /// <summary>What the character says.</summary>
    public required string Text { get; init; }

    /// <summary>The ids this reply names: a quest, an item, a global flag.</summary>
    public IReadOnlyList<string> Symbols { get; init; } = [];

    /// <summary>What the player may say back.</summary>
    public IReadOnlyList<DialogChoice> Choices { get; init; } = [];
}

/// <summary>One topic of a conversation: what the player can ask, and what comes back.</summary>
public sealed record DialogTopic
{
    /// <summary>The topic's id. When the entry is a reference, this names an entry in the shared pool.</summary>
    public required string Id { get; init; }

    /// <summary>
    /// Whether this entry is a reference into the shared dialog pool rather than a copy of the text.
    /// The engine reads the text only for the other kind, which is why a referenced topic here has
    /// no wording of its own until <see cref="Adventure.ResolveTopic"/> looks it up.
    /// </summary>
    public required bool IsReference { get; init; }

    /// <summary>The menu label, e.g. <c>About the village</c>.</summary>
    public string Topic { get; init; } = "";

    /// <summary>What the player actually says, e.g. <c>Can you tell me something about your village?</c>.</summary>
    public string Question { get; init; } = "";

    /// <summary>The script that decides whether the topic appears at all.</summary>
    public string Gate { get; init; } = "";

    /// <summary>The replies, in order.</summary>
    public IReadOnlyList<DialogReply> Replies { get; init; } = [];

    /// <summary>Whether this entry carries any wording of its own.</summary>
    public bool HasText => Topic.Length > 0 || Question.Length > 0 || Replies.Count > 0;
}

/// <summary>A conversation: the topics on offer, and the ones the engine keeps in a second list.</summary>
public sealed record Dialog(IReadOnlyList<DialogTopic> Topics, IReadOnlyList<DialogTopic> Extra)
{
    /// <summary>Both lists, in order.</summary>
    public IEnumerable<DialogTopic> All => Topics.Concat(Extra);
}

/// <summary>One placed NPC: a person in the world, with their conversation and their shop stock.</summary>
public sealed record AdventureNpc
{
    /// <summary>The internal id, e.g. <c>base_holyman</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The name shown over their head.</summary>
    public required string Name { get; init; }

    /// <summary>The NPC type they are built from, when the record names one.</summary>
    public required string TypeId { get; init; }

    /// <summary>Gold they carry, which is also a shopkeeper's purse.</summary>
    public required uint Gold { get; init; }

    /// <summary>What they sell, as (id, id) pairs from the record's stock list.</summary>
    public IReadOnlyList<(string First, string Second)> Stock { get; init; } = [];

    /// <summary>Their conversation, or null when they have none.</summary>
    public Dialog? Dialog { get; init; }
}

/// <summary>
/// One map object — a door, a chest, a sign, a teleport, a lever.
///
/// Only the id and one flag are common to every kind; everything else is in a blob whose shape
/// depends on the derived class, and the derived classes were not traced. The blob is kept so its
/// text can be harvested, and <see cref="Text"/> is what that harvest found.
/// </summary>
public sealed record AdventureMapObject
{
    public required string Id { get; init; }

    /// <summary>The one byte the base class stores beyond the id.</summary>
    public required byte Kind { get; init; }

    /// <summary>The undecoded per-kind payload.</summary>
    public required byte[] Payload { get; init; }

    /// <summary>Readable text found in the payload — a sign's wording, a chest's message.</summary>
    public IReadOnlyList<string> Text { get; init; } = [];
}

/// <summary>A race the player can be, with the flavour text the character generator shows.</summary>
public sealed record AdventureRace(string Id, string Name, string Description);

/// <summary>A skill, with its description.</summary>
public sealed record AdventureSkill(string Id, string Name, string Description);

/// <summary>An attribute, with the three-letter form the status panel uses.</summary>
public sealed record AdventureAttribute(string Id, string Name, string Abbreviation);

/// <summary>
/// One adventure: a world database decoded as far as this reader understands it.
///
/// "Adventure" is the game's own word for what a <c>.pak</c> ships — the base game is one, each
/// expansion another — and each is exactly one <c>ThQW</c> database inside that pak.
/// </summary>
public sealed class Adventure
{
    /// <summary>The pak the world came out of.</summary>
    public required string SourcePath { get; init; }

    /// <summary>The database name, e.g. <c>TheQuestBase</c>.</summary>
    public required string Database { get; init; }

    /// <summary>The world's displayed name, e.g. <c>Freymore</c> or <c>Islands of Ice and Fire</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The resource pack prefix, e.g. <c>base</c>.</summary>
    public required string Pack { get; init; }

    /// <summary>The prefix an outdoor cell's id is built from, e.g. <c>base_s</c>.</summary>
    public required string GridPrefix { get; init; }

    /// <summary>Cells across the outdoor grid.</summary>
    public required int GridWidth { get; init; }

    /// <summary>Cells down the outdoor grid.</summary>
    public required int GridHeight { get; init; }

    /// <summary>The serialization version the header declares.</summary>
    public required int FormatVersion { get; init; }

    public IReadOnlyList<AdventureMap> Maps { get; init; } = [];
    public IReadOnlyList<AdventureQuest> Quests { get; init; } = [];
    public IReadOnlyList<AdventureItem> Items { get; init; } = [];
    public IReadOnlyList<AdventureSpell> Spells { get; init; } = [];
    public IReadOnlyList<AdventureMonster> Monsters { get; init; } = [];
    public IReadOnlyList<AdventureNpcType> NpcTypes { get; init; } = [];
    public IReadOnlyList<AdventureNpc> People { get; init; } = [];
    public IReadOnlyList<AdventureMapObject> MapObjects { get; init; } = [];
    public IReadOnlyList<AdventureRace> Races { get; init; } = [];
    public IReadOnlyList<AdventureSkill> Skills { get; init; } = [];
    public IReadOnlyList<AdventureAttribute> Attributes { get; init; } = [];

    /// <summary>The shared dialog pool, keyed by topic id. NPC conversations reference it.</summary>
    public IReadOnlyDictionary<string, DialogTopic> DialogPool { get; init; } =
        new Dictionary<string, DialogTopic>();

    /// <summary>Anything the reader could not decode, in the order it was met.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Outdoor cells only, in reading order.</summary>
    public IEnumerable<AdventureMap> OutdoorMaps => Maps.Where(m => m.IsOutdoorCell);

    /// <summary>Interiors only.</summary>
    public IEnumerable<AdventureMap> Interiors => Maps.Where(m => !m.IsOutdoorCell);

    /// <summary>
    /// A topic with its wording filled in: a referenced entry carries only an id, and the words live
    /// once in the shared pool. Returns the entry unchanged when it already has text or the pool
    /// does not know the id.
    /// </summary>
    public DialogTopic ResolveTopic(DialogTopic topic)
    {
        ArgumentNullException.ThrowIfNull(topic);
        if (topic.HasText) return topic;
        return DialogPool.TryGetValue(topic.Id, out var shared) ? shared : topic;
    }
}
