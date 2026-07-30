namespace PiratesTrainer.Game;

/// <summary>
/// One settlement as the game stores it: a 24-byte record of twelve data bytes followed by a
/// twelve-character name. Values here are the <b>starting</b> state for the era — the live table in the
/// running game drifts as towns are sacked, change hands and grow.
/// </summary>
/// <param name="Index">Position in the era's table; the game addresses cities by this index.</param>
/// <param name="Name">Name exactly as the game's table spells it (twelve columns, so several are abbreviated).</param>
/// <param name="X">Map column (record byte 1), 0-255 west-to-east across the Spanish Main.</param>
/// <param name="Y">Map row (record byte 2), 0-255 north-to-south.</param>
/// <param name="Nation">Owning power (record byte 3: 0 Spanish, 1 English, 2 French, 3 Dutch).</param>
/// <param name="Forts">Number of forts guarding the approach (record byte 4, low nibble).</param>
/// <param name="Soldiers">Garrison strength — record byte 5 times ten, as the info screen prints it.</param>
/// <param name="Citizens">Population — (record byte 6 + 1) times one hundred, as the info screen prints it.</param>
/// <param name="GoldThousands">Treasury in thousands of gold pieces (record byte 7).</param>
/// <param name="Prosperity">Wealth band (record byte 8, top two bits).</param>
public sealed record City(
    int Index, string Name, int X, int Y, string Nation, int Forts,
    int Soldiers, int Citizens, int GoldThousands, string Prosperity)
{
    /// <summary>Gold in the town's treasury, in gold pieces.</summary>
    public int Gold => GoldThousands * 1000;

    /// <summary>Compact "Forts/Soldiers/Citizens" summary for a grid column.</summary>
    public string Defence => $"{Forts} fort{(Forts == 1 ? "" : "s")}, {Soldiers} soldiers";
}

/// <summary>
/// The six historical eras and their settlement tables, decoded from <c>DISK1</c> (one 1,024-byte block
/// per era at file offset 0x54000 + 0x400 x era; the running game loads the whole block to
/// <c>DGROUP:0x4240</c>). Nothing here touches the live process — it is the reference a player uses to
/// decide where the money is, and it is what the trainer's located city table is validated against.
/// </summary>
public static class CityBook
{
    /// <summary>Start years of the six selectable time periods, in table order.</summary>
    public static readonly IReadOnlyList<int> EraYears = new[] { 1560, 1600, 1620, 1640, 1660, 1680 };

    /// <summary>Display names of the six selectable time periods, in table order.</summary>
    public static readonly IReadOnlyList<string> EraNames = new[]
    {
        "The Silver Empire (1560)", "Merchants and Smugglers (1600)", "The New Colonists (1620)",
        "War For Profit (1640)", "The Buccaneer Heroes (1660)", "Pirates' Sunset (1680)",
    };

