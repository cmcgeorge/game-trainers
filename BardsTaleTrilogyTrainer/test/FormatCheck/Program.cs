using System.Text;
using BardsTaleTrilogyTrainer.Game;
using BardsTaleTrilogyTrainer.Memory;
using BardsTaleTrilogyTrainer.ViewModels;

namespace BardsTaleTrilogyTrainer.FormatCheck;

/// <summary>
/// Headless verification harness: no GUI, no running game. It asserts the reverse-engineered
/// constants against each other, the map catalogue against itself, and every memory path
/// (locate, read the party's position, teleport) against a synthetic IL2CPP heap built here.
/// </summary>
public static class Program
{
    private static int _failures;
    private static int _checks;

    public static int Main()
    {
        Console.WriteLine("BardsTaleTrilogyTrainer FormatCheck");
        Console.WriteLine("===================================");

        CheckGameFacts();
        CheckCharacterFormat();
        CheckMapFormat();
        CheckMapBook();
        CheckMapFileParser();
        CheckClassBook();
        CheckSpellbook();
        CheckSpellIds();
        CheckSpellCatalog();
        CheckLearntSpells();
        CheckRemoteStub();
        CheckItemBook();
        CheckFakeMemorySource();
        CheckIl2CppHelpers();
        CheckCharacterRecordRoundTrip();
        CheckSpellLevelRowsSurviveJunk();
        CheckClassLocatorAndNavigator();
        CheckGameLocatorStructuralScan();
        CheckMapArchive();

        Console.WriteLine($"\n{_checks} checks, {_failures} failures.");
        return _failures > 0 ? 1 : 0;
    }

    private static void Check(string label, bool ok)
    {
        _checks++;
        if (!ok) _failures++;
        Console.WriteLine($"  {(ok ? "PASS" : "FAIL")} {label}");
    }

    // ================================ constants =================================
    private static void CheckGameFacts()
    {
        Console.WriteLine("\n--- GameFacts ---");
        Check("ProcessName is TheBardsTaleTrilogy", GameFacts.ProcessName == "TheBardsTaleTrilogy");
        Check("GameModuleName is GameAssembly.dll", GameFacts.GameModuleName == "GameAssembly.dll");
        Check("GameNamespace is BardsTale", GameFacts.GameNamespace == "BardsTale");
        Check("PartySlots is 7 (Party.MaxSlots)", GameFacts.PartySlots == 7);
        Check("CharacterInventorySize is 16", GameFacts.CharacterInventorySize == 16);
        Check("PartyInventorySize is 40", GameFacts.PartyInventorySize == 40);
        Check("class slot RVAs are distinct", new[]
        {
            GameFacts.PartyClassRva, GameFacts.PlayerClassRva,
            GameFacts.GlobalMapsClassRva, GameFacts.TeleportTargetClassRva, GameFacts.AutomapClassRva,
        }.Distinct().Count() == 5);
        Check("class slot RVAs are 8-byte aligned", new[]
        {
            GameFacts.PartyClassRva, GameFacts.PlayerClassRva,
            GameFacts.GlobalMapsClassRva, GameFacts.TeleportTargetClassRva, GameFacts.AutomapClassRva,
        }.All(rva => rva % 8 == 0));
    }

    private static void CheckCharacterFormat()
    {
        Console.WriteLine("\n--- CharacterFormat (offsets from the game's own metadata) ---");
        // These three are the ones the community had already confirmed by other means; they
        // fall out of the metadata unchanged, which is what anchors the rest of the table.
        Check("OffExperience is 0x50 [Confirmed]", CharacterFormat.OffExperience == 0x50);
        Check("OffHpCur is 0x84 [Confirmed]", CharacterFormat.OffHpCur == 0x84);
        Check("OffSpCur is 0x8C [Confirmed]", CharacterFormat.OffSpCur == 0x8C);
        Check("PartyGold is 0x68 [Confirmed]", CharacterFormat.PartyGold == 0x68);

        Check("OffRace is 0x34", CharacterFormat.OffRace == 0x34);
        Check("OffClass is 0x38", CharacterFormat.OffClass == 0x38);
        Check("OffLevel is 0x7C", CharacterFormat.OffLevel == 0x7C);
        Check("OffHpMax sits 4 before OffHpCur", CharacterFormat.OffHpMax + 4 == CharacterFormat.OffHpCur);
        Check("OffSpMax sits 4 before OffSpCur", CharacterFormat.OffSpMax + 4 == CharacterFormat.OffSpCur);
        Check("attributes are five consecutive ints",
            CharacterFormat.OffIntelligence == CharacterFormat.OffStrength + 4 &&
            CharacterFormat.OffDexterity == CharacterFormat.OffStrength + 8 &&
            CharacterFormat.OffConstitution == CharacterFormat.OffStrength + 12 &&
            CharacterFormat.OffLuck == CharacterFormat.OffStrength + 16);
        Check("OffSpellLevels is 0xD0", CharacterFormat.OffSpellLevels == 0xD0);
        Check("SpellLevelSlots is 16 (one per class id)", CharacterFormat.SpellLevelSlots == 16);
        Check("ItemCharges is 0x24", CharacterFormat.ItemCharges == 0x24);
        Check("ObjectSize is 0x108", CharacterFormat.ObjectSize == 0x108);
        Check("every offset lies inside the object",
            new[]
            {
                CharacterFormat.OffName, CharacterFormat.OffRace, CharacterFormat.OffClass,
                CharacterFormat.OffExperience, CharacterFormat.OffGold, CharacterFormat.OffLevel,
                CharacterFormat.OffHpMax, CharacterFormat.OffHpCur, CharacterFormat.OffSpMax,
                CharacterFormat.OffSpCur, CharacterFormat.OffCondition, CharacterFormat.OffSpellLevels,
                CharacterFormat.OffInventory, CharacterFormat.OffInitialClass,
            }.All(o => o >= Il2Cpp.ObjectHeaderSize && o < CharacterFormat.ObjectSize));

        Check("16 class names (Warrior..NPC)", CharacterFormat.Classes.Length == 16);
        Check("ClassName(0) is Warrior", CharacterFormat.ClassName(0) == "Warrior");
        Check("ClassName(12) is Geomancer", CharacterFormat.ClassName(12) == "Geomancer");
        Check("Races has 7 entries", CharacterFormat.Races.Length == 7);
        Check("RaceName(6) is Gnome", CharacterFormat.RaceName(6) == "Gnome");
        Check("Conditions has 9 entries", CharacterFormat.Conditions.Length == 9);
        Check("ConditionName(3) is Dead", CharacterFormat.ConditionName(3) == "Dead");
        Check("CasterClasses covers ids 6..12",
            CharacterFormat.CasterClasses.Select(c => c.ClassId).SequenceEqual(new[] { 6, 7, 8, 9, 10, 11, 12 }));

        var buf = ValidCharacterBytes();
        Check("LooksLikeCharacter accepts valid data", CharacterFormat.LooksLikeCharacter(buf));

        Check("rejects negative experience", !Mutated(buf, CharacterFormat.OffExperience, -1L));
        Check("rejects an implausible race", !Mutated(buf, CharacterFormat.OffRace, 99));
        Check("rejects a class beyond the enum", !Mutated(buf, CharacterFormat.OffClass, 16));
        Check("accepts class 12 (Geomancer)", Mutated(buf, CharacterFormat.OffClass, 12));
        Check("rejects HP above HP max", !Mutated(buf, CharacterFormat.OffHpCur, 999));
        Check("rejects SP above SP max", !Mutated(buf, CharacterFormat.OffSpCur, 999));
        Check("rejects an unknown condition", !Mutated(buf, CharacterFormat.OffCondition, 42));
        Check("rejects a buffer shorter than the object", !CharacterFormat.LooksLikeCharacter(new byte[0x80]));
    }

    private static void CheckMapFormat()
    {
        Console.WriteLine("\n--- MapFormat ---");
        Check("Player.m_gridX is 0xEC", MapFormat.PlayerGridX == 0xEC);
        Check("Player.m_gridZ follows m_gridX", MapFormat.PlayerGridZ == MapFormat.PlayerGridX + 4);
        Check("Player.m_facing sits before m_gridX", MapFormat.PlayerFacing + 4 == MapFormat.PlayerGridX);
        Check("Player.m_queueTeleport is 0x68", MapFormat.PlayerQueueTeleport == 0x68);
        Check("GameMap.m_mapIdx is 0x19C", MapFormat.GameMapIndex == 0x19C);
        Check("GameMap.m_isDungeonMap sits before m_mapIdx",
            MapFormat.GameMapIsDungeon + 4 == MapFormat.GameMapIndex);
        Check("GameMap.m_height follows m_width", MapFormat.GameMapHeight == MapFormat.GameMapWidth + 4);
        Check("TeleportTarget fields are inside the object",
            new[]
            {
                MapFormat.TeleportIsValid, MapFormat.TeleportIsDungeon, MapFormat.TeleportMap,
                MapFormat.TeleportX, MapFormat.TeleportZ, MapFormat.TeleportFacing,
                MapFormat.TeleportKind, MapFormat.TeleportDone, MapFormat.TeleportPostJournal,
            }.All(o => o >= Il2Cpp.ObjectHeaderSize && o < MapFormat.TeleportTargetSize));
        Check("GlobalMaps statics: Instance then chapter",
            MapFormat.GlobalMapsInstanceStatic == 0 && MapFormat.GlobalMapsChapterStatic == 8);
        Check("facing names", MapFormat.FacingName(0) == "North" && MapFormat.FacingName(3) == "West");
        Check("north steps +Z, east steps +X",
            MapFormat.Step(Facing.North) == (0, 1) && MapFormat.Step(Facing.East) == (1, 0));
        Check("south and west are the opposites",
            MapFormat.Step(Facing.South) == (0, -1) && MapFormat.Step(Facing.West) == (-1, 0));
    }

