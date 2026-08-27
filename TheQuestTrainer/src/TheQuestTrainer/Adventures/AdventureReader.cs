using System.Text;
using TheQuestTrainer.Game;

namespace TheQuestTrainer.Adventures;

/// <summary>
/// Decodes a Quest world database into an <see cref="Adventure"/>.
///
/// <b>The order is the format.</b> The engine's own loader (<c>FUN_004C53C0</c>) opens the header by
/// record id, then walks the records <i>in order</i> from <see cref="AdventureLayout.FirstContentRecordId"/>,
/// each collection consuming as many following records as its own index record declares. A record's
/// first byte is a class tag, but tags are only meaningful along that walk: the per-map tile and
/// image records that come later are raw data whose first byte can be anything, and several of them
/// happen to start with an item's or a spell's tag. Reading by tag alone finds those and decodes
/// nonsense.
///
/// So this reader walks in order too, and stops the object phase at the map list — the last thing
/// the engine loads before the per-map data begins. Everything before it is one tagged object per
/// record; everything after belongs to a map and is reached through the map list's record ids.
///
/// Every object is parsed by the same field order its serializer in <c>TheQuest.exe</c> reads, which
/// means every parse ends exactly at the end of its record. That is the check: a record left with
/// four or more bytes unread is a record this code has misunderstood, and it becomes a warning
/// rather than a silently truncated entry.
/// </summary>
public static class AdventureReader
{
    /// <summary>The most records to walk before giving up. Freymore uses 3,000.</summary>
    private const int MaxRecords = 65_536;

    /// <summary>The most entries any one collection may declare, as a guard against a bad count.</summary>
    private const int MaxCollectionEntries = 8192;

    /// <summary>Record ids a map owns: tiles, terrain, two undecoded, and the placement list.</summary>
    public const int RecordsPerMap = 5;

    /// <summary>Which of a map's five records holds the list of objects placed on it.</summary>
    public const int PlacementRecordOffset = 3;

    /// <summary>Id slots a dialog reply carries, whether or not each is filled.</summary>
    public const int ReplySymbolSlots = 5;

    /// <summary>Follow-up options a dialog reply carries.</summary>
    public const int ReplyChoiceSlots = 4;

    /// <summary>Shortest run of printable bytes that counts as text when harvesting a payload.</summary>
    public const int MinHarvestedTextLength = 4;

    /// <summary>
    /// Reads <paramref name="database"/>.
    /// </summary>
    /// <param name="database">A <c>ThQW</c> database.</param>
    /// <param name="sourcePath">Where it came from, for the cluebook's provenance line.</param>
    /// <param name="why">Set when the return is null.</param>
    public static Adventure? Read(PalmDatabase database, string sourcePath, out string why)
    {
        ArgumentNullException.ThrowIfNull(database);
        why = "";

        var byId = new Dictionary<int, PalmRecord>();
        foreach (var record in database.Records) byId.TryAdd(record.UniqueId, record);

        if (!byId.TryGetValue(AdventureLayout.HeaderRecordId, out var headerRecord))
        {
            why = $"there is no record {AdventureLayout.HeaderRecordId}, so this is not a world database";
            return null;
        }

        var warnings = new List<string>();
        WorldHeader header;
        try
        {
            header = ReadHeader(database.Open(headerRecord));
        }
        catch (ArchiveException e)
        {
            why = $"the header record did not decode: {e.Message}";
            return null;
        }

        if (header.Version < AdventureLayout.OldestSupportedVersion)
        {
            why = $"format version {header.Version} is older than anything this reader has been " +
                  $"checked against (it knows {AdventureLayout.OldestSupportedVersion} and up)";
            return null;
        }
        if (header.Version > AdventureLayout.KnownVersion)
        {
            warnings.Add(
                $"The world declares format version {header.Version}; this reader was written " +
                $"against {AdventureLayout.KnownVersion}. Fields added since then are not shown, " +
                "and anything below may be wrong.");
        }

        var state = new Walk(database, header.Version, header.GridPrefix, warnings);
        state.Run(database, headerRecord.Index);

        return new Adventure
        {
            SourcePath = sourcePath,
            Database = database.Name,
            Name = header.Name,
            Pack = header.Pack,
            GridPrefix = header.GridPrefix,
            GridWidth = header.GridWidth,
            GridHeight = header.GridHeight,
            FormatVersion = header.Version,
            Maps = state.Maps,
            Quests = state.Quests,
            Items = state.Items,
            Spells = state.Spells,
            Monsters = state.Monsters,
            NpcTypes = state.NpcTypes,
            People = state.People,
            MapObjects = state.MapObjects,
            Races = state.Races,
            Skills = state.Skills,
            Attributes = state.Attributes,
            DialogPool = state.DialogPool,
            Warnings = warnings,
        };
    }

