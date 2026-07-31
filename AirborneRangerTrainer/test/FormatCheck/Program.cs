using System.IO;
using System.Text;
using AirborneRangerTrainer.Game;
using AirborneRangerTrainer.Memory;
using AirborneRangerTrainer.ViewModels;
using GameTrainers.Common.Memory;

namespace FormatCheck;

/// <summary>
/// Headless verification for the Airborne Ranger trainer. Exits 0 when every check passes and 1
/// otherwise, so <c>Run.ps1 -Test</c> can gate on it.
///
/// Nothing here needs the game running. The layout is asserted against a fixture built from the
/// values that were read out of a live session and matched against the screen; the locator is driven
/// over a synthetic address space; and the roster editor is exercised against a synthetic file
/// plus, when it is present, the real shipped <c>ROSTER.DAT</c>.
/// </summary>
public static class Program
{
    private static int _passed;
    private static readonly List<string> Failures = new();

    public static int Main()
    {
        Console.WriteLine("Airborne Ranger trainer — format checks");
        Console.WriteLine(new string('-', 60));

        MissionLayout();
        MissionArithmetic();
        MissionStateEditing();
        LocatorAnchors();
        LocatorOverSyntheticMemory();
        RosterLayout();
        RosterEditing();
        RosterRoundTrip();
        RosterSaving();
        RosterEditorViewModel();
        ReferenceTables();
        ViewModelBehaviour();
        PanelRendering();
        ShippedRoster();

        Console.WriteLine(new string('-', 60));
        if (Failures.Count == 0)
        {
            Console.WriteLine($"All {_passed} checks passed.");
            return 0;
        }

        Console.WriteLine($"{_passed} passed, {Failures.Count} FAILED:");
        foreach (var f in Failures) Console.WriteLine("  " + f);
        return 1;
    }

    // --- assertions ----------------------------------------------------------

    private static void Check(bool ok, string what)
    {
        if (ok) _passed++;
        else Failures.Add(what);
    }

    private static void Eq<T>(T actual, T expected, string what)
    {
        if (EqualityComparer<T>.Default.Equals(actual, expected)) _passed++;
        else Failures.Add($"{what}: expected {expected}, got {actual}");
    }

    private static void Group(string name) => Console.WriteLine($"[{name}]");

    // --- the confirmed layout ------------------------------------------------

    /// <summary>
    /// A mission-state window holding exactly what a live session held at the moment the game's own
    /// status panel read <c>CARBINE MAGS 04 / GRENADES 03 / LAW ROCKETS 01 / TIME BOMBS 01 /
    /// WOUNDS 00 / FIRST AID 01 / WEIGHT 22 / TIME 600</c>.
    /// </summary>
    private static byte[] LiveFixture()
    {
        var w = new byte[MissionFormat.WindowLength];
        void U8(int dgroup, int v) => w[dgroup - MissionFormat.WindowStart] = (byte)v;
        void U16(int dgroup, int v) => MissionFormat.WriteU16(w, dgroup - MissionFormat.WindowStart, (ushort)v);

        U8(MissionFormat.OffWounds, 0);
        U8(MissionFormat.OffRoundsInMagazine, 30);
        U8(MissionFormat.OffSpareMagazines, 3);
        U8(MissionFormat.OffGrenades, 3);
        U8(MissionFormat.OffLawRockets, 1);
        U8(MissionFormat.OffTimeBombs, 1);
        U8(MissionFormat.OffFirstAidKits, 1);
        U16(MissionFormat.OffCarriedWeight, 21);
        U16(MissionFormat.OffMagazineLoaded, 1);
        U8(MissionFormat.OffClockHundreds, 6);
        U8(MissionFormat.OffClockTens, 0);
        U8(MissionFormat.OffClockUnits, 0);
        U8(MissionFormat.OffSelectedWeapon, 0);
        U16(MissionFormat.OffMeritPoints, 0);
        U8(MissionFormat.OffSoldiersKilled, 0);
        U8(MissionFormat.OffTargetsDestroyed, 0);
        return w;
    }

    private static void MissionLayout()
    {
        Group("mission layout");

        // Pin every offset to the DGROUP address the game's own panel-fill routine names, so a
        // typo in the table is caught here rather than by writing to a stranger's memory.
        Eq(MissionFormat.OffWounds, 0xC892, "wounds offset");
        Eq(MissionFormat.OffRoundsInMagazine, 0xC894, "rounds-in-magazine offset");
        Eq(MissionFormat.OffSpareMagazines, 0xC895, "spare-magazines offset");
        Eq(MissionFormat.OffGrenades, 0xC896, "grenades offset");
        Eq(MissionFormat.OffLawRockets, 0xC897, "LAW-rockets offset");
        Eq(MissionFormat.OffTimeBombs, 0xC898, "time-bombs offset");
        Eq(MissionFormat.OffFirstAidKits, 0xC89A, "first-aid offset");
        Eq(MissionFormat.OffCarriedWeight, 0xCA42, "carried-weight offset");
        Eq(MissionFormat.OffMagazineLoaded, 0xE248, "magazine-loaded offset");
        Eq(MissionFormat.OffClockHundreds, 0xBE54, "clock hundreds offset");
        Eq(MissionFormat.OffClockTens, 0xBE55, "clock tens offset");
        Eq(MissionFormat.OffClockUnits, 0xBE56, "clock units offset");
        Eq(MissionFormat.OffSelectedWeapon, 0xC891, "selected-weapon offset");
        Eq(MissionFormat.OffMeritPoints, 0xA2D4, "merit-points offset");
        Eq(MissionFormat.OffSoldiersKilled, 0xA2D6, "soldiers-killed offset");
        Eq(MissionFormat.OffTargetsDestroyed, 0xA2D8, "targets-destroyed offset");
        Eq(MissionFormat.OffStatusPanel, 0xB910, "status-panel offset");

        // The window must cover every field it claims to. Asserting `WindowStart == OffMeritPoints`
        // would only restate its own definition, so check the property that actually matters: that
        // no field lies outside it.
        foreach (var (off, name) in new[]
                 {
                     (MissionFormat.OffWounds, "wounds"), (MissionFormat.OffRoundsInMagazine, "rounds"),
                     (MissionFormat.OffSpareMagazines, "magazines"), (MissionFormat.OffGrenades, "grenades"),
                     (MissionFormat.OffLawRockets, "rockets"), (MissionFormat.OffTimeBombs, "bombs"),
                     (MissionFormat.OffFirstAidKits, "kits"), (MissionFormat.OffCarriedWeight, "weight"),
                     (MissionFormat.OffClockHundreds, "clock"), (MissionFormat.OffSelectedWeapon, "weapon"),
                     (MissionFormat.OffSoldiersKilled, "soldiers"), (MissionFormat.OffTargetsDestroyed, "targets"),
                 })
            Check(off >= MissionFormat.WindowStart && off < MissionFormat.WindowStart + MissionFormat.WindowLength,
                  $"{name} lies inside the polled window");

        Check(MissionFormat.WindowStart + MissionFormat.WindowLength <= MissionFormat.DataSegmentSize,
              "the polled window fits inside the data segment");
        Check(MissionFormat.OffStatusPanel + MissionFormat.StatusPanelLength <= MissionFormat.DataSegmentSize,
              "the status panel fits inside the data segment");

        Eq(MissionFormat.FullMagazine, 30, "a full magazine is 30 rounds");
        Eq(MissionFormat.FatalWounds, 3, "three wounds is death");
        Eq(MissionFormat.MaxClock, 999, "the three-digit clock tops out at 999");
    }