    // ================================ map data ==================================
    private static void CheckMapBook()
    {
        Console.WriteLine("\n--- MapBook (extracted from the game's GlobalMaps objects) ---");
        Check("121 maps in total", MapBook.Maps.Count == 121);
        Check("BT1 has 17 maps (1 city, 16 dungeons)",
            MapBook.ForChapter(GameChapter.TalesOfTheUnknown).Count() == 17);
        Check("BT2 has 33 maps (7 cities, 26 dungeons)",
            MapBook.ForChapter(GameChapter.DestinyKnight).Count() == 33);
        Check("BT3 has 71 maps (10 cities, 61 dungeons)",
            MapBook.ForChapter(GameChapter.ThiefOfFate).Count() == 71);

        Check("(chapter, kind, index) is unique",
            MapBook.Maps.Select(m => (m.Chapter, m.IsDungeon, m.Index)).Distinct().Count() == MapBook.Maps.Count);
        Check("indices run 0..n-1 within each chapter and kind",
            MapBook.Maps.GroupBy(m => (m.Chapter, m.IsDungeon))
                        .All(g => g.Select(m => m.Index).OrderBy(i => i).SequenceEqual(Enumerable.Range(0, g.Count()))));
        Check("asset names are unique", MapBook.Maps.Select(m => m.Asset).Distinct().Count() == MapBook.Maps.Count);
        Check("asset names follow map_bt<n>_{city|dung}NN_",
            MapBook.Maps.All(m => m.Asset.StartsWith(
                $"map_bt{(int)m.Chapter + 1}_{(m.IsDungeon ? "dung" : "city")}{m.Index:D2}_", StringComparison.Ordinal)));
        Check("every map has a positive grid size", MapBook.Maps.All(m => m.Width > 0 && m.Height > 0));
        // Multi-level areas share one entry point across their floors, so it can fall outside a
        // smaller floor. Ice Dungeon Lv2 (5x5, entry 2,8) is the game's only such case.
        Check("entry points are non-negative", MapBook.Maps.All(m => m.EntryX >= 0 && m.EntryZ >= 0));
        Check("at most one map's entry point falls outside its own floor",
            MapBook.Maps.Count(m => m.EntryX >= m.Width || m.EntryZ >= m.Height) == 1);
        Check("no map is larger than 64 squares a side", MapBook.Maps.All(m => m.Width <= 64 && m.Height <= 64));

        var skara = MapBook.Find(GameChapter.TalesOfTheUnknown, isDungeon: false, index: 0);
        Check("BT1 city 0 is Skara Brae, 30x30",
            skara is { Name: "Skara Brae", Width: 30, Height: 30 });
        var cellars = MapBook.Find(GameChapter.TalesOfTheUnknown, isDungeon: true, index: 0);
        Check("BT1 dungeon 0 is the Cellars, 22x22 and wrapping",
            cellars is { Name: "Cellars", Width: 22, Height: 22, WrapsAround: true });
        var mangar = MapBook.Find(GameChapter.TalesOfTheUnknown, isDungeon: true, index: 15);
        Check("Mangar's Tower level 5 is a tower on floor index 4",
            mangar is { IsTower: true, Level: 4 } && mangar.Group == "Mangars Tower");
        Check("BT2 city 0 is The Forest, the 32x48 wilderness",
            MapBook.Find(GameChapter.DestinyKnight, false, 0) is
                { Name: "The Forest", Width: 32, Height: 48, IsWilderness: true });
        Check("BT3 dungeon 60 is Tarjan, the last map of the trilogy",
            MapBook.Find(GameChapter.ThiefOfFate, true, 60) is { Name: "Tarjan" });

        Check("grouping strips the level suffix",
            MapBook.Maps.Count(m => m.Group == "Mangars Tower") == 5);
        Check("categories split by chapter and kind",
            MapBook.Maps.Select(m => m.Category).Distinct().Count() == 6);

        Console.WriteLine("\n--- MapBook: dream-spell targets and start points ---");
        Check("7 ZZGO destinations", MapBook.DreamSpellTargets.Count == 7);
        // The spell drops the party at the dungeon's entrance out in the world, so each entry
        // names a city or wilderness square rather than a square inside the dungeon.
        Check("every dream target names a real BT2 city map",
            MapBook.DreamSpellTargets.All(t => MapBook.Find(GameChapter.DestinyKnight, false, t.Map) != null));
        Check("every dream target lands inside that city map",
            MapBook.DreamSpellTargets.All(t =>
            {
                var m = MapBook.Find(GameChapter.DestinyKnight, false, t.Map)!;
                return t.X >= 0 && t.X < m.Width && t.Z >= 0 && t.Z < m.Height;
            }));
        Check("dream targets agree with the dungeons' own parent squares",
            MapBook.DreamSpellTargets.Any(t => t.Name == "Maze of Dread" && t.Map == 6 && t.X == 11 && t.Z == 14) &&
            MapBook.Maps.Any(m => m.Name == "Maze of Dread Lv1" && m.ParentMap == 6 && m.ParentX == 11 && m.ParentZ == 14));
        // …but only six of the seven do, and the exception is not a transcription slip: the
        // game's own location script for Fanskar's Castle sits at (17,27), one north of the
        // castle's parent link at (17,26). Pinned because "they all match the parent link" is
        // the plausible-sounding generalisation that would otherwise get this entry "fixed"
        // into being wrong. The archive check below proves the (17,27) reading against the
        // installed game.
        Check("Fanskar's Castle is the one dream target its dungeon's parent link disagrees with",
            MapBook.DreamSpellTargets.Count(t =>
                !MapBook.Maps.Any(m => m.Chapter == GameChapter.DestinyKnight && m.IsDungeon &&
                                       m.Level == 0 && m.ParentMap == t.Map &&
                                       m.ParentX == t.X && m.ParentZ == t.Z)) == 1 &&
            MapBook.DreamSpellTargets.Any(t => t.Name == "Fanskar's Castle" && t.Map == 0 && t.X == 17 && t.Z == 27) &&
            MapBook.Maps.Any(m => m.Name == "The Castle" && m.ParentMap == 0 && m.ParentX == 17 && m.ParentZ == 26));
        Check("the Destiny Stone is one of them",
            MapBook.DreamSpellTargets.Any(t => t.Name == "Destiny Stone"));

        Check("a new-game start is known for all three chapters", MapBook.NewGameLocations.Count == 3);
        Check("every start point lands inside its map",
            MapBook.NewGameLocations.All(kv =>
            {
                var m = MapBook.Find(kv.Key, kv.Value.IsDungeon, kv.Value.Map);
                return m != null && kv.Value.X < m.Width && kv.Value.Z < m.Height;
            }));
        Check("BT1 starts in Skara Brae at 24,15 facing west",
            MapBook.NewGameLocations[GameChapter.TalesOfTheUnknown] == (false, 0, 24, 15, Facing.West));

        Check("chapter names", MapBook.ChapterName(GameChapter.DestinyKnight) == "The Destiny Knight");
        Check("chapter tags", MapBook.ChapterTag(GameChapter.ThiefOfFate) == "BT3");
    }

    /// <summary>Parses a miniature map file in the game's own text format.</summary>
    private static void CheckMapFileParser()
    {
        Console.WriteLine("\n--- MapFileParser ---");
        const string text = """
            name=SCRIPTSTRING_0001
            isDungeon=1
            width=3
            height=2
            isTower=1
            isOutside=0
            wrapAroundEnable=1
            level=2
            map
              0,0:Door,Solid,Solid,Solid, RandomCombat
              1,0:None,SolidNoPHDO,Solid,None, StairsOut, Darkness
              2,0:Solid,Solid,Solid,None
              0,1:SecretDoor,None,Door,Solid, Spinner
              1,1:LockedDoor,None,None,None
              2,1:Solid,Solid,None,None, HarmParty
            locationScript=1,0,L100
            scripts
            L100
                @StairsOut
            """;

        var grid = MapFileParser.Parse(text.Replace("\n", "\r\n"));
        Check("header size is read", grid is { Width: 3, Height: 2 });
        Check("dungeon, tower, wrapping and level are read",
            grid is { IsDungeon: true, IsTower: true, WrapsAround: true, Level: 2 });
        Check("walls decode per side",
            grid[0, 0].North == WallKind.Door && grid[0, 0].East == WallKind.Solid);
        Check("the NoPHDO suffix folds into the base wall kind",
            grid[1, 0].East == WallKind.Solid);
        Check("secret and locked doors keep their own kind",
            grid[0, 1].North == WallKind.SecretDoor && grid[1, 1].North == WallKind.LockedDoor);
        Check("cell flags decode",
            grid[1, 0].Flags.HasFlag(CellFlags.StairsOut) && grid[1, 0].Flags.HasFlag(CellFlags.Darkness));
        Check("several flags combine on one cell",
            grid[1, 0].Flags == (CellFlags.StairsOut | CellFlags.Darkness));
        Check("HasStairs sees the stairs", grid[1, 0].HasStairs && !grid[0, 0].HasStairs);
        Check("location scripts are indexed by square",
            grid.LocationScripts.TryGetValue((1, 0), out var label) && label == "L100");
        Check("out-of-range squares read as empty", grid[9, 9] == MapCell.Empty);
        Check("Contains bounds the grid", grid.Contains(2, 1) && !grid.Contains(3, 1) && !grid.Contains(-1, 0));

        const string city = """
            name=SCRIPTSTRING_0002
            isDungeon=0
            width=2
            height=2
            isOutside=1
            level=0
            map
              0,0:0,2,69,Generic,Passable
              1,0:14,4,74,Tavern,Passable,KAP=1
              0,1:1,0,255,None,Blocked
              1,1:17,2,75,Temple,Passable,Face=North
            scripts
            """;

        var town = MapFileParser.Parse(city.Replace("\n", "\r\n"));
        Check("city maps decode their modules",
            town[1, 0].Module == CityModule.Tavern && town[1, 1].Module == CityModule.Temple);
        Check("blocked squares are marked", town[0, 1].IsBlocked && !town[0, 0].IsBlocked);
        Check("parameterised tokens are not mistaken for flags",
            !town[1, 0].Flags.HasFlag(CellFlags.Blocked) && town[1, 0].Flags.HasFlag(CellFlags.Passable));
        Check("module labels are short enough to draw",
            MapFileParser.ModuleLabel(CityModule.Tavern) == "TAV" &&
            MapFileParser.ModuleLabel(CityModule.Generic) == null);

        Check("a file with no map section is rejected", Throws(() => MapFileParser.Parse("width=3\n")));
        Check("an absurd size is rejected", Throws(() => MapFileParser.Parse("width=99999\nheight=99999\nmap\n")));
    }