    /// <summary>Settlements of the 1560 era (32 in the table).</summary>
    public static readonly IReadOnlyList<City> Era1560 = new[]
    {
        new City( 0, "BORBURATA",     125, 173, "Spanish", 1,  180,  2200,  10, "Surviving"),
        new City( 1, "CAMPECHE",       26,  90, "Spanish", 1,  250,  3100,  40, "Prospering"),
        new City( 2, "CARTAGENA",      89, 178, "Spanish", 4,  400,  4600,  40, "Prospering"),
        new City( 3, "CORO",          113, 165, "Spanish", 0,  100,  1400,  10, "Surviving"),
        new City( 4, "CUMANA",        137, 172, "Spanish", 1,  200,  1700,  25, "Prospering"),
        new City( 5, "ELEUTHERA",      85,  50, "English", 0,   10,   200,   0, "Struggling"),
        new City( 6, "FLORIDA KEYS",   65,  49, "French",  0,   10,   300,   0, "Struggling"),
        new City( 7, "GIBRALTAR",     108, 183, "Spanish", 0,   50,  1700,   0, "Surviving"),
        new City( 8, "GRAN GRANADA",   44, 160, "Spanish", 0,  250,  1900,  15, "Surviving"),
        new City( 9, "GRAND BAHAMA",   75,  33, "French",  0,   10,   300,   0, "Struggling"),
        new City(10, "HAVANA",         60,  64, "Spanish", 3,  250,  2100,  50, "Prospering"),
        new City(11, "ISABELLA",      107,  93, "Spanish", 0,   50,  1100,   0, "Surviving"),
        new City(12, "MARACAIBO",     105, 174, "Spanish", 1,  120,  1300,  15, "Prospering"),
        new City(13, "MARGARITA",     139, 167, "Spanish", 1,  130,   900,  25, "Surviving"),
        new City(14, "NASSAU",         80,  47, "English", 0,   10,   200,   0, "Struggling"),
        new City(15, "NOMBRE DIOS",    72, 184, "Spanish", 1,  120,  1400,  15, "Surviving"),
        new City(16, "PANAMA",         71, 189, "Spanish", 1,  250,  5100,  50, "Wealthy"),
        new City(17, "PR.CABELLO",    121, 174, "Spanish", 1,   80,  1100,  10, "Surviving"),
        new City(18, "PR.PRINCIPE",    78,  79, "Spanish", 1,  120,  2600,  15, "Prospering"),
        new City(19, "RIO DE HACHA",   99, 166, "Spanish", 1,  160,  2300,  30, "Prospering"),
        new City(20, "SAN JUAN",      127, 103, "Spanish", 3,  300,  3300,   6, "Prospering"),
        new City(21, "SANT.DOMINGO",  111, 104, "Spanish", 4,  500,  4100,  10, "Prospering"),
        new City(22, "SANTIAGO",       86,  91, "Spanish", 3,  450,  5100,  90, "Wealthy"),
        new City(23, "SANTIGO VEGA",   83, 110, "Spanish", 0,   20,   700,   0, "Struggling"),
        new City(24, "SANTA MARTA",    94, 168, "Spanish", 1,   80,  1900,  12, "Surviving"),
        new City(25, "ST.AUGUSTINE",   65,   4, "French",  0,  150,   700,   0, "Struggling"),
        new City(26, "TRINIDAD",      149, 172, "Spanish", 0,   10,   600,   0, "Struggling"),
        new City(27, "VERA CRUZ",       4,  92, "Spanish", 2,  350,  3100,  50, "Prospering"),
        new City(28, "VILLAHERMOSA",   17, 104, "Spanish", 0,  200,  2100,  20, "Surviving"),
        new City(29, "YAGUANA",       102, 103, "Spanish", 0,   30,   900,   0, "Struggling"),
        new City(30, "FLORIDA CHNL",   67,  51, "Spanish", 0,    0,   100,   0, "Wealthy"),
        new City(31, "FLORIDA CHNL",   71,  33, "Spanish", 0,    0,   100,   0, "Wealthy"),
    };

