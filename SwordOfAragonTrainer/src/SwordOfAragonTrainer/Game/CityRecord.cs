namespace SwordOfAragonTrainer.Game;

/// <summary>The seven things a city can invest in, in the order they appear in a save's city block.</summary>
public enum DevelopmentCategory
{
    Agriculture = 0, Lumber = 1, Mining = 2, Manufacture = 3, Commerce = 4, Structure = 5, Fortification = 6,
}

/// <summary>
/// A typed, mutable view over one 14-line city block of an <c>ARAGON.HS&lt;letter&gt;</c> save.
/// Reads and writes go straight through to the underlying line list, one CSV field at a time, so
/// untouched fields — including every field whose meaning is still unproven — survive verbatim.
///
/// Field positions and their evidence are in <c>docs/RE.md</c> §6.2. Only Confirmed fields are
/// exposed as settable properties.
/// </summary>
public sealed class CityRecord
{
    private readonly List<string> _lines;
    private readonly int _base;

    /// <summary>Lines per city block.</summary>
    public const int BlockLines = 14;

    /// <summary>Number of investment categories per city.</summary>
    public const int CategoryCount = 7;

    // line offsets within the block
    private const int LineHeader = 0;        // "Name", population, income
    private const int LineMood = 1;          // ?, morale, loyalty, health
    private const int LineFinance = 2;       // taxRate, cityGold, trade, ?
    private const int LineRecruits = 3;      // recruits, ?, position
    private const int LineDeltas = 4;        // dPopulation, dMorale, dLoyalty, dHealth
    private const int LineFirstCategory = 7; // seven economy lines follow

    // field indices
    private const int FieldPopulation = 1;
    private const int FieldIncome = 2;
    private const int FieldMorale = 1;
    private const int FieldLoyalty = 2;
    private const int FieldHealth = 3;
    private const int FieldTaxRate = 0;
    private const int FieldCityGold = 1;
    private const int FieldRecruits = 0;
    private const int FieldPosition = 2;
    private const int FieldDevel = 0;
    private const int FieldCost = 1;
    private const int FieldResrc = 2;
    private const int FieldProd = 3;

    /// <summary>Highest value the trainer writes into morale, loyalty or health.</summary>
    public const int MaxMood = 999;

    /// <summary>The natural top of the morale/loyalty/health scale the game displays.</summary>
    public const int FullMood = 100;

    /// <summary>Highest population the trainer writes.</summary>
    public const int MaxPopulation = 32_000;

    /// <summary>Highest development level or resource ceiling the trainer writes.</summary>
    public const int MaxDevelopment = 99;

    /// <summary>Highest recruit pool the trainer writes.</summary>
    public const int MaxRecruits = 30_000;

    /// <summary>
    /// Highest city treasury the trainer writes. Every value the game itself puts in this field is
    /// int16-shaped (Tetrada's 15,420 is the largest), and QuickBASIC reads the line with
    /// <c>INPUT #</c> into an <c>INTEGER</c>, so anything past 32,767 would make the game raise an
    /// Overflow error while loading the save rather than simply looking odd.
    /// </summary>
    public const int MaxCityGold = short.MaxValue;

    /// <summary>Index of this city within the save, 0..19.</summary>
    public int Index { get; }

    internal CityRecord(List<string> lines, int index, int baseLine)
    {
        _lines = lines;
        Index = index;
        _base = baseLine;
    }

    /// <summary>The city's name as the save spells it (not editable — it keys the game's own tables).</summary>
    public string Name => CsvRow.GetString(_lines[_base + LineHeader], 0);

    /// <summary>Total inhabitants.</summary>
    public int Population
    {
        get => CsvRow.GetInt(_lines[_base + LineHeader], FieldPopulation);
        set => SetInt(LineHeader, FieldPopulation, value, 0, MaxPopulation);
    }

    /// <summary>Gold this city produced last month. Recomputed by the game each turn, so read-only here.</summary>
    public double Income => CsvRow.GetDouble(_lines[_base + LineHeader], FieldIncome);

    /// <summary>How content the population is. Not capped at 100 by the game.</summary>
    public int Morale
    {
        get => CsvRow.GetInt(_lines[_base + LineMood], FieldMorale);
        set => SetInt(LineMood, FieldMorale, value, 0, MaxMood);
    }

    /// <summary>How loyal the population is to you.</summary>
    public int Loyalty
    {
        get => CsvRow.GetInt(_lines[_base + LineMood], FieldLoyalty);
        set => SetInt(LineMood, FieldLoyalty, value, 0, MaxMood);
    }

    /// <summary>How healthy the population is.</summary>
    public int Health
    {
        get => CsvRow.GetInt(_lines[_base + LineMood], FieldHealth);
        set => SetInt(LineMood, FieldHealth, value, 0, MaxMood);
    }

    /// <summary>Tax rate as a percentage; the game accepts 0–80.</summary>
    public int TaxRate
    {
        get => CsvRow.GetInt(_lines[_base + LineFinance], FieldTaxRate);
        set => SetInt(LineFinance, FieldTaxRate, value, 0, GameFacts.MaxTaxRate);
    }