    // ================================ books =====================================
    private static void CheckClassBook()
    {
        Console.WriteLine("\n--- ClassBook ---");
        Check("13 playable classes", ClassBook.Classes.Count == 13);
        Check("MaxPlayableClassId is 12", ClassBook.MaxPlayableClassId == 12);
        Check("ids are sequential", ClassBook.Classes.Select((c, i) => c.Id == i).All(ok => ok));
        Check("names agree with CharacterFormat",
            ClassBook.Classes.Select(c => c.Name).SequenceEqual(CharacterFormat.Classes.Take(13)));
        Check("arts agree with Spellbook.ArtForClass",
            ClassBook.Classes.All(c => c.Art == Spellbook.ArtForClass(c.Id)));
        Check("casting classes are exactly ids 6..12",
            ClassBook.CastingClasses.Select(c => c.Id).SequenceEqual(Enumerable.Range(6, 7)));
        Check("class id round-trips through Spellbook",
            ClassBook.CastingClasses.All(c => Spellbook.ClassIdFor(c.Art) == c.Id));
        Check("non-schools have no class id",
            Spellbook.ClassIdFor(SpellClass.AnyMagicUser) == -1 && Spellbook.ClassIdFor(SpellClass.None) == -1);

        // UpgradeMage: Mathf.Min(7, (level + 1) / 2)
        Check("SpellLevelForLevel(1) is 1", ClassBook.SpellLevelForLevel(1) == 1);
        Check("SpellLevelForLevel(3) is 2", ClassBook.SpellLevelForLevel(3) == 2);
        Check("SpellLevelForLevel(13) is 7", ClassBook.SpellLevelForLevel(13) == 7);
        Check("SpellLevelForLevel(99) caps at 7", ClassBook.SpellLevelForLevel(99) == 7);
        Check("LevelForSpellLevel inverts it",
            Enumerable.Range(1, 7).All(sl => ClassBook.SpellLevelForLevel(ClassBook.LevelForSpellLevel(sl)) == sl));
        Check("MaxSpellLevel agrees with CharacterFormat",
            ClassBook.MaxSpellLevel == CharacterFormat.MaxSpellLevel);

        Check("MeleeAttacks(1) is 1 and (5) is 2",
            ClassBook.MeleeAttacks(1) == 1 && ClassBook.MeleeAttacks(5) == 2);
        Check("MonkUnarmedDamage is monotonic", Enumerable.Range(1, 98)
            .All(l => ClassBook.MonkUnarmedDamage(l) <= ClassBook.MonkUnarmedDamage(l + 1)));
        Check("ScoreAsPercent maps 0/128/255 to 0/50/100%",
            ClassBook.ScoreAsPercent(0) == "0%" && ClassBook.ScoreAsPercent(128) == "50%" &&
            ClassBook.ScoreAsPercent(255) == "100%");

        int[] none = new int[CharacterFormat.SpellLevelSlots];
        Check("a Warrior may become a Conjurer", ClassBook.CanChangeTo(0, 6, 5, none).Allowed);
        Check("changing to the class already held is refused", !ClassBook.CanChangeTo(4, 4, 5, none).Allowed);
        Check("an unplayable class id is refused", !ClassBook.CanChangeTo(0, 13, 5, none).Allowed);
        Check("Sorcerer needs one school at 3", !ClassBook.CanChangeTo(6, 8, 9, none).Allowed);

        var oneSchool = new int[CharacterFormat.SpellLevelSlots];
        oneSchool[6] = 3;
        Check("Sorcerer allowed once one school reaches 3", ClassBook.CanChangeTo(6, 8, 9, oneSchool).Allowed);
        Check("Wizard still needs a second school", !ClassBook.CanChangeTo(6, 9, 9, oneSchool).Allowed);

        var mastered = new int[CharacterFormat.SpellLevelSlots];
        for (int id = 6; id <= 9; id++) mastered[id] = ClassBook.MaxSpellLevel;
        Check("a magic user may not return to a school already held",
            !ClassBook.CanChangeTo(6, 7, 20, mastered).Allowed);
        Check("Chronomancer allowed with three schools mastered",
            ClassBook.CanChangeTo(6, 11, 20, mastered).Allowed);
        Check("Geomancer is refused to a caster", !ClassBook.CanChangeTo(6, 12, 20, mastered).Allowed);
        Check("Geomancer is allowed to a fighter", ClassBook.CanChangeTo(0, 12, 20, none).Allowed);

        var scores = new ClassScores(2, 100, 90, 80, 70, 3, 6);
        Check("a Rogue's abilities list its three bonuses",
            ClassBook.AbilitiesFor(2, 10, 18, scores, none).Count == 3);
        Check("a caster's abilities mention its school",
            ClassBook.AbilitiesFor(6, 10, 18, scores, none).Any(a => a.Name == "Magical school"));
        Check("an unplayable class still returns something",
            ClassBook.AbilitiesFor(99, 10, 18, scores, none).Count > 0);

        // Maxing the class scores tops up what the game rolls against and refills the
        // Bard's tunes, but leaves the two fields that are counts rather than chances.
        var maxed = ClassBook.MaxAbilityScores(scores, 12);
        Check("maxing sets disarm to a certainty", maxed.DisarmTrapBonus == ClassBook.MaxAbilityScore);
        Check("maxing sets hide in shadows to a certainty", maxed.HideInShadowsBonus == ClassBook.MaxAbilityScore);
        Check("maxing sets identify to a certainty", maxed.IdentifyBonus == ClassBook.MaxAbilityScore);
        Check("maxing sets critical hit to a certainty", maxed.CriticalHit == ClassBook.MaxAbilityScore);
        Check("maxing refills tunes to the character's level", maxed.SongsRemaining == 12);
        Check("maxing leaves attacks per round alone", maxed.Attacks == scores.Attacks);
        Check("maxing leaves songs known alone", maxed.SongsKnown == scores.SongsKnown);
        Check("maxing leaves at least one tune at level 0",
            ClassBook.MaxAbilityScores(scores, 0).SongsRemaining == 1);
        Check("a maxed score reads as 100%", ClassBook.ScoreAsPercent(maxed.CriticalHit) == "100%");
    }

    private static void CheckSpellbook()
    {
        Console.WriteLine("\n--- Spellbook (school to class id) ---");
        Check("the seven casting schools map to class ids 6-12",
            Enumerable.Range(6, 7).All(id => Spellbook.ClassIdFor(Spellbook.ArtForClass(id)) == id));
        Check("non-casting classes have no school",
            new[] { 0, 1, 2, 3, 4, 5 }.All(id => Spellbook.ArtForClass(id) == SpellClass.None));
        Check("the school-free cases have no class id",
            Spellbook.ClassIdFor(SpellClass.AnyMagicUser) == -1 && Spellbook.ClassIdFor(SpellClass.None) == -1);
        Check("ArtName covers every school",
            Enum.GetValues<SpellClass>().All(c => !string.IsNullOrEmpty(Spellbook.ArtName(c))));
        Check("there are six bard songs", Spellbook.BardSongs.Length == 6);
    }

    // ============================== spell ids ===================================
    private static void CheckSpellIds()
    {
        Console.WriteLine("\n--- SpellId ---");

        // These four values are what the trainer writes into m_learntSpells; if they drift, the
        // wrong spell is granted, so they are pinned against the game's own enum.
        Check("ZZGO is DreamSpell = 78", (int)SpellId.DreamSpell == 78);
        Check("NUKE is Gotterdamurung = 154", (int)SpellId.Gotterdamurung == 154);
        Check("GILL is GillesGills = 152", (int)SpellId.GillesGills == 152);
        Check("DIVA is DivineIntervention = 153", (int)SpellId.DivineIntervention == 153);
        Check("NONE is 255 and MAX is 272", (int)SpellId.NONE == 255 && (int)SpellId.MAX == 272);
        Check("the enum carries the whole table", Enum.GetValues<SpellId>().Length == 249);
        Check("every id is distinct",
            Enum.GetValues<SpellId>().Select(v => (int)v).Distinct().Count() == 249);

        Check("the four cross-game spells are offered", SpecialSpells.All.Count == 4);
        Check("ZZGO is found by code", SpecialSpells.FindByCode("ZZGO")?.Id == SpellId.DreamSpell);
        Check("codes are matched case-insensitively and trimmed",
            SpecialSpells.FindByCode(" nuke ")?.Id == SpellId.Gotterdamurung);
        Check("an unknown code returns null", SpecialSpells.FindByCode("XXXX") == null);

        Check("readable names split the enum's camel case",
            SpellCatalog.ReadableName(SpellId.DreamSpell) == "Dream Spell");
        Check("readable names keep single words intact",
            SpellCatalog.ReadableName(SpellId.Gotterdamurung) == "Gotterdamurung");
    }

    // ============================ spell catalogue ===============================
    /// <summary>
    /// Builds a synthetic <c>GlobalSpells</c> singleton holding two descriptions — one granted by
    /// a school level, one granted only outright — and reads it back through the real code path.
    /// </summary>
    private static void CheckSpellCatalog()
    {
        Console.WriteLine("\n--- SpellCatalog ---");
        var mem = new FakeMemorySource();

        const nuint klass = 0x40000, statics = 0x40100, instance = 0x40200;
        const nuint table = 0x40300, normal = 0x40400, special = 0x40500;
        const nuint normalCode = 0x40600, specialCode = 0x40700;

        foreach (var (at, size) in new (nuint, int)[]
                 {
                     (klass, 0x100), (statics, 0x40), (instance, 0x40), (table, 0x60),
                     (normal, 0xC0), (special, 0xC0), (normalCode, 0x40), (specialCode, 0x40),
                 })
            mem.Map(at, new byte[size]);

        mem.WritePtr(klass + (nuint)Il2Cpp.ClassStaticFieldsOffset, statics);
        mem.WritePtr(statics + (nuint)CharacterFormat.GlobalSpellsInstanceStatic, instance);
        mem.WritePtr(instance + (nuint)CharacterFormat.GlobalSpellsByEnum, table);
        mem.WriteI32(table + Il2Cpp.ArrayLengthOffset, 2);
        mem.WritePtr(Il2Cpp.ArrayElement(table, 0), normal);
        mem.WritePtr(Il2Cpp.ArrayElement(table, 1), special);

        // A Conjurer level-3 spell.
        SyntheticWorld.ManagedString(mem, normalCode, "MAST");
        mem.WritePtr(normal + (nuint)CharacterFormat.SpellDescriptionCode, normalCode);
        mem.WriteI32(normal + (nuint)CharacterFormat.SpellDescriptionSpell, (int)SpellId.Magestar);
        mem.WriteI32(normal + (nuint)CharacterFormat.SpellDescriptionClass, 6);
        mem.WriteI32(normal + (nuint)CharacterFormat.SpellDescriptionLevel, 3);
        mem.WriteI32(normal + (nuint)CharacterFormat.SpellDescriptionCost, 4);
        mem.WriteBool(normal + (nuint)CharacterFormat.SpellDescriptionBt1, true);

        // ZZGO: level 0, so no school level can ever grant it.
        SyntheticWorld.ManagedString(mem, specialCode, "ZZGO");
        mem.WritePtr(special + (nuint)CharacterFormat.SpellDescriptionCode, specialCode);
        mem.WriteI32(special + (nuint)CharacterFormat.SpellDescriptionSpell, (int)SpellId.DreamSpell);
        mem.WriteI32(special + (nuint)CharacterFormat.SpellDescriptionClass, 6);
        mem.WriteI32(special + (nuint)CharacterFormat.SpellDescriptionLevel, 0);
        mem.WriteI32(special + (nuint)CharacterFormat.SpellDescriptionCost, 60);
        mem.WriteBool(special + (nuint)(CharacterFormat.SpellDescriptionBt1 + 1), true);

        var catalog = SpellCatalog.Read(mem, klass);
        Check("the table is read", catalog.IsLive && catalog.All.Count == 2);
        Check("a code is read from the managed string", catalog.FindByCode("MAST")?.Id == SpellId.Magestar);
        Check("codes are matched case-insensitively", catalog.FindByCode("zzgo")?.Id == SpellId.DreamSpell);
        Check("a school spell carries its school and level",
            catalog.Find(SpellId.Magestar) is { ClassId: 6, Level: 3, Cost: 4, IsSpecial: false });
        Check("a level of 0 marks a spell as grantable only outright",
            catalog.Find(SpellId.DreamSpell) is { IsSpecial: true, Cost: 60 });
        Check("the special list holds only the level-0 spell",
            catalog.Special.Count == 1 && catalog.Special[0].Id == SpellId.DreamSpell);
        Check("a school lists only its own graded spells",
            catalog.ForSchool(6).Count() == 1 && catalog.ForSchool(6).First().Id == SpellId.Magestar);
        Check("game flags are read",
            catalog.Find(SpellId.Magestar)!.Games == "BT1" && catalog.Find(SpellId.DreamSpell)!.Games == "BT2");
        Check("the source line explains how a spell is reached",
            catalog.Find(SpellId.DreamSpell)!.Source == "learnt outright");

        Check("no singleton yields an empty catalogue",
            !SpellCatalog.Read(new FakeMemorySource(), 0).IsLive);
        Check("an empty catalogue finds nothing", SpellCatalog.Empty.FindByCode("ZZGO") == null);
    }

