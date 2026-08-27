namespace TheQuestTrainer.Adventures;

/// <summary>
/// The class tags and format version of a Quest world database.
///
/// Every serialized object starts with a one-byte tag, and the engine's own loader refuses a record
/// whose tag is not what it expected — so these are checks, not guesses. They were read off the
/// serializers in <c>TheQuest.exe</c>; <c>docs/ReverseEngineering.md</c> §18 lists which function
/// each one came from.
///
/// The tags are not a namespace: a run of image data can start with any byte, so a tag only means
/// something for a record the loader has *reached* in order. <see cref="AdventureReader"/> is what
/// keeps that order.
/// </summary>
public static class AdventureLayout
{
    /// <summary>The record id of the database header. The engine opens this one by number.</summary>
    public const int HeaderRecordId = 3999;

    /// <summary>The record id the engine's own walk starts from, straight after the header.</summary>
    public const int FirstContentRecordId = 4000;

    /// <summary>
    /// The two magic bytes and the version marker the header carries after its unused first word.
    /// The third, <c>0x11</c>, is checked too.
    /// </summary>
    public const byte HeaderMagic0 = 0x9E;

    /// <inheritdoc cref="HeaderMagic0"/>
    public const byte HeaderMagic1 = (byte)'I';

    /// <inheritdoc cref="HeaderMagic0"/>
    public const byte HeaderMagic2 = 0x11;

    /// <summary>
    /// The format version shipped by The Quest v1.9.10 and its expansion, 124.
    ///
    /// Every serializer is a chain of <c>if (version &gt; n)</c> tests, so this number decides which
    /// fields exist. It is <b>read from the header</b>, never assumed — this constant is only what
    /// the shipped data turned out to hold, and the reader refuses a version it has not been checked
    /// against rather than mis-parsing a newer or older one.
    /// </summary>
    public const int KnownVersion = 0x7C;

    /// <summary>The oldest version whose field set this reader still gets right.</summary>
    public const int OldestSupportedVersion = 0x6E;

    // ---- object tags --------------------------------------------------------------------------

    /// <summary>The map list — one record, holding every map's id, name, flags and record id.</summary>
    public const byte TagMapList = 0x01;

    /// <summary>One map, inside <see cref="TagMapList"/>. <c>'G'</c>.</summary>
    public const byte TagMap = 0x47;

    /// <summary>A spell, one per record.</summary>
    public const byte TagSpell = 0x04;

    /// <summary>The index record that owns the run of <see cref="TagSpell"/> records.</summary>
    public const byte TagSpellIndex = 0x03;

    /// <summary>One effect: a modifier with a source id and four words.</summary>
    public const byte TagEffect = 0x05;

    /// <summary>The wrapper an item or spell keeps its effect list in.</summary>
    public const byte TagEffectHolder = 0x06;

    /// <summary>The quest list — one record holding every quest.</summary>
    public const byte TagQuestList = 0x08;

    /// <summary>One quest, inside <see cref="TagQuestList"/>.</summary>
    public const byte TagQuest = 0x09;

    /// <summary>An ability granted to an NPC type.</summary>
    public const byte TagAbility = 0x18;

    /// <summary>The monster-type list.</summary>
    public const byte TagMonsterList = 0x1C;

    /// <summary>One monster type.</summary>
    public const byte TagMonster = 0x1D;

    /// <summary>The NPC-type list.</summary>
    public const byte TagNpcTypeList = 0x1E;

    /// <summary>One NPC type.</summary>
    public const byte TagNpcType = 0x1F;

    /// <summary>A script: the source text the engine's own VM parses.</summary>
    public const byte TagScript = 0x20;

    /// <summary>One thing the player may say back: an optional id and the wording of the option.</summary>
    public const byte TagChoice = 0x21;

    /// <summary>One reply in a dialog: what the character says, plus the scripts it runs.</summary>
    public const byte TagReply = 0x22;

    /// <summary>One topic in a dialog.</summary>
    public const byte TagDialogTopic = 0x23;

    /// <summary>The shared dialog pool, which NPCs reference by id.</summary>
    public const byte TagDialog = 0x24;

    /// <summary>A shop's stock entry, inside an NPC.</summary>
    public const byte TagStock = 0x29;

    /// <summary>One NPC, one per record.</summary>
    public const byte TagNpc = 0x2A;

    /// <summary>The index record that owns the run of <see cref="TagNpc"/> records.</summary>
    public const byte TagNpcIndex = 0x2B;

    /// <summary>The index record that owns the run of <see cref="TagMapObject"/> records.</summary>
    public const byte TagMapObjectIndex = 0x27;

    /// <summary>One map object — a door, chest, sign, teleport and so on.</summary>
    public const byte TagMapObject = 0x28;

    /// <summary>The index record that owns the run of <see cref="TagItem"/> records.</summary>
    public const byte TagItemIndex = 0x14;

    /// <summary>One item type, one per record.</summary>
    public const byte TagItem = 0x15;

    /// <summary>The race list. <c>'N'</c>.</summary>
    public const byte TagRaceList = 0x4E;

    /// <summary>One race. <c>'O'</c>.</summary>
    public const byte TagRace = 0x4F;

    /// <summary>The skill list. <c>'P'</c>.</summary>
    public const byte TagSkillList = 0x50;

    /// <summary>One skill. <c>'Q'</c>.</summary>
    public const byte TagSkill = 0x51;

    /// <summary>The attribute list. <c>'R'</c>.</summary>
    public const byte TagAttributeList = 0x52;

    /// <summary>One attribute. <c>'S'</c>.</summary>
    public const byte TagAttribute = 0x53;

    /// <summary>The per-map list of the map objects placed on it.</summary>
    public const byte TagMapObjectPlacement = 0x49;

    /// <summary>The per-map tile data.</summary>
    public const byte TagMapTiles = 0x48;
}
