namespace DarkDesigns1Trainer.Game;

/// <summary>
/// The character-creation "rolled stats" buffer, and the dice the game rolls it from.
///
/// On the (C)reate screen the game rolls <see cref="RolledCount"/> values into a small array of
/// uint16 LE — the same width the finished record uses for its attributes — and lets the player
/// arrange them across Strength/Dexterity/Constitution/Intelligence/Piety with the arrow keys and
/// Return, or press <c>R</c> to throw the whole set away and roll again. The pool is <em>not</em>
/// a roster record yet (no name, class or level exists until the arrangement is finished), so
/// <see cref="Memory.RosterLocator"/> cannot see it; <see cref="Memory.CreationScanner"/> locates
/// it separately.
///
/// Because every rolled value can go to any attribute, a per-attribute target is a question about
/// the pool as a <em>multiset</em>, not about any particular slot — see <see cref="Arrange"/>.
///
/// <para><b>Confirmed live</b> (DOSBox-X, running game): the five values are contiguous uint16 LE,
/// they change on every <c>R</c>, and a value written into the pool is picked up by the game and
/// becomes the created character's attribute — so the trainer can both read rolls and set them.</para>
/// </summary>
public static class CreationFormat
{
    /// <summary>How many values the create screen rolls — one per attribute.</summary>
    public const int RolledCount = CharacterFormat.AttributeCount;   // 5

    /// <summary>Each rolled value is a uint16 LE, matching the record's attribute width.</summary>
    public const int ValueSize = CharacterFormat.AttributeSize;      // 2

    /// <summary>Size of the whole rolled-stat pool in bytes.</summary>
    public const int PoolBytes = RolledCount * ValueSize;            // 10

    // --- the roll's distribution ---------------------------------------------
    // Measured from the running game: 400 automated re-rolls (2,000 values) gave a mean of 13.99
    // over a 10..18 range with the frequencies 3.80 / 7.90 / 12.25 / 16.50 / 19.95 / 15.40 / 12.50 /
    // 8.55 / 3.15 %, an almost exact fit (chi-square p ~ 0.66, 8 d.f.) to the sum of two uniform
    // 0..4 draws plus 10 — i.e. Borland C's `10 + random(5) + random(5)`. All five positions came
    // out independent and identically distributed (per-position means 13.84–14.07).

    /// <summary>Lowest value a rolled stat can take (both dice at their minimum).</summary>
    public const int RollBase = 10;

    /// <summary>Faces on each of the two dice: a uniform 0..<see cref="DieSides"/>-1 draw.</summary>
    public const int DieSides = 5;

    /// <summary>How many dice are summed into one rolled value.</summary>
    public const int DiceCount = 2;

    /// <summary>Lowest value one rolled stat can be (10).</summary>
    public const int MinRoll = RollBase;

    /// <summary>Highest value one rolled stat can be (18).</summary>
    public const int MaxRoll = RollBase + DiceCount * (DieSides - 1);

    /// <summary>Lowest possible total across all five rolled values (50).</summary>
    public const int MinTotal = RolledCount * MinRoll;

    /// <summary>Highest possible total across all five rolled values (90).</summary>
    public const int MaxTotal = RolledCount * MaxRoll;

    // The target boxes accept numbers above what the dice can reach on purpose. Clamping them to
    // MaxRoll would silently rewrite an over-ambitious target into an achievable one, leaving the
    // player wondering why the roller stopped instantly; letting the value through instead lets the
    // odds readout say "out of reach" and point at the write-the-roll-directly option.

    /// <summary>Highest per-attribute minimum the target boxes accept — deliberately above
    /// <see cref="MaxRoll"/> so an unreachable target can be stated and explained.</summary>
    public const int MaxTargetValue = 99;

    /// <summary>Highest total minimum the target box accepts, for the same reason.</summary>
    public const int MaxTargetTotal = RolledCount * MaxTargetValue;

    // The plausibility gate is deliberately wider than the observed 10..18 roll: it only has to
    // answer "do these bytes still look like a rolled stat pool?" (for the signature scan and for
    // noticing that the create screen has closed), so it uses the game's full attribute range.
    // The tight 10..18 range above is what the odds model is built on.

