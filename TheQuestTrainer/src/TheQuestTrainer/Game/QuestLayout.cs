namespace TheQuestTrainer.Game;

/// <summary>
/// Where every editable number lives inside The Quest's character record.
///
/// The game keeps one monolithic "engine" object on the heap. A character record — the C++ class
/// whose vtable sits in the image's read-only data — is embedded in it at
/// <see cref="RecordInEngine"/>, and a second, pristine copy of the same class (the prototype a new
/// game is stamped from) is embedded earlier in the same object. Everything below is measured from
/// the <i>record</i> base, not from the engine object, because the record is what the locator finds
/// and what the save file serialises field-for-field.
///
/// Offsets were read out of <c>TheQuest.exe</c> v1.9.10 (link stamp 2020-02-27) with Ghidra and then
/// confirmed against a live session; see <c>docs/ReverseEngineering.md</c> for the derivation of
/// each one. Sizes are restated as arithmetic (<c>Gold + 4</c>, <c>SkillSlots * 2</c>) so a mistyped
/// constant fails a harness check instead of quietly reading the wrong field.
/// </summary>
public static class QuestLayout
{
    // ---- the two ways in ------------------------------------------------------------------

    /// <summary>
    /// RVA of the <c>.data</c> slot holding the engine-object pointer. A shortcut, not a
    /// dependency: <see cref="CharacterLocator"/>'s second chain finds the same record without it,
    /// and the harness proves that. Only trusted when the RVA lands in a writable, non-executable
    /// section of the image that is actually mapped.
    /// </summary>
    public const uint EngineSlotRva = 0x0033_5790;

    /// <summary>Offset of the live character record inside the engine object.</summary>
    public const uint RecordInEngine = 0x3DC8;

    // ---- record fields --------------------------------------------------------------------

    /// <summary>First dword of the record: the character class's vtable pointer, in the image.</summary>
    public const uint VTable = 0x000;

    /// <summary>
    /// A second copy of <see cref="Experience"/> that the save file also writes, before the name.
    /// It held the same number in every session observed, but nothing was found that reads it and
    /// no rule was established, so the trainer neither reads nor writes it — it is recorded here
    /// only so the next person to open the record knows what that dword is.
    /// </summary>
    public const uint ExperienceMirror = 0x010;

    /// <summary>MSVC <c>std::string</c> holding the character's name.</summary>
    public const uint Name = 0x014;

    /// <summary>MSVC <c>std::string</c> holding the portrait resource id, e.g. <c>bres_head00_racederth</c>.</summary>
    public const uint PortraitId = Name + StdString.Bytes;

    /// <summary>Current health. The maximum is derived from Endurance and level and is not stored.</summary>
    public const uint Health = 0x046;

    /// <summary>Current mana. The maximum is derived from Intelligence and level and is not stored.</summary>
    public const uint Mana = Health + 2;

    /// <summary>Character level, 1..<see cref="GameFacts.MaxLevel"/>. Stored as a word, read as a byte.</summary>
    public const uint Level = Mana + 2;

    /// <summary>Total experience.</summary>
    public const uint Experience = Level + 2;

    /// <summary>Experience the next level needs. The game caches it; a level edit must keep it honest.</summary>
    public const uint ExperienceForNextLevel = 0x058;

    /// <summary>
    /// The per-level experience thresholds, copied into every character record. Its first entries
    /// are the trainer's structural signature — see <see cref="GameTables.ExperienceSignature"/>.
    /// </summary>
    public const uint ExperienceTable = 0x064;

    /// <summary>
    /// Gold. Four bytes past the end of the experience table — the table ends at <c>+0x1EC</c>,
    /// which holds an unidentified dword that read zero in every session.
    /// </summary>
    public const uint Gold = ExperienceTable + GameFacts.ExperienceTableEntries * 4 + 4;

    /// <summary>
    /// Base attribute values, one word per attribute. Slot 0 is unused — the game's attribute ids
    /// are 1..5 (Strength, Dexterity, Endurance, Intelligence, Personality) — so the array is
    /// <see cref="GameFacts.AttributeSlots"/> wide and <see cref="Attribute"/> indexes into it.
    /// </summary>
    public const uint BaseAttributes = Gold + 4;

    /// <summary>Unspent attribute points.</summary>
    public const uint AttributePoints = BaseAttributes + GameFacts.AttributeSlots * 2;

    /// <summary>
    /// How many more points each attribute may take, again with an unused slot 0. Non-zero only
    /// where the game is willing to show a "+" button; the trainer writes attributes directly and
    /// leaves this alone.
    /// </summary>
    public const uint AttributeAllowance = AttributePoints + 2;

    /// <summary>
    /// The order the skills screen lists skills in: 20 bytes, each a skill id 1..20, primaries
    /// first. Read-only here, but it is what proved the skill ids in the first place.
    /// </summary>
    public const uint SkillDisplayOrder = AttributeAllowance + GameFacts.AttributeSlots * 2;

    /// <summary>Unspent skill points.</summary>
    public const uint SkillPoints = SkillDisplayOrder + GameFacts.SkillDisplayOrderBytes;

    /// <summary>
    /// The skill values the character was created with. Untouched by training, so it is the game's
    /// record of "where you started"; the trainer shows it and never writes it.
    /// </summary>
    public const uint StartingSkills = SkillPoints + 2;

    /// <summary>
    /// Base skill values — the array training raises and the one the trainer edits. What the skills
    /// screen shows is this plus racial and item bonuses, so an edited skill reads back higher than
    /// it was written for the three magic schools a Derth gets +10 in, and so on.
    /// </summary>
    public const uint BaseSkills = StartingSkills + GameFacts.SkillSlots * 2;

    /// <summary>Fame, a signed word in -100..+100. Drives the reputation band on the status screen.</summary>
    public const uint Fame = 0x3D0;

    /// <summary>Outstanding crime (the bounty guards collect). Zeroing it clears the wanted state.</summary>
    public const uint Crime = 0x3D4;

    /// <summary>Race id, 0..5 — see <see cref="GameTables.Races"/>.</summary>
    public const uint Race = Crime + 4;

    /// <summary>How much of the record the reader snapshots in one go. Everything above fits.</summary>
    public const int RecordBytes = 0x400;

    /// <summary>Address of attribute <paramref name="id"/> (1..5) in the record at <paramref name="record"/>.</summary>
    public static uint Attribute(uint record, int id) => record + BaseAttributes + (uint)id * 2;

    /// <summary>Address of base skill <paramref name="id"/> (1..20) in the record at <paramref name="record"/>.</summary>
    public static uint Skill(uint record, int id) => record + BaseSkills + (uint)id * 2;

    /// <summary>Address of experience-table entry <paramref name="index"/> (0-based) in the record.</summary>
    public static uint ExperienceTableEntry(uint record, int index) => record + ExperienceTable + (uint)index * 4;
}
