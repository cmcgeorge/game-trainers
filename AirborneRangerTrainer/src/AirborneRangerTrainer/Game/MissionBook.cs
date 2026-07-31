namespace AirborneRangerTrainer.Game;

/// <summary>The three terrain types the twelve missions cycle through.</summary>
public enum Terrain
{
    /// <summary>Heat drains stamina fast; running distance drops sharply.</summary>
    Desert,

    /// <summary>Farmland — ditches, ponds, tank traps.</summary>
    Temperate,

    /// <summary>Snow and wind muffle sound, so the enemy hears you later.</summary>
    Arctic,
}

/// <summary>One mission, exactly as the game lists and briefs it.</summary>
/// <param name="Number">Position in the game's mission list, from 1.</param>
/// <param name="Name">The name on the selection screen.</param>
/// <param name="Terrain">Which terrain set it uses.</param>
/// <param name="ChallengeLevel">The game's own rating, from the table at <c>DGROUP:0xA2D9</c>.</param>
/// <param name="Briefing">The briefing text, verbatim.</param>
/// <param name="Tip">A tactical note derived from the briefing and the strategy guide.</param>
public readonly record struct MissionInfo(
    int Number, string Name, Terrain Terrain, int ChallengeLevel, string Briefing, string Tip);

/// <summary>
/// The twelve missions plus the campaign.
///
/// <para>The names and briefings are the game's own literals (<c>DGROUP:0xA379</c> and
/// <c>DGROUP:0xA841</c> onwards). The challenge levels are the thirteen-character ASCII table
/// <c>"2111332222333"</c> at <c>DGROUP:0xA2D9</c>, one digit per list entry — confirmed live by
/// watching the <i>Challenge Level</i> readout change as the highlight moves down the list. The
/// terrain of each mission is the one its own briefing names, and it cycles
/// Desert → Temperate → Arctic cleanly.</para>
/// </summary>
public static class MissionBook
{
    /// <summary>The challenge-level table exactly as stored, including the campaign's digit.</summary>
    public const string ChallengeTable = "2111332222333";

    /// <summary>Challenge level of the campaign, the last digit of <see cref="ChallengeTable"/>.</summary>
    public static int CampaignChallengeLevel => ChallengeTable[^1] - '0';

