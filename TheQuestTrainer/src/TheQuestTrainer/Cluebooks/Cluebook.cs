using TheQuestTrainer.Adventures;

namespace TheQuestTrainer.Cluebooks;

/// <summary>Where an id was mentioned, precise enough for a reader to go and look.</summary>
/// <param name="Kind">"Conversation", "Map object", "Item", "Spell".</param>
/// <param name="Who">The person, object or thing doing the mentioning.</param>
/// <param name="Where">The topic or field it turned up in.</param>
/// <param name="What">The line of text, when there is one.</param>
public sealed record Mention(string Kind, string Who, string Where, string What)
{
    public override string ToString() =>
        What.Length > 0 ? $"{Who} — {Where}: {What}" : $"{Who} — {Where}";
}

/// <summary>
/// Everything the adventure does with one quest, item or spell.
///
/// This is the spine of a walkthrough, and it falls out of the world's single flat namespace: every
/// object has one id, and every conversation, condition and map object that cares about it names it
/// by that id. Gathering the mentions is therefore exact, not a text search.
/// </summary>
public sealed class Dossier
{
    /// <summary>The id, e.g. <c>base_jeweler</c>.</summary>
    public required string Id { get; init; }

    /// <summary>What kind of thing it is: "Quest", "Item", "Spell".</summary>
    public required string Kind { get; init; }

    /// <summary>The name the game shows.</summary>
    public required string Name { get; init; }

    /// <summary>The description, where the object has one.</summary>
    public string Description { get; init; } = "";

    /// <summary>Everywhere the id is named.</summary>
    public List<Mention> Mentions { get; } = [];

    /// <summary>Whether anything in the adventure refers to it.</summary>
    public bool IsUsed => Mentions.Count > 0;
}

/// <summary>A chapter of the gazetteer: one map and what stands on it.</summary>
public sealed record MapChapter
{
    public required AdventureMap Map { get; init; }

    /// <summary>The map objects the map names, resolved against the object catalog where possible.</summary>
    public required IReadOnlyList<AdventureMapObject> Objects { get; init; }

    /// <summary>Ids the map names that are not in the object catalog — usually people, not objects.</summary>
    public required IReadOnlyList<string> UnresolvedIds { get; init; }

    /// <summary>The people whose id the map names.</summary>
    public required IReadOnlyList<AdventureNpc> People { get; init; }
}

/// <summary>What to put in a cluebook.</summary>
public sealed class CluebookOptions
{
    /// <summary>Include every map, including the empty sea cells an outdoor grid is padded with.</summary>
    public bool IncludeEmptyMaps { get; init; }

    /// <summary>Include the full item catalog. Freymore has 893 of them.</summary>
    public bool IncludeItems { get; init; } = true;

    /// <summary>Include every conversation in full.</summary>
    public bool IncludeConversations { get; init; } = true;

    /// <summary>Include the bestiary and the rules chapters.</summary>
    public bool IncludeReference { get; init; } = true;

    /// <summary>Draw the outdoor grid as a plan.</summary>
    public bool IncludeMap { get; init; } = true;

    /// <summary>Pixels per outdoor cell in the rendered plan.</summary>
    public int PlanCellSize { get; init; } = 92;
}

/// <summary>
/// A decompiled adventure, ready to render.
///
/// The shape follows the FRUA cluebook in the sibling <c>FruaEditor</c> project deliberately: an
/// overview, a chapter per place, a dossier per quest and item, and a notes section that says what
/// the reader should not trust. The two games have nothing in common technically, but a strategy
/// guide is a strategy guide.
/// </summary>
public sealed class Cluebook
{
    public required Adventure Adventure { get; init; }
    public required CluebookOptions Options { get; init; }

    /// <summary>One chapter per map, outdoor cells in grid order, then interiors by name.</summary>
    public required IReadOnlyList<MapChapter> Chapters { get; init; }

    /// <summary>Maps left out because nothing stands on them.</summary>
    public required IReadOnlyList<AdventureMap> EmptyMaps { get; init; }

    public required IReadOnlyList<Dossier> Quests { get; init; }
    public required IReadOnlyList<Dossier> Items { get; init; }
    public required IReadOnlyList<Dossier> Spells { get; init; }

    /// <summary>People who have something to say, in name order.</summary>
    public required IReadOnlyList<AdventureNpc> Speakers { get; init; }

