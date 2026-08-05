using TheQuestTrainer.Memory;

namespace TheQuestTrainer.Game;

/// <summary>How the character record was found.</summary>
public enum LocateChain
{
    /// <summary>Not found.</summary>
    None = 0,

    /// <summary>Read straight out of the module's own engine-object pointer.</summary>
    StaticSlot,

    /// <summary>Found by sweeping the heap for the experience table's byte signature.</summary>
    HeapScan,
}

/// <summary>Outcome of a location attempt.</summary>
/// <param name="Record">Address of the character record, or 0 when nothing was found.</param>
/// <param name="Chain">Which chain produced it.</param>
/// <param name="Candidates">How many records passed validation (before the tie-break).</param>
/// <param name="Detail">One line for the status bar, always populated — including on failure.</param>
public readonly record struct LocateResult(uint Record, LocateChain Chain, int Candidates, string Detail)
{
    /// <summary>Whether a record was found.</summary>
    public bool Found => Record != 0 && Chain != LocateChain.None;
}

/// <summary>
/// Finds The Quest's live character record without asking the user to search for a value.
///
/// Two independent chains, one validator:
///
/// <b>Chain A — the module's own pointer.</b> <c>.data</c> holds a pointer to the engine object at
/// <see cref="QuestLayout.EngineSlotRva"/>, and the live character record is embedded in it at
/// <see cref="QuestLayout.RecordInEngine"/>. Two reads, no scanning. The slot is only read when its
/// RVA lands in a writable, non-executable section of the <i>mapped</i> PE — a different build
/// could put code there.
///
/// <b>Chain B — the structural sweep.</b> Every character record carries a copy of the per-level
/// experience table, and its first eight entries (400, 900, 1500, 2500, 4000, 7000, 11000, 17000)
/// are a 32-byte pattern nothing else in the process matches by accident. Subtracting
/// <see cref="QuestLayout.ExperienceTable"/> from a hit gives a candidate record base. This chain
/// knows no RVAs at all, so it survives a build that moves the static slot.
///
/// <b>The validator is what makes either chain safe.</b> The record's first dword must be a vtable
/// pointer into the image's read-only data; the name and portrait must be well-formed MSVC
/// <c>std::string</c>s; the name must be non-empty; the level, health, mana, attributes and race id
/// must be in range; and the embedded experience table must actually start with the signature.
/// That last pair of checks is what separates the live character from the pristine "new character"
/// prototype the game keeps in the same engine object — the prototype has no name and no
/// next-level threshold.
/// </summary>
public static class CharacterLocator
{
    /// <summary>Upper bound on records the sweep will collect before giving up on being useful.</summary>
    private const int MaxCandidates = 64;

    /// <summary>Bytes read per region during the sweep. Regions larger than this are read in slices.</summary>
    private const int SliceBytes = 4 * 1024 * 1024;

    /// <summary>
    /// Runs chain A, then chain B if A did not produce a validated record.
    /// </summary>
    public static LocateResult Locate(IMemorySource source, PeImage? image, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var viaSlot = LocateViaStaticSlot(source, image);
        if (viaSlot.Found) return viaSlot;

        var viaScan = LocateViaHeapScan(source, image, ct);
        if (viaScan.Found) return viaScan;

        return new LocateResult(0, LocateChain.None, 0,
            $"No character record found. {viaSlot.Detail} {viaScan.Detail}".Trim());
    }

    /// <summary>Chain A on its own — the module's engine-object pointer.</summary>
    public static LocateResult LocateViaStaticSlot(IMemorySource source, PeImage? image)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (image is not null && !image.IsWritableDataRva(QuestLayout.EngineSlotRva))
            return new LocateResult(0, LocateChain.None, 0,
                "Static slot skipped: its RVA is not in a writable data section of this build.");

        long slot = (long)source.ModuleBase + QuestLayout.EngineSlotRva;
        if (slot > uint.MaxValue)
            return new LocateResult(0, LocateChain.None, 0, "Static slot skipped: address out of range.");

