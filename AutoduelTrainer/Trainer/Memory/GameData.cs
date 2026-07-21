namespace AutoduelTrainer.Memory;

/// <summary>
/// Constants recovered by reverse-engineering AUTODUEL (see .docs/reverse-engineering.md).
/// All offsets are relative to the start of the player / car record.
/// </summary>
public static class GameData
{
    // --- guest-RAM anatomy ---------------------------------------------------
    // The player record sits at COM image base + 0x148BD.
    // The 24 bytes at COM file offset 0x8000 make a reliable, unique signature.
    public static readonly byte[] Signature =
    {
        0x89, 0x7D, 0x21, 0x8B, 0x46, 0xFE, 0x3B, 0x06,
        0xC8, 0x89, 0x7D, 0x05, 0xBE, 0x07, 0x00, 0xEB,
        0x5B, 0x8B, 0x46, 0xFE, 0x3B, 0x06, 0xCA, 0x89
    };
    public const long SignatureFileOffset = 0x8000; // where the signature lives inside the .COM image
    public const long ComToPlayerDelta = 0x148BD;    // player = comBase + this

    // --- player record (0x28 bytes) -----------------------------------------
    public const int PlayerRecordSize = 0x28;
    public const int OffName = 0x00;          // 16 bytes, ASCII, NUL-terminated
    public const int NameLength = 16;
    public const int OffMoney = 0x10;         // 3 bytes, base-100 LE
    public const int OffPrestige = 0x13;      // 1 byte
    public const int OffDriving = 0x14;       // 1 byte
    public const int OffMarksmanship = 0x15;  // 1 byte
    public const int OffMechanic = 0x16;      // 1 byte
    public const int OffFlags = 0x17;         // 0x20 = car with player, 0x80 = active
    public const int OffHealth = 0x1A;        // 1 byte (3 = healthy)
    public const int OffCity = 0x1B;          // 1 byte, 0..15
    public const int OffDestCity = 0x1C;      // 1 byte
    public const int OffTimeOfDay = 0x1D;     // 1 byte, hour/time counter; advances while driving
    public const int OffDay = 0x1E;           // 1 byte, days since Jan 1 2030
    public const int OffDayOfWeek = 0x21;     // 1 byte, 0 = Sunday
    public const int OffBodyArmor = 0x22;     // 1 byte

    // --- live "working" copies of the current city --------------------------
    // The running game renders the location banner / city menu from working
    // copies in the data segment, NOT from OffCity in the persistent record.
    // Found by diffing six memory dumps of known cities: these bytes equal the
    // current city in every city dump, and OffWorkCityActive reads 0xFF while
    // on the road. Editing only OffCity changes the saved value but does not
    // relocate the live game. Offsets are signed, relative to the record start
    // (they sit ahead of it in the data segment: player = DS:0x627D).
    public const int OffWorkCityActive = -0x37A9; // DS:0x2AD4 — 0xFF while driving
    public const int OffWorkCityMirror = -0x918;  // DS:0x5965 — mirror of current city
    public const int OffWorkCityBlock  = 0x191;   // DS:0x640E — copy in the road/quest block

    // --- car record (0xC5 bytes), begins at player + 0x50 -------------------
    public const int CarOffset = 0x50;
    public const int CarRecordSize = 0xC5;
    public const int CarOffName = 0x00;       // 16 bytes
    public const int CarOffMaxWeight = 0x13;  // 2 bytes base-100
    public const int CarOffWeightLeft = 0x15; // 2 bytes base-100
    public const int CarOffMaxSpaces = 0x17;  // 1 byte
    public const int CarOffSpacesLeft = 0x18; // 1 byte
    public const int CarOffHandling = 0x19;   // 1 byte
    public const int CarOffAccel = 0x1A;      // 1 byte
    public const int CarOffSuspension = 0x1B; // 1 byte type index (1 = Improved observed)
    public const int CarOffChassis = 0x1C;    // 1 byte type index (0 = Light observed)
    public const int CarOffValue = 0x1D;      // 2 bytes base-100
    public const int CarOffCity = 0x21;       // 1 byte
    public const int CarOffBattery = 0x22;    // 1 byte current
    public const int CarOffBatteryMax = 0x23; // 1 byte max
    public const int CarOffComponents = 0x24; // 20 records x 8 bytes

