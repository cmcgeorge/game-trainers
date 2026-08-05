// Headless verification harness for the Civilization III: Conquests trainer.
//
// Needs no running game and no copyrighted files: everything is asserted either against constants
// pinned to the addresses the reverse engineering established, or against synthetic address spaces
// built by FakeModule. Exits 0 when every check passes, 1 otherwise.
//
//   dotnet run --project test\FormatCheck

using Civilization3ConquestsTrainer.Game;
using Civilization3ConquestsTrainer.ViewModels;
using FormatCheck;
using GameTrainers.Common.Memory;

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

void Group(string title) => Console.WriteLine(title);

const uint ImageBase = GameFacts.ImageBase;

// =================================================================================================
Group("Build fingerprint and module facts");
// =================================================================================================

Equal("process name", GameFacts.ProcessName, "Civ3Conquests");
Equal("image base", GameFacts.ImageBase, 0x00400000u);
Equal("known file size", GameFacts.KnownFileSize, 3_518_464L);
Equal("known PE timestamp", GameFacts.KnownTimeDateStamp, 0x550A3E1Fu);
Equal("leader slots", GameFacts.MaxPlayers, 32);
Equal("sliders total", GameFacts.SliderTotal, 10);
Check("target hints are lower-case for case-insensitive matching",
    GameFacts.TargetHints.All(h => h == h.ToLowerInvariant()));

// =================================================================================================
Group("Layout constants pinned to the recovered absolute addresses");
// =================================================================================================
// Written as VA - ImageBase so a mistyped RVA cannot silently shift the whole table: each line
// restates the address the C3X Steam column gives and that the live probe confirmed.

Equal("leaders", Civ3Layout.RvaLeaders, 0xA75698 - ImageBase);
Equal("p_cities", Civ3Layout.RvaCities, 0xA75668 - ImageBase);
Equal("p_units", Civ3Layout.RvaUnits, 0xA75680 - ImageBase);
Equal("p_bic_data", Civ3Layout.RvaBicData, 0x9E5D08 - ImageBase);
Equal("p_main_screen_form", Civ3Layout.RvaMainScreenForm, 0xA1AF00 - ImageBase);
Equal("p_current_turn_no", Civ3Layout.RvaCurrentTurn, 0xA74EA4 - ImageBase);
Equal("p_human_player_bits", Civ3Layout.RvaHumanPlayerBits, 0xA74EB4 - ImageBase);
Equal("p_player_bits", Civ3Layout.RvaPlayerBits, 0xA74EB8 - ImageBase);
Equal("p_debug_mode_bits", Civ3Layout.RvaDebugModeBits, 0xA74E78 - ImageBase);
Equal("p_game_difficulty", Civ3Layout.RvaGameDifficulty, 0xA74E7C - ImageBase);
Equal("p_preferences", Civ3Layout.RvaPreferences, 0xA74E70 - ImageBase);
Equal("p_toggleable_rules", Civ3Layout.RvaToggleableRules, 0xA74E74 - ImageBase);
Equal("p_is_pbem_game", Civ3Layout.RvaIsPbemGame, 0xA74FAC - ImageBase);
Equal("p_is_offline_mp_game", Civ3Layout.RvaIsOfflineMpGame, 0xA75189 - ImageBase);

Equal("sizeof(Leader)", Civ3Layout.LeaderStride, 0x20E4);
Check("leaders array end matches the game's own cmp immediate",
    0xA75698 + GameFacts.MaxPlayers * Civ3Layout.LeaderStride == 0xAB7318);

Equal("Leader.Gold_Decrement", Civ3Layout.LeaderGoldDecrement, 0x44);
Equal("Leader.Gold_Encoded", Civ3Layout.LeaderGoldEncoded, 0x48);
Equal("Leader sliders are contiguous",
    (Civ3Layout.LeaderScienceSlider - Civ3Layout.LeaderLuxurySlider,
     Civ3Layout.LeaderGoldSlider - Civ3Layout.LeaderScienceSlider), (4, 4));
Equal("Leader.Culture", Civ3Layout.LeaderCulture, 0x181C);
Check("Culture's own tag lands at Leader+0x1824",
    Civ3Layout.LeaderCulture + Civ3Layout.BaseClassNameOffset == 0x1824);
Check("Culture.cultural_level lands at Leader+0x1838",
    Civ3Layout.LeaderCulture + Civ3Layout.CultureLevel == 0x1838);

Equal("Unit_Body.Damage", Civ3Layout.UnitDamage, 0x30);
Equal("Unit_Body.Moves", Civ3Layout.UnitMoves, 0x34);
Equal("Unit_Body.Combat_Experience", Civ3Layout.UnitExperience, 0x28);
Equal("Unit_Body.Job_Value", Civ3Layout.UnitJobValue, 0x38);
Equal("Unit_Body.Job_ID", Civ3Layout.UnitJobId, 0x3C);
Check("the job pair follows Moves with no gap",
    Civ3Layout.UnitJobValue == Civ3Layout.UnitMoves + 4 && Civ3Layout.UnitJobId == Civ3Layout.UnitJobValue + 4);
Check("a unit probe reaches the job fields",
    Civ3Layout.UnitJobId + 4 <= Civ3Layout.UnitRecordProbeBytes);
Equal("City_Body.StoredFood", Civ3Layout.CityStoredFood, 0x24);
Equal("City_Body.StoredProduction", Civ3Layout.CityStoredProduction, 0x28);
Check("nothing past the anchor-bracketed City prefix is exposed",
    Civ3Layout.CityDraftCount + 4 <= Civ3Layout.CityTrustedPrefixEnd
    && Civ3Layout.CityCulturalLevel + 4 <= Civ3Layout.CityTrustedPrefixEnd
    && Civ3Layout.CityStoredProduction + 4 <= Civ3Layout.CityTrustedPrefixEnd
    && Civ3Layout.CityStoredFood + 4 <= Civ3Layout.CityTrustedPrefixEnd);

Equal("Map inside BIC", Civ3Layout.BicMap, 0x3E64);
Equal("Race stride", Civ3Layout.RaceStride, 0x974);
Equal("UnitType stride", Civ3Layout.UnitTypeStride, 0x138);

// The worker-job table's address is written as the difference of the two absolute addresses the game's
// own code uses — `mov esi,[0x9E9B24]` inside get_worker_remaining_turns_to_complete, against
// p_bic_data at 0x9E5D08 — so a mistyped offset cannot silently point the table somewhere else.
const uint BicDataVa = 0x9E5D08;
Equal("BIC.WorkerJobs", (uint)Civ3Layout.BicWorkerJobs, 0x9E9B24 - BicDataVa);
Equal("BIC.WorkerJobCount", Civ3Layout.BicWorkerJobCount, 0x8B8);
Equal("Worker_Job stride", Civ3Layout.WorkerJobStride, 0x74);
Equal("Worker_Job.TurnToComplete", Civ3Layout.WorkerJobTurnToComplete, 0x44);
Check("the job table sits between the two confirmed BIC anchors it was derived from",
    Civ3Layout.BicUnitTypes < Civ3Layout.BicWorkerJobs && Civ3Layout.BicWorkerJobs < Civ3Layout.BicMap);
Check("a job probe reaches the cost field",
    Civ3Layout.WorkerJobTurnToComplete + 4 <= Civ3Layout.WorkerJobRecordProbeBytes
    && Civ3Layout.WorkerJobRecordProbeBytes <= Civ3Layout.WorkerJobStride);

