using System.Text;

namespace AirborneRangerTrainer.Game;

/// <summary>
/// The reverse-engineered layout of <c>Airborne Ranger</c>'s live mission state.
///
/// <para><c>AR.EXE</c> is an EXEPACK-compressed, medium-model 16-bit program: one code segment, one
/// data segment (<c>DGROUP</c>) and a separate stack segment. Only the load segment moves between
/// sessions, so every global below sits at a constant <c>DGROUP</c> offset and the trainer only has
/// to find <c>DGROUP:0000</c> once.</para>
///
/// <para>The offsets were not guessed. The post-landing status panel is a fill-in-the-blanks text
/// template — the shipped executable stores literal <c>X</c> placeholders that the game overwrites
/// with ASCII digits — so searching the code segment for the placeholder addresses finds the single
/// routine that fills the panel, and that routine names its own source variables
/// (<c>mov al,[0xC896]</c> / <c>mov si,0xB955</c> / <c>call</c> the two-digit renderer). Every field
/// was then read out of a live DOSBox session and matched against the screen. See
/// <c>docs/ReverseEngineering.md</c>.</para>
/// </summary>
public static class MissionFormat
{
    /// <summary>Size of the game's data segment, in bytes. The whole trainer works inside this window.</summary>
    public const int DataSegmentSize = 0xE690;

    // --- the mission state ---------------------------------------------------

    /// <summary>Wounds taken. Three is death — the game gates on <c>cmp al,3</c> in three places. [Confirmed]</summary>
    public const int OffWounds = 0xC892;

    /// <summary>
    /// Rounds left in the loaded magazine, <b>signed</b>: 30 is a full magazine and a negative value
    /// means no magazine is loaded, which is how the panel decides whether to count one. [Confirmed]
    /// </summary>
    public const int OffRoundsInMagazine = 0xC894;

    /// <summary>Spare carbine magazines. The panel shows this <b>plus one</b> for the loaded one. [Confirmed]</summary>
    public const int OffSpareMagazines = 0xC895;

    /// <summary>Hand grenades. [Confirmed]</summary>
    public const int OffGrenades = 0xC896;

    /// <summary>LAW rockets. [Confirmed]</summary>
    public const int OffLawRockets = 0xC897;

    /// <summary>Time bombs. [Confirmed]</summary>
    public const int OffTimeBombs = 0xC898;

    /// <summary>First-aid kits. One kit removes exactly one wound. [Confirmed]</summary>
    public const int OffFirstAidKits = 0xC89A;

    /// <summary>
    /// Carried weight, excluding the loaded magazine — the panel prints this plus
    /// <see cref="OffMagazineLoaded"/>. Read-only here: the game recomputes it. [Confirmed]
    /// </summary>
    public const int OffCarriedWeight = 0xCA42;

    /// <summary>1 while a magazine is loaded; added to the displayed weight. [Confirmed]</summary>
    public const int OffMagazineLoaded = 0xE248;

    /// <summary>
    /// Mission countdown, hundreds digit. The clock is <b>three separate bytes, one decimal digit
    /// each</b> — which is why scanning all of DOSBox's RAM for the 16-bit value 600 while the panel
    /// read <c>TIME 600</c> finds nothing. [Confirmed]
    /// </summary>
    public const int OffClockHundreds = 0xBE54;

    /// <summary>Mission countdown, tens digit. [Confirmed]</summary>
    public const int OffClockTens = 0xBE55;

    /// <summary>Mission countdown, units digit. [Confirmed]</summary>
    public const int OffClockUnits = 0xBE56;

    /// <summary>Selected weapon — see <see cref="WeaponBook"/>. [Confirmed]</summary>
    public const int OffSelectedWeapon = 0xC891;

    /// <summary>Merit points earned so far this mission (u16). [Confirmed]</summary>
    public const int OffMeritPoints = 0xA2D4;

    /// <summary>Enemy soldiers eliminated this mission. [Confirmed]</summary>
    public const int OffSoldiersKilled = 0xA2D6;