    /// <summary>Settlements of the 1600 era (32 in the table).</summary>
    public static readonly IReadOnlyList<City> Era1600 = new[]
    {
        new City( 0, "CAMPECHE",       26,  90, "Spanish", 1,  220,  2900,  30, "Prospering"),
        new City( 1, "CARACAS",       125, 173, "Spanish", 1,  300,  2600,   5, "Surviving"),
        new City( 2, "CARTAGENA",      89, 178, "Spanish", 4,  400,  4700,  50, "Prospering"),
        new City( 3, "CORO",          113, 165, "Spanish", 0,   80,  1000,   0, "Surviving"),
        new City( 4, "CUMANA",        137, 172, "Spanish", 1,  200,  1900,  15, "Surviving"),
        new City( 5, "ELEUTHERA",      85,  50, "French",  0,   50,   300,   0, "Struggling"),
        new City( 6, "GIBRALTAR",     108, 183, "Spanish", 0,   60,  1700,   0, "Surviving"),
        new City( 7, "GRAN GRANADA",   44, 160, "Spanish", 0,  230,  2300,  30, "Prospering"),
        new City( 8, "GRAND BAHAMA",   75,  33, "French",  0,   50,   300,   0, "Struggling"),
        new City( 9, "GRENADA",       148, 157, "English", 0,   10,   400,   0, "Struggling"),
        new City(10, "HAVANA",         60,  64, "Spanish", 4,  500,  6100,  50, "Wealthy"),
        new City(11, "LA VEGA",       107,  93, "Spanish", 0,   50,   300,   0, "Struggling"),
        new City(12, "MARACAIBO",     105, 174, "Spanish", 1,  130,  1800,  10, "Prospering"),
        new City(13, "MARGARITA",     139, 167, "Spanish", 1,  110,  1000,  10, "Surviving"),
        new City(14, "PANAMA",         71, 189, "Spanish", 1,  250,  5100,  70, "Wealthy"),
        new City(15, "PR.CABELLO",    121, 174, "Spanish", 1,   80,  1100,   0, "Surviving"),
        new City(16, "PR.PRINCIPE",    78,  79, "Spanish", 1,  120,  2400,  15, "Prospering"),
        new City(17, "PUERTO BELLO",   70, 184, "Spanish", 2,  150,  1300,  10, "Surviving"),
        new City(18, "RIO DE HACHA",   99, 166, "Spanish", 1,  200,  2600,  30, "Prospering"),
        new City(19, "SAN JUAN",      127, 103, "Spanish", 3,  280,  3100,   6, "Prospering"),
        new City(20, "SANT.DOMINGO",  111, 104, "Spanish", 4,  400,  4300,  10, "Prospering"),
        new City(21, "SANTA MARTA",    94, 168, "Spanish", 1,   90,  1900,  10, "Surviving"),
        new City(22, "SANTIAGO",       86,  91, "Spanish", 3,  400,  4600,  90, "Prospering"),
        new City(23, "SANTIGO VEGA",   83, 110, "Spanish", 0,   20,   700,   0, "Struggling"),
        new City(24, "ST.AUGUSTINE",   65,   4, "Spanish", 1,  100,   700,   0, "Struggling"),
        new City(25, "ST.LUCIA",      149, 138, "English", 0,   10,   400,   0, "Struggling"),
        new City(26, "ST.THOME",      146, 187, "Spanish", 0,   40,   300,   0, "Struggling"),
        new City(27, "TRINIDAD",      149, 172, "Spanish", 0,   30,  1200,   0, "Struggling"),
        new City(28, "VERA CRUZ",       4,  92, "Spanish", 2,  300,  2900,  45, "Prospering"),
        new City(29, "VILLAHERMOSA",   17, 104, "Spanish", 0,  180,  2200,  20, "Surviving"),
        new City(30, "FLORIDA CHNL",   67,  51, "Spanish", 0,    0,   100,   0, "Wealthy"),
        new City(31, "FLORIDA CHNL",   71,  33, "Spanish", 0,    0,   100,   0, "Wealthy"),
    };