    // ============================= learnt spells ================================
    /// <summary>
    /// Exercises the learnt-spell list — the only place the game keeps a spell no school level
    /// grants. Covers appending into spare capacity, refusing to grow without the runtime helper,
    /// removal, and the full <c>KnowsSpell</c> rule.
    /// </summary>
    private static void CheckLearntSpells()
    {
        Console.WriteLine("\n--- learnt spells (m_learntSpells) ---");
        var mem = new FakeMemorySource();

        const nuint addr = 0x50000, list = 0x51000, items = 0x51100, spellArray = 0x51200;
        mem.Map(addr, ValidCharacterBytes());
        mem.Map(list, new byte[0x40]);
        mem.Map(items, new byte[Il2Cpp.ArrayHeaderSize + 4 * 4]);
        mem.Map(spellArray, new byte[Il2Cpp.ArrayHeaderSize + CharacterFormat.SpellLevelSlots * 4]);

        // A List<Spell> holding one entry, with room for four.
        mem.WritePtr(addr + (nuint)CharacterFormat.OffLearntSpells, list);
        mem.WritePtr(list + Il2Cpp.ListItemsOffset, items);
        mem.WriteI32(list + Il2Cpp.ListSizeOffset, 1);
        mem.WriteI32(items + Il2Cpp.ArrayLengthOffset, 4);
        mem.WriteI32(items + Il2Cpp.ArrayHeaderSize, (int)SpellId.DreamSpell);

        mem.WriteI32(spellArray + Il2Cpp.ArrayLengthOffset, CharacterFormat.SpellLevelSlots);
        mem.WritePtr(addr + (nuint)CharacterFormat.OffSpellLevels, spellArray);

        var record = new CharacterRecord(mem, addr, slot: 0);

        Check("the list reads back", record.ReadLearntSpells().SequenceEqual(new[] { SpellId.DreamSpell }));
        Check("a granted spell is recognised", record.HasLearntSpell(SpellId.DreamSpell));
        Check("an ungranted spell is not", !record.HasLearntSpell(SpellId.Gotterdamurung));

        // Appending while the backing array has room needs no allocation at all.
        var grant = record.GrantSpell(SpellId.Gotterdamurung, runtime: null);
        Check("NUKE is appended in place", grant.Outcome == CharacterRecord.GrantOutcome.AppendedInPlace);
        Check("the count follows the element", mem.ReadI32(list + Il2Cpp.ListSizeOffset) == 2);
        Check("the element landed in the array",
            mem.ReadI32(items + Il2Cpp.ArrayHeaderSize + 4) == (int)SpellId.Gotterdamurung);
        Check("the version is bumped so the game re-reads the list",
            mem.ReadI32(list + Il2Cpp.ListVersionOffset) == 1);

        Check("granting twice is a no-op",
            record.GrantSpell(SpellId.Gotterdamurung, null).Outcome == CharacterRecord.GrantOutcome.AlreadyKnown
            && mem.ReadI32(list + Il2Cpp.ListSizeOffset) == 2);

        // Fill the remaining capacity, then confirm the honest failure rather than a bad write.
        record.GrantSpell(SpellId.GillesGills, null);
        record.GrantSpell(SpellId.DivineIntervention, null);
        Check("the list fills to capacity", mem.ReadI32(list + Il2Cpp.ListSizeOffset) == 4);

        var full = record.GrantSpell(SpellId.Vitality, null);
        Check("a full list without the runtime helper fails cleanly",
            full.Outcome == CharacterRecord.GrantOutcome.Failed && !full.Success);
        Check("nothing was written past the end of the array",
            mem.ReadI32(list + Il2Cpp.ListSizeOffset) == 4);

        // Removal shifts the tail down, the way List<T>.RemoveAt does.
        Check("a granted spell can be removed", record.RevokeSpell(SpellId.DreamSpell));
        Check("the tail shifted down",
            record.ReadLearntSpells().SequenceEqual(new[]
            {
                SpellId.Gotterdamurung, SpellId.GillesGills, SpellId.DivineIntervention,
            }));
        Check("removing something absent reports false", !record.RevokeSpell(SpellId.DreamSpell));

        // KnowsSpell: the learnt list first, then the school level, never for a level-0 spell.
        var catalog = BuildTinyCatalog(mem);
        record.SetSpellLevel(6, 3);
        Check("a school level grants its spell", record.KnowsSpell(SpellId.Magestar, catalog));
        record.SetSpellLevel(6, 2);
        Check("too low a school level does not", !record.KnowsSpell(SpellId.Magestar, catalog));
        Check("a level-0 spell is never granted by a school level",
            !record.KnowsSpell(SpellId.DreamSpell, catalog));
        record.GrantSpell(SpellId.DreamSpell, null);
        Check("but it is known once it is in the learnt list",
            record.KnowsSpell(SpellId.DreamSpell, catalog));
    }

    // ============================== remote stub =================================
    /// <summary>
    /// Pins the machine code <see cref="X64Stub"/> emits for the allocation call.
    ///
    /// <para>This is the one piece of the trainer that cannot fail safely: a mis-encoded
    /// instruction is not an exception, it is a crash inside the game. Nothing about it can be
    /// checked at run time either, so the bytes are held against a disassembly verified by hand:</para>
    ///
    /// <code>
    ///   sub    rsp, 0x28                 ; shadow space + 16-byte alignment
    ///   movabs rax, il2cpp_domain_get    ; call rax
    ///   mov    rcx, rax                  ; domain -> first argument
    ///   movabs rax, il2cpp_thread_attach ; call rax
    ///   movabs rdx, threadSlot           ; mov [rdx], rax   (save the thread)
    ///   movabs rax, il2cpp_gc_disable    ; call rax
    ///   movabs rcx, klass                ; array type
    ///   movabs rdx, 4                    ; length
    ///   movabs rax, il2cpp_array_new_specific ; call rax
    ///   movabs rdx, resultSlot           ; mov [rdx], rax   (save the array)
    ///   movabs rax, threadSlot           ; mov rcx, [rax]
    ///   movabs rax, il2cpp_thread_detach ; call rax
    ///   xor    eax, eax                  ; ThreadProc returns 0
    ///   add    rsp, 0x28
    ///   ret
    /// </code>
    /// </summary>
    private static void CheckRemoteStub()
    {
        Console.WriteLine("\n--- remote stub encoding ---");

        // unchecked: these are deliberate 64-bit sentinels, and nuint is 64-bit here.
        nuint domainGet = unchecked((nuint)0x1111111111111111UL);
        nuint threadAttach = unchecked((nuint)0x2222222222222222UL);
        nuint gcDisable = unchecked((nuint)0x4444444444444444UL);
        nuint arrayNew = unchecked((nuint)0x5555555555555555UL);
        nuint threadDetach = unchecked((nuint)0x7777777777777777UL);
        nuint klass = unchecked((nuint)0x6666666666666666UL);
        nuint page = unchecked((nuint)0x3000000000000000UL);

        var stub = new X64Stub();
        stub.Prologue();
        stub.AttachThread(domainGet, threadAttach, page + 8);
        stub.CallNoArgs(gcDisable);
        stub.SetFlag(page + 0x10);
        stub.CallTwoArgs(arrayNew, klass, 4);
        stub.StoreRaxTo(page);
        stub.DetachThread(threadDetach, page + 8);
        stub.Epilogue();

        byte[] code = stub.ToArray();
        string hex = Convert.ToHexString(code).ToLowerInvariant();

        const string expected =
            "4883ec28" +                                 // sub rsp, 0x28
            "48b81111111111111111" + "ffd0" +            // movabs rax, domain_get; call rax
            "4889c1" +                                   // mov rcx, rax
            "48b82222222222222222" + "ffd0" +            // movabs rax, thread_attach; call rax
            "48ba0800000000000030" + "488902" +          // movabs rdx, threadSlot; mov [rdx], rax
            "48b84444444444444444" + "ffd0" +            // movabs rax, gc_disable; call rax
            "48ba1000000000000030" + "c70201000000" +    // movabs rdx, flagSlot; mov dword [rdx], 1
            "48b96666666666666666" +                     // movabs rcx, klass
            "48ba0400000000000000" +                     // movabs rdx, 4
            "48b85555555555555555" + "ffd0" +            // movabs rax, array_new_specific; call rax
            "48ba0000000000000030" + "488902" +          // movabs rdx, resultSlot; mov [rdx], rax
            "48b80800000000000030" + "488b08" +          // movabs rax, threadSlot; mov rcx, [rax]
            "48b87777777777777777" + "ffd0" +            // movabs rax, thread_detach; call rax
            "31c0" + "4883c428" + "c3";                  // xor eax, eax; add rsp, 0x28; ret

        Check("the stub encodes exactly as disassembled", hex == expected);
        Check("the stub is 149 bytes", code.Length == 149);
        Check("the stub fits the scratch page with room to spare", code.Length + 0x20 < 0x1000);
        // The marker is what lets a timed-out call be told apart from one that never ran, so
        // it has to be stamped *after* gc_disable returns and before the allocation is tried.
        Check("the collector marker is stamped between the disable and the allocation",
            hex.IndexOf("c70201000000", StringComparison.Ordinal) >
            hex.IndexOf("48b84444444444444444", StringComparison.Ordinal) &&
            hex.IndexOf("c70201000000", StringComparison.Ordinal) <
            hex.IndexOf("48b85555555555555555", StringComparison.Ordinal));
        Check("the stack is realigned before returning",
            code[0] == 0x48 && code[1] == 0x83 && code[2] == 0xEC && code[3] == 0x28
            && code[^5] == 0x48 && code[^4] == 0x83 && code[^3] == 0xC4 && code[^2] == 0x28);
        Check("it returns as a ThreadProc", code[^1] == 0xC3);
    }

