// Headless verification harness for the Legend of Grimrock trainer.
//
// Needs no running game and no copyrighted files. Everything is asserted either against constants
// pinned to what the reverse engineering established, or against synthetic LuaJIT heaps built by
// FakeHeap — including the failure cases a live game cannot be asked to produce: a stale static
// pointer, a coroutine masquerading as the main thread, a relocated module, a heap with an
// unreadable hole in it, and a process with no Lua VM at all.
//
//   dotnet run --project test\FormatCheck

using FormatCheck;
using LegendOfGrimrock1Trainer.Game;
using LegendOfGrimrock1Trainer.Lua;
using LegendOfGrimrock1Trainer.ViewModels;

int checks = 0, failures = 0;

void Check(string name, bool condition)
{
    checks++;
    if (condition) return;
    failures++;
    Console.WriteLine($"  FAIL  {name}");
}

void Equal<T>(string name, T actual, T expected)
{
    checks++;
    if (EqualityComparer<T>.Default.Equals(actual, expected)) return;
    failures++;
    Console.WriteLine($"  FAIL  {name}: expected {expected}, got {actual}");
}

void Close(string name, double actual, double expected, double tolerance = 1e-9)
{
    checks++;
    if (Math.Abs(actual - expected) <= tolerance) return;
    failures++;
    Console.WriteLine($"  FAIL  {name}: expected {expected}, got {actual}");
}

void Group(string title) => Console.WriteLine(title);

// =================================================================================================
Group("Build fingerprint and module facts");
// =================================================================================================

Equal("process name", GameFacts.ProcessName, "grimrock");
Equal("preferred image base", GameFacts.PreferredImageBase, 0x00400000u);
Equal("known file size", GameFacts.KnownFileSize, 1_804_800L);
Equal("known PE timestamp", GameFacts.KnownTimeDateStamp, 0x5115140Bu);
Equal("game version", GameFacts.KnownGameVersion, "1.3.7");
Equal("LuaJIT version", GameFacts.LuaJitVersion, "LuaJIT 2.0.0-beta9");
Equal("party size", GameFacts.PartySize, 4);
Equal("campaign levels", GameFacts.CampaignLevels, 13);
Equal("max skill level", GameFacts.MaxSkillLevel, 50);
Check("target hints are lower-case for case-insensitive matching",
    GameFacts.TargetHints.All(h => h == h.ToLowerInvariant()));

// The one module-relative constant, restated as the absolute VA the Ghidra teardown reported so a
// mistyped RVA cannot silently point the fast path somewhere else.
Equal("static lua_State slot", GrimrockLayout.LuaStateSlotRva, 0x00588AB8u - GameFacts.PreferredImageBase);

// =================================================================================================
Group("LuaJIT 2.0 (32-bit) object layout");
// =================================================================================================
// These are properties of LuaJIT, not of Grimrock. Restated as the arithmetic that produces them so
// a transcription slip shows up here rather than as a garbage read against a live game.

Equal("TValue size", LuaLayout.TValueSize, 8);
Equal("string tag", LuaLayout.ItString, ~4u);
Equal("table tag", LuaLayout.ItTable, ~11u);
Equal("thread tag", LuaLayout.ItThread, ~6u);
Equal("userdata tag", LuaLayout.ItUserData, ~12u);
Equal("nil tag", LuaLayout.ItNil, ~0u);
Equal("gct of a string", LuaLayout.GcTypeString, (byte)4);
Equal("gct of a table", LuaLayout.GcTypeTable, (byte)11);
Equal("gct of a thread", LuaLayout.GcTypeThread, (byte)6);
Equal("GCstr header size", LuaLayout.StringHeaderSize, 16);
Equal("GCstr len offset", LuaLayout.StringLength, 12);
Equal("GCtab size", LuaLayout.TableSize, 32);
Equal("GCtab node offset", LuaLayout.TableNode, 20);
Equal("GCtab hmask offset", LuaLayout.TableHashMask, 28);
Equal("Node size", LuaLayout.NodeSize, 24);
Equal("Node key offset", LuaLayout.NodeKey, 8);
Equal("lua_State size", LuaLayout.StateSize, 48);
Equal("lua_State env offset", LuaLayout.StateEnv, 36);
Equal("lua_State glref offset", LuaLayout.StateGlobalRef, 8);
Equal("GG_State delta is sizeof(lua_State)", LuaLayout.MainThreadGlobalStateDelta, 48);
Equal("number boundary is LuaJIT's LJ_TISNUM", LuaLayout.ItNumberBoundary, ~13u);
Check("the boundary sits below every real tag", LuaLayout.ItNumberBoundary < LuaLayout.ItUserData);
Check("every tag is above the number boundary",
    new[] { LuaLayout.ItNil, LuaLayout.ItFalse, LuaLayout.ItTrue, LuaLayout.ItLightUserData,
            LuaLayout.ItString, LuaLayout.ItUpValue, LuaLayout.ItThread, LuaLayout.ItProto,
            LuaLayout.ItFunction, LuaLayout.ItTrace, LuaLayout.ItCData, LuaLayout.ItTable,
            LuaLayout.ItUserData }.All(t => t >= LuaLayout.ItNumberBoundary));

// A double's high word must stay below the boundary or a number would parse as a tag. Because the
// boundary is LuaJIT's own, this holds at the edges too: negative infinity's high word is 0xFFF00000
// and a negative NaN's is 0xFFF80000, both still under 0xFFFFFFF2.
foreach (var probe in new[] { 0.0, -0.0, 1.0, -1.0, 850.0, 1e308, -1e308, double.Epsilon,
                              double.PositiveInfinity, double.NegativeInfinity, double.NaN })
{
    uint hi = BitConverter.ToUInt32(BitConverter.GetBytes(probe), 4);
    Check($"double {probe:g3} parses as a number", hi < LuaLayout.ItNumberBoundary);
    Equal($"double {probe:g3} round-trips as a number",
        LuaValue.Parse(BitConverter.GetBytes(probe), 0, 0).Kind, LuaKind.Number);
}

// =================================================================================================
Group("TValue parsing");
// =================================================================================================