    // ---- the header ---------------------------------------------------------------------------

    /// <summary>What record 3999 says about a world, without decoding any of it.</summary>
    /// <param name="Name">The world's displayed name, e.g. <c>Freymore</c>.</param>
    /// <param name="Pack">The resource pack prefix, e.g. <c>base</c>.</param>
    /// <param name="Database">The database name, e.g. <c>TheQuestBase</c>.</param>
    /// <param name="GridPrefix">The prefix an outdoor cell's id is built from. Empty for a resource database.</param>
    /// <param name="GridWidth">Cells across the outdoor grid.</param>
    /// <param name="GridHeight">Cells down the outdoor grid.</param>
    /// <param name="Version">The serialization version, which decides every other record's field set.</param>
    public readonly record struct WorldHeader(string Name, string Pack, string Database, string GridPrefix,
                                              int GridWidth, int GridHeight, int Version);

    /// <summary>
    /// Reads only record 3999.
    ///
    /// This is what naming an adventure in a list needs, and it is two orders of magnitude cheaper
    /// than decoding the world: <see cref="AdventureCatalog.Find"/> runs on the UI thread while the
    /// trainer is attaching, and Freymore's 2,973 records are not something to walk there.
    /// </summary>
    /// <param name="database">A <c>ThQW</c> database.</param>
    /// <param name="why">Set when the return is null.</param>
    public static WorldHeader? Describe(PalmDatabase database, out string why)
    {
        ArgumentNullException.ThrowIfNull(database);
        why = "";

        foreach (var record in database.Records)
        {
            if (record.UniqueId != AdventureLayout.HeaderRecordId) continue;
            try
            {
                return ReadHeader(database.Open(record));
            }
            catch (ArchiveException e)
            {
                why = $"the header record did not decode: {e.Message}";
                return null;
            }
        }

        why = $"there is no record {AdventureLayout.HeaderRecordId}, so this is not a world database";
        return null;
    }

    /// <summary>
    /// Record 3999, the world header. The engine reads it before anything else and the version it
    /// carries decides which fields every other record has.
    ///
    /// The first word is written from uninitialised memory — it reads <c>CD CD CD CD</c> in every
    /// shipped database — so it is read and thrown away, exactly as the engine does.
    /// </summary>
    private static WorldHeader ReadHeader(RecordArchive a)
    {
        a.ReadUInt32();

        byte m0 = a.ReadByte(), m1 = a.ReadByte(), m2 = a.ReadByte();
        if (m0 != AdventureLayout.HeaderMagic0 || m1 != AdventureLayout.HeaderMagic1 ||
            m2 != AdventureLayout.HeaderMagic2)
        {
            throw new ArchiveException($"header magic is {m0:X2} {m1:X2} {m2:X2}, not 9E 49 11");
        }

        int version = a.ReadByte();
        a.ReadByte();
        a.ReadByte();

        string name = a.ReadString();
        string pack = a.ReadString();
        string database = a.ReadString();

        // Both of these appeared at version 0x77 and 0x7A; older headers stop before them.
        if (version >= 0x77) a.ReadByte();
        if (version >= 0x7A) a.ReadByte();

        int width = a.ReadUInt16();
        int height = a.ReadUInt16();
        string gridPrefix = a.ReadString();

        return new WorldHeader(name, pack, database, gridPrefix, width, height, version);
    }

    // ---- the walk -----------------------------------------------------------------------------

