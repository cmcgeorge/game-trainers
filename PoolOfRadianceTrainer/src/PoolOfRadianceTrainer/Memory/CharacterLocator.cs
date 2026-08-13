using PoolOfRadianceTrainer.Game;

namespace PoolOfRadianceTrainer.Memory;

/// <summary>A located character/monster record: its live process address and a decoded view.</summary>
public sealed class LocatedCharacter
{
    public nuint Address { get; }
    public CharacterRecord Record { get; }

    public LocatedCharacter(nuint address, CharacterRecord record)
    {
        Address = address;
        Record = record;
    }

    public bool IsMonster => Record.LooksLikeMonster;

    /// <summary>A monster record the game is really fighting with (see
    /// <see cref="CharacterRecord.LooksLikeLiveCombatant"/>), not a look-alike scratch buffer.</summary>
    public bool IsLiveMonster => Record.LooksLikeMonster && Record.LooksLikeLiveCombatant;

    public override string ToString() => $"{Record.Name} @ 0x{(ulong)Address:X}";
}

/// <summary>
/// Scans a target process for Pool of Radiance character/monster records by testing the
/// <see cref="CharacterSignature"/> at every byte offset of every committed region.
/// Party members and any in-combat monsters share the same record format, so both are
/// returned; the caller distinguishes them (monsters have race 0 / class 17).
/// </summary>
public static class CharacterLocator
{
    private const int ChunkSize = 1 << 20;   // 1 MiB scan window

    public static List<LocatedCharacter> FindAll(ProcessMemory mem,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var hits = new List<LocatedCharacter>();
        var regions = mem.EnumerateRegions().ToList();

        nuint totalBytes = 0;
        foreach (var r in regions) totalBytes += r.Size;
        nuint scanned = 0;

        // One reusable buffer across the whole walk avoids thousands of LOH allocations.
        byte[] buf = new byte[ChunkSize + PorFormat.RecordSize];
        var seen = new HashSet<ulong>();

        foreach (var region in regions)
        {
            ct.ThrowIfCancellationRequested();

            for (nuint offset = 0; offset < region.Size;)
            {
                int want = (int)Math.Min((nuint)ChunkSize, region.Size - offset);
                // Read an extra record's worth so a record straddling a chunk edge is still seen.
                int readWant = (int)Math.Min((nuint)(ChunkSize + PorFormat.RecordSize), region.Size - offset);
                int read = mem.Read(region.Base + offset, buf, readWant);
                if (read < PorFormat.RecordSize)
                {
                    scanned += (nuint)want;
                    break;
                }

                for (int i = 0; i + PorFormat.RecordSize <= read; i++)
                {
                    if (!CharacterSignature.Looks(buf, i)) continue;
                    nuint absolute = region.Base + offset + (nuint)i;
                    if (!seen.Add((ulong)absolute)) continue;
                    hits.Add(new LocatedCharacter(absolute, new CharacterRecord(buf, i)));
                }

                // On a full read the +RecordSize overlap already caught boundary-straddling
                // records, so advance by `want`. On a short (partial) read, advance only past
                // what we could actually scan so readable records past the gap aren't skipped.
                nuint advance = read >= want
                    ? (nuint)want
                    : (nuint)Math.Max(1, read - PorFormat.RecordSize + 1);
                offset += advance;
                scanned += advance;
                progress?.Report(totalBytes == 0 ? 0 : Math.Min(1.0, (double)scanned / totalBytes));
            }
        }

        // Party members cluster together (adjacent-ish addresses); monsters live in the
        // combat arena. Sort by address so the party reads top-to-bottom in game order.
        hits.Sort((a, b) => a.Address.CompareTo(b.Address));

        return Dedupe(hits);
    }

    /// <summary>
    /// Drops the extra copies DOSBox produces when it maps the same guest RAM at more than one host
    /// address, without losing genuinely distinct creatures that happen to read the same.
    ///
    /// <para>Byte-identical records alone are not proof of aliasing: two same-species monsters can be
    /// byte-for-byte equal at the moment a fight starts, and collapsing them would quietly lose a
    /// combatant. What separates the two cases is distance. The game keeps the party and the combat
    /// arena inside one 640 KiB DOS heap, so real creatures are always within
    /// <see cref="ArenaRadius"/> of each other; a second mapping of that heap is a different host
    /// region entirely, far outside it. So an identical record is treated as an alias only when it
    /// is further away than any real creature could be.</para>
    /// </summary>
    public static List<LocatedCharacter> Dedupe(List<LocatedCharacter> hits)
    {
        // Keyed on a hash of the record bytes rather than a hex string — this runs once per hit of
        // a full-process scan, and the string would be 570 chars of garbage each time.
        var kept = new Dictionary<ulong, List<LocatedCharacter>>();
        var deduped = new List<LocatedCharacter>(hits.Count);

        foreach (var h in hits)
        {
            ulong key = Fnv1a(h.Record.Bytes);
            if (!kept.TryGetValue(key, out var sameHash))
                kept[key] = sameHash = new List<LocatedCharacter>();

            bool isAlias = false;
            foreach (var prior in sameHash)
            {
                if (!h.Record.Bytes.AsSpan().SequenceEqual(prior.Record.Bytes)) continue;   // hash collision
                nuint gap = h.Address > prior.Address ? h.Address - prior.Address : prior.Address - h.Address;
                if (gap > (nuint)ArenaRadius) { isAlias = true; break; }
            }
            if (isAlias) continue;

            sameHash.Add(h);
            deduped.Add(h);
        }
        return deduped;
    }

