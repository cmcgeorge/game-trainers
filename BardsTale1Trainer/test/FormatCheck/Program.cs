using BardsTale1Trainer.Game;

// Headless verification of the Bard's Tale 1 record format and the extracted game
// tables. Runs against the two bundled .TPW sample characters in docs\, and — if the
// large DOSBox-X memory dump is present in testdata\ — against the live party array
// and the data-segment string anchors too. Needs neither admin nor the game running.

int failures = 0;
void Check(string label, bool ok, string? detail = null)
{
    Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Red;
    Console.Write(ok ? "PASS " : "FAIL ");
    Console.ResetColor();
    Console.Write(label);
    if (detail != null) Console.Write($"  — {detail}");
    Console.WriteLine();
    if (!ok) failures++;
}

string repoRoot = FindRepoRoot();
string docs = Path.Combine(repoRoot, "docs");
string testdata = Path.Combine(repoRoot, "testdata");

Console.WriteLine("=== Static tables ===");
Check("Item table has 126 entries", ItemBook.ItemNames.Length == 126,
    $"{ItemBook.ItemNames.Length}");
Check("Item id 5 is Dagger", ItemBook.ItemName(5) == "Dagger", ItemBook.ItemName(5));
Check("Item id 16 is Robes", ItemBook.ItemName(16) == "Robes", ItemBook.ItemName(16));
Check("Item id 0 is empty", ItemBook.ItemName(0) == "(empty)");
Check("Spellbook has 79 spells", Spellbook.All.Count == 79, $"{Spellbook.All.Count}");
Check("Spell code MAFL is MAGE FLAME (Conjurer L1)",
    Spellbook.All.Any(s => s.Code == "MAFL" && s.Name == "MAGE FLAME"
        && s.Class == SpellClass.Conjurer && s.Level == 1));
Check("Spell code GRSU is GREATER SUMMON (Wizard L7)",
    Spellbook.All.Any(s => s.Code == "GRSU" && s.Class == SpellClass.Wizard && s.Level == 7));
Check("Conjurer class id 6 maps to Conjurer art", Spellbook.ArtForClass(6) == SpellClass.Conjurer);
Check("Warrior class id 0 is a non-caster", Spellbook.ArtForClass(0) == SpellClass.None);
// Spell-mastery bytes at 0x41 are ordered Magician, Conjurer, Sorcerer, Wizard.
Check("SpellLevelIndexForClass maps casters to their mastery byte (Mag0/Con1/Sor2/Wiz3)",
    PartyFormat.SpellLevelIndexForClass(7) == 0 && PartyFormat.SpellLevelIndexForClass(6) == 1
    && PartyFormat.SpellLevelIndexForClass(8) == 2 && PartyFormat.SpellLevelIndexForClass(9) == 3,
    $"{PartyFormat.SpellLevelIndexForClass(7)},{PartyFormat.SpellLevelIndexForClass(6)}," +
    $"{PartyFormat.SpellLevelIndexForClass(8)},{PartyFormat.SpellLevelIndexForClass(9)}");
Check("SpellLevelIndexForClass returns -1 for non-casters",
    PartyFormat.SpellLevelIndexForClass(0) == -1 && PartyFormat.SpellLevelIndexForClass(3) == -1);
Check("10 classes, 7 races", PartyFormat.Classes.Length == 10 && PartyFormat.Races.Length == 7);

