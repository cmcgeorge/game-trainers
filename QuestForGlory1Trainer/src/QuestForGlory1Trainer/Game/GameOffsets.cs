namespace QuestForGlory1Trainer.Game;

/// <summary>
/// SCI0 global-variable indices and engine constants for Quest for Glory I.
/// These are the <em>array indices</em> into the SCI0 global-variable array, not absolute
/// memory addresses — the address of global[N] changes every DOSBox session because the SCI
/// heap is dynamically allocated. Use the trainer's value scanner to locate the actual address,
/// then use these constants only as documentation / labelling.
/// </summary>
internal static class GameOffsets
{
    /// <summary>Global[1] — current room number (16-bit word).</summary>
    public const int GlobalRoom = 1;

    /// <summary>Global[2] — previous room number; written on every room transition.</summary>
    public const int GlobalPreviousRoom = 2;

    /// <summary>Global[3] — current in-game day (1-based, 16-bit word).</summary>
    public const int GlobalDay = 3;

    /// <summary>Global[4] — ticks within the current day (0–3599, 16-bit word).</summary>
    public const int GlobalTimeTicks = 4;

    /// <summary>Number of ticks per complete in-game day.</summary>
    public const int TicksPerDay = 3600;

    /// <summary>Number of ticks per in-game hour (24 × 150 = 3600).</summary>
    public const int TicksPerHour = 150;

    /// <summary>Tick boundary for Dawn (0 = midnight/dawn start).</summary>
    public const int TimeDawn = 0;

    /// <summary>Tick boundary for Mid-morning.</summary>
    public const int TimeMidMorning = 450;

    /// <summary>Tick boundary for Midday.</summary>
    public const int TimeMidDay = 1050;

    /// <summary>Tick boundary for Mid-afternoon.</summary>
    public const int TimeMidAfternoon = 1650;

    /// <summary>Tick boundary for Sunset.</summary>
    public const int TimeSunset = 2250;

    /// <summary>Tick boundary for Night.</summary>
    public const int TimeNight = 2850;

    /// <summary>Tick boundary for Midnight (late night).</summary>
    public const int TimeMidnight = 3300;
}