Equal("'LEAD' tag", Civ3Layout.TagLead, 0x4441454Cu);
Equal("'CITY' tag", Civ3Layout.TagCity, 0x59544943u);
Equal("'UNIT' tag", Civ3Layout.TagUnit, 0x54494E55u);
Equal("'TILE' tag", Civ3Layout.TagTile, 0x454C4954u);
Equal("'CULT' tag", Civ3Layout.TagCult, 0x544C5543u);
foreach (var (name, tag) in new[]
         {
             ("LEAD", Civ3Layout.TagLead), ("CITY", Civ3Layout.TagCity), ("UNIT", Civ3Layout.TagUnit),
             ("TILE", Civ3Layout.TagTile), ("CULT", Civ3Layout.TagCult), ("BIC ", Civ3Layout.TagBic),
         })
    Equal($"tag dword decodes to \"{name}\"",
        System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes(tag)), name);

// =================================================================================================
Group("Gold codec (the treasury is stored as two fields that sum to it)");
// =================================================================================================

Equal("decode adds the two halves", Civ3Layout.DecodeGold(-12345, 12355), 10L);
Equal("decode with a positive key", Civ3Layout.DecodeGold(500, -400), 100L);
Equal("decode of a zero pair", Civ3Layout.DecodeGold(0, 0), 0L);

foreach (long target in new long[] { 0, 1, -1, 10, 12345, -50_000, 1_000_000, GameFacts.MaxTreasuryPreset })
    foreach (int key in new[] { -12345, 0, 7, 1_000_000 })
    {
        Check($"round-trip {target} against key {key}", Civ3Layout.TryEncodeGold(target, key, out int enc)
                                                         && Civ3Layout.DecodeGold(key, enc) == target);
    }

Check("encoding refuses a value that would overflow int32",
    !Civ3Layout.TryEncodeGold(long.MaxValue, -1, out _));
Check("encoding refuses an underflow", !Civ3Layout.TryEncodeGold(long.MinValue, 1, out _));
// Those two are caught by the plausibility range before any subtraction happens, which is the point
// of range-checking the input first. This one reaches the int32 guard itself: the target is in range
// but the difference against the key is not.
Check("encoding refuses a treasury past the plausible range",
    !Civ3Layout.TryEncodeGold(3_000_000_000L, 0, out _));
Check("encoding refuses a difference past int32 even when the target is plausible",
    !Civ3Layout.TryEncodeGold(2_000_000_000L, -1_000_000_000, out _));
Check("encoding accepts a difference that only just fits",
    Civ3Layout.TryEncodeGold(1_000_000_000L, -1_000_000_000, out _));

Check("plausible treasury accepts zero", Civ3Layout.IsPlausibleTreasury(0));
Check("plausible treasury accepts debt", Civ3Layout.IsPlausibleTreasury(-5000));
Check("plausible treasury rejects absurd", !Civ3Layout.IsPlausibleTreasury(9_000_000_000));
Check("the max-treasury preset is itself plausible",
    Civ3Layout.IsPlausibleTreasury(GameFacts.MaxTreasuryPreset));

// =================================================================================================
Group("Slider, civ-id and pointer predicates");
// =================================================================================================

Check("0/6/4 is a valid slider set", Civ3Layout.IsPlausibleSliderSet(0, 6, 4));
Check("10/0/0 is valid", Civ3Layout.IsPlausibleSliderSet(10, 0, 0));
Check("a set summing to 9 is rejected", !Civ3Layout.IsPlausibleSliderSet(0, 5, 4));
Check("a set summing to 11 is rejected", !Civ3Layout.IsPlausibleSliderSet(1, 6, 4));
Check("a negative slider is rejected", !Civ3Layout.IsPlausibleSliderSet(-1, 7, 4));
Check("a slider above 10 is rejected", !Civ3Layout.IsPlausibleSliderSet(11, 0, -1));

Check("civ 0 (barbarians) is valid", Civ3Layout.IsValidCivId(0));
Check("civ 31 is valid", Civ3Layout.IsValidCivId(31));
Check("civ 32 is out of range", !Civ3Layout.IsValidCivId(32));
Check("civ -1 is out of range", !Civ3Layout.IsValidCivId(-1));

Check("bit set is detected", Civ3Layout.IsBitSet(0x1FFF, 12));
Check("bit clear is detected", !Civ3Layout.IsBitSet(0x1FFF, 13));
Check("an out-of-range civ never reads as set", !Civ3Layout.IsBitSet(0xFFFFFFFF, 32));

Check("a null pointer is rejected", !Civ3Layout.LooksLikeHeapPointer(0));
Check("a misaligned pointer is rejected", !Civ3Layout.LooksLikeHeapPointer(0x02000001));
Check("a kernel-range pointer is rejected", !Civ3Layout.LooksLikeHeapPointer(0x80000000));
Check("a plausible heap pointer is accepted", Civ3Layout.LooksLikeHeapPointer(0x083BE1F4));

Check("map validates on the staggered-grid identity", Civ3Layout.ValidateMap(130, 130, 8450));
Check("map rejects a tile count that isn't W*H/2", !Civ3Layout.ValidateMap(130, 130, 16900));
Check("map rejects a zero dimension", !Civ3Layout.ValidateMap(0, 130, 0));

// =================================================================================================
Group("Record validation over synthetic buffers");
// =================================================================================================

var probe = new FakeModule(0x400000, 0x700000);
probe.WritePeHeader(GameFacts.KnownTimeDateStamp);
probe.PlantLeader(Civ3Layout.RvaLeaders, 0, gold: 250);
uint rdataStart = 0x400000 + FakeModule.RdataRva;
uint rdataEnd = rdataStart + FakeModule.RdataSize;

byte[] leader = probe.Read(probe.At(Civ3Layout.RvaLeaders), Civ3Layout.LeaderStride);
Check("a well-formed leader validates", Civ3Layout.ValidateLeader(leader, 0, rdataStart, rdataEnd));
Check("a leader validated against the wrong slot is rejected",
    !Civ3Layout.ValidateLeader(leader, 1, rdataStart, rdataEnd));
Check("a truncated leader buffer is rejected",
    !Civ3Layout.ValidateLeader(leader.AsSpan(0, 0x100), 0, rdataStart, rdataEnd));

void Corrupt(string name, int offset, int value)
{
    byte[] copy = (byte[])leader.Clone();
    BitConverter.TryWriteBytes(copy.AsSpan(offset, 4), value);
    Check(name, !Civ3Layout.ValidateLeader(copy, 0, rdataStart, rdataEnd));
}

Corrupt("a wrong class tag is rejected", Civ3Layout.BaseClassNameOffset, 0x11111111);
Corrupt("an ID that isn't the slot index is rejected", Civ3Layout.LeaderId, 7);
Corrupt("a vtable outside .rdata is rejected", 0, 0x00112233);
Corrupt("an out-of-range race id is rejected", Civ3Layout.LeaderRaceId, 99);
Corrupt("sliders that don't total ten are rejected", Civ3Layout.LeaderScienceSlider, 9);
Corrupt("an absurd era is rejected", Civ3Layout.LeaderEra, 99);
Corrupt("a negative city count is rejected", Civ3Layout.LeaderCitiesCount, -1);
Corrupt("an absurd unit count is rejected", Civ3Layout.LeaderUnitCount, 1_000_000);
Corrupt("a missing 'CULT' tag is rejected",
    Civ3Layout.LeaderCulture + Civ3Layout.BaseClassNameOffset, 0);
Corrupt("a culture object owned by another civ is rejected",
    Civ3Layout.LeaderCulture + Civ3Layout.CultureCivId, 5);
Corrupt("an unencodable treasury is rejected", Civ3Layout.LeaderGoldEncoded, int.MaxValue);