    /// <summary>
    /// The city's own treasury. Non-zero only for cities you do not own — an AI city's purse, and the
    /// prize for taking it.
    /// </summary>
    public int CityGold
    {
        get => CsvRow.GetInt(_lines[_base + LineFinance], FieldCityGold);
        set => SetInt(LineFinance, FieldCityGold, value, 0, MaxCityGold);
    }

    /// <summary>Recruits waiting in the pool.</summary>
    public int Recruits
    {
        get => CsvRow.GetInt(_lines[_base + LineRecruits], FieldRecruits);
        set => SetInt(LineRecruits, FieldRecruits, value, 0, MaxRecruits);
    }

    /// <summary>Map position as the save encodes it: <c>x * 100 + y</c>, or 0 for a region with no city hex.</summary>
    public int PositionCode => CsvRow.GetInt(_lines[_base + LineRecruits], FieldPosition);

    /// <summary>Map column, or -1 if this region has no city hex.</summary>
    public int X => PositionCode > 0 ? PositionCode / 100 : -1;

    /// <summary>Map row, or -1 if this region has no city hex.</summary>
    public int Y => PositionCode > 0 ? PositionCode % 100 : -1;

    /// <summary>Whether this region occupies a hex on the world map.</summary>
    public bool HasCityHex => PositionCode > 0;

    /// <summary>
    /// True when the save is carrying "changed this month" figures for this city. The game only fills
    /// those lines in for cities the player owns, which makes it a reliable ownership test.
    /// </summary>
    public bool LooksPlayerOwned =>
        CsvRow.Split(_lines[_base + LineDeltas]).Any(f => f.Trim() is not ("0" or ""));

    // --- development ------------------------------------------------------------
    /// <summary>What has been built in a category.</summary>
    public int Develop(DevelopmentCategory category) =>
        CsvRow.GetInt(CategoryLine(category), FieldDevel);

    /// <summary>Sets what has been built in a category.</summary>
    public void SetDevelop(DevelopmentCategory category, int value) =>
        SetInt(LineFirstCategory + (int)category, FieldDevel, value, 0, MaxDevelopment);

    /// <summary>Gold per investment step in a category (a fixed property of the city).</summary>
    public int Cost(DevelopmentCategory category) => CsvRow.GetInt(CategoryLine(category), FieldCost);

    /// <summary>
    /// The city's natural ceiling for a category. While Develop is below it, further investment stays
    /// cheap; past it the game charges a steep premium.
    /// </summary>
    public int Resource(DevelopmentCategory category) =>
        CsvRow.GetInt(CategoryLine(category), FieldResrc);

    /// <summary>Sets the natural ceiling for a category.</summary>
    public void SetResource(DevelopmentCategory category, int value) =>
        SetInt(LineFirstCategory + (int)category, FieldResrc, value, 0, MaxDevelopment);

    /// <summary>Gold produced by a category. Recomputed monthly by the game, so read-only here.</summary>
    public int Production(DevelopmentCategory category) =>
        CsvRow.GetInt(CategoryLine(category), FieldProd);

    /// <summary>Raises every category's Develop to that category's own resource ceiling.</summary>
    public void DevelopToResourceCeiling()
    {
        foreach (DevelopmentCategory category in Enum.GetValues<DevelopmentCategory>())
        {
            int ceiling = Resource(category);
            if (Develop(category) < ceiling) SetDevelop(category, ceiling);
        }
    }

    /// <summary>Sets morale, loyalty and health to the top of the game's natural scale.</summary>
    public void RestoreMood()
    {
        Morale = FullMood;
        Loyalty = FullMood;
        Health = FullMood;
    }

    /// <summary>
    /// Checks that this block has the shape the format requires: 14 lines, a quoted name, and seven
    /// economy lines with at least five fields each. Returns null when the block is well-formed.
    /// </summary>
    internal string? Validate()
    {
        if (_base + BlockLines > _lines.Count) return $"city block {Index} is truncated";
        if (string.IsNullOrEmpty(Name)) return $"city block {Index} has no name field";
        if (CsvRow.Count(_lines[_base + LineMood]) < 4) return $"'{Name}' mood line is too short";
        if (CsvRow.Count(_lines[_base + LineFinance]) < 3) return $"'{Name}' finance line is too short";
        if (CsvRow.Count(_lines[_base + LineRecruits]) < 3) return $"'{Name}' recruit line is too short";
        for (int i = 0; i < CategoryCount; i++)
            if (CsvRow.Count(_lines[_base + LineFirstCategory + i]) < 5)
                return $"'{Name}' economy line {i + 1} is too short";
        return null;
    }

    private string CategoryLine(DevelopmentCategory category) =>
        _lines[_base + LineFirstCategory + (int)category];

    private void SetInt(int line, int field, int value, int min, int max) =>
        _lines[_base + line] = CsvRow.SetInt(_lines[_base + line], field, Math.Clamp(value, min, max));
}