    /// <summary>Military targets destroyed this mission. [Confirmed]</summary>
    public const int OffTargetsDestroyed = 0xA2D8;

    /// <summary>
    /// Start of the status-panel text template. The trainer reads it to show the panel exactly as
    /// the game last rendered it, and never writes to it — writing here would change the caption
    /// without changing the state behind it.
    /// </summary>
    public const int OffStatusPanel = 0xB910;

    /// <summary>Bytes of <see cref="OffStatusPanel"/> that cover the whole panel message.</summary>
    public const int StatusPanelLength = 0xA0;

    /// <summary>
    /// Contiguous window covering every field the poll loop reads. It spans
    /// <see cref="OffMeritPoints"/> (lowest) to <see cref="OffMagazineLoaded"/> (highest), so one
    /// read per tick refreshes everything.
    ///
    /// <para>The fields are scattered across ~16 KB, so most of what this covers is unrelated —
    /// including the anchor literals themselves, which sit between them. That is deliberate: one
    /// contiguous read is a few tens of microseconds and keeps every offset in this file a real
    /// <c>DGROUP</c> address, whereas five separate windows would buy nothing and introduce five
    /// chances to mis-map an offset. Writes are always one to three bytes, never the window.</para>
    /// </summary>
    public const int WindowStart = OffMeritPoints;

    /// <summary>Length of <see cref="WindowStart"/>, inclusive of the two-byte field at its end.</summary>
    public const int WindowLength = OffMagazineLoaded + 2 - WindowStart;

    // --- caps ----------------------------------------------------------------

    /// <summary>Rounds in a full carbine magazine — the value the game itself loads on a reload.</summary>
    public const int FullMagazine = 30;

    /// <summary>Wound count the game treats as death.</summary>
    public const int FatalWounds = 3;

    /// <summary>
    /// "Max" target for a supply counter. The panel renders two digits, so 99 is the largest value
    /// that displays correctly; the fields themselves are bytes.
    /// </summary>
    public const int MaxSupply = 99;

    /// <summary>
    /// "Max" target for <see cref="OffSpareMagazines"/>, which is one lower than every other counter
    /// because the panel prints it <b>plus one</b>. The game's two-digit renderer subtracts tens in a
    /// loop and does not range-check, so a displayed 100 comes out as the two characters <c>:0</c>
    /// rather than as a number — 98 spares is the most that still reads as <c>99</c>.
    /// </summary>
    public const int MaxSpareMagazines = MaxSupply - 1;

    /// <summary>Hard ceiling for a byte-sized supply counter.</summary>
    public const int SupplyCeiling = byte.MaxValue;

    /// <summary>Largest mission clock the three-digit display can show.</summary>
    public const int MaxClock = 999;

    /// <summary>Clock value a fresh mission starts at.</summary>
    public const int StartingClock = 600;

    /// <summary>"Max" target for merit points (u16).</summary>
    public const int MaxMeritPoints = 60_000;

    /// <summary>Hard ceiling for a u16 field.</summary>
    public const int MeritCeiling = ushort.MaxValue;

    // --- anchors used to find DGROUP in a live process ------------------------

    /// <summary>A literal from the game's data segment together with its <c>DGROUP</c> offset.</summary>
    /// <param name="Name">Human-readable name, shown in the status line.</param>
    /// <param name="Bytes">The literal's bytes.</param>
    /// <param name="DgroupOffset">Where the literal sits inside <c>DGROUP</c>.</param>
    public sealed record Anchor(string Name, byte[] Bytes, int DgroupOffset);

    private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    /// <summary>
    /// The status panel's first caption. Twelve bytes, unique in a live session, and the template it
    /// belongs to is the one whose placeholder addresses gave up every offset above.
    /// </summary>
    public static readonly Anchor PrimaryAnchor =
        new("status panel caption", Ascii("CARBINE MAGS"), 0xB923);