    /// <summary>
    /// The record walk and everything it collects. Kept as a class so the parsers can add a warning
    /// without threading a list through every call.
    /// </summary>
    private sealed class Walk(PalmDatabase database, int version, string gridPrefix, List<string> warnings)
    {
        private readonly PalmDatabase _db = database;
        private readonly int _version = version;
        private readonly string _gridPrefix = gridPrefix;
        private readonly List<string> _warnings = warnings;
        private readonly Dictionary<string, DialogTopic> _pool = [];

        public List<AdventureMap> Maps { get; } = [];
        public List<AdventureQuest> Quests { get; } = [];
        public List<AdventureItem> Items { get; } = [];
        public List<AdventureSpell> Spells { get; } = [];
        public List<AdventureMonster> Monsters { get; } = [];
        public List<AdventureNpcType> NpcTypes { get; } = [];
        public List<AdventureNpc> People { get; } = [];
        public List<AdventureMapObject> MapObjects { get; } = [];
        public List<AdventureRace> Races { get; } = [];
        public List<AdventureSkill> Skills { get; } = [];
        public List<AdventureAttribute> Attributes { get; } = [];
        public IReadOnlyDictionary<string, DialogTopic> DialogPool => _pool;

        /// <summary>
        /// Walks from the record after the header to the map list, decoding every tag it knows, then
        /// picks each map's placed-object list out of the records the map list points at.
        /// </summary>
        public void Run(PalmDatabase db, int headerIndex)
        {
            int mapListIndex = -1;

            for (int i = headerIndex + 1; i < db.Records.Count && i - headerIndex < MaxRecords; i++)
            {
                var record = db.Records[i];
                int tag = db.TagOf(record);
                if (tag < 0) continue;

                if (tag == AdventureLayout.TagMapList)
                {
                    if (TryParse(record, "map list", ReadMapList)) mapListIndex = i;
                    break;
                }

                switch ((byte)tag)
                {
                    case AdventureLayout.TagItem:
                        TryParse(record, "item", a => Items.Add(ReadItem(a)));
                        break;
                    case AdventureLayout.TagNpc:
                        TryParse(record, "person", a => People.Add(ReadNpc(a)));
                        break;
                    case AdventureLayout.TagMapObject:
                        TryParse(record, "map object", a => MapObjects.Add(ReadMapObject(a)));
                        break;
                    case AdventureLayout.TagSpell:
                        TryParse(record, "spell", a => Spells.Add(ReadSpell(a)));
                        break;
                    case AdventureLayout.TagQuestList:
                        TryParse(record, "quest list", ReadQuestList);
                        break;
                    case AdventureLayout.TagMonsterList:
                        // The record ends with a second, undecoded list of picture variants, so the
                        // usual "consumed to the end" check cannot apply here.
                        TryParse(record, "monster list", ReadMonsterList, checkConsumed: false);
                        break;
                    case AdventureLayout.TagNpcTypeList:
                        TryParse(record, "person types", ReadNpcTypeList);
                        break;
                    case AdventureLayout.TagDialog:
                        TryParse(record, "dialog pool", ReadDialogPool);
                        break;
                    case AdventureLayout.TagRaceList:
                        TryParse(record, "races", ReadRaceList);
                        break;
                    case AdventureLayout.TagSkillList:
                        TryParse(record, "skills", ReadSkillList);
                        break;
                    case AdventureLayout.TagAttributeList:
                        TryParse(record, "attributes", ReadAttributeList);
                        break;
                }
            }

            if (mapListIndex < 0)
            {
                _warnings.Add("No map list was found, so the gazetteer is empty.");
                return;
            }

            AttachPlacements(db, mapListIndex);
        }