var unit = new byte[0x40];
BitConverter.TryWriteBytes(unit.AsSpan(Civ3Layout.UnitId, 4), 3);
BitConverter.TryWriteBytes(unit.AsSpan(Civ3Layout.UnitCivId, 4), 2);
BitConverter.TryWriteBytes(unit.AsSpan(Civ3Layout.UnitX, 4), 46);
BitConverter.TryWriteBytes(unit.AsSpan(Civ3Layout.UnitY, 4), 26);
BitConverter.TryWriteBytes(unit.AsSpan(Civ3Layout.UnitExperience, 4), 1);
Check("a well-formed unit validates", Civ3Layout.ValidateUnit(unit, 3, 130, 130));
Check("a unit validated against the wrong slot is rejected", !Civ3Layout.ValidateUnit(unit, 4, 130, 130));
Check("a unit outside the map is rejected", !Civ3Layout.ValidateUnit(unit, 3, 20, 20));
Check("a unit is accepted when map bounds are unknown", Civ3Layout.ValidateUnit(unit, 3, 0, 0));
BitConverter.TryWriteBytes(unit.AsSpan(Civ3Layout.UnitDamage, 4), -1);
Check("negative damage is rejected", !Civ3Layout.ValidateUnit(unit, 3, 130, 130));

var city = new byte[Civ3Layout.CityTrustedPrefixEnd];
BitConverter.TryWriteBytes(city.AsSpan(Civ3Layout.CityId, 4), 0);
BitConverter.TryWriteBytes(city.AsSpan(Civ3Layout.CityX, 2), (short)75);
BitConverter.TryWriteBytes(city.AsSpan(Civ3Layout.CityY, 2), (short)17);
city[Civ3Layout.CityCivId] = 6;
BitConverter.TryWriteBytes(city.AsSpan(Civ3Layout.CityStoredFood, 4), 16);
BitConverter.TryWriteBytes(city.AsSpan(Civ3Layout.CityStoredProduction, 4), 6);
BitConverter.TryWriteBytes(city.AsSpan(Civ3Layout.CityCulturalLevel, 4), 1);
Check("a well-formed city validates", Civ3Layout.ValidateCity(city, 0, 130, 130));
Check("a city validated against the wrong slot is rejected", !Civ3Layout.ValidateCity(city, 1, 130, 130));
Check("a city outside the map is rejected", !Civ3Layout.ValidateCity(city, 0, 50, 50));
Check("a short city buffer is rejected", !Civ3Layout.ValidateCity(city.AsSpan(0, 8), 0, 130, 130));

// Worker_Job is the one BIC table with no ID field, so ValidateWorkerJob is doing the work that
// `Table[i].ID == i` does everywhere else. It has to be strict enough that arbitrary memory fails it.
var job = new byte[Civ3Layout.WorkerJobRecordProbeBytes];
System.Text.Encoding.ASCII.GetBytes("Road").CopyTo(job.AsSpan(Civ3Layout.WorkerJobName));
BitConverter.TryWriteBytes(job.AsSpan(Civ3Layout.WorkerJobTurnToComplete, 4), 6);
Check("a well-formed worker job validates", Civ3Layout.ValidateWorkerJob(job));
Check("a short job buffer is rejected", !Civ3Layout.ValidateWorkerJob(job.AsSpan(0, 8)));

BitConverter.TryWriteBytes(job.AsSpan(Civ3Layout.WorkerJobTurnToComplete, 4), -1);
Check("a negative job cost is rejected", !Civ3Layout.ValidateWorkerJob(job));
BitConverter.TryWriteBytes(job.AsSpan(Civ3Layout.WorkerJobTurnToComplete, 4),
    GameFacts.MaxWorkerJobTurnToComplete + 1);
Check("an absurd job cost is rejected", !Civ3Layout.ValidateWorkerJob(job));
BitConverter.TryWriteBytes(job.AsSpan(Civ3Layout.WorkerJobTurnToComplete, 4), 0);
Check("a free job is allowed — a mod may ship one", Civ3Layout.ValidateWorkerJob(job));

job[Civ3Layout.WorkerJobName] = 0;
Check("an empty job name is rejected", !Civ3Layout.ValidateWorkerJob(job));
job[Civ3Layout.WorkerJobName] = (byte)'R';
job[Civ3Layout.WorkerJobName + 2] = 0x01;
Check("a name with a control byte in it is rejected", !Civ3Layout.ValidateWorkerJob(job));
var unterminated = new byte[Civ3Layout.WorkerJobRecordProbeBytes];
unterminated.AsSpan(Civ3Layout.WorkerJobName, 32).Fill((byte)'A');
Check("a name with no terminator is rejected", !Civ3Layout.ValidateWorkerJob(unterminated));
Check("an all-zero record is rejected", !Civ3Layout.ValidateWorkerJob(new byte[Civ3Layout.WorkerJobRecordProbeBytes]));

// What "Finish job" banks: the ruleset's base cost, scaled to clear the terrain factor the trainer
// does not decode. It has to exceed the base cost, and stay a plausible count of worker-turns.
Check("finishing a job banks more than its base cost",
    Civ3Layout.WorkerJobWorkToFinish(6) > 6 && Civ3Layout.WorkerJobWorkToFinish(24) > 24);
Equal("and by the documented factor", Civ3Layout.WorkerJobWorkToFinish(6), 6 * GameFacts.WorkerJobTerrainFactorCeiling);
Check("a zero-cost job still banks something", Civ3Layout.WorkerJobWorkToFinish(0) > 0);
Check("an absurd cost cannot overflow the work written",
    Civ3Layout.WorkerJobWorkToFinish(int.MaxValue) > 0);
Check("the terrain ceiling clears the epic rules' worst terrain",
    GameFacts.WorkerJobTerrainFactorCeiling >= 3);
Equal("instant jobs cost one worker-turn, not zero", GameFacts.InstantWorkerJobTurns, 1);

// =================================================================================================
Group("PE header parsing");
// =================================================================================================

var pe = PeImage.Parse(probe.Read(probe.ModuleBase, PeImage.HeaderReadSize));
Check("a PE32 header parses", pe != null);
if (pe != null)
{
    Equal("machine", pe.Machine, (ushort)0x014C);
    Equal("timestamp", pe.TimeDateStamp, GameFacts.KnownTimeDateStamp);
    Equal("image base", pe.ImageBase, 0x400000u);
    Check("ASLR is not set", !pe.HasAslr);
    Equal("section count", pe.Sections.Count, 3);
    Check(".rdata is found", pe.Section(".rdata") is { Rva: FakeModule.RdataRva });
    Check(".text is found", pe.Section(".text") is { Rva: FakeModule.TextRva });
    Check("a missing section returns null", pe.Section(".nope") == null);
    Check(".data is recognised as writable data by its characteristics",
        pe.Section(".data")!.Value.IsWritableData);
    Check(".text is not writable data", !pe.Section(".text")!.Value.IsWritableData);
    Check(".rdata is not writable data", !pe.Section(".rdata")!.Value.IsWritableData);
    Check("an RVA inside .rdata is recognised", pe.Section(".rdata")!.Value.ContainsRva(FakeModule.RdataRva + 8));
    Check("an RVA before .rdata is not", !pe.Section(".rdata")!.Value.ContainsRva(FakeModule.RdataRva - 8));
}

Check("a non-PE buffer is rejected", PeImage.Parse(new byte[512]) == null);
Check("a truncated buffer is rejected", PeImage.Parse(new byte[8]) == null);