    /// <summary>
    /// Corroborating literals. Their distance from <see cref="PrimaryAnchor"/> is fixed by the
    /// executable, so requiring at least <see cref="MinValidators"/> of them to line up rejects a
    /// stale copy of the caption sitting in a disk buffer or an unrelated allocation.
    /// </summary>
    public static readonly Anchor[] Validators =
    {
        new("rank table", Ascii("PFC CPL SGT SSG PSG SGM 2LT 1LT CPT MAJ LTC COL"), 0xBB64),
        new("decoration line", Ascii("COM1 COM2 BSTR SSTR DSC CMH"), 0xBBA6),
        new("mission list", Ascii("Destroy a Munitions Depot"), 0xA379),
        new("version string", Ascii("441.01"), 0xB7F7),
    };

    /// <summary>How many of <see cref="Validators"/> must line up before a candidate is accepted.</summary>
    public const int MinValidators = 2;

    // --- little-endian accessors ---------------------------------------------

    /// <summary>Reads the byte at <paramref name="off"/>.</summary>
    public static byte ReadU8(byte[] b, int off) => b[off];

    /// <summary>Reads the byte at <paramref name="off"/> as a signed value.</summary>
    public static sbyte ReadI8(byte[] b, int off) => unchecked((sbyte)b[off]);

    /// <summary>Reads the little-endian 16-bit value at <paramref name="off"/>.</summary>
    public static ushort ReadU16(byte[] b, int off) => (ushort)(b[off] | (b[off + 1] << 8));

    /// <summary>Writes the little-endian 16-bit value at <paramref name="off"/>.</summary>
    public static void WriteU16(byte[] b, int off, ushort v)
    {
        b[off] = (byte)v;
        b[off + 1] = (byte)(v >> 8);
    }

    /// <summary>
    /// The magazine count the panel prints: spare magazines plus one when a magazine is loaded.
    /// Reproduces the game's own <c>or al,al / jnz / cmp [rounds],0 / jl / inc al</c>.
    /// </summary>
    public static int DisplayedMagazines(int spare, int roundsInMagazine) =>
        spare != 0 || roundsInMagazine >= 0 ? spare + 1 : spare;

    /// <summary>Combines the three clock digit bytes into the number the panel shows.</summary>
    public static int ComposeClock(int hundreds, int tens, int units) =>
        hundreds * 100 + tens * 10 + units;

    /// <summary>Splits a clock value into the three digit bytes the game stores.</summary>
    public static (byte Hundreds, byte Tens, byte Units) SplitClock(int value)
    {
        int v = Math.Clamp(value, 0, MaxClock);
        return ((byte)(v / 100), (byte)(v / 10 % 10), (byte)(v % 10));
    }

    /// <summary>
    /// True when <paramref name="window"/> — a read of <see cref="WindowLength"/> bytes starting at
    /// <see cref="WindowStart"/> — holds a plausible mission state.
    ///
    /// <para>Deliberately loose. Between missions the game leaves this block holding whatever the
    /// last one ended with, and a fresh install has never run one, so the check must accept a
    /// completely zeroed block. What it rejects is a window that could not be mission state at all:
    /// clock digits outside 0..9, more wounds than the game can represent, or a magazine holding
    /// more rounds than one can take.</para>
    /// </summary>
    public static bool LooksLikeMissionState(byte[] window)
    {
        if (window == null || window.Length < WindowLength) return false;

        int Rel(int dgroupOffset) => dgroupOffset - WindowStart;

        if (window[Rel(OffClockHundreds)] > 9) return false;
        if (window[Rel(OffClockTens)] > 9) return false;
        if (window[Rel(OffClockUnits)] > 9) return false;

        // The game writes 4 for an instant kill, so accept one past the fatal count but no more.
        if (window[Rel(OffWounds)] > FatalWounds + 1) return false;

        if (ReadI8(window, Rel(OffRoundsInMagazine)) > FullMagazine) return false;

        return window[Rel(OffSelectedWeapon)] <= WeaponBook.MaxCode;
    }
}