    /// <summary>What the reader should know about how this was produced.</summary>
    public required IReadOnlyList<string> Notes { get; init; }

    /// <summary>Quests, items and spells anything refers to.</summary>
    public IEnumerable<Dossier> UsedQuests => Quests.Where(d => d.IsUsed);

    /// <inheritdoc cref="UsedQuests"/>
    public IEnumerable<Dossier> UsedItems => Items.Where(d => d.IsUsed);

    /// <inheritdoc cref="UsedQuests"/>
    public IEnumerable<Dossier> UsedSpells => Spells.Where(d => d.IsUsed);

    /// <summary>How many conversation topics the adventure holds, counting shared ones once.</summary>
    public int TopicCount =>
        Adventure.DialogPool.Count +
        Adventure.People.Sum(p => p.Dialog?.All.Count(t => !t.IsReference) ?? 0);

    /// <summary>Builds a cluebook from a decoded adventure.</summary>
    public static Cluebook Build(Adventure adventure, CluebookOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(adventure);
        options ??= new CluebookOptions();

        var dossiers = BuildDossiers(adventure);
        Gather(adventure, dossiers);

        var objectsById = new Dictionary<string, AdventureMapObject>(StringComparer.Ordinal);
        foreach (var o in adventure.MapObjects) objectsById.TryAdd(o.Id, o);

        var peopleById = new Dictionary<string, AdventureNpc>(StringComparer.Ordinal);
        foreach (var p in adventure.People) peopleById.TryAdd(p.Id, p);

        var chapters = new List<MapChapter>();
        var empty = new List<AdventureMap>();

        foreach (var map in Ordered(adventure))
        {
            var objects = new List<AdventureMapObject>();
            var people = new List<AdventureNpc>();
            var unresolved = new List<string>();

            foreach (string id in map.ObjectIds)
            {
                if (objectsById.TryGetValue(id, out var found)) objects.Add(found);
                else if (peopleById.TryGetValue(id, out var person)) people.Add(person);
                else unresolved.Add(id);
            }

            var chapter = new MapChapter
            {
                Map = map,
                Objects = objects,
                People = people,
                UnresolvedIds = unresolved,
            };

            bool bare = !map.HasPlacements && objects.Count == 0 && people.Count == 0 && unresolved.Count == 0;
            if (bare && !options.IncludeEmptyMaps) empty.Add(map);
            else chapters.Add(chapter);
        }

        return new Cluebook
        {
            Adventure = adventure,
            Options = options,
            Chapters = chapters,
            EmptyMaps = empty,
            Quests = [.. dossiers.Values.Where(d => d.Kind == "Quest").OrderBy(d => d.Name, StringComparer.CurrentCulture)],
            Items = [.. dossiers.Values.Where(d => d.Kind == "Item").OrderBy(d => d.Name, StringComparer.CurrentCulture)],
            Spells = [.. dossiers.Values.Where(d => d.Kind == "Spell").OrderBy(d => d.Name, StringComparer.CurrentCulture)],
            Speakers = [.. adventure.People.Where(p => p.Dialog is not null)
                                           .OrderBy(p => p.Name, StringComparer.CurrentCulture)
                                           .ThenBy(p => p.Id, StringComparer.Ordinal)],
            Notes = BuildNotes(adventure, chapters, empty),
        };
    }

    /// <summary>Outdoor cells row by row, then interiors alphabetically — the order a reader walks.</summary>
    private static IEnumerable<AdventureMap> Ordered(Adventure adventure) =>
        adventure.OutdoorMaps.OrderBy(m => m.Row).ThenBy(m => m.Column)
                 .Concat(adventure.Interiors.OrderBy(m => m.Name, StringComparer.CurrentCulture)
                                            .ThenBy(m => m.Id, StringComparer.Ordinal));

    private static Dictionary<string, Dossier> BuildDossiers(Adventure adventure)
    {
        var dossiers = new Dictionary<string, Dossier>(StringComparer.Ordinal);

        foreach (var q in adventure.Quests)
            dossiers.TryAdd(q.Id, new Dossier { Id = q.Id, Kind = "Quest", Name = q.Name, Description = q.Description });

        foreach (var i in adventure.Items)
            dossiers.TryAdd(i.Id, new Dossier { Id = i.Id, Kind = "Item", Name = i.Name, Description = i.Description });

        foreach (var s in adventure.Spells)
            dossiers.TryAdd(s.Id, new Dossier { Id = s.Id, Kind = "Spell", Name = s.Name, Description = s.Description });

        return dossiers;
    }

