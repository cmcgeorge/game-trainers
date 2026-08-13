namespace PoolOfRadianceTrainer.Game;

/// <summary>
/// The overland Moonsea map the party travels once Sokal Keep is cleared.
///
/// <para><b>Provenance — read this before trusting a square.</b> Unlike the districts and dungeons,
/// whose walls are decoded from the game's own <c>GEO*.DAX</c> level geometry (see
/// <see cref="MapTerrainData"/>), the overland terrain below is <i>transcribed from the clue-book
/// map</i> reproduced in <c>docs/strategy-guide.md</c> §9. The game does not keep the wilderness
/// terrain in RAM as a grid — a template match of this map against all 16 MB of the emulated guest,
/// at every width from 36 to 48 and one byte per square, finds nothing — and no shipped file
/// decodes to one either, so there is no decoded version to prefer. Treat the terrain as a travel
/// aid, not ground truth; the party's live coordinates and the teleport target are read from and
/// written to the game itself and are exact.</para>
///
/// <para><b>Coordinates</b> are the ones the game prints in its own status line ("25,25 W 04:09"),
/// which is what the trainer's live position and teleport target use. Origin (0,0) top-left,
/// x east, y south — the same convention as the indoor maps.</para>
///
/// <para><b>Extent.</b> The transcription is 40 columns wide (rows 2, 3 and 6 carry one or two
/// stray characters past that and are cut back to 40). The guide's own encounter-band note runs the
/// eastern band out to x = 41, so the area is declared two columns wider than the transcription and
/// those squares — like rows 0 and 1, which the map draws blank — are left
/// <see cref="FloorKind.Unknown"/> rather than invented. Teleport is not limited by any of this: it
/// writes whatever coordinates are typed.</para>
/// </summary>
public static class WildernessMap
{
    /// <summary>Columns the trainer draws — two more than the transcription covers (see remarks).</summary>
    public const int Width = 42;

    /// <summary>Rows the trainer draws; rows 0 and 1 are blank on the clue-book map.</summary>
    public const int Height = 33;

    /// <summary>
    /// One character per square, keyed as the guide prints it:
    /// <c>.</c> plains, <c>"</c> swamp, <c>+</c> forest, <c>&amp;</c> hills, <c>^</c> mountains,
    /// <c>~</c> river, <c>=</c> deep water. Letters are landmarks (see <see cref="Landmarks"/>) and
    /// leave the square's terrain unrecorded.
    /// </summary>
    public static readonly string[] Rows =
    {
        /*  0 */ "",
        /*  1 */ "",
        /*  2 */ "...&&&^^&&&&&&&&&&&&&&&&..&&&&&&&&&&&&&&",
        /*  3 */ "&&&^^^^^^^&&^^^^&^^&&&&..&&&&&&&&&&&&&&&",
        /*  4 */ "&^^^^^^^^^&&^^^^^^^^&&&&..&&&&&&&&&&&&&&",
        /*  5 */ "^^^^^^^^^&&&^^^^^^^^^&&&.&&&&&&&&&&&&&&&",
        /*  6 */ "^^^^^^^^&&&^^^^^^^^^^&&&..&&&&&&&&&&+&+&",
        /*  7 */ "^^^^^^^^&^^^^^^^^^^^&&&&.&&&&&&&&+++++&&",
        /*  8 */ "^^^^^^^^&&^^^^^^^^^&&&....&&&&&&+++m++&&",
        /*  9 */ "^^^^^^^^k&^^^^^^^^&&&&....&&&&&&+++++&&&",
        /* 10 */ "^^^^^^^^&&^^&&&&&&.&&......&&&&&&+++&&&&",
        /* 11 */ "^^^^^^^^&~&&&&&&++.....h....&&&&&&&&&&&&",
        /* 12 */ "^^^^^^^^&&~~~~+++++....++......&&&&&&&&&",
        /* 13 */ "&&^^^^^&&&&&&&~+++++..++++.....&&&&&&&&&",
        /* 14 */ "&&&^^^&&&&&..++~+f+++.+++++...&&&&&&&&&&",
        /* 15 */ ".&&&&&.&&&..+++====++++++++..&l&&&&&&&&&",
        /* 16 */ "..........+++++==g=.+++++++++.&~&&&&&&&&",
        /* 17 */ ".........+++++++===..+++++++++.~~&&&&&&&",
        /* 18 */ ".........+++++++.~~...++++++++..~~&&&&&&",
        /* 19 */ "....\"\"...++++++..~~...++++++++..&~&&&&&&",
        /* 20 */ "....\"\"....+++++..~....+++++++++.&~~&&&&&",
        /* 21 */ "....\"\".....++++..~~...+++++++++.&~&&&&&&",
        /* 22 */ "....\"\"\".....+++.++~~...+++++++&&&~&&&&&&",
        /* 23 */ ".....\"\".....++.+++.~...+++++++&&&~&..&&&",
        /* 24 */ ".....\"j\"......+++++~~...++++++&~~~&...&&",
        /* 25 */ ".....\"\"\"......+++++.~~~.+++++++~&...+++&",
        /* 26 */ ".....\"\".......++++++..~cc++++++~.++++++=",
        /* 27 */ ".....\"\"........+++++++.ab.++.++~+++++===",
        /* 28 */ ".....\"\"........+++++++d==.....+~~~~=====",
        /* 29 */ ".....\"\".........++e++++=========~e======",
        /* 30 */ ".....\"\".....==.=====+===================",
        /* 31 */ "..........n=============================",
        /* 32 */ ".i........==============================",
    };