    /// <summary>Settlements of the 1620 era (38 in the table).</summary>
    public static readonly IReadOnlyList<City> Era1620 = new[]
    {
        new City( 0, "BARBADOS",      156, 146, "English", 1,   70,  1900,   2, "Surviving"),
        new City( 1, "CAMPECHE",       26,  90, "Spanish", 1,  200,  2600,  30, "Prospering"),
        new City( 2, "CARACAS",       125, 173, "Spanish", 1,  250,  2300,   5, "Surviving"),
        new City( 3, "CARTAGENA",      89, 178, "Spanish", 4,  400,  4600,  50, "Prospering"),
        new City( 4, "CORO",          113, 165, "Spanish", 0,   70,   800,   0, "Struggling"),
        new City( 5, "CUMANA",        137, 172, "Spanish", 1,  190,  1800,  15, "Surviving"),
        new City( 6, "CURACAO",       116, 159, "Dutch",   1,   90,   900,   0, "Surviving"),
        new City( 7, "ELEUTHERA",      85,  50, "English", 0,   40,   400,   0, "Struggling"),
        new City( 8, "FLORIDA KEYS",   65,  49, "Dutch",   0,   30,   200,   0, "Struggling"),
        new City( 9, "GRAN GRANADA",   44, 160, "Spanish", 0,  180,  2200,  28, "Prospering"),
        new City(10, "GRAND BAHAMA",   75,  33, "Dutch",   0,   30,   200,   0, "Struggling"),
        new City(11, "GIBRALTAR",     108, 183, "Spanish", 0,   50,  1600,   0, "Surviving"),
        new City(12, "HAVANA",         60,  64, "Spanish", 4,  440,  5900,  70, "Prospering"),
        new City(13, "LA VEGA",       107,  93, "Spanish", 0,   10,   400,   0, "Struggling"),
        new City(14, "MARACAIBO",     105, 174, "Spanish", 1,  140,  1800,  12, "Prospering"),
        new City(15, "MARGARITA",     139, 167, "Spanish", 0,   90,  1000,  10, "Struggling"),
        new City(16, "NEVIS",         142, 113, "English", 0,   40,   600,   0, "Struggling"),
        new City(17, "PANAMA",         71, 189, "Spanish", 1,  300,  5600,  75, "Wealthy"),
        new City(18, "PETIT GOAVE",    99, 106, "French",  0,   50,   400,   0, "Struggling"),
        new City(19, "PR.CABELLO",    121, 174, "Spanish", 1,   70,  1000,   0, "Surviving"),
        new City(20, "PR.PRINCIPE",    78,  79, "Spanish", 2,  130,  2300,  10, "Surviving"),
        new City(21, "PROVIDENCE",     63, 150, "English", 1,   80,   800,   2, "Surviving"),
        new City(22, "PUERTO BELLO",   70, 184, "Spanish", 2,  160,  1800,  18, "Prospering"),
        new City(23, "RIO DE HACHA",   99, 166, "Spanish", 1,  200,  2100,  25, "Prospering"),
        new City(24, "SAN JUAN",      127, 103, "Spanish", 3,  270,  2900,   5, "Prospering"),
        new City(25, "SANT.DOMINGO",  111, 104, "Spanish", 3,  330,  3900,   9, "Prospering"),
        new City(26, "SANTA MARTA",    94, 168, "Spanish", 1,   90,  1700,  10, "Surviving"),
        new City(27, "SANTIAGO",       86,  91, "Spanish", 3,  360,  4300,  60, "Prospering"),
        new City(28, "SANTIGO VEGA",   83, 110, "Spanish", 0,   30,   800,   0, "Struggling"),
        new City(29, "ST.AUGUSTINE",   65,   4, "Spanish", 1,   80,   900,   0, "Struggling"),
        new City(30, "ST.CHRISTOPH",  141, 111, "French",  0,   40,  1000,   0, "Surviving"),
        new City(31, "ST.THOME",      146, 187, "Spanish", 0,   20,   600,   0, "Struggling"),
        new City(32, "TORTUGA",        99,  91, "French",  0,   70,   400,   0, "Struggling"),
        new City(33, "TRINIDAD",      149, 172, "Spanish", 0,   30,   900,   0, "Surviving"),
        new City(34, "VERA CRUZ",       4,  92, "Spanish", 3,  250,  2700,  40, "Prospering"),
        new City(35, "VILLAHERMOSA",   17, 104, "Spanish", 0,  160,  1900,  20, "Surviving"),
        new City(36, "FLORIDA CHNL",   67,  51, "Spanish", 0,    0,   100,   0, "Wealthy"),
        new City(37, "FLORIDA CHNL",   71,  33, "Spanish", 0,    0,   100,   0, "Wealthy"),
    };