    /// <summary>A two-entry catalogue mirroring the one built in the catalogue check.</summary>
    private static SpellCatalog BuildTinyCatalog(FakeMemorySource mem)
    {
        const nuint klass = 0x60000, statics = 0x60100, instance = 0x60200;
        const nuint table = 0x60300, normal = 0x60400, special = 0x60500, code = 0x60600;

        foreach (var (at, size) in new (nuint, int)[]
                 {
                     (klass, 0x100), (statics, 0x40), (instance, 0x40), (table, 0x60),
                     (normal, 0xC0), (special, 0xC0), (code, 0x40),
                 })
            mem.Map(at, new byte[size]);

        mem.WritePtr(klass + (nuint)Il2Cpp.ClassStaticFieldsOffset, statics);
        mem.WritePtr(statics, instance);
        mem.WritePtr(instance + (nuint)CharacterFormat.GlobalSpellsByEnum, table);
        mem.WriteI32(table + Il2Cpp.ArrayLengthOffset, 2);
        mem.WritePtr(Il2Cpp.ArrayElement(table, 0), normal);
        mem.WritePtr(Il2Cpp.ArrayElement(table, 1), special);

        SyntheticWorld.ManagedString(mem, code, "MAST");
        mem.WritePtr(normal + (nuint)CharacterFormat.SpellDescriptionCode, code);
        mem.WriteI32(normal + (nuint)CharacterFormat.SpellDescriptionSpell, (int)SpellId.Magestar);
        mem.WriteI32(normal + (nuint)CharacterFormat.SpellDescriptionClass, 6);
        mem.WriteI32(normal + (nuint)CharacterFormat.SpellDescriptionLevel, 3);

        mem.WriteI32(special + (nuint)CharacterFormat.SpellDescriptionSpell, (int)SpellId.DreamSpell);
        mem.WriteI32(special + (nuint)CharacterFormat.SpellDescriptionClass, 6);
        mem.WriteI32(special + (nuint)CharacterFormat.SpellDescriptionLevel, 0);

        return SpellCatalog.Read(mem, klass);
    }

    private static void CheckItemBook()
    {
        Console.WriteLine("\n--- ItemBook ---");
        Check("the item catalogue is populated", ItemBook.Choices.Count > 100);
        Check("every entry has a display string", ItemBook.Choices.All(c => !string.IsNullOrWhiteSpace(c.Display)));
    }

    // ============================== memory paths ================================
    private static void CheckFakeMemorySource()
    {
        Console.WriteLine("\n--- FakeMemorySource ---");
        var mem = new FakeMemorySource();
        mem.Map(0x1000, new byte[0x100]);

        Check("writes then reads back", mem.Write(0x1010, new byte[] { 1, 2, 3, 4 }) &&
            mem.ReadI32(0x1010) == 0x04030201);
        Check("an unmapped read returns nothing", mem.Read(0x9999, new byte[4], 4) == 0);
        Check("a read past the end of a page fails", mem.Read(0x10F0, new byte[0x40], 0x40) == 0);
        Check("regions are enumerated", mem.EnumerateRegions().Any(r => r.Base == 0x1000));

        nuint block = mem.Allocate(0x40);
        Check("allocation returns a usable block", block != 0 && mem.Write(block, new byte[] { 9 }));
        Check("allocations do not overlap", mem.Allocate(0x40) != block);
    }

    private static void CheckIl2CppHelpers()
    {
        Console.WriteLine("\n--- Il2Cpp helpers ---");
        var mem = new FakeMemorySource();
        mem.Map(0x2000, new byte[0x1000]);

        // A managed string: length at +0x10, UTF-16 characters at +0x14.
        const nuint str = 0x2100;
        mem.WriteI32(str + Il2Cpp.StringLengthOffset, 5);
        mem.Write(str + Il2Cpp.StringCharsOffset, Encoding.Unicode.GetBytes("Skara"));
        Check("managed strings decode", mem.ReadManagedString(str) == "Skara");
        Check("a null string reference reads empty", mem.ReadManagedString(0) == "");

        // A managed array of references: length at +0x18, elements from +0x20.
        const nuint array = 0x2200;
        mem.WriteI32(array + Il2Cpp.ArrayLengthOffset, 3);
        mem.WritePtr(Il2Cpp.ArrayElement(array, 1), 0xDEAD);
        Check("array length is read", mem.ReadArrayLength(array) == 3);
        Check("array elements start at +0x20", Il2Cpp.ArrayElement(array, 0) == array + 0x20);
        Check("array elements are read", mem.ReadArrayRef(array, 1) == 0xDEAD);
        Check("a null array has no length", mem.ReadArrayLength(0) == 0);

        // A native (ASCII) string, as used for class names.
        const nuint name = 0x2300;
        mem.Write(name, Encoding.ASCII.GetBytes("Player\0"));
        Check("native strings decode", mem.ReadNativeString(name) == "Player");
        const nuint garbage = 0x2320;
        mem.Write(garbage, new byte[] { 0x01, 0x02, 0x00 });
        Check("non-printable bytes are not treated as a name", mem.ReadNativeString(garbage) == "");

        // A class, its static block, and the singleton in it.
        const nuint klass = 0x2400;
        const nuint statics = 0x2500;
        const nuint instance = 0x2600;
        const nuint ns = 0x2340;
        mem.Write(ns, Encoding.ASCII.GetBytes("BardsTale\0"));
        mem.WritePtr(klass + Il2Cpp.ClassNameOffset, name);
        mem.WritePtr(klass + Il2Cpp.ClassNamespaceOffset, ns);
        mem.WritePtr(klass + Il2Cpp.ClassStaticFieldsOffset, statics);
        mem.WritePtr(statics, instance);
        Check("a class is matched by name and namespace", mem.ClassMatches(klass, "Player", "BardsTale"));
        Check("the wrong name does not match", !mem.ClassMatches(klass, "Party", "BardsTale"));
        Check("the wrong namespace does not match", !mem.ClassMatches(klass, "Player", "Unity"));
        Check("the singleton is read through static_fields", mem.ReadStaticRef(klass) == instance);
        Check("64-bit fields round-trip", mem.WriteI64(0x2050, 12_000_000_000L) && mem.ReadI64(0x2050) == 12_000_000_000L);
    }

    /// <summary>
    /// A host that records what the view-model says, so the view-models can be exercised
    /// without a running game or a WPF dispatcher.
    /// </summary>
    private sealed class SilentHost : ICharacterHost
    {
        public List<string> Messages { get; } = new();
        public void OnMessage(string msg) => Messages.Add(msg);
        public SpellCatalog Spells => SpellCatalog.Empty;
        public Il2CppRuntime? Runtime => null;
    }

    /// <summary>
    /// The spell-level rows are built from whatever <c>m_spellLevel</c> reads back, and on the
    /// structural-scan fallback that can be an object that only looks like a character. The row
    /// has to survive it: an exception here unwinds out of the view-model constructor and
    /// aborts the whole locate, leaving a half-populated party and no explanation.
    /// </summary>
    private static void CheckSpellLevelRowsSurviveJunk()
    {
        Console.WriteLine("\n--- spell-level rows against junk ---");
        var mem = new FakeMemorySource();
        const nuint addr = 0x30000, levels = 0x31000;
        mem.Map(addr, ValidCharacterBytes());
        mem.Map(levels, new byte[Il2Cpp.ArrayHeaderSize + CharacterFormat.SpellLevelSlots * 4]);
        mem.WriteI32(levels + Il2Cpp.ArrayLengthOffset, CharacterFormat.SpellLevelSlots);
        mem.WritePtr(addr + (nuint)CharacterFormat.OffSpellLevels, levels);

        // Put an impossible level into every school, as a false-positive object would.
        for (int i = 0; i < CharacterFormat.SpellLevelSlots; i++)
            mem.WriteI32(levels + (nuint)(Il2Cpp.ArrayHeaderSize + i * 4), 9);

        var record = new CharacterRecord(mem, addr, slot: 0);
        Check("the junk levels really are out of range", record.ReadSpellLevels().Any(v => v > CharacterFormat.MaxSpellLevel));

        CharacterViewModel? vm = null;
        bool built = true;
        try { vm = new CharacterViewModel(record, new SilentHost()); }
        catch (Exception) { built = false; }

        Check("a character with out-of-range spell levels still builds", built);
        Check("every row is clamped into range", built &&
            vm!.SpellLevels.All(r => r.Level >= 0 && r.Level <= CharacterFormat.MaxSpellLevel));

        // Pull must leave the row writable again, or the user's edits stop reaching the game.
        if (built)
        {
            var row = vm!.SpellLevels[0];
            row.Level = 3;
            Check("an edit after the clamp is written through",
                record.GetSpellLevel(row.ClassId) == 3);
        }
    }