Console.WriteLine("\n=== .TPW sample characters ===");
CheckTpw("CHRISTOPHER", Path.Combine(docs, "CHRISTOPHER-64.TPW"), rec =>
{
    Check("  CHRISTOPHER class is Conjurer (6)", rec.Class == 6, rec.ClassName);
    Check("  CHRISTOPHER race is Gnome (6)", rec.Race == 6, rec.RaceName);
    Check("  CHRISTOPHER stats St17 IQ14 Dx12 Cn12 Lk12",
        rec.GetStatMax(0) == 17 && rec.GetStatMax(1) == 14 && rec.GetStatMax(2) == 12
        && rec.GetStatMax(3) == 12 && rec.GetStatMax(4) == 12,
        $"{rec.GetStatMax(0)},{rec.GetStatMax(1)},{rec.GetStatMax(2)},{rec.GetStatMax(3)},{rec.GetStatMax(4)}");
    Check("  CHRISTOPHER HP 26/26", rec.HpCur == 26 && rec.HpMax == 26, $"{rec.HpCur}/{rec.HpMax}");
    Check("  CHRISTOPHER SP 16/16", rec.SpCur == 16 && rec.SpMax == 16, $"{rec.SpCur}/{rec.SpMax}");
    Check("  CHRISTOPHER AC 9", rec.ArmorClass == 9, $"{rec.ArmorClass}");
    Check("  CHRISTOPHER level 2", rec.Level == 2, $"{rec.Level}");
    Check("  CHRISTOPHER experience 3198", rec.Experience == 3198, $"{rec.Experience}");
    // Slot 0 = equipped Dagger (id 5), slot 1 = equipped Robes (id 16).
    ushort w0 = rec.GetItemWord(0), w1 = rec.GetItemWord(1);
    Check("  CHRISTOPHER slot 1 = equipped Dagger",
        (w0 & PartyFormat.ItemEquippedFlag) != 0 && (w0 & 0x7FFF) == 5, $"0x{w0:X4}");
    Check("  CHRISTOPHER slot 2 = equipped Robes",
        (w1 & PartyFormat.ItemEquippedFlag) != 0 && (w1 & 0x7FFF) == 16, $"0x{w1:X4}");
    Check("  CHRISTOPHER Conjurer spell level 1",
        rec.GetSpellLevel(1) == 1, $"{rec.GetSpellLevel(1)}");
    Check("  CHRISTOPHER name parsed", rec.Name == "CHRISTOPHER", rec.Name);
});

CheckTpw("A R HELPER", Path.Combine(docs, "A-R-HELPER-79.TPW"), rec =>
{
    Check("  A R HELPER class is Warrior (0)", rec.Class == 0, rec.ClassName);
    Check("  A R HELPER race is Human (0)", rec.Race == 0, rec.RaceName);
    Check("  A R HELPER stats St14 IQ13 Dx15 Cn14 Lk7",
        rec.GetStatMax(0) == 14 && rec.GetStatMax(1) == 13 && rec.GetStatMax(2) == 15
        && rec.GetStatMax(3) == 14 && rec.GetStatMax(4) == 7,
        $"{rec.GetStatMax(0)},{rec.GetStatMax(1)},{rec.GetStatMax(2)},{rec.GetStatMax(3)},{rec.GetStatMax(4)}");
    Check("  A R HELPER HP 29/29", rec.HpCur == 29 && rec.HpMax == 29, $"{rec.HpCur}/{rec.HpMax}");
    Check("  A R HELPER SP 0 (non-caster)", rec.SpMax == 0, $"{rec.SpMax}");
    Check("  A R HELPER level 1", rec.Level == 1, $"{rec.Level}");
    Check("  A R HELPER no spell levels",
        Enumerable.Range(0, 4).All(i => rec.GetSpellLevel(i) == 0));
    Check("  A R HELPER name parsed", rec.Name == "A R HELPER", rec.Name);
});

// Round-trip: load CHRISTOPHER, bump HP, ensure the byte change is exact and isolated.
{
    var bytes = File.ReadAllBytes(Path.Combine(docs, "CHRISTOPHER-64.TPW"));
    var rec = CharacterRecord.FromTpw(bytes)!;
    var before = rec.ToArray();
    rec.HpCur = 999;
    Check("Round-trip: HpCur reads back 999", rec.HpCur == 999);
    int diff = 0;
    for (int i = 0; i < before.Length; i++) if (before[i] != rec.Raw[i]) diff++;
    Check("Round-trip: only the 2 HP-current bytes changed", diff == 2, $"{diff} bytes differ");
}

