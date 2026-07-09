using System.Diagnostics;
using System.Text;

namespace AutoduelTrainer.Memory;

/// <summary>
/// Attaches to a running DOSBox process, locates the AUTODUEL player record in the
/// emulated guest RAM, and reads / writes the game state.
/// </summary>
public sealed class TrainerEngine : IDisposable
{
    private ProcessMemory? _mem;

    public bool Attached => _mem is { IsOpen: true } && PlayerAddress != IntPtr.Zero;
    public int ProcessId => _mem?.ProcessId ?? 0;
    public IntPtr PlayerAddress { get; private set; }
    public IntPtr CarAddress => PlayerAddress == IntPtr.Zero
        ? IntPtr.Zero : (IntPtr)(PlayerAddress.ToInt64() + GameData.CarOffset);

    /// <summary>True once the target process has exited (or can no longer be queried).</summary>
    public bool TargetProcessExited()
    {
        if (_mem is not { IsOpen: true }) return true;
        try { return Process.GetProcessById(_mem.ProcessId).HasExited; }
        catch (ArgumentException) { return true; }   // process no longer exists
        catch (InvalidOperationException) { return true; }
    }

    /// <summary>Attach to the given PID and scan for the player record.</summary>
    public void Attach(int processId)
    {
        Detach();
        _mem = new ProcessMemory(processId);
        PlayerAddress = LocatePlayer(_mem)
            ?? throw new InvalidOperationException(
                "Attached to the process, but could not find the AUTODUEL data in memory. " +
                "Make sure a driver is loaded in the game (past the title screen), then click Re-scan.");
    }

    /// <summary>Re-scan for the player record on the already-attached process.</summary>
    public void Rescan()
    {
        if (_mem is not { IsOpen: true })
            throw new InvalidOperationException("Not attached to a process.");
        PlayerAddress = LocatePlayer(_mem)
            ?? throw new InvalidOperationException("Could not find the AUTODUEL data in memory.");
    }

    public void Detach()
    {
        _mem?.Dispose();
        _mem = null;
        PlayerAddress = IntPtr.Zero;
    }

    // ---------------------------------------------------------------- scanning
    private static IntPtr? LocatePlayer(ProcessMemory mem)
    {
        var sig = GameData.Signature;
        // DOSBox keeps the emulated RAM in a large committed private block; scan
        // the biggest regions first, but fall back to smaller ones just in case.
        var regions = mem.EnumerateRegions(minSize: 1 << 20)
            .OrderByDescending(r => r.Size)
            .ToList();

        foreach (var region in regions)
        {
            const int chunk = 4 << 20;   // 4 MB
            const int overlap = 64;      // so a signature straddling a chunk edge is still found
            long size = region.Size;
            var buffer = new byte[chunk + overlap];

            for (long pos = 0; pos < size; pos += chunk)
            {
                int want = (int)Math.Min(buffer.Length, size - pos);
                var readBuf = want == buffer.Length ? buffer : new byte[want];
                var addr = (IntPtr)(region.Base.ToInt64() + pos);
                if (!mem.TryRead(addr, readBuf, out int got) || got < sig.Length)
                    continue;

                var span = new ReadOnlySpan<byte>(readBuf, 0, got);
                int searchFrom = 0;
                while (true)
                {
                    int idx = span.Slice(searchFrom).IndexOf(sig);
                    if (idx < 0) break;
                    int hit = searchFrom + idx;
                    long sigLinear = pos + hit;
                    long playerLinear = sigLinear - GameData.SignatureFileOffset + GameData.ComToPlayerDelta;
                    if (playerLinear >= 0 && playerLinear + GameData.PlayerRecordSize <= size)
                    {
                        var candidate = (IntPtr)(region.Base.ToInt64() + playerLinear);
                        if (Validate(mem, candidate))
                            return candidate;
                    }
                    searchFrom = hit + 1;
                }
            }
        }
        return null;
    }