    private static void CheckCharacterRecordRoundTrip()
    {
        Console.WriteLine("\n--- CharacterRecord round-trip ---");
        var mem = new FakeMemorySource();
        const nuint addr = 0x30000;
        mem.Map(addr, ValidCharacterBytes());

        var record = new CharacterRecord(mem, addr, slot: 1);
        Check("the synthetic character validates", record.IsOccupied);
        Check("level reads back", record.Level == 5);
        Check("experience is 64-bit", record.Experience == 5_000_000_000L);

        record.HpCur = 42;
        record.Level = 21;
        record.Experience = 9_000_000_000L;
        record.Gold = 4_000_000_000L;
        record.SetStat(2, 24);
        Check("hit points write back", record.HpCur == 42);
        Check("level writes back", record.Level == 21);
        Check("64-bit experience writes back", record.Experience == 9_000_000_000L);
        Check("64-bit gold writes back", record.Gold == 4_000_000_000L);
        Check("attributes write back", record.GetStat(2) == 24);

        // m_spellLevel is a pointer to an int[16].
        const nuint spellArray = 0x31000;
        mem.Map(spellArray, new byte[Il2Cpp.ArrayHeaderSize + CharacterFormat.SpellLevelSlots * 4]);
        mem.WriteI32(spellArray + Il2Cpp.ArrayLengthOffset, CharacterFormat.SpellLevelSlots);
        mem.WritePtr(addr + (nuint)CharacterFormat.OffSpellLevels, spellArray);

        Check("spell levels start empty", record.GetSpellLevel(6) == 0);
        Check("a spell level writes back", record.SetSpellLevel(6, 4) && record.GetSpellLevel(6) == 4);
        Check("spell levels clamp to 7", record.SetSpellLevelClamped(7, 99) && record.GetSpellLevel(7) == 7);
        Check("an out-of-range school is refused", !record.SetSpellLevel(99, 3));
        record.LearnAllClassSpells();
        Check("learning everything fills all seven schools",
            CharacterFormat.CasterClasses.All(c => record.GetSpellLevel(c.ClassId) == CharacterFormat.MaxSpellLevel));
        Check("the whole array reads back",
            record.ReadSpellLevels().Length == CharacterFormat.SpellLevelSlots);

        // Class change grants the new school's level.
        record.Level = 9;
        for (int id = 6; id <= 12; id++) record.SetSpellLevel(id, 0);
        record.Class = 0;
        string what = record.ChangeClass(6);
        Check("changing to a caster grants its spell level",
            record.Class == 6 && record.GetSpellLevel(6) == ClassBook.SpellLevelForLevel(9));
        Check("the class change is described", what.Contains("Conjurer", StringComparison.Ordinal));
        record.ChangeClass(0);
        Check("changing to a fighter grants nothing", record.Class == 0);

        // Inventory: Character.m_inventory -> Inventory.m_items -> Item[].
        const nuint inventory = 0x32000;
        const nuint items = 0x32100;
        const nuint item0 = 0x32200;
        mem.Map(inventory, new byte[0x20]);
        mem.Map(items, new byte[Il2Cpp.ArrayHeaderSize + 2 * 8]);
        mem.Map(item0, new byte[0x30]);
        mem.WritePtr(addr + (nuint)CharacterFormat.OffInventory, inventory);
        mem.WritePtr(inventory + (nuint)CharacterFormat.InventoryItems, items);
        mem.WriteI32(items + Il2Cpp.ArrayLengthOffset, 2);
        mem.WritePtr(Il2Cpp.ArrayElement(items, 0), item0);
        mem.WriteI32(item0 + (nuint)CharacterFormat.ItemCharges, 7);

        var charges = record.ReadItemCharges();
        Check("item charges are read through the inventory", charges.Length == 2 && charges[0] == 7);
        Check("an empty slot reads as no item", charges[1] == null);
        Check("charges are zeroed for unlimited use",
            record.SetAllItemsInfinite() && mem.ReadI32(item0 + (nuint)CharacterFormat.ItemCharges) == 0);

        var scores = record.ReadClassScores();
        record.WriteClassScores(scores with { CriticalHit = 200 });
        Check("class scores write back", record.ReadClassScores().CriticalHit == 200);
    }

    /// <summary>
    /// Builds a synthetic IL2CPP world — module slots, classes, statics, a Player and a
    /// GameMap — then drives the real locator and navigator over it, including a teleport.
    /// </summary>
    private static void CheckClassLocatorAndNavigator()
    {
        Console.WriteLine("\n--- Il2CppClassLocator and MapNavigator ---");
        var world = SyntheticWorld.Build();
        var mem = world.Memory;

        var classes = Il2CppClassLocator.Resolve(mem, world.ModuleBase, world.ModuleSize);
        Check("Player resolves from its class slot", classes.Player == world.PlayerClass);
        Check("GlobalMaps resolves", classes.GlobalMaps == world.GlobalMapsClass);
        Check("Party resolves", classes.Party == world.PartyClass);
        Check("TeleportTarget resolves", classes.TeleportTarget == world.TeleportTargetClass);
        Check("GlobalSpells resolves", classes.GlobalSpells == world.GlobalSpellsClass);
        Check("the map features are available", classes.HasMapClasses && classes.CanFabricateTeleport);
        Check("the known slots were used", classes.Method == "known class slots");

        var nav = new MapNavigator(mem, classes);
        Check("Player.Instance is found", nav.PlayerInstance == world.Player);
        Check("the chapter is read from GlobalMaps", nav.Chapter == GameChapter.TalesOfTheUnknown);

        var where = nav.ReadLocation();
        Check("the party position is read", where is { X: 12, Z: 7, Facing: Facing.East });
        Check("the map is identified", where is { IsDungeon: true, MapIndex: 0, Width: 22, Height: 22 });
        Check("the map name comes from the game", where!.MapName == "Cellars");
        Check("the catalogue entry is matched", where.Info?.Name == "Cellars");
        Check("the summary names the chapter", where.Summary.StartsWith("BT1", StringComparison.Ordinal));

        var live = nav.ReadLiveMaps();
        Check("the live map list is read from GlobalMaps", live.Count == 2);
        Check("live entries carry names and sizes",
            live[0] is { IsDungeon: false, Index: 0, Name: "Skara Brae" } &&
            live[1] is { IsDungeon: true, Index: 0, Name: "Cellars" });

        // Teleport into a different map.
        var target = MapBook.Find(GameChapter.TalesOfTheUnknown, isDungeon: true, index: 11)!;
        bool queued = nav.TryTeleport(target, 3, 4, Facing.South, TeleportType.Fade, journal: false, out string msg);
        Check("the teleport is accepted", queued);
        Check("the message names the destination", msg.Contains("Mangars Tower Lv1", StringComparison.Ordinal));

        nuint queue = mem.ReadPtr(world.Player + (nuint)MapFormat.PlayerQueueTeleport);
        Check("the queue now points at a target", queue != 0);
        Check("the target carries the TeleportTarget class",
            mem.ReadObjectClass(queue) == world.TeleportTargetClass);
        Check("m_isValid is set — the game only acts on the queue when it is",
            mem.ReadBool(queue + (nuint)MapFormat.TeleportIsValid));
        Check("m_teleportDone is clear", !mem.ReadBool(queue + (nuint)MapFormat.TeleportDone));
        Check("the destination map is written",
            mem.ReadI32(queue + (nuint)MapFormat.TeleportMap) == 11 &&
            mem.ReadBool(queue + (nuint)MapFormat.TeleportIsDungeon));
        Check("the destination square is written",
            mem.ReadI32(queue + (nuint)MapFormat.TeleportX) == 3 &&
            mem.ReadI32(queue + (nuint)MapFormat.TeleportZ) == 4);
        Check("the facing is written",
            mem.ReadI32(queue + (nuint)MapFormat.TeleportFacing) == (int)Facing.South);
        Check("the map size is passed along",
            mem.ReadI32(queue + (nuint)MapFormat.TeleportMapWidth) == target.Width &&
            mem.ReadI32(queue + (nuint)MapFormat.TeleportMapHeight) == target.Height);
        Check("the transition style is written",
            mem.ReadI32(queue + (nuint)MapFormat.TeleportKind) == (int)TeleportType.Fade);
        Check("no journal entry was requested",
            !mem.ReadBool(queue + (nuint)MapFormat.TeleportDoJournal));

        // The same block is reused rather than leaking a new one per teleport.
        nav.TryTeleport(target, 5, 6, Facing.North, TeleportType.Quiet, journal: true, out _);
        Check("the trainer reuses its own target block",
            mem.ReadPtr(world.Player + (nuint)MapFormat.PlayerQueueTeleport) == queue);
        Check("the reused target is refilled",
            mem.ReadI32(queue + (nuint)MapFormat.TeleportX) == 5 &&
            mem.ReadBool(queue + (nuint)MapFormat.TeleportDoJournal));

        // Out-of-bounds destinations are refused before anything is written.
        Check("a square outside the map is refused",
            !nav.TryTeleport(target, 99, 0, Facing.North, TeleportType.Fade, false, out string oob) &&
            oob.Contains("outside", StringComparison.Ordinal));

        // A destination from another chapter is the one that breaks the game rather than the
        // trainer: TeleportTarget.m_map is a bare index into the *loaded* chapter's own array
        // and LoadMap does not bounds-check it. The picker lists all three games at once, so
        // this has to be caught here.
        Check("the live map arrays are sized as BT1 sizes them",
            nav.ReadMapArrayCounts() == (1, 16));

        int armedMap = mem.ReadI32(queue + (nuint)MapFormat.TeleportMap);
        var otherChapter = MapBook.Find(GameChapter.ThiefOfFate, isDungeon: true, index: 60)!;
        Check("a map from another chapter is refused",
            !nav.TryTeleport(otherChapter, 1, 1, Facing.North, TeleportType.Fade, false, out string cross));
        Check("the refusal says which game is loaded",
            cross.Contains("Thief of Fate", StringComparison.Ordinal) &&
            cross.Contains("Tales of the Unknown", StringComparison.Ordinal));
        Check("nothing was written for the refused chapter",
            mem.ReadI32(queue + (nuint)MapFormat.TeleportMap) == armedMap);

        // Past the end of the loaded chapter's own array, which is the same crash by the other
        // route. BT1 has 16 dungeon maps, so index 16 is one too far.
        Check("a map past the end of the live array is refused",
            !nav.TryTeleport(
                MapBook.Find(GameChapter.TalesOfTheUnknown, isDungeon: true, index: 15)! with { Index = 16 },
                1, 1, Facing.North, TeleportType.Fade, false, out string past) &&
            past.Contains("only has 16 dungeon maps", StringComparison.Ordinal));

        // In-chapter and in-range stays accepted — the guard must not cost the feature.
        Check("an in-chapter destination is still accepted",
            nav.TryTeleport(target, 2, 2, Facing.North, TeleportType.Fade, false, out _));

        // The direct fallback writes the grid fields.
        Check("setting the grid position directly works",
            nav.TrySetGridPosition(9, 9, Facing.West, out _) &&
            mem.ReadI32(world.Player + (nuint)MapFormat.PlayerGridX) == 9 &&
            mem.ReadI32(world.Player + (nuint)MapFormat.PlayerGridZ) == 9);
        Check("the previous square is kept in step",
            mem.ReadI32(world.Player + (nuint)MapFormat.PlayerPrevX) == 9);
        Check("turning on the spot works",
            nav.TrySetFacing(Facing.North) &&
            mem.ReadI32(world.Player + (nuint)MapFormat.PlayerFacing) == 0);

        // A stale slot must not be trusted: the name check is what makes that safe.
        var wrong = new FakeMemorySource();
        wrong.Map(world.ModuleBase + 0xE44000, new byte[0x3000]);
        var none = Il2CppClassLocator.Resolve(wrong, world.ModuleBase, 0x3000);
        Check("empty slots resolve nothing", !none.HasMapClasses && none.Method == "not found");
    }

