namespace LegendOfFaerghailTrainer.Game;

/// <summary>
/// Static facts about the target build, and the DGROUP-relative constants the locator needs.
///
/// <para><b>The build.</b> <c>LOF.EXE</c> is a plain (unpacked) MZ image: 315,152 bytes, 5,428
/// relocations, header 0x054F paragraphs, entry <c>2F0E:0010</c>. It is a Microsoft C 1988
/// large-model build — the run-time carries <c>"MS Run-Time Library - Copyright (c) 1988,
/// Microsoft Corp"</c> and the <c>R6000</c>/<c>R6001</c> error strings — stamped
/// <c>19.06.1990</c>, published by reLINE Software GmbH (Hannover). The shipped copy is the
/// 1992 "Nemesis" hard-disk release; <c>START.BAT</c> runs <c>VOR.EXE</c> (the intro) and then
/// <c>LOF.EXE</c>, which refuses to start on its own ("Please start LOF with START.BAT!").</para>
///
/// <para><b>Why a locator works.</b> Large model gives the program one data group, so every
/// global has a constant <c>DS:</c> offset even though the load segment moves. DGROUP sits at
/// file offset 0x3BCD0, i.e. <c>DS offset = file offset - 0x3BCD0</c> for anything in it —
/// verified against ten separate literals read out of a live 16 MB guest. The party and roster
/// buffers themselves are heap allocations whose addresses change every session, but the game
/// keeps far pointers to both at fixed DGROUP offsets, so one anchored sweep resolves them.</para>
/// </summary>
public static class GameFacts
{
    public const string Title = "Legend of Faerghail";
    public const string Publisher = "reLINE Software GmbH";
    public const string Developer = "Electronic Design Hannover";
    public const string Year = "1990";
    public const string BuildStamp = "19.06.1990";
    public const string Executable = "LOF.EXE";

    /// <summary>Process names to offer on the attach list (DOSBox / DOSBox-X / DOSBox Staging).</summary>
    public static readonly string[] EmulatorProcessHints =
    {
        "dosbox", "dosbox-x", "dosbox_x", "dosbox-staging", "dosboxstaging"
    };

    // --- DGROUP anchors ---------------------------------------------------------
    // Every offset below is `file offset - 0x3BCD0`; each string was confirmed to occur
    // exactly once in a live 16 MB DOSBox guest.

    /// <summary>Primary anchor: the character sheet's abilities caption (24 bytes, unique).</summary>
    public const string PrimaryAnchorText = "Negotiating ability.... ";
    public const int PrimaryAnchorOffset = 0xF371;

    /// <summary>Corroborating anchors, each at its own DGROUP offset.</summary>
    public static readonly (string Text, int Offset)[] SecondaryAnchors =
    {
        ("Hit point ",               0xF23D),
        ("Common language",          0xF490),
        ("Warrior    ",              0x61C8),
        ("Load which gamestanding:", 0xDBE4),
    };

    /// <summary>DGROUP offset of the far pointer to party slot 0 (six records).</summary>
    public const int PartyPointerOffset = 0x0030;

    /// <summary>DGROUP offset of the far pointer to roster slot 0 (thirty-two records).</summary>
    public const int RosterPointerOffset = 0x3FF6;

    /// <summary>
    /// The party array sits immediately after the roster array in the same allocation, with two
    /// bytes between them (measured live: roster 0x4E6A2, party 0x519E4, delta 0x3342 =
    /// 32 x 410 + 2). Used only as a cross-check on the two pointers.
    /// </summary>
    public const int RosterToPartyDelta = CharacterFormat.RosterSlots * CharacterFormat.RecordSize + 2;

    // --- speed control ----------------------------------------------------------

    /// <summary>
    /// The game has no frame limiter of its own: it redraws and polls as fast as the CPU allows,
    /// so on anything above roughly 3,000 DOSBox cycles the text pages flick past and the
    /// wilderness scrolls too fast to steer. DOSBox's own cycle hotkeys are the fix.
    /// </summary>
    public const int SuggestedCycles = 3000;
}