    private static ulong Fnv1a(byte[] bytes)
    {
        ulong hash = 14695981039346656037;
        foreach (byte b in bytes) hash = (hash ^ b) * 1099511628211;
        return hash;
    }

    // --- combat-arena sweep --------------------------------------------------
    // Monster records exist only while a battle is on screen, and the game builds them fresh
    // (at fresh addresses) for every encounter, so the enemy list can't come from the one-off
    // full scan — it has to be re-found as the battle runs. A full 250 MiB walk is far too slow
    // to repeat on the poll timer, but the arena is always allocated in the same DOS heap as the
    // party, so sweeping a window around the party records is both cheap and sufficient.

    /// <summary>Bytes swept either side of the party by <see cref="FindCombatants"/>. 512 KiB
    /// covers the whole 640 KiB DOS conventional-memory area the game's heap lives in.</summary>
    public const int ArenaRadius = 0x80000;

    private const int SweepChunk = 1 << 16;   // 64 KiB per read: one unreadable page costs little

    /// <summary>Length of the scratch buffer <see cref="FindCombatants"/> requires.</summary>
    public const int SweepBufferSize = SweepChunk + PorFormat.RecordSize;

    /// <summary>
    /// Re-finds the live monster records in the combat arena — the window reaching
    /// <see cref="ArenaRadius"/> bytes either side of the party — so the combat panel can follow a
    /// battle without a full re-scan. Only records that decode as a monster *and* as a plausible
    /// live combatant are returned, so a stale look-alike buffer never shows up as an enemy.
    /// Results are address-ordered (the order the game built the encounter in).
    /// <paramref name="buffer"/> is a reusable scratch buffer of <see cref="SweepBufferSize"/> bytes.
    /// </summary>
    public static List<LocatedCharacter> FindCombatants(ProcessMemory mem, nuint partyLow, nuint partyHigh, byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (buffer.Length < SweepBufferSize)
            throw new ArgumentException($"buffer must be at least {SweepBufferSize} bytes.", nameof(buffer));

        var hits = new List<LocatedCharacter>();
        nuint start = partyLow > (nuint)ArenaRadius ? partyLow - (nuint)ArenaRadius : 0;
        nuint end = partyHigh + (nuint)ArenaRadius;
        if (end < partyHigh) end = nuint.MaxValue;   // guard the (impossible) wrap

        for (nuint addr = start; addr < end;)
        {
            int want = (int)Math.Min((nuint)SweepChunk, end - addr);
            // Read an extra record's worth so a record straddling a chunk edge is still seen.
            int read = mem.Read(addr, buffer, Math.Min(want + PorFormat.RecordSize, buffer.Length));

            for (int i = 0; i + PorFormat.RecordSize <= read; i++)
            {
                if (!CharacterSignature.Looks(buffer, i)) continue;
                var record = new CharacterRecord(buffer, i);
                if (!record.LooksLikeMonster || !record.LooksLikeLiveCombatant) continue;
                hits.Add(new LocatedCharacter(addr + (nuint)i, record));
            }

            // Unreadable pages are normal at the window's edges (the party can sit near the end of
            // a region); skip that chunk rather than abandoning the sweep. A short read only
            // advances past what was actually scanned.
            addr += read >= want ? (nuint)want
                  : read > PorFormat.RecordSize ? (nuint)(read - PorFormat.RecordSize + 1)
                  : (nuint)want;
        }

        hits.Sort((a, b) => a.Address.CompareTo(b.Address));

        // The sweep window can span a second mapping of the same heap, and unlike the full scan
        // nothing here would notice: the same fight would be listed twice. Dedupe on the same
        // rule the full scan uses.
        return Dedupe(hits);
    }

    /// <summary>
    /// Re-reads a single record into a caller-supplied scratch buffer (length >= record size),
    /// for the poll loop — reusing one buffer across all characters avoids per-tick allocation.
    /// Returns true if the full record was read.
    ///
    /// <para>A successful read is not on its own proof the record is still the one that was found
    /// there: the game frees and reuses heap slots across area and combat transitions, so an
    /// address remembered from the last scan can come back holding something else entirely. Pass
    /// <paramref name="expected"/> to have the bytes checked against the record's identity before
    /// they are accepted — the poll loop does, so a recycled slot drops the character rather than
    /// decoding a stranger's bytes under their name (and, worse, seeding freeze writes from them).</para>
    /// </summary>
    public static bool Reread(ProcessMemory mem, nuint address, byte[] buffer,
                              CharacterRecord? expected = null)
    {
        if (mem.Read(address, buffer, PorFormat.RecordSize) != PorFormat.RecordSize) return false;
        if (expected == null) return true;
        return CharacterSignature.Looks(buffer, 0) &&
               new CharacterRecord(buffer).IsSameCreatureAs(expected);
    }
}
