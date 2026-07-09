using System.Diagnostics;
using ShogunTrainer.Memory;

namespace ShogunTrainer.Game;

/// <summary>
/// Live snapshot of the player entity, for the UI.
/// </summary>
public sealed record PlayerState(int Cash, int Hp, int Followers, bool TableValid);

/// <summary>
/// UI-agnostic trainer: finds the DOSBox-X process, locates the live entity table
/// by signature, and applies cheats. All memory writes go through here.
/// </summary>
public sealed class TrainerEngine : IDisposable
{
    private ProcessMemory? _mem;
    private ulong _tableAddress;   // absolute address of entity 0 in the target

    public bool Attached => _mem is not null && _tableAddress != 0;
    public ulong TableAddress => _tableAddress;
    public int ProcessId => _mem?.ProcessId ?? 0;

    // Freeze flags + frozen values.
    public bool FreezeCash;
    public int FrozenCash = 255;
    public bool FreezeHp;
    public int FrozenHp = 254;
    public bool FreezeTimer;
    public bool ForceFollowing;             // keep the following tally above the contest threshold

    /// <summary>Find and attach to DOSBox-X, then locate the entity table.</summary>
    public string Attach()
    {
        Detach();

        Process? proc = FindDosbox();
        if (proc is null)
            return "DOSBox-X process not found. Start the game first.";

        try
        {
            _mem = ProcessMemory.Open(proc);
        }
        catch (Exception ex)
        {
            return $"Failed to open process: {ex.Message}";
        }

        ulong? table = ScanForTable(_mem);
        if (table is null)
        {
            string name = _mem.ProcessName;
            Detach();
            return $"Attached to {name} (pid {proc.Id}) but the Shogun entity table was not found. " +
                   "Make sure you are in-game (past the title screen).";
        }

        _tableAddress = table.Value;
        return $"Attached to {_mem.ProcessName} (pid {_mem.ProcessId}). " +
               $"Entity table @ 0x{_tableAddress:X}.";
    }

    public void Detach()
    {
        _mem?.Dispose();
        _mem = null;
        _tableAddress = 0;
    }

    private static Process? FindDosbox()
    {
        foreach (var p in Process.GetProcesses())
        {
            if (p.ProcessName.Contains("dosbox", StringComparison.OrdinalIgnoreCase))
                return p;
        }
        return null;
    }

    /// <summary>
    /// Scan the target's private, writable, committed regions for the entity table
    /// signature. Returns the absolute address of entity 0 if found.
    /// </summary>
    private static ulong? ScanForTable(ProcessMemory mem)
    {
        ulong bestAddr = 0;
        int bestScore = -1;

        // Need room for the entity table + the world-object table that validates it.
        int minSpan = ShogunGame.WorldObjOffset + ShogunGame.WorldObjCount * ShogunGame.WorldObjStride;

        foreach (var region in mem.EnumerateRegions())
        {
            // The live table lives in DOSBox-X's private, writable guest RAM.
            if (!region.IsPrivate || !region.IsWritable)
                continue;
            if (region.Size < (ulong)minSpan)
                continue;
            // Guest RAM is tens of MB; skip absurdly large regions to stay quick.
            if (region.Size > 512UL * 1024 * 1024)
                continue;

            byte[] buf = mem.ReadPartial(region.BaseAddress, (int)Math.Min(region.Size, int.MaxValue));
            int limit = buf.Length - minSpan;

            // The table base is paragraph (16-byte) aligned inside guest RAM.
            for (int off = 0; off <= limit; off += 16)
            {
                if (!ShogunGame.LooksLikeTable(buf, off))
                    continue;
                // Reject DOSBox-X savestate/rewind copies: only the live table has the
                // real, sorted world-object table following it.
                if (!ShogunGame.HasValidWorldObjectTable(buf, off))
                    continue;
                int score = ShogunGame.Confidence(buf, off);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestAddr = region.BaseAddress + (ulong)off;
                }
            }
        }