    /// <summary>The twelve selectable missions, in list order.</summary>
    public static readonly IReadOnlyList<MissionInfo> All = new[]
    {
        new MissionInfo(1, "Destroy a Munitions Depot", Terrain.Desert, 2,
            "The enemy depot consists of an ammunition shack, a bunker-like explosives magazine, " +
            "and a fuel dump. All three should be destroyed.",
            "Three separate targets. Save the LAW rocket for the bunker-like magazine and use time " +
            "bombs or grenades on the shack and the fuel dump."),

        new MissionInfo(2, "Steal a Code Book", Terrain.Temperate, 1,
            "Infiltrate an enemy headquarters area, find the communications post, and move next to " +
            "it to steal the code book. WARNING: Enemy units are expecting trouble.",
            "No demolition needed — just reach the communications post. The garrison is already " +
            "alert, so crawl and stay in cover. One of the best missions to learn on."),

        new MissionInfo(3, "Disable Enemy Aircraft", Terrain.Arctic, 1,
            "Avoid enemy contact until you arrive in the runway area. Premature contact may cause " +
            "the enemy aircraft to leave. When you reach the runway, destroy all jet fighters " +
            "stationed there.",
            "Every shot before you reach the runway risks the aircraft scrambling and the mission " +
            "becoming unwinnable. Snow deadens sound, which is in your favour."),

        new MissionInfo(4, "Capture an Enemy Officer", Terrain.Desert, 1,
            "Infiltrate the enemy headquarters area. Search among the tents until you find an enemy " +
            "officer. Move next to him to capture, then recall your aircraft. Defend the prisoner " +
            "until the aircraft arrives.",
            "The officer wears a different-coloured uniform. Recall the aircraft the moment you have " +
            "him — the defence phase is a fixed fight and you want it short."),

        new MissionInfo(5, "Cut a Pipeline", Terrain.Temperate, 3,
            "Penetrate the defenses around a pipeline pumping station and destroy it. WARNING: " +
            "Beware of enemy minitanks deployed near the pumping station.",
            "The pumping station is armoured — use a time bomb, not grenades. The LAW rocket is the " +
            "only answer to a minitank."),

        new MissionInfo(6, "Knock Out Enemy Radar Array", Terrain.Arctic, 3,
            "Advance north of the icy river and destroy all radar antennas deployed there. Beware " +
            "of unsafe ice patches.",
            "Crossing the river is the mission. Unsafe ice drops you through and crawling underwater " +
            "drowns you, so pick your crossing on the map screen first."),

        new MissionInfo(7, "Disable SAM Site", Terrain.Desert, 2,
            "Destroy all SAM platforms at the launch site. Avoid enemy contact until you arrive in " +
            "the launch site area. Premature contact will result in a penalty.",
            "An explicit merit-point penalty for shooting early. One to four platforms, all of which " +
            "must go."),

        new MissionInfo(8, "Liberate a P.O.W. Camp", Terrain.Temperate, 2,
            "Avoid enemy contact until you arrive in the prison area, or the prisoners may be " +
            "removed. Ranger prisoners are being held in pit cells. To free them, blow up the " +
            "central control module, then kick the exposed lever. Recall your aircraft and defend " +
            "the prisoners until it arrives.",
            "Two stages: destroy the control module, then walk into the exposed lever to kick it. " +
            "Contact on the way in can empty the camp."),

        new MissionInfo(9, "Photograph an Experimental Aircraft", Terrain.Arctic, 2,
            "Infiltrate an enemy airfield and sneak into the hangar. Do not allow yourself to be " +
            "seen entering the hangar!",
            "Being seen entering fails it. Clear or avoid every sentry with a line of sight to the " +
            "door first — the knife is silent and alerts nobody."),

        new MissionInfo(10, "Free the Hostages", Terrain.Desert, 2,
            "Blow open the door on the Hostage Prison, then recall your aircraft. Defend the " +
            "hostages until the aircraft arrives. Beware of enemy attempts to destroy the prison " +
            "and kill the hostages.",
            "The enemy actively tries to kill the hostages, so you cannot just hide. Recall the " +
            "aircraft the instant the door is open."),

        new MissionInfo(11, "Create a Diversion", Terrain.Temperate, 3,
            "Do not commence combat until the countdown buzzer sounds. Shoot whenever it sounds " +
            "again. Fight your way to the border, causing combat as often as possible. Pickup Point " +
            "is in the border zone. The aircraft cannot be recalled early - be there when the " +
            "countdown clock reaches zero.",
            "The only mission where noise is the objective and the only one where recall does " +
            "nothing. Be standing on the Pickup Point before the clock reaches zero."),

        new MissionInfo(12, "Delayed Sabotage", Terrain.Arctic, 3,
            "Sneak past an enemy airfield's defense perimeter and plant a time bomb at the aviation " +
            "fuel dump. The time bomb will not explode until long after you leave; if it is to " +
            "remain undiscovered, it is essential that you not be seen in the vicinity of the fuel " +
            "dump.",
            "Being seen near the dump ruins it even with the bomb planted. Use the 15-second fuse " +
            "and leave by a different route."),
    };

    /// <summary>The campaign entry as it appears at the end of the mission list.</summary>
    public const string CampaignName = "***CAMPAIGN***";

    /// <summary>Number of selectable missions.</summary>
    public static int Count => All.Count;

    /// <summary>The mission with the given 1-based number, or null.</summary>
    public static MissionInfo? ByNumber(int number) =>
        number >= 1 && number <= All.Count ? All[number - 1] : null;
}