    /// <summary>Lowest value the plausibility gate accepts (the game's attribute floor).</summary>
    public const int MinPlausible = GameFacts.AttributeMin;   // 3

    /// <summary>Highest value the plausibility gate accepts (the game's attribute ceiling).</summary>
    public const int MaxPlausible = GameFacts.AttributeMax;   // 18

    /// <summary>Labels for the five rolled values, in the order the create screen shows them.</summary>
    public static readonly string[] SlotNames =
        { "Rolled #1", "Rolled #2", "Rolled #3", "Rolled #4", "Rolled #5" };

    /// <summary>Labels for the rolled values ranked highest to lowest, used by the statistics panel.</summary>
    public static readonly string[] RankNames =
        { "Best", "2nd", "3rd", "4th", "Worst" };

    // --- plausibility --------------------------------------------------------
    /// <summary>True when every value looks like a rolled stat (within
    /// <see cref="MinPlausible"/>..<see cref="MaxPlausible"/>).</summary>
    public static bool LooksLikeRoll(IReadOnlyList<int> values)
    {
        if (values == null || values.Count < RolledCount) return false;
        for (int i = 0; i < RolledCount; i++)
            if (values[i] < MinPlausible || values[i] > MaxPlausible) return false;
        return true;
    }

    /// <summary>Sum of the rolled values.</summary>
    public static int Total(IReadOnlyList<int> values)
    {
        int t = 0;
        for (int i = 0; i < values.Count && i < RolledCount; i++) t += values[i];
        return t;
    }

    // --- arranging the pool onto the attributes ------------------------------
    /// <summary>
    /// Works out which rolled value to put on each attribute so that every attribute reaches its
    /// minimum, and returns <c>slot[a]</c> = the index of the rolled value assigned to attribute
    /// <c>a</c> (attributes in <see cref="CharacterFormat.AttributeNames"/> order). Returns null
    /// when no arrangement can satisfy the minimums.
    ///
    /// The greedy rule — hand the largest remaining value to the attribute with the largest
    /// remaining minimum — is optimal here because the constraints are all plain lower bounds: if
    /// some arrangement works, swapping any two assignments back into descending order keeps both
    /// of them satisfied, so the fully sorted arrangement works too. A minimum of 0 (or below)
    /// means "no requirement" and is always satisfied.
    /// </summary>
    public static int[]? Arrange(IReadOnlyList<int> rolled, IReadOnlyList<int>? mins)
    {
        if (rolled == null || rolled.Count < RolledCount) return null;

        // Attribute indices ordered by their minimum, most demanding first; ties keep attribute
        // order so the same roll and target always produce the same suggestion.
        var attrs = new int[RolledCount];
        for (int i = 0; i < RolledCount; i++) attrs[i] = i;
        Array.Sort(attrs, (x, y) =>
        {
            int c = MinOf(mins, y).CompareTo(MinOf(mins, x));
            return c != 0 ? c : x.CompareTo(y);
        });

        // Rolled-value slots ordered by value, largest first; ties keep slot order for the same reason.
        var slots = new int[RolledCount];
        for (int i = 0; i < RolledCount; i++) slots[i] = i;
        Array.Sort(slots, (x, y) =>
        {
            int c = rolled[y].CompareTo(rolled[x]);
            return c != 0 ? c : x.CompareTo(y);
        });

        var result = new int[RolledCount];
        for (int i = 0; i < RolledCount; i++)
        {
            int attr = attrs[i], slot = slots[i];
            if (rolled[slot] < MinOf(mins, attr)) return null;   // the best value left can't reach it
            result[attr] = slot;
        }
        return result;
    }

    /// <summary>True when the rolled values can be arranged to meet every attribute minimum
    /// <em>and</em> they sum to at least <paramref name="totalMin"/>.</summary>
    public static bool MeetsTarget(IReadOnlyList<int> rolled, IReadOnlyList<int>? mins, int totalMin) =>
        Arrange(rolled, mins) != null && Total(rolled) >= totalMin;

