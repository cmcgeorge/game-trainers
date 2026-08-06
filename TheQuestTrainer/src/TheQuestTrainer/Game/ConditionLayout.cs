namespace TheQuestTrainer.Game;

/// <summary>
/// Where the character's adverse conditions live: poison, disease, curse and paralysis.
///
/// None of the four is a flag. Poison, curse and paralysis are <b>lists of effect objects</b>, and
/// the record holds twenty-five <c>std::vector&lt;SEffect*&gt;</c> in a row — one per <i>effect
/// group</i> — immediately after the fields <see cref="QuestLayout"/> describes. A separate table
/// maps the game's effect <i>kind</i> ids onto those groups, and the game's own "cure" reads exactly
/// that table before it touches a vector, which is why the trainer does too rather than baking in a
/// group number.
///
/// Disease is the odd one out: it is a <c>std::vector&lt;SDiseaseType*&gt;</c> of pointers to shared
/// type objects, laid out like the item/item-type split in <see cref="ItemLayout"/>. Being a disease
/// costs no allocation — the vector holds borrowed pointers — but a disease also *grants* effects
/// into the groups above, tagged with <see cref="SourceDisease"/>, and those are allocations.
///
/// Everything here was read out of <c>TheQuest.exe</c> v1.9.10 with Ghidra and then confirmed
/// against a live session; <c>docs/ReverseEngineering.md</c> §16 derives each offset. As elsewhere,
/// sizes are restated as arithmetic so a mistyped constant fails a harness check instead of quietly
/// reading a neighbouring field.
/// </summary>
public static class ConditionLayout
{
    // ---- diseases -----------------------------------------------------------------------------

    /// <summary>
    /// First of the three pointers of the diseases <c>std::vector&lt;SDiseaseType*&gt;</c>. The
    /// game's "are you diseased" test is simply <c>begin != end</c>, and its "cure" erases the
    /// matching element — the pointed-at types are shared and are never owned by the character.
    /// </summary>
    public const uint DiseasesBegin = 0x3B4;

    /// <summary>One past the last disease.</summary>
    public const uint DiseasesEnd = DiseasesBegin + 4;

    /// <summary>End of the allocation. Read for validation only; the trainer never grows the vector.</summary>
    public const uint DiseasesCapacity = DiseasesEnd + 4;

    /// <summary>
    /// The most diseases the reader will walk. The game imposes no limit, so this is the trainer's
    /// own guard against a garbage vector — a character with more than this is not a character.
    /// </summary>
    public const int MaxDiseases = 64;

    // ---- the effect groups --------------------------------------------------------------------

    /// <summary>
    /// The <c>begin</c> pointer of effect group 0. The groups are <see cref="EffectGroupSlots"/>
    /// consecutive vectors; the game's "strip every effect from this source" loop walks them from
    /// group 1 to group <see cref="LastEffectGroup"/>, leaving slot 0 unused — the same convention
    /// the attribute, skill and equipment arrays follow.
    /// </summary>
    public const uint EffectGroups = 0x404;

    /// <summary>A group is one <c>std::vector</c>: begin, end, end-of-allocation.</summary>
    public const int EffectGroupBytes = 12;

    /// <summary>Groups in the array, including the unused slot 0.</summary>
    public const int EffectGroupSlots = 25;

    /// <summary>Lowest group the game itself ever touches.</summary>
    public const int FirstEffectGroup = 1;

    /// <summary>Highest group the game itself ever touches.</summary>
    public const int LastEffectGroup = EffectGroupSlots - 1;

    /// <summary>
    /// The effect-kind table: <c>table[kind]</c> is the group holding effects of that kind. It abuts
    /// the group array exactly, which is what pins both — the game computes a group's <c>begin</c> as
    /// <c>(table[kind] * 3 + 0x101) * 4</c>, and that is this arithmetic written out.
    /// </summary>
    public const uint EffectGroupOfKind = EffectGroups + EffectGroupSlots * EffectGroupBytes;

    /// <summary>
    /// Highest kind the table covers. The trainer only ever looks up three of them, so this is a
    /// bounds guard and a note to the next reader rather than something it depends on: past this the
    /// dwords stop being group numbers.
    /// </summary>
    public const int MaxEffectKind = 0x3A;

    /// <summary>
    /// The most effects the reader will walk in one group. Well past anything a character
    /// accumulates — the probed session's largest group held four — and small enough that a pair of
    /// pointers that is not a vector is refused rather than walked.
    /// </summary>
    public const int MaxEffectsPerGroup = 256;