var pe64 = new FakeModule(0x400000, 0x2000);
pe64.WritePeHeader(1, machine: 0x8664);
var parsed64 = PeImage.Parse(pe64.Read(pe64.ModuleBase, PeImage.HeaderReadSize));
Check("a non-i386 machine type is reported", parsed64 != null && parsed64.Machine == 0x8664);
var pe64Locator = new GameLocator(pe64);
Check("and the locator refuses it", pe64Locator.Locate() == null);
Check("with an explanation naming the architecture", pe64Locator.LastError.Contains("32-bit"));

// =================================================================================================
Group("Locator over a synthetic address space");
// =================================================================================================

var good = new FakeModule(0x400000, 0x700000);
good.WritePeHeader(GameFacts.KnownTimeDateStamp);
good.PlantGame(Civ3Layout.RvaLeaders, humanCivId: 1, playerCount: 13);

var locator = new GameLocator(good);
var loc = locator.Locate();
Check("the locator finds a planted game", loc != null);
if (loc != null)
{
    Equal("all 32 leader slots validate", loc.ValidatedLeaders, GameFacts.MaxPlayers);
    Equal("chain is the static-globals fast path", loc.Chain, LocateChain.StaticGlobals);
    Check("the build is recognised", loc.IsKnownBuild);
    Equal("human civ id", loc.HumanCivId, 1);
    Equal("leaders address", loc.Leaders, good.At(Civ3Layout.RvaLeaders));
    Equal("leader 5 address", loc.Leader(5), good.At(Civ3Layout.RvaLeaders + 5 * Civ3Layout.LeaderStride));
    Equal("map width", loc.MapWidth, 100);
    Equal("map height", loc.MapHeight, 80);
    Equal("tile count", loc.TileCount, 4000);
}

// A module mapped somewhere other than the preferred base must still resolve, since the locator
// adds RVAs to whatever base the OS reports rather than assuming 0x400000.
var rebased = new FakeModule(0x10000000, 0x700000);
rebased.WritePeHeader(GameFacts.KnownTimeDateStamp, imageBase: 0x400000);
rebased.PlantGame(Civ3Layout.RvaLeaders, humanCivId: 2, playerCount: 6);
var rebasedLoc = new GameLocator(rebased).Locate();
Check("a relocated module still locates", rebasedLoc != null);
Equal("relocated leaders address", rebasedLoc?.Leaders, rebased.At(Civ3Layout.RvaLeaders));

// An unrecognised build is located but flagged, not silently trusted.
var otherBuild = new FakeModule(0x400000, 0x700000);
otherBuild.WritePeHeader(0xDEADBEEF);
otherBuild.PlantGame(Civ3Layout.RvaLeaders);
var otherLoc = new GameLocator(otherBuild).Locate();
Check("an unknown build still locates when the layout matches", otherLoc != null);
Check("an unknown build is flagged", otherLoc is { IsKnownBuild: false });

// One corrupted slot must fail the whole locate rather than yielding a partial answer.
var broken = new FakeModule(0x400000, 0x700000);
broken.WritePeHeader(GameFacts.KnownTimeDateStamp);
broken.PlantGame(Civ3Layout.RvaLeaders);
broken.PutInt32(Civ3Layout.RvaLeaders + (uint)(17 * Civ3Layout.LeaderStride) + (uint)Civ3Layout.LeaderId, 999);
var brokenLocator = new GameLocator(broken);
Check("a single corrupted leader slot fails the locate", brokenLocator.Locate() == null);
Check("the failure is explained", brokenLocator.LastError.Length > 0);

// Empty memory must not produce a confident wrong answer.
var empty = new FakeModule(0x400000, 0x700000);
empty.WritePeHeader(GameFacts.KnownTimeDateStamp);
var emptyLocator = new GameLocator(empty);
Check("an empty image does not locate", emptyLocator.Locate() == null);
Check("the empty case says a game must be loaded", emptyLocator.LastError.Contains("Load a game"));

// No PE header at all.
var garbage = new FakeModule(0x400000, 0x4000);
var garbageLocator = new GameLocator(garbage);
Check("a module with no PE header does not locate", garbageLocator.Locate() == null);
Check("the no-header case is explained", garbageLocator.LastError.Contains("PE header"));

// A human civ id that is not one of the game's players must be refused.
var badCiv = new FakeModule(0x400000, 0x700000);
badCiv.WritePeHeader(GameFacts.KnownTimeDateStamp);
badCiv.PlantGame(Civ3Layout.RvaLeaders, humanCivId: 20, playerCount: 4);
var badCivLocator = new GameLocator(badCiv);
Check("a human civ id outside the player set is refused", badCivLocator.Locate() == null);
Check("that refusal is explained", badCivLocator.LastError.Contains("civ id"));

// Chain B: move the array away from its known RVA and leave only the code idiom behind.
const uint MovedLeaders = 0x2A0000;                       // inside the fake .data
var moved = new FakeModule(0x400000, 0x700000);
moved.WritePeHeader(GameFacts.KnownTimeDateStamp);
moved.PlantGame(MovedLeaders, humanCivId: 3, playerCount: 8);
moved.PlantArrayWalk(FakeModule.TextRva + 0x2000, (uint)moved.At(MovedLeaders), Civ3Layout.LeaderStride);
var movedLocator = new GameLocator(moved);
var movedLoc = movedLocator.Locate();
Check("the signature chain re-derives a moved leader array", movedLoc != null);
Equal("the signature chain is reported", movedLoc?.Chain, LocateChain.SignatureScan);
Equal("the re-derived address is correct", movedLoc?.Leaders, moved.At(MovedLeaders));
Equal("the re-derived array fully validates", movedLoc?.ValidatedLeaders, GameFacts.MaxPlayers);

// Chain B must recover the stride as well as the base, otherwise it can only handle a moved array
// and not a rebuilt one — which is the case its own documentation advertises.
const int WiderStride = Civ3Layout.LeaderStride + 0x40;
var resized = new FakeModule(0x400000, 0x700000);
resized.WritePeHeader(0xDEADBEEF);
resized.PlantGame(MovedLeaders, humanCivId: 2, playerCount: 5, stride: WiderStride);
resized.PlantArrayWalk(FakeModule.TextRva + 0x3000, (uint)resized.At(MovedLeaders), WiderStride);
var resizedLocator = new GameLocator(resized);
var resizedLoc = resizedLocator.Locate();
Check("the signature chain recovers a resized leader record", resizedLoc != null);
Equal("the recovered stride is used, not the baked one", resizedLoc?.LeaderStride, WiderStride);
Equal("and record addresses follow it",
    resizedLoc?.Leader(3), resized.At(MovedLeaders + (uint)(3 * WiderStride)));

// Without the code idiom, the same moved array must not be found by accident.
var movedNoCode = new FakeModule(0x400000, 0x700000);
movedNoCode.WritePeHeader(GameFacts.KnownTimeDateStamp);
movedNoCode.PlantGame(MovedLeaders, humanCivId: 3, playerCount: 8);
Check("a moved array with no code idiom is not found", new GameLocator(movedNoCode).Locate() == null);

// =================================================================================================
Group("Reference data");
// =================================================================================================

Equal("nine conquests ship with the expansion", ConquestBook.All.Count, 9);
Check("every conquest is fully described",
    ConquestBook.All.All(c => c.Name.Length > 0 && c.Era.Length > 0 && c.Setting.Length > 0
                              && c.Victory.Length > 0 && c.Note.Length > 0));
Check("conquest names are unique", ConquestBook.All.Select(c => c.Name).Distinct().Count() == 9);
Check("Mesopotamia is present", ConquestBook.All.Any(c => c.Name == "Mesopotamia"));
Check("the WWII Pacific conquest is present", ConquestBook.All.Any(c => c.Name.Contains("Pacific")));
Check("behaviour notes are present", ConquestBook.Notes.Count >= 5);
// A record, not a tuple: WPF resolves binding paths through properties, and ValueTuple's Item1/Item2
// are fields, so a tuple would render the whole tab blank.
Check("notes expose bindable properties",
    typeof(BehaviourNote).GetProperty(nameof(BehaviourNote.Topic)) != null
    && typeof(BehaviourNote).GetProperty(nameof(BehaviourNote.Body)) != null);
