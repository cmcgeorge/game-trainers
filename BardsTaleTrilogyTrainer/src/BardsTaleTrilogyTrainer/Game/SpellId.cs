namespace BardsTaleTrilogyTrainer.Game;

/// <summary>
/// The game's own <c>BardsTale.Spell</c> enumeration, extracted from
/// <c>global-metadata.dat</c>. These are the values the game stores in a character's
/// <c>m_learntSpells</c> list and looks spells up by, so they are what the trainer has to
/// write to grant a spell outright.
///
/// <para>Deliberately not a curated list: it is the whole enum, in value order, including the
/// item, breath, ranged-attack and monster-ability entries that share the numbering. A spell's
/// four-letter code, school and level are <em>not</em> here — the game keeps those in
/// serialized <c>SpellDescription</c> assets, so <see cref="SpellCatalog"/> reads them out of
/// the running game instead of guessing.</para>
/// </summary>
public enum SpellId
{
    MageFlame = 0,                     ArcFire = 1,                       SorcererShield = 2,                TrapZap = 3,
    FreezeFoes = 4,                    KielsCompass = 5,                  Battleskill = 6,                   WordOfHealing = 7,
    Magestar = 8,                      LesserRevelation = 9,              Levitation = 10,                   Warstrike = 11,
    InstantWolf = 12,                  FleshRestore = 13,                 PoisonStrike = 14,                 GreaterRevelation = 15,
    WrathOfValhalla = 16,              ShockSphere = 17,                  InstantOgre = 18,                  MajorLevitation = 19,
    FleshAnew = 20,                    ApportArcane = 21,                 FarFoe = 22,                       InstantSlayer = 23,
    Regeneration = 24,                 VorpalPlating = 25,                AirArmor = 26,                     Steelight = 27,
    ScrySight = 28,                    QuickFix = 29,                     HolyWater = 30,                    WitherStrike = 31,
    MageGauntlets = 32,                AreaEnchant = 33,                  MysticShield = 34,                 OgreStrength = 35,
    MithrilMight = 36,                 Starflare = 37,                    SpectreTouch = 38,                 DragonBreath = 39,
    Stonelight = 40,                   AntiMagic = 41,                    AnimatedSword = 42,                StoneTouch = 43,
    GiantStrength = 44,                PhaseDoor = 45,                    MysticalArmor = 46,                Restoration = 47,
    DeathStrike = 48,                  SafetySpell = 49,                  WizardWall = 50,                   IceStorm = 51,
    StoneToFlesh = 52,                 MindJab = 53,                      PhaseBlur = 54,                    LocateTraps = 55,
    HypnoticImage = 56,                Disbelieve = 57,                   TargetDummy = 58,                  MindFist = 59,
    WordOfFear = 60,                   WindWolf = 61,                     VanishingSpell = 62,               SecondSight = 63,
    CurseWithFear = 64,                CatEyes = 65,                      WindWarrior = 66,                  Invisibility = 67,
    WindOgre = 68,                     DisruptIllusion = 69,              MindBlade = 70,                    WindDragon = 71,
    MindWarp = 72,                     WindGiant = 73,                    SorcererSight = 74,                MageMaelstrom = 75,
    WindHero = 76,                     WindMage = 77,                     DreamSpell = 78,                   Preclusion = 79,
    Rimefang = 80,                     SummonDead = 81,                   RepelDead = 82,                    ForceFocus = 83,
    SummonElement = 84,                DemonBane = 85,                    LesserSummon = 86,                 GateWraith = 87,
    PrimeSummoning = 88,               Dispossess = 89,                   SummonPhantom = 90,                FlameColumn = 91,
    AnimateDead = 92,                  SummonHerb = 93,                   DemonStrike = 94,                  SpellBind = 95,
    SoulWhip = 96,                     BeyondDeath = 97,                  SpellSpirit = 98,                  WizardWar = 99,
    GreaterSummon = 100,               Haltfoe = 101,                     MeleeMen = 102,                    Batchspell = 103,
    Camaraderie = 104,                 NightLance = 105,                  HealAll = 106,                     BrothersKringle = 107,
    MangarsMallet = 108,               Vitality = 109,                    TeleportArbo = 110,                TeleportEnik = 111,
    Witherfist = 112,                  FrostForce = 113,                  TeleportGeli = 114,                TeleportEcul = 115,
    GodFire = 116,                     Stun = 117,                        TeleportLuce = 118,                TeleportIleg = 119,
    LuckChant = 120,                   FarDeath = 121,                    TeleportKine = 122,                TeleportObra = 123,
    Identify = 124,                    Youth = 125,                       TeleportOluk = 126,                TeleportEcea = 127,
    GraveRobber = 128,                 ForceOfTarjan = 129,               TeleportAece = 130,                TeleportKulo = 131,
    ShadowShield = 132,                FatalFist = 133,                   TeleportEvil = 134,                TeleportLive = 135,
    EarthDagger = 136,                 EarthSong = 137,                   EarthWard = 138,                   Trebuchet = 139,
    EarthElemental = 140,              WallWarp = 141,                    Petrify = 142,                     RoscoesAlert = 143,
    SuccorSong = 144,                  Sandstorm = 145,                   Sanctuary = 146,                   GlacierStrike = 147,
    Pathfinder = 148,                  MagmaBlast = 149,                  JoltBolt = 150,                    EarthMaw = 151,
    GillesGills = 152,                 DivineIntervention = 153,          Gotterdamurung = 154,              Torch = 155,
    Lamp = 156,                        FireHorn = 157,                    FrostHorn = 158,                   FigurineSamurai = 159,
    FigurineOgre = 160,                FigurineGiant = 161,               FigurineGolem = 162,               FigurineTitan = 163,
    FigurineDragon = 164,              FigurineMage = 165,                FigurineMongo = 166,               FigurineLich = 167,
    FigurineThor = 168,                FigurineOldMan = 169,              DragonShield = 170,                DragonWand = 171,
    FlameHorn = 172,                   Doppleganger = 173,                Breath79 = 174,                    Breath80 = 175,
    Breath81 = 176,                    Breath82 = 177,                    Breath83 = 178,                    Breath84 = 179,
    Breath85 = 180,                    Breath86 = 181,                    Breath87 = 182,                    Breath88 = 183,
    Breath89 = 184,                    Breath90 = 185,                    Breath91 = 186,                    Breath92 = 187,
    Breath93 = 188,                    Breath94 = 189,                    Breath95 = 190,                    Ranged00Arrow = 191,
    Ranged01Spear = 192,               Ranged02WarAxe = 193,              Ranged03Shuriken = 194,            Ranged04SpellSpear = 195,
    Ranged05MithrilArrow = 196,        Ranged06Boomerang = 197,           Ranged07Shuriken = 198,            Ranged08AgsArrow = 199,
    Ranged09ThorsHammer = 200,         Ranged10ZenArrow = 201,            Ranged11SongAxe = 202,             Ranged12SwordOfZar = 203,
    Ranged13AramsKnife = 204,          Ranged14ThievesDart = 205,         Ranged15DeadlyRang = 206,          ColdHorn = 230,
    Sceptre = 231,                     FigurineMolten = 232,              FigurineBulldozer = 233,           FigurineSlayer = 234,
    FigurineVan = 235,                 FigurineHerb = 236,                DorkRing = 237,                    BlastHorn = 238,
    ShockHorn = 239,                   MoltenHorn = 240,                  RestoreMana = 241,                 BreathBlast200 = 242,
    FigurineMasterMage = 243,          SummonHelp = 244,                  Wine = 245,                        Acorn = 246,
    Item = 247,                        Ranged = 248,                      Summon = 249,                      Defend = 250,
    Breath = 251,                      BlackWizard = 252,                 Tarjan = 253,                      NONE = 255,
    RangedKielsAxe = 256,              RangedMithAxe = 257,               RangedDeathStars = 258,            RangedNightSpear = 259,
    RangedArrowsOfLife = 260,          RangedFireSpear = 261,             RangedLightstar = 262,             RangedStealthArrows = 263,
    RangedBlackArrows = 264,           RangedLifeThiefDart = 265,         RangedHolyMissile = 266,           HornOfDeath = 267,
    CliLyre = 268,                     HornOfGods = 269,                  FigurineDeath = 270,               FigurineFamiliar = 271,
    MAX = 272,
}

