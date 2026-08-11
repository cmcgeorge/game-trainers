namespace RedBaronTrainer.Game;

/// <summary>Which of Red Baron's two executables is currently in the emulator.</summary>
public enum GameModule
{
    /// <summary>Nothing recognisable was found.</summary>
    None,

    /// <summary>PS.EXE — the front end: main menu, career, roster, briefings.</summary>
    Shell,

    /// <summary>RB.EXE — the flight simulator.</summary>
    Simulator,
}

/// <summary>
/// Everything reverse-engineered about Red Baron (Dynamix, 1990), expressed as offsets inside
/// each executable's data group. Nothing here is an absolute address: <c>BARON.COM</c> chains
/// <c>PS.EXE</c>, which in turn chains <c>RB.EXE</c>, so both land at whatever segment DOS hands
/// them. <see cref="Memory.GameLocator"/> finds the data group at run time by sweeping for a
/// literal that lives at a known offset inside it, and every constant below is relative to that.
///
/// <para>Both executables are 16-bit Borland Turbo C++ builds. Their startup code does
/// <c>mov dx, DGROUP / mov ds, dx / mov ss, dx</c>, so <c>DS</c> is constant for the life of the
/// process and a "DS offset" is a stable, meaningful address.</para>
/// </summary>
public static class GameFacts
{
    // ---------------------------------------------------------------- shell (PS.EXE)

    /// <summary>Sits at <see cref="ShellAnchorOffset"/> in PS.EXE's data group; unique in a live guest.</summary>
    public const string ShellAnchorText = "Red Baron ver. 1.0, Copyright 1990 Dynamix, Inc.";

    public const int ShellAnchorOffset = 0x0A6F;

    /// <summary>Further PS.EXE literals used to corroborate a candidate data group.</summary>
    public static readonly (string Text, int Offset)[] ShellValidators =
    {
        ("Turbo C++ - Copyright 1990 Borland Intl.", 0x0004),
        ("DOGFIGHT A FAMOUS ACE", 0x0430),
        ("BALLOON BUSTING!", 0x07DC),
        ("PSVOLS.MAP", 0x0AD8),
    };

    /// <summary>The career currently being flown — one <see cref="PilotRecord"/>, same shape as a roster slot.</summary>
    public const int ActivePilotOffset = 0x557E;

    /// <summary>Ten <see cref="PilotRecord"/>s, byte-for-byte the body of <c>ROSTER.DAT</c> after its 8-byte header.</summary>
    public const int RosterOffset = 0x5610;

    /// <summary>PS.EXE's working copy of the single-mission realism panel (<c>MREAL.PRF</c>).</summary>
    public const int ShellRealismOffset = 0x4FBE;

    // ------------------------------------------------------------ simulator (RB.EXE)

    /// <summary>Sits at <see cref="SimAnchorOffset"/> in RB.EXE's data group; unique in a live guest.</summary>
    public const string SimAnchorText = "TIME COMPRESS DEACTIVATED.";

    public const int SimAnchorOffset = 0x014C;

    /// <summary>Further RB.EXE literals used to corroborate a candidate data group.</summary>
    public static readonly (string Text, int Offset)[] SimValidators =
    {
        ("YOU'RE LOW ON FUEL.", 0x0233),
        ("YOU'RE DANGEROUSLY LOW.", 0x026A),
        ("JOYSTICK AND RUDDER PEDALS DISABLED", 0x2163),
        ("VOLUME.RMF", 0x2E10),
    };

    /// <summary>
    /// Non-zero while the sim is reading the game port for stick and rudder. This is the flag the
    /// in-flight <c>Alt-J</c> toggle drives; RB.EXE keeps a second copy at
    /// <see cref="SimJoystickFlagMirrorOffset"/> and both must agree or the next toggle flips only one.
    /// </summary>
    public const int SimJoystickFlagOffset = 0x27B4;

    public const int SimJoystickFlagMirrorOffset = 0x6932;

    // ------------------------------------------------------------------ shared shapes

    /// <summary>Bytes per pilot record, in <c>ROSTER.DAT</c> and in memory.</summary>
    public const int PilotRecordSize = 90;

    /// <summary>Pilot slots in the roster.</summary>
    public const int RosterSlots = 10;

    /// <summary>Bytes of NUL-padded pilot name at the start of a pilot record.</summary>
    public const int PilotNameLength = 18;

    /// <summary><c>ROSTER.DAT</c> starts with this many bytes before the first record.</summary>
    public const int RosterFileHeaderSize = 8;

    /// <summary>Settings on the realism panel — the 13 16-bit values in <c>MREAL.PRF</c>/<c>CREAL.PRF</c>.</summary>
    public const int RealismSettingCount = 13;

    /// <summary>Bytes of a realism panel vector, in a <c>.PRF</c> file and in memory.</summary>
    public const int RealismBlockSize = RealismSettingCount * 2;

    /// <summary>Single-mission realism panel, written by the shell and read by the sim.</summary>
    public const string MissionRealismFileName = "MREAL.PRF";

    /// <summary>Career realism panel, written by the shell and read by the sim.</summary>
    public const string CareerRealismFileName = "CREAL.PRF";

    public const string RosterFileName = "ROSTER.DAT";

    /// <summary>Files that must all be present for a folder to be Red Baron's game directory.</summary>
    public static readonly string[] GameFolderMarkers = { "RB.EXE", "PS.EXE", "VOLUME.RMF" };
}