    private static void MissionArithmetic()
    {
        Group("mission arithmetic");

        var state = new MissionState(LiveFixture());
        Eq(state.Wounds, 0, "fixture wounds");
        Eq(state.RoundsInMagazine, 30, "fixture rounds");
        Eq(state.SpareMagazines, 3, "fixture spare magazines");
        Eq(state.Grenades, 3, "fixture grenades");
        Eq(state.LawRockets, 1, "fixture LAW rockets");
        Eq(state.TimeBombs, 1, "fixture time bombs");
        Eq(state.FirstAidKits, 1, "fixture first-aid kits");
        Eq(state.Clock, 600, "fixture clock");
        Eq(state.SelectedWeapon, 0, "fixture weapon");
        Eq(state.SelectedWeaponName, "Carbine", "fixture weapon name");

        // The two derived readouts are what proved the layout in the first place.
        Eq(state.DisplayedMagazines, 4, "the panel shows 4 magazines for 3 spare + 1 loaded");
        Eq(state.CarriedWeight, 22, "the panel shows weight 22");

        // ...and the weight is exactly the sum of the supply-pod item prices.
        int fromPrices = 0;
        foreach (var e in GameFacts.Equipment)
        {
            int count = e.Name switch
            {
                "Carbine magazine" => 3,
                "Hand grenade" => 3,
                "First-aid kit" => 1,
                "Time bomb" => 1,
                "LAW rocket" => 1,
                _ => 0,
            };
            fromPrices += e.Weight * count;
        }
        Eq(fromPrices + 1, state.CarriedWeight, "weight reconstructed from the item price table");

        // The magazine rule, including the "no magazine loaded" edge.
        Eq(MissionFormat.DisplayedMagazines(0, -1), 0, "no spares and no magazine shows 0");
        Eq(MissionFormat.DisplayedMagazines(0, 0), 1, "no spares but an empty magazine still shows 1");
        Eq(MissionFormat.DisplayedMagazines(5, -1), 6, "spares alone still count the chambered one");

        // Clock composition round-trips across the whole range.
        for (int v = 0; v <= MissionFormat.MaxClock; v++)
        {
            var (h, t, u) = MissionFormat.SplitClock(v);
            if (MissionFormat.ComposeClock(h, t, u) != v)
            {
                Failures.Add($"clock round-trip failed at {v}");
                return;
            }
        }
        _passed++;
        var (ch, ct2, cu) = MissionFormat.SplitClock(5000);
        Eq(MissionFormat.ComposeClock(ch, ct2, cu), MissionFormat.MaxClock, "clock clamps above 999");
    }

    private static void MissionStateEditing()
    {
        Group("mission state editing");

        var writes = new List<(int Offset, int Length)>();
        var state = new MissionState(LiveFixture(), (off, len) => writes.Add((off, len)));

        state.Grenades = 9;
        Eq(writes.Count, 1, "setting grenades flushes once");
        Eq(writes[0], (MissionFormat.OffGrenades, 1), "grenades flush one byte at its own offset");

        writes.Clear();
        state.Grenades = 9;
        Eq(writes.Count, 0, "writing an unchanged value flushes nothing");

        writes.Clear();
        state.Clock = 987;
        Eq(writes.Count, 1, "setting the clock flushes once");
        Eq(writes[0], (MissionFormat.OffClockHundreds, 3), "the clock flushes all three digit bytes together");
        Eq(state.Clock, 987, "clock reads back");

        writes.Clear();
        state.MeritPoints = 1234;
        Eq(writes[0], (MissionFormat.OffMeritPoints, 2), "merit points flush two bytes");
        Eq(state.MeritPoints, 1234, "merit points read back");

        // Clamping.
        state.Grenades = 9999;
        Eq(state.Grenades, MissionFormat.SupplyCeiling, "supplies clamp to the byte ceiling");
        state.Grenades = -5;
        Eq(state.Grenades, 0, "supplies clamp at zero");
        state.RoundsInMagazine = 250;
        Eq(state.RoundsInMagazine, MissionFormat.FullMagazine, "rounds clamp to a full magazine");
        state.RoundsInMagazine = -1;
        Eq(state.RoundsInMagazine, -1, "rounds accept the negative 'no magazine' marker");
        state.Wounds = 99;
        Eq(state.Wounds, MissionFormat.FatalWounds + 1, "wounds clamp just past fatal");
        state.Clock = -20;
        Eq(state.Clock, 0, "clock clamps at zero");

        // Bulk actions.
        var bulk = new MissionState(LiveFixture());
        bulk.Wounds = 2;
        bulk.Heal();
        Eq(bulk.Wounds, 0, "Heal clears wounds");

        bulk.Resupply();
        Eq(bulk.SpareMagazines, MissionFormat.MaxSpareMagazines, "Resupply fills magazines");
        // The panel prints spare + 1 through a renderer that only produces two characters, so the
        // magazine ceiling has to be one lower than every other counter or the panel shows ':0'.
        Eq(bulk.DisplayedMagazines, MissionFormat.MaxSupply,
           "a resupplied magazine count still fits the panel's two digits");
        Eq(MissionFormat.MaxSpareMagazines + 1, MissionFormat.MaxSupply,
           "the spare-magazine ceiling is exactly one below the others");
        Eq(bulk.Grenades, MissionFormat.MaxSupply, "Resupply fills grenades");
        Eq(bulk.LawRockets, MissionFormat.MaxSupply, "Resupply fills rockets");
        Eq(bulk.TimeBombs, MissionFormat.MaxSupply, "Resupply fills bombs");
        Eq(bulk.FirstAidKits, MissionFormat.MaxSupply, "Resupply fills first-aid kits");
        Eq(bulk.RoundsInMagazine, MissionFormat.FullMagazine, "Resupply reloads the magazine");

        var all = new MissionState(LiveFixture());
        all.Wounds = 2;
        all.MaxEverything();
        Eq(all.Wounds, 0, "MaxEverything heals");
        Eq(all.Clock, MissionFormat.MaxClock, "MaxEverything refills the clock");
        Eq(all.Grenades, MissionFormat.MaxSupply, "MaxEverything resupplies");

        // A too-small window must be refused rather than silently indexed out of range.
        bool threw = false;
        try { _ = new MissionState(new byte[4]); }
        catch (ArgumentException) { threw = true; }
        Check(threw, "a short window is rejected");
    }

    // --- the locator ---------------------------------------------------------

    private static void LocatorAnchors()
    {
        Group("locator anchors");

        Eq(MissionFormat.PrimaryAnchor.DgroupOffset, 0xB923, "primary anchor offset");
        Eq(Encoding.ASCII.GetString(MissionFormat.PrimaryAnchor.Bytes), "CARBINE MAGS", "primary anchor text");
        Eq(MissionFormat.Validators.Length, 4, "four corroborating literals");
        Check(MissionFormat.MinValidators >= 2 && MissionFormat.MinValidators <= MissionFormat.Validators.Length,
              "MinValidators is a sane fraction of the validator set");

        foreach (var v in MissionFormat.Validators)
        {
            Check(v.Bytes.Length >= 6, $"validator '{v.Name}' is long enough to be distinctive");
            Check(v.DgroupOffset + v.Bytes.Length <= MissionFormat.DataSegmentSize,
                  $"validator '{v.Name}' lies inside the data segment");
        }

        // The anchors must not overlap each other, or one hit would satisfy two validators.
        var spans = new List<(int Start, int End, string Name)>
        {
            (MissionFormat.PrimaryAnchor.DgroupOffset,
             MissionFormat.PrimaryAnchor.DgroupOffset + MissionFormat.PrimaryAnchor.Bytes.Length,
             MissionFormat.PrimaryAnchor.Name),
        };
        foreach (var v in MissionFormat.Validators)
            spans.Add((v.DgroupOffset, v.DgroupOffset + v.Bytes.Length, v.Name));
        for (int i = 0; i < spans.Count; i++)
            for (int j = i + 1; j < spans.Count; j++)
                Check(spans[i].End <= spans[j].Start || spans[j].End <= spans[i].Start,
                      $"anchors '{spans[i].Name}' and '{spans[j].Name}' do not overlap");

        // LooksLikeMissionState must accept the real thing and an all-zero block, and reject junk.
        Check(MissionFormat.LooksLikeMissionState(LiveFixture()), "the live fixture is recognised");
        Check(MissionFormat.LooksLikeMissionState(new byte[MissionFormat.WindowLength]),
              "an all-zero window is accepted (a game that has not run a mission yet)");

        var bad = LiveFixture();
        bad[MissionFormat.OffClockTens - MissionFormat.WindowStart] = 10;
        Check(!MissionFormat.LooksLikeMissionState(bad), "a clock digit above 9 is rejected");

        bad = LiveFixture();
        bad[MissionFormat.OffWounds - MissionFormat.WindowStart] = 200;
        Check(!MissionFormat.LooksLikeMissionState(bad), "an impossible wound count is rejected");

        bad = LiveFixture();
        bad[MissionFormat.OffRoundsInMagazine - MissionFormat.WindowStart] = 60;
        Check(!MissionFormat.LooksLikeMissionState(bad), "an over-full magazine is rejected");

        bad = LiveFixture();
        bad[MissionFormat.OffSelectedWeapon - MissionFormat.WindowStart] = 99;
        Check(!MissionFormat.LooksLikeMissionState(bad), "an unknown weapon code is rejected");

        Check(!MissionFormat.LooksLikeMissionState(new byte[4]), "a short window is rejected");
        Check(!MissionFormat.LooksLikeMissionState(null!), "a null window is rejected");
    }