        if (!TryReadUInt32(source, (uint)slot, out uint engine) || engine == 0)
            return new LocateResult(0, LocateChain.None, 0, "Static slot is empty — no game loaded yet?");

        long record = (long)engine + QuestLayout.RecordInEngine;
        if (record > uint.MaxValue)
            return new LocateResult(0, LocateChain.None, 0, "Static slot holds an implausible pointer.");

        if (!Validate(source, image, (uint)record, out string why))
            return new LocateResult(0, LocateChain.None, 0, $"Static slot rejected: {why}");

        return new LocateResult((uint)record, LocateChain.StaticSlot, 1,
            $"Found via the module's engine pointer at +0x{QuestLayout.EngineSlotRva:X}.");
    }

    /// <summary>Chain B on its own — the experience-table sweep. Knows no RVAs.</summary>
    public static LocateResult LocateViaHeapScan(IMemorySource source, PeImage? image, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        byte[] pattern = SignatureBytes();
        var accepted = new List<uint>();
        int seen = 0;

        foreach (uint hit in ScanForPattern(source, pattern, ct))
        {
            seen++;
            if (hit < QuestLayout.ExperienceTable) continue;
            uint record = hit - QuestLayout.ExperienceTable;
            if (!Validate(source, image, record, out _)) continue;
            if (!accepted.Contains(record)) accepted.Add(record);
            if (accepted.Count >= MaxCandidates) break;
        }

        if (accepted.Count == 0)
            return new LocateResult(0, LocateChain.None, 0,
                seen == 0
                    ? "Heap sweep found no experience table — is a game loaded?"
                    : $"Heap sweep found {seen} experience table(s) but none belonged to a live character.");

        uint best = PickBest(source, accepted);
        return new LocateResult(best, LocateChain.HeapScan, accepted.Count,
            $"Found by sweeping the heap for the experience table ({accepted.Count} candidate(s)).");
    }

    /// <summary>
    /// Whether the record at <paramref name="record"/> is a live playable character.
    /// <paramref name="why"/> explains the first failed check, for the status line.
    /// </summary>
    public static bool Validate(IMemorySource source, PeImage? image, uint record, out string why)
    {
        ArgumentNullException.ThrowIfNull(source);

        why = "";
        if (record == 0 || (record & 3) != 0) { why = "address is null or unaligned."; return false; }

        var buffer = new byte[QuestLayout.RecordBytes];
        if (source.Read(record, buffer, buffer.Length) != buffer.Length) { why = "record is unreadable."; return false; }

        uint vtable = BitConverter.ToUInt32(buffer, (int)QuestLayout.VTable);
        long rva = (long)vtable - source.ModuleBase;
        if (rva < 0 || rva >= source.ModuleSize) { why = "first dword is not a pointer into the game module."; return false; }
        if (image is not null && !image.IsReadOnlyDataRva((uint)rva)) { why = "first dword does not point at read-only data."; return false; }

        for (int i = 0; i < GameTables.ExperienceSignature.Count; i++)
        {
            uint want = GameTables.ExperienceSignature[i];
            uint got = BitConverter.ToUInt32(buffer, (int)QuestLayout.ExperienceTable + i * 4);
            if (got != want) { why = "the embedded experience table does not match."; return false; }
        }

        if (!StdString.IsPlausible(source, buffer, (int)QuestLayout.Name, requireNonEmpty: true))
        { why = "the name is empty or not a std::string (this is the game's new-character prototype)."; return false; }

        if (!StdString.IsPlausible(source, buffer, (int)QuestLayout.PortraitId, requireNonEmpty: false))
        { why = "the portrait id is not a std::string."; return false; }

        ushort level = BitConverter.ToUInt16(buffer, (int)QuestLayout.Level);
        if (level < 1 || level > GameFacts.MaxLevel) { why = $"level {level} is out of range."; return false; }

        uint nextLevel = BitConverter.ToUInt32(buffer, (int)QuestLayout.ExperienceForNextLevel);
        if (nextLevel == 0) { why = "no next-level threshold (this is the game's new-character prototype)."; return false; }

        ushort health = BitConverter.ToUInt16(buffer, (int)QuestLayout.Health);
        ushort mana = BitConverter.ToUInt16(buffer, (int)QuestLayout.Mana);
        if (health > GameFacts.MaxHealthOrMana || mana > GameFacts.MaxHealthOrMana)
        { why = "health or mana is out of range."; return false; }

        for (int id = 1; id <= GameTables.Attributes.Count; id++)
        {
            ushort value = BitConverter.ToUInt16(buffer, (int)QuestLayout.BaseAttributes + id * 2);
            if (value == 0 || value > GameFacts.MaxHealthOrMana) { why = $"attribute {id} is {value}."; return false; }
        }

        uint race = BitConverter.ToUInt32(buffer, (int)QuestLayout.Race);
        if (race >= (uint)GameTables.Races.Count) { why = $"race id {race} is unknown."; return false; }

        return true;
    }

    /// <summary>
    /// Picks between validated candidates. The live character is the one that has played: highest
    /// experience wins, then the lowest address so the choice is stable across sessions.
    /// </summary>
    private static uint PickBest(IMemorySource source, List<uint> candidates)
    {
        uint best = candidates[0];
        long bestExp = -1;
        foreach (uint c in candidates)
        {
            long exp = TryReadUInt32(source, c + QuestLayout.Experience, out uint e) ? e : -1;
            if (exp > bestExp || (exp == bestExp && c < best))
            {
                bestExp = exp;
                best = c;
            }
        }
        return best;
    }

    /// <summary>The experience-table signature as little-endian bytes.</summary>
    public static byte[] SignatureBytes()
    {
        var pattern = new byte[GameTables.ExperienceSignature.Count * 4];
        for (int i = 0; i < GameTables.ExperienceSignature.Count; i++)
            BitConverter.GetBytes(GameTables.ExperienceSignature[i]).CopyTo(pattern, i * 4);
        return pattern;
    }

    /// <summary>
    /// Yields every address whose bytes match <paramref name="pattern"/>. Regions are read in
    /// slices that overlap by <c>pattern.Length - 1</c> so a match never falls down the seam.
    /// </summary>
    private static IEnumerable<uint> ScanForPattern(IMemorySource source, byte[] pattern, CancellationToken ct)
    {
        var buffer = new byte[SliceBytes];
        int overlap = pattern.Length - 1;

        foreach (var region in source.Regions())
        {
            ct.ThrowIfCancellationRequested();
            if (region.Size < pattern.Length) continue;

            long offset = 0;
            while (offset < region.Size)
            {
                ct.ThrowIfCancellationRequested();
                int want = (int)Math.Min(SliceBytes, region.Size - offset);
                uint sliceBase = region.Base + (uint)offset;
                int got = source.Read(sliceBase, buffer, want);
                if (got == want)
                {
                    for (int i = 0; i + pattern.Length <= want; i++)
                    {
                        if (!MatchesAt(buffer, i, pattern)) continue;
                        yield return sliceBase + (uint)i;
                    }
                }

                if (want <= overlap) break;
                offset += want - overlap;
            }
        }
    }

    private static bool MatchesAt(byte[] data, int offset, byte[] pattern)
    {
        for (int k = 0; k < pattern.Length; k++)
            if (data[offset + k] != pattern[k]) return false;
        return true;
    }

    private static bool TryReadUInt32(IMemorySource source, uint address, out uint value)
    {
        var word = new byte[4];
        if (source.Read(address, word, 4) != 4) { value = 0; return false; }
        value = BitConverter.ToUInt32(word, 0);
        return true;
    }
}