{
    var buf = new byte[8];
    BitConverter.GetBytes(123.5).CopyTo(buf, 0);
    var number = LuaValue.Parse(buf, 0, 0x1000);
    Equal("number kind", number.Kind, LuaKind.Number);
    Close("number value", number.Number, 123.5);
    Equal("number carries its slot", number.Slot, 0x1000u);
    Equal("AsInt rounds away from zero", LuaValue.Parse(BitConverter.GetBytes(2.5), 0, 0).AsInt(), 3);
    Equal("AsInt rounds negatives away from zero", LuaValue.Parse(BitConverter.GetBytes(-2.5), 0, 0).AsInt(), -3);

    BitConverter.GetBytes(0xCAFEu).CopyTo(buf, 0);
    BitConverter.GetBytes(LuaLayout.ItString).CopyTo(buf, 4);
    var str = LuaValue.Parse(buf, 0, 0);
    Equal("string kind", str.Kind, LuaKind.String);
    Equal("string reference", str.Reference, 0xCAFEu);

    BitConverter.GetBytes(LuaLayout.ItTrue).CopyTo(buf, 4);
    Equal("true is a boolean", LuaValue.Parse(buf, 0, 0).Kind, LuaKind.Boolean);
    Check("true reads true", LuaValue.Parse(buf, 0, 0).AsBool());
    BitConverter.GetBytes(LuaLayout.ItFalse).CopyTo(buf, 4);
    Check("false reads false", !LuaValue.Parse(buf, 0, 0).AsBool());

    BitConverter.GetBytes(LuaLayout.ItNil).CopyTo(buf, 4);
    Equal("nil kind", LuaValue.Parse(buf, 0, 0).Kind, LuaKind.Nil);

    Equal("a short buffer is unreadable", LuaValue.Parse(new byte[4], 0, 0).Kind, LuaKind.Unreadable);
    Equal("an out-of-range offset is unreadable", LuaValue.Parse(new byte[8], 4, 0).Kind, LuaKind.Unreadable);
    Equal("AsNumber falls back for a non-number", LuaValue.Parse(buf, 0, 0).AsNumber(-1), -1);
}

// =================================================================================================
Group("Heap reader over a synthetic LuaJIT heap");
// =================================================================================================

{
    var heap = FakeHeap.BuildGame();
    var lua = new LuaHeap(heap);

    Check("globals parse as a table", lua.TryReadTable(heap.Globals, out var globals));
    Equal("_G points back at itself", lua.GetField(globals, "_G").Reference, heap.Globals);
    Equal("_VERSION reads Lua 5.1", lua.StringOf(lua.GetField(globals, "_VERSION")), "Lua 5.1");
    Check("a missing key is nil", lua.GetField(globals, "definitely_not_here").Kind == LuaKind.Nil);

    Check("party resolves", lua.GetField(globals, "party").IsTable);
    var champions = lua.GetPath(heap.Globals, "party", "champions");
    Check("a key path resolves", champions.IsTable);
    Check("champions[1] is a table", lua.GetIndex(champions.Reference, 1).IsTable);
    Check("champions[99] is nil", lua.GetIndex(champions.Reference, 99).Kind == LuaKind.Nil);

    lua.TryReadTable(champions, out var roster);
    Equal("sequence length stops at the first gap", lua.SequenceLength(roster), GameFacts.PartySize);

    // Enumeration must see array and hash entries, and must skip nils.
    var firstChampion = lua.GetIndex(champions.Reference, 1);
    lua.TryReadTable(firstChampion, out var champion);
    var keys = lua.Entries(champion).Select(e => lua.StringOf(e.Key)).Where(k => k is not null).ToList();
    Check("enumeration finds the champion's name key", keys.Contains("name"));
    Check("enumeration finds the champion's stats key", keys.Contains("stats"));

    // A pointer that is not a table, and one that is not readable at all.
    Check("a non-table address is rejected", !lua.TryReadTable(heap.LuaState, out _));
    Check("an unmapped address is rejected", !lua.TryReadTable(0x7000_0000, out _));
    Check("an unmapped string reads null", lua.ReadString(0x7000_0000) is null);
}

// =================================================================================================
Group("Locator — static-pointer chain");
// =================================================================================================

{
    var heap = FakeHeap.BuildGame();
    var lua = new LuaHeap(heap);
    var result = new GameLocator(heap, lua).Locate();

    Check("locates", result.Found);
    Equal("uses the static pointer when it is good", result.Chain, LocateChain.StaticPointer);
    Equal("finds the right lua_State", result.LuaState, heap.LuaState);
    Equal("finds the right globals", result.Globals, heap.Globals);
    Equal("no scanning was needed", result.RegionsScanned, 0);
    Check("reports the matching build", result.BuildMatches);
}

// =================================================================================================
Group("Locator — falls back to the heap scan");
// =================================================================================================

{
    // The static slot points at garbage: the scan must still find the VM.
    var heap = FakeHeap.BuildGame(staticPointerValid: false);
    var result = new GameLocator(heap, new LuaHeap(heap)).Locate();
    Check("locates despite a stale static pointer", result.Found);
    Equal("falls through to the signature scan", result.Chain, LocateChain.HeapSignature);
    Equal("still finds the right lua_State", result.LuaState, heap.LuaState);
    Check("the scan actually swept memory", result.BytesScanned > 0);
}

{
    // The static slot points at a real object that is not a thread.
    var heap = FakeHeap.BuildGame();
    heap.SetStaticStatePointer(heap.Globals);
    var result = new GameLocator(heap, new LuaHeap(heap)).Locate();
    Check("a static pointer aimed at a table is refused", result.Chain == LocateChain.HeapSignature);
    Equal("and the scan finds the real state", result.LuaState, heap.LuaState);
}

{
    // The static slot points at the decoy coroutine: right gct, wrong glref.
    var heap = FakeHeap.BuildGame();
    uint decoy = heap.NewThread(heap.Globals, mainThread: false);
    heap.SetStaticStatePointer(decoy);
    var result = new GameLocator(heap, new LuaHeap(heap)).Locate();
    Check("a coroutine is not mistaken for the main thread", result.LuaState != decoy);
    Equal("the main thread is found instead", result.LuaState, heap.LuaState);
}

{
    // No writable data section covering the slot: the shortcut must not even be attempted.
    var heap = new FakeHeap();
    heap.BuildModule(0x00990000, includeDataSection: false);
    heap.BuildGlobals();
    uint state = heap.NewThread(heap.Globals, mainThread: true);
    heap.SetStaticStatePointer(state);
    var result = new GameLocator(heap, new LuaHeap(heap)).Locate();
    Check("locates without the static chain", result.Found);
    Equal("because the slot is not in writable data", result.Chain, LocateChain.HeapSignature);
}

// =================================================================================================
Group("Locator — negative cases");
// =================================================================================================

{
    // A heap with threads but no globals table that validates.
    var heap = new FakeHeap();
    heap.BuildModule(0x00990000);
    uint bogus = heap.NewTable(0, 4);       // a table, but not a globals table
    heap.NewThread(bogus, mainThread: true);
    var result = new GameLocator(heap, new LuaHeap(heap)).Locate();
    Check("a thread whose env is not _G is refused", !result.Found);
    Equal("and nothing is reported as located", result.Chain, LocateChain.None);
}