    /// <summary>
    /// Walks every conversation and map object and files each id it names.
    ///
    /// A dialog reply carries up to five symbols and up to four gates, and each is an id in the same
    /// namespace as the quests and items — so "who talks about the Slave Key" is an exact lookup
    /// rather than a search of the prose. What a reply *does* with the id is not recorded here,
    /// because the record does not say: see the note in <see cref="BuildNotes"/>.
    /// </summary>
    private static void Gather(Adventure adventure, Dictionary<string, Dossier> dossiers)
    {
        void File(string id, Mention mention)
        {
            if (id.Length > 0 && dossiers.TryGetValue(id, out var dossier)) dossier.Mentions.Add(mention);
        }

        foreach (var person in adventure.People)
        {
            if (person.Dialog is null) continue;

            foreach (var raw in person.Dialog.All)
            {
                var topic = adventure.ResolveTopic(raw);
                string where = topic.Topic.Length > 0 ? topic.Topic : topic.Id;

                File(topic.Gate, new Mention("Conversation", person.Name, where,
                                             "the topic only comes up when this is set"));

                foreach (var reply in topic.Replies)
                {
                    foreach (string symbol in reply.Symbols)
                        File(symbol, new Mention("Conversation", person.Name, where, reply.Text));
                    foreach (var choice in reply.Choices)
                        File(choice.Symbol, new Mention("Conversation", person.Name, where,
                                                        $"you can answer: {choice.Text}"));
                }
            }

            foreach (var (first, second) in person.Stock)
            {
                File(first, new Mention("Shop", person.Name, "sells", ""));
                File(second, new Mention("Shop", person.Name, "sells", ""));
            }
        }

        foreach (var item in adventure.Items)
        {
            File(item.SpellId, new Mention("Item", item.Name, "casts", ""));
            foreach (var effect in item.Effects.Where(e => e.IsNamed))
                File(effect.SourceId, new Mention("Item", item.Name, "carries", ""));
        }

        foreach (var spell in adventure.Spells)
            foreach (var effect in spell.Effects.Where(e => e.IsNamed))
                File(effect.SourceId, new Mention("Spell", spell.Name, "applies", ""));

        foreach (var mapObject in adventure.MapObjects)
            foreach (string text in mapObject.Text)
                File(text, new Mention("Map object", mapObject.Id, "refers to it", ""));
    }

    /// <summary>
    /// The honest part: what this cluebook knows and what it does not.
    ///
    /// Every claim here is one that would otherwise mislead a reader — a missing chapter that looks
    /// like an empty map, an id list that looks like a set of coordinates, a decode that failed.
    /// </summary>
    private static IReadOnlyList<string> BuildNotes(Adventure adventure, List<MapChapter> chapters,
                                                    List<AdventureMap> empty)
    {
        var notes = new List<string>
        {
            "Everything here was read out of the adventure's own data files, in the copy of the game " +
            "installed on this machine. Nothing from the game is reproduced beyond the text the " +
            "adventure itself holds.",

            "A map's entry lists the things the map names, not where they stand: the placement " +
            "record's own field layout has not been worked out, so this cluebook gives you the cast " +
            "of each place and leaves the coordinates to the game. A placed thing only carries an id " +
            "when it has one, so a map with scenery and no named objects will show an empty cast.",

            "A conversation shows what is said, what you may say back, and which ids each reply " +
            "names. It does not say whether a reply gives, takes or merely tests a thing — the " +
            "reply stores the id and a number, and what that number means has not been established.",
        };

        if (empty.Count > 0)
        {
            notes.Add(
                $"{empty.Count} map{(empty.Count == 1 ? " has" : "s have")} nothing placed on " +
                (empty.Count == 1 ? "it" : "them") + " at all, so " + (empty.Count == 1 ? "it has" : "they have") +
                " no chapter. They are listed at the end of the gazetteer; most are open sea.");
        }

        int interiors = chapters.Count(c => !c.Map.IsOutdoorCell);
        if (interiors > 0)
        {
            notes.Add(
                $"{interiors} of the places below are interiors. They have no square on the plan, " +
                "because the world's grid holds only the outdoor cells; you reach an interior by " +
                "walking into it from the cell it sits on.");
        }

        foreach (string warning in adventure.Warnings) notes.Add(warning);
        return notes;
    }
}