    /// <summary>A synthetic address space, so the locator can be driven with no game running.</summary>
    private sealed class FakeMemory : IMemorySource
    {
        private readonly List<(nuint Base, byte[] Data)> _regions = new();

        /// <summary>Pages that refuse to read, to exercise the salvage path.</summary>
        public HashSet<nuint> UnreadablePages { get; } = new();

        public void Add(nuint baseAddress, byte[] data) => _regions.Add((baseAddress, data));

        public IEnumerable<MemoryRegion> EnumerateRegions()
        {
            foreach (var (b, d) in _regions) yield return new MemoryRegion(b, (nuint)d.Length);
        }

        public byte[] Read(nuint address, int count)
        {
            var buf = new byte[count];
            return Read(address, buf, count) == count ? buf : Array.Empty<byte>();
        }

        public int Read(nuint address, byte[] buffer, int count)
        {
            foreach (var (b, d) in _regions)
            {
                if (address < b || address + (nuint)count > b + (nuint)d.Length) continue;
                for (nuint p = address & ~(nuint)0xFFF; p < address + (nuint)count; p += 0x1000)
                    if (UnreadablePages.Contains(p)) return 0;
                Array.Copy(d, (int)(address - b), buffer, 0, count);
                return count;
            }
            return 0;
        }
    }

    /// <summary>
    /// Builds a data segment with the anchors and a mission state in place.
    ///
    /// <para>The mission state goes down <b>first</b> and the anchors on top, because in the real
    /// data segment the anchor literals sit <i>inside</i> the polled window's address range — the
    /// window is one contiguous span from the lowest field to the highest, and the game's static
    /// text happens to live between them. Writing them the other way round would wipe the anchors
    /// and make this fixture test something the game never does.</para>
    /// </summary>
    private static byte[] SyntheticDgroup(int validators = 4, bool plausibleState = true)
    {
        var seg = new byte[MissionFormat.DataSegmentSize];

        var state = plausibleState ? LiveFixture() : new byte[MissionFormat.WindowLength];
        if (!plausibleState) state[MissionFormat.OffClockTens - MissionFormat.WindowStart] = 0x5A;
        Array.Copy(state, 0, seg, MissionFormat.WindowStart, state.Length);

        var a = MissionFormat.PrimaryAnchor;
        Array.Copy(a.Bytes, 0, seg, a.DgroupOffset, a.Bytes.Length);
        for (int i = 0; i < validators && i < MissionFormat.Validators.Length; i++)
        {
            var v = MissionFormat.Validators[i];
            Array.Copy(v.Bytes, 0, seg, v.DgroupOffset, v.Bytes.Length);
        }
        return seg;
    }

