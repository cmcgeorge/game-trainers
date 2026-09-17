using System.Text;
using Space1889Trainer.Game;
using Space1889Trainer.Memory;

namespace Space1889Trainer.FormatCheck;

internal static partial class Program
{
    private static void CheckLocator()
    {
        Section("locator over a synthetic DOSBox guest");
        var mexe = GameFacts.Modules[0];
        var sexe = GameFacts.Modules[1];
        var cgexe = GameFacts.Modules[2];
        nuint guestZero = (nuint)(FakeMemory.GuestRegionBase + FakeMemory.GuestPad);

        // 1. the ordinary case: block plus M.EXE's data group pointing at it
        var (mem, _) = FakeMemory.BuildGuest(Synthetic.Block(), module: mexe);
        var found = GameLocator.Find(mem, out string status);
        Check(found is not null, "found with M.EXE running: " + status);
        if (found is not null)
        {
            CheckEqual(guestZero + FakeMemory.BlockLinear, found.BlockHost, "block host address");
            CheckEqual(guestZero, found.GuestZero, "guest linear 0 pinned past DOSBox's pad");
            CheckEqual((long)FakeMemory.BlockLinear, found.GuestLinear, "guest linear address");
            CheckEqual("0208:0000", found.GuestAddress, "segment:offset as seen live");
            CheckEqual(PointerWitness.GroundGame, found.Witness, "M.EXE witness");
            CheckEqual(2, found.AnchorHits, "two anchor hits: the block and M.EXE's own copy of the name");
            CheckEqual(1, found.ValidatedCandidates, "only the block validates");
            Check(status.Contains("M.EXE"), "status names the witness");
        }

        // 2. S.EXE and CG.EXE as the running program
        var (memS, _) = FakeMemory.BuildGuest(Synthetic.Block(), module: sexe);
        CheckEqual(PointerWitness.SpaceGame, GameLocator.Find(memS, out _)?.Witness, "S.EXE witness");
        var (memC, _) = FakeMemory.BuildGuest(Synthetic.Block(), module: cgexe);
        CheckEqual(PointerWitness.CharacterGenerator, GameLocator.Find(memC, out _)?.Witness, "CG.EXE witness");

        // 3. between programs: no data group, still found, no witness
        var (memNone, _) = FakeMemory.BuildGuest(Synthetic.Block());
        var between = GameLocator.Find(memNone, out string betweenStatus);
        Check(between is not null && between.Witness == PointerWitness.None, "found with no program loaded");
        Check(betweenStatus.Contains("no running Space 1889 program"), "status says nothing vouched for it");

        // 4. a pointer to somewhere else is not a witness
        var (memWrong, _) = FakeMemory.BuildGuest(Synthetic.Block(), module: mexe, pointerTarget: 0x3000);
        CheckEqual(PointerWitness.None, GameLocator.Find(memWrong, out _)?.Witness, "a pointer elsewhere does not vouch");

        // 5. a stray copy of the block higher up loses to the witnessed one, even though... it is found first by address order
        var (memTwo, guestTwo) = FakeMemory.BuildGuest(Synthetic.Block(), blockLinear: 0x90000, module: mexe);
        var decoy = Synthetic.Block();
        Array.Copy(decoy, 0, guestTwo, FakeMemory.GuestPad + 0x40000, decoy.Length);
        var two = GameLocator.Find(memTwo, out _);
        CheckEqual(2, two?.ValidatedCandidates, "two valid blocks");
        CheckEqual((long)0x90000, two?.GuestLinear, "the witnessed block wins over a lower copy");

        // 5b. with no witness at all, a block inside emulated RAM beats a stray copy in a lower host allocation
        var (memStray, _) = FakeMemory.BuildGuest(Synthetic.Block());
        var stray = new byte[1 << 20];
        Array.Copy(Synthetic.Block(), 0, stray, 0x1000, StateFormat.BlockLength);
        memStray.AddRegion((nuint)(FakeMemory.GuestRegionBase - 0x40_0000), stray);
        var pinnedWins = GameLocator.Find(memStray, out _);
        CheckEqual(2, pinnedWins?.ValidatedCandidates, "the stray copy validates too");
        CheckEqual((long)FakeMemory.BlockLinear, pinnedWins?.GuestLinear, "an unwitnessed block in guest RAM beats a stray copy lower down");

        // 6. no block at all: only M.EXE's own string
        var (memEmpty, _) = FakeMemory.BuildGuest(null, module: mexe);
        Check(GameLocator.Find(memEmpty, out string emptyStatus) is null, "M.EXE's own copy of the name is not a block");
        Check(emptyStatus.Contains("none sits in a valid game-state block"), "status explains the rejected hit");

        // 7. nothing at all
        var (memNothing, _) = FakeMemory.BuildGuest(null);
        Check(GameLocator.Find(memNothing, out string nothing) is null && nothing.Contains("not found"), "empty guest");

        // 8. no BIOS data area: still found, address unknown
        var (memNoBios, _) = FakeMemory.BuildGuest(Synthetic.Block(), module: mexe, biosArea: false);
        var noBios = GameLocator.Find(memNoBios, out _);
        Check(noBios is not null && noBios.GuestLinear == -1 && noBios.Witness == PointerWitness.None, "found without a BIOS anchor, unwitnessed");

        // 9. an anchor straddling the 1 MiB sweep seam
        int seamLinear = (1 << 20) - StateFormat.PartyAccountNameOffset - 5 - FakeMemory.GuestPad;
        var (memSeam, _) = FakeMemory.BuildGuest(Synthetic.Block(), blockLinear: seamLinear);
        CheckEqual((long)seamLinear, GameLocator.Find(memSeam, out _)?.GuestLinear, "anchor across the chunk seam");

        // 10. an unreadable page before the block does not hide it
        var (memHole, _) = FakeMemory.BuildGuest(Synthetic.Block(), blockLinear: 0x80000, module: mexe);
        memHole.UnreadablePages.Add((nuint)FakeMemory.GuestRegionBase + 0x10000);
        CheckEqual((long)0x80000, GameLocator.Find(memHole, out _)?.GuestLinear, "found past an unreadable page");

        // 10b. a hole later in the same megabyte fails the whole chunk read; the page-by-page retry must still find an
        //      anchor in front of it (here straddling two pages) and the data group that vouches for it
        int straddle = 0x3000 - 6 - StateFormat.PartyAccountNameOffset - FakeMemory.GuestPad;
        var (memAhead, _) = FakeMemory.BuildGuest(Synthetic.Block(), blockLinear: straddle, module: mexe);
        memAhead.UnreadablePages.Add((nuint)FakeMemory.GuestRegionBase + 0x50000);
        var ahead = GameLocator.Find(memAhead, out _);
        CheckEqual((long)straddle, ahead?.GuestLinear, "found before an unreadable page in the same megabyte, across a page seam");
        CheckEqual(PointerWitness.GroundGame, ahead?.Witness, "and the data group in that megabyte still vouches for it");
        CheckEqual(2, ahead?.AnchorHits, "each anchor reported once by the page-by-page retry");

        // 11. corrupted block stops validating
        var (memCorrupt, guestCorrupt) = FakeMemory.BuildGuest(Synthetic.Block(), module: mexe);
        var ok = GameLocator.Find(memCorrupt, out _);
        Check(ok is not null && GameLocator.StillValid(memCorrupt, ok.BlockHost), "still valid");
        guestCorrupt[FakeMemory.GuestPad + FakeMemory.BlockLinear + StateFormat.PartyAccountNameOffset] = (byte)'Q';
        Check(ok is not null && !GameLocator.StillValid(memCorrupt, ok.BlockHost), "a quit-to-DOS overwrite fails StillValid");

        // 12. cancellation
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        bool cancelled = false;
        try { GameLocator.Find(mem, out _, cts.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "cancellation honoured");

        // identity strings really are unique to their program's data group
        Check(GameFacts.Modules.Select(m => m.IdentityText).Distinct().Count() == 3, "three distinct module identities");
        Check(Encoding.ASCII.GetByteCount(GameFacts.TurboCBanner) == 42, "Turbo C banner length");
    }
}