// ---- .TPW writer: ToTpw must reproduce the original file (modulo the name's NUL quirk) ----
Console.WriteLine("\n=== .TPW writer (ToTpw) ===");
{
    var original = File.ReadAllBytes(Path.Combine(docs, "CHRISTOPHER-64.TPW"));
    var rec = CharacterRecord.FromTpw(original)!;
    var written = rec.ToTpw();
    Check("ToTpw produces a 109-byte file", written.Length == PartyFormat.TpwFileSize, $"{written.Length}");
    Check("ToTpw record bytes match the original",
        written.AsSpan(PartyFormat.TpwRecordOffset, PartyFormat.RecordSize)
            .SequenceEqual(original.AsSpan(PartyFormat.TpwRecordOffset, PartyFormat.RecordSize)));
    Check("ToTpw name reads back as CHRISTOPHER",
        CharacterRecord.FromTpw(written)!.Name == "CHRISTOPHER");
    Check("ToTpw sets the on-disk marker even from a live-state record",
        written[PartyFormat.TpwRecordOffset] == 1);
    // A live record (marker 0) must not be mutated by serialisation.
    rec.SetByte(PartyFormat.OffDiskMarker, 0);
    _ = rec.ToTpw();
    Check("ToTpw leaves the in-memory record untouched", rec.GetByte(PartyFormat.OffDiskMarker) == 0);
}

// ---- PartySnapshot: 7-block .TPW-format snapshot build + read round-trip ----
Console.WriteLine("\n=== PartySnapshot (build/read round-trip) ===");
{
    var tpw = File.ReadAllBytes(Path.Combine(docs, "CHRISTOPHER-64.TPW"));
    var snapRecords = new List<CharacterRecord>();
    foreach (var slot in new[] { 1, 4, 6 })
    {
        var rec = CharacterRecord.FromTpw(tpw)!;
        rec.Slot = slot;
        rec.Name = $"SNAP{slot}";
        snapRecords.Add(rec);
    }
    var snapFile = PartySnapshot.Build(snapRecords);
    Check("snapshot file is 7 .TPW blocks", snapFile.Length == PartySnapshot.FileSize
        && PartySnapshot.FileSize == PartyFormat.PartySlots * PartyFormat.TpwFileSize);
    var snapBack = PartySnapshot.Read(snapFile);
    Check("snapshot reads back the 3 characters at their slots",
        snapBack.Count == 3 && snapBack.Select(s => s.Slot).SequenceEqual(new[] { 1, 4, 6 }));
    Check("snapshot names survive the round-trip",
        snapBack.All(s => s.Name == $"SNAP{s.Slot}"));
    Check("snapshot records are byte-identical apart from the disk marker",
        snapBack.All(s => s.Record.AsSpan(1).SequenceEqual(
            snapRecords.First(r => r.Slot == s.Slot).Raw.AsSpan(1))));
    Check("empty slots read as no character", PartySnapshot.Read(new byte[PartySnapshot.FileSize]).Count == 0);
    Check("out-of-range slots are skipped",
        PartySnapshot.Build(new[] { new CharacterRecord { Slot = 9 } }).All(b => b == 0));
}

// ---- MapCalibration: two-anchor linear transform ----
Console.WriteLine("\n=== MapCalibration (two-anchor transform) ===");
{
    var calA = new MapAnchor(100, 200, 0, 0);
    var calB = new MapAnchor(260, 40, 16, 16);   // 10 px per cell in X, -10 in Y (north is up)
    var cal = MapCalibration.FromAnchors(calA, calB);
    Check("two good anchors calibrate", cal != null);
    var px = cal!.ToPixel(8, 8);
    Check("ToPixel maps the midpoint", Math.Abs(px.X - 180) < 1e-9 && Math.Abs(px.Y - 120) < 1e-9);
    Check("ToGame inverts ToPixel (with rounding)", cal.ToGame(183, 116) == (8, 8));
    Check("anchors are interchangeable", MapCalibration.FromAnchors(calB, calA)!.ToGame(183, 116) == (8, 8));
    Check("anchors sharing a row can't calibrate",
        MapCalibration.FromAnchors(new MapAnchor(0, 0, 3, 7), new MapAnchor(50, 50, 9, 7)) == null);
    Check("anchors sharing a column can't calibrate",
        MapCalibration.FromAnchors(new MapAnchor(0, 0, 3, 7), new MapAnchor(50, 50, 3, 12)) == null);
    Check("coincident pixels can't calibrate",
        MapCalibration.FromAnchors(new MapAnchor(10, 10, 0, 0), new MapAnchor(10, 10, 5, 5)) == null);
}