    private static void LocatorOverSyntheticMemory()
    {
        Group("locator over synthetic memory");

        // A plain hit, with padding on both sides so DGROUP is not at the region base.
        const int pad = 0x3000;
        var seg = SyntheticDgroup();
        var image = new byte[pad + seg.Length + pad];
        Array.Copy(seg, 0, image, pad, seg.Length);
        var mem = new FakeMemory();
        mem.Add(0x40000000, image);

        var found = GameLocator.Locate(mem);
        Check(found.Found, "the locator finds a synthetic data segment");
        Eq((ulong)found.DgroupAddress, 0x40000000UL + pad, "DGROUP address");
        Eq(found.ValidatorsMatched, 4, "all four validators matched");

        // Exactly MinValidators must be enough, and one fewer must not be.
        for (int n = 0; n <= MissionFormat.Validators.Length; n++)
        {
            var s = SyntheticDgroup(n);
            var img = new byte[pad + s.Length + pad];
            Array.Copy(s, 0, img, pad, s.Length);
            var m = new FakeMemory();
            m.Add(0x40000000, img);
            var r = GameLocator.Locate(m);
            Check(r.Found == (n >= MissionFormat.MinValidators),
                  $"{n} validator(s) {(n >= MissionFormat.MinValidators ? "accepted" : "rejected")}");
        }

        // An anchor with implausible state behind it must be rejected — but reported as "found the
        // game, wrong state" rather than "not found", because the two need opposite advice.
        var junkSeg = SyntheticDgroup(4, plausibleState: false);
        var junkImg = new byte[pad + junkSeg.Length + pad];
        Array.Copy(junkSeg, 0, junkImg, pad, junkSeg.Length);
        var junkMem = new FakeMemory();
        junkMem.Add(0x40000000, junkImg);
        var junkResult = GameLocator.Locate(junkMem);
        Check(!junkResult.Found, "an anchor with implausible state behind it is rejected");
        Check(junkResult.AnchorsMatchedButStateDidNot, "...and reported as anchors-matched-state-did-not");
        Eq((ulong)junkResult.RejectedAddress, 0x40000000UL + pad, "...with the address it was rejected at");
        Check(!GameLocator.Locate(new FakeMemory()).AnchorsMatchedButStateDidNot,
              "a genuine miss is not reported as a rejected state");

        // Two candidates that both clear MinValidators: the stronger must win regardless of which
        // the ascending-address sweep reaches first. A stale copy of the caption with a couple of
        // coincidental matches sitting below the live segment must not shadow it.
        {
            var weak = SyntheticDgroup(2);
            var strong = SyntheticDgroup(3);
            const int gap = 0x1000;
            var img = new byte[pad + weak.Length + gap + strong.Length + pad];
            Array.Copy(weak, 0, img, pad, weak.Length);
            Array.Copy(strong, 0, img, pad + weak.Length + gap, strong.Length);
            var m = new FakeMemory();
            m.Add(0x70000000, img);
            var r = GameLocator.Locate(m);
            Check(r.Found, "two candidates: one is found");
            Eq(r.ValidatorsMatched, 3, "the stronger candidate wins even though the weaker comes first");
            Eq((ulong)r.DgroupAddress, 0x70000000UL + pad + (ulong)weak.Length + gap,
               "the stronger candidate's address is the one returned");
        }

        // Nothing at all.
        var empty = new FakeMemory();
        empty.Add(0x40000000, new byte[0x20000]);
        Check(!GameLocator.Locate(empty).Found, "an empty region yields nothing");

        // The anchor cut in half by the 1 MiB chunk seam, at several split points.
        const int chunk = 1 << 20;
        for (int split = 1; split < MissionFormat.PrimaryAnchor.Bytes.Length; split++)
        {
            var s = SyntheticDgroup();
            int anchorInSeg = MissionFormat.PrimaryAnchor.DgroupOffset;
            int want = chunk - split;                     // put the anchor's start `split` bytes before the seam
            int lead = want - anchorInSeg;
            if (lead < 0) continue;
            var img = new byte[lead + s.Length + 0x1000];
            Array.Copy(s, 0, img, lead, s.Length);
            var m = new FakeMemory();
            m.Add(0x50000000, img);
            var r = GameLocator.Locate(m);
            Check(r.Found, $"anchor straddling the chunk seam (split {split}) is found");
        }

        // An unreadable page inside the scanned region must not lose the whole megabyte.
        var s2 = SyntheticDgroup();
        var img2 = new byte[0x10000 + s2.Length + 0x10000];
        Array.Copy(s2, 0, img2, 0x10000, s2.Length);
        var m2 = new FakeMemory();
        m2.Add(0x60000000, img2);
        m2.UnreadablePages.Add(0x60000000);              // the very first page of the region
        var r2 = GameLocator.Locate(m2);
        Check(r2.Found, "an unreadable page is salvaged past rather than losing the region");

        // Cancellation is honoured.
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        bool cancelled = false;
        try { GameLocator.Locate(mem, cts.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "cancellation is honoured");

        // An anchor near address zero must not underflow when DGROUP is computed.
        var low = new FakeMemory();
        var lowImage = new byte[0x2000];
        Array.Copy(MissionFormat.PrimaryAnchor.Bytes, 0, lowImage, 0x10, MissionFormat.PrimaryAnchor.Bytes.Length);
        low.Add(0x1000, lowImage);
        Check(!GameLocator.Locate(low).Found, "an anchor too close to address zero is skipped, not wrapped");

        // Reread and ReadStatusPanel against the good image.
        var buf = new byte[MissionFormat.WindowLength];
        Check(GameLocator.Reread(mem, found.DgroupAddress, buf), "Reread succeeds");
        Check(MissionFormat.LooksLikeMissionState(buf), "Reread returns the mission state");
        Check(!GameLocator.Reread(mem, found.DgroupAddress, new byte[4]), "Reread refuses a short buffer");
        Check(!GameLocator.Reread(mem, 0, buf), "Reread refuses a null DGROUP");
        Check(GameLocator.ReadStatusPanel(mem, found.DgroupAddress) is { Length: MissionFormat.StatusPanelLength },
              "the status panel reads back at full length");
        Check(GameLocator.ReadStatusPanel(mem, 0) == null, "ReadStatusPanel refuses a null DGROUP");
    }

    // --- the roster ----------------------------------------------------------

    /// <summary>Builds a valid, empty roster image the way the game's blank template describes it.</summary>
    private static byte[] SyntheticRoster()
    {
        var f = new byte[RosterFormat.FileLength];
        for (int slot = 0; slot < RosterFormat.RecordCount; slot++)
        {
            int b = RosterFormat.RecordOffset(slot);
            RosterFormat.WriteAscii(f, b + RosterFormat.OffLine1, RosterFormat.LineLength, null);
            RosterFormat.WriteAscii(f, b + RosterFormat.OffLine1 + RosterFormat.LineRankColumn, 3, "PFC");
            RosterFormat.WriteAscii(f, b + RosterFormat.OffLine1 + RosterFormat.LineScoreColumn,
                                    RosterFormat.ScoreDigits, "000000");
            f[b + RosterFormat.OffLine1 + RosterFormat.LineLength] = 0x0D;
            f[b + RosterFormat.OffLine1 + RosterFormat.LineLength + 1] = 0xFF;
            RosterFormat.WriteAscii(f, b + RosterFormat.OffLine2, RosterFormat.DecorationLineLength, null);
            f[b + RosterFormat.OffLine2 + RosterFormat.DecorationLineLength] = 0x0D;
            f[b + RosterFormat.OffLine2 + RosterFormat.DecorationLineLength + 1] = 0xFF;
            // The tail bytes the trainer never interprets — give them recognisable values.
            f[b + RosterFormat.OffTail + 3] = 0xAB;
            f[b + RosterFormat.OffTail + 4] = 0xCD;
            f[b + RosterFormat.OffTail + 6] = 1;
            f[b + RosterFormat.OffTail + 7] = 2;
            f[b + RosterFormat.OffTail + 8] = 3;
            f[b + RosterFormat.OffTail + 9] = 4;
        }
        return f;
    }

    private static void RosterLayout()
    {
        Group("roster layout");

        // Pin the geometry to the literals measured from the shipped file, not to the constants'
        // own definitions: `FileLength` and `OffTail` are *declared* as sums of the others, so
        // restating those sums would assert nothing.
        Eq(RosterFormat.FileLength, 495, "roster file length");
        Eq(RosterFormat.RecordLength, 81, "record length");
        Eq(RosterFormat.RecordCount, 6, "record count");
        Eq(RosterFormat.HeaderLength, 6, "header length");
        Eq(RosterFormat.TrailerLength, 3, "trailer length");
        Eq(RosterFormat.LineLength, 33, "the rank/name/score line is 33 characters");
        Eq(RosterFormat.DecorationLineLength, 34, "the ribbon line is 34 characters");
        Eq(RosterFormat.TailLength, 10, "the binary tail is 10 bytes");

        // The record's internal geometry has to add up exactly, or a write lands in a neighbour.
        Eq(RosterFormat.OffLine1, 0, "line 1 starts the record");
        Eq(RosterFormat.OffLine2, 35, "line 2 follows line 1 and its terminator");
        Eq(RosterFormat.OffTail, 71, "the tail follows line 2 and its terminator");
        Eq(RosterFormat.OffTail + RosterFormat.TailLength, RosterFormat.RecordLength, "the tail ends the record");

        // Line 1's columns must tile the line exactly, matching the game's own blank template
        // "    PFC                    000000".
        Eq(RosterFormat.LineRankColumn, 4, "the rank mnemonic starts at column 4");
        Eq(RosterFormat.LineNameColumn, 8, "the name starts at column 8");
        Eq(RosterFormat.NameLength, 19, "the name field is 19 characters");
        Eq(RosterFormat.LineScoreColumn, 27, "the score starts at column 27");
        Eq(RosterFormat.LineScoreColumn + RosterFormat.ScoreDigits, RosterFormat.LineLength, "the score ends the line");

        Eq(RosterFormat.RecordOffset(0), 6, "the first record follows the six-byte header");
        Eq(RosterFormat.RecordOffset(5), 411, "the last record starts at 411");
        Eq(RosterFormat.RecordOffset(5) + RosterFormat.RecordLength + RosterFormat.TrailerLength,
           RosterFormat.FileLength, "the last record plus the trailer ends the file");
        bool threw = false;
        try { RosterFormat.RecordOffset(6); } catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "an out-of-range slot is rejected");

        Check(RosterFormat.LooksLikeRoster(SyntheticRoster()), "the synthetic roster is recognised");
        Check(!RosterFormat.LooksLikeRoster(null), "null is not a roster");
        Check(!RosterFormat.LooksLikeRoster(new byte[100]), "a short file is not a roster");
        Check(!RosterFormat.LooksLikeRoster(new byte[RosterFormat.FileLength]),
              "a right-length file without line terminators is rejected");

        var broken = SyntheticRoster();
        broken[RosterFormat.RecordOffset(3) + RosterFormat.OffLine2 + RosterFormat.DecorationLineLength] = 0;
        Check(!RosterFormat.LooksLikeRoster(broken), "a missing terminator in any record is rejected");

        // Name sanitising. The leading-space cases matter: the reader trims both ends, so a
        // sanitiser that trimmed only the trailing end would store a name the editor could never
        // correct — typing it again without the space would compare equal and write nothing.
        Eq(RosterFormat.SanitiseName("Daniel"), "Daniel", "a plain name survives");
        Eq(RosterFormat.SanitiseName(" Bob"), "Bob", "a leading space is trimmed");
        Eq(RosterFormat.SanitiseName("  Bob  "), "Bob", "blanks are trimmed from both ends");
        Eq(RosterFormat.SanitiseName("  "), "", "an all-blank name collapses to empty");
        Eq(RosterFormat.SanitiseName(new string('X', 40)).Length, RosterFormat.NameLength, "a long name is truncated");
        Eq(RosterFormat.SanitiseName("AB"), "A B", "control characters become spaces");
        Eq(RosterFormat.SanitiseName(null), "", "a null name is empty");

        // The same guarantee end to end: what is written is what reads back, so an edit converges.
        var trimFile = RosterFile.TryParse(SyntheticRoster());
        if (trimFile != null)
        {
            trimFile.Records[0].Name = " Bob";
            Eq(trimFile.Records[0].Name, "Bob", "a leading space never reaches the stored name");
            Eq(RosterFormat.ReadAscii(trimFile.Bytes,
                   RosterFormat.RecordOffset(0) + RosterFormat.OffLine1 + RosterFormat.LineNameColumn, 3),
               "Bob", "the name starts in its own column, not one across");
        }
    }