        /// <summary>
        /// Fills in each map's placed-object list.
        ///
        /// After the map list the database holds each map's own records. <b>A map owns exactly five
        /// consecutive record ids</b>, and which is which does not vary: <c>+0</c> is the tile data,
        /// <c>+1</c> the outdoor terrain (absent for an interior), <c>+2</c> and <c>+4</c> are
        /// untagged per-map data, and <c>+3</c> is the list of map objects placed on it. The ids are
        /// allocated per map whether or not every record is written, so this is arithmetic rather
        /// than a search — and it is checked, because the record at <c>+3</c> must carry the
        /// placement tag or it is ignored.
        ///
        /// Both shipped worlds agree: every one of Freymore's 159 placement records and every one of
        /// the expansion's 88 sits at its map's id plus three, and nothing else does.
        /// </summary>
        private void AttachPlacements(PalmDatabase db, int mapListIndex)
        {
            var byId = new Dictionary<int, PalmRecord>();
            for (int i = mapListIndex; i < db.Records.Count; i++) byId.TryAdd(db.Records[i].UniqueId, db.Records[i]);

            for (int m = 0; m < Maps.Count; m++)
            {
                if (!byId.TryGetValue(Maps[m].RecordId + PlacementRecordOffset, out var record)) continue;
                if (db.TagOf(record) != AdventureLayout.TagMapObjectPlacement) continue;

                Maps[m] = Maps[m] with
                {
                    HasPlacements = true,
                    ObjectIds = ReadPlacementIds(db.Bytes(record)),
                };
            }
        }

        // ---- collections ----------------------------------------------------------------------

        private void ReadQuestList(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagQuestList, "quest list");
            int count = Count(a, "quest list");
            for (int i = 0; i < count; i++)
            {
                a.ExpectTag(AdventureLayout.TagQuest, "quest");
                Quests.Add(new AdventureQuest(a.ReadString(), a.ReadString(), a.ReadString()));
            }
        }

