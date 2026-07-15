namespace QuestForGlory1Trainer.Memory;

/// <summary>
/// Describes a successfully located stat block in guest RAM.
/// All addresses are live process addresses (not dump file offsets).
/// </summary>
public sealed class LocatedStats
{
    /// <summary>Address of the first stat word (STR).</summary>
    public nuint StrAddress { get; }

    /// <summary>Raw 16-bit values for the 13 skill stats (words 0–12).</summary>
    public IReadOnlyList<short> SkillValues { get; }

    /// <summary>
    /// Address of word 14: HP internal. Displayed HP = HP_internal / 2.
    /// </summary>
    public nuint HpAddress { get; }

    /// <summary>
    /// Address of word 15: Stamina internal. Max raw = STR×3 + VIT×4; displayed = raw / 4 (rounded).
    /// </summary>
    public nuint StaminaAddress { get; }

    /// <summary>
    /// Address of word 16: Mana current. Displayed Mana = this value (1:1).
    /// </summary>
    public nuint ManaAddress { get; }

    /// <summary>Snapshot of the raw HP_internal word read at locate time.</summary>
    public short HpRaw { get; }

    /// <summary>Snapshot of the raw Stamina_internal word read at locate time.</summary>
    public short StaminaRaw { get; }

    /// <summary>Snapshot of the Mana word read at locate time.</summary>
    public short ManaRaw { get; }

    public LocatedStats(nuint strAddress, IReadOnlyList<short> skillValues,
                        short hpRaw, short staminaRaw, short manaRaw)
    {
        StrAddress     = strAddress;
        SkillValues    = skillValues;
        HpAddress      = strAddress + (14 * 2);
        StaminaAddress = strAddress + (15 * 2);
        ManaAddress    = strAddress + (16 * 2);
        HpRaw          = hpRaw;
        StaminaRaw     = staminaRaw;
        ManaRaw        = manaRaw;
    }

    /// <summary>Displayed HP value (internal / 2).</summary>
    public int HpDisplayed => HpRaw / 2;

    /// <summary>Displayed Stamina value (internal / 4, rounded).</summary>
    public int StaminaDisplayed => (int)Math.Round(StaminaRaw / 4.0);

    /// <summary>Mana (displayed == internal, 1:1).</summary>
    public int ManaDisplayed => ManaRaw;
}

/// <summary>
/// Locates the Quest for Glory I character stat block in DOSBox guest RAM using a purely
/// structural scan — no user-supplied stat values needed.
///
/// <b>Memory layout (confirmed from live DOSBox dump):</b>
/// <code>
/// Word  0: STR        Word  5: Weapon Use   Word 10: Throwing
/// Word  1: INT        Word  6: Parry        Word 11: Climbing
/// Word  2: AGI        Word  7: Dodge        Word 12: Magic skill
/// Word  3: VIT        Word  8: Stealth      Word 13: XP (raw)
/// Word  4: LCK        Word  9: Pick Locks   Word 14: HP internal (displayed × 2)
///                                           Word 15: Stamina internal (STR×3+VIT×4 when full)
///                                           Word 16: Mana current (1:1 with displayed)
/// </code>
///
/// <b>Disambiguation strategy:</b> When multiple candidate windows pass the structural
/// constraints, the one whose HP_raw and Stamina_raw are <em>closest to their respective
/// formula maximums</em> is returned. A freshly loaded save game will have both resources
/// near their formula ceilings; a false-positive data structure will not.
/// </summary>
public static class StatLocator
{
    private const int  ChunkSize        = 1 << 20;          // 1 MiB scan window
    private const int  PageSize         = 0x1000;           // salvage granularity
    private const int  StatWordCount    = 17;               // words 0–16
    private const int  BlockBytes       = StatWordCount * 2;
    private const long MinRegionBytes   = 2 * 1024 * 1024; // 2 MiB — DOSBox guest RAM is ≥16 MiB;
                                                            // false positives live in ≤1.8 MiB regions