Check("every note is fully populated",
    ConquestBook.Notes.All(n => n.Topic.Length > 0 && n.Body.Length > 0));
Check("the gold obfuscation is documented in the UI",
    ConquestBook.Notes.Any(n => n.Topic.Contains("obfuscated") && n.Body.Contains("two fields")));
Check("the damage inversion is documented in the UI",
    ConquestBook.Notes.Any(n => n.Body.Contains("lost")));
// The UI must not imply a freeze can prevent a death: Civ3 resolves a battle inside one call, so a
// poll loop can never intervene. This is the single most likely thing for a user to expect and not get.
Check("the limits of freezing a unit are stated in the UI",
    ConquestBook.Notes.Any(n => n.Topic.Contains("invincible") && n.Body.Contains("inside one call")));

Equal("empty tables report no races", GameTables.Empty.Races.Count, 0);
Equal("an unknown race id degrades to a label", GameTables.Empty.RaceName(7), "Race 7");
Equal("an unset race id reads as none", GameTables.Empty.RaceName(-1), "(none)");
Equal("an unknown unit type degrades to a label", GameTables.Empty.UnitTypeName(3), "Type 3");
Equal("a race with no leader displays its country only",
    new RaceInfo(0, "", "A Barbarian Chiefdom", "Barbarian", 0).Display, "A Barbarian Chiefdom");
Equal("a race with a leader displays both",
    new RaceInfo(1, "Caesar", "Rome", "Roman", 1).Display, "Rome — Caesar");
Equal("unit stats render", new UnitTypeInfo(9, "Swordsman", 3, 2, 1, 30).Stats, "A3 D2 M1  30 shields");

// =================================================================================================
Group("Process picker");
// =================================================================================================
// The trainer's own executable is Civ3ConqTrainer.exe, whose process name contains "civ3" and sorts
// BEFORE Civ3Conquests under an ordinal comparison. A picker that substring-matched and then sorted
// by name would auto-select the trainer itself and report "not a 32-bit image" — which is exactly
// what happened before this was fixed, so it is pinned here.

Equal("the game is an exact match", ProcessPicker.Rank("Civ3Conquests"), ProcessMatch.Exact);
Equal("case does not matter", ProcessPicker.Rank("civ3conquests"), ProcessMatch.Exact);
Equal("the trainer itself is only a hint match", ProcessPicker.Rank("Civ3ConqTrainer"), ProcessMatch.Hint);
Equal("an unrelated process matches nothing", ProcessPicker.Rank("explorer"), ProcessMatch.None);
Check("the trainer's own name really does sort first, which is what made this a bug",
    StringComparer.OrdinalIgnoreCase.Compare("Civ3ConqTrainer", "Civ3Conquests") < 0);

Check("the trainer never offers itself", !ProcessPicker.IsSelectable(1234, 1234));
Check("other processes are offered", ProcessPicker.IsSelectable(1234, 5678));

var candidates = new[]
{
    new ProcessEntry(1, "Civ3ConqTrainer", ProcessPicker.Rank("Civ3ConqTrainer")),
    new ProcessEntry(2, "Civ3Conquests", ProcessPicker.Rank("Civ3Conquests")),
    new ProcessEntry(3, "explorer", ProcessPicker.Rank("explorer")),
};
var ordered = ProcessPicker.Order(candidates, e => e.Match, e => e.Name).ToList();
Equal("the exact match is ordered first despite sorting later by name", ordered[0].Name, "Civ3Conquests");
Equal("a hint match outranks an unrelated process", ordered[1].Name, "Civ3ConqTrainer");

var chosen = ProcessPicker.ChooseDefault(ordered, e => e.Match, e => e.Id, null);
Equal("the default selection is the game, not the trainer", chosen?.Name, "Civ3Conquests");

var hintsOnly = ProcessPicker.Order(new[]
{
    new ProcessEntry(1, "Civ3ConqTrainer", ProcessPicker.Rank("Civ3ConqTrainer")),
    new ProcessEntry(3, "explorer", ProcessPicker.Rank("explorer")),
}, e => e.Match, e => e.Name).ToList();
Check("with no exact match nothing is auto-selected, rather than guessing",
    ProcessPicker.ChooseDefault(hintsOnly, e => e.Match, e => e.Id, null) == null);
Equal("a previous selection is preserved across a refresh",
    ProcessPicker.ChooseDefault(ordered, e => e.Match, e => e.Id, 3)?.Name, "explorer");

// A 64-bit image — which is what attaching to the trainer itself produced — must be refused with a
// message that points at the fix.
var wrongArch = new FakeModule(0x400000, 0x2000);
wrongArch.WritePeHeader(GameFacts.KnownTimeDateStamp, machine: 0x8664);
var wrongArchLocator = new GameLocator(wrongArch);
Check("a 64-bit target is refused", wrongArchLocator.Locate() == null);
Check("and the message names the process to pick instead",
    wrongArchLocator.LastError.Contains(GameFacts.ProcessName));

// =================================================================================================
Group("Rules tables read from BIC");
// =================================================================================================

// The stride search must clear the widest stride we know about (Race is 0x974). A ceiling below it
// would make the fallback unable to rediscover the very table it exists for.
foreach (int plantedStride in new[] { Civ3Layout.RaceStride, 0x400, 0xA00 })
{
    var bicModule = new FakeModule(0x400000, 0x700000);
    bicModule.WritePeHeader(GameFacts.KnownTimeDateStamp);
    bicModule.PlantGame(Civ3Layout.RvaLeaders);
    bicModule.PlantRaces(0x2B0000, raceCount: 6, stride: plantedStride);
    var bicLoc = new GameLocator(bicModule).Locate();
    var tables = bicLoc != null ? GameTables.Read(bicModule, bicLoc) : GameTables.Empty;
    Equal($"races recovered at stride 0x{plantedStride:X}", tables.Races.Count, 6);
    if (tables.Races.Count == 6)
    {
        Equal($"race 0 name at stride 0x{plantedStride:X}", tables.Races[0].Country, "Country0");
        Equal($"race 5 leader at stride 0x{plantedStride:X}", tables.Races[5].Leader, "Leader5");
    }
}

var noTables = new FakeModule(0x400000, 0x700000);
noTables.WritePeHeader(GameFacts.KnownTimeDateStamp);
noTables.PlantGame(Civ3Layout.RvaLeaders);
var noTablesLoc = new GameLocator(noTables).Locate();
var emptyTables = noTablesLoc != null ? GameTables.Read(noTables, noTablesLoc) : GameTables.Empty;
Equal("an absent races table degrades to empty rather than garbage", emptyTables.Races.Count, 0);
Equal("and labels fall back", emptyTables.RaceName(3), "Race 3");
Equal("an absent worker-job table degrades to empty", emptyTables.WorkerJobs.Count, 0);
Equal("and yields no address to write through", emptyTables.WorkerJobsTable, (nuint)0);