    /// <summary>
    /// How far a roll falls short of the target, as a single number used to rank one roll against
    /// another ("best so far"): the per-attribute gaps once both the values and the minimums are
    /// lined up largest-first, plus any shortfall on the total. 0 means the target is met.
    /// </summary>
    public static int Shortfall(IReadOnlyList<int> rolled, IReadOnlyList<int>? mins, int totalMin)
    {
        var values = SortedDescending(rolled);
        var need = SortedDescendingMins(mins);

        int short_ = 0;
        for (int i = 0; i < RolledCount; i++)
        {
            int d = need[i] - values[i];
            if (d > 0) short_ += d;
        }
        int t = totalMin - Total(rolled);
        if (t > 0) short_ += t;
        return short_;
    }

    /// <summary>The rolled values sorted highest first (the pool's rank order).</summary>
    public static int[] SortedDescending(IReadOnlyList<int> rolled)
    {
        var v = new int[RolledCount];
        for (int i = 0; i < RolledCount; i++) v[i] = i < rolled.Count ? rolled[i] : 0;
        Array.Sort(v);
        Array.Reverse(v);
        return v;
    }

    /// <summary>The attribute minimums sorted highest first, with blanks read as "no requirement".</summary>
    public static int[] SortedDescendingMins(IReadOnlyList<int>? mins)
    {
        var v = new int[RolledCount];
        for (int i = 0; i < RolledCount; i++) v[i] = MinOf(mins, i);
        Array.Sort(v);
        Array.Reverse(v);
        return v;
    }

    private static int MinOf(IReadOnlyList<int>? mins, int index)
    {
        if (mins == null || index < 0 || index >= mins.Count) return 0;
        return Math.Max(0, mins[index]);
    }

    // --- encoding ------------------------------------------------------------
    /// <summary>Decodes the five uint16 LE values at <paramref name="offset"/> into
    /// <paramref name="dest"/>. Returns false when the buffer is too short.</summary>
    public static bool Decode(byte[] buffer, int offset, int[] dest)
    {
        if (buffer == null || dest == null || dest.Length < RolledCount) return false;
        if (offset < 0 || offset + PoolBytes > buffer.Length) return false;
        for (int i = 0; i < RolledCount; i++)
        {
            int o = offset + i * ValueSize;
            dest[i] = buffer[o] | (buffer[o + 1] << 8);
        }
        return true;
    }

    /// <summary>
    /// Parses a typed-in roll: <see cref="RolledCount"/> numbers, or a single number to use for all
    /// of them, separated by spaces, commas, tabs or slashes. Values are clamped to the game's
    /// <em>attribute</em> range (<see cref="MinPlausible"/>..<see cref="MaxPlausible"/>, i.e. 3–18)
    /// so the create screen is never handed something it can't draw. Note that is wider than the
    /// range the dice actually produce (<see cref="MinRoll"/>..<see cref="MaxRoll"/>, 10–18):
    /// writing a value the game could not have rolled is the point of this feature. Returns false
    /// with a message the UI can show verbatim.
    /// </summary>
    public static bool TryParseValues(string? text, out int[] values, out string error)
    {
        values = Array.Empty<int>();
        error = "";
        var parts = (text ?? "").Split(new[] { ' ', ',', '\t', '/' }, StringSplitOptions.RemoveEmptyEntries);

        var parsed = new List<int>();
        foreach (var p in parts)
        {
            if (!int.TryParse(p, out int n))
            {
                error = $"'{p}' isn't a number. Enter {RolledCount} values, or one value to use for all of them.";
                return false;
            }
            parsed.Add(Math.Clamp(n, MinPlausible, MaxPlausible));
        }

        if (parsed.Count == 1)
        {
            values = new int[RolledCount];
            Array.Fill(values, parsed[0]);
            return true;
        }
        if (parsed.Count == RolledCount)
        {
            values = parsed.ToArray();
            return true;
        }

        error = $"Enter {RolledCount} values (or one value to use for all of them); "
              + $"each is clamped to {MinPlausible}–{MaxPlausible}.";
        return false;
    }

    /// <summary>Encodes five values as uint16 LE, ready to write over the pool.</summary>
    public static byte[] Encode(IReadOnlyList<int> values)
    {
        var buf = new byte[PoolBytes];
        for (int i = 0; i < RolledCount; i++)
        {
            int v = i < values.Count ? Math.Clamp(values[i], 0, 0xFFFF) : 0;
            buf[i * ValueSize] = (byte)(v & 0xFF);
            buf[i * ValueSize + 1] = (byte)((v >> 8) & 0xFF);
        }
        return buf;
    }
}
