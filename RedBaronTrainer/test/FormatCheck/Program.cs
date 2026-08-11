using System.IO;
using System.Text;
using GameTrainers.Common.Memory;
using RedBaronTrainer.Game;
using RedBaronTrainer.Memory;

namespace RedBaronTrainer.FormatCheck;

/// <summary>
/// Headless checks for everything in the trainer that can be asserted without a desktop: the
/// realism codec, the pilot-record parser, the emulator-config parsing, the file writers, and an
/// end-to-end run of <see cref="GameLocator"/> over a synthetic guest. Exits 0 on success, 1 on any
/// failure.
///
/// <para>Pass <c>--game &lt;dir&gt;</c> to additionally parse a real installation's
/// <c>ROSTER.DAT</c>, <c>MREAL.PRF</c> and <c>CREAL.PRF</c>, and <c>--live &lt;pid&gt;</c> to run
/// the locator against a running emulator. Both extras are read-only, and neither is needed for the
/// default run — no copyrighted bytes live in this repository.</para>
/// </summary>
internal static class Program
{
    private static int _failures;

    private static int Main(string[] args)
    {
        string? gameDir = null;
        int livePid = 0;
        bool badArgs = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--game" when i + 1 < args.Length: gameDir = args[++i]; break;
                case "--live" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out livePid))
                    {
                        Console.WriteLine($"  [FAIL] --live expects a process id, got '{args[i]}'");
                        badArgs = true;
                    }
                    break;
                case "--game":
                case "--live":
                    Console.WriteLine($"  [FAIL] {args[i]} needs a value");
                    badArgs = true;
                    break;
            }
        }
        if (badArgs) _failures++;

        Section("realism panel codec");
        CheckRealismCodec();

        Section("pilot records");
        CheckPilotRecords();

        Section("emulator config parsing");
        CheckConfigParsing();

        Section("file writers");
        CheckFileWriters();

        Section("locator over a synthetic guest");
        CheckLocator();

        if (gameDir != null)
        {
            Section($"live game files in {gameDir}");
            CheckGameFiles(gameDir);
        }
        else
        {
            Console.WriteLine("(skipping the on-disk file checks; pass --game <dir> to include them)");
        }

        if (livePid != 0)
        {
            Section($"live emulator, pid {livePid}");
            CheckLive(livePid);
        }
        else
        {
            Console.WriteLine("(skipping the live locate; pass --live <dosbox pid> to include it)");
        }

        Console.WriteLine();
        Console.WriteLine(_failures == 0 ? "All checks passed." : $"{_failures} check(s) FAILED.");
        return _failures == 0 ? 0 : 1;
    }

    // ------------------------------------------------------------------ realism

    private static void CheckRealismCodec()
    {
        Check("thirteen settings are described", RealismSettings.All.Length == GameFacts.RealismSettingCount);
        Check("setting indices are 0..12 in order",
            !RealismSettings.All.Where((s, i) => s.Index != i).Any());

        foreach (var (name, preset) in new (string, ushort[])[]
                 {
                     ("Novice", RealismSettings.Novice),
                     ("Expert", RealismSettings.Expert),
                     ("No limits", RealismSettings.Invulnerable),
                 })
        {
            Check($"{name} preset has 13 values", preset.Length == GameFacts.RealismSettingCount);
            var block = RealismSettings.Encode(preset);
            Check($"{name} encodes to 26 bytes", block.Length == GameFacts.RealismBlockSize);
            var back = RealismSettings.Decode(block);
            Check($"{name} round-trips", back != null && back.SequenceEqual(preset));
            Check($"{name} is recognised as plausible", RealismSettings.LooksPlausible(block));
        }

        // The two presets are what the game itself writes, so they are the strongest fixture there is:
        // exactly one setting falls between Novice and Expert, and it is Midair Collisions.
        var novice = RealismSettings.Novice;
        var expert = RealismSettings.Expert;
        var dropped = Enumerable.Range(0, GameFacts.RealismSettingCount)
                                .Where(i => expert[i] < novice[i]).ToArray();
        Check("exactly one setting is lower at Expert than Novice", dropped.Length == 1);
        Check("that setting is Midair collisions",
            dropped.Length == 1 && RealismSettings.All[dropped[0]].Name == "Midair collisions");
        Check("Expert sets combat level to Hard", expert[10] == 2);
        Check("Expert sets flight model to Expert", expert[12] == 2);

        // "No limits" must differ from Expert in exactly the settings the UI and the docs name -
        // no more. Turning off blackouts or carburettor freezes would make it quietly easier than
        // the preset it claims to be, and the README would be wrong.
        var invulnerable = RealismSettings.Invulnerable;
        var cleared = Enumerable.Range(0, GameFacts.RealismSettingCount)
                                .Where(i => invulnerable[i] != expert[i]).ToArray();
        var advertised = Enumerable.Range(0, GameFacts.RealismSettingCount)
                                   .Where(i => RealismSettings.All[i].OffIsEasier).ToArray();
        Check("No limits differs from Expert exactly where OffIsEasier says",
            cleared.SequenceEqual(advertised),
            $"cleared [{string.Join(",", cleared)}] vs advertised [{string.Join(",", advertised)}]");
        foreach (int i in advertised)
            Check($"No limits clears '{RealismSettings.All[i].Name}'", invulnerable[i] == 0);
        Check("No limits leaves combat level on Hard", invulnerable[10] == 2);
        Check("No limits leaves the flight model on Expert", invulnerable[12] == 2);

        Check("an out-of-range value is rejected",
            RealismSettings.Decode(new byte[] { 5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }) == null);
        Check("a short block is rejected", RealismSettings.Decode(new byte[10]) == null);
        Check("an all-zero block is not plausible", !RealismSettings.LooksPlausible(new byte[GameFacts.RealismBlockSize]));
    }

    // ------------------------------------------------------------------ pilots

    private static void CheckPilotRecords()
    {
        var record = new PilotRecord(MakePilot("Zeno Zwick"));
        Check("name is read back", record.Name == "Zeno Zwick");
        Check("occupied slot is detected", record.IsOccupied);

        record.SetName("Manfred");
        Check("name is replaced", record.Name == "Manfred");
        var bytes = record.ToArray();
        Check("the whole name field is cleared behind a shorter name",
            bytes.Take(GameFacts.PilotNameLength).Skip(7).All(b => b == 0));
        Check("bytes past the name are untouched by SetName",
            bytes.Skip(GameFacts.PilotNameLength).SequenceEqual(
                MakePilot("Zeno Zwick").Skip(GameFacts.PilotNameLength)));

        Check("an over-long name is truncated inside the field",
            new PilotRecord(MakePilot("x")).Also(r => r.SetName(new string('A', 40))).Name.Length
                == GameFacts.PilotNameLength - 1);

        Check("a normal name is writable", PilotRecord.IsWritableName("Werner Voss"));
        Check("an empty name is refused", !PilotRecord.IsWritableName(""));
        Check("a null name is refused", !PilotRecord.IsWritableName(null));
        Check("a 17-character name is writable", PilotRecord.IsWritableName(new string('A', 17)));
        Check("an 18-character name is refused", !PilotRecord.IsWritableName(new string('A', 18)));
        Check("a control character is refused", !PilotRecord.IsWritableName("Voss\tW"));
        Check("a high-bit character is refused", !PilotRecord.IsWritableName("Vo\u00dfs"));

        var empty = new byte[GameFacts.PilotRecordSize];
        Check("an all-zero slot is empty", PilotRecord.IsEmptySlot(empty, 0));
        Check("an all-zero slot is not occupied", !PilotRecord.IsOccupiedSlot(empty, 0));

        // The shell does not scrub what it stops using, so a vacated slot keeps its career bytes.
        var vacated = MakePilot("Gone");
        Array.Clear(vacated, 0, GameFacts.PilotNameLength);
        Check("a slot with a cleared name but residual career bytes counts as empty",
            PilotRecord.IsEmptySlot(vacated, 0));
        Check("...and not as occupied", !PilotRecord.IsOccupiedSlot(vacated, 0));

        var unterminated = MakePilot(new string('A', GameFacts.PilotNameLength));
        Check("an unterminated name is rejected", !PilotRecord.IsOccupiedSlot(unterminated, 0));

        var roster = new byte[GameFacts.RosterSlots * GameFacts.PilotRecordSize];
        MakePilot("Ernst Udet").CopyTo(roster, 0);
        MakePilot("Werner Voss").CopyTo(roster, 3 * GameFacts.PilotRecordSize);
        Check("a sparse roster is plausible", PilotRecord.IsPlausibleRoster(roster));
        Check("an empty roster is not plausible",
            !PilotRecord.IsPlausibleRoster(new byte[GameFacts.RosterSlots * GameFacts.PilotRecordSize]));

        vacated.CopyTo(roster, 5 * GameFacts.PilotRecordSize);
        Check("a vacated slot does not fail the roster check", PilotRecord.IsPlausibleRoster(roster));

        roster[GameFacts.PilotRecordSize] = 0xFF;   // neither a pilot nor a free slot
        Check("a corrupt slot fails the roster check", !PilotRecord.IsPlausibleRoster(roster));
    }

    // ------------------------------------------------------------------ config parsing

    private static void CheckConfigParsing()
    {
        foreach (var (line, expected) in new (string, string?)[]
                 {
                     (@"mount c C:\Temp\Win31DOSBox\C-DRIVE", @"C:\Temp\Win31DOSBox\C-DRIVE"),
                     (@"mount c c:\games -t dir", @"c:\games"),
                     (@"mount c c:\games -freesize 1024", @"c:\games"),
                     (@"mount d d:\ -t cdrom -usecd 0 -ioctl", @"d:\"),
                     (@"  MOUNT C ""C:\Program Files\My Games\RED""  ", @"C:\Program Files\My Games\RED"),
                     (@"mount c c:\dos-games -label GAMES", @"c:\dos-games"),
                     (@"imgmount c disk.img", null),
                     (@"c:", null),
                 })
        {
            Check($"mount line: {line.Trim()}", DosBoxInspector.ParseMountPath(line) == expected,
                $"got '{DosBoxInspector.ParseMountPath(line)}'");
        }

        string conf = Path.Combine(Path.GetTempPath(), $"rbtrainer-conf-{Guid.NewGuid():N}.conf");
        try
        {
            File.WriteAllText(conf, string.Join(Environment.NewLine, new[]
            {
                "# a comment",
                "[cpu]",
                "core=normal",
                "cycles=max",
                "",
                "[joystick]",
                "joysticktype = auto",
                "timed=true",
            }));

            var settings = DosBoxInspector.ReadSettings(conf);
            Check("section.key flattening works", settings.GetValueOrDefault("cpu.cycles") == "max");
            Check("spaces around '=' are trimmed", settings.GetValueOrDefault("joystick.joysticktype") == "auto");
            Check("comments are ignored", !settings.ContainsKey("# a comment"));

            var findings = DosBoxInspector.CheckConfig(conf);
            Check("cycles=max is reported as an error",
                findings.Any(f => f.Setting.Contains("cycles") && f.Severity == "error"));
            Check("joysticktype=auto is reported as a warning",
                findings.Any(f => f.Setting.Contains("joysticktype") && f.Severity == "warn"));
            Check("timed=true is reported as a warning",
                findings.Any(f => f.Setting.Contains("timed") && f.Severity == "warn"));

            File.WriteAllText(conf, "[cpu]\ncycles=fixed 12000\n[joystick]\njoysticktype=2axis\ntimed=false\n");
            var good = DosBoxInspector.CheckConfig(conf);
            Check("a good config produces no errors or warnings",
                good.All(f => f.Severity == "ok"));
        }
        finally
        {
            TryDelete(conf);
        }
    }

    // ------------------------------------------------------------------ file writers

    private static void CheckFileWriters()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"rbtrainer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var folder = new GameFolder(dir);
            string mreal = folder.PathOf(GameFacts.MissionRealismFileName);

            // A file the shipped size: the writer must replace it exactly.
            File.WriteAllBytes(mreal, RealismSettings.Encode(RealismSettings.Novice));
            folder.BackUpOnce(GameFacts.MissionRealismFileName);
            Check("BackUpOnce creates the backup", File.Exists(mreal + ".bak"));
            Check("the backup holds the original bytes",
                RealismSettings.Decode(File.ReadAllBytes(mreal + ".bak"))!.SequenceEqual(RealismSettings.Novice));

            folder.WriteRealism(career: false, RealismSettings.Invulnerable);
            Check("WriteRealism round-trips through ReadRealism",
                folder.ReadRealism(career: false)!.SequenceEqual(RealismSettings.Invulnerable));
            Check("WriteRealism leaves the file the shipped size",
                new FileInfo(mreal).Length == GameFacts.RealismBlockSize);
            Check("no temporary file is left behind", !File.Exists(mreal + ".tmp"));

            folder.BackUpOnce(GameFacts.MissionRealismFileName);
            Check("BackUpOnce does not overwrite an existing backup",
                RealismSettings.Decode(File.ReadAllBytes(mreal + ".bak"))!.SequenceEqual(RealismSettings.Novice));

            // A longer variant: everything past the 26 bytes we understand must survive.
            var longer = new byte[GameFacts.RealismBlockSize + 6];
            RealismSettings.Encode(RealismSettings.Novice).CopyTo(longer, 0);
            for (int i = GameFacts.RealismBlockSize; i < longer.Length; i++) longer[i] = (byte)(0xA0 + i);
            File.WriteAllBytes(folder.PathOf(GameFacts.CareerRealismFileName), longer);
            folder.WriteRealism(career: true, RealismSettings.Expert);
            var after = File.ReadAllBytes(folder.PathOf(GameFacts.CareerRealismFileName));
            Check("a longer realism file keeps its length", after.Length == longer.Length);
            Check("bytes past the block are preserved",
                after.Skip(GameFacts.RealismBlockSize).SequenceEqual(longer.Skip(GameFacts.RealismBlockSize)));
            Check("the block itself was replaced",
                RealismSettings.Decode(after)!.SequenceEqual(RealismSettings.Expert));

            // Writing where no file exists yet must create one rather than throw.
            TryDelete(mreal);
            folder.WriteRealism(career: false, RealismSettings.Novice);
            Check("WriteRealism creates a missing file",
                folder.ReadRealism(career: false)!.SequenceEqual(RealismSettings.Novice));

            // A roster the shipped shape must parse; a truncated one must be refused rather than
            // half-read, because a partial parse is how a save editor eats somebody's careers.
            var roster = new byte[GameFacts.RosterFileHeaderSize + GameFacts.RosterSlots * GameFacts.PilotRecordSize];
            new byte[] { 0xFF, 0xFF, 0, 0, 0, 0, 0x0A, 0 }.CopyTo(roster, 0);
            MakePilot("Albert Ball").CopyTo(roster, GameFacts.RosterFileHeaderSize);
            File.WriteAllBytes(folder.PathOf(GameFacts.RosterFileName), roster);
            var read = folder.ReadRoster();
            Check("ROSTER.DAT parses", read != null);
            Check("the header comes back verbatim",
                read != null && read.Value.Header.SequenceEqual(roster.Take(GameFacts.RosterFileHeaderSize)));
            Check("slot 0 is the pilot we wrote", read?.Pilots[0].Name == "Albert Ball");
            Check("the other slots read as free", read != null && !read.Value.Pilots[1].IsOccupied);

            File.WriteAllBytes(folder.PathOf(GameFacts.RosterFileName), roster.Take(500).ToArray());
            Check("a truncated ROSTER.DAT is refused", folder.ReadRoster() == null);

            Check("a folder without RB.EXE is not a game folder", !GameFolder.IsGameFolder(dir));
            Check("a nonexistent folder is not a game folder",
                !GameFolder.IsGameFolder(Path.Combine(dir, "nope")));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    // ------------------------------------------------------------------ locator

    private static void CheckLocator()
    {
        foreach (var module in new[] { GameModule.Shell, GameModule.Simulator })
        {
            var guest = new FakeGuest(module);
            var located = GameLocator.Find(guest, out string status);
            Check($"{module}: located", located != null, status);
            if (located == null) continue;

            Check($"{module}: module identified", located.Module == module);
            Check($"{module}: data group is where it was planted",
                located.Dgroup == guest.HostBase + (nuint)FakeGuest.DgroupLinear);
            Check($"{module}: all validators matched", located.ValidatorsMatched == 4);
            Check($"{module}: segment reported correctly",
                located.DgroupSegment == FakeGuest.DgroupLinear / 16);
            Check($"{module}: not flagged ambiguous", !located.Ambiguous);
            Check($"{module}: the anchor check agrees", GameLocator.AnchorStillMatches(guest, located));

            if (module == GameModule.Shell)
            {
                Check("shell: realism block located",
                    located.RealismAddress == located.AtOffset(GameFacts.ShellRealismOffset));
                Check("shell: roster located",
                    located.RosterAddress == located.AtOffset(GameFacts.RosterOffset));
                Check("shell: active career located",
                    located.ActivePilotAddress == located.AtOffset(GameFacts.ActivePilotOffset));

                var block = RealismSettings.Encode(RealismSettings.Invulnerable);
                Check("shell: realism write lands", guest.Write(located.RealismAddress, block));
                var read = RealismSettings.Decode(guest.Read(located.RealismAddress, block.Length));
                Check("shell: realism write reads back",
                    read != null && read.SequenceEqual(RealismSettings.Invulnerable));
            }
            else
            {
                Check("sim: joystick flag located",
                    located.JoystickFlagAddress == located.AtOffset(GameFacts.SimJoystickFlagOffset));
                Check("sim: joystick mirror located",
                    located.JoystickMirrorAddress == located.AtOffset(GameFacts.SimJoystickFlagMirrorOffset));
            }
        }

        // A guest with the anchor present but the corroborating literals scrubbed must be refused.
        var poisoned = new FakeGuest(GameModule.Shell, scrubValidators: true);
        Check("a lone anchor is not enough", GameLocator.Find(poisoned, out _) == null);

        // The anchor at a non-paragraph address is a copy in a scratch buffer, not a data group.
        var misaligned = new FakeGuest(GameModule.Shell, dgroupSkew: 3);
        Check("a misaligned data group is refused", GameLocator.Find(misaligned, out _) == null);

        // DOS does not scrub memory it frees, so the program that just exited can still be lying in
        // guest RAM. Nothing in guest memory says which of two identical-looking data groups is the
        // live one, so the contract is not "pick right" - it is "keep looking past the first hit,
        // and admit the doubt".
        var both = new FakeGuest(GameModule.Shell, alsoPlant: GameModule.Simulator);
        var pick = GameLocator.Find(both, out string ambiguousStatus);
        Check("two data groups still produce a locate", pick != null, ambiguousStatus);
        Check("...and it is flagged ambiguous", pick?.Ambiguous == true);
        Check("...and the status says so", ambiguousStatus.Contains("still in guest RAM"));

        // The realistic leftover is a second copy of the *same* module: the chain visits PS.EXE
        // twice around every mission. That must be flagged too, not silently resolved to whichever
        // copy sits at the lower address.
        var twoShells = new FakeGuest(GameModule.Shell, alsoPlant: GameModule.Shell);
        var shellPick = GameLocator.Find(twoShells, out string twoShellStatus);
        Check("two copies of the same module still produce a locate", shellPick != null, twoShellStatus);
        Check("...and that is flagged ambiguous too", shellPick?.Ambiguous == true);

        // One unreadable page must not cost the sweep the megabyte around it. The anchor is planted
        // far past the hole, so a locator that just skipped forward would never reach it.
        var holed = new FakeGuest(GameModule.Shell, unreadablePageAt: 0x20000);
        var throughHole = GameLocator.Find(holed, out string holeStatus);
        Check("an unreadable page does not hide the rest of the region", throughHole != null, holeStatus);
        Check("...and the data group is still the right one",
            throughHole?.Dgroup == holed.HostBase + (nuint)FakeGuest.DgroupLinear);

        // The page immediately *before* a hole is the hard case: the salvage read asks for a page
        // plus the anchor overlap, which reaches into the dead page and fails the whole call, so a
        // salvage pass without a retry loses 4 KB of perfectly readable RAM - and the anchor with it.
        int anchorPage = (FakeGuest.GuestPad + FakeGuest.DgroupLinear + GameFacts.ShellAnchorOffset) & ~0xFFF;
        var holeAfterAnchor = new FakeGuest(GameModule.Shell, unreadablePageAt: anchorPage + 0x1000);
        var found = GameLocator.Find(holeAfterAnchor, out string edgeStatus);
        Check("an anchor in the page before an unreadable one is still found", found != null, edgeStatus);

        // A locate can beat the game to its own BSS: the anchor is in initialised data and valid the
        // instant the image is mapped, while the roster and realism block are still zero. Nothing
        // re-sweeps after that, so the structures have to be re-resolvable in place.
        var early = new FakeGuest(GameModule.Shell, blankBss: true);
        var earlyLocate = GameLocator.Find(early, out string earlyStatus);
        Check("a data group with an empty BSS still locates", earlyLocate != null, earlyStatus);
        Check("...with nothing resolved yet",
            earlyLocate != null && GameLocator.HasUnresolvedStructures(earlyLocate));
        Check("...and no roster address to write through", earlyLocate?.RosterAddress == 0);
        early.FillBss();
        var reresolved = earlyLocate == null ? null : GameLocator.Reresolve(early, earlyLocate);
        Check("re-resolving picks the structures up once the game fills them in",
            reresolved != null && !GameLocator.HasUnresolvedStructures(reresolved));
        Check("...at the same data group",
            reresolved?.Dgroup == early.HostBase + (nuint)FakeGuest.DgroupLinear);
        Check("...and the roster is where it should be",
            reresolved?.RosterAddress == reresolved?.AtOffset(GameFacts.RosterOffset));

        // Cancellation must be honoured rather than swallowed.
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        bool cancelled = false;
        try { GameLocator.Find(new FakeGuest(GameModule.Shell), out _, cts.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check("a cancelled sweep throws", cancelled);
    }

    // ------------------------------------------------------------------ optional passes

    private static void CheckGameFiles(string dir)
    {
        Check("folder looks like a Red Baron installation", GameFolder.IsGameFolder(dir));
        if (!GameFolder.IsGameFolder(dir)) return;

        var folder = new GameFolder(dir);

        foreach (bool career in new[] { false, true })
        {
            string name = career ? GameFacts.CareerRealismFileName : GameFacts.MissionRealismFileName;
            var values = folder.ReadRealism(career);
            Check($"{name} parses", values != null);
            if (values != null)
                Console.WriteLine($"    {name}: {string.Join(" ", values)}");
        }

        var roster = folder.ReadRoster();
        Check("ROSTER.DAT parses", roster != null);
        if (roster == null) return;
        Console.WriteLine($"    header: {BitConverter.ToString(roster.Value.Header)}");
        foreach (var pilot in roster.Value.Pilots)
            Console.WriteLine($"    {(pilot.IsOccupied ? pilot.Name : "(empty)")}");
    }

    /// <summary>
    /// Runs the real locate against a running emulator. Read-only: it reports what it found and never
    /// writes, so it is safe to point at a game mid-mission.
    /// </summary>
    private static void CheckLive(int pid)
    {
        System.Diagnostics.Process process;
        try { process = System.Diagnostics.Process.GetProcessById(pid); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            Check("the pid names a running process", false, e.Message);
            return;
        }

        using (process)
        {
            string? conf = DosBoxInspector.FindConfigFile(process);
            Console.WriteLine($"    emulator config: {conf ?? "(not found)"}");
            string? folder = DosBoxInspector.FindGameFolder(conf);
            Check("game folder discovered from the emulator's mount lines", folder != null, "no mount matched");
            if (folder != null) Console.WriteLine($"    game folder: {folder}");
            foreach (var finding in DosBoxInspector.CheckConfig(conf))
                Console.WriteLine($"    [{finding.Severity}] {finding.Setting} = {finding.Value}");

            ProcessMemory mem;
            try { mem = ProcessMemory.Open(pid); }
            catch (Exception e)
            {
                Check("opened the emulator process", false, e.Message);
                return;
            }

            using (mem)
            {
                var source = new ProcessMemorySource(mem);
                var located = GameLocator.Find(source, out string status);
                Check("located Red Baron in the live emulator", located != null, status);
                if (located == null) return;

                Console.WriteLine($"    {status}");
                Console.WriteLine($"    guest linear 0 at host 0x{(ulong)located.GuestZero:X}");
                Console.WriteLine($"    data group     at host 0x{(ulong)located.Dgroup:X} (DS {located.DgroupSegment:X4})");
                Check("the anchor check agrees with the locate", GameLocator.AnchorStillMatches(source, located));

                if (located.Module == GameModule.Shell)
                {
                    Check("shell: realism panel located", located.RealismAddress != 0);
                    if (located.RealismAddress != 0)
                    {
                        var values = RealismSettings.Decode(source.Read(located.RealismAddress, GameFacts.RealismBlockSize));
                        Console.WriteLine($"    live realism: {(values == null ? "(invalid)" : string.Join(" ", values))}");
                    }
                    Check("shell: roster located", located.RosterAddress != 0);
                    if (located.RosterAddress != 0)
                    {
                        int bytes = GameFacts.RosterSlots * GameFacts.PilotRecordSize;
                        var buf = source.Read(located.RosterAddress, bytes);
                        Check("shell: roster reads back in full", buf.Length == bytes);
                        if (buf.Length == bytes)
                        {
                            for (int slot = 0; slot < GameFacts.RosterSlots; slot++)
                            {
                                int off = slot * GameFacts.PilotRecordSize;
                                if (!PilotRecord.IsOccupiedSlot(buf, off)) continue;
                                Console.WriteLine($"    slot {slot + 1}: {new PilotRecord(buf.AsSpan(off, GameFacts.PilotRecordSize)).Name}");
                            }
                        }
                    }
                    if (located.ActivePilotAddress != 0)
                    {
                        var buf = source.Read(located.ActivePilotAddress, GameFacts.PilotRecordSize);
                        if (buf.Length == GameFacts.PilotRecordSize)
                            Console.WriteLine($"    active career: {new PilotRecord(buf).Name}");
                    }
                }
                else
                {
                    Check("sim: joystick flag located", located.JoystickFlagAddress != 0);
                    if (located.JoystickFlagAddress != 0)
                    {
                        var b = source.Read(located.JoystickFlagAddress, 1);
                        Console.WriteLine($"    stick and rudder: {(b.Length == 1 && b[0] != 0 ? "enabled" : "disabled")}");
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------ helpers

    private static byte[] MakePilot(string name)
    {
        var record = new byte[GameFacts.PilotRecordSize];
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(name.Length, GameFacts.PilotNameLength)).CopyTo(record);
        // Some non-zero tail so "did SetName touch anything past the name?" is a real question.
        for (int i = GameFacts.PilotNameLength; i < record.Length; i++) record[i] = (byte)(i & 0x7F);
        return record;
    }

    private static PilotRecord Also(this PilotRecord record, Action<PilotRecord> action)
    {
        action(record);
        return record;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"== {title}");
    }

    private static void Check(string what, bool ok, string? detail = null)
    {
        Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}{(detail != null && !ok ? $" - {detail}" : "")}");
        if (!ok) _failures++;
    }
}

/// <summary>
/// A synthetic DOSBox: one 2 MB "guest RAM" region containing an emulated BIOS data area and one of
/// Red Baron's two data groups, laid out exactly as the locator expects to find them. Lets the whole
/// locate path be exercised with no emulator, no game, and no copyrighted bytes in the repository.
///
/// <para>It can also plant the <i>other</i> module's data group (the leftover DOS did not scrub) and
/// mark a page unreadable, because both are things the real thing does and neither is visible from a
/// happy-path fixture.</para>
/// </summary>
internal sealed class FakeGuest : IMemorySource
{
    public const int GuestSize = 2 << 20;
    public const int GuestPad = 0x40;         // DOSBox pads its allocation; the locator must not assume 0
    public const int DgroupLinear = 0x3E590;  // paragraph-aligned, as a real DS: base always is
    public const int StaleDgroupLinear = 0x1E590;

    private readonly byte[] _ram = new byte[GuestPad + GuestSize];
    private readonly int _unreadablePage;
    private readonly GameModule _module;

    public nuint HostBase { get; } = 0x10000;   // arbitrary "host address" of guest linear 0

    public FakeGuest(GameModule module, bool scrubValidators = false, int dgroupSkew = 0,
        GameModule alsoPlant = GameModule.None, int unreadablePageAt = -1, bool blankBss = false)
    {
        _unreadablePage = unreadablePageAt;
        _module = module;

        // BIOS data area: 40:0000 = COM1 port, 40:0013 = conventional memory size in KB.
        Write16(GuestPad + 0x400, 0x03F8);
        Write16(GuestPad + 0x413, 640);

        Plant(module, GuestPad + DgroupLinear + dgroupSkew, scrubValidators, blankBss);
        if (alsoPlant != GameModule.None)
            Plant(alsoPlant, GuestPad + StaleDgroupLinear, scrubValidators: false, blankBss: false);
    }

    /// <summary>Writes the BSS structures a program fills in after it has been mapped and started reading files.</summary>
    public void FillBss() => PlantBss(_module, GuestPad + DgroupLinear);

    private void Plant(GameModule module, int dgroup, bool scrubValidators, bool blankBss)
    {
        var (anchor, anchorOffset, validators) = module == GameModule.Shell
            ? (GameFacts.ShellAnchorText, GameFacts.ShellAnchorOffset, GameFacts.ShellValidators)
            : (GameFacts.SimAnchorText, GameFacts.SimAnchorOffset, GameFacts.SimValidators);

        WriteAscii(dgroup + anchorOffset, anchor);
        if (!scrubValidators)
            foreach (var (text, offset) in validators) WriteAscii(dgroup + offset, text);

        if (!blankBss) PlantBss(module, dgroup);
    }

    private void PlantBss(GameModule module, int dgroup)
    {
        if (module == GameModule.Shell)
        {
            RealismSettings.Encode(RealismSettings.Expert).CopyTo(_ram, dgroup + GameFacts.ShellRealismOffset);
            WritePilot(dgroup + GameFacts.ActivePilotOffset, "Zeno Zwick");
            WritePilot(dgroup + GameFacts.RosterOffset, "Ernst Udet");
            WritePilot(dgroup + GameFacts.RosterOffset + 2 * GameFacts.PilotRecordSize, "Werner Voss");
        }
        else
        {
            _ram[dgroup + GameFacts.SimJoystickFlagOffset] = 1;
            _ram[dgroup + GameFacts.SimJoystickFlagMirrorOffset] = 1;
        }
    }

    private void Write16(int at, int value)
    {
        _ram[at] = (byte)(value & 0xFF);
        _ram[at + 1] = (byte)(value >> 8);
    }

    private void WriteAscii(int at, string text) => Encoding.ASCII.GetBytes(text).CopyTo(_ram, at);

    private void WritePilot(int at, string name) => Encoding.ASCII.GetBytes(name).CopyTo(_ram, at);

    public IEnumerable<MemoryRegion> EnumerateRegions()
    {
        yield return new MemoryRegion(HostBase - GuestPad, (nuint)_ram.Length);
    }

    public int Read(nuint address, byte[] buffer, int count)
    {
        long offset = (long)address - ((long)HostBase - GuestPad);
        if (offset < 0 || offset >= _ram.Length) return 0;
        int n = (int)Math.Min(count, _ram.Length - offset);
        // ReadProcessMemory is all-or-nothing: one unreadable page anywhere in the range fails the
        // whole call. Model that, because a fixture that silently truncates instead would let a
        // locator bug through.
        if (_unreadablePage >= 0 && offset < _unreadablePage + 0x1000 && offset + n > _unreadablePage)
            return 0;
        Array.Copy(_ram, offset, buffer, 0, n);
        return n;
    }

    public byte[] Read(nuint address, int count)
    {
        var buf = new byte[count];
        int n = Read(address, buf, count);
        if (n != count) Array.Resize(ref buf, n);
        return buf;
    }

    public bool Write(nuint address, byte[] buffer)
    {
        long offset = (long)address - ((long)HostBase - GuestPad);
        if (offset < 0 || offset + buffer.Length > _ram.Length) return false;
        buffer.CopyTo(_ram, offset);
        return true;
    }
}