/// <summary>
/// The spells the trilogy never grants through a school level. Their
/// <c>SpellDescription.m_level</c> is 0, which makes <c>Character.KnowsSpell</c> skip the
/// school-level test entirely, so the only way to hold one is for it to be in
/// <c>m_learntSpells</c>. The game itself only ever puts them there from a map script, a
/// Review Board purchase, or a chapter's quest-spell grant.
/// </summary>
public static class SpecialSpells
{
    /// <summary>One cross-game spell, with the code the game displays for it.</summary>
    public sealed record Entry(SpellId Id, string Code, string Name, string Note);

    public static readonly IReadOnlyList<Entry> All = new[]
    {
        new Entry(SpellId.DreamSpell, "ZZGO", "Dream Spell",
            "BT2. Gates the party to a dungeon level chosen from the chapter's dream-spell table."),
        new Entry(SpellId.Gotterdamurung, "NUKE", "Gotterdammerung",
            "BT3. The trilogy's heaviest damage spell."),
        new Entry(SpellId.GillesGills, "GILL", "Gilles Gills",
            "BT3. Lets the party breathe under water."),
        new Entry(SpellId.DivineIntervention, "DIVA", "Divine Intervention",
            "BT2. Restores the party when everything else has failed."),
    };

    /// <summary>Looks up a cross-game spell by its four-letter code, case-insensitively.</summary>
    public static Entry? FindByCode(string code) =>
        All.FirstOrDefault(e => string.Equals(e.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
}