    /// <summary>
    /// Scans all readable regions of <paramref name="mem"/> for a stat block matching the
    /// QFG1 structural layout. Returns the candidate whose HP and Stamina are closest to
    /// their formula maximums, or <c>null</c> when no character data is present (e.g. the
    /// game is still at the title screen).
    /// </summary>
    public static LocatedStats? Find(ProcessMemory mem, CancellationToken ct = default)
    {
        byte[] buf = new byte[ChunkSize + BlockBytes];
        var candidates = new List<LocatedStats>();

        foreach (var region in mem.EnumerateRegions())
        {
            if ((long)region.Size < MinRegionBytes) continue;   // skip small regions — all observed false positives are ≤1.8 MiB
            ct.ThrowIfCancellationRequested();
            nuint regionEnd = region.Base + region.Size;

            for (nuint start = region.Base; start < regionEnd;)
            {
                nuint remaining = regionEnd - start;
                int want    = (int)Math.Min((nuint)ChunkSize, remaining);
                int readLen = (int)Math.Min((nuint)(want + BlockBytes), remaining);
                int read    = mem.Read(start, buf, readLen);

                if (read >= BlockBytes)
                {
                    int limit = read - BlockBytes + 1;
                    for (int i = 0; i < limit; i += 2)
                    {
                        var located = TryValidate(buf, i, read, start);
                        if (located != null) candidates.Add(located);
                    }
                }
                else if (want > PageSize)
                {
                    ScanByPage(mem, start, regionEnd, candidates, ct);
                    break;
                }

                start += (nuint)Math.Max(PageSize, want);
            }
        }

        return BestCandidate(candidates);
    }

    // ---- helpers ------------------------------------------------------------

    private static short Read16(byte[] buf, int off)
        => (short)(buf[off] | (buf[off + 1] << 8));

    private static LocatedStats? TryValidate(byte[] buf, int offset, int read, nuint windowBase)
    {
        if (offset + BlockBytes > read) return null;

        // words 0–4: base stats — must all be in [1, 100]
        for (int w = 0; w <= 4; w++)
        {
            short v = Read16(buf, offset + w * 2);
            if (v < 1 || v > 100) return null;
        }

        short str = Read16(buf, offset + 0);
        short vit = Read16(buf, offset + 6); // word 3

        // words 5–12: skill values — must be in [0, 200]
        for (int w = 5; w <= 12; w++)
        {
            short v = Read16(buf, offset + w * 2);
            if (v < 0 || v > 200) return null;
        }

        // word 13: XP — non-negative
        if (Read16(buf, offset + 13 * 2) < 0) return null;

        // word 14: HP internal.
        // Max HP_raw = STR+VIT+1 (confirmed from dump: STR=10,VIT=15 → HPraw=26=10+15+1).
        // Allow 3× that ceiling as headroom for fully levelled characters.
        short hpRaw = Read16(buf, offset + 14 * 2);
        int hpMax = str + vit + 1;
        if (hpRaw < 2 || hpRaw > hpMax * 3) return null;

        // word 15: Stamina internal.
        // Max Stamina_raw = STR×3 + VIT×4 (confirmed: STR=10,VIT=15 → StamRaw=90=30+60).
        // Require the raw value to be in [10% of max, max].
        short stamRaw = Read16(buf, offset + 15 * 2);
        int maxStam = str * 3 + vit * 4;
        int minStam = Math.Max(2, maxStam / 10);
        if (stamRaw < minStam || stamRaw > maxStam) return null;

        // Stamina_raw must be at least as large as HP_raw: max stamina always exceeds max HP
        // (STR×3+VIT×4 > STR+VIT+1 for any positive stats).
        if (stamRaw < hpRaw) return null;

        // word 16: Mana — in [0, 200]
        short manaRaw = Read16(buf, offset + 16 * 2);
        if (manaRaw < 0 || manaRaw > 200) return null;

        // Gold and Silver sit at −284 / −282 bytes from the STR word (word 0).
        // Confirmed from dump analysis: both are non-negative SCI0 currency globals < 10,000.
        // This rejects false-positive data structures that coincidentally satisfy all
        // structural stat checks but don't have valid currency values at those offsets.
        if (offset >= 284)
        {
            short gold   = Read16(buf, offset - 284);
            short silver = Read16(buf, offset - 282);
            if (gold < 0 || gold >= 10_000 || silver < 0 || silver >= 10_000) return null;
        }

        var skills = new short[13];
        for (int i = 0; i < 13; i++)
            skills[i] = Read16(buf, offset + i * 2);

        nuint strAddress = windowBase + (nuint)offset;
        return new LocatedStats(strAddress, skills, hpRaw, stamRaw, manaRaw);
    }

