using System.Diagnostics;
using System.Text;

namespace SyndicateTrainer;

/// <summary>
/// Locates the Syndicate game data inside a running DOSBox-X process and exposes
/// the reverse-engineered game values (currently: the player's Budget / money).
///
/// All offsets below are the game's own linear (LE) addresses, recovered by
/// reverse-engineering SYNDICAT\MAIN.EXE with Ghidra and cross-checking three
/// DOSBox-X process dumps. See the "Live-Memory Trainer" appendix in
/// .docs\Syndicate-Reverse-Engineering.md for the full derivation.
/// </summary>
public sealed class GameConnection : IDisposable
{
    // ---- Reverse-engineered game linear addresses (LE image, base 0x10000) ----

    /// Unique anchor string in the game data segment (VA 0x53204). The whole data
    /// segment is contiguous in guest RAM, so finding this fixes every other global.
    private static readonly byte[] Anchor = Encoding.ASCII.GetBytes("PERSUADERTRON\0");
    private const long AnchorVa = 0x53204;

    // Validation anchors at known relative offsets (guards against false positives).
    private static readonly byte[] ValGauss = Encoding.ASCII.GetBytes("GAUSS GUN\0");
    private const long ValGaussVa = 0x53240;         // +0x3C
    private static readonly byte[] ValShotgun = Encoding.ASCII.GetBytes("SHOTGUN\0");
    private const long ValShotgunVa = 0x5326C;       // +0x68

    /// Array of 8 syndicate records; player money is field 0 of the player's record.
    private const long SyndicateArrayVa = 0x5E49C;
    private const long SyndicateStride = 0x417;       // 1047 bytes per syndicate
    /// Current player syndicate index (DAT_00060b16). Normally 0 (EuroCorp).
    private const long PlayerIndexVa = 0x60B16;

    // Offsets *inside* the syndicate record (confirmed via world-map/clock code).
    private const long OffMoney = 0x00;   // int32  budget
    private const long OffDay = 0x08;     // int16  day (1..365)
    private const long OffYear = 0x0A;    // int16  year

    // ---- In-mission agent health ----
    // The live player-agent "ped" array is heap-resident. The DOS guest has no
    // internal ASLR, so the allocation is deterministic and its VA is stable per
    // build (verified live: writing here heals the agent and sticks).
    //   agent[k] current health = game_base + 0x6C123 + k*0x5C   (16-bit)
    private const long HealthBaseVa = 0x6C123;
    private const long HealthStride = 0x5C;
    public const int ActiveAgentCount = 4;
    public const int HealthFull = 4096;         // 0x1000, observed full value
    private const int HealthSaneMax = 0x2000;   // guard: values above this aren't agent health

    // ---- In-mission recoil (knock-back when an agent is shot) ----
    // Found by diffing an agent's 92-byte ped record between "walking normally" and
    // "recoiling from a shot" (dumps 105355 vs 105423). These fields sit at their resting
    // baseline during BOTH idle and walking, and only deviate while the agent is being
    // knocked back — so forcing them to baseline is a no-op except during a hit. Offsets
    // are relative to the ped base (= the health address; health is ped field +0).
    private const int RecoilImpulseOff = 0x03;   // int16: 0 normally -> ~0x033E during recoil
    private const int RecoilFlagOff = 0x0A;      // byte:  hit-reaction bit 0x08 set during recoil
    private const int RecoilFlagBit = 0x08;
    private const int RecoilAuxOff = 0x48;        // byte (+72): 0 normally -> nonzero during recoil

    // ---- In-mission ammo (weapon-object array) ----
    // The player's carried weapons live in a heap array of 0x24-byte records; each
    // record holds the weapon's ammo (16-bit) at +0xC. Verified live: agent 1's
    // pistol ammo tracked firing 12 -> 7 -> 2 across two differentials.
    // Located by signature scan (not a fixed address), since the array is heap-resident
    // and its size/position depend on the loadout.
    private const long WeaponScanStartVa = 0x50000;
    private const long WeaponScanEndVa = 0x120000;
    private const int WeaponStride = 0x24;
    private const int AmmoOffset = 0xC;
    private const int MinWeaponRun = 4;         // need >= this many consecutive records to trust a run
    public const int AmmoFreezeTarget = 500;    // topped-up "infinite" value (16-bit)