// ---- MapBook: the area list backing the Maps tab ----
Console.WriteLine("\n=== MapBook (area list) ===");
Check("17 areas: the city + 16 dungeon levels", MapBook.Maps.Count == 17, $"{MapBook.Maps.Count}");
Check("Skara Brae is 30×30; every dungeon level is 22×22",
    MapBook.Maps[0].Name == "Skara Brae" && MapBook.Maps[0].Width == 30 && MapBook.Maps[0].Height == 30
    && MapBook.Maps.Skip(1).All(m => m is { Width: 22, Height: 22 }));
Check("map names are unique (calibration is keyed by name)",
    MapBook.Maps.Select(m => m.Name).Distinct().Count() == MapBook.Maps.Count);

// ---- MonsterBook: the bestiary extracted from the live data segment ----
Console.WriteLine("\n=== MonsterBook (bestiary) ===");
Check("bestiary has 127 monsters with sequential 0-based ids",
    MonsterBook.Bestiary.Count == MonsterBook.Count && MonsterBook.Count == PartyFormat.MonsterCount
    && MonsterBook.Bestiary.Select(m => m.Id).SequenceEqual(Enumerable.Range(0, 127)));
Check("markup decodes: Kobold^^s^ → Kobold / Kobolds",
    MonsterBook.DecodeName("Kobold^^s^") == ("Kobold", "Kobolds"));
Check("markup decodes: Dwar^f^ves^ → Dwarf / Dwarves",
    MonsterBook.DecodeName("Dwar^f^ves^") == ("Dwarf", "Dwarves"));
Check("markup decodes: Old M^an^en^ → Old Man / Old Men",
    MonsterBook.DecodeName("Old M^an^en^") == ("Old Man", "Old Men"));
Check("markup decodes: Mercenar^y^ies^ → Mercenary / Mercenaries",
    MonsterBook.DecodeName("Mercenar^y^ies^") == ("Mercenary", "Mercenaries"));
Check("no markup means invariant plural (Samurai)",
    MonsterBook.DecodeName("Samurai") == ("Samurai", "Samurai"));
Check("id 0 is the Kobold, 117 is Mangar, 126 is the Old Man",
    MonsterBook.Bestiary[0].Name == "Kobold" && MonsterBook.Bestiary[117].Name == "Mangar"
    && MonsterBook.Bestiary[126].Name == "Old Man" && MonsterBook.Bestiary[126].Plural == "Old Men");
Check("the tier-2 band ends with the four enemy caster classes (ids 20-23)",
    MonsterBook.Bestiary[20].Name == "Conjurer" && MonsterBook.Bestiary[21].Name == "Magician"
    && MonsterBook.Bestiary[22].Name == "Sorcerer" && MonsterBook.Bestiary[23].Name == "Wizard");
Check("group boundaries: 0/15 → tier 1, 16 → tier 2, 126 → tier 8",
    MonsterBook.GroupOf(0) == MonsterBook.GroupOf(15) && MonsterBook.GroupOf(15) != MonsterBook.GroupOf(16)
    && MonsterBook.GroupOf(126).StartsWith("Tier 8"));
Check("every monster has a usable name and plural",
    MonsterBook.Bestiary.All(m => !string.IsNullOrWhiteSpace(m.Name) && !string.IsNullOrWhiteSpace(m.Plural)
        && !m.Name.Contains('^') && !m.Plural.Contains('^')));