// The worker-job table has no ID column, so its stride is recovered purely from every record passing
// ValidateWorkerJob. Planting it at a non-standard stride is what proves that substitute actually works.
foreach (int plantedStride in new[] { Civ3Layout.WorkerJobStride, 0x80 })
{
    var jobModule = new FakeModule(0x400000, 0x700000);
    jobModule.WritePeHeader(GameFacts.KnownTimeDateStamp);
    jobModule.PlantGame(Civ3Layout.RvaLeaders);
    jobModule.PlantWorkerJobs(0x2C0000, jobCount: 13, stride: plantedStride, 12, 8, 16, 6);
    var jobLoc = new GameLocator(jobModule).Locate();
    var jobTables = jobLoc != null ? GameTables.Read(jobModule, jobLoc) : GameTables.Empty;

    Equal($"worker jobs recovered at stride 0x{plantedStride:X}", jobTables.WorkerJobs.Count, 13);
    if (jobTables.WorkerJobs.Count != 13) continue;
    Equal($"job 0 name at stride 0x{plantedStride:X}", jobTables.WorkerJobs[0].Name, "Job0");
    Equal($"job 3 cost at stride 0x{plantedStride:X}", jobTables.WorkerJobs[3].TurnToComplete, 6);
    Equal($"the recovered stride is reported at 0x{plantedStride:X}", jobTables.WorkerJobStride, plantedStride);
    Equal($"and the table address at stride 0x{plantedStride:X}", jobTables.WorkerJobsTable, jobModule.At(0x2C0000));
    Equal("an idle unit's job has no name", jobTables.WorkerJobName(-1), "");
    Check("and no job record", jobTables.WorkerJob(-1) == null);
    Check("a job id past the table has no record", jobTables.WorkerJob(99) == null);
}

// =================================================================================================
Group("Scan value helpers");
// =================================================================================================

Check("decimal parses", ScanValue.TryParse("500", out long v1) && v1 == 500);
Check("negative parses", ScanValue.TryParse("-42", out long v2) && v2 == -42);
Check("0x hex parses", ScanValue.TryParse("0x1F4", out long v3) && v3 == 500);
Check("trailing-h hex parses", ScanValue.TryParse("1F4h", out long v4) && v4 == 500);
Check("empty is rejected", !ScanValue.TryParse("", out _));
Check("whitespace is rejected", !ScanValue.TryParse("   ", out _));
Check("nonsense is rejected", !ScanValue.TryParse("banana", out _));

Check("255 fits a byte", ScanValue.FitsWidth(255, ScanWidth.Byte));
Check("-128 fits a byte", ScanValue.FitsWidth(-128, ScanWidth.Byte));
Check("256 does not fit a byte", !ScanValue.FitsWidth(256, ScanWidth.Byte));
Check("65535 fits an int16", ScanValue.FitsWidth(65535, ScanWidth.Int16));
Check("70000 does not fit an int16", !ScanValue.FitsWidth(70000, ScanWidth.Int16));
Check("int.MaxValue fits an int32", ScanValue.FitsWidth(int.MaxValue, ScanWidth.Int32));

Equal("-1 folds to 0xFF as a byte", ScanValue.Canonicalize(-1, ScanWidth.Byte), 0xFFL);
Equal("-1 folds to 0xFFFF as an int16", ScanValue.Canonicalize(-1, ScanWidth.Int16), 0xFFFFL);
Equal("-1 folds to 0xFFFFFFFF as an int32", ScanValue.Canonicalize(-1, ScanWidth.Int32), 0xFFFFFFFFL);
Equal("a positive value is unchanged", ScanValue.Canonicalize(1234, ScanWidth.Int32), 1234L);

// =================================================================================================
Group("Frozen-value rows through a fake scan host");
// =================================================================================================

var scanHost = new FakeScanHost();
var pin = new FrozenValueViewModel(scanHost, 0x1000, ScanWidth.Int32, 500, "Treasury");
Equal("a pin starts at its captured value", pin.Target, 500L);
Equal("the label is kept", pin.Label, "Treasury");
Equal("the address renders as hex", pin.AddressHex, "0x1000");

pin.Target = 12345;
Equal("editing the target pokes once", scanHost.WriteCount, 1);
Equal("the target is kept", pin.Target, 12345L);

pin.ApplyFreeze();
Equal("an unfrozen pin does not re-poke", scanHost.WriteCount, 1);
pin.Frozen = true;
pin.ApplyFreeze();
Equal("a frozen pin re-pokes each tick", scanHost.WriteCount, 2);

var narrow = new FrozenValueViewModel(scanHost, 0x2000, ScanWidth.Byte, 5);
int before = scanHost.WriteCount;
narrow.Target = 9999;
Equal("a value too wide for the pin is rejected", narrow.Target, 5L);
Equal("and nothing is written", scanHost.WriteCount, before);

scanHost.AllowWrites = false;
pin.ApplyFreeze();
Check("a failed write is reported", scanHost.FailureReports > 0);

pin.RefreshLive(777);
Equal("the live column updates independently of the target", pin.Live, 777L);
Equal("and the target is untouched", pin.Target, 12345L);

// =================================================================================================
Group("Player row: treasury encoding and slider rebalancing");
// =================================================================================================

var host = new FakeGameHost();
nuint record = 0x50000;
host.Seed(record + (nuint)Civ3Layout.LeaderGoldDecrement, -12345);
host.Seed(record + (nuint)Civ3Layout.LeaderGoldEncoded, 12445);
host.Seed(record + (nuint)Civ3Layout.LeaderLuxurySlider, 0);
host.Seed(record + (nuint)Civ3Layout.LeaderScienceSlider, 6);
host.Seed(record + (nuint)Civ3Layout.LeaderGoldSlider, 4);

var player = new PlayerRowViewModel(host, record, 1, isHuman: true);
player.Refresh(GameTables.Empty);
Equal("the treasury decodes from the two halves", player.Treasury, 100L);
Equal("sliders read back", (player.LuxuryRate, player.ScienceRate, player.TaxRate), (0, 6, 4));

player.Treasury = 5000;
Check("setting the treasury writes the encoded half only",
    host.Writes.Any(w => w.Address == record + (nuint)Civ3Layout.LeaderGoldEncoded));
Check("the game's key is never written",
    host.Writes.All(w => w.Address != record + (nuint)Civ3Layout.LeaderGoldDecrement));
Check("the write decodes back to the requested amount",
    host.ReadInt32(record + (nuint)Civ3Layout.LeaderGoldEncoded, out int enc2)
    && Civ3Layout.DecodeGold(-12345, enc2) == 5000);

host.Writes.Clear();
player.Treasury = 9_000_000_000;
Equal("an absurd treasury is rejected before any write", host.Writes.Count, 0);
Equal("and the row keeps its previous value", player.Treasury, 5000L);

// The freeze re-encodes against the key as it is at that moment, rather than replaying bytes.
player.FreezeTreasury = true;
host.Seed(record + (nuint)Civ3Layout.LeaderGoldDecrement, 777);      // the game re-seeds its key
host.Writes.Clear();
player.ApplyFreeze();
Check("a freeze re-encodes against the current key",
    host.ReadInt32(record + (nuint)Civ3Layout.LeaderGoldEncoded, out int enc3)
    && Civ3Layout.DecodeGold(777, enc3) == 5000);

host.Writes.Clear();
player.ScienceRate = 10;
Equal("raising a slider to 10 zeroes the others", (player.LuxuryRate, player.ScienceRate, player.TaxRate), (0, 10, 0));
Check("the rebalanced set is still valid",
    Civ3Layout.IsPlausibleSliderSet(player.LuxuryRate, player.ScienceRate, player.TaxRate));
Equal("all three sliders are written", host.Writes.Count, 3);

player.ScienceRate = 4;
Check("lowering a slider redistributes the remainder",
    player.LuxuryRate + player.ScienceRate + player.TaxRate == GameFacts.SliderTotal);