    /// <summary>Settlements of the 1640 era (41 in the table).</summary>
    public static readonly IReadOnlyList<City> Era1640 = new[]
    {
        new City( 0, "ANTIGUA",       145, 114, "English", 0,   20,   700,   0, "Struggling"),
        new City( 1, "BARBADOS",      156, 146, "English", 2,  180,  3100,  12, "Prospering"),
        new City( 2, "BERMUDA",       131,   3, "English", 0,   30,   700,   0, "Struggling"),
        new City( 3, "CAMPECHE",       26,  90, "Spanish", 2,  220,  2100,  25, "Surviving"),
        new City( 4, "CARACAS",       125, 173, "Spanish", 2,  200,  2600,  10, "Surviving"),
        new City( 5, "CARTAGENA",      89, 178, "Spanish", 4,  400,  5100,  50, "Prospering"),
        new City( 6, "CURACAO",       116, 159, "Dutch",   2,  200,  2200,   8, "Prospering"),
        new City( 7, "CUMANA",        137, 172, "Spanish", 2,  200,  2100,  10, "Surviving"),
        new City( 8, "ELEUTHERA",      85,  50, "English", 0,   50,   700,   0, "Struggling"),
        new City( 9, "FLORIDA KEYS",   65,  49, "French",  0,   10,   200,   0, "Struggling"),
        new City(10, "GRAN GRANADA",   44, 160, "Spanish", 0,  220,  2100,  25, "Prospering"),
        new City(11, "GUADELOUPE",    146, 121, "French",  1,  100,  1300,   2, "Surviving"),
        new City(12, "GIBRALTAR",     108, 183, "Spanish", 0,   60,  1800,   0, "Surviving"),
        new City(13, "HAVANA",         60,  64, "Spanish", 4,  420,  6500,  65, "Wealthy"),
        new City(14, "LA VEGA",       107,  93, "Spanish", 0,   40,   700,   0, "Struggling"),
        new City(15, "MARACAIBO",     105, 174, "Spanish", 2,  200,  2100,  15, "Surviving"),
        new City(16, "MARGARITA",     139, 167, "Spanish", 0,   90,   900,   6, "Surviving"),
        new City(17, "MARTINIQUE",    149, 134, "French",  1,  100,  1300,   2, "Surviving"),
        new City(18, "MONTSERRAT",    144, 116, "English", 0,   20,   700,   0, "Struggling"),
        new City(19, "NEVIS",         142, 113, "English", 1,  110,  2000,   0, "Surviving"),
        new City(20, "PANAMA",         71, 189, "Spanish", 1,  350,  5600,  75, "Wealthy"),
        new City(21, "PETIT GOAVE",    99, 106, "French",  0,   70,   400,   2, "Struggling"),
        new City(22, "PR.PRINCIPE",    78,  79, "Spanish", 2,  160,  2600,  10, "Prospering"),
        new City(23, "PUERTO BELLO",   70, 184, "Spanish", 2,  250,  2600,  20, "Prospering"),
        new City(24, "RIO DE HACHA",   99, 166, "Spanish", 1,  160,  2000,  20, "Surviving"),
        new City(25, "SAN JUAN",      127, 103, "Spanish", 3,  260,  2800,   5, "Surviving"),
        new City(26, "SAN.CATALINA",   63, 150, "Spanish", 2,  140,   900,   0, "Struggling"),
        new City(27, "SANT.DOMINGO",  111, 104, "Spanish", 3,  290,  3500,   9, "Prospering"),
        new City(28, "SANTA MARTA",    94, 168, "Spanish", 0,  100,  1800,  10, "Surviving"),
        new City(29, "SANTIAGO",       86,  91, "Spanish", 3,  350,  4600,  50, "Prospering"),
        new City(30, "SANTIGO VEGA",   83, 110, "Spanish", 0,   50,  1000,   0, "Surviving"),
        new City(31, "ST.AUGUSTINE",   65,   4, "Spanish", 1,   70,   800,   0, "Struggling"),
        new City(32, "ST.EUSTATIUS",  141, 109, "Dutch",   1,  110,  1600,   7, "Surviving"),
        new City(33, "ST.KITTS",      141, 111, "English", 2,  140,  2400,   4, "Surviving"),
        new City(34, "ST.MARTIN",     140, 105, "Dutch",   0,   70,   900,   0, "Surviving"),
        new City(35, "TORTUGA",        99,  91, "French",  2,  150,  1700,   8, "Surviving"),
        new City(36, "TRINIDAD",      149, 172, "Spanish", 0,   40,  1100,   0, "Surviving"),
        new City(37, "VERA CRUZ",       4,  92, "Spanish", 3,  250,  2600,  40, "Prospering"),
        new City(38, "VILLAHERMOSA",   17, 104, "Spanish", 0,  180,  1600,  18, "Surviving"),
        new City(39, "FLORIDA CHNL",   67,  51, "Spanish", 0,    0,   100,   0, "Wealthy"),
        new City(40, "FLORIDA CHNL",   71,  33, "Spanish", 0,    0,   100,   0, "Wealthy"),
    };

