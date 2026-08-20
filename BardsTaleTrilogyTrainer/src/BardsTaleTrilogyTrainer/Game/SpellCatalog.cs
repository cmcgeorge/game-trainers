using System.Text;
using BardsTaleTrilogyTrainer.Memory;

namespace BardsTaleTrilogyTrainer.Game;

/// <summary>
/// One spell as the running game describes it, read from a <c>BardsTale.SpellDescription</c>.
/// </summary>
/// <param name="Id">The <see cref="SpellId"/> this describes — what goes into a character's learnt-spell list.</param>
/// <param name="Code">The four-letter code the game shows, e.g. <c>ZZGO</c>.</param>
/// <param name="Name">A readable name derived from the enum member; the game itself localises its display names.</param>
/// <param name="ClassId">The casting school, as a class id (6 = Conjurer … 12 = Geomancer).</param>
/// <param name="Level">School level that grants it, or 0 when no school ever does.</param>
/// <param name="Cost">Spell points per cast.</param>
public sealed record SpellEntry(
    SpellId Id,
    string Code,
    string Name,
    int ClassId,
    int Level,
    int Cost,
    bool Combat,
    bool NonCombat,
    bool Bt1,
    bool Bt2,
    bool Bt3)
{
    /// <summary>
    /// True when no school level can ever grant this spell, because
    /// <c>Character.KnowsSpell</c> bails out before the school test when the level is 0. These
    /// are the spells that have to be written into <c>m_learntSpells</c> directly.
    /// </summary>
    public bool IsSpecial => Level == 0;

    /// <summary>The school's name, or a note that it belongs to none.</summary>
    public string SchoolName => IsSpecial ? "no school" : ClassBook.ClassName(ClassId);

    /// <summary>Which games of the trilogy the spell appears in.</summary>
    public string Games
    {
        get
        {
            var games = new List<string>(3);
            if (Bt1) games.Add("BT1");
            if (Bt2) games.Add("BT2");
            if (Bt3) games.Add("BT3");
            return games.Count == 0 ? "—" : string.Join(", ", games);
        }
    }

    /// <summary>How the spell is reached: a school level, or an outright grant.</summary>
    public string Source => IsSpecial
        ? "learnt outright"
        : $"{SchoolName} level {Level}";

    public string Display => Code.Length > 0 ? $"{Code} — {Name}" : Name;
}

/// <summary>
/// The game's own spell table, read live out of <c>GlobalSpells.Instance.m_spellsByEnum</c>.
///
/// <para>Every spell's code, school and level is serialized Unity asset data, not something in
/// the executable, so there is no honest way to hard-code it — earlier versions of this trainer
/// shipped a community-sourced table whose schools and levels did not match the remaster. Reading
/// the array the game is actually using removes the guesswork, and keeps working if a patch
/// rebalances a spell.</para>
///
/// <para>Reading needs the game attached and past its loading screens. Until then the catalogue
/// is <see cref="IsLive"/> <c>false</c> and callers fall back to <see cref="SpecialSpells"/>,
/// which is enough to grant the cross-game spells because their ids come from the enum.</para>
/// </summary>
public sealed class SpellCatalog
{
    /// <summary>An array longer than this is treated as a bad read rather than a spell table.</summary>
    private const int MaxTableLength = 1024;

    private readonly Dictionary<SpellId, SpellEntry> _byId;

    private SpellCatalog(IReadOnlyList<SpellEntry> entries)
    {
        All = entries;
        _byId = entries.ToDictionary(e => e.Id);
    }

    /// <summary>The catalogue before the game has been read, holding no entries.</summary>
    public static SpellCatalog Empty { get; } = new(Array.Empty<SpellEntry>());

    /// <summary>Every spell the game loaded, in <see cref="SpellId"/> order.</summary>
    public IReadOnlyList<SpellEntry> All { get; }

    /// <summary>True once the table has actually been read from the game.</summary>
    public bool IsLive => All.Count > 0;