    private static void RosterEditing()
    {
        Group("roster editing");

        var file = RosterFile.TryParse(SyntheticRoster());
        Check(file != null, "the synthetic roster parses");
        if (file == null) return;

        var r = file.Records[2];
        Check(!r.IsOccupied, "a blank slot reads as empty");

        r.Name = "T. van der Beek";
        Eq(r.Name, "T. van der Beek", "the name round-trips");
        Check(r.IsOccupied, "a named slot reads as occupied");

        r.RankIndex = 11;
        Eq(r.RankMnemonic, "COL", "setting the rank index rewrites the text mnemonic");
        Eq(r.RankName, "Colonel", "the rank name follows the index");
        r.RankIndex = 999;
        Eq(r.RankIndex, RankBook.Count - 1, "the rank index clamps");

        r.RankIndex = 11;
        r.Score = 581_350;
        Eq(r.Score, 581_350, "the score round-trips");
        Check(r.TextLine.EndsWith("581350", StringComparison.Ordinal), "the score is stored as six digits");
        r.Score = 5_000_000;
        Eq(r.Score, RosterFormat.MaxScore, "the score clamps to six digits");
        r.Score = -1;
        Eq(r.Score, 0, "a negative score clamps to zero");

        r.Score = 581_350;
        r.Decorations = DecorationBook.AllMask;
        Eq(r.Decorations, DecorationBook.AllMask, "the decoration mask round-trips");
        Eq(r.DecorationLine, "COM1 COM2 BSTR SSTR DSC CMH", "all six decorations render as the game does");

        r.HasCampaignRibbon = true;
        Check(r.HasCampaignRibbon, "the campaign ribbon sets");
        Eq(r.DecorationLine, "COM1 COM2 BSTR SSTR DSC CMH (CMPN)", "the full ribbon line matches the shipped format");

        r.Decorations = 0x01;
        Check(r.HasCampaignRibbon, "editing decorations preserves the campaign ribbon");
        // "COM1" then blanks in the other five mnemonics' columns, then the marker: the shape the
        // shipped roster's PSG record has. Built rather than typed so a mis-counted space in the
        // test cannot masquerade as a bug in the renderer.
        string expected = "COM1" + new string(' ', RosterFormat.DecorationLineLength - 4 - 6) + "(CMPN)";
        Eq(r.DecorationLine, expected, "a single decoration renders in its own column");

        r.SetDecoration(DecorationBook.All[2].Bit, true);
        Check(r.HasDecoration(DecorationBook.All[2].Bit), "SetDecoration sets one bit");
        r.SetDecoration(DecorationBook.All[2].Bit, false);
        Check(!r.HasDecoration(DecorationBook.All[2].Bit), "SetDecoration clears one bit");

        r.Decorations = 0xFF;
        Eq(r.Decorations, DecorationBook.AllMask, "unknown decoration bits are masked off");

        // Editing one record must not disturb its neighbours or the undecoded tail bytes.
        int b = RosterFormat.RecordOffset(2);
        Eq(file.Bytes[b + RosterFormat.OffTail + 3], (byte)0xAB, "undecoded tail byte 3 is untouched");
        Eq(file.Bytes[b + RosterFormat.OffTail + 4], (byte)0xCD, "undecoded tail byte 4 is untouched");
        var pristine = SyntheticRoster();
        for (int slot = 0; slot < RosterFormat.RecordCount; slot++)
        {
            if (slot == 2) continue;
            int o = RosterFormat.RecordOffset(slot);
            Check(file.Bytes.AsSpan(o, RosterFormat.RecordLength)
                            .SequenceEqual(pristine.AsSpan(o, RosterFormat.RecordLength)),
                  $"record {slot} is unchanged by editing record 2");
        }
        Check(RosterFormat.LooksLikeRoster(file.Bytes), "the edited file is still a valid roster");
    }

    private static void RosterRoundTrip()
    {
        Group("roster round-trip");

        var original = SyntheticRoster();
        var file = RosterFile.TryParse(original);
        Check(file != null, "parse for round-trip");
        if (file == null) return;

        Check(file.Bytes.AsSpan().SequenceEqual(original), "an unedited roster round-trips byte for byte");

        // TryParse must copy, not alias, or an edit would mutate the caller's array.
        file.Records[0].Name = "Mutated";
        Check(!file.Bytes.AsSpan().SequenceEqual(original), "editing changes the parsed copy");
        Check(original.AsSpan().SequenceEqual(SyntheticRoster()), "the caller's array is untouched");

        Check(RosterFile.TryParse(new byte[10]) == null, "a malformed image is refused");
        Check(RosterFile.TryParse(null) == null, "a null image is refused");
    }

    private static void RosterSaving()
    {
        Group("roster saving");

        // Save is the only path in this trainer that can destroy something irreplaceable, so it is
        // exercised for real against a temp directory rather than asserted in prose.
        string dir = Path.Combine(Path.GetTempPath(), "ARangerTrainer-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, RosterFormat.FileName);
            var original = SyntheticRoster();
            File.WriteAllBytes(path, original);

            var file = RosterFile.Load(path);
            Check(file != null, "a roster loads from disk");
            if (file == null) return;
            Eq(file.Path, path, "the loaded file remembers its path");

            file.Records[0].Name = "Editor One";
            file.Records[0].RankIndex = 4;
            string? backup = file.Save(path);

            Eq(backup, path + ".bak", "the first save reports the backup it created");
            Check(File.Exists(path + ".bak"), "the first save creates a .bak");
            Check(File.ReadAllBytes(path + ".bak").AsSpan().SequenceEqual(original),
                  "the .bak holds the file exactly as it was before the trainer touched it");
            Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(file.Bytes),
                  "the saved file is byte-identical to the edited image");

            // The backup is one-shot: a second save must not replace the pre-trainer copy with a
            // copy of the already-edited file, which would silently destroy the only way back.
            file.Records[1].Name = "Editor Two";
            string? second = file.Save(path);
            Eq(second, null, "a later save reports no new backup");
            Check(File.ReadAllBytes(path + ".bak").AsSpan().SequenceEqual(original),
                  "a later save leaves the original .bak untouched");
            Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(file.Bytes),
                  "a later save still writes the current image");

            // What was written must still be a roster, and must reload identically.
            var reloaded = RosterFile.Load(path);
            Check(reloaded != null, "the saved file reloads");
            if (reloaded != null)
            {
                Eq(reloaded.Records[0].Name, "Editor One", "slot 0 survived the round-trip to disk");
                Eq(reloaded.Records[0].RankMnemonic, "PSG", "slot 0's rank survived the round-trip to disk");
                Eq(reloaded.Records[1].Name, "Editor Two", "slot 1 survived the round-trip to disk");
                Check(reloaded.Bytes.AsSpan().SequenceEqual(file.Bytes), "the reload is byte-identical");
            }

            // Saving to a path that has no file yet needs no backup and must not invent one.
            string fresh = Path.Combine(dir, "FRESH.DAT");
            Eq(file.Save(fresh), null, "saving to a new path reports no backup");
            Check(!File.Exists(fresh + ".bak"), "saving to a new path creates no .bak");