{
    // Globals that self-reference but carry the wrong Lua version.
    var heap = new FakeHeap();
    heap.BuildModule(0x00990000);
    uint g = heap.NewTable(0, 32);
    heap.SetTable(g, "_G", g);
    heap.SetString(g, "_VERSION", "Lua 5.4");
    foreach (var cls in GrimrockLayout.EngineClassKeys) heap.SetTable(g, cls, heap.NewTable(0, 2));
    heap.NewThread(g, mainThread: true);
    Check("the wrong Lua version is refused", !new GameLocator(heap, new LuaHeap(heap)).Locate().Found);
}

{
    // Globals that look like Lua but are not Grimrock: no engine class tables.
    var heap = new FakeHeap();
    heap.BuildModule(0x00990000);
    uint g = heap.NewTable(0, 32);
    heap.SetTable(g, "_G", g);
    heap.SetString(g, "_VERSION", "Lua 5.1");
    heap.NewThread(g, mainThread: true);
    Check("a Lua host that is not Grimrock is refused", !new GameLocator(heap, new LuaHeap(heap)).Locate().Found);
}

{
    // Empty process: no thread objects at all.
    var heap = new FakeHeap();
    heap.BuildModule(0x00990000);
    var result = new GameLocator(heap, new LuaHeap(heap)).Locate();
    Check("an empty process locates nothing", !result.Found);
    Check("and says so", result.Detail.Contains("no LuaJIT main thread", StringComparison.Ordinal));
}

{
    // A header that will not parse is a refusal for the shortcut, not a waiver of the section check:
    // an unverifiable fast path is worth less than the sweep that does not need one.
    var heap = FakeHeap.BuildGame();
    heap.CorruptModuleHeader();
    var result = new GameLocator(heap, new LuaHeap(heap)).Locate();
    Check("locates without a readable PE header", result.Found);
    Equal("but not through the unverifiable shortcut", result.Chain, LocateChain.HeapSignature);
}

{
    // A 64-bit module: the whole Lua layer assumes 32-bit object sizes, so say so rather than sweep.
    var heap = FakeHeap.BuildGame();
    heap.SetModuleMachine(0x8664);
    var result = new GameLocator(heap, new LuaHeap(heap)).Locate();
    Check("a non-x86 image is refused", !result.Found);
    Check("and the reason names the machine type",
        result.Detail.Contains("32-bit x86", StringComparison.Ordinal));
    Equal("without sweeping a byte", result.BytesScanned, 0L);
}

{
    // A module relocated somewhere else entirely: ASLR is real, so this must still work.
    var heap = FakeHeap.BuildGame(moduleBase: 0x1A30_0000);
    var result = new GameLocator(heap, new LuaHeap(heap)).Locate();
    Check("locates with the module relocated", result.Found);
    Equal("and reports the relocated base", result.ModuleBase, 0x1A30_0000u);
}

// =================================================================================================
Group("Party reader");
// =================================================================================================

{
    var heap = FakeHeap.BuildGame();
    var lua = new LuaHeap(heap);
    var reader = new LegendOfGrimrock1Trainer.Game.PartyReader(lua);
    var party = reader.ReadParty(heap.Globals);

    Check("party reads", party is not null);
    Equal("level", party!.Level, 1);
    Equal("x", party.X, 2);
    Equal("y", party.Y, 8);
    Equal("facing", party.Facing, 0);
    Equal("champion count", party.Champions.Count, GameFacts.PartySize);

    var first = party.Champions[0];
    Equal("champion name", first.Name, "Champion 1");
    Equal("champion race", first.Race, "Human");
    Equal("champion class", first.ClassName, "Fighter");
    Check("champion is enabled", first.Enabled);
    Equal("champion level", first.Level, 1);
    Equal("next level threshold", first.NextLevel, 850d);
    Equal("champion food", first.Food, 750d);
    Equal("stat count", first.Stats.Count, GameTables.Stats.Length);
    Equal("condition count", first.Conditions.Count, GameTables.Conditions.Length);
    Equal("skill count", first.Skills.Count, 3);
    Equal("talents", string.Join(",", first.Talents), "athletic");

    Equal("stats are in the game's order", first.Stats[0].Name, "health");
    Equal("second stat", first.Stats[1].Name, "energy");
    Close("health value", first.Stat("health")!.Value, 61);
    Close("health max", first.Stat("health")!.Max, 61);
    Check("every stat carries a writable slot", first.Stats.All(s => s.ValueSlot != 0 && s.MaxSlot != 0));

    Equal("poison is set on the fixture", first.Condition("poison")!.Value, 1d);
    Equal("poison timer", first.Condition("poison")!.Timer, 30d);
    Equal("poison is harmful", first.Condition("poison")!.Kind, ConditionKind.Harmful);
    Equal("haste is beneficial", first.Condition("haste")!.Kind, ConditionKind.Beneficial);
    Equal("level-up marker is neutral", first.Condition("unused_skill_points")!.Kind, ConditionKind.Neutral);

    Equal("skills keep the champion's own order", first.Skills[2].Name, "swords");
    Equal("skill level", first.Skills[2].Level, 3);
    Equal("skill labels come from the table", first.Skills[2].UiName, "Swords");

    Equal("statistics count", party.Statistics.Count, 2);
    Equal("statistic label", party.Statistics[0].UiName, "Play Time");
    Equal("map count", party.Maps.Count, 1);
    Equal("map name", party.Maps[0].Name, "Into the Dark");
    Equal("map size", $"{party.Maps[0].Width}x{party.Maps[0].Height}", "4x4");
    Check("current map resolves", party.CurrentMap is not null);
}

{
    // Attached, but at the main menu: the party global simply is not there.
    var heap = FakeHeap.BuildGame(withGame: false);
    var reader = new LegendOfGrimrock1Trainer.Game.PartyReader(new LuaHeap(heap));
    Check("no party at the main menu", reader.ReadParty(heap.Globals) is null);
}

{
    // A party of fewer than four champions (the game allows a slot to be empty during creation).
    var heap = FakeHeap.BuildGame(champions: 2);
    var reader = new LegendOfGrimrock1Trainer.Game.PartyReader(new LuaHeap(heap));
    Equal("a short roster reads short", reader.ReadParty(heap.Globals)!.Champions.Count, 2);
}

// =================================================================================================
Group("Edits — read, validate, write");
// =================================================================================================