player.TaxRate = 100;
Equal("an out-of-range slider is clamped, not rejected outright", player.TaxRate, 10);
Check("and the set stays valid",
    Civ3Layout.IsPlausibleSliderSet(player.LuxuryRate, player.ScienceRate, player.TaxRate));

host.WritesAllowed = false;
host.Writes.Clear();
player.Treasury = 1;
Equal("no write reaches the game when writes are blocked", host.Writes.Count, 0);

// =================================================================================================
Group("Unit and city rows");
// =================================================================================================

var unitHost = new FakeGameHost();
nuint body = 0x60000;
var unitRow = new UnitRowViewModel(unitHost, body, 0);
unitRow.Damage = 3;
Check("damage is written", unitHost.Writes.Any(w => w.Address == body + (nuint)Civ3Layout.UnitDamage && w.Value == 3));
unitRow.FullHeal();
Check("full heal writes zero damage",
    unitHost.Writes.Last(w => w.Address == body + (nuint)Civ3Layout.UnitDamage).Value == 0);
unitRow.MovesUsed = 2;
unitRow.RefreshMoves();
Check("refresh moves writes zero movement",
    unitHost.Writes.Last(w => w.Address == body + (nuint)Civ3Layout.UnitMoves).Value == 0);
Equal("the veteran ladder tops out at Elite = 3", GameFacts.MaxCombatExperience, 3);
unitRow.Experience = 99;
Equal("an out-of-range veteran level clamps to Elite", unitRow.Experience, 3);
unitRow.Experience = 0;                                   // start from Conscript so the promote is real
unitHost.Writes.Clear();
unitRow.MakeElite();
Equal("promote-to-elite reaches the top of the ladder", unitRow.Experience, 3);
Check("and actually writes it",
    unitHost.Writes.Any(w => w.Address == body + (nuint)Civ3Layout.UnitExperience && w.Value == 3));

// A worker mid-job, driven through the same Refresh the poll loop uses — so the row picks the job name
// and its cost out of the loaded ruleset rather than being handed them, which is what makes "Finish
// job" bank the right number under a scenario or mod that priced its jobs differently.
var jobsModule = new FakeModule(0x400000, 0x700000);
jobsModule.WritePeHeader(GameFacts.KnownTimeDateStamp);
jobsModule.PlantGame(Civ3Layout.RvaLeaders);
jobsModule.PlantWorkerJobs(0x2C0000, jobCount: 13, stride: Civ3Layout.WorkerJobStride, 12, 8, 16, 6);
var jobsLoc = new GameLocator(jobsModule).Locate();
var jobsTables = jobsLoc != null ? GameTables.Read(jobsModule, jobsLoc) : GameTables.Empty;
Check("the worker-job fixture located", jobsLoc != null && jobsTables.WorkerJobs.Count == 13);

if (jobsLoc != null)
{
    var workerHost = new FakeGameHost();
    nuint workerBody = 0x70000;
    workerHost.Seed(workerBody + (nuint)Civ3Layout.UnitCivId, 1);
    workerHost.Seed(workerBody + (nuint)Civ3Layout.UnitX, 46);
    workerHost.Seed(workerBody + (nuint)Civ3Layout.UnitY, 26);
    workerHost.Seed(workerBody + (nuint)Civ3Layout.UnitExperience, 1);
    workerHost.Seed(workerBody + (nuint)Civ3Layout.UnitJobId, 3);          // "Job3", costing 6
    workerHost.Seed(workerBody + (nuint)Civ3Layout.UnitJobValue, 2);

    var workerRow = new UnitRowViewModel(workerHost, workerBody, 0);
    Check("a worker row refreshes", workerRow.Refresh(jobsTables, jobsLoc));
    Equal("the job is named out of the ruleset", workerRow.JobName, "Job3");
    Equal("job progress is read as work done", workerRow.JobProgress, 2);
    Check("and the row knows it is working", workerRow.IsWorking);

    workerHost.Writes.Clear();
    Check("finish job reports that it acted", workerRow.FinishJob());
    Check("and banks that job's own cost, scaled for terrain",
        workerHost.Writes.Any(w => w.Address == workerBody + (nuint)Civ3Layout.UnitJobValue
                                   && w.Value == Civ3Layout.WorkerJobWorkToFinish(6)));

    // "Keep worker jobs banked" calls this on every poll, so re-banking an already-banked job has to be
    // free — otherwise the toggle would write to every working unit twice a second for no reason.
    workerHost.Writes.Clear();
    Check("re-banking an already-banked job still reports success", workerRow.FinishJob());
    Equal("but issues no write", workerHost.Writes.Count, 0);

    workerHost.Writes.Clear();
    workerRow.JobProgress = -1;
    Equal("negative job progress is refused", workerHost.Writes.Count, 0);

    // The movement hold writes movement and nothing else — it is not the per-row freeze, which also
    // clears damage. A worker that got its move back can be re-ordered onto the same job in the same
    // turn, which is what forces Civ3 to re-run its "is this job done?" test without waiting a turn.
    workerHost.Writes.Clear();
    workerRow.HoldMoves();
    Check("the movement hold zeroes spent movement",
        workerHost.Writes.Any(w => w.Address == workerBody + (nuint)Civ3Layout.UnitMoves && w.Value == 0));
    Check("and touches nothing else",
        workerHost.Writes.All(w => w.Address == workerBody + (nuint)Civ3Layout.UnitMoves));

    workerHost.WritesAllowed = false;
    workerHost.Writes.Clear();
    workerRow.HoldMoves();
    Equal("the movement hold is refused when writes are blocked", workerHost.Writes.Count, 0);
    workerHost.WritesAllowed = true;

    // An idle unit reads Job_ID as -1, and there is no job on it to finish. Poking Job_Value anyway
    // would write a number nothing reads, so the action has to decline rather than claim success.
    var idleHost = new FakeGameHost();
    nuint idleBody = 0x78000;
    idleHost.Seed(idleBody + (nuint)Civ3Layout.UnitCivId, 1);
    idleHost.Seed(idleBody + (nuint)Civ3Layout.UnitX, 46);
    idleHost.Seed(idleBody + (nuint)Civ3Layout.UnitY, 26);
    idleHost.Seed(idleBody + (nuint)Civ3Layout.UnitJobId, -1);

    var idleRow = new UnitRowViewModel(idleHost, idleBody, 0);
    Check("an idle unit row refreshes", idleRow.Refresh(jobsTables, jobsLoc));
    Check("an idle unit is not working", !idleRow.IsWorking);
    Equal("and shows no job", idleRow.JobName, "");
    idleHost.Writes.Clear();
    Check("finish job declines on an idle unit", !idleRow.FinishJob());
    Equal("and no write reaches the game", idleHost.Writes.Count, 0);
}

// "Finish research" banks points rather than granting a tech: Civ3 compares the accumulated points
// against the advance's cost at the turn boundary. The value has to clear any real tech cost — modded
// or late-game trees included, which is why it is a million rather than the 30,000 it started as —
// while staying far from int overflow, since the game does carry-over arithmetic on it. Banking more
// still does not make an advance arrive instantly; the game appears to floor how few turns one takes.
Check("the finish-research preset clears any plausible tech cost",
    GameFacts.FinishResearchBulbs >= 100_000);
Check("and leaves room for the game's own arithmetic",
    GameFacts.FinishResearchBulbs < int.MaxValue / 1000);
Check("the city-store preset is generous but bounded",
    GameFacts.MaxCityStorePreset is >= 1_000 and <= 100_000);