    /// <summary>The spells no school level can grant — ZZGO, NUKE and the quest spells.</summary>
    public IReadOnlyList<SpellEntry> Special =>
        All.Where(e => e.IsSpecial && e.Code.Length > 0).ToList();

    /// <summary>The spells a given school grants, in level order.</summary>
    public IEnumerable<SpellEntry> ForSchool(int classId) =>
        All.Where(e => !e.IsSpecial && e.ClassId == classId).OrderBy(e => e.Level).ThenBy(e => e.Code);

    public SpellEntry? Find(SpellId id) => _byId.GetValueOrDefault(id);

    /// <summary>Looks a spell up by its four-letter code, case-insensitively.</summary>
    public SpellEntry? FindByCode(string code)
    {
        string wanted = code.Trim();
        return wanted.Length == 0
            ? null
            : All.FirstOrDefault(e => string.Equals(e.Code, wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Reads <c>GlobalSpells.Instance.m_spellsByEnum</c>. Returns <see cref="Empty"/> when the
    /// singleton has not been created yet, which is the normal state on the title screen.
    /// </summary>
    public static SpellCatalog Read(IMemorySource mem, nuint globalSpellsClass)
    {
        if (globalSpellsClass == 0) return Empty;

        nuint instance = mem.ReadStaticRef(globalSpellsClass, CharacterFormat.GlobalSpellsInstanceStatic);
        if (instance == 0) return Empty;

        nuint table = mem.ReadPtr(instance + (nuint)CharacterFormat.GlobalSpellsByEnum);
        int length = mem.ReadArrayLength(table);
        if (length <= 0 || length > MaxTableLength) return Empty;

        var entries = new List<SpellEntry>(length);
        for (int i = 0; i < length; i++)
        {
            nuint description = mem.ReadArrayRef(table, i);
            if (description == 0) continue;                  // a gap in the enum's numbering

            var id = (SpellId)mem.ReadI32(description + (nuint)CharacterFormat.SpellDescriptionSpell);
            entries.Add(new SpellEntry(
                Id: id,
                Code: mem.ReadManagedString(mem.ReadPtr(description + (nuint)CharacterFormat.SpellDescriptionCode)),
                Name: ReadableName(id),
                ClassId: mem.ReadI32(description + (nuint)CharacterFormat.SpellDescriptionClass),
                Level: mem.ReadI32(description + (nuint)CharacterFormat.SpellDescriptionLevel),
                Cost: mem.ReadI32(description + (nuint)CharacterFormat.SpellDescriptionCost),
                Combat: mem.ReadBool(description + (nuint)CharacterFormat.SpellDescriptionCombat),
                NonCombat: mem.ReadBool(description + (nuint)CharacterFormat.SpellDescriptionNonCombat),
                Bt1: mem.ReadBool(description + (nuint)CharacterFormat.SpellDescriptionBt1),
                Bt2: mem.ReadBool(description + (nuint)(CharacterFormat.SpellDescriptionBt1 + 1)),
                Bt3: mem.ReadBool(description + (nuint)(CharacterFormat.SpellDescriptionBt1 + 2))));
        }

        // Two descriptions claiming the same id means the layout is off, not that the game has
        // duplicates; treat that as a failed read rather than showing nonsense.
        return entries.Select(e => e.Id).Distinct().Count() == entries.Count && entries.Count > 0
            ? new SpellCatalog(entries)
            : Empty;
    }

    /// <summary>
    /// Turns an enum member name into something readable — <c>DreamSpell</c> to "Dream Spell".
    /// The game's own display names come from its localisation tables, which the trainer does
    /// not read, so the enum name is the most faithful thing available offline.
    /// </summary>
    public static string ReadableName(SpellId id)
    {
        string raw = id.ToString();
        var text = new StringBuilder(raw.Length + 8);

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            bool boundary = i > 0 &&
                (char.IsUpper(c) && !char.IsUpper(raw[i - 1]) ||
                 char.IsDigit(c) != char.IsDigit(raw[i - 1]));
            if (boundary) text.Append(' ');
            text.Append(c);
        }
        return text.ToString();
    }
}
