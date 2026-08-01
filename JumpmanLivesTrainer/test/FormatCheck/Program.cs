using JumpmanLivesTrainer.Game;
using JumpmanLivesTrainer.Memory;
using JumpmanLivesTrainer.ViewModels;

namespace FormatCheck;

internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static void Main()
    {
        TestLayoutConstants();
        TestLeAccessors();
        TestValidationHelpers();
        TestGameFacts();
        TestLocatorWithSyntheticMemory();
        TestLocatorEdgeCases();
        TestPlayerViewModel();

        Console.WriteLine();
        Console.WriteLine($"Passed: {_passed}");
        Console.WriteLine($"Failed: {_failed}");
        Environment.Exit(_failed == 0 ? 0 : 1);
    }

    private static void Check(bool condition, string label)
    {
        if (condition) { _passed++; }
        else { _failed++; Console.WriteLine($"  FAIL: {label}"); }
    }

    // --- layout constants ---

    private static void TestLayoutConstants()
    {
        Console.WriteLine("Layout constants");
        Check(GameLayout.AnchorOffset == 0x7D46, "jp1 offset is 0x7D46");
        Check(GameLayout.AnchorBytes.Length == 22, "jp1 anchor is 22 bytes");
        Check(GameLayout.PlayspeedOffset == 0x7D26, "PLAYSPEED offset is 0x7D26");
        Check(GameLayout.PlayspeedBytes.Length == 8, "PLAYSPEED is 8 bytes");
        Check(GameLayout.FtwoOffset == 0x7D90, "ftwo offset is 0x7D90");
        Check(GameLayout.FtwoBytes.Length == 6, "ftwo is 6 bytes");

        Check(GameLayout.OffTrainer == 0x7D2E, "trainer offset is 0x7D2E");
        Check(GameLayout.OffCurrentLevel == 0x7D3A, "current_level offset is 0x7D3A");
        Check(GameLayout.OffBonus == 0x7D3C, "bonus offset is 0x7D3C");
        Check(GameLayout.OffMaxpl == 0x7D40, "maxpl offset is 0x7D40");
        Check(GameLayout.OffPl == 0xD981, "pl offset is 0xD981");
        Check(GameLayout.PlayerArrayOffset == 0xCFE6, "player array offset is 0xCFE6");
        Check(GameLayout.PlayerRecordSize == 92, "player record is 92 bytes");

        Check(GameLayout.PlayerX == 6, "player.x offset is 6");
        Check(GameLayout.PlayerY == 8, "player.y offset is 8");
        Check(GameLayout.PlayerPdeath == 51, "player.pdeath offset is 51");
        Check(GameLayout.PlayerLives == 52, "player.lives offset is 52");
        Check(GameLayout.PlayerSpeed == 80, "player.speed offset is 80");
        Check(GameLayout.PlayerScore == 88, "player.score offset is 88");
        Check(GameLayout.PlayerScoreBytes == 4, "score is 4 bytes");

        Check(GameLayout.MaxLives == 99, "max lives is 99");
        Check(GameLayout.StartingLives == 7, "starting lives is 7");
        Check(GameLayout.TrainerLives == 21, "trainer lives is 21");
        Check(GameLayout.DefaultBonus == 1500, "default bonus is 1500");
        Check(GameLayout.MaxLevel == 45, "max level is 45");
        Check(GameLayout.ExtraLifeThreshold == 10_000, "extra life at 10000");
    }

    // --- LE accessors ---

    private static void TestLeAccessors()
    {
        Console.WriteLine("Little-endian accessors");

        var b = new byte[] { 0x2A, 0x01, 0x00, 0x10, 0xFF };
        Check(GameLayout.ReadU8(b, 0) == 0x2A, "ReadU8");
        Check(GameLayout.ReadI8(b, 4) == -1, "ReadI8 of 0xFF is -1");
        Check(GameLayout.ReadU16(b, 0) == 0x012A, "ReadU16 LE");
        Check(GameLayout.ReadI16(b, 0) == 298, "ReadI16 of 0x012A is 298");
        Check(GameLayout.ReadI32(b, 1) == unchecked((int)0xFF100001), "ReadI32 LE");

        var w = new byte[4];
        GameLayout.WriteI32(w, 0, 1234567);
        Check(GameLayout.ReadI32(w, 0) == 1234567, "WriteI32/ReadI32 round-trip");

        var w2 = new byte[2];
        GameLayout.WriteI16(w2, 0, -100);
        Check(GameLayout.ReadI16(w2, 0) == -100, "WriteI16/ReadI16 round-trip negative");

        GameLayout.WriteU8(w, 0, 42);
        Check(w[0] == 42, "WriteU8");
    }

    // --- validation helpers ---

    private static void TestValidationHelpers()
    {
        Console.WriteLine("Validation helpers");

        int gw = GameLayout.GlobalWindowLength;
        var window = new byte[gw];

        int psRel = GameLayout.PlayspeedOffset - GameLayout.GlobalWindowStart;
        Array.Copy(GameLayout.PlayspeedBytes, 0, window, psRel, GameLayout.PlayspeedBytes.Length);
        int ftRel = GameLayout.FtwoOffset - GameLayout.GlobalWindowStart;
        Array.Copy(GameLayout.FtwoBytes, 0, window, ftRel, GameLayout.FtwoBytes.Length);

        int trainerRel = GameLayout.OffTrainer - GameLayout.GlobalWindowStart;
        int levelRel = GameLayout.OffCurrentLevel - GameLayout.GlobalWindowStart;
        int maxplRel = GameLayout.OffMaxpl - GameLayout.GlobalWindowStart;
        window[trainerRel] = 0;
        window[levelRel] = 1;
        window[maxplRel] = 1;

        Check(GameLayout.ValidateGlobals(window), "ValidateGlobals accepts correct window");
        Check(GameLayout.IsPlausibleGlobals(window), "IsPlausibleGlobals accepts correct values");

        window[levelRel] = 50;
        Check(!GameLayout.IsPlausibleGlobals(window), "level=50 rejected");
        window[levelRel] = 1;

        window[maxplRel] = 5;
        Check(!GameLayout.IsPlausibleGlobals(window), "maxpl=5 rejected");
        window[maxplRel] = 1;

        window[trainerRel] = 2;
        Check(!GameLayout.IsPlausibleGlobals(window), "trainer=2 rejected");
        window[trainerRel] = 0;

        window[psRel] = 0;
        Check(!GameLayout.ValidateGlobals(window), "bad PLAYSPEED rejected");
        window[psRel] = GameLayout.PlayspeedBytes[0];

        window[ftRel] = 0;
        Check(!GameLayout.ValidateGlobals(window), "bad ftwo rejected");
    }

    // --- game facts ---

    private static void TestGameFacts()
    {
        Console.WriteLine("Game facts");

        Check(GameFacts.Levels.Count == 45, "45 levels");
        Check(GameFacts.Levels[0].Number == 1, "first level is 1");
        Check(GameFacts.Levels[44].Number == 45, "last level is 45");
        Check(GameFacts.Levels[0].Name == "NOTHING TO IT", "level 1 title");
        Check(GameFacts.Levels[44].Name == "GRAND PUZZLE III", "level 45 title");

        int zeroBonusCount = GameFacts.Levels.Count(l => l.Bonus == 0);
        Check(zeroBonusCount == 3, "3 levels with bonus=0");

        Check(GameFacts.Controls.Count >= 8, "at least 8 controls listed");
        Check(GameFacts.Tips.Count >= 8, "at least 8 tips listed");

        var sets = GameFacts.Levels.Select(l => l.Set).Distinct().ToList();
        Check(sets.Count == 3, "3 level sets");
        Check(sets.Contains("Jumpman"), "Jumpman set present");
        Check(sets.Contains("Jumpman Jr"), "Jumpman Jr set present");
        Check(sets.Contains("Original"), "Original set present");

        int jumpmanCount = GameFacts.Levels.Count(l => l.Set == "Jumpman");
        Check(jumpmanCount == 12, "12 Jumpman levels");
        int jrCount = GameFacts.Levels.Count(l => l.Set == "Jumpman Jr");
        Check(jrCount == 15, "15 Jumpman Jr levels");
        int origCount = GameFacts.Levels.Count(l => l.Set == "Original");
        Check(origCount == 18, "18 Original levels");
    }

    // --- synthetic memory for locator tests ---

    private sealed class FakeMemory : IMemorySource
    {
        private readonly byte[] _data;
        private readonly int _base;
        private readonly int _size;
        private int _unreadableStart = -1;
        private int _unreadableEnd = -1;

        public FakeMemory(int size, int dataBase, byte[] data)
        {
            _size = size;
            _data = new byte[size];
            _base = dataBase;
            Array.Copy(data, 0, _data, dataBase, Math.Min(data.Length, size - dataBase));
        }

        public void MarkUnreadable(int start, int end) { _unreadableStart = start; _unreadableEnd = end; }

        public IEnumerable<MemoryRegion> EnumerateRegions()
        {
            if (_unreadableStart < 0)
                return new[] { new MemoryRegion((nuint)0, (nuint)_size) };
            return new[]
            {
                new MemoryRegion((nuint)0, (nuint)_unreadableStart),
                new MemoryRegion((nuint)_unreadableEnd, (nuint)(_size - _unreadableEnd)),
            };
        }

        public int Read(nuint address, byte[] buffer, int count)
        {
            int addr = (int)address;
            if (addr < 0 || addr + count > _size) return 0;
            if (_unreadableStart >= 0 && addr >= _unreadableStart && addr < _unreadableEnd) return 0;
            Array.Copy(_data, addr, buffer, 0, count);
            return count;
        }

        public byte[] Read(nuint address, int count)
        {
            var buf = new byte[count];
            int read = Read(address, buf, count);
            if (read != count) Array.Resize(ref buf, read);
            return buf;
        }
    }

    private static byte[] BuildDgroup()
    {
        var d = new byte[0x10000];

        int ps = GameLayout.PlayspeedOffset;
        Array.Copy(GameLayout.PlayspeedBytes, 0, d, ps, GameLayout.PlayspeedBytes.Length);

        int jp1 = GameLayout.AnchorOffset;
        Array.Copy(GameLayout.AnchorBytes, 0, d, jp1, GameLayout.AnchorBytes.Length);

        int ft = GameLayout.FtwoOffset;
        Array.Copy(GameLayout.FtwoBytes, 0, d, ft, GameLayout.FtwoBytes.Length);

        d[GameLayout.OffTrainer] = 0;
        d[GameLayout.OffCurrentLevel] = 1;
        d[GameLayout.OffMaxpl] = 1;
        d[GameLayout.OffPl] = 1;

        int pl1 = GameLayout.PlayerArrayOffset + 0 * GameLayout.PlayerRecordSize;
        d[pl1 + GameLayout.PlayerLives] = 7;
        d[pl1 + GameLayout.PlayerSpeed] = 5;
        GameLayout.WriteI32(d, pl1 + GameLayout.PlayerScore, 12500);

        return d;
    }

    private static void TestLocatorWithSyntheticMemory()
    {
        Console.WriteLine("Locator (synthetic memory)");

        var dgroup = BuildDgroup();
        int dgroupBase = 0x100000;
        var mem = new FakeMemory(0x200000, dgroupBase, dgroup);

        var result = GameLocator.Locate(mem);
        Check(result.Found, "locator found the game");
        Check(result.DgroupAddress == (nuint)dgroupBase, "locator returned correct DGROUP base");
        Check(result.ValidatorsMatched == 2, "both validators matched");

        var p1 = GameLocator.ReadPlayer(mem, result.DgroupAddress, 1);
        Check(p1 != null, "ReadPlayer(1) returns data");
        Check(p1![GameLayout.PlayerLives] == 7, "player 1 lives is 7");
        Check(p1[GameLayout.PlayerSpeed] == 5, "player 1 speed is 5");
        Check(GameLayout.ReadI32(p1, GameLayout.PlayerScore) == 12500, "player 1 score is 12500");

        var p2 = GameLocator.ReadPlayer(mem, result.DgroupAddress, 2);
        Check(p2 != null, "ReadPlayer(2) returns data");
        Check(p2![GameLayout.PlayerLives] == 0, "player 2 lives is 0 (empty slot)");

        Check(GameLocator.ReadPlayer(mem, result.DgroupAddress, 0) == null, "player index 0 rejected");
        Check(GameLocator.ReadPlayer(mem, result.DgroupAddress, 5) == null, "player index 5 rejected");
    }

    private static void TestLocatorEdgeCases()
    {
        Console.WriteLine("Locator edge cases");

        var dgroup = BuildDgroup();
        int dgroupBase = 0x100000;
        var mem = new FakeMemory(0x200000, dgroupBase, dgroup);

        var result = GameLocator.Locate(mem);
        Check(result.Found, "locator found the game");

        dgroup[GameLayout.OffCurrentLevel] = 99;
        var mem2 = new FakeMemory(0x200000, dgroupBase, dgroup);
        var result2 = GameLocator.Locate(mem2);
        Check(!result2.Found, "implausible level rejected");
        Check(result2.AnchorsMatchedButGlobalsDidNot, "reported as anchors-matched-but-rejected");

        dgroup[GameLayout.OffCurrentLevel] = 1;

        var badDgroup = new byte[0x10000];
        var mem3 = new FakeMemory(0x200000, dgroupBase, badDgroup);
        var result3 = GameLocator.Locate(mem3);
        Check(!result3.Found, "empty memory not found");
        Check(!result3.AnchorsMatchedButGlobalsDidNot, "empty memory is not anchors-matched-but-rejected");

        var ct = new CancellationTokenSource();
        ct.Cancel();
        try
        {
            GameLocator.Locate(mem, ct.Token);
            Check(false, "cancelled locate should throw");
        }
        catch (OperationCanceledException)
        {
            Check(true, "cancelled locate throws OperationCanceledException");
        }
    }

    // --- PlayerViewModel tests ---

    private sealed class FakeGameHost : IGameHost
    {
        public byte[] Memory { get; }
        public string LastStatus { get; private set; } = "";
        public bool WriteSucceeds { get; set; } = true;
        public int WriteCount { get; private set; }

        public FakeGameHost(byte[] memory) { Memory = memory; }

        public bool WriteBytes(int dgroupOffset, byte[] bytes)
        {
            WriteCount++;
            if (!WriteSucceeds) return false;
            Array.Copy(bytes, 0, Memory, dgroupOffset, bytes.Length);
            return true;
        }

        public void ReportStatus(string message) => LastStatus = message;
    }

    private static LocateResult BuildLocateResult(int dgroupBase, byte[] dgroup)
    {
        var globals = new byte[GameLayout.GlobalWindowLength];
        Array.Copy(dgroup, GameLayout.GlobalWindowStart, globals, 0, globals.Length);
        return new LocateResult((nuint)dgroupBase, globals, "test", 2);
    }

    private static void TestPlayerViewModel()
    {
        Console.WriteLine("PlayerViewModel");

        var dgroup = BuildDgroup();
        int dgroupBase = 0x100000;
        var host = new FakeGameHost(dgroup);
        var located = BuildLocateResult(dgroupBase, dgroup);

        var vm = new PlayerViewModel(host, located, 1);
        Array.Copy(dgroup, GameLayout.PlayerArrayOffset, vm.LivePlayer, 0, GameLayout.PlayerRecordSize);
        Array.Copy(located.Globals, vm.LiveGlobals, Math.Min(located.Globals.Length, vm.LiveGlobals.Length));
        vm.SyncFromLive();

        Check(vm.Lives == 7, "initial lives is 7");
        Check(vm.Score == 12500, "initial score is 12500");
        Check(vm.Speed == 5, "initial speed is 5");
        Check(vm.CurrentLevel == 1, "initial level is 1");

        vm.Lives = 50;
        Check(vm.Lives == 50, "lives set to 50");
        Check(dgroup[GameLayout.PlayerArrayOffset + GameLayout.PlayerLives] == 50,
            "lives written to memory at correct offset");

        vm.Lives = 150;
        Check(vm.Lives == 99, "lives clamped to 99");

        vm.Lives = -5;
        Check(vm.Lives == 0, "lives clamped to 0");

        vm.Score = 999999;
        Check(vm.Score == 999999, "score set to 999999");

        vm.Speed = 10;
        Check(vm.Speed == 8, "speed clamped to 8");

        vm.Speed = 0;
        Check(vm.Speed == 1, "speed clamped to 1");

        vm.CurrentLevel = 50;
        Check(vm.CurrentLevel == 45, "level clamped to 45");

        vm.Bonus = 2000;
        Check(vm.Bonus == 1500, "bonus clamped to 1500");

        vm.TrainerMode = true;
        Check(vm.TrainerMode, "trainer mode enabled");
        Check(dgroup[GameLayout.OffTrainer] == 1, "trainer flag written to memory");

        vm.MaxEverythingCommand.Execute(null);
        Check(vm.Lives == 99, "max everything sets lives to 99");
        Check(vm.Bonus == 1500, "max everything sets bonus to 1500");
        Check(vm.TrainerMode, "max everything enables trainer mode");

        vm.LivePlayer[GameLayout.PlayerLives] = 3;
        vm.FreezeLives = true;
        vm.OnPolled();
        Check(dgroup[GameLayout.PlayerArrayOffset + GameLayout.PlayerLives] == 99,
            "freeze writes pinned lives when live differs");

        vm.FreezeLives = false;

        host.WriteSucceeds = false;
        vm.Lives = 42;
        Check(vm.Lives == 99, "failed write does not update shadow");
        Check(host.LastStatus.Contains("Write failed"), "failed write reports error");
    }
}