    private static void CheckGameLocatorStructuralScan()
    {
        Console.WriteLine("\n--- GameLocator ---");
        var world = SyntheticWorld.Build();
        var found = GameLocator.Locate(world.Memory, world.ModuleBase, world.ModuleSize);
        Check("locate succeeds", found != null);
        Check("it goes through Party.Instance rather than scanning", found is { UsedFallback: false });
        Check("the party object is the one in the static block", found!.PartyObject == world.Party);
        Check("the roster is read through PartyMember.m_character", found.CharacterCount == 1);
        Check("the character, not its slot wrapper, is what was found",
            found.CharacterAddresses[0] == world.Character);
        Check("the map classes come along", found.Classes.HasMapClasses);

        // With nothing mapped at the class slots, the shape scan still finds a character.
        var mem = new FakeMemorySource();
        mem.Map(0x50000, ValidCharacterBytes());
        var scanned = GameLocator.Locate(mem, 0, 0);
        Check("the fallback scan finds a plausible character", scanned is { UsedFallback: true, CharacterCount: 1 });
        Check("the fallback reports no party object", scanned!.PartyObject == 0);

        var empty = new FakeMemorySource();
        empty.Map(0x60000, new byte[0x400]);
        Check("empty memory yields nothing", GameLocator.Locate(empty, 0, 0) == null);
    }

    /// <summary>
    /// End-to-end check against a real installation: open the game's <c>resources.assets</c>,
    /// decode all 121 maps, and hold each one against the catalogue. Skipped, not failed, when
    /// the game is not installed — the rest of the harness must still run anywhere.
    /// </summary>
    private static void CheckMapArchive()
    {
        Console.WriteLine("\n--- MapArchive (needs the game installed) ---");
        string? dir = GameLocator.FindGameDirectory(null);
        if (dir == null)
        {
            Console.WriteLine("  SKIP the game is not installed on this machine");
            return;
        }

        using var archive = MapArchive.TryOpen(dir, out string error);
        Check($"resources.assets opens ({dir})", archive != null);
        if (archive == null)
        {
            Console.WriteLine($"       {error}");
            return;
        }

        Check("the archive holds every catalogued map",
            MapBook.Maps.All(m => archive.MapAssets.Contains(m.Asset)));

        int decoded = 0, sizeMatches = 0, kindMatches = 0;
        var problems = new List<string>();
        foreach (var info in MapBook.Maps)
        {
            var grid = archive.TryGetMap(info.Asset, out string mapError);
            if (grid == null)
            {
                problems.Add($"{info.Name}: {mapError}");
                continue;
            }
            decoded++;
            if (grid.Width == info.Width && grid.Height == info.Height) sizeMatches++;
            else problems.Add($"{info.Name}: catalogue says {info.Width}x{info.Height}, file says {grid.Width}x{grid.Height}");
            if (grid.IsDungeon == info.IsDungeon) kindMatches++;
        }

        Check($"all {MapBook.Maps.Count} maps decode", decoded == MapBook.Maps.Count);
        Check("every decoded size matches the catalogue", sizeMatches == MapBook.Maps.Count);
        Check("every decoded kind matches the catalogue", kindMatches == MapBook.Maps.Count);
        foreach (var p in problems.Take(5)) Console.WriteLine($"       {p}");

        // Every ZZGO destination against the game's own location scripts. This is what settles
        // the Fanskar's Castle entry above: each of the seven lands exactly on the square whose
        // script names that dungeon, so the table is right where it parts company with the
        // dungeon's parent link.
        // The script names are pinned literally rather than fuzzy-matched against the target
        // names: they are the game's own identifiers, and spelling them out is what makes the
        // Fanskar's Castle square evidence rather than an assumption.
        var expectedDreamScripts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["The Tombs"] = "TheTombs",
            ["Fanskar's Castle"] = "FanskarCastle",
            ["Dargoth's Tower"] = "DargothsTowerEntrance",
            ["Maze of Dread"] = "MazeOfDread",
            ["Oscon's Fortress"] = "OsconsFortressTimeCheck",
            ["Grey Crypt"] = "GreyCrypt",
            ["Destiny Stone"] = "DestinyStoneEntrance",
        };

        var dreamMisses = new List<string>();
        foreach (var t in MapBook.DreamSpellTargets)
        {
            var city = MapBook.Find(GameChapter.DestinyKnight, isDungeon: false, t.Map);
            var grid = city == null ? null : archive.TryGetMap(city.Asset, out _);
            if (grid == null) { dreamMisses.Add($"{t.Name}: city map {t.Map} did not decode"); continue; }
            grid.LocationScripts.TryGetValue((t.X, t.Z), out string? script);
            if (!expectedDreamScripts.TryGetValue(t.Name, out string? want))
                dreamMisses.Add($"{t.Name}: no expected script recorded");
            else if (script != want)
                dreamMisses.Add($"{t.Name}: ({t.X},{t.Z}) holds '{script ?? "nothing"}', expected '{want}'");
        }
        Check("every dream target sits on the game's own entrance script", dreamMisses.Count == 0);
        foreach (var p in dreamMisses) Console.WriteLine($"       {p}");

        // Spot-check a map whose contents are known from the game's own data.
        var tower = archive.TryGetMap("map_bt3_dung03_tower_asc", out _);
        Check("BT3's first tower floor is a 5x5 tower", tower is { Width: 5, Height: 5, IsTower: true });
        Check("its south-west corner is walled in on two sides",
            tower![0, 0].South == WallKind.Solid && tower[0, 0].West == WallKind.Solid);
        Check("its stairs out sit at the entry point named in the catalogue",
            tower[MapBook.Find(GameChapter.ThiefOfFate, true, 3)!.EntryX,
                  MapBook.Find(GameChapter.ThiefOfFate, true, 3)!.EntryZ]
                .Flags.HasFlag(CellFlags.StairsOut));

        // Pixel mapping must round-trip for every square of a real grid.
        var cellars = archive.TryGetMap("map_bt1_dung00_cellars_asc", out _)!;
        bool roundTrips = true;
        for (int z = 0; z < cellars.Height && roundTrips; z++)
            for (int x = 0; x < cellars.Width; x++)
            {
                var (px, py) = MapRenderer.CellToPixel(cellars, x, z);
                if (MapRenderer.PixelToCell(cellars, px, py) == (x, z)) continue;
                roundTrips = false;
                break;
            }
        Check("cell/pixel mapping round-trips across a whole map", roundTrips);

        // Actually draw a few real maps. Rendering is the one path the synthetic tests cannot
        // reach, and a bad brush or geometry would only show up here or in the window.
        var toDraw = new[]
        {
            ("map_bt1_city00_skarabrae_asc", "Skara Brae, the largest city grid"),
            ("map_bt1_dung00_cellars_asc", "the Cellars, a wrapping dungeon"),
            ("map_bt2_city00_theforest_asc", "the BT2 wilderness, the tallest grid"),
            ("map_bt3_dung03_tower_asc", "a 5x5 tower floor"),
        };
        foreach (var (asset, what) in toDraw)
        {
            var grid = archive.TryGetMap(asset, out _);
            if (grid == null) { Check($"render {what}", false); continue; }
            var (w, h, ok) = RenderOnStaThread(grid);
            Check($"render {what} ({grid.Width}x{grid.Height} squares -> {w}x{h} px)",
                ok && w == MapRenderer.PixelWidth(grid) && h == MapRenderer.PixelHeight(grid));
        }
        Check("north is drawn upward",
            MapRenderer.CellToPixel(cellars, 0, cellars.Height - 1).Y < MapRenderer.CellToPixel(cellars, 0, 0).Y);
        Check("east is drawn rightward",
            MapRenderer.CellToPixel(cellars, 1, 0).X > MapRenderer.CellToPixel(cellars, 0, 0).X);
    }

    /// <summary>
    /// Renders a grid on a dedicated STA thread — WPF's <c>RenderTargetBitmap</c> needs one, and
    /// a console host is MTA. Returns the bitmap's size and whether it drew at all.
    /// </summary>
    private static (int Width, int Height, bool Ok) RenderOnStaThread(MapGrid grid)
    {
        int width = 0, height = 0;
        bool ok = false;
        var thread = new Thread(() =>
        {
            try
            {
                var image = MapRenderer.Render(grid);
                width = (int)image.Width;
                height = (int)image.Height;
                ok = image.CanFreeze || image.IsFrozen;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"       render threw: {ex.Message}");
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return (width, height, ok);
    }

    // ================================ helpers ===================================
    /// <summary>A byte image of a plausible level-5 character.</summary>
    private static byte[] ValidCharacterBytes()
    {
        var buf = new byte[CharacterFormat.ObjectSize];
        WriteI64(buf, CharacterFormat.OffExperience, 5_000_000_000L);
        WriteI64(buf, CharacterFormat.OffGold, 1200);
        WriteI32(buf, CharacterFormat.OffLevel, 5);
        WriteI32(buf, CharacterFormat.OffRealLevel, 5);
        WriteI32(buf, CharacterFormat.OffHpMax, 100);
        WriteI32(buf, CharacterFormat.OffHpCur, 100);
        WriteI32(buf, CharacterFormat.OffSpMax, 50);
        WriteI32(buf, CharacterFormat.OffSpCur, 50);
        WriteI32(buf, CharacterFormat.OffRace, 0);
        WriteI32(buf, CharacterFormat.OffClass, 1);
        WriteI32(buf, CharacterFormat.OffGender, 0);
        WriteI32(buf, CharacterFormat.OffCondition, 0);
        for (int i = 0; i < CharacterFormat.StatCount; i++)
            WriteI32(buf, CharacterFormat.OffStrength + i * 4, 18);
        return buf;
    }

    /// <summary>Runs <see cref="CharacterFormat.LooksLikeCharacter"/> over a copy with one field changed.</summary>
    private static bool Mutated(byte[] source, int offset, int value)
    {
        var copy = (byte[])source.Clone();
        WriteI32(copy, offset, value);
        return CharacterFormat.LooksLikeCharacter(copy);
    }

    private static bool Mutated(byte[] source, int offset, long value)
    {
        var copy = (byte[])source.Clone();
        WriteI64(copy, offset, value);
        return CharacterFormat.LooksLikeCharacter(copy);
    }

    private static void WriteI32(byte[] buf, int offset, int value) =>
        BitConverter.GetBytes(value).CopyTo(buf, offset);

    private static void WriteI64(byte[] buf, int offset, long value) =>
        BitConverter.GetBytes(value).CopyTo(buf, offset);

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }
}