{
    var heap = FakeHeap.BuildGame();
    var lua = new LuaHeap(heap);
    var reader = new LegendOfGrimrock1Trainer.Game.PartyReader(lua);
    var actions = new TrainerActions(reader);
    var party = reader.ReadParty(heap.Globals)!;
    var champion = party.Champions[0];

    // Damage the champion, then restore.
    reader.Write(champion.Stat("health")!.ValueSlot, 5);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Close("health took the poke", champion.Stat("health")!.Value, 5);
    var restored = actions.Restore(champion);
    Check("restore reports success", restored.Complete);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Close("restore puts health back to max", champion.Stat("health")!.Value, 61);
    Close("restore puts energy back to max", champion.Stat("energy")!.Value, 41);

    // SetStat raises a bar's cap so the value cannot overflow its own track...
    actions.SetStat(champion, "health", 400);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Close("health value", champion.Stat("health")!.Value, 400);
    Close("health max follows the value up", champion.Stat("health")!.Max, 400);

    // ...but never lowers it. Dropping current health must not throw away a maximum of 400, which is
    // the one edit here nothing in the game could undo — and Grimrock autosaves.
    actions.SetStat(champion, "health", 30);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Close("health value came down", champion.Stat("health")!.Value, 30);
    Close("health max was left alone", champion.Stat("health")!.Max, 400);
    actions.SetStat(champion, "energy", 5);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Close("energy max was left alone too", champion.Stat("energy")!.Max, 41);

    // A score is not a bar: Grimrock holds the same number in both fields, so both move.
    actions.SetStat(champion, "strength", 30);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Close("a score's value moves", champion.Stat("strength")!.Value, 30);
    Close("and its max moves with it", champion.Stat("strength")!.Max, 30);
    actions.SetStat(champion, "strength", 8);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Close("a score comes down too", champion.Stat("strength")!.Max, 8);
    actions.SetStat(champion, "health", 400);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];

    // And it clamps rather than writing anything the sheet cannot draw.
    actions.SetStat(champion, "strength", 1e9);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Close("an absurd stat is clamped", champion.Stat("strength")!.Value, GameFacts.MaxStatValue);
    actions.SetStat(champion, "strength", -5);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Close("a negative stat is clamped to zero", champion.Stat("strength")!.Value, 0);
    Close("and a score's cap follows it to zero, not to one", champion.Stat("strength")!.Max, 0);
    actions.SetStat(champion, "health", 0);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Check("but a bar's cap keeps a floor of at least 1", champion.Stat("health")!.Max >= 1);
    actions.SetStat(champion, "health", 61);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];

    // Cure clears harmful conditions and leaves the rest alone.
    var cured = actions.Cure(champion);
    Check("cure wrote something", cured.Applied > 0);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Equal("poison is gone", champion.Condition("poison")!.Value, 0d);
    Equal("poison timer is gone", champion.Condition("poison")!.Timer, 0d);
    Check("curing an already-clean champion reports nothing to do", actions.Cure(champion).Attempted == 0);

    // Bless sets the beneficial ones with a timer.
    var blessed = actions.Bless(champion, 120);
    Check("bless wrote something", blessed.Applied > 0);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Equal("haste is on", champion.Condition("haste")!.Value, 1d);
    Equal("haste has the requested timer", champion.Condition("haste")!.Timer, 120d);
    Equal("burdened is not a bless target", champion.Condition("burdened")!.Value, 0d);
    Check("a non-positive duration is refused", actions.Bless(champion, 0).Attempted == 0);

    // A condition whose slots did not resolve must not be counted as applied — a status line that
    // says "4/4 written" when nothing was written is worse than one that says nothing happened.
    var unwritable = new ChampionSnapshot
    {
        Index = 1,
        Name = "Ghost",
        Conditions = new[] { new ConditionSnapshot("poison", "Poisoned", ConditionKind.Harmful, 1, 30, 0, 0) },
    };
    Check("a harmful condition with no slots is not claimed as cured", actions.Cure(unwritable).Attempted == 0);
    var unblessable = new ChampionSnapshot
    {
        Index = 1,
        Name = "Ghost",
        Conditions = new[] { new ConditionSnapshot("haste", "Hastened", ConditionKind.Beneficial, 0, 0, 0, 0) },
    };
    Check("a beneficial condition with no slots is not claimed as set", actions.Bless(unblessable, 60).Attempted == 0);

    // Food and skill points.
    actions.SetFood(champion, 2000);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Close("food is clamped to the bar", champion.Food, GameFacts.MaxFood);
    actions.SetSkillPoints(champion, 7);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Equal("skill points", champion.SkillPoints, 7);
    Equal("the Level Up badge follows the points", champion.Condition("unused_skill_points")!.Value, 1d);
    actions.SetSkillPoints(champion, 0);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Equal("and clears when they are spent", champion.Condition("unused_skill_points")!.Value, 0d);
    var granted = actions.SetSkillPoints(champion, 5000);
    Check("an absurd grant reports the clamped number, not the request",
        granted.Summary.Contains($"has {TrainerActions.MaxSkillPoints} ", StringComparison.Ordinal));
    Equal("and counts the badge write it also made", granted.Attempted, 2);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Equal("skill points are clamped", champion.SkillPoints, TrainerActions.MaxSkillPoints);
    actions.SetSkillPoints(champion, 0);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];

    // Skills clamp to the game's own ceiling.
    var skilled = actions.SetSkill(champion.Skills[0], 999);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Equal("a skill is clamped to 50", champion.Skills[0].Level, GameFacts.MaxSkillLevel);
    Check("and the clamped level is what is reported",
        skilled.Summary.Contains("= 50", StringComparison.Ordinal));
    var xp = actions.SetExperience(champion, -100);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Close("negative experience is clamped to zero", champion.Experience, 0);
    Check("and zero is what is reported", xp.Summary.Contains("has 0 XP", StringComparison.Ordinal));
    actions.SetExperience(champion, 12345);

    // Level and experience.
    actions.SetLevel(champion, 12);
    actions.SetExperience(champion, 12345);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Equal("level", champion.Level, 12);
    Close("experience", champion.Experience, 12345);
    var levelled = actions.SetLevel(champion, 0);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Equal("level is clamped to at least 1", champion.Level, 1);
    Check("and the clamped level is what is reported",
        levelled.Summary.Contains("is level 1", StringComparison.Ordinal));
    Close("setting a level leaves experience alone", champion.Experience, 12345);

    // MaxStats leaves anything already above the target alone.
    actions.SetStat(champion, "willpower", 500);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    actions.MaxStats(champion, 100);
    champion = reader.ReadParty(heap.Globals)!.Champions[0];
    Close("a stat above the target is untouched", champion.Stat("willpower")!.Value, 500);
    Close("a stat below the target is raised", champion.Stat("vitality")!.Value, 100);

    // A snapshot whose slots are zero must be refused rather than writing to address 0.
    var orphan = new ChampionSnapshot { Index = 9, Name = "Ghost" };
    Check("an empty snapshot cannot be fed", actions.SetFood(orphan, 100).Attempted == 0);
    Check("an empty snapshot cannot be levelled", actions.SetLevel(orphan, 5).Attempted == 0);
    Check("an empty snapshot has no stat to set", actions.SetStat(orphan, "health", 5).Attempted == 0);
}

// =================================================================================================
Group("Party-wide actions");
// =================================================================================================