    /// <summary>
    /// The lettered squares on the map above, expanded from the guide's landmark key. Every one is a
    /// place worth teleporting to, which is the point of listing them.
    /// </summary>
    public static readonly IReadOnlyList<MapLocation> Landmarks = new MapLocation[]
    {
        new("City edge → Phlan (a)",      23, 27, "Step here to return to New Phlan."),
        new("City edge → Phlan (b)",      24, 27, "Step here to return to New Phlan."),
        new("City edge → Phlan (c)",      23, 26, "Step here to return to New Phlan."),
        new("City edge → Phlan (c)",      24, 26, "Step here to return to New Phlan."),
        new("City edge → Phlan (d)",      22, 28, "Step here to return to New Phlan."),
        new("Boat landing (e)",           18, 29, "Boat to and from the Phlan docks."),
        new("Boat landing (e)",           33, 29, "Boat to and from the Phlan docks."),
        new("Rowboat to the pyramid (f)", 17, 14, "Rowboat across to Sorcerer's Isle."),
        new("Yarash's Pyramid (g)",       17, 16, "Sorcerer's Isle. Skip the maze with the teleporters — throw a rock " +
                                                  "through a portal to toggle its destination. Colour dial at (5,0) inside: " +
                                                  "Blue = exit, Copper/Silver/Gold = treasure rooms. Level-3 password NOKNOK; " +
                                                  "free the lizardmen (\"be Nice\") for the friend-word SAVIOR."),
        new("Nomad Camp (h)",             23, 11, ""),
        new("Zhentil Keep Outpost (i)",    1, 32, "Cadorna's betrayal trap. Set a watch, survive the night ambush, kill the " +
                                                  "Commandant for the Javelin of Lightning (a key weakness of the final dragon), " +
                                                  "Plate Mail +2 and a Ring of Fire Resistance."),
        new("Unidentified landmark (j)",   6, 24, "Lettered on the clue-book map but absent from its own key — left in rather " +
                                                  "than guessed at."),
        new("Silver dragon Diogenes (k)",  8,  9, "Parley — he is friendly."),
        new("Kobold Caves (l)",           30, 15, "Enter the Large entrance. Throne room is 3 waves (heal, but don't end each " +
                                                  "combat); envoys drop 2× Two-Handed Sword +2. Brass bottle (Efreeti) at (12,0) " +
                                                  "— say \"No\" to keep the bottle. Free Princess Fatima at (1,3)."),
        new("Lizardman Keep (m)",         35,  8, "Anti-magic zone. Give the old lizardman SAVIOR and champion him against " +
                                                  "Drythh to win the alliance without a bloodbath; catacomb pools hide 3× Shield +2."),
        new("Buccaneer Base (n)",         10, 31, ""),
    };

    /// <summary>The parsed terrain grid, sized <see cref="Width"/> × <see cref="Height"/>.</summary>
    public static BoardSquare[,] Terrain() => MapAscii.ParseTerrain(Rows, Width, Height);
}