/// <summary>
/// A miniature IL2CPP world in synthetic memory: the module's class slots, four
/// <c>Il2CppClass</c> structures with their static blocks, and live <c>Player</c>,
/// <c>GameMap</c>, <c>GlobalMaps</c> and <c>Party</c> objects. It is laid out exactly as the
/// real runtime lays them out, so the locator and navigator can be driven over it unchanged.
/// </summary>
internal sealed class SyntheticWorld
{
    public FakeMemorySource Memory { get; private init; } = new();
    public nuint ModuleBase { get; private init; }
    public nuint ModuleSize { get; private init; }
    public nuint PartyClass { get; private init; }
    public nuint PlayerClass { get; private init; }
    public nuint GlobalMapsClass { get; private init; }
    public nuint TeleportTargetClass { get; private init; }
    public nuint GlobalSpellsClass { get; private init; }
    public nuint Player { get; private init; }
    public nuint Party { get; private init; }
    public nuint GlobalMaps { get; private init; }
    public nuint Character { get; private init; }

    public static SyntheticWorld Build()
    {
        var mem = new FakeMemorySource();
        // The game's usual image base. nuint is 64-bit on the x64 target this builds for.
        nuint moduleBase = unchecked((nuint)0x180000000UL);

        // Only the window of the module holding the class slots needs to exist.
        const nuint slotPage = 0xE44000;
        const int slotPageSize = 0x3000;
        mem.Map(moduleBase + slotPage, new byte[slotPageSize]);

        // One page for the heap objects, one for the strings they point at.
        const nuint heap = 0x20000000;
        mem.Map(heap, new byte[0x4000]);
        const nuint strings = 0x21000000;
        mem.Map(strings, new byte[0x800]);

        nuint cursor = strings;
        nuint Ascii(string s)
        {
            nuint at = cursor;
            mem.Write(at, Encoding.ASCII.GetBytes(s + "\0"));
            cursor += (nuint)(s.Length + 1 + 7) & ~(nuint)7;
            return at;
        }

        nuint ns = Ascii("BardsTale");
        nuint namePlayer = Ascii("Player");
        nuint nameParty = Ascii("Party");
        nuint nameGlobalMaps = Ascii("GlobalMaps");
        nuint nameTeleport = Ascii("TeleportTarget");
        nuint nameGlobalSpells = Ascii("GlobalSpells");

        // Heap layout: classes, static blocks, then objects.
        const nuint playerClass = heap + 0x0000;
        const nuint partyClass = heap + 0x0200;
        const nuint globalMapsClass = heap + 0x0400;
        const nuint teleportClass = heap + 0x0600;
        const nuint playerStatics = heap + 0x0800;
        const nuint partyStatics = heap + 0x0840;
        const nuint globalStatics = heap + 0x0880;
        const nuint player = heap + 0x0900;
        const nuint gameMap = heap + 0x0B00;
        const nuint globalMaps = heap + 0x0E00;
        const nuint party = heap + 0x0F00;
        const nuint members = heap + 0x1000;
        const nuint partyMember = heap + 0x1080;
        const nuint character = heap + 0x1100;
        const nuint mapNameString = heap + 0x1300;
        const nuint cityArray = heap + 0x1400;
        const nuint cityDesc = heap + 0x1500;
        const nuint dungeonDesc = heap + 0x1700;
        const nuint cityNameString = heap + 0x1900;
        const nuint dungeonNameString = heap + 0x1980;
        const nuint globalSpellsClass = heap + 0x1A00;
        const nuint spellStatics = heap + 0x1B00;
        const nuint globalSpells = heap + 0x1B80;
        const nuint spellTable = heap + 0x1C00;

        // Last, and on its own: BT1's dungeon array is 16 elements, so its payload runs to
        // +0xA0 and would otherwise overlap whatever came next.
        const nuint dungeonArray = heap + 0x2000;

        void Klass(nuint klass, nuint name, nuint statics)
        {
            mem.WritePtr(klass + Il2Cpp.ClassNameOffset, name);
            mem.WritePtr(klass + Il2Cpp.ClassNamespaceOffset, ns);
            mem.WritePtr(klass + Il2Cpp.ClassStaticFieldsOffset, statics);
        }

        Klass(playerClass, namePlayer, playerStatics);
        Klass(partyClass, nameParty, partyStatics);
        Klass(globalMapsClass, nameGlobalMaps, globalStatics);
        Klass(teleportClass, nameTeleport, 0);
        Klass(globalSpellsClass, nameGlobalSpells, spellStatics);

        // The module's metadata-usage slots hold the class pointers.
        mem.WritePtr(moduleBase + (nuint)GameFacts.PlayerClassRva, playerClass);
        mem.WritePtr(moduleBase + (nuint)GameFacts.PartyClassRva, partyClass);
        mem.WritePtr(moduleBase + (nuint)GameFacts.GlobalMapsClassRva, globalMapsClass);
        mem.WritePtr(moduleBase + (nuint)GameFacts.TeleportTargetClassRva, teleportClass);
        mem.WritePtr(moduleBase + (nuint)GameFacts.GlobalSpellsClassRva, globalSpellsClass);

        // Static blocks: Instance first, then GlobalMaps' chapter.
        mem.WritePtr(playerStatics, player);
        mem.WritePtr(partyStatics, party);
        mem.WritePtr(globalStatics + MapFormat.GlobalMapsInstanceStatic, globalMaps);
        mem.WriteI32(globalStatics + MapFormat.GlobalMapsChapterStatic, (int)GameChapter.TalesOfTheUnknown);

        // GlobalSpells holds the spell table; an empty one is enough for the locator.
        mem.WritePtr(spellStatics + CharacterFormat.GlobalSpellsInstanceStatic, globalSpells);
        mem.WritePtr(globalSpells + (nuint)CharacterFormat.GlobalSpellsByEnum, spellTable);
        mem.WriteI32(spellTable + Il2Cpp.ArrayLengthOffset, 0);

        // The party is standing in the Cellars, the first BT1 dungeon.
        mem.WritePtr(player + Il2Cpp.ObjectClassOffset, playerClass);
        mem.WritePtr(player + MapFormat.PlayerMap, gameMap);
        mem.WriteI32(player + MapFormat.PlayerGridX, 12);
        mem.WriteI32(player + MapFormat.PlayerGridZ, 7);
        mem.WriteI32(player + MapFormat.PlayerFacing, (int)Facing.East);

        mem.WriteI32(gameMap + MapFormat.GameMapWidth, 22);
        mem.WriteI32(gameMap + MapFormat.GameMapHeight, 22);
        mem.WriteI32(gameMap + MapFormat.GameMapIndex, 0);
        mem.WriteI32(gameMap + MapFormat.GameMapLevel, 0);
        mem.WriteBool(gameMap + MapFormat.GameMapIsDungeon, true);
        mem.WritePtr(gameMap + MapFormat.GameMapName, mapNameString);
        ManagedString(mem, mapNameString, "Cellars");

        // GlobalMaps' two map arrays, sized as BT1 really sizes them — 1 city and 16 dungeons.
        // Only element 0 of each is filled in; the rest stay null, which is what the teleport
        // bounds check is deliberately indifferent to (it bounds against the array length,
        // since that is what Player.LoadMap indexes).
        mem.WritePtr(globalMaps + MapFormat.GlobalMapsCityMaps, cityArray);
        mem.WritePtr(globalMaps + MapFormat.GlobalMapsDungeonMaps, dungeonArray);
        mem.WriteI32(cityArray + Il2Cpp.ArrayLengthOffset, 1);
        mem.WriteI32(dungeonArray + Il2Cpp.ArrayLengthOffset, 16);
        mem.WritePtr(Il2Cpp.ArrayElement(cityArray, 0), cityDesc);
        mem.WritePtr(Il2Cpp.ArrayElement(dungeonArray, 0), dungeonDesc);
        mem.WritePtr(cityDesc + MapFormat.MapDescName, cityNameString);
        mem.WritePtr(dungeonDesc + MapFormat.MapDescName, dungeonNameString);
        ManagedString(mem, cityNameString, "Skara Brae");
        ManagedString(mem, dungeonNameString, "Cellars");
        mem.WriteI32(dungeonDesc + MapFormat.MapDescWidth, 22);
        mem.WriteI32(dungeonDesc + MapFormat.MapDescHeight, 22);

        // A one-member party. m_members holds PartyMember wrappers, each pointing at a
        // Character; the second slot is an empty wrapper, as it is for a short party.
        mem.WritePtr(party + CharacterFormat.PartyMembers, members);
        mem.WriteI64(party + CharacterFormat.PartyGold, 25_000);
        mem.WriteI32(members + Il2Cpp.ArrayLengthOffset, 2);
        mem.WritePtr(Il2Cpp.ArrayElement(members, 0), partyMember);
        mem.WritePtr(partyMember + CharacterFormat.PartyMemberCharacter, character);
        mem.Write(character, PlausibleCharacter());

        return new SyntheticWorld
        {
            Memory = mem,
            ModuleBase = moduleBase,
            ModuleSize = slotPage + slotPageSize,
            PlayerClass = playerClass,
            PartyClass = partyClass,
            GlobalMapsClass = globalMapsClass,
            TeleportTargetClass = teleportClass,
            GlobalSpellsClass = globalSpellsClass,
            Player = player,
            Party = party,
            GlobalMaps = globalMaps,
            Character = character,
        };
    }

    /// <summary>Lays out an IL2CPP string; shared with the checks in <see cref="Program"/>.</summary>
    internal static void ManagedString(FakeMemorySource mem, nuint at, string value)
    {
        mem.WriteI32(at + Il2Cpp.StringLengthOffset, value.Length);
        mem.Write(at + Il2Cpp.StringCharsOffset, Encoding.Unicode.GetBytes(value));
    }

    private static byte[] PlausibleCharacter()
    {
        var buf = new byte[CharacterFormat.ObjectSize];
        BitConverter.GetBytes(60_000L).CopyTo(buf, CharacterFormat.OffExperience);
        BitConverter.GetBytes(500L).CopyTo(buf, CharacterFormat.OffGold);
        BitConverter.GetBytes(7).CopyTo(buf, CharacterFormat.OffLevel);
        BitConverter.GetBytes(64).CopyTo(buf, CharacterFormat.OffHpMax);
        BitConverter.GetBytes(64).CopyTo(buf, CharacterFormat.OffHpCur);
        BitConverter.GetBytes(20).CopyTo(buf, CharacterFormat.OffSpMax);
        BitConverter.GetBytes(20).CopyTo(buf, CharacterFormat.OffSpCur);
        BitConverter.GetBytes(3).CopyTo(buf, CharacterFormat.OffClass);
        for (int i = 0; i < CharacterFormat.StatCount; i++)
            BitConverter.GetBytes(17).CopyTo(buf, CharacterFormat.OffStrength + i * 4);
        return buf;
    }
}