    /// <summary>Settlements of the 1660 era (41 in the table).</summary>
    public static readonly IReadOnlyList<City> Era1660 = new[]
    {
        new City( 0, "ANTIGUA",       145, 114, "English", 0,   60,  1300,   2, "Surviving"),
        new City( 1, "BARBADOS",      156, 146, "English", 2,  150,  3000,  18, "Prospering"),
        new City( 2, "BERMUDA",       131,   3, "English", 0,   30,   800,   1, "Surviving"),
        new City( 3, "CAMPECHE",       26,  90, "Spanish", 3,  250,  2100,  20, "Surviving"),
        new City( 4, "CARACAS",       125, 173, "Spanish", 2,  250,  2900,  12, "Surviving"),
        new City( 5, "CARTAGENA",      89, 178, "Spanish", 4,  400,  5100,  55, "Prospering"),
        new City( 6, "CUMANA",        137, 172, "Spanish", 2,  180,  2200,  10, "Surviving"),
        new City( 7, "CURACAO",       116, 159, "Dutch",   2,  180,  2300,  15, "Prospering"),
        new City( 8, "ELEUTHERA",      85,  50, "English", 0,   20,   700,   1, "Struggling"),
        new City( 9, "GRAN GRANADA",   44, 160, "Spanish", 0,  200,  2100,  25, "Prospering"),
        new City(10, "GIBRALTAR",     108, 183, "Spanish", 0,   50,  1500,   1, "Surviving"),
        new City(11, "GUADELOUPE",    146, 121, "French",  2,  160,  2000,  10, "Surviving"),
        new City(12, "HAVANA",         60,  64, "Spanish", 4,  450,  6600,  60, "Wealthy"),
        new City(13, "LEOGANE",       100, 104, "French",  0,   60,   900,   2, "Struggling"),
        new City(14, "MARACAIBO",     105, 174, "Spanish", 2,  120,  1900,  15, "Prospering"),
        new City(15, "MARGARITA",     139, 167, "Spanish", 0,   80,  1000,   6, "Surviving"),
        new City(16, "MARTINIQUE",    149, 134, "French",  2,  160,  2000,  10, "Surviving"),
        new City(17, "MONTSERRAT",    144, 116, "English", 1,   60,  1300,   2, "Surviving"),
        new City(18, "NEVIS",         142, 113, "English", 1,   80,  2100,   2, "Surviving"),
        new City(19, "PANAMA",         71, 189, "Spanish", 1,  400,  6100,  80, "Wealthy"),
        new City(20, "PETIT GOAVE",    99, 106, "French",  0,   80,  1400,   5, "Surviving"),
        new City(21, "PORT-DE-PAIX",   99,  93, "French",  1,   90,  1700,   8, "Surviving"),
        new City(22, "PORT ROYALE",    83, 110, "English", 1,   80,  1600,   4, "Surviving"),
        new City(23, "PR.PRINCIPE",    78,  79, "Spanish", 2,  220,  3100,  10, "Prospering"),
        new City(24, "PUERTO BELLO",   70, 184, "Spanish", 2,  250,  2600,  15, "Surviving"),
        new City(25, "RIO DE HACHA",   99, 166, "Spanish", 1,  120,  1900,  18, "Surviving"),
        new City(26, "SAN JUAN",      127, 103, "Spanish", 3,  250,  2500,   4, "Surviving"),
        new City(27, "SAN.CATALINA",   63, 150, "Spanish", 1,   70,   700,   0, "Struggling"),
        new City(28, "SANT.DOMINGO",  111, 104, "Spanish", 3,  280,  3200,   8, "Surviving"),
        new City(29, "SANTA MARTA",    94, 168, "Spanish", 1,   80,  1700,  10, "Surviving"),
        new City(30, "SANTIAGO",       86,  91, "Spanish", 3,  300,  4100,  40, "Prospering"),
        new City(31, "ST.AUGUSTINE",   65,   4, "Spanish", 1,   80,  1100,   0, "Struggling"),
        new City(32, "ST.EUSTATIUS",  141, 109, "Dutch",   2,   80,  1600,  12, "Prospering"),
        new City(33, "ST.KITTS",      141, 111, "English", 2,  100,  2600,   6, "Prospering"),
        new City(34, "ST.MARTIN",     140, 105, "Dutch",   1,   60,  1400,   3, "Surviving"),
        new City(35, "TORTUGA",        99,  91, "French",  1,   70,  1400,   5, "Surviving"),
        new City(36, "TRINIDAD",      149, 172, "Spanish", 0,   50,  1100,   0, "Surviving"),
        new City(37, "VERA CRUZ",       4,  92, "Spanish", 4,  300,  2600,  35, "Prospering"),
        new City(38, "VILLAHERMOSA",   17, 104, "Spanish", 0,  150,  1700,  15, "Surviving"),
        new City(39, "FLORIDA CHNL",   67,  51, "Spanish", 0,    0,   100,   0, "Wealthy"),
        new City(40, "FLORIDA CHNL",   71,  33, "Spanish", 0,    0,   100,   0, "Prospering"),
    };