        private void ReadMonsterList(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagMonsterList, "monster list");
            int count = Count(a, "monster list");
            for (int i = 0; i < count; i++) Monsters.Add(ReadMonster(a));
        }

        private void ReadNpcTypeList(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagNpcTypeList, "person types");
            int count = Count(a, "person types");
            for (int i = 0; i < count; i++) NpcTypes.Add(ReadNpcType(a));
        }

        private void ReadRaceList(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagRaceList, "races");
            int count = Count(a, "races");
            for (int i = 0; i < count; i++)
            {
                a.ExpectTag(AdventureLayout.TagRace, "race");
                string id = a.ReadString();
                string name = a.ReadString();
                string description = a.ReadString();
                a.ReadString();
                if (_version > 0x33) a.ReadString();
                Races.Add(new AdventureRace(id, name, description));
            }
        }

        private void ReadSkillList(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagSkillList, "skills");
            int count = Count(a, "skills");
            for (int i = 0; i < count; i++)
            {
                a.ExpectTag(AdventureLayout.TagSkill, "skill");
                string id = a.ReadString();
                string name = a.ReadString();
                string description = _version > 0x49 ? a.ReadString() : "";
                if (_version > 0x6D) a.ReadString();
                a.ReadByte();
                if (_version > 0x52) a.ReadByte();
                if (_version > 0x55) a.ReadString();
                Skills.Add(new AdventureSkill(id, name, description));
            }
        }

        private void ReadAttributeList(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagAttributeList, "attributes");
            int count = Count(a, "attributes");
            for (int i = 0; i < count; i++)
            {
                a.ExpectTag(AdventureLayout.TagAttribute, "attribute");
                Attributes.Add(new AdventureAttribute(a.ReadString(), a.ReadString(), a.ReadString()));
            }
        }

        private void ReadDialogPool(RecordArchive a)
        {
            var pool = ReadDialog(a, shared: true);
            foreach (var topic in pool.All)
                if (topic.Id.Length > 0) _pool[topic.Id] = topic;
        }

        /// <summary>
        /// The map list: two resource indexes, a one-byte map count, then one entry per map.
        ///
        /// The count really is one byte — the engine reads it with a plain byte read — which caps a
        /// world at 255 maps. Freymore ships 239 and the expansion 210.
        /// </summary>
        private void ReadMapList(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagMapList, "map list");
            a.ReadUInt16();
            a.ReadUInt16();
            int count = a.ReadByte();

            for (int i = 0; i < count; i++)
            {
                int recordId = a.ReadUInt16();
                a.ExpectTag(AdventureLayout.TagMap, "map");

                string name = a.ReadString();
                string id = a.ReadString();
                ushort flags = a.ReadUInt16();

                // Ambient colour and light, twice — day and night — then two more words, a byte, the
                // four unidentified bytes at +0x34..+0x37, and four resource indexes.
                if (_version > 0x74) { a.ReadUInt16(); a.ReadByte(); }
                a.ReadUInt16();
                a.ReadByte();
                if (_version >= 0x75) { a.ReadUInt16(); a.ReadUInt16(); }
                if (_version > 0x75) a.ReadByte();
                a.ReadByte(); a.ReadByte(); a.ReadByte(); a.ReadByte();
                if (_version > 0x0C)
                {
                    a.ReadUInt16(); a.ReadUInt16(); a.ReadUInt16(); a.ReadUInt16();
                }

                var cell = MapLayout.CellFromId(id, _gridPrefix);
                Maps.Add(new AdventureMap
                {
                    RecordId = recordId,
                    Id = id,
                    Name = name,
                    Flags = flags,
                    Column = cell?.Column,
                    Row = cell?.Row,
                });
            }
        }

        // ---- objects ----------------------------------------------------------------------------

        /// <summary>
        /// One item type. The field order is <c>FUN_00509880</c>'s; the offsets those fields land on
        /// are the ones §15.3 already documents for the live object.
        /// </summary>
        private AdventureItem ReadItem(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagItem, "item");

            string id = a.ReadString();
            a.ReadString();
            a.ReadString();
            string name = a.ReadString();
            if (_version is >= 0x3E and <= 0x48) a.ReadString();
            string description = a.ReadString();
            a.ReadString();
            a.ReadString();
            string spellId = _version > 0x71 ? a.ReadString() : "";

            var effects = a.ReadBool() ? ReadEffectHolder(a) : [];

            uint value = a.ReadUInt32();
            ushort weight = a.ReadUInt16();
            ushort damageMin = a.ReadUInt16();
            ushort damageMax = a.ReadUInt16();
            ushort armour = a.ReadUInt16();
            ushort enchantStorage = a.ReadUInt16();
            ushort maxCondition = a.ReadUInt16();
            a.ReadByte();
            byte category = a.ReadByte();
            byte subtype = a.ReadByte();
            byte alignment = a.ReadByte();
            byte flags = a.ReadByte();
            if (_version > 0x3F) a.ReadByte();
            if (_version > 0x54) a.ReadByte();

            return new AdventureItem
            {
                Id = id,
                Name = name,
                Description = description,
                SpellId = spellId,
                Value = value,
                Weight = weight,
                DamageMin = damageMin,
                DamageMax = damageMax,
                Armour = armour,
                EnchantStorage = enchantStorage,
                MaxCondition = maxCondition,
                Category = category,
                Subtype = subtype,
                Alignment = alignment,
                Flags = flags,
                Effects = effects,
            };
        }

        /// <summary>One spell. <c>FUN_00519EE0</c>.</summary>
        private AdventureSpell ReadSpell(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagSpell, "spell");

            string id = a.ReadString();
            string name = a.ReadString();
            string description = a.ReadString();
            a.ReadString();
            a.ReadString();
            a.ReadString();
            a.ReadString();
            if (_version > 0x1A) { a.ReadString(); a.ReadString(); }
            if (_version < 0x33) a.ReadBlob();

            ushort cost = a.ReadUInt16();
            ushort difficulty = a.ReadUInt16();
            ushort duration = a.ReadUInt16();
            var effects = ReadEffects(a);
            a.ReadByte(); a.ReadByte(); a.ReadByte(); a.ReadByte();
            if (_version > 0x33) a.ReadString();

            return new AdventureSpell
            {
                Id = id,
                Name = name,
                Description = description,
                Cost = cost,
                Difficulty = difficulty,
                Duration = duration,
                Effects = effects,
            };
        }

        /// <summary>One monster type. <c>FUN_00511CA0</c>.</summary>
        private AdventureMonster ReadMonster(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagMonster, "monster");

            string id = a.ReadString();
            string name = a.ReadString();
            string plural = a.ReadString();
            SkipStringList(a);

            if (_version > 0x0F) a.ReadString();
            if (_version > 0x15)
            {
                a.ReadString();
                a.ReadString();
                if (_version > 0x68) a.ReadString();
                a.ReadString();
            }

            var stats = new ushort[10];
            for (int i = 0; i < stats.Length; i++) stats[i] = a.ReadUInt16();

            a.ReadByte();
            int health = 0;
            if (_version > 0x2C) health = _version < 0x83 ? a.ReadByte() : a.ReadUInt16();
            if (_version > 0x2D) a.ReadByte();
            if (_version > 0x5E) a.ReadByte();
            if (_version > 0x60) a.ReadByte();
            SkipStringList(a);
            if (_version > 0x4E) a.ReadByte();
            if (_version > 0x4F) a.ReadString();

            return new AdventureMonster { Id = id, Name = name, PluralName = plural, Stats = stats, Health = health };
        }

        /// <summary>One NPC type. <c>FUN_00510210</c>.</summary>
        private AdventureNpcType ReadNpcType(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagNpcType, "person type");

            string id = a.ReadString();
            a.ReadString();
            string name = a.ReadString();
            a.ReadString();
            if (_version < 0x5E) a.ReadString();
            if (_version > 0x14) a.ReadString();
            if (_version is >= 0x16 and < 0x69) { a.ReadString(); a.ReadString(); a.ReadString(); }

            var effects = ReadEffects(a);

            int abilities = Count(a, "abilities");
            for (int i = 0; i < abilities; i++)
            {
                a.ExpectTag(AdventureLayout.TagAbility, "ability");
                a.ReadUInt16();
                a.ReadByte(); a.ReadByte(); a.ReadByte();
                if (_version > 0x1B) a.ReadByte();
            }

            var stats = new ushort[10];
            for (int i = 0; i < stats.Length; i++) stats[i] = a.ReadUInt16();

            a.ReadByte(); a.ReadByte(); a.ReadByte();
            if (_version > 0x4E) a.ReadByte();
            if (_version > 0x4F) a.ReadString();
            if (_version > 0x57) a.ReadByte();
            if (_version > 0x5F) a.ReadByte();
            if (_version > 0x60) a.ReadByte();

            return new AdventureNpcType { Id = id, Name = name, Stats = stats, Effects = effects };
        }

        /// <summary>One placed NPC. <c>FUN_00514900</c>.</summary>
        private AdventureNpc ReadNpc(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagNpc, "person");

            string id = a.ReadString();
            string name = a.ReadString();
            a.ReadString();
            string typeId = a.ReadString();
            if (_version > 0x30) a.ReadString();
            if (_version > 0x37) a.ReadString();
            if (_version > 0x50) a.ReadString();

            uint gold = a.ReadUInt32();
            if (_version > 0x44) a.ReadByte();
            a.ReadUInt16();
            for (int i = 0; i < 6; i++) a.ReadByte();
            if (_version > 0x67) a.ReadUInt16();

            var stock = new List<(string, string)>();
            if (a.ReadBool())
            {
                int count = Count(a, "shop stock");
                for (int i = 0; i < count; i++)
                {
                    a.ExpectTag(AdventureLayout.TagStock, "stock entry");
                    stock.Add((a.ReadString(), a.ReadString()));
                }
            }

            Dialog? dialog = a.ReadBool() ? ReadDialog(a, shared: false) : null;

            if (_version > 0x38) a.ReadByte();
            if (_version > 0x42) a.ReadByte();

            return new AdventureNpc
            {
                Id = id,
                Name = name,
                TypeId = typeId,
                Gold = gold,
                Stock = stock,
                Dialog = dialog,
            };
        }

        /// <summary>
        /// One map object. <c>FUN_00516610</c> is the whole base class: an id, one byte, and a
        /// length-prefixed blob the derived class owns. The derived classes were not traced, so the
        /// blob is kept and its text harvested rather than decoded.
        /// </summary>
        private AdventureMapObject ReadMapObject(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagMapObject, "map object");
            string id = a.ReadString();
            byte kind = _version > 0x33 ? a.ReadByte() : (byte)0;
            byte[] payload = a.ReadBlob();

            return new AdventureMapObject
            {
                Id = id,
                Kind = kind,
                Payload = payload,
                Text = HarvestText(payload),
            };
        }

        // ---- shared shapes ----------------------------------------------------------------------

        /// <summary>An effect: a source id and the numbers behind it. <c>FUN_00505940</c>.</summary>
        private AdventureEffect ReadEffect(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagEffect, "effect");
            string source = a.ReadString();
            ushort v0 = a.ReadUInt16(), v1 = a.ReadUInt16(), v2 = a.ReadUInt16(), v3 = a.ReadUInt16();
            byte b0 = a.ReadByte(), b1 = a.ReadByte(), b2 = a.ReadByte();
            byte b3 = _version > 0x18 ? a.ReadByte() : (byte)0;
            return new AdventureEffect(source, v0, v1, v2, v3, b0, b1, b2, b3);
        }

        /// <summary>A counted list of effects. <c>FUN_004FEB00</c>.</summary>
        private List<AdventureEffect> ReadEffects(RecordArchive a)
        {
            int count = Count(a, "effects");
            var effects = new List<AdventureEffect>(count);
            for (int i = 0; i < count; i++) effects.Add(ReadEffect(a));
            return effects;
        }

        /// <summary>The wrapper an item keeps its effects in. <c>FUN_00505FE0</c>.</summary>
        private List<AdventureEffect> ReadEffectHolder(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagEffectHolder, "effect holder");
            a.ReadUInt32();
            if (_version < 0x54) a.ReadUInt16();
            return ReadEffects(a);
        }

        /// <summary>
        /// A script. <c>FUN_005128A0</c> stores its source text, its parsed length and three bytes;
        /// the engine re-parses the text at load, so the source is the useful half and the rest is
        /// skipped.
        /// </summary>
        private static string ReadScript(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagScript, "script");
            string text = a.ReadString();
            a.ReadUInt16();
            a.ReadByte(); a.ReadByte(); a.ReadByte();
            return text;
        }

        /// <summary>
        /// One thing the player may say back: an optional id and the wording of the option.
        /// <c>FUN_00513300</c>.
        /// </summary>
        private static DialogChoice ReadChoice(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagChoice, "choice");
            string symbol = a.ReadBool() ? ReadScript(a) : "";
            string text = a.ReadString();
            return new DialogChoice(text, symbol);
        }

        /// <summary>
        /// One reply. <c>FUN_00513540</c>: five optional ids, the line itself, four optional
        /// follow-up options, and the compiled bytecode, which is skipped because the engine
        /// re-parses it from the source anyway.
        /// </summary>
        private static DialogReply ReadReply(RecordArchive a)
        {
            a.ExpectTag(AdventureLayout.TagReply, "reply");

            var symbols = new List<string>();
            for (int i = 0; i < ReplySymbolSlots; i++)
                if (a.ReadBool()) symbols.Add(ReadScript(a));

            string text = a.ReadString();

            var choices = new List<DialogChoice>();
            for (int i = 0; i < ReplyChoiceSlots; i++)
                if (a.ReadBool()) choices.Add(ReadChoice(a));

            a.ReadBlob();

            return new DialogReply
            {
                Text = text,
                Symbols = [.. symbols.Where(x => x.Length > 0)],
                Choices = choices,
            };
        }

        /// <summary>
        /// One topic. <c>FUN_005137C0</c>.
        ///
        /// The wording is only stored when the entry is <i>not</i> a reference: a reference carries
        /// its id and nothing else, and the engine fills the rest in from the shared pool after the
        /// load. That branch is the reason a naive reader falls off a cliff two NPCs in.
        /// </summary>
        private DialogTopic ReadTopic(RecordArchive a, bool shared)
        {
            a.ExpectTag(AdventureLayout.TagDialogTopic, "dialog topic");

            bool isReference = a.ReadUInt32() != 0;
            string id = a.ReadString();

            if (!shared && isReference)
                return new DialogTopic { Id = id, IsReference = true };

            string topic = _version > 0x25 ? a.ReadString() : "";
            string gate = _version > 0x24 && a.ReadBool() ? ReadScript(a) : "";
            string question = _version > 0x3B ? a.ReadString() : "";

            int count = Count(a, "replies");
            var replies = new List<DialogReply>(count);
            for (int i = 0; i < count; i++) replies.Add(ReadReply(a));

            return new DialogTopic
            {
                Id = id,
                IsReference = isReference,
                Topic = topic,
                Question = question,
                Gate = gate,
                Replies = replies,
            };
        }

        /// <summary>A conversation: two counted lists of topics. <c>FUN_00513B60</c>.</summary>
        private Dialog ReadDialog(RecordArchive a, bool shared)
        {
            a.ExpectTag(AdventureLayout.TagDialog, "dialog");
            return new Dialog(ReadTopicList(a, shared), ReadTopicList(a, shared));
        }

        private List<DialogTopic> ReadTopicList(RecordArchive a, bool shared)
        {
            int count = Count(a, "dialog topics");
            var topics = new List<DialogTopic>(count);
            for (int i = 0; i < count; i++) topics.Add(ReadTopic(a, shared));
            return topics;
        }

        private static void SkipStringList(RecordArchive a)
        {
            int count = a.ReadCount("string list");
            for (int i = 0; i < count; i++) a.ReadString();
        }

        private static int Count(RecordArchive a, string what)
        {
            int count = a.ReadCount(what);
            if (count > MaxCollectionEntries)
                throw new ArchiveException($"{what}: {count} entries is beyond anything the game ships");
            return count;
        }

        // ---- plumbing ---------------------------------------------------------------------------

        /// <summary>
        /// Runs one parser over one record and turns a failure into a warning.
        ///
        /// <paramref name="checkConsumed"/> is what makes a wrong schema loud rather than quiet: a
        /// parser that agrees with the serializer ends within the record's four-byte padding, so
        /// anything left over means a field was missed and everything after it is suspect.
        /// </summary>
        private bool TryParse(in PalmRecord record, string what, Action<RecordArchive> parse,
                              bool checkConsumed = true)
        {
            var a = _db.Open(record);
            try
            {
                parse(a);
            }
            catch (ArchiveException e)
            {
                _warnings.Add($"Record {record.UniqueId} ({what}) did not decode: {e.Message}");
                return false;
            }

            if (checkConsumed && !a.ConsumedWithinPadding)
            {
                _warnings.Add(
                    $"Record {record.UniqueId} ({what}) left {a.Remaining} of {a.Length} bytes unread, " +
                    "so this reader does not fully understand it.");
            }
            return true;
        }

        /// <summary>
        /// The ids in a map's placement record.
        ///
        /// The record's own field layout was not traced, so this does not claim to know where each
        /// object stands — only which objects the map names. The ids are NUL-terminated and follow
        /// the world's own naming, which is what makes picking them out of the record safe.
        /// </summary>
        private static List<string> ReadPlacementIds(ReadOnlySpan<byte> bytes)
        {
            var ids = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string text in HarvestText(bytes))
            {
                if (!LooksLikeId(text) || !seen.Add(text)) continue;
                ids.Add(text);
            }
            return ids;
        }

        private static bool LooksLikeId(string text)
        {
            if (text.Length < 4 || !text.Contains('_')) return false;
            foreach (char c in text)
                if (!char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-') return false;
            return char.IsAsciiLetter(text[0]);
        }
    }

    /// <summary>
    /// Every run of printable text in a blob, NUL-terminated and at least
    /// <see cref="MinHarvestedTextLength"/> characters. Used for the payloads whose field layout is
    /// not known, so a sign's wording still reaches the cluebook.
    /// </summary>
    public static List<string> HarvestText(ReadOnlySpan<byte> bytes)
    {
        var found = new List<string>();
        var run = new StringBuilder();

        foreach (byte b in bytes)
        {
            if (b >= 0x20 && b != 0x7F)
            {
                run.Append((char)b);
                continue;
            }
            if (b == 0 && run.Length >= MinHarvestedTextLength) found.Add(run.ToString());
            run.Clear();
        }
        if (run.Length >= MinHarvestedTextLength) found.Add(run.ToString());
        return found;
    }

}