    /// <summary>Sanity-check a candidate player record so we don't latch onto a false positive.</summary>
    private static bool Validate(ProcessMemory mem, IntPtr player)
    {
        try
        {
            var rec = mem.Read(player, GameData.PlayerRecordSize);
            // Name must start with a printable ASCII letter/space run.
            bool nameOk = false;
            for (int i = 0; i < GameData.NameLength; i++)
            {
                byte b = rec[GameData.OffName + i];
                if (b == 0) { nameOk = i > 0; break; }
                if (b < 0x20 || b > 0x7E) return false;
                if (i == GameData.NameLength - 1) nameOk = true;
            }
            if (!nameOk) return false;

            if (rec[GameData.OffDriving] > 99) return false;
            if (rec[GameData.OffMarksmanship] > 99) return false;
            if (rec[GameData.OffMechanic] > 99) return false;
            if (rec[GameData.OffHealth] > 20) return false;
            if (rec[GameData.OffPrestige] > 99) return false;
            if (rec[GameData.OffCity] > 15) return false;
            // money digits must be valid base-100
            if (rec[GameData.OffMoney] > 99 || rec[GameData.OffMoney + 1] > 99 ||
                rec[GameData.OffMoney + 2] > 99) return false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------------------------------------------- reading
    private ProcessMemory Mem => _mem ?? throw new InvalidOperationException("Not attached.");

    public GameSnapshot Read()
    {
        var p = Mem.Read(PlayerAddress, GameData.PlayerRecordSize);
        var snap = new GameSnapshot
        {
            DriverName = ReadName(p, GameData.OffName),
            Money = GameData.DecodeBase100(p.AsSpan(GameData.OffMoney, 3)),
            Prestige = p[GameData.OffPrestige],
            Health = p[GameData.OffHealth],
            BodyArmor = p[GameData.OffBodyArmor],
            Driving = p[GameData.OffDriving],
            Marksmanship = p[GameData.OffMarksmanship],
            Mechanic = p[GameData.OffMechanic],
            CityId = p[GameData.OffCity],
            Day = p[GameData.OffDay],
            DayOfWeek = p[GameData.OffDayOfWeek],
            TimeOfDay = p[GameData.OffTimeOfDay],
            HasCarWithPlayer = (p[GameData.OffFlags] & 0x20) != 0
        };

        var c = Mem.Read(CarAddress, GameData.CarRecordSize);
        string carName = ReadName(c, GameData.CarOffName);
        snap.HasCar = carName.Length > 0 && snap.HasCarWithPlayer;
        snap.CarName = carName;
        snap.MaxWeight = GameData.DecodeBase100(c.AsSpan(GameData.CarOffMaxWeight, 2));
        snap.WeightLeft = GameData.DecodeBase100(c.AsSpan(GameData.CarOffWeightLeft, 2));
        snap.MaxSpaces = c[GameData.CarOffMaxSpaces];
        snap.SpacesLeft = c[GameData.CarOffSpacesLeft];
        snap.Handling = c[GameData.CarOffHandling];
        snap.Acceleration = c[GameData.CarOffAccel];
        snap.Suspension = c[GameData.CarOffSuspension];
        snap.Chassis = c[GameData.CarOffChassis];
        snap.CarValue = GameData.DecodeBase100(c.AsSpan(GameData.CarOffValue, 2));
        snap.Battery = c[GameData.CarOffBattery];
        snap.BatteryMax = c[GameData.CarOffBatteryMax];

        for (int slot = 0; slot < GameData.ComponentCount; slot++)
        {
            int off = GameData.CarOffComponents + slot * GameData.ComponentSize;
            var info = new ComponentInfo
            {
                Slot = slot,
                Type = c[off + GameData.CompType],
                CurrentDp = c[off + GameData.CompCurDp],
                MaxDp = c[off + GameData.CompMaxDp],
                Location = c[off + GameData.CompLocation],
                Present = (c[off + GameData.CompFlags] & 0x80) != 0,
                Ammo = GameData.DecodeBase100(c.AsSpan(off + GameData.CompAmmoLo, 2))
            };
            if (slot >= GameData.FirstArmorSlot && slot < GameData.FirstWeaponSlot && info.Present)
                snap.Armor.Add(info);
            else if (slot >= GameData.FirstWeaponSlot &&
                     info.Type != GameData.EmptyWeaponType && info.Present)
                snap.Weapons.Add(info);
            else if (slot < GameData.FirstArmorSlot && info.Present)   // plant (0) + tires (1–4)
                snap.Drivetrain.Add(info);
        }
        return snap;
    }

    private static string ReadName(byte[] rec, int off)
    {
        int len = 0;
        while (len < GameData.NameLength && rec[off + len] != 0) len++;
        return Encoding.ASCII.GetString(rec, off, len).TrimEnd();
    }

    // ---------------------------------------------------------------- writing
    private IntPtr PlayerField(int off) => (IntPtr)(PlayerAddress.ToInt64() + off);
    private IntPtr CarField(int off) => (IntPtr)(CarAddress.ToInt64() + off);

    public void SetMoney(int value) =>
        Mem.Write(PlayerField(GameData.OffMoney),
            GameData.EncodeBase100(Math.Clamp(value, 0, GameData.MoneyMax), 3));

    public void SetPrestige(int value) =>
        Mem.WriteByte(PlayerField(GameData.OffPrestige), (byte)Math.Clamp(value, 0, 99));

    public void SetHealth(int value) =>
        Mem.WriteByte(PlayerField(GameData.OffHealth), (byte)Math.Clamp(value, 0, GameData.HealthMax));

    public void SetBodyArmor(int value) =>
        Mem.WriteByte(PlayerField(GameData.OffBodyArmor), (byte)Math.Clamp(value, 0, 99));

    public void SetDriving(int value) =>
        Mem.WriteByte(PlayerField(GameData.OffDriving), (byte)Math.Clamp(value, 0, 99));

    public void SetMarksmanship(int value) =>
        Mem.WriteByte(PlayerField(GameData.OffMarksmanship), (byte)Math.Clamp(value, 0, 99));

    public void SetMechanic(int value) =>
        Mem.WriteByte(PlayerField(GameData.OffMechanic), (byte)Math.Clamp(value, 0, 99));

    /// <summary>Set only the current-city byte (no route/car side effects).</summary>
    public void SetCity(int cityId) =>
        Mem.WriteByte(PlayerField(GameData.OffCity), (byte)Math.Clamp(cityId, 0, 15));

    /// <summary>
    /// Set the day counter (days since 1 Jan 2030) and keep the day-of-week byte
    /// consistent with it via the documented relationship day-of-week = (day + 5) % 7.
    /// </summary>
    public void SetDay(int value)
    {
        byte day = (byte)Math.Clamp(value, 0, GameData.DayMax);
        Mem.WriteByte(PlayerField(GameData.OffDay), day);
        Mem.WriteByte(PlayerField(GameData.OffDayOfWeek), (byte)GameData.WeekdayForDay(day));
    }

    /// <summary>
    /// Teleport the driver to a city: set the current city, clear any in-progress
    /// route (destination = current so the game does not immediately re-route), and
    /// bring the car along when it is with the player.
    /// </summary>
    public void Teleport(int cityId)
    {
        byte c = (byte)Math.Clamp(cityId, 0, 15);
        Mem.WriteByte(PlayerField(GameData.OffCity), c);
        Mem.WriteByte(PlayerField(GameData.OffDestCity), c);
        // The live game reads the current city from working copies, not the
        // persistent record — without these it won't actually relocate.
        Mem.WriteByte(PlayerField(GameData.OffWorkCityActive), c);
        Mem.WriteByte(PlayerField(GameData.OffWorkCityMirror), c);
        Mem.WriteByte(PlayerField(GameData.OffWorkCityBlock), c);

        var p = Mem.Read(PlayerAddress, GameData.PlayerRecordSize);
        bool carWithPlayer = (p[GameData.OffFlags] & 0x20) != 0;
        if (carWithPlayer)
            Mem.WriteByte(CarField(GameData.CarOffCity), c);
    }

    public void SetBattery(int value)
    {
        var c = Mem.Read(CarAddress, GameData.CarRecordSize);
        int max = c[GameData.CarOffBatteryMax];
        if (max == 0) max = 99; // fallback, mirroring ChargeBatteryFull
        byte v = (byte)Math.Clamp(value, 0, max);
        Mem.WriteByte(CarField(GameData.CarOffBattery), v);
    }

    public void SetBatteryMax(int value) =>
        Mem.WriteByte(CarField(GameData.CarOffBatteryMax), (byte)Math.Clamp(value, 0, 99));

    // --- editable car stats (base-100 pairs are clamped by EncodeBase100) ---
    public void SetMaxWeight(int value) =>
        Mem.Write(CarField(GameData.CarOffMaxWeight), GameData.EncodeBase100(value, 2));

    public void SetWeightLeft(int value) =>
        Mem.Write(CarField(GameData.CarOffWeightLeft), GameData.EncodeBase100(value, 2));

    public void SetMaxSpaces(int value) =>
        Mem.WriteByte(CarField(GameData.CarOffMaxSpaces), (byte)Math.Clamp(value, 0, 99));

    public void SetSpacesLeft(int value) =>
        Mem.WriteByte(CarField(GameData.CarOffSpacesLeft), (byte)Math.Clamp(value, 0, 99));

    public void SetHandling(int value) =>
        Mem.WriteByte(CarField(GameData.CarOffHandling), (byte)Math.Clamp(value, 0, 99));

    public void SetAcceleration(int value) =>
        Mem.WriteByte(CarField(GameData.CarOffAccel), (byte)Math.Clamp(value, 0, 99));

    public void SetSuspension(int value) =>
        Mem.WriteByte(CarField(GameData.CarOffSuspension), (byte)Math.Clamp(value, 0, 99));

    public void SetChassis(int value) =>
        Mem.WriteByte(CarField(GameData.CarOffChassis), (byte)Math.Clamp(value, 0, 99));

    public void SetCarValue(int value) =>
        Mem.Write(CarField(GameData.CarOffValue), GameData.EncodeBase100(value, 2));

    /// <summary>
    /// Set the time-of-day counter (player <see cref="GameData.OffTimeOfDay"/>). Freezing this
    /// to a daytime reading keeps shops open — the game stops reporting "closed for the evening".
    /// </summary>
    public void SetTimeOfDay(int value) =>
        Mem.WriteByte(PlayerField(GameData.OffTimeOfDay), (byte)Math.Clamp(value, 0, 255));

    /// <summary>Write only the current &amp; max DP of a component slot (armor facet, power
    /// plant, tire), preserving its type/location/ammo. Values are clamped to 0..99.</summary>
    public void SetComponentDp(int slot, int currentDp, int maxDp)
    {
        if (slot < 0 || slot >= GameData.ComponentCount) return;
        int off = GameData.CarOffComponents + slot * GameData.ComponentSize;
        Mem.WriteByte(CarField(off + GameData.CompCurDp), (byte)Math.Clamp(currentDp, 0, 99));
        Mem.WriteByte(CarField(off + GameData.CompMaxDp), (byte)Math.Clamp(maxDp, 0, 99));
    }

    /// <summary>
    /// Write the editable fields of one weapon/component slot: type, current &amp; max DP,
    /// facing/location and ammo. Self-powered weapons (laser, heavy rocket) keep their
    /// 0xDD ammo sentinel — no magazine count is written. All values are clamped to legal ranges.
    /// </summary>
    public void SetComponent(int slot, int type, int currentDp, int maxDp, int location, int ammo)
    {
        if (slot < 0 || slot >= GameData.ComponentCount) return;
        int off = GameData.CarOffComponents + slot * GameData.ComponentSize;
        byte t = (byte)Math.Clamp(type, 0, GameData.EmptyWeaponType);
        Mem.WriteByte(CarField(off + GameData.CompType), t);
        Mem.WriteByte(CarField(off + GameData.CompCurDp), (byte)Math.Clamp(currentDp, 0, 99));
        Mem.WriteByte(CarField(off + GameData.CompMaxDp), (byte)Math.Clamp(maxDp, 0, 99));
        Mem.WriteByte(CarField(off + GameData.CompLocation), (byte)Math.Clamp(location, 0, 9));
        if (t is not (GameData.LaserType or GameData.HeavyRocketType))
            Mem.Write(CarField(off + GameData.CompAmmoLo), GameData.EncodeBase100(ammo, 2));
    }

    public void ChargeBatteryFull()
    {
        var c = Mem.Read(CarAddress, GameData.CarRecordSize);
        byte max = c[GameData.CarOffBatteryMax];
        Mem.WriteByte(CarField(GameData.CarOffBattery), max == 0 ? (byte)99 : max);
    }

    /// <summary>Refill every mounted weapon's magazine (skips self-powered laser / heavy rocket).</summary>
    public int ReloadAllWeapons(int rounds = 99)
    {
        var c = Mem.Read(CarAddress, GameData.CarRecordSize);
        var ammo = GameData.EncodeBase100(rounds, 2);
        int changed = 0;
        for (int slot = GameData.FirstWeaponSlot; slot < GameData.ComponentCount; slot++)
        {
            int off = GameData.CarOffComponents + slot * GameData.ComponentSize;
            byte type = c[off + GameData.CompType];
            bool present = (c[off + GameData.CompFlags] & 0x80) != 0;
            if (!present || type == GameData.EmptyWeaponType) continue;
            if (type is GameData.LaserType or GameData.HeavyRocketType) continue;
            Mem.Write(CarField(off + GameData.CompAmmoLo), ammo);
            changed++;
        }
        return changed;
    }

    /// <summary>Restore every mounted component's current DP to its max DP.</summary>
    public int RepairAll()
    {
        var c = Mem.Read(CarAddress, GameData.CarRecordSize);
        int changed = 0;
        for (int slot = 0; slot < GameData.ComponentCount; slot++)
        {
            int off = GameData.CarOffComponents + slot * GameData.ComponentSize;
            bool present = (c[off + GameData.CompFlags] & 0x80) != 0;
            byte type = c[off + GameData.CompType];
            if (!present) continue;
            if (slot >= GameData.FirstWeaponSlot && type == GameData.EmptyWeaponType) continue;
            byte max = c[off + GameData.CompMaxDp];
            if (max == 0) continue;
            Mem.WriteByte(CarField(off + GameData.CompCurDp), max);
            changed++;
        }
        return changed;
    }

    public void Dispose() => Detach();
}