    public const int ComponentCount = 20;
    public const int ComponentSize = 8;
    public const int CompType = 0;
    public const int CompCurDp = 1;
    public const int CompMaxDp = 2;
    public const int CompLocation = 3;
    public const int CompSpaces = 4;
    public const int CompFlags = 5;      // 0x80 = present
    public const int CompAmmoLo = 6;
    public const int CompAmmoHi = 7;

    public const int PlantSlot = 0;
    public const int FirstTireSlot = 1;   // 1..4
    public const int FirstArmorSlot = 5;  // 5..9
    public const int FirstWeaponSlot = 10; // 10..19
    public const byte EmptyWeaponType = 0x0C;
    public const byte LaserType = 0x05;
    public const byte HeavyRocketType = 0x0B;
    public const byte SelfPoweredAmmo = 0xDD;

    public static readonly string[] Cities =
    {
        "Watertown", "Manchester", "Buffalo", "Syracuse",
        "Albany", "Boston", "Scranton", "New York",
        "Providence", "Pittsburgh", "Harrisburg", "Philadelphia",
        "Atlantic City", "Baltimore", "Dover", "Washington"
    };

    public static readonly string[] DaysOfWeek =
    {
        "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"
    };

    /// <summary>Weekday index of the epoch: 1 Jan 2030 (day 0) is a Friday.</summary>
    public const int EpochWeekday = 5;

    /// <summary>Highest value the single-byte day counter can hold.</summary>
    public const int DayMax = 255;

    /// <summary>
    /// A mid-morning value for the hour-of-day counter (<see cref="OffTimeOfDay"/>).
    /// Teleport resets the clock to this so the driver arrives during the day instead
    /// of at night (which forces an overnight stay at the truck stop). Higher values
    /// bring on the evening; normal in-city play was observed across hours 1–14, so 8
    /// sits solidly in daytime business hours.
    /// </summary>
    public const int DaytimeHour = 8;

    /// <summary>Weekday index (0 = Sunday) for a given day counter, per <see cref="EpochWeekday"/>.</summary>
    public static int WeekdayForDay(int day) => ((day % 7) + EpochWeekday) % 7;

    public static readonly string[] WeaponNames =
    {
        "Machine gun", "Flamethrower", "Rocket launcher", "Recoilless rifle",
        "Anti-tank gun", "Laser", "Minedropper", "Spikedropper",
        "Smokescreen", "Paint sprayer", "Oil jet", "Heavy rocket"
    };

    public static readonly string[] ArmorFacets =
    {
        "Front", "Back", "Left", "Right", "Underbody"
    };

    public static readonly string[] TireLocations =
    {
        "Front-left", "Front-right", "Back-left", "Back-right"
    };

    public static string WeaponName(byte type) =>
        type == EmptyWeaponType ? "(empty)"
        : type < WeaponNames.Length ? WeaponNames[type]
        : $"type {type}";

    public const int MoneyMax = 999_999;
    public const int SkillMax = 99;
    public const int HealthMax = 3;

    // --- base-100 ("centimal") integer codec --------------------------------
    // Valid digits are 0..99; any byte above that (0xFF = empty, 0xDD = self-powered
    // weapon sentinel, or leftover garbage) is not a real digit and contributes 0.
    public static int DecodeBase100(ReadOnlySpan<byte> bytes)
    {
        int value = 0, mul = 1;
        foreach (byte b in bytes)
        {
            if (b <= 99) value += b * mul;
            mul *= 100;
        }
        return value;
    }

    /// <summary>Largest value representable in <paramref name="byteCount"/> base-100 digits.</summary>
    public static int MaxBase100(int byteCount)
    {
        int max = 1;
        for (int i = 0; i < byteCount; i++) max *= 100;
        return max - 1;
    }

    public static byte[] EncodeBase100(int value, int byteCount)
    {
        value = Math.Clamp(value, 0, MaxBase100(byteCount));
        var result = new byte[byteCount];
        for (int i = 0; i < byteCount; i++)
        {
            result[i] = (byte)(value % 100);
            value /= 100;
        }
        return result;
    }
}