{
    var heap = FakeHeap.BuildGame();
    var reader = new LegendOfGrimrock1Trainer.Game.PartyReader(new LuaHeap(heap));
    var actions = new TrainerActions(reader);
    var party = reader.ReadParty(heap.Globals)!;

    var result = actions.ForEachChampion(party, actions.Cure, "cured the party");
    Check("every champion was cured", result.Complete);
    party = reader.ReadParty(heap.Globals)!;
    Check("no champion is still poisoned", party.Champions.All(c => c.Condition("poison")!.Value == 0));

    party = reader.ReadParty(heap.Globals)!;
    actions.ForEachChampion(party, c => actions.MaxStats(c, 250), "maxed the party");
    party = reader.ReadParty(heap.Globals)!;
    Check("every stat reached the target",
        party.Champions.All(c => c.Stats.All(s => s.Value >= 250)));
}

// =================================================================================================
Group("Freeze — the write side");
// =================================================================================================
// The display side lives on the row view-model; this is the part that actually touches the game, and
// it is why FreezeWriter is a separate static rather than a private method on the session, which
// nothing without a WPF dispatcher could reach.

{
    var heap = FakeHeap.BuildGame();
    var reader = new LegendOfGrimrock1Trainer.Game.PartyReader(new LuaHeap(heap));
    var actions = new TrainerActions(reader);
    var party = reader.ReadParty(heap.Globals)!;

    var held = new[] { ("health", 500d) };
    Equal("a stat away from its target is written", FreezeWriter.Apply(actions, party, 1, held), 1);
    party = reader.ReadParty(heap.Globals)!;
    Close("and reaches the target", party.Champions[0].Stat("health")!.Value, 500);
    Close("with the cap raised to fit", party.Champions[0].Stat("health")!.Max, 500);

    Equal("a stat already at its target is left alone", FreezeWriter.Apply(actions, party, 1, held), 0);

    // Drift smaller than the tolerance is not worth four writes a second; real damage is.
    reader.Write(party.Champions[0].Stat("health")!.ValueSlot, 500 - FreezeWriter.Tolerance / 2);
    party = reader.ReadParty(heap.Globals)!;
    Equal("drift within the tolerance is ignored", FreezeWriter.Apply(actions, party, 1, held), 0);
    reader.Write(party.Champions[0].Stat("health")!.ValueSlot, 120);
    party = reader.ReadParty(heap.Globals)!;
    Equal("real damage is undone", FreezeWriter.Apply(actions, party, 1, held), 1);
    party = reader.ReadParty(heap.Globals)!;
    Close("back to the frozen value", party.Champions[0].Stat("health")!.Value, 500);

    Equal("an unknown stat name writes nothing",
        FreezeWriter.Apply(actions, party, 1, new[] { ("not_a_stat", 1d) }), 0);
    Equal("an absent champion writes nothing", FreezeWriter.Apply(actions, party, 99, held), 0);
    Equal("an empty freeze set writes nothing",
        FreezeWriter.Apply(actions, party, 1, Array.Empty<(string, double)>()), 0);
}

// =================================================================================================
Group("Map edits");
// =================================================================================================

{
    var heap = FakeHeap.BuildGame();
    var reader = new LegendOfGrimrock1Trainer.Game.PartyReader(new LuaHeap(heap));
    var actions = new TrainerActions(reader);
    var party = reader.ReadParty(heap.Globals)!;
    var map = party.Maps[0];

    Equal("cell index arithmetic", GrimrockLayout.CellIndex(2, 8, 32), 8 * 32 + 2 + 1);
    Equal("cell index of the origin", GrimrockLayout.CellIndex(0, 0, 32), 1);
    Equal("an index that would overflow is refused",
        GrimrockLayout.CellIndex(99_999, 99_999, 100_000), -1);
    Check("and the teleport refuses a map that size",
        actions.Teleport(party, map with { Width = 100_000, Height = 100_000 }, 1, 1).Attempted == 0);
    Check("the border is a wall", !actions.IsWalkable(map, 0, 0));
    Check("the middle is open", actions.IsWalkable(map, 1, 1));
    Check("outside the map is not walkable", !actions.IsWalkable(map, 99, 99));

    // The fixture's party sits at (2, 8), which is off this 4x4 map — the teleport must refuse
    // rather than write, because the tile it would be leaving is not on this map at all.
    Check("a destination outside the map is refused", actions.Teleport(party, map, 9, 9).Attempted == 0);
    Check("a wall is refused", actions.Teleport(party, map, 0, 0).Attempted == 0);
    Check("a party standing off the map is refused", actions.Teleport(party, map, 1, 1).Attempted == 0);
    Check("and says which coordinates it distrusts",
        actions.Teleport(party, map, 1, 1).Summary.Contains("off this map", StringComparison.Ordinal));

    // Put the party somewhere legal first, then move it and watch the occupancy bit follow.
    reader.Write(party.XSlot, 1);
    reader.Write(party.YSlot, 1);
    actions.SetFacing(party, 1);
    party = reader.ReadParty(heap.Globals)!;
    Equal("facing was written", party.Facing, 1);
    Equal("facing wraps negatives", actions.SetFacing(party, -1).Applied, 1);
    party = reader.ReadParty(heap.Globals)!;
    Equal("and lands on west", party.Facing, 3);

    reader.WriteCell(map, 1, 1, (long)reader.ReadCell(map, 1, 1)!.Value | GrimrockLayout.CellBits.DynamicObstacle);
    Check("teleporting to where the party already is is refused",
        actions.Teleport(party, map, 1, 1).Attempted == 0);
    var moved = actions.Teleport(party, map, 2, 2);
    Check("the teleport applied every write", moved.Complete);
    party = reader.ReadParty(heap.Globals)!;
    Equal("party x moved", party.X, 2);
    Equal("party y moved", party.Y, 2);
    Check("the tile left behind is clear",
        ((long)reader.ReadCell(map, 1, 1)!.Value & GrimrockLayout.CellBits.DynamicObstacle) == 0);
    Check("the tile entered is occupied",
        ((long)reader.ReadCell(map, 2, 2)!.Value & GrimrockLayout.CellBits.DynamicObstacle) != 0);
    Check("and the tile it left kept its own bits",
        ((long)reader.ReadCell(map, 1, 1)!.Value & GrimrockLayout.CellBits.Pit) != 0);

    // Reveal sets only the automap bits, never touches a wall tile, and decides each seen-wall bit
    // from the neighbouring tile rather than setting all four everywhere.
    double wallBefore = reader.ReadCell(map, 0, 0)!.Value;
    long floorBefore = (long)reader.ReadCell(map, 1, 2)!.Value;
    Check("the fixture's floor really does carry non-automap bits it could lose",
        (floorBefore & (GrimrockLayout.CellBits.Pit | GrimrockLayout.CellBits.Pad)) != 0);
    var revealed = actions.RevealMap(map);
    Check("reveal wrote every tile it attempted", revealed.Complete);
    Close("a wall tile is untouched", reader.ReadCell(map, 0, 0)!.Value, wallBefore);

    long after = (long)reader.ReadCell(map, 1, 2)!.Value;
    Check("the floor tile is marked seen", (after & GrimrockLayout.CellBits.MapFloor) != 0);
    Check("and kept everything it already had", (after & floorBefore) == floorBefore);
    Check("and gained nothing but automap bits",
        (after & ~(floorBefore | GrimrockLayout.CellBits.MapAll)) == 0);

    // (1,2) is an interior tile of a 4x4 level: walls lie west and south, open floor north and east.
    Check("a wall to the west is recorded", (after & GrimrockLayout.CellBits.MapWallWest) != 0);
    Check("a wall to the south is recorded", (after & GrimrockLayout.CellBits.MapWallSouth) != 0);
    Check("open floor to the north is not claimed as a wall",
        (after & GrimrockLayout.CellBits.MapWallNorth) == 0);
    Check("open floor to the east is not claimed as a wall",
        (after & GrimrockLayout.CellBits.MapWallEast) == 0);
    Check("revealing twice reports nothing to do", actions.RevealMap(map).Attempted == 0);

    // Cross-level travel is refused outright.
    var elsewhere = map with { Level = 2 };
    Check("cross-level teleport is refused", actions.Teleport(party, elsewhere, 1, 1).Attempted == 0);

    // A torn read of width/height must not turn the sweep into an unbounded run of syscalls, nor an
    // allocation sized from a number the game did not really hold.
    var absurd = map with { Width = 100_000, Height = 100_000 };
    Check("an absurd map size is not plausible", !absurd.HasPlausibleSize);
    var refused = actions.RevealMap(absurd);
    Equal("and it is refused rather than swept", refused.Attempted, 0);
    Check("with a message naming the size it distrusts",
        refused.Summary.Contains("100000x100000", StringComparison.Ordinal));
    Check("a real map is plausible", map.HasPlausibleSize);
    Check("a map with no cell table is refused",
        actions.RevealMap(map with { CellsTable = 0 }).Attempted == 0);
}