    // All access to _mem and the derived address state is serialised by _lock.
    // The (long-running) memory scan in Attach runs on a *local* ProcessMemory and
    // only publishes its result via a fast, locked swap, so the 250 ms UI timer can
    // never observe a half-attached or disposed handle.
    private readonly object _lock = new();
    private ProcessMemory? _mem;

    /// Process linear address that game VA 0 maps to (anchorAddr - AnchorVa).
    public long GameBase { get; private set; }
    public long MoneyAddress { get; private set; }
    public byte PlayerIndex { get; private set; }

    public bool IsAttached
    {
        get { lock (_lock) return _mem is { IsOpen: true } && MoneyAddress != 0; }
    }

    public Process? TargetProcess
    {
        get { lock (_lock) return _mem?.Process; }
    }

    /// <summary>Enumerate candidate DOSBox / DOSBox-X processes.</summary>
    public static IReadOnlyList<Process> FindDosBoxProcesses()
    {
        var list = new List<Process>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.ProcessName.Contains("dosbox", StringComparison.OrdinalIgnoreCase))
                    list.Add(p);
            }
            catch { /* access denied on some system processes */ }
        }
        return list;
    }

    /// <summary>
    /// Attach to a process and scan for the game data. Returns true on success.
    /// The scan runs on a local handle; shared state is only published atomically
    /// once the game is fully located.
    /// </summary>
    public bool Attach(Process process, Action<string>? log = null, CancellationToken ct = default)
    {
        // Drop any previous connection first (thread-safe).
        Detach();

        ProcessMemory mem;
        try
        {
            mem = new ProcessMemory(process);
        }
        catch (Exception ex)
        {
            log?.Invoke("Open failed: " + ex.Message);
            return false;
        }
        log?.Invoke($"Opened process {process.ProcessName} (PID {process.Id}). Scanning memory for the game...");

        long found = 0;
        mem.ScanSignature(Anchor, hit =>
        {
            // Validate the two secondary anchors at their expected relative offsets.
            if (ReadMatches(mem, hit + (ValGaussVa - AnchorVa), ValGauss) &&
                ReadMatches(mem, hit + (ValShotgunVa - AnchorVa), ValShotgun))
            {
                found = hit;
                return false; // stop scanning
            }
            return true;
        }, privateOnly: true, ct: ct);

        if (found == 0 || ct.IsCancellationRequested)
        {
            log?.Invoke("Could not find Syndicate in that process. Make sure the game is actually running (past the intro).");
            mem.Dispose();
            return false;
        }

        long gameBase = found - AnchorVa;

        // Player index (defensive: clamp to 0..7).
        byte playerIndex = 0;
        if (mem.TryReadByte(gameBase + PlayerIndexVa, out byte idx) && idx < 8)
            playerIndex = idx;

        long moneyAddress = gameBase + SyndicateArrayVa + playerIndex * SyndicateStride + OffMoney;

        // Publish the fully-formed connection atomically.
        lock (_lock)
        {
            _mem = mem;
            GameBase = gameBase;
            PlayerIndex = playerIndex;
            MoneyAddress = moneyAddress;
        }

        log?.Invoke($"Game found. base=0x{gameBase:X}  player index={playerIndex}  money@0x{moneyAddress:X}");
        return true;
    }

    // Used only during the scan, on the not-yet-published local handle.
    private static bool ReadMatches(ProcessMemory mem, long address, byte[] expected)
    {
        var buf = new byte[expected.Length];
        if (!mem.TryReadBytes(address, buf, expected.Length)) return false;
        for (int i = 0; i < expected.Length; i++)
            if (buf[i] != expected[i]) return false;
        return true;
    }

    // ---- Live value access -------------------------------------------------

    public bool TryGetMoney(out int money)
    {
        lock (_lock)
        {
            money = 0;
            return _mem is not null && MoneyAddress != 0 && _mem.TryReadInt32(MoneyAddress, out money);
        }
    }

    public bool SetMoney(int value)
    {
        lock (_lock)
            return _mem is not null && MoneyAddress != 0 && _mem.WriteInt32(MoneyAddress, value);
    }

    /// <summary>Read <paramref name="count"/> bytes at a game linear (LE) address.</summary>
    public bool TryReadGame(long gameVa, byte[] buffer, int count)
    {
        lock (_lock)
            return _mem is not null && GameBase != 0 && _mem.TryReadBytes(GameBase + gameVa, buffer, count);
    }

    /// <summary>Write bytes at a game linear (LE) address.</summary>
    public bool WriteGame(long gameVa, byte[] data)
    {
        lock (_lock)
            return _mem is not null && GameBase != 0 && _mem.WriteBytes(GameBase + gameVa, data);
    }

    public bool TryGetDate(out int day, out int year)
    {
        lock (_lock)
        {
            day = 0; year = 0;
            if (_mem is null || MoneyAddress == 0) return false;
            long recBase = MoneyAddress - OffMoney;
            bool okDay = _mem.TryReadUInt16(recBase + OffDay, out ushort d);
            bool okYear = _mem.TryReadUInt16(recBase + OffYear, out ushort y);
            day = d; year = y;
            return okDay && okYear;
        }
    }

    // ---- In-mission agent health -------------------------------------------

    private long HealthAddr(int idx) => GameBase + HealthBaseVa + idx * HealthStride;

    /// <summary>Read agent <paramref name="idx"/> (0..3) current health.</summary>
    public bool TryGetAgentHealth(int idx, out int hp)
    {
        hp = 0;
        if (idx < 0 || idx >= ActiveAgentCount) return false;
        lock (_lock)
        {
            if (_mem is null || GameBase == 0) return false;
            var b = new byte[2];
            if (!_mem.TryReadBytes(HealthAddr(idx), b, 2)) return false;
            hp = b[0] | (b[1] << 8);
            return true;
        }
    }

    /// <summary>
    /// A slot is healable only if it reads a sane, alive value. This self-validates
    /// the heap address: if the layout ever differs, the read will not look like
    /// health and we simply refuse to write (never corrupt memory).
    /// </summary>
    public bool IsAgentHealable(int idx)
        => TryGetAgentHealth(idx, out int hp) && hp > 0 && hp <= HealthSaneMax;

    /// <summary>
    /// Read-validate-write, atomically under the lock: only writes to a slot that
    /// currently reads a sane, alive value, so a dead agent is never revived and a
    /// shifted heap layout can never be corrupted.
    /// </summary>
    public bool SetAgentHealth(int idx, int value)
    {
        if (idx < 0 || idx >= ActiveAgentCount) return false;
        value = Math.Clamp(value, 1, HealthSaneMax);
        lock (_lock)
        {
            if (_mem is null || GameBase == 0) return false;
            long addr = HealthAddr(idx);
            var b = new byte[2];
            if (!_mem.TryReadBytes(addr, b, 2)) return false;
            int hp = b[0] | (b[1] << 8);
            if (hp <= 0 || hp > HealthSaneMax) return false;     // only alive, sane slots
            return _mem.WriteBytes(addr, BitConverter.GetBytes((ushort)value));
        }
    }

    /// <summary>Heal every currently-alive active agent to <paramref name="value"/>.
    /// Returns the number of agents affected.</summary>
    public int HealAliveAgents(int value)
    {
        int n = 0;
        for (int i = 0; i < ActiveAgentCount; i++)
            if (SetAgentHealth(i, value)) n++;
        return n;
    }

    /// <summary>True while a mission is active (at least one alive agent present).</summary>
    public bool IsMissionActive()
    {
        for (int i = 0; i < ActiveAgentCount; i++)
            if (IsAgentHealable(i)) return true;
        return false;
    }

    // ---- In-mission recoil suppression ---------------------------------------

    /// <summary>
    /// Cancel the knock-back an agent receives when shot by resetting the walk-invariant
    /// "hit-reaction" fields of its ped to their resting baseline. Self-validating like the
    /// health writes: only touches a slot that currently reads a sane, alive agent, and only
    /// writes a field that is genuinely off-baseline — so during normal idle/walking it issues
    /// no writes at all. Returns true if it cancelled an active recoil on this agent this call.
    /// </summary>
    public bool SuppressAgentRecoil(int idx)
    {
        if (idx < 0 || idx >= ActiveAgentCount) return false;
        lock (_lock)
        {
            if (_mem is null || GameBase == 0) return false;
            long addr = HealthAddr(idx);                 // ped base (health at +0)
            const int stride = (int)HealthStride;
            var rec = new byte[stride];
            if (!_mem.TryReadBytes(addr, rec, stride)) return false;
            int hp = rec[0] | (rec[1] << 8);
            if (hp <= 0 || hp > HealthSaneMax) return false;   // only live, sane peds

            bool suppressed = false;
            // Knock-back impulse/timer (int16) -> 0.
            if (rec[RecoilImpulseOff] != 0 || rec[RecoilImpulseOff + 1] != 0)
                suppressed |= _mem.WriteBytes(addr + RecoilImpulseOff, new byte[] { 0, 0 });
            // Hit-reaction flag bit -> clear (leave the byte's other bits untouched).
            if ((rec[RecoilFlagOff] & RecoilFlagBit) != 0)
                suppressed |= _mem.WriteBytes(addr + RecoilFlagOff,
                    new[] { (byte)(rec[RecoilFlagOff] & ~RecoilFlagBit) });
            // Auxiliary reaction byte -> 0 (least-certain field; harmless to clear since it
            // is baseline-zero whenever the agent is not being hit).
            if (rec[RecoilAuxOff] != 0)
                suppressed |= _mem.WriteBytes(addr + RecoilAuxOff, new byte[] { 0 });
            return suppressed;
        }
    }

    /// <summary>Suppress recoil on every currently-alive active agent.
    /// Returns the number of agents whose recoil was cancelled this call.</summary>
    public int SuppressRecoilAliveAgents()
    {
        int n = 0;
        for (int i = 0; i < ActiveAgentCount; i++)
            if (SuppressAgentRecoil(i)) n++;
        return n;
    }

    // ---- In-mission ammo -----------------------------------------------------

    private long _weaponBaseVa;   // 0 = not located
    private int _weaponCount;
    private int _relocateCountdown;                // throttles full re-scans while unlocated
    private const int RelocateThrottleTicks = 4;   // ~1 s between scans at the 250 ms poll

    /// <summary>Number of weapon records currently being tracked (0 if not located).</summary>
    public int WeaponCount { get { lock (_lock) return _weaponBaseVa == 0 ? 0 : _weaponCount; } }

    // A record "looks like" a weapon of ANY type (pistol, flamer, uzi, ...). The
    // record starts with a per-weapon dynamic word (+0/+1) then a weapon-type byte (+2)
    // and 5 zero bytes (+3..+7); position is at +8/+0xA and ammo (16-bit) at +0xC with
    // its high half (+0xE/+0xF) zero. NOTE: an earlier version keyed on +0==00/+1==01,
    // which only matched pistols and wrongly excluded flamers.
    private static bool LooksLikeWeapon(byte[] buf, int o)
    {
        if (o + WeaponStride > buf.Length) return false;
        if (buf[o + 3] != 0 || buf[o + 4] != 0 || buf[o + 5] != 0 || buf[o + 6] != 0 || buf[o + 7] != 0)
            return false;                                                   // +3..+7 zero (all weapon types)
        if (buf[o + AmmoOffset + 2] != 0 || buf[o + AmmoOffset + 3] != 0)
            return false;                                                   // ammo high bytes (+0xE/+0xF) zero
        int type = buf[o + 2];
        if (type == 0 || type > 0x40) return false;                         // weapon type present
        int ammo = buf[o + AmmoOffset] | (buf[o + AmmoOffset + 1] << 8);
        if (ammo < 0 || ammo > 0x4000) return false;
        int px = buf[o + 8] | (buf[o + 9] << 8);
        int py = buf[o + 0xA] | (buf[o + 0xB] << 8);
        return px >= 1 && px <= 0x4000 && py >= 1 && py <= 0x4000;          // has a world position
    }

    /// <summary>
    /// Signature-scan the player's heap region for the longest run of weapon records
    /// and cache it. Returns true if a plausible array (>= MinWeaponRun) was found.
    /// </summary>
    public bool LocateWeaponArray()
    {
        int size = (int)(WeaponScanEndVa - WeaponScanStartVa);
        var buf = new byte[size];
        for (int off = 0; off < size; off += 0x10000)
        {
            int n = Math.Min(0x10000, size - off);
            var t = new byte[n];
            TryReadGame(WeaponScanStartVa + off, t, n); // partial reads tolerated (zero-filled)
            Array.Copy(t, 0, buf, off, n);
        }

        int bestStart = -1, bestLen = 0;
        for (int o = 0; o + WeaponStride <= size; o++)
        {
            if (!LooksLikeWeapon(buf, o)) continue;
            int len = 0, p = o;
            while (p + WeaponStride <= size && LooksLikeWeapon(buf, p)) { len++; p += WeaponStride; }
            if (len > bestLen) { bestLen = len; bestStart = o; }
            // Only skip past a *confirmed* run — records are stride-aligned, so no longer
            // run can begin inside one. For an isolated false-positive match, fall through
            // to o++ so we never jump over the real array's start sitting a few bytes ahead.
            if (len >= MinWeaponRun) o = p;
        }

        lock (_lock)
        {
            if (bestLen >= MinWeaponRun)
            {
                _weaponBaseVa = WeaponScanStartVa + bestStart;
                _weaponCount = bestLen;
                return true;
            }
            _weaponBaseVa = 0; _weaponCount = 0;
            return false;
        }
    }

    /// <summary>
    /// Top up every valid player weapon's ammo to <paramref name="target"/> (only when
    /// below it, so nothing is ever reduced). Re-locates the array if the cache is stale.
    /// Returns the number of weapons written this call.
    /// </summary>
    public int FreezeAmmo(int target)
    {
        long baseVa; int count;
        lock (_lock) { baseVa = _weaponBaseVa; count = _weaponCount; }
        if (baseVa == 0)
        {
            // The array isn't located yet (e.g. no mission in progress). A full-region
            // scan is expensive, so don't repeat it on every poll — attempt it at most
            // once every few ticks rather than 4x/sec while idling on a menu/world map.
            if (_relocateCountdown > 0) { _relocateCountdown--; return 0; }
            _relocateCountdown = RelocateThrottleTicks;
            if (!LocateWeaponArray()) return 0;
            lock (_lock) { baseVa = _weaponBaseVa; count = _weaponCount; }
        }

        int frozen = 0;
        bool anyValid = false;
        var rec = new byte[WeaponStride];
        var val = new[] { (byte)(target & 0xff), (byte)((target >> 8) & 0xff) };
        for (int k = 0; k < count; k++)
        {
            long recVa = baseVa + (long)k * WeaponStride;
            if (!TryReadGame(recVa, rec, WeaponStride)) continue;
            if (!LooksLikeWeapon(rec, 0)) continue;      // per-record self-validation
            anyValid = true;
            int ammo = rec[AmmoOffset] | (rec[AmmoOffset + 1] << 8);
            if (ammo >= 1 && ammo < target && WriteGame(recVa + AmmoOffset, val))
                frozen++;
        }

        // Cache went stale (e.g. new mission relocated the array): force a re-scan.
        // Clear the throttle too so the next poll re-locates immediately — a relocation
        // is a real event worth reacting to promptly, unlike idling with no mission.
        if (!anyValid) { lock (_lock) { _weaponBaseVa = 0; _weaponCount = 0; } _relocateCountdown = 0; }
        return frozen;
    }

    /// <summary>
    /// Re-read the money value; if the read fails the process is gone/relocated.
    /// </summary>
    public bool VerifyStillValid() => TryGetMoney(out _);

    public void Detach()
    {
        ProcessMemory? old;
        lock (_lock)
        {
            old = _mem;
            _mem = null;
            GameBase = 0;
            MoneyAddress = 0;
            PlayerIndex = 0;
            _weaponBaseVa = 0;
            _weaponCount = 0;
        }
        // Dispose outside the lock, after no reader can reach the old handle.
        old?.Dispose();
    }

    public void Dispose() => Detach();
}