    /// <summary>
    /// Picks the best candidate from those that passed structural validation.
    /// Scoring is based on how close HP_raw and Stamina_raw are to their formula
    /// maximums. A freshly loaded save will score near zero; a false-positive data
    /// structure will score higher (farther from its "formula" targets).
    /// </summary>
    private static LocatedStats? BestCandidate(List<LocatedStats> candidates)
    {
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        LocatedStats? best = null;
        double bestScore = double.MaxValue;

        foreach (var c in candidates)
        {
            short str = c.SkillValues[0];
            short vit = c.SkillValues[3];
            int maxHp  = str + vit + 1;
            int maxSt  = str * 3 + vit * 4;

            double hpDelta   = Math.Abs(c.HpRaw      - maxHp) / (double)maxHp;
            double stamDelta = Math.Abs(c.StaminaRaw  - maxSt) / (double)maxSt;
            double score     = hpDelta + stamDelta;

            if (score < bestScore) { bestScore = score; best = c; }
        }

        return best;
    }

    private static void ScanByPage(ProcessMemory mem, nuint start, nuint regionEnd,
                                   List<LocatedStats> candidates, CancellationToken ct)
    {
        byte[] page = new byte[PageSize + BlockBytes];
        for (nuint p = start; p < regionEnd; p += PageSize)
        {
            ct.ThrowIfCancellationRequested();
            nuint remaining = regionEnd - p;
            int readLen = (int)Math.Min((nuint)(PageSize + BlockBytes), remaining);
            int read    = mem.Read(p, page, readLen);
            if (read < BlockBytes) continue;

            int limit = read - BlockBytes + 1;
            for (int i = 0; i < limit; i += 2)
            {
                var located = TryValidate(page, i, read, p);
                if (located != null) candidates.Add(located);
            }
        }
    }

    /// <summary>
    /// Searches a ±256 KB window around the already-located global HP address for the SCI0
    /// Ego actor's property copy. Dump analysis confirmed the actor block stores resources in
    /// reversed order relative to the globals: the actor layout is […, Stam, HP, Mana, …]
    /// whereas the global stat block uses […, HP, Stam, Mana]. The SCI0 combat engine
    /// applies damage to the actor properties; freezing only the global is insufficient during
    /// combat. Returns the actor HP word address, or <c>nuint.Zero</c> when not found.
    /// </summary>
    public static nuint FindActorHp(ProcessMemory mem, LocatedStats stats)
    {
        const int HalfWindow = 256 * 1024;

        short hpRaw   = stats.HpRaw;
        short stamRaw = stats.StaminaRaw;
        short manaRaw = stats.ManaRaw;

        short str   = stats.SkillValues[0];
        short intel = stats.SkillValues[1];
        short agi   = stats.SkillValues[2];
        short vit   = stats.SkillValues[3];
        short lck   = stats.SkillValues[4];

        nuint mainHpAddr  = stats.HpAddress;
        nuint regionStart = mainHpAddr > (nuint)HalfWindow
            ? mainHpAddr - (nuint)HalfWindow
            : nuint.Zero;

        int bufLen = HalfWindow * 2 + 8;
        byte[] buf = new byte[bufLen];
        int read = mem.Read(regionStart, buf, bufLen);
        if (read < 6) return nuint.Zero;

        for (int i = 0; i + 5 < read; i += 2)
        {
            if (Read16(buf, i)     != stamRaw) continue;
            if (Read16(buf, i + 2) != hpRaw)  continue;
            if (Read16(buf, i + 4) != manaRaw) continue;

            nuint candidateHpAddr = regionStart + (nuint)(i + 2);
            if (candidateHpAddr == mainHpAddr) continue;

            int hpOff = i + 2;
            if (hpOff < 20) continue;
            if (Read16(buf, hpOff - 20) != str)   continue;
            if (Read16(buf, hpOff - 18) != intel)  continue;
            if (Read16(buf, hpOff - 16) != agi)    continue;
            if (Read16(buf, hpOff - 14) != vit)    continue;
            if (Read16(buf, hpOff - 12) != lck)    continue;

            return candidateHpAddr;
        }

        return nuint.Zero;
    }
}