// =================================================================================================
Group("Unreadable memory is survived, not crashed on");
// =================================================================================================

{
    var heap = FakeHeap.BuildGame();
    var lua = new LuaHeap(heap);
    var reader = new LegendOfGrimrock1Trainer.Game.PartyReader(lua);
    var party = reader.ReadParty(heap.Globals)!;
    Check("the party read before poisoning", party.Champions.Count == GameFacts.PartySize);

    Check("a read through an unmapped page returns empty", lua.Read(0x7FF0_0000, 32).Length == 0);
    Check("a value read from an unmapped page is unreadable",
        lua.ReadValue(0x7FF0_0000).Kind == LuaKind.Unreadable);
    Check("a write to an unmapped page fails cleanly", !lua.WriteNumber(0x7FF0_0000, 1));
    Check("a write to slot zero is refused", !lua.WriteNumber(0, 1));
}

{
    // A page that goes unreadable *inside* the heap, which is the hazard read-validate-write exists
    // for — not merely an address that was never mapped.
    var heap = FakeHeap.BuildGame();
    var lua = new LuaHeap(heap);
    var reader = new LegendOfGrimrock1Trainer.Game.PartyReader(lua);
    var actions = new TrainerActions(reader);

    var before = reader.ReadParty(heap.Globals);
    Check("the party read before the hole was punched", before is not null);

    heap.PoisonChampionStats(1);
    lua.ResetCache();

    var after = reader.ReadParty(heap.Globals);
    Check("the party still reads", after is not null);
    Equal("the champion with the unreadable stat table reports no stats",
        after!.Champions[0].Stats.Count, 0);
    Check("the other champions are unaffected", after.Champions[1].Stats.Count > 0);
    Check("and an edit against the damaged champion is refused rather than misdirected",
        actions.SetStat(after.Champions[0], "health", 100).Attempted == 0);
    Check("restoring it writes nothing", actions.Restore(after.Champions[0]).Attempted == 0);
}

{
    // A failed string read must not be memoised: a transient failure would otherwise unmatch that
    // key for the rest of the session and every write to it would silently become a no-op.
    var heap = FakeHeap.BuildGame();
    var lua = new LuaHeap(heap);
    uint text = heap.NewString("health");
    heap.Poison(text);
    Check("an unreadable string reads null", lua.ReadString(text) is null);
    heap.Revive(text);
    Equal("and reads correctly once the page comes back", lua.ReadString(text), "health");
}

// =================================================================================================
Group("Process picker");
// =================================================================================================

Equal("the game is an exact match", ProcessPicker.Rank("grimrock"), ProcessMatch.Exact);
Equal("case does not matter", ProcessPicker.Rank("Grimrock"), ProcessMatch.Exact);
Equal("the trainer itself is only a hint", ProcessPicker.Rank("GrimrockTrainer"), ProcessMatch.Hint);
Equal("an unrelated process does not match", ProcessPicker.Rank("explorer"), ProcessMatch.None);
Check("the trainer never offers itself", !ProcessPicker.IsSelectable(42, 42));
Check("another process is selectable", ProcessPicker.IsSelectable(43, 42));

{
    var entries = new List<ProcessEntry>
    {
        new(1, "explorer", ""),
        new(2, "GrimrockTrainer", "Legend of Grimrock — Trainer"),
        new(3, "grimrock", "Legend of Grimrock"),
    };
    var ordered = ProcessPicker.Order(entries, e => e.Match, e => e.Name).ToList();
    Equal("the game sorts first", ordered[0].Name, "grimrock");
    Equal("the trainer sorts second", ordered[1].Name, "GrimrockTrainer");

    var chosen = ProcessPicker.ChooseDefault(ordered, e => e.Match, e => e.Id, null);
    Equal("the default is the exact match, not the trainer", chosen?.Name, "grimrock");

    var sticky = ProcessPicker.ChooseDefault(ordered, e => e.Match, e => e.Id, 1);
    Equal("a previous selection survives a refresh", sticky?.Id, 1);

    var hintsOnly = ProcessPicker.Order(entries.Where(e => e.Match != ProcessMatch.Exact),
        e => e.Match, e => e.Name).ToList();
    Check("a hint-only match is never chosen automatically",
        ProcessPicker.ChooseDefault(hintsOnly, e => e.Match, e => e.Id, null) is null);

    Check("the display line carries the window title",
        entries[2].Display.Contains("Legend of Grimrock", StringComparison.Ordinal));
}

// =================================================================================================
Group("Row view-models");
// =================================================================================================