        return bestScore >= 0 ? bestAddr : null;
    }

    private ulong EntityAddr(int index) => _tableAddress + (ulong)(index * ShogunGame.EntityStride);

    /// <summary>Absolute address of a DS:0xD0xx global (globals segment = table - 0x3900).</summary>
    private ulong GlobalAddr(int dsOffset) =>
        (ulong)((long)_tableAddress + ShogunGame.GlobalsSegDelta + dsOffset);

    private ulong FollowerTallyAddr(int index) =>
        _tableAddress + (ulong)(ShogunGame.FollowerTallyOffset + index);

    /// <summary>Confirm the globals segment maps where we expect (player-index global == 0).</summary>
    public bool GlobalsValid()
    {
        if (_mem is null || _tableAddress == 0) return false;
        var b = _mem.Read(GlobalAddr(ShogunGame.GbPlayerIndex), 1);
        return b is not null && b[0] == ShogunGame.PlayerIndex;
    }

    public int ReadArea() => _mem is null ? -1 : _mem.ReadByte(GlobalAddr(ShogunGame.GbArea));

    public int ReadTimer()
    {
        if (_mem is null) return -1;
        var w = _mem.Read(GlobalAddr(ShogunGame.GbTimer), 2);
        return w is null ? -1 : w[0] | (w[1] << 8);
    }

    public int ReadFollowingTally() =>
        _mem is null ? -1 : _mem.ReadByte(FollowerTallyAddr(ShogunGame.PlayerIndex));

    /// <summary>Turn-by-turn walking directions from the current area to <paramref name="target"/>,
    /// routed around impassable screens (BFS). Falls back to a straight-line hint if unreachable.</summary>
    public string RouteTo(int target)
    {
        if (!GlobalsValid()) return "Globals not mapped.";
        if (target < 0 || target > 254) return "Area must be 0–254.";
        int cur = ReadArea();
        if (cur < 0) return "Can't read current area.";
        if (cur == target) return $"You're already in area {target}.";

        var path = ShogunGame.FindPath(cur, target);
        if (path is not null)
            return $"Area {cur} → {target} ({path.Length - 1} screens): {ShogunGame.Directions(path)}";

        // No walkable path (e.g. an isolated screen like Zen Masters Palace): give the crow-flies hint.
        int W = ShogunGame.MapWidth;
        int dCol = (target % W) - (cur % W), dRow = (target / W) - (cur / W);
        var parts = new List<string>();
        if (dCol != 0) parts.Add($"{(dCol > 0 ? "East" : "West")} {Math.Abs(dCol)}");
        if (dRow != 0) parts.Add($"{(dRow > 0 ? "South" : "North")} {Math.Abs(dRow)}");
        return $"No walkable route to {target} (screen may be isolated). Straight line: {string.Join(", ", parts)}.";
    }

    /// <summary>
    /// Experimental: warp the player to <paramref name="target"/> by writing the current-area
    /// global (0xD0C2) and the player's own location byte, and centring the player on the screen.
    /// The game redraws the destination on its next frame. Save first — an unreachable screen
    /// could leave you stuck.
    /// </summary>
    public bool Teleport(int target)
    {
        if (_mem is null || _tableAddress == 0 || !GlobalsValid()) return false;
        if (target < 0 || target > 254) return false;
        bool ok = _mem.WriteByte(GlobalAddr(ShogunGame.GbArea), (byte)target);
        ok &= _mem.WriteByte(EntityAddr(ShogunGame.PlayerIndex) + ShogunGame.OffLocation, (byte)target);
        // Centre the player so we don't spawn against an edge.
        _mem.WriteByte(EntityAddr(ShogunGame.PlayerIndex) + ShogunGame.OffX, 16);
        _mem.WriteByte(EntityAddr(ShogunGame.PlayerIndex) + ShogunGame.OffY, 8);
        return ok;
    }

    /// <summary>Re-validate the cached table address; rescan (re-attach) if it moved.</summary>
    public bool EnsureValid()
    {
        if (_mem is null) return false;
        if (_tableAddress != 0)
        {
            int span = ShogunGame.WorldObjOffset + ShogunGame.WorldObjCount * ShogunGame.WorldObjStride;
            byte[]? head = _mem.Read(_tableAddress, span);
            if (head is not null
                && ShogunGame.LooksLikeTable(head, 0)
                && ShogunGame.HasValidWorldObjectTable(head, 0))
                return true;
        }
        // Address went stale (game reloaded / guest RAM moved). Try to relocate.
        ulong? table = ScanForTable(_mem);
        if (table is null) { _tableAddress = 0; return false; }
        _tableAddress = table.Value;
        return true;
    }

    public PlayerState ReadPlayer()
    {
        if (_mem is null || _tableAddress == 0)
            return new PlayerState(0, 0, 0, false);

        byte[]? table = _mem.Read(_tableAddress, ShogunGame.TableSize);
        if (table is null || !ShogunGame.LooksLikeTable(table, 0))
            return new PlayerState(0, 0, 0, false);

        int cash = table[ShogunGame.PlayerIndex * ShogunGame.EntityStride + ShogunGame.OffCash];
        int hp = table[ShogunGame.PlayerIndex * ShogunGame.EntityStride + ShogunGame.OffHp];

        int followers = 0;
        for (int n = 0; n < ShogunGame.EntityCount; n++)
        {
            if (n == ShogunGame.PlayerIndex) continue;
            int rec = n * ShogunGame.EntityStride;
            if (table[rec + ShogunGame.OffOwner] == ShogunGame.PlayerIndex && table[rec + ShogunGame.OffHp] > 0)
                followers++;
        }
        return new PlayerState(cash, hp, followers, true);
    }

    // ---- Cheat operations ----

    public bool SetCash(int value)
    {
        if (_mem is null || _tableAddress == 0) return false;
        return _mem.WriteByte(EntityAddr(ShogunGame.PlayerIndex) + ShogunGame.OffCash, (byte)Clamp(value));
    }

    public bool SetHp(int value)
    {
        if (_mem is null || _tableAddress == 0) return false;
        return _mem.WriteByte(EntityAddr(ShogunGame.PlayerIndex) + ShogunGame.OffHp, (byte)Clamp(value));
    }

    /// <summary>Recruit up to <paramref name="max"/> living NPCs to the player (owner = 0).</summary>
    public int Recruit(int max)
    {
        if (_mem is null || _tableAddress == 0) return 0;
        byte[]? table = _mem.Read(_tableAddress, ShogunGame.TableSize);
        if (table is null) return 0;

        int done = 0;
        for (int n = 1; n < ShogunGame.EntityCount && done < max; n++)
        {
            int rec = n * ShogunGame.EntityStride;
            if (table[rec + ShogunGame.OffHp] == 0) continue;              // skip the dead
            if (table[rec + ShogunGame.OffOwner] == ShogunGame.PlayerIndex) continue; // already ours
            ulong a = EntityAddr(n);
            _mem.WriteByte(a + ShogunGame.OffOwner, ShogunGame.PlayerIndex);
            _mem.WriteByte(a + ShogunGame.OffMaster2, ShogunGame.PlayerIndex);
            done++;
        }
        return done;
    }

    /// <summary>
    /// Make every living NPC maximally well-disposed toward the player — the state you
    /// would reach by gifting each of them repeatedly. Sets the gift/befriend friendliness
    /// nibble (low nibble of +0x09) to max and the attention/disposition byte (+0x04) to
    /// its top value, while preserving each byte's high bits/flag. Does not recruit them.
    /// Returns how many NPCs were affected.
    /// </summary>
    public int MakeFriendly()
    {
        if (_mem is null || _tableAddress == 0) return 0;
        byte[]? table = _mem.Read(_tableAddress, ShogunGame.TableSize);
        if (table is null) return 0;

        int done = 0;
        for (int n = 1; n < ShogunGame.EntityCount; n++)   // skip entity 0 (the player)
        {
            int rec = n * ShogunGame.EntityStride;
            if (table[rec + ShogunGame.OffHp] == 0) continue;   // skip the dead

            ulong a = EntityAddr(n);
            byte disp = table[rec + ShogunGame.OffDisposition];
            byte friend = table[rec + ShogunGame.OffTrait2];
            // +0x04: max attention (0x7F) but keep the 0x80 flag bit as-is.
            _mem.WriteByte(a + ShogunGame.OffDisposition, (byte)((disp & 0x80) | 0x7F));
            // +0x09: max friendliness low nibble, keep the packed high-nibble trait.
            _mem.WriteByte(a + ShogunGame.OffTrait2, (byte)((friend & 0xF0) | 0x0F));
            done++;
        }
        return done;
    }

    /// <summary>Maximise the six packed personality nibbles (0xFF each) for an entity.</summary>
    public bool MaxStats(int index)
    {
        if (_mem is null || _tableAddress == 0) return false;
        ulong a = EntityAddr(index);
        bool ok = _mem.WriteByte(a + ShogunGame.OffTrait1, 0xFF);
        ok &= _mem.WriteByte(a + ShogunGame.OffTrait2, 0xFF);
        ok &= _mem.WriteByte(a + ShogunGame.OffTrait3, 0xFF);
        return ok;
    }

    /// <summary>
    /// Win helper: place the three relics (caste 0xC0) at the open contest palace in the
    /// world-object table, so the become-Shogun check counts them. Requires the contest to
    /// already be open (0xD0D0 set — turn on Force following and pick up an object first).
    /// Keeps the table sorted by location, which the victory scan relies on.
    /// </summary>
    public string PlaceRelicsAtContest()
    {
        if (_mem is null || _tableAddress == 0) return "Not attached.";
        if (!GlobalsValid()) return "Globals not mapped — can't read the contest location.";

        int contest = _mem.ReadByte(GlobalAddr(ShogunGame.GbContestLoc));
        if (contest == 0)
            return "The contest isn't open yet. Turn on \"Force following\", pick up any object to open it, then try again.";

        ulong wtAddr = _tableAddress + (ulong)ShogunGame.WorldObjOffset;
        int bytes = ShogunGame.WorldObjCount * ShogunGame.WorldObjStride;
        byte[]? t = _mem.Read(wtAddr, bytes);
        if (t is null) return "Couldn't read the world-object table.";

        // Repurpose 3 slots (the real Buddha/Scroll/Mirror if present, else spares) to be
        // caste-0xC0 relics sitting at the contest location.
        var used = new HashSet<int>();
        foreach (int id in new[] { 13, 14, 15 })       // Buddha, Scroll, Mirror
        {
            int slot = PickWorldSlot(t, id, used);
            if (slot < 0) return "No world-object slot available to place a relic.";
            used.Add(slot);
            t[slot * 4 + 0] = (byte)contest;
            t[slot * 4 + 1] = (byte)(ShogunGame.CasteRelic | id);
            t[slot * 4 + 2] = 0;
            t[slot * 4 + 3] = 0;
        }
        SortWorldTable(t);

        return _mem.Write(wtAddr, t)
            ? $"Placed Buddha, Scroll & Mirror at the contest palace (area {contest}). Pick up any object there to become Shōgun."
            : "Write to the world-object table failed.";
    }

    /// <summary>Choose a table slot for a relic: the matching object if present, else a
    /// low-value spare (food), else any unused slot.</summary>
    private static int PickWorldSlot(byte[] t, int id, HashSet<int> used)
    {
        int foodSpare = -1, anySpare = -1;
        for (int i = 0; i < ShogunGame.WorldObjCount; i++)
        {
            if (used.Contains(i)) continue;
            byte desc = t[i * 4 + 1];
            int oid = desc & 0x0F, caste = desc & 0xE0;
            if (oid == id) return i;                       // the real relic — move it
            if (foodSpare < 0 && caste == 0x20) foodSpare = i;   // sacrifice a FISH/CHERRIES/SAKI
            if (anySpare < 0) anySpare = i;
        }
        return foodSpare >= 0 ? foodSpare : anySpare;
    }

    /// <summary>Stable-sort the 64 world-object records ascending by location byte.</summary>
    private static void SortWorldTable(byte[] t)
    {
        var recs = new List<byte[]>(ShogunGame.WorldObjCount);
        for (int i = 0; i < ShogunGame.WorldObjCount; i++)
            recs.Add(new[] { t[i * 4], t[i * 4 + 1], t[i * 4 + 2], t[i * 4 + 3] });
        var sorted = recs.OrderBy(r => r[0]).ToList();     // OrderBy is stable
        for (int i = 0; i < ShogunGame.WorldObjCount; i++)
            Array.Copy(sorted[i], 0, t, i * 4, 4);
    }

    /// <summary>Put the three victory relics into the player's inventory slots.</summary>
    public bool GiveRelics()
    {
        if (_mem is null || _tableAddress == 0) return false;
        ulong a = EntityAddr(ShogunGame.PlayerIndex);
        bool ok = _mem.WriteByte(a + ShogunGame.OffInv0 + 0, ShogunGame.RelicBuddha);
        ok &= _mem.WriteByte(a + ShogunGame.OffInv0 + 1, ShogunGame.RelicScroll);
        ok &= _mem.WriteByte(a + ShogunGame.OffInv0 + 2, ShogunGame.RelicMirror);
        return ok;
    }

    private int _frozenTimer = -1;

    public bool SetTimer(int value)
    {
        if (_mem is null || _tableAddress == 0) return false;
        return _mem.Write(GlobalAddr(ShogunGame.GbTimer),
            new[] { (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF) });
    }

    /// <summary>
    /// Push the player's following tally over the contest threshold. The game re-derives
    /// this tally, so ForceFollowing re-applies it every tick; once the player next picks
    /// up an object the become-Shogun contest opens.
    /// </summary>
    public bool SetFollowing(int value)
    {
        if (_mem is null || _tableAddress == 0) return false;
        return _mem.WriteByte(FollowerTallyAddr(ShogunGame.PlayerIndex), (byte)Clamp(value));
    }

    /// <summary>Called on a UI timer: enforce freezes. Returns a fresh player snapshot.</summary>
    public PlayerState Tick()
    {
        if (!EnsureValid())
            return new PlayerState(0, 0, 0, false);

        if (FreezeCash) SetCash(FrozenCash);
        if (FreezeHp) SetHp(FrozenHp);

        if (FreezeTimer && GlobalsValid())
        {
            if (_frozenTimer < 0) _frozenTimer = ReadTimer();
            if (_frozenTimer > 0) SetTimer(_frozenTimer);
        }
        else _frozenTimer = -1;

        if (ForceFollowing && GlobalsValid())
            SetFollowing(ShogunGame.FollowingThreshold + 6);   // comfortably over 19

        return ReadPlayer();
    }

    private static int Clamp(int v) => v < 0 ? 0 : v > 255 ? 255 : v;

    public void Dispose() => Detach();
}