    /// <summary>Settlements of the 1680 era (41 in the table).</summary>
    public static readonly IReadOnlyList<City> Era1680 = new[]
    {
        new City( 0, "ANTIGUA",       145, 114, "English", 0,   60,  1600,   2, "Surviving"),
        new City( 1, "BARBADOS",      156, 146, "English", 3,  180,  2800,  15, "Prospering"),
        new City( 2, "BELIZE",         34, 111, "English", 0,   20,  1100,   0, "Struggling"),
        new City( 3, "BERMUDA",       131,   3, "English", 0,   30,   900,   2, "Surviving"),
        new City( 4, "CAMPECHE",       26,  90, "Spanish", 3,  240,  2200,  20, "Surviving"),
        new City( 5, "CARACAS",       125, 173, "Spanish", 2,  250,  3100,  10, "Surviving"),
        new City( 6, "CARTAGENA",      89, 178, "Spanish", 4,  350,  4100,  60, "Prospering"),
        new City( 7, "CUMANA",        137, 172, "Spanish", 2,  160,  2200,   8, "Surviving"),
        new City( 8, "CURACAO",       116, 159, "Dutch",   3,  160,  2100,  20, "Prospering"),
        new City( 9, "ELEUTHERA",      85,  50, "English", 0,   40,   800,   2, "Struggling"),
        new City(10, "GRAN GRANDA",    44, 160, "Spanish", 0,  180,  1900,  25, "Surviving"),
        new City(11, "GUADELOUPE",    146, 121, "French",  3,  200,  2400,  10, "Prospering"),
        new City(12, "HAVANA",         60,  64, "Spanish", 4,  400,  6100,  60, "Wealthy"),
        new City(13, "LEOGANE",       100, 104, "French",  2,  110,  1900,   3, "Surviving"),
        new City(14, "MARACAIBO",     105, 174, "Spanish", 3,  150,  1600,   0, "Surviving"),
        new City(15, "MARGARITA",     139, 167, "Spanish", 1,   60,  1000,   4, "Surviving"),
        new City(16, "MARTINIQUE",    149, 134, "French",  3,  200,  2400,  10, "Prospering"),
        new City(17, "MONTSERRAT",    144, 116, "English", 1,   60,  1600,   0, "Surviving"),
        new City(18, "NASSAU",         80,  47, "English", 1,   50,   800,   4, "Struggling"),
        new City(19, "NEVIS",         142, 113, "English", 1,  100,  1900,   2, "Surviving"),
        new City(20, "PANAMA",         71, 189, "Spanish", 2,  450,  5100,  50, "Prospering"),
        new City(21, "PETIT GOAVE",    99, 106, "French",  0,  100,  1600,   4, "Surviving"),
        new City(22, "PORT-DE-PAIX",   99,  93, "French",  2,  150,  2100,  10, "Surviving"),
        new City(23, "PORT ROYALE",    83, 110, "English", 2,  120,  2600,  22, "Prospering"),
        new City(24, "PR.PRINCIPE",    78,  79, "Spanish", 2,  220,  3100,  10, "Prospering"),
        new City(25, "PUERTO BELLO",   70, 184, "Spanish", 2,  250,  2100,  10, "Surviving"),
        new City(26, "RIO DE HACHA",   99, 166, "Spanish", 1,  120,  1900,   4, "Surviving"),
        new City(27, "SAN JUAN",      127, 103, "Spanish", 3,  260,  2400,   4, "Surviving"),
        new City(28, "SANT.DOMINGO",  111, 104, "Spanish", 3,  300,  3100,   8, "Surviving"),
        new City(29, "SANTA MARTA",    94, 168, "Spanish", 1,   70,  1300,   0, "Struggling"),
        new City(30, "SANTIAGO",       86,  91, "Spanish", 2,  300,  4100,  35, "Prospering"),
        new City(31, "ST.AUGUSTINE",   65,   4, "Spanish", 2,   90,  1200,   1, "Struggling"),
        new City(32, "ST.EUSTATIUS",  141, 109, "Dutch",   1,   60,  1500,  10, "Surviving"),
        new City(33, "ST.KITTS",      141, 111, "English", 1,  120,  2500,   8, "Prospering"),
        new City(34, "ST.MARTIN",     140, 105, "Dutch",   1,   50,  1300,   4, "Surviving"),
        new City(35, "TORTUGA",        99,  91, "French",  1,   80,  1600,  10, "Struggling"),
        new City(36, "TRINIDAD",      149, 172, "Spanish", 0,   50,  1100,   0, "Surviving"),
        new City(37, "VERA CRUZ",       4,  92, "Spanish", 4,  280,  2700,  35, "Surviving"),
        new City(38, "VILLAHERMOSA",   17, 104, "Spanish", 0,  140,  1600,  15, "Surviving"),
        new City(39, "FLORIDA CHNL",   67,  51, "Spanish", 0,    0,   100,   0, "Prospering"),
        new City(40, "FLORIDA CHNL",   71,  33, "Spanish", 0,    0,   100,   0, "Surviving"),
    };

    /// <summary>Every era's table, indexed 0-5 in the same order as <see cref="EraYears"/>.</summary>
    public static readonly IReadOnlyList<IReadOnlyList<City>> ByEra = new[]
    { Era1560, Era1600, Era1620, Era1640, Era1660, Era1680 };

    /// <summary>The settlements of one era, or an empty list if <paramref name="era"/> is out of range.</summary>
    public static IReadOnlyList<City> ForEra(int era) =>
        era >= 0 && era < ByEra.Count ? ByEra[era] : Array.Empty<City>();

    /// <summary>Era index (0-5) for a start year, or -1 if it is not one of the six.</summary>
    public static int EraForYear(int year) => EraYears.ToList().IndexOf(year);
}