            Check(RosterFile.Load(Path.Combine(dir, "MISSING.DAT")) == null, "a missing file loads as null");
            File.WriteAllBytes(Path.Combine(dir, "JUNK.DAT"), new byte[] { 1, 2, 3 });
            Check(RosterFile.Load(Path.Combine(dir, "JUNK.DAT")) == null, "a malformed file loads as null");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { /* leave the temp directory behind rather than fail the run */ }
        }
    }

    private static void RosterEditorViewModel()
    {
        Group("roster editor view-model");

        string dir = Path.Combine(Path.GetTempPath(), "ARangerTrainer-vm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, RosterFormat.FileName);
            File.WriteAllBytes(path, SyntheticRoster());

            var messages = new List<string>();
            var vm = new RosterViewModel(messages.Add);

            Check(!vm.HasFile, "no file is open to begin with");
            Check(!vm.SaveCommand.CanExecute(null), "Save is disabled with no file");
            Check(!vm.WouldDiscardEdits, "nothing to discard with no file");

            Check(!vm.Load(Path.Combine(dir, "NOPE.DAT")), "loading a missing file fails");
            Check(!vm.HasFile, "a failed load leaves no file open");
            Check(messages.Count == 1, "a failed load is reported");

            Check(vm.Load(path), "loading the synthetic roster succeeds");
            Eq(vm.Rangers.Count, RosterFormat.RecordCount, "every slot gets a view-model");
            Check(!vm.IsDirty, "a freshly loaded roster is not dirty");
            Check(!vm.SaveCommand.CanExecute(null), "Save is disabled until something changes");
            Check(!vm.WouldDiscardEdits, "a clean roster discards nothing");

            vm.Rangers[0].Name = "Bloggs";
            Check(vm.IsDirty, "editing a name marks the file dirty");
            Check(vm.SaveCommand.CanExecute(null), "Save is enabled once dirty");
            Check(vm.WouldDiscardEdits, "a dirty roster would discard edits");

            // Every editable surface must mark the file dirty, or an edit can be lost silently.
            foreach (var (label, edit) in new (string, Action)[]
                     {
                         ("rank", () => vm.Rangers[1].RankIndex = 3),
                         ("score", () => vm.Rangers[1].Score = 1234),
                         ("decoration", () => vm.Rangers[1].Decorations[0].IsSet = true),
                         ("campaign ribbon", () => vm.Rangers[1].HasCampaignRibbon = true),
                     })
            {
                vm.RevertCommand.Execute(null);
                Check(!vm.IsDirty, $"Revert clears the dirty flag before the {label} check");
                edit();
                Check(vm.IsDirty, $"editing the {label} marks the file dirty");
            }

            vm.RevertCommand.Execute(null);
            Check(!vm.IsDirty, "Revert clears the dirty flag");
            Eq(vm.Rangers[0].Name, "", "Revert throws the edits away");
            Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(SyntheticRoster()),
                  "Revert never wrote anything to disk");

            vm.Rangers[2].Name = "Saved";
            vm.SaveCommand.Execute(null);
            Check(!vm.IsDirty, "Save clears the dirty flag");
            Check(File.Exists(path + ".bak"), "Save takes the backup");
            var onDisk = RosterFile.Load(path);
            Eq(onDisk?.Records[2].Name, "Saved", "Save reached the file");

            // Clamping through the view-model, which is where a text box's input lands.
            vm.Rangers[3].Score = 9_999_999;
            Eq(vm.Rangers[3].Score, RosterFormat.MaxScore, "the view-model clamps an over-large score");
            vm.Rangers[3].RankIndex = 500;
            Eq(vm.Rangers[3].RankIndex, RankBook.Count - 1, "the view-model clamps an out-of-range rank");
            vm.Rangers[3].Name = "   ";
            Check(!vm.Rangers[3].IsOccupied, "a blank name empties the slot");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { /* leave the temp directory behind rather than fail the run */ }
        }
    }

    private static void ShippedRoster()
    {
        Group("shipped roster");

        string? path = FindShippedRoster();
        if (path == null)
        {
            Console.WriteLine("  skipped — no copyrighted ROSTER.DAT found (put one in .game\\ to run this group)");
            return;
        }

        var bytes = File.ReadAllBytes(path);
        Check(RosterFormat.LooksLikeRoster(bytes), $"{path} is a valid roster");
        var file = RosterFile.TryParse(bytes, path);
        if (file == null) return;

        Check(file.Bytes.AsSpan().SequenceEqual(bytes), "the shipped roster round-trips byte for byte");

        foreach (var r in file.Records)
        {
            if (!r.IsOccupied) continue;
            // The tail's rank index and the text mnemonic are two views of the same fact, and the
            // decoration mask and the ribbon line are another. If the decode is right they agree.
            Check(r.TextLine.Contains(r.RankMnemonic, StringComparison.Ordinal),
                  $"slot {r.Slot}: the tail rank index matches the printed mnemonic");
            Eq(r.DecorationLine, DecorationBook.RenderLine(r.Decorations, r.HasCampaignRibbon).TrimEnd(),
               $"slot {r.Slot}: the decoration mask reproduces the stored ribbon line");
            Console.WriteLine($"  {r.RankMnemonic} {r.Name} — {r.Score:N0}" +
                              (r.DecorationLine.Length > 0 ? $"  [{r.DecorationLine}]" : ""));
        }
    }

    private static string? FindShippedRoster()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, ".game", RosterFormat.FileName);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    // --- reference tables ----------------------------------------------------

    private static void ReferenceTables()
    {
        Group("reference tables");

        Eq(MissionBook.Count, 12, "twelve missions");
        Eq(MissionBook.ChallengeTable, "2111332222333", "the challenge table is the game's own string");
        Eq(MissionBook.ChallengeTable.Length, MissionBook.Count + 1, "one digit per mission plus the campaign");
        Eq(MissionBook.CampaignChallengeLevel, 3, "the campaign's challenge level");

        for (int i = 0; i < MissionBook.All.Count; i++)
        {
            var m = MissionBook.All[i];
            Eq(m.Number, i + 1, $"mission {i + 1} is numbered in list order");
            Eq(m.ChallengeLevel, MissionBook.ChallengeTable[i] - '0',
               $"mission {m.Number} challenge level matches the game's table");
            Eq(m.Terrain, (Terrain)(i % 3), $"mission {m.Number} terrain follows the Desert/Temperate/Arctic cycle");
            Check(m.Briefing.Length > 40, $"mission {m.Number} has a briefing");
            Check(m.Tip.Length > 20, $"mission {m.Number} has a tip");
        }
        Check(MissionBook.ByNumber(1)?.Name == "Destroy a Munitions Depot", "ByNumber finds the first mission");
        Check(MissionBook.ByNumber(0) == null && MissionBook.ByNumber(13) == null, "ByNumber rejects out-of-range");

        Eq(RankBook.Count, 15, "fifteen rank slots");
        Eq(RankBook.Mnemonic(0), "PFC", "rank 0 is PFC");
        Eq(RankBook.Mnemonic(11), "COL", "rank 11 is COL");
        Eq(RankBook.Mnemonic(13), "KIA", "rank 13 is KIA");
        Eq(RankBook.Mnemonic(14), "POW", "rank 14 is POW");
        Eq(RankBook.Mnemonic(99), "   ", "an out-of-range rank is blank");
        Eq(RankBook.HighestPromotion, 11, "COL is the highest promotion");
        for (int i = 0; i < RankBook.Count; i++)
        {
            Eq(RankBook.All[i].Index, i, $"rank {i} is stored at its own index");
            Eq(RankBook.All[i].Mnemonic.Length, 3, $"rank {i} mnemonic is three characters");
        }

        Eq(DecorationBook.All.Count, 6, "six decorations");
        int mask = 0;
        foreach (var d in DecorationBook.All) mask |= d.Bit;
        Eq(mask, DecorationBook.AllMask, "the decoration bits cover AllMask exactly");
        for (int i = 0; i < DecorationBook.All.Count; i++)
            Eq(DecorationBook.All[i].Bit, 1 << i, $"decoration {i} occupies bit {i}");
        Eq(DecorationBook.RenderLine(0, false).Length, RosterFormat.DecorationLineLength,
           "an empty ribbon line is still full width");
        Eq(DecorationBook.RenderLine(DecorationBook.AllMask, true).Length, RosterFormat.DecorationLineLength,
           "a full ribbon line is exactly the field width");

        Eq(GameFacts.StandardLoadWeight, GameFacts.SupplyPodCapacity,
           "the STANDARD pod loadout fills the pod exactly");
        Eq(GameFacts.Equipment.Count, 5, "five supply-pod items");
        Eq(GameFacts.ProtectionRibbons.Count, 23, "23 copy-protection ribbons");
        Check(GameFacts.Controls.Count >= 8, "the control list is complete");
        Check(GameFacts.Tips.Count >= 8, "there are tips");
        Eq(GameFacts.Version, "441.01", "the build version string");

        Eq(WeaponBook.All.Count, 5, "five weapon codes");
        Eq(WeaponBook.MaxCode, 4, "the highest weapon code is 4");
        Eq(WeaponBook.Name(0), "Carbine", "weapon 0 is the carbine");
        Eq(WeaponBook.Name(4), "Knife", "weapon 4 is the knife");
        Check(WeaponBook.Name(9).Contains('9'), "an unknown weapon code is reported, not hidden");

        var reference = new ReferenceViewModel();
        Eq(reference.Missions.Count, 12, "the reference view-model exposes the missions");
        Check(reference.MapSchematic.Contains("Pickup Point", StringComparison.Ordinal), "the map schematic is present");
        Check(reference.SupplyPodSummary.Contains(GameFacts.SupplyPodCapacity.ToString()), "the pod summary quotes the capacity");
    }

    // --- the view-model ------------------------------------------------------

    private sealed class FakeHost : IMissionHost
    {
        public List<(int Offset, byte[] Bytes)> Writes { get; } = new();
        public List<string> Messages { get; } = new();
        public bool FailWrites { get; set; }

        public bool WriteBytes(int dgroupOffset, byte[] bytes)
        {
            if (FailWrites) return false;
            Writes.Add((dgroupOffset, bytes));
            return true;
        }

        public void ReportStatus(string message) => Messages.Add(message);
    }

    private static (MissionViewModel Vm, FakeHost Host) NewViewModel()
    {
        var host = new FakeHost();
        var located = new LocateResult((nuint)0x1000, LiveFixture(), "test", 4);
        var vm = new MissionViewModel(host, located);
        Array.Copy(LiveFixture(), vm.LiveBuffer, MissionFormat.WindowLength);
        vm.OnPolled();
        host.Writes.Clear();
        return (vm, host);
    }

    private static void ViewModelBehaviour()
    {
        Group("view-model behaviour");

        var (vm, host) = NewViewModel();
        Eq(vm.Wounds, 0, "view-model reads the fixture");
        Eq(vm.DisplayedMagazines, 4, "view-model derives the panel magazine count");

        vm.Grenades = 12;
        Eq(host.Writes.Count, 1, "editing a field writes once");
        Eq(host.Writes[0].Offset, MissionFormat.OffGrenades, "the write lands at the field's own offset");
        Eq(host.Writes[0].Bytes.Length, 1, "the write is one byte");

        // Re-setting the same value is a no-op only when the *game* also holds it — the shadow
        // buffer alone is not evidence, because the game moves these counters constantly.
        host.Writes.Clear();
        vm.LiveBuffer[MissionFormat.OffGrenades - MissionFormat.WindowStart] = 12;
        vm.OnPolled();
        host.Writes.Clear();
        vm.Grenades = 12;
        Eq(host.Writes.Count, 0, "re-setting a value the game already holds writes nothing");

        host.Writes.Clear();
        vm.Grenades = 9999;
        Eq(vm.Grenades, MissionFormat.SupplyCeiling, "an out-of-range edit clamps");
        Eq(host.Writes.Count, 1, "the clamped value is still written");

        // A failed write must be reported rather than silently swallowed.
        host.Writes.Clear();
        host.Messages.Clear();
        host.FailWrites = true;
        vm.Grenades = 7;
        Check(host.Messages.Any(m => m.Contains("failed", StringComparison.OrdinalIgnoreCase)),
              "a failed write is reported");
        host.FailWrites = false;

        // Freezes.
        var (fz, fzHost) = NewViewModel();
        fz.FreezeWounds = true;                              // pins at the live value, 0
        fzHost.Writes.Clear();
        fz.LiveBuffer[MissionFormat.OffWounds - MissionFormat.WindowStart] = 2;   // the game wounds us
        fz.OnPolled();
        Eq(fzHost.Writes.Count, 1, "a frozen wound counter is re-pinned on the next tick");
        Eq(fzHost.Writes[0].Offset, MissionFormat.OffWounds, "the re-pin writes the wound counter");
        Eq(fzHost.Writes[0].Bytes[0], (byte)0, "the re-pin restores the pinned value");

        fzHost.Writes.Clear();
        fz.OnPolled();
        Eq(fzHost.Writes.Count, 1, "the re-pin repeats while the game keeps changing it");

        // A freeze armed after the game has moved on pins what the game has now.
        var (late, lateHost) = NewViewModel();
        late.LiveBuffer[MissionFormat.OffWounds - MissionFormat.WindowStart] = 1;
        late.OnPolled();
        lateHost.Writes.Clear();
        late.FreezeWounds = true;
        late.OnPolled();
        Eq(lateHost.Writes.Count, 0, "a freeze armed at the live value writes nothing");

        // Nothing frozen means nothing written on a tick.
        var (idle, idleHost) = NewViewModel();
        idle.LiveBuffer[MissionFormat.OffGrenades - MissionFormat.WindowStart] = 1;
        idle.OnPolled();
        Eq(idleHost.Writes.Count, 0, "an unfrozen field is not written on a poll tick");

        // Editing a frozen field re-pins it, so the edit sticks.
        var (edit, editHost) = NewViewModel();
        edit.FreezeAmmo = true;
        edit.Grenades = 50;
        editHost.Writes.Clear();
        edit.OnPolled();                                     // live still says 3
        Check(editHost.Writes.Any(w => w.Offset == MissionFormat.OffGrenades && w.Bytes[0] == 50),
              "an edit to a frozen field is re-pinned to the edited value");

        // Bulk actions re-pin too, so a frozen field does not undo them.
        var (bulk, bulkHost) = NewViewModel();
        bulk.FreezeAmmo = true;
        bulk.ResupplyCommand.Execute(null);
        bulkHost.Writes.Clear();
        bulk.OnPolled();
        Check(bulkHost.Writes.Any(w => w.Offset == MissionFormat.OffGrenades &&
                                       w.Bytes[0] == MissionFormat.MaxSupply),
              "Resupply re-pins the ammo freeze to the new values");

        // The clock writes three bytes as one range.
        var (clk, clkHost) = NewViewModel();
        clkHost.Writes.Clear();
        clk.Clock = 123;
        Eq(clkHost.Writes.Count, 1, "the clock writes once");
        Eq(clkHost.Writes[0].Offset, MissionFormat.OffClockHundreds, "the clock write starts at the hundreds digit");
        Eq(clkHost.Writes[0].Bytes.Length, 3, "the clock write covers all three digits");
        Eq(clkHost.Writes[0].Bytes[0], (byte)1, "hundreds digit");
        Eq(clkHost.Writes[0].Bytes[1], (byte)2, "tens digit");
        Eq(clkHost.Writes[0].Bytes[2], (byte)3, "units digit");

        // ReloadFromGame is refused before the first poll and works after it.
        var fresh = new MissionViewModel(new FakeHost(), new LocateResult((nuint)0x1000, LiveFixture(), "test", 4));
        Check(!fresh.ReloadFromGame(), "reload is refused before the first poll");
        fresh.OnPolled();
        Check(fresh.ReloadFromGame(), "reload works after a poll");

        // The live mirror must be usable straight out of the constructor, before any poll. This is
        // asserted directly rather than through a freeze, because the provisional-pin machinery
        // would otherwise mask an unseeded buffer.
        var seeded = new MissionViewModel(new FakeHost(), new LocateResult((nuint)0x1000, LiveFixture(), "test", 4));
        Check(seeded.MissionIsRunning, "the located window is visible to the live mirror before the first poll");
        Check(seeded.LiveAmmo.Contains("Grenades 3", StringComparison.Ordinal),
              "the live summary reads the located values before the first poll");
        Check(seeded.LiveProgress.Contains("Time 600", StringComparison.Ordinal),
              "the live clock reads the located value before the first poll");

        // A freeze armed while no mission is running must re-pin from the mission that actually
        // starts, not restore the between-missions zeros over it.
        var idleHost2 = new FakeHost();
        var idleWindow = LiveFixture();
        idleWindow[MissionFormat.OffClockHundreds - MissionFormat.WindowStart] = 0;
        idleWindow[MissionFormat.OffGrenades - MissionFormat.WindowStart] = 0;
        idleWindow[MissionFormat.OffSpareMagazines - MissionFormat.WindowStart] = 0;
        var idleVm = new MissionViewModel(idleHost2, new LocateResult((nuint)0x1000, idleWindow, "test", 4));
        Check(!idleVm.MissionIsRunning, "a zero clock reads as no mission running");
        idleVm.FreezeAmmo = true;
        idleVm.FreezeClock = true;
        Array.Copy(idleWindow, idleVm.LiveBuffer, MissionFormat.WindowLength);
        idleVm.OnPolled();
        Eq(idleHost2.Writes.Count, 0, "a freeze never fires while no mission is running");

        Array.Copy(LiveFixture(), idleVm.LiveBuffer, MissionFormat.WindowLength);   // a mission starts
        idleHost2.Writes.Clear();
        idleVm.OnPolled();
        Eq(idleHost2.Writes.Count, 0, "the first tick of a new mission re-pins instead of restoring zeros");
        Check(idleVm.MissionIsRunning, "the new mission reads as running");

        idleVm.LiveBuffer[MissionFormat.OffGrenades - MissionFormat.WindowStart] = 1;   // the game spends one
        idleVm.OnPolled();
        Check(idleHost2.Writes.Any(w => w.Offset == MissionFormat.OffGrenades && w.Bytes[0] == 3),
              "once re-pinned, the freeze holds the new mission's values");

        // A freeze must hold values for ONE mission. A pin taken mid-mission must not survive the
        // mission boundary and be forced onto the next one — that would clamp a fresh 600-second
        // clock back to wherever the last mission ended and restore a dead ranger's spent loadout.
        var (cross, crossHost) = NewViewModel();
        cross.LiveBuffer[MissionFormat.OffClockHundreds - MissionFormat.WindowStart] = 4;
        cross.LiveBuffer[MissionFormat.OffClockTens - MissionFormat.WindowStart] = 5;
        cross.LiveBuffer[MissionFormat.OffClockUnits - MissionFormat.WindowStart] = 0;
        cross.LiveBuffer[MissionFormat.OffGrenades - MissionFormat.WindowStart] = 0;
        cross.OnPolled();                                   // mid-mission: clock 450, no grenades left
        cross.FreezeClock = true;
        cross.FreezeAmmo = true;
        crossHost.Writes.Clear();

        cross.LiveBuffer[MissionFormat.OffGrenades - MissionFormat.WindowStart] = 1;
        cross.OnPolled();
        Check(crossHost.Writes.Any(w => w.Offset == MissionFormat.OffGrenades && w.Bytes[0] == 0),
              "a freeze armed mid-mission holds that mission's values");

        // The mission ends...
        cross.LiveBuffer[MissionFormat.OffClockHundreds - MissionFormat.WindowStart] = 0;
        cross.LiveBuffer[MissionFormat.OffClockTens - MissionFormat.WindowStart] = 0;
        cross.LiveBuffer[MissionFormat.OffClockUnits - MissionFormat.WindowStart] = 0;
        crossHost.Writes.Clear();
        cross.OnPolled();
        Eq(crossHost.Writes.Count, 0, "a mid-mission freeze stops writing once the mission is over");

        // ...and the next one starts fresh: 600 on the clock, a full loadout.
        Array.Copy(LiveFixture(), cross.LiveBuffer, MissionFormat.WindowLength);
        crossHost.Writes.Clear();
        cross.OnPolled();
        Eq(crossHost.Writes.Count, 0, "the previous mission's pin is not forced onto the next mission");

        cross.LiveBuffer[MissionFormat.OffGrenades - MissionFormat.WindowStart] = 2;
        cross.OnPolled();
        Check(crossHost.Writes.Any(w => w.Offset == MissionFormat.OffGrenades && w.Bytes[0] == 3),
              "the freeze re-pinned to the new mission's loadout, not the last one's");

        // An edit must reach the game even when the trainer's own shadow already holds that value
        // and the game has since moved on — the counters this trainer edits change constantly.
        var (stale, staleHost) = NewViewModel();
        stale.Grenades = 9;
        stale.LiveBuffer[MissionFormat.OffGrenades - MissionFormat.WindowStart] = 2;   // the game spends them
        stale.OnPolled();
        staleHost.Writes.Clear();
        stale.Grenades = 9;
        Check(staleHost.Writes.Any(w => w.Offset == MissionFormat.OffGrenades && w.Bytes[0] == 9),
              "re-applying a value the game has drifted away from still writes");

        staleHost.Writes.Clear();
        stale.LiveBuffer[MissionFormat.OffGrenades - MissionFormat.WindowStart] = 9;   // the game agrees
        stale.OnPolled();
        staleHost.Writes.Clear();
        stale.Grenades = 9;
        Eq(staleHost.Writes.Count, 0, "setting a field to what the game already holds writes nothing");

        // Live summaries mention the values a user would check against the screen.
        Check(vm.LiveCondition.Contains("Wounds"), "the live condition line is rendered");
        Check(vm.LiveAmmo.Contains("Grenades"), "the live ammo line is rendered");
        Check(vm.LiveProgress.Contains("Merit"), "the live progress line is rendered");

        // A locate result with no state must be refused.
        bool threw = false;
        try { _ = new MissionViewModel(new FakeHost(), LocateResult.None); }
        catch (ArgumentException) { threw = true; }
        Check(threw, "a view-model cannot be built from a failed locate");
    }

    private static void PanelRendering()
    {
        Group("status-panel rendering");

        // A slice of the real template, with the digits the game had written into it.
        var panel = new List<byte>();
        panel.AddRange(new byte[] { 0x01, 0x09, 0x1A, 0x1B, 0x1C, 0x20 });
        panel.AddRange(Encoding.ASCII.GetBytes("CARBINE MAGS"));
        panel.Add(0x1F);
        panel.AddRange(Encoding.ASCII.GetBytes("04 "));
        panel.Add(0x0D);
        panel.AddRange(Encoding.ASCII.GetBytes("TIME"));
        panel.Add(0x1F);
        panel.AddRange(Encoding.ASCII.GetBytes("600"));
        panel.Add(0xFF);
        panel.AddRange(Encoding.ASCII.GetBytes("NOT PART OF THE MESSAGE"));

        string text = MissionViewModel.RenderPanel(panel.ToArray());
        Check(text.Contains("CARBINE MAGS", StringComparison.Ordinal), "the caption survives rendering");
        Check(text.Contains("04", StringComparison.Ordinal), "the filled-in digits survive rendering");
        Check(text.Contains('\n'), "the newline control byte becomes a line break");
        Check(!text.Contains("NOT PART", StringComparison.Ordinal), "rendering stops at the end-of-message byte");
        Eq(MissionViewModel.RenderPanel(null), "", "a null panel renders as empty");
        Eq(MissionViewModel.RenderPanel(Array.Empty<byte>()), "", "an empty panel renders as empty");
    }
}