{
    var heap = FakeHeap.BuildGame();
    var lua = new LuaHeap(heap);
    var reader = new LegendOfGrimrock1Trainer.Game.PartyReader(lua);
    var host = new FakeHost(reader, heap.Globals);
    var actions = host.Actions!;
    var snapshot = reader.ReadParty(heap.Globals)!.Champions[0];

    var vm = new ChampionViewModel(host, snapshot);
    Equal("name", vm.Name, "Champion 1");
    Equal("description", vm.Description, "Human Fighter (male)");
    Equal("tab header", vm.TabHeader, "1. Champion 1");
    Equal("stat rows", vm.Stats.Count, GameTables.Stats.Length);
    Equal("skill rows", vm.Skills.Count, 3);
    Equal("condition rows", vm.Conditions.Count, GameTables.Conditions.Length);

    // An edit writes through.
    vm.Stats.First(s => s.Name == "strength").Value = 42;
    var after = reader.ReadParty(heap.Globals)!.Champions[0];
    Close("the stat edit reached the game", after.Stat("strength")!.Value, 42);
    Check("the edit asked for a refresh", host.RefreshRequests > 0);

    // Out of range is refused and reported.
    int before = host.RefreshRequests;
    vm.Stats.First(s => s.Name == "strength").Value = -1;
    Check("an out-of-range stat is refused", host.LastMessage.Contains("between", StringComparison.Ordinal));
    Equal("and nothing was written", host.RefreshRequests, before);

    // Read-only mode refuses every edit.
    host.WritesAllowed = false;
    vm.Stats.First(s => s.Name == "vitality").Value = 99;
    Check("read-only mode is honoured", host.LastMessage.Contains("Writes are disabled", StringComparison.Ordinal));
    after = reader.ReadParty(heap.Globals)!.Champions[0];
    Check("and the game was not touched", Math.Abs(after.Stat("vitality")!.Value - 99) > 0.5);
    host.WritesAllowed = true;

    // Update must not write values back as it pushes them in.
    int writesBefore = heap.Writes;
    vm.Update(reader.ReadParty(heap.Globals)!.Champions[0]);
    Equal("a refresh writes nothing", heap.Writes, writesBefore);

    // Conditions round-trip through the row.
    var haste = vm.Conditions.First(c => c.Name == "haste");
    haste.Timer = 90;
    after = reader.ReadParty(heap.Globals)!.Champions[0];
    Equal("the condition was armed", after.Condition("haste")!.Value, 1d);
    Equal("with the requested timer", after.Condition("haste")!.Timer, 90d);
    haste.Active = false;
    after = reader.ReadParty(heap.Globals)!.Champions[0];
    Equal("and cleared again", after.Condition("haste")!.Value, 0d);

    // Skill rows clamp.
    var swords = vm.Skills.First(s => s.Name == "swords");
    swords.Level = 5;
    after = reader.ReadParty(heap.Globals)!.Champions[0];
    Equal("the skill edit reached the game", after.Skills.First(s => s.Name == "swords").Level, 5);
    swords.Level = 999;
    Check("an out-of-range skill is refused", host.LastMessage.Contains("between 0 and 50", StringComparison.Ordinal));

    // Freeze: the target is latched when the box is ticked, and must survive the refreshes that
    // follow. Deriving it from the displayed value instead would make the "freeze" chase the game —
    // writing the frozen number one tick and the damaged number the next, forever.
    var health = vm.Stats.First(s => s.Name == "health");
    Check("nothing is frozen to begin with", !vm.FrozenStats.Any());
    actions.SetStat(reader.ReadParty(heap.Globals)!.Champions[0], "health", 100);
    vm.Update(reader.ReadParty(heap.Globals)!.Champions[0]);
    health.Frozen = true;
    Equal("one stat is frozen", vm.FrozenStats.Count(), 1);
    Equal("and it is the one that was ticked", vm.FrozenStats.First().Name, "health");
    Close("the target is the value it was ticked at", health.FreezeTarget, 100);

    // The game takes 40 damage and three refreshes go by.
    var damaged = reader.ReadParty(heap.Globals)!.Champions[0];
    reader.Write(damaged.Stat("health")!.ValueSlot, 60);
    for (int tick = 0; tick < 3; tick++)
        vm.Update(reader.ReadParty(heap.Globals)!.Champions[0]);
    Close("the freeze target did not follow the damage down", health.FreezeTarget, 100);
    Close("and the row still shows the frozen number", health.Value, 100);

    // Editing a frozen stat retargets the freeze rather than fighting it.
    health.Value = 250;
    Close("editing a frozen stat moves the target", health.FreezeTarget, 250);
    health.Frozen = false;
    Check("releasing clears it", !vm.FrozenStats.Any());

    // Freezes survive a rebuild of the row objects.
    health.Frozen = true;
    var rebuilt = new ChampionViewModel(host, reader.ReadParty(heap.Globals)!.Champions[0]);
    rebuilt.RestoreFreezes(vm.FrozenStats);
    Equal("a rebuilt champion keeps its freezes", rebuilt.FrozenStats.Count(), 1);
    Close("with the same target", rebuilt.FrozenStats.First().Value, 250);

    // Read-only mode refuses a freeze too, rather than claiming one is in effect.
    host.WritesAllowed = false;
    var evasion = vm.Stats.First(s => s.Name == "evasion");
    evasion.Frozen = true;
    Check("freezing is refused in read-only mode", !evasion.Frozen);
    Check("and says so", host.LastMessage.Contains("Writes are disabled", StringComparison.Ordinal));
    host.WritesAllowed = true;

    // An untimed condition has no duration to set: writing one would switch the condition on.
    var overloaded = vm.Conditions.First(c => c.Name == "overloaded");
    Check("overloaded is not a timed condition", !overloaded.IsTimed);
    overloaded.Timer = 120;
    Check("setting its timer is refused", host.LastMessage.Contains("no timer", StringComparison.Ordinal));
    after = reader.ReadParty(heap.Globals)!.Champions[0];
    Equal("and the condition was not switched on", after.Condition("overloaded")!.Value, 0d);

    // A refresh must not clobber a value the user is part-way through typing.
    host.EditorHasFocus = true;
    var champBefore = reader.ReadParty(heap.Globals)!.Champions[0];
    reader.Write(champBefore.FoodSlot, 123);
    double shown = vm.Food;
    vm.Update(reader.ReadParty(heap.Globals)!.Champions[0]);
    Close("an editor with focus is not overwritten", vm.Food, shown);
    host.EditorHasFocus = false;
    vm.Update(reader.ReadParty(heap.Globals)!.Champions[0]);
    Close("and catches up once focus leaves", vm.Food, 123);
}

// =================================================================================================
Group("A refused edit leaves nothing behind");
// =================================================================================================