// ---- DumpComparer: address-space diff of two dump files ----
Console.WriteLine("\n=== DumpComparer (dump diff) ===");
string diffDir = Path.Combine(Path.GetTempPath(), "bt1-formatcheck-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(diffDir);
try
{
    // Old dump: region 0x1000 (32 bytes) + region 0x3000 (16 bytes).
    // New dump: same two regions but stored in the opposite file order (layout shift),
    // plus a region 0x5000 that only the new dump has.
    var oldR1 = new byte[32]; var oldR2 = new byte[16];
    var newR1 = (byte[])oldR1.Clone(); var newR2 = (byte[])oldR2.Clone();
    newR1[4] = 0xAA;                      // one-byte change at 0x1004
    newR1[8] = 0xBB;                      // 4-byte gap from the last diff → merges into one run
    newR1[20] = 0xCC; newR1[21] = 0xCD;   // separate two-byte run at 0x1014
    newR2[3] = 0x11;                      // run in the second region at 0x3003
    File.WriteAllBytes(Path.Combine(diffDir, "old.bin"), oldR1.Concat(oldR2).ToArray());
    File.WriteAllText(Path.Combine(diffDir, "old.bin.csv"),
        "FileOffset,ProcessAddress,Size,UnreadableBytes\n0x0,0x1000,0x20,0x0\n0x20,0x3000,0x10,0x0\n");
    File.WriteAllBytes(Path.Combine(diffDir, "new.bin"), newR2.Concat(newR1).Concat(new byte[8]).ToArray());
    File.WriteAllText(Path.Combine(diffDir, "new.bin.csv"),
        "FileOffset,ProcessAddress,Size,UnreadableBytes\n0x0,0x3000,0x10,0x0\n0x10,0x1000,0x20,0x0\n0x30,0x5000,0x8,0x0\n");

    var oldIdx = DumpComparer.ReadIndex(Path.Combine(diffDir, "old.bin.csv"));
    var newIdx = DumpComparer.ReadIndex(Path.Combine(diffDir, "new.bin.csv"));
    Check("index parses offsets/addresses/sizes",
        oldIdx.Count == 2 && oldIdx[1].Address == 0x3000 && oldIdx[1].FileOffset == 0x20 && oldIdx[1].Size == 0x10);

    var diff = DumpComparer.Compare(
        Path.Combine(diffDir, "old.bin"), oldIdx,
        Path.Combine(diffDir, "new.bin"), newIdx,
        maxRuns: 100, progress: null, CancellationToken.None);
    Check("compares only the shared 48 bytes", diff.BytesCompared == 48);
    Check("counts the 5 changed bytes", diff.BytesChanged == 5);
    Check("reports the new-only region as uncompared", diff.BytesOnlyInOne == 8);
    Check("finds 3 runs (≤4-byte gaps merged)", diff.Runs.Count == 3 && !diff.Truncated);
    var run0 = diff.Runs.FirstOrDefault(r => r.Address == 0x1004);
    Check("merged run spans 0x1004..0x1008 with both changed bytes",
        run0 != null && run0.Length == 5
        && run0.OldBytes.SequenceEqual(new byte[] { 0, 0 }) && run0.NewBytes.SequenceEqual(new byte[] { 0xAA, 0xBB }));
    Check("two-byte run at 0x1014 reads CC CD",
        diff.Runs.Any(r => r.Address == 0x1014 && r.Length == 2 && r.NewBytes.SequenceEqual(new byte[] { 0xCC, 0xCD })));
    Check("second region's run lands at 0x3003",
        diff.Runs.Any(r => r.Address == 0x3003 && r.NewBytes.SequenceEqual(new byte[] { 0x11 })));

    var truncated = DumpComparer.Compare(
        Path.Combine(diffDir, "old.bin"), oldIdx,
        Path.Combine(diffDir, "new.bin"), newIdx,
        maxRuns: 1, progress: null, CancellationToken.None);
    Check("run cap marks the result truncated", truncated.Truncated && truncated.Runs.Count <= 2);

    // A changed run straddling a streamed-chunk boundary must come back as ONE run —
    // shrink the chunk size so the 64-byte region spans several chunks (boundary at 16).
    var oldBig = new byte[64];
    var newBig = (byte[])oldBig.Clone();
    foreach (var i in new[] { 14, 15, 16, 17, 18 }) newBig[i] = 0xEE;   // contiguous across the boundary
    newBig[21] = 0xEF;                                                  // 2-byte gap, still merges (≤4)
    File.WriteAllBytes(Path.Combine(diffDir, "oldbig.bin"), oldBig);
    File.WriteAllText(Path.Combine(diffDir, "oldbig.bin.csv"),
        "FileOffset,ProcessAddress,Size,UnreadableBytes\n0x0,0x7000,0x40,0x0\n");
    File.WriteAllBytes(Path.Combine(diffDir, "newbig.bin"), newBig);
    File.WriteAllText(Path.Combine(diffDir, "newbig.bin.csv"),
        "FileOffset,ProcessAddress,Size,UnreadableBytes\n0x0,0x7000,0x40,0x5\n");
    var bigIdxOld = DumpComparer.ReadIndex(Path.Combine(diffDir, "oldbig.bin.csv"));
    var bigIdxNew = DumpComparer.ReadIndex(Path.Combine(diffDir, "newbig.bin.csv"));
    var bigDiff = DumpComparer.Compare(
        Path.Combine(diffDir, "oldbig.bin"), bigIdxOld,
        Path.Combine(diffDir, "newbig.bin"), bigIdxNew,
        maxRuns: 100, progress: null, CancellationToken.None, chunkSize: 16);
    Check("run straddling a chunk boundary merges into one",
        bigDiff.Runs.Count == 1 && bigDiff.Runs[0].Address == 0x700E && bigDiff.Runs[0].Length == 8
        && bigDiff.BytesChanged == 6 && bigDiff.BytesCompared == 64);
    Check("unreadable byte counts are surfaced", bigDiff.BytesUnreadable == 5);
}
finally
{
    Directory.Delete(diffDir, recursive: true);
}

Console.WriteLine("\n=== Memory dump (optional) ===");
string? dump = Directory.Exists(testdata)
    ? Directory.GetFiles(testdata, "*.bin").FirstOrDefault()
    : null;
if (dump == null)
{
    Console.WriteLine("(no .bin dump in testdata\\ — skipping live-layout checks)");
}
else
{
    // The data segment sits at file offset 0x3A00B00 in this dump (guest base + DGROUP).
    // Verify the three string anchors and the CHRISTOPHER live record at slot 6.
    const long DsFileOffset = 0x3A00B00;
    using var fs = File.OpenRead(dump);

    bool Anchor(string label, int dsOffset, byte[] expected)
    {
        var got = ReadAt(fs, DsFileOffset + dsOffset, expected.Length);
        bool ok = got.AsSpan().SequenceEqual(expected);
        Check($"  anchor {label} @ DS:0x{dsOffset:X}", ok);
        return ok;
    }
    Anchor("race table", PartyFormat.DsRaceTable, PartyFormat.RaceTableBytes);
    Anchor("item table", PartyFormat.DsItemTable, PartyFormat.ItemTableBytes);
    Anchor("class table", PartyFormat.DsClassTable, PartyFormat.ClassTableBytes);

    // Slot 6 of the party array should be CHRISTOPHER (the only occupied slot in the dump).
    long slot6 = DsFileOffset + PartyFormat.DsPartySlots + 6 * PartyFormat.RecordSize;
    var rec = new CharacterRecord(ReadAt(fs, slot6, PartyFormat.RecordSize));
    Check("  live slot 6 status occupied (0)", rec.Status == 0, $"{rec.Status}");
    Check("  live slot 6 is a Conjurer (6)", rec.Class == 6, rec.ClassName);
    Check("  live slot 6 HP 26/26", rec.HpCur == 26 && rec.HpMax == 26, $"{rec.HpCur}/{rec.HpMax}");
    Check("  live slot 6 Conjurer spell level 1", rec.GetSpellLevel(1) == 1, $"{rec.GetSpellLevel(1)}");

    // Roster name row 6 should read CHRISTOPHER.
    long row6 = DsFileOffset + PartyFormat.DsPartyRows + 6 * PartyFormat.PartyRowStride;
    var nameBytes = ReadAt(fs, row6, PartyFormat.PartyRowNameLength);
    string name = new string(nameBytes.TakeWhile(b => b >= 0x20 && b < 0x7F).Select(b => (char)b).ToArray()).Trim();
    Check("  roster row 6 name = CHRISTOPHER", name == "CHRISTOPHER", name);

    // The bestiary must match the live monster name table verbatim: walk the pointer
    // table at DS:0x2F3E and compare every raw (markup-carrying) name to MonsterBook.
    var dsBytes = ReadAt(fs, DsFileOffset, 0x3100);
    int mismatches = 0, decoded = 0;
    for (int id = 0; id < PartyFormat.MonsterCount; id++)
    {
        int p = dsBytes[PartyFormat.DsMonsterNamePtrs + id * 2]
              | (dsBytes[PartyFormat.DsMonsterNamePtrs + id * 2 + 1] << 8);
        if (p < PartyFormat.DsMonsterNames || p >= PartyFormat.DsMonsterNamePtrs) break;
        int end = p;
        while (end < dsBytes.Length && dsBytes[end] != 0) end++;
        string raw = System.Text.Encoding.ASCII.GetString(dsBytes, p, end - p);
        decoded++;
        if (raw != MonsterBook.Bestiary[id].Raw) mismatches++;
    }
    Check($"  monster name table decodes all {PartyFormat.MonsterCount} entries",
        decoded == PartyFormat.MonsterCount, $"{decoded}");
    Check("  MonsterBook matches the live name table verbatim", mismatches == 0, $"{mismatches} mismatch(es)");
    int afterTable = dsBytes[PartyFormat.DsMonsterNamePtrs + PartyFormat.MonsterCount * 2]
                   | (dsBytes[PartyFormat.DsMonsterNamePtrs + PartyFormat.MonsterCount * 2 + 1] << 8);
    Check("  pointer table ends after 127 entries",
        afterTable < PartyFormat.DsMonsterNames || afterTable >= PartyFormat.DsMonsterNamePtrs, $"0x{afterTable:X4}");
}

Console.WriteLine();
if (failures == 0)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("All checks passed.");
    Console.ResetColor();
    return 0;
}
Console.ForegroundColor = ConsoleColor.Red;
Console.WriteLine($"{failures} check(s) FAILED.");
Console.ResetColor();
return 1;

// --- helpers ---------------------------------------------------------------------
void CheckTpw(string who, string path, Action<CharacterRecord> body)
{
    if (!File.Exists(path)) { Check($"{who} .TPW present", false, path); return; }
    var rec = CharacterRecord.FromTpw(File.ReadAllBytes(path));
    if (rec == null) { Check($"{who} parsed", false); return; }
    body(rec);
}

static byte[] ReadAt(FileStream fs, long offset, int count)
{
    fs.Seek(offset, SeekOrigin.Begin);
    var buf = new byte[count];
    int read = 0;
    while (read < count)
    {
        int n = fs.Read(buf, read, count - read);
        if (n <= 0) break;
        read += n;
    }
    return buf;
}

static string FindRepoRoot()
{
    var dir = AppContext.BaseDirectory;
    for (int i = 0; i < 8 && dir != null; i++)
    {
        if (File.Exists(Path.Combine(dir, "BardsTale1Trainer.sln"))) return dir;
        dir = Directory.GetParent(dir)?.FullName;
    }
    return AppContext.BaseDirectory;
}