var researchHost = new FakeGameHost();
nuint researchRecord = 0xB0000;
researchHost.Seed(researchRecord + (nuint)Civ3Layout.LeaderGoldDecrement, -1);
researchHost.Seed(researchRecord + (nuint)Civ3Layout.LeaderResearchBulbs, 12);
var researcher = new PlayerRowViewModel(researchHost, researchRecord, 1, isHuman: true);
researcher.FinishResearch();
Check("finish research writes the banked points",
    researchHost.Writes.Any(w => w.Address == researchRecord + (nuint)Civ3Layout.LeaderResearchBulbs
                                 && w.Value == GameFacts.FinishResearchBulbs));
Equal("and the row reflects it", researcher.ResearchBulbs, GameFacts.FinishResearchBulbs);

researchHost.WritesAllowed = false;
researchHost.Writes.Clear();
researcher.FinishResearch();
Equal("finish research is refused when writes are blocked", researchHost.Writes.Count, 0);

// "Max treasury" writes the amount from the toolbar box rather than the preset, so the row helper has
// to honour whatever it is handed — including a small, plausible-looking amount — and still refuse one
// the game could not hold. The preset survives only as the box's starting value.
var amountHost = new FakeGameHost();
nuint amountRecord = 0xC0000;
amountHost.Seed(amountRecord + (nuint)Civ3Layout.LeaderGoldDecrement, 4242);
var richer = new PlayerRowViewModel(amountHost, amountRecord, 1, isHuman: true);
richer.MaxTreasury(5_000);
Equal("max treasury takes the amount it is given", richer.Treasury, 5_000L);
Check("and still goes through the codec, writing the encoded half only",
    amountHost.ReadInt32(amountRecord + (nuint)Civ3Layout.LeaderGoldEncoded, out int encAmount)
    && Civ3Layout.DecodeGold(4242, encAmount) == 5_000
    && amountHost.Writes.All(w => w.Address != amountRecord + (nuint)Civ3Layout.LeaderGoldDecrement));
amountHost.Writes.Clear();
richer.MaxTreasury(9_000_000_000);
Equal("an amount outside Civ3's range writes nothing", amountHost.Writes.Count, 0);
Equal("and the row keeps the amount that took", richer.Treasury, 5_000L);
Check("the default amount is one Civ3 can hold", Civ3Layout.IsPlausibleTreasury(GameFacts.MaxTreasuryPreset));

// "Max culture" writes a cultural *level*, not a culture total: the level indexes the ruleset's own
// table, so the preset stays a small number that a city record still validates with.
Check("the city-culture preset clears the epic game's ladder but stays a small level",
    GameFacts.MaxCityCulturePreset > 2 && GameFacts.MaxCityCulturePreset <= 20);
BitConverter.TryWriteBytes(city.AsSpan(Civ3Layout.CityCulturalLevel, 4), GameFacts.MaxCityCulturePreset);
Check("a city holding the culture preset still validates", Civ3Layout.ValidateCity(city, 0, 130, 130));
BitConverter.TryWriteBytes(city.AsSpan(Civ3Layout.CityCulturalLevel, 4), 1);

unitHost.Writes.Clear();
unitRow.Damage = -5;
Equal("negative damage is refused", unitHost.Writes.Count, 0);

unitRow.Damage = 4;
unitRow.MovesUsed = 1;
unitHost.Writes.Clear();
unitRow.ApplyFreeze();
Equal("an unfrozen unit is not re-poked", unitHost.Writes.Count, 0);
unitRow.Freeze = true;
unitRow.ApplyFreeze();
Equal("a frozen unit re-pokes damage and movement", unitHost.Writes.Count, 2);
Check("and both are written as zero", unitHost.Writes.All(w => w.Value == 0));

var cityHost = new FakeGameHost();
nuint cityBody = 0x70000;
var cityRow = new CityRowViewModel(cityHost, cityBody, 0);
cityRow.StoredFood = 100;
cityRow.StoredProduction = 200;
cityRow.CulturalLevel = 4;
Equal("three city fields are written", cityHost.Writes.Count, 3);
cityHost.Writes.Clear();
cityRow.StoredFood = -1;
cityRow.CulturalLevel = 500;
Equal("out-of-range city edits are refused", cityHost.Writes.Count, 0);
cityHost.Writes.Clear();
cityRow.CulturalLevel = GameFacts.MaxCityCulturePreset;
Check("max culture writes the preset level into the city record",
    cityHost.Writes.Any(w => w.Address == cityBody + (nuint)Civ3Layout.CityCulturalLevel
                             && w.Value == GameFacts.MaxCityCulturePreset));
Check("and touches nothing else in the record",
    cityHost.Writes.All(w => w.Address == cityBody + (nuint)Civ3Layout.CityCulturalLevel));
cityRow.StoredFood = 300;
cityRow.StoredProduction = 400;
var failHost = new FakeGameHost { FailReads = true };
var failCity = new CityRowViewModel(failHost, 0xA0000, 0);
Check("a city row drops out when its record cannot be read", !failCity.Refresh(GameTables.Empty, loc!));
var failUnit = new UnitRowViewModel(failHost, 0xA0000, 0);
Check("a unit row drops out when its record cannot be read", !failUnit.Refresh(GameTables.Empty, loc!));

cityRow.Freeze = true;
cityHost.Writes.Clear();
cityRow.ApplyFreeze();
Equal("a frozen city re-pokes food and shields", cityHost.Writes.Count, 2);
Check("a frozen city writes back the pinned amounts",
    cityHost.Writes.Any(w => w.Address == cityBody + (nuint)Civ3Layout.CityStoredFood && w.Value == 300)
    && cityHost.Writes.Any(w => w.Address == cityBody + (nuint)Civ3Layout.CityStoredProduction && w.Value == 400));

// The poll loop refreshes a row immediately before applying its freeze, so the freeze has to hold a
// captured target rather than whatever Refresh just read back — otherwise it silently does nothing.
var decayHost = new FakeGameHost();
nuint decayBody = 0x80000;
decayHost.Seed(decayBody + (nuint)Civ3Layout.CityId, 0);
decayHost.Seed(decayBody + (nuint)Civ3Layout.CityStoredFood, 300);
decayHost.Seed(decayBody + (nuint)Civ3Layout.CityStoredProduction, 400);
var decayRow = new CityRowViewModel(decayHost, decayBody, 0);
decayRow.StoredFood = 300;
decayRow.StoredProduction = 400;
decayRow.Freeze = true;
decayHost.Seed(decayBody + (nuint)Civ3Layout.CityStoredFood, 12);        // the game consumed the store
decayHost.Seed(decayBody + (nuint)Civ3Layout.CityStoredProduction, 7);
var decayLoc = loc!;
decayRow.Refresh(GameTables.Empty, decayLoc);
decayHost.Writes.Clear();
decayRow.ApplyFreeze();
Check("a city freeze survives the poll loop's refresh",
    decayHost.Writes.Any(w => w.Address == decayBody + (nuint)Civ3Layout.CityStoredFood && w.Value == 300)
    && decayHost.Writes.Any(w => w.Address == decayBody + (nuint)Civ3Layout.CityStoredProduction && w.Value == 400));

// Blocked writes must be refused up front, not committed locally and left to drift.
var blockedHost = new FakeGameHost { WritesAllowed = false };
var blockedRow = new CityRowViewModel(blockedHost, 0x90000, 0);
blockedRow.StoredFood = 999;
Equal("a blocked city edit writes nothing", blockedHost.Writes.Count, 0);
Equal("and the row does not pretend it took", blockedRow.StoredFood, 0);
Check("and the user is told why", blockedHost.LastReport.Contains("Writes are disabled"));

// =================================================================================================
Console.WriteLine();
Console.WriteLine(failures == 0
    ? $"OK — {checks} checks passed."
    : $"FAILED — {failures} of {checks} checks failed.");
return failures == 0 ? 0 : 1;