{
    // The champion's stat table is unreadable, so every edit against it must be refused *and* the
    // bound value must go back to what it was — a grid showing a number the game never received is
    // the failure mode the whole read-validate-write discipline exists to avoid.
    var heap = FakeHeap.BuildGame();
    var lua = new LuaHeap(heap);
    var reader = new LegendOfGrimrock1Trainer.Game.PartyReader(lua);
    var host = new FakeHost(reader, heap.Globals);
    var vm = new ChampionViewModel(host, reader.ReadParty(heap.Globals)!.Champions[0]);

    double statBefore = vm.Stats.First(s => s.Name == "protection").Value;
    int skillBefore = vm.Skills.First(s => s.Name == "swords").Level;
    double foodBefore = vm.Food;
    int levelBefore = vm.Level;

    // First: only the stat table is unreadable, so only stat edits are refused. Everything else must
    // keep working — a trainer that stops responding because one table hiccupped is worse than one
    // that refuses the edit it cannot make.
    heap.PoisonChampionStats(1);
    lua.ResetCache();

    vm.Stats.First(s => s.Name == "protection").Value = 77;
    Close("a refused stat edit reverts", vm.Stats.First(s => s.Name == "protection").Value, statBefore);
    Check("and the user is told", host.LastMessage.Length > 0);

    vm.Skills.First(s => s.Name == "swords").Level = skillBefore + 1;
    Equal("while an edit that can still be written goes through",
        vm.Skills.First(s => s.Name == "swords").Level, skillBefore + 1);

    // Then: every write fails, as a page turned read-only under the trainer would. Nothing may be
    // left showing a number the game did not take.
    heap.RefuseWrites = true;
    vm.Food = 42;
    Close("a refused food edit reverts", vm.Food, foodBefore);
    vm.Level = 40;
    Equal("a refused level edit reverts", vm.Level, levelBefore);
    vm.Skills.First(s => s.Name == "swords").Level = 9;
    Equal("a refused skill edit reverts",
        vm.Skills.First(s => s.Name == "swords").Level, skillBefore + 1);
    heap.RefuseWrites = false;
}

{
    // A brand-new row must take the game's numbers even while an editor holds focus: it has nothing
    // half-typed to protect, and a row left at zero would both mis-report the stat and let a freeze
    // latch onto that zero and pin the champion's health at nothing.
    var heap = FakeHeap.BuildGame();
    var reader = new LegendOfGrimrock1Trainer.Game.PartyReader(new LuaHeap(heap));
    var host = new FakeHost(reader, heap.Globals) { EditorHasFocus = true };
    var snapshot = reader.ReadParty(heap.Globals)!.Champions[0];
    var vm = new ChampionViewModel(host, snapshot);

    Close("a new row shows the game's value, not zero",
        vm.Stats.First(s => s.Name == "health").Value, snapshot.Stat("health")!.Value);
    Close("and so does the champion's food", vm.Food, snapshot.Food);
    Equal("and its level", vm.Level, snapshot.Level);
    Equal("and a skill row", vm.Skills.First(s => s.Name == "swords").Level, 3);
    Close("and a condition timer", vm.Conditions.First(c => c.Name == "poison").Timer, 30);
}

// =================================================================================================
Group("Reference tables match the game's own");
// =================================================================================================

Equal("twelve stats", GameTables.Stats.Length, 12);
Equal("eighteen conditions", GameTables.Conditions.Length, 18);
Equal("seventeen skills", GameTables.Skills.Length, 17);
Equal("twenty spells", GameTables.Spells.Length, 20);
Equal("thirteen campaign levels", GameTables.CampaignLevelNames.Length, GameFacts.CampaignLevels);
Check("every skill has a label", GameTables.Skills.All(GameTables.SkillUiNames.ContainsKey));
Check("no duplicate stat keys", GameTables.Stats.Select(s => s.Name).Distinct().Count() == GameTables.Stats.Length);
Check("no duplicate condition keys",
    GameTables.Conditions.Select(c => c.Name).Distinct().Count() == GameTables.Conditions.Length);
Check("every timed condition is a real condition",
    GameTables.TimedConditions.All(GameTables.ConditionsByName.ContainsKey));
Check("every beneficial condition except none is timed",
    GameTables.Conditions.Where(c => c.Kind == ConditionKind.Beneficial)
        .All(c => GameTables.TimedConditions.Contains(c.Name)));
Check("burdened and overloaded are not timed",
    !GameTables.TimedConditions.Contains("burdened") && !GameTables.TimedConditions.Contains("overloaded"));
Check("resource stats are the two bars",
    GameTables.ResourceStats.SequenceEqual(new[] { "health", "energy" }));
Check("every spell names a real skill",
    GameTables.Spells.All(s => GameTables.Skills.Contains(s.Skill)));
Check("every rune letter is A-I",
    GameTables.Spells.All(s => s.Runes.All(c => c >= 'A' && c <= 'I')));
Equal("fireball's runes", GameTables.Spells.First(s => s.Name == "fireball").Runes, "ACF");
Equal("light's runes", GameTables.Spells.First(s => s.Name == "light").Runes, "BE");
Equal("the cheapest air spell is the arrow enchant, not Shock",
    GameTables.Spells.Where(s => s.Skill == "air_magic" && s.ManaCost > 0).OrderBy(s => s.ManaCost).First().Name,
    "enchant_shock_arrow");
Equal("humanise", GameTables.Humanise("resist_poison"), "Resist Poison");
Equal("humanise leaves a single word alone", GameTables.Humanise("haste"), "Haste");
Equal("humanise of an empty key", GameTables.Humanise(""), "");
Equal("four compass labels", GameTables.FacingNames.Length, 4);

// =================================================================================================
Group("PE header parsing");
// =================================================================================================

{
    var heap = new FakeHeap();
    heap.BuildModule(0x00990000);
    var header = new byte[PeImage.HeaderBytes];
    heap.Read(0x00990000, header, header.Length);
    var image = PeImage.Parse(header);

    Check("the header parses", image is not null);
    Equal("machine is i386", image!.Machine, (ushort)0x014C);
    Check("it is a 32-bit x86 image", image.IsWin32X86);
    Equal("timestamp", image.TimeDateStamp, GameFacts.KnownTimeDateStamp);
    Equal("preferred image base", image.ImageBase, GameFacts.PreferredImageBase);
    Check("ASLR is on, which is why nothing is hard-coded", image.HasAslr);
    Equal("two sections", image.Sections.Count, 2);
    Check(".text is not writable data", !image.Section(".text")!.Value.IsWritableData);
    Check(".data is writable data", image.Section(".data")!.Value.IsWritableData);
    Check("the static slot lands in writable data", image.IsWritableDataRva(GrimrockLayout.LuaStateSlotRva));
    Check("a code RVA does not", !image.IsWritableDataRva(0x1000));
    Check("garbage is rejected", PeImage.Parse(new byte[64]) is null);
    Check("a truncated header is rejected", PeImage.Parse(header.AsSpan(0, 16)) is null);
}

// =================================================================================================
Console.WriteLine();
Console.WriteLine(failures == 0
    ? $"OK — {checks} checks passed."
    : $"FAILED — {failures} of {checks} checks failed.");
return failures == 0 ? 0 : 1;