    // ---- the effect object --------------------------------------------------------------------

    /// <summary>
    /// First field of the 20-byte effect object: a heap buffer the game frees alongside it. It read
    /// zero in every effect observed, and nothing found in the disassembly reads it.
    /// </summary>
    public const uint EffectOwner = 0x00;

    /// <summary>
    /// What the effect is *of* — the key the game looks a disease or a resistance type up by. Read
    /// for nothing here; recorded so the next reader knows what the dword is.
    /// </summary>
    public const uint EffectTypeKey = EffectOwner + 4;

    /// <summary>
    /// How big the effect is, signed: health per turn for poison, the percentage for a resistance,
    /// the modifier for an attribute or skill. A word, not a dword — <c>+0x0A</c> is padding.
    /// </summary>
    public const uint EffectMagnitude = EffectTypeKey + 4;

    /// <summary>Turns remaining. Zero for the ones that last until they are cured, poison among them.</summary>
    public const uint EffectDuration = EffectMagnitude + 4;

    /// <summary>Which group the effect is filed under — the same index the kind table produces.</summary>
    public const uint EffectGroup = EffectDuration + 4;

    /// <summary>Where the effect came from, which is what decides whether a cure may remove it.</summary>
    public const uint EffectSource = EffectGroup + 1;

    /// <summary>Which attribute or skill a group-1 or group-2 effect modifies.</summary>
    public const uint EffectSubject = EffectSource + 1;

    /// <summary>Size of the allocation, as the game's own <c>operator delete</c> states it.</summary>
    public const int EffectBytes = 0x14;

    // ---- effect kinds -------------------------------------------------------------------------

    /// <summary>Poison: loses the character <see cref="EffectMagnitude"/> health every turn until cured.</summary>
    public const int KindPoison = 0x1A;

    /// <summary>Curse: the character's attack power is reduced for <see cref="EffectDuration"/> turns.</summary>
    public const int KindCurse = 0x1B;

    /// <summary>Paralysis: the character cannot attack or move.</summary>
    public const int KindParalysis = 0x1C;

    // ---- effect sources -----------------------------------------------------------------------

    /// <summary>Granted by something worn or wielded; rebuilt whenever equipment changes.</summary>
    public const byte SourceEquipment = 1;

    /// <summary>Granted by a disease; rebuilt whenever the disease list changes.</summary>
    public const byte SourceDisease = 4;

    /// <summary>Granted by the character's race — the Derth's −5/−5/−5/+10 and its three +10 schools.</summary>
    public const byte SourceRace = 5;

    /// <summary>
    /// Whether a cure may remove an effect from this source.
    ///
    /// These three are the game's own set, taken from the function every "Cure poison", "Cure
    /// paralysis" and "Remove curse" ends in. Everything else — equipment, race, a disease — is
    /// re-derived by the game from something that still exists, so removing one would either be
    /// undone on the next recalculation or lose the player something a cure was never meant to take.
    /// </summary>
    public static bool IsCurable(byte source) => source is 2 or 3 or 6;

    // ---- the disease type ---------------------------------------------------------------------

    /// <summary>Pointer to the disease's internal id. A plain C string, as on an item type.</summary>
    public const uint DiseaseTypeId = 0x04;

    /// <summary>
    /// Pointer to the name the game shows — the <c>%s</c> in <c>You have been cured of %s.</c>
    /// </summary>
    public const uint DiseaseTypeName = 0x08;

    // ---- addressing ---------------------------------------------------------------------------

    /// <summary>Address of the <c>begin</c> pointer of effect group <paramref name="group"/>.</summary>
    public static uint EffectGroupBegin(uint record, int group) =>
        record + EffectGroups + (uint)group * EffectGroupBytes;

    /// <summary>Address of the <c>end</c> pointer of effect group <paramref name="group"/>.</summary>
    public static uint EffectGroupEnd(uint record, int group) => EffectGroupBegin(record, group) + 4;

    /// <summary>Address of the kind table's entry for <paramref name="kind"/>.</summary>
    public static uint EffectGroupSlot(uint record, int kind) =>
        record + EffectGroupOfKind + (uint)kind * 4;

    /// <summary>Whether <paramref name="group"/> is one the game itself files effects into.</summary>
    public static bool IsEffectGroup(long group) => group >= FirstEffectGroup && group <= LastEffectGroup;
}
