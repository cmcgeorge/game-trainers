using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using GameTrainers.Common.Memory;
using HillsfarTrainer.Game;
using HillsfarTrainer.Memory;
using HillsfarTrainer.ViewModels;

namespace FormatCheck;

/// <summary>
/// Headless verification harness. Asserts the reverse-engineered layout, the reference tables, the
/// record's clamps and flush ranges, the locator driven over a synthetic address space, and the
/// view-models' behaviour through a fake host. Needs neither the game nor an emulator.
///
/// <para>A final group parses the shipped <c>.HIL</c>/<c>.PRE</c> files when they are present. Those
/// are copyrighted and are not in the repository, so that group is <b>skipped with a note</b> rather
/// than failed when they are absent.</para>
/// </summary>
internal static class Program
{
    private static int _checks;
    private static int _failures;
    private static string _group = "";

    private static int Main(string[] args)
    {
        // An explicit path is validated the same way as a discovered one, so pointing the harness at
        // a folder that has no character files skips that group rather than failing it.
        string? corpus = args.Length > 0 ? ValidCorpus(args[0]) : FindCorpus();

        Layout();
        ClassTables();
        RaceAlignmentTables();
        ClockAndHealing();
        LocationHours();
        ArenaTable();
        TextCodecChecks();
        RecordAccessors();
        FlushRanges();
        NameField();
        LockPicks();
        FileRoundTrip();
        LocatorChecks();
        ViewModelChecks();
        FreezeEntryChecks();
        BindabilityChecks();
        RegressionChecks();
        CrossReviewChecks();
        ShippedCorpus(corpus);

        Console.WriteLine();
        Console.WriteLine(_failures == 0
            ? $"PASS — {_checks} checks."
            : $"FAIL — {_failures} of {_checks} checks failed.");
        return _failures == 0 ? 0 : 1;
    }

    // --- tiny assertion helpers ----------------------------------------------

    private static void Group(string name)
    {
        _group = name;
        Console.WriteLine($"-- {name}");
    }

    private static void Check(bool ok, string what)
    {
        _checks++;
        if (ok) return;
        _failures++;
        Console.WriteLine($"   FAIL [{_group}] {what}");
    }

    private static void Eq<T>(T actual, T expected, string what) =>
        Check(EqualityComparer<T>.Default.Equals(actual, expected),
              $"{what}: expected {expected}, got {actual}");

    // --- 1. the offset table --------------------------------------------------

    private static void Layout()
    {
        Group("record layout");
        Eq(CharacterFormat.RecordLength, 188, "record length");
        Eq(CharacterFormat.DgroupRecordOffset, 0x094C, "record sits at DGROUP:0x094C");

        // Each offset is pinned to the DGROUP address the reverse engineering established, so a
        // typo in one constant cannot quietly shift the whole table.
        var expected = new (int Off, int Dgroup, string Name)[]
        {
            (CharacterFormat.OffName, 0x0950, "name"),
            (CharacterFormat.OffStrength, 0x0960, "strength"),
            (CharacterFormat.OffStrengthPercentile, 0x0961, "strength percentile"),
            (CharacterFormat.OffIntelligence, 0x0962, "intelligence"),
            (CharacterFormat.OffWisdom, 0x0963, "wisdom"),
            (CharacterFormat.OffDexterity, 0x0964, "dexterity"),
            (CharacterFormat.OffConstitution, 0x0965, "constitution"),
            (CharacterFormat.OffCharisma, 0x0966, "charisma"),
            (CharacterFormat.OffAlignment, 0x0968, "alignment"),
            (CharacterFormat.OffAge, 0x096A, "age"),
            (CharacterFormat.OffHitPoints, 0x096C, "hit points"),
            (CharacterFormat.OffHitPointsMax, 0x096D, "hit points max"),
            (CharacterFormat.OffClassIndex, 0x0970, "class index"),
            (CharacterFormat.OffGold, 0x0974, "gold"),
            (CharacterFormat.OffGender, 0x0978, "gender"),
            (CharacterFormat.OffRace, 0x0979, "race"),
            (CharacterFormat.OffExperience, 0x097A, "experience"),
            (CharacterFormat.OffClassMask, 0x0981, "class mask"),
            (CharacterFormat.OffDay, 0x098A, "day"),
            (CharacterFormat.OffTickTime, 0x098C, "clock tick time"),
            (CharacterFormat.OffHour, 0x0990, "hour"),
            (CharacterFormat.OffFlags, 0x0991, "flags"),
            (CharacterFormat.OffLockPicks, 0x0992, "lock picks"),
            (CharacterFormat.OffKnockRings, 0x09D2, "knock rings"),
            (CharacterFormat.OffHealingPotions, 0x09D3, "healing potions"),
            (CharacterFormat.OffHourTimers, 0x09D5, "hour timers"),
            (CharacterFormat.OffArcheryLevel, 0x09EB, "archery level"),
            (CharacterFormat.OffHealCountdown, 0x09F7, "heal countdown"),
            (CharacterFormat.OffLevelCleric, 0x0A03, "cleric level"),
            (CharacterFormat.OffLevelMagicUser, 0x0A04, "magic-user level"),
            (CharacterFormat.OffLevelFighter, 0x0A05, "fighter level"),
            (CharacterFormat.OffLevelThief, 0x0A06, "thief level"),
        };
        foreach (var (off, dgroup, name) in expected)
            Eq(CharacterFormat.DgroupRecordOffset + off, dgroup, $"{name} at DGROUP");

        // Every field must fit inside the record.
        foreach (var (off, _, name) in expected)
            Check(off >= 0 && off < CharacterFormat.RecordLength, $"{name} inside the record");
        Check(CharacterFormat.OffLockPicks + LockPickSet.BlockLength <= CharacterFormat.RecordLength,
              "the pick block fits inside the record");
        Check(CharacterFormat.OffHourTimers + CharacterFormat.HourTimerCount
              <= CharacterFormat.RecordLength, "the hour timers fit inside the record");
        Eq(CharacterFormat.OffName + CharacterFormat.NameFieldLength, CharacterFormat.OffStrength,
           "the name field ends where strength begins");

        // The four level bytes are contiguous and in descending class-mask order.
        Eq(CharacterFormat.OffLevelMagicUser, CharacterFormat.OffLevelCleric + 1, "MU level follows cleric");
        Eq(CharacterFormat.OffLevelFighter, CharacterFormat.OffLevelMagicUser + 1, "fighter follows MU");
        Eq(CharacterFormat.OffLevelThief, CharacterFormat.OffLevelFighter + 1, "thief follows fighter");

        Eq(CharacterFormat.MaxConsumable, 99, "consumable cap");
        Eq(CharacterFormat.MaxArcheryLevel, 15, "archery cap");
        Eq(CharacterFormat.HoursPerDay, 24, "hours per day");
        Eq(CharacterFormat.MinValidators, 2, "minimum corroborating literals");

        // Anchor byte arrays must match their documented text and be long enough to be distinctive.
        Eq(Encoding.ASCII.GetString(CharacterFormat.PrimaryAnchor.Bytes),
           "WARNING: DO NOT RUN MEMORY RESIDENT PROGRAMS WHILE PLAYING HILLSFAR!!",
           "primary anchor text");
        Eq(CharacterFormat.PrimaryAnchor.DgroupOffset, 0x0D1A, "primary anchor offset");
        Eq(CharacterFormat.Validators.Length, 4, "validator count");
        foreach (var v in CharacterFormat.Validators)
        {
            Check(v.Bytes.Length >= 8, $"validator at 0x{v.DgroupOffset:X4} is at least 8 bytes");
            Check(!string.IsNullOrWhiteSpace(v.Description),
                  $"validator at 0x{v.DgroupOffset:X4} has a description");
        }
        Check(CharacterFormat.MinValidators <= CharacterFormat.Validators.Length,
              "MinValidators is reachable");
    }

    // --- 2. class tables ------------------------------------------------------

    private static void ClassTables()
    {
        Group("class tables");
        Eq(ClassBook.Classes.Count, 11, "eleven legal class combinations");

        // The four single classes and their bit values.
        Eq(ClassBook.NameForMask(ClassBook.MaskThief), "Thief", "mask 1");
        Eq(ClassBook.NameForMask(ClassBook.MaskFighter), "Fighter", "mask 2");
        Eq(ClassBook.NameForMask(ClassBook.MaskMagicUser), "Magic-User", "mask 4");
        Eq(ClassBook.NameForMask(ClassBook.MaskCleric), "Cleric", "mask 8");
        Eq(ClassBook.NameForMask(0x7), "FTR/MU/TH", "mask 7");
        Eq(ClassBook.NameForMask(0xE), "CL/FTR/MU", "mask 14");

        // Every pairing of Cleric with Thief is illegal.
        foreach (int mask in new[] { 0x0, 0x9, 0xB, 0xD, 0xF })
            Check(!ClassBook.IsLegalMask(mask), $"mask {mask} is illegal");
        foreach (var c in ClassBook.Classes)
        {
            bool clericThief = (c.Mask & ClassBook.MaskCleric) != 0 &&
                               (c.Mask & ClassBook.MaskThief) != 0;
            Check(!clericThief, $"{c.Name} is not a cleric/thief mix");
        }

        // The mask is stored in both nibbles.
        Eq(ClassBook.PackMask(0x2), (byte)0x22, "packed fighter mask");
        Eq(ClassBook.PackMask(0x8), (byte)0x88, "packed cleric mask");
        Eq(ClassBook.PackMask(0x1), (byte)0x11, "packed thief mask");
        Eq(ClassBook.PackMask(0x4), (byte)0x44, "packed magic-user mask");

        // NameForMask must accept the byte as stored, not just a bare nibble.
        Eq(ClassBook.NameForMask(0x22), "Fighter", "NameForMask accepts a packed byte");

        // The index table, verbatim from DGROUP:0x91DC, and the indices the four shipped .PRE files
        // carry — which is what pinned its alignment.
        Eq(ClassBook.IndexToMask.Count, 16, "index table length");
        Eq(ClassBook.IndexToMask[0], (byte)0x08, "index 0 -> Cleric");
        Eq(ClassBook.IndexToMask[2], (byte)0x02, "index 2 -> Fighter");
        Eq(ClassBook.IndexToMask[5], (byte)0x04, "index 5 -> Magic-User");
        Eq(ClassBook.IndexToMask[6], (byte)0x01, "index 6 -> Thief");
        Eq(ClassBook.IndexForMask(ClassBook.MaskCleric), (byte)0, "CLERIC.PRE index");
        Eq(ClassBook.IndexForMask(ClassBook.MaskFighter), (byte)2, "FIGHTER.PRE index");
        Eq(ClassBook.IndexForMask(ClassBook.MaskMagicUser), (byte)5, "MAGICUSE.PRE index");
        Eq(ClassBook.IndexForMask(ClassBook.MaskThief), (byte)6, "THIEF.PRE index");

        // Every legal mask must round-trip name -> mask.
        foreach (var c in ClassBook.Classes)
            Eq(ClassBook.ForMask(c.Mask)?.Name, c.Name, $"{c.Name} round-trips");
        Eq(ClassBook.NameForMask(0x9), "(none)", "illegal mask names safely");
        Check(ClassBook.ForMask(0x9) is null, "ForMask returns null for an illegal mask");

        // The convenience flags on ClassInfo must agree with the bits.
        var fmt = ClassBook.ForMask(0x7)!.Value;
        Check(fmt.IsFighter && fmt.IsMagicUser && fmt.IsThief && !fmt.IsCleric,
              "FTR/MU/TH flags");
    }

    private static void RaceAlignmentTables()
    {
        Group("race, gender and alignment tables");
        Eq(RaceBook.Races.Count, 6, "six races");
        Eq(RaceBook.Races[0], "Dwarf", "race 0");
        Eq(RaceBook.Races[1], "Elf", "race 1 — confirmed live");
        Eq(RaceBook.Races[5], "Human", "race 5 — confirmed live");
        Eq(RaceBook.NameForRace(99), "(unknown)", "out-of-range race");

        Eq(RaceBook.Genders.Count, 2, "two genders");
        Eq(RaceBook.NameForGender(0), "Male", "gender 0 — confirmed live");
        Eq(RaceBook.NameForGender(1), "Female", "gender 1 — confirmed live");

        Eq(AlignmentBook.Alignments.Count, 9, "nine alignments");
        Eq(AlignmentBook.NameFor(0), "Lawful Good", "alignment 0 — confirmed live");
        Eq(AlignmentBook.NameFor(3), "Neutral Good", "alignment 3");
        Eq(AlignmentBook.NameFor(4), "True Neutral", "alignment 4 — the game reverses this one");
        Eq(AlignmentBook.NameFor(8), "Chaotic Evil", "alignment 8 — confirmed live");
        Eq(AlignmentBook.NameFor(-1), "(unknown)", "out-of-range alignment");

        // The nine names must be law*3 + moral, with 'True Neutral' the only reordered one.
        string[] law = { "Lawful", "Neutral", "Chaotic" };
        string[] moral = { "Good", "Neutral", "Evil" };
        for (int l = 0; l < 3; l++)
            for (int m = 0; m < 3; m++)
            {
                int i = l * 3 + m;
                string want = i == 4 ? "True Neutral" : $"{law[l]} {moral[m]}";
                Eq(AlignmentBook.Alignments[i], want, $"alignment {i} composes correctly");
            }
    }

    // --- 3. clock and healing -------------------------------------------------

    private static void ClockAndHealing()
    {
        Group("clock and healing");
        Eq(GameFacts.RealSecondsPerGameHour, 122, "one game hour in real seconds");

        // The display rule from the game's own clock routine: subtract 12 above 12, and hour 24 and
        // anything below 12 is 'am'.
        Eq(GameFacts.FormatHour(15), "3 pm", "15 -> 3 pm (confirmed on screen)");
        Eq(GameFacts.FormatHour(1), "1 am", "1 -> 1 am");
        Eq(GameFacts.FormatHour(11), "11 am", "11 -> 11 am");
        Eq(GameFacts.FormatHour(12), "12 pm", "12 -> 12 pm");
        Eq(GameFacts.FormatHour(13), "1 pm", "13 -> 1 pm");
        Eq(GameFacts.FormatHour(23), "11 pm", "23 -> 11 pm");
        Eq(GameFacts.FormatHour(24), "12 am", "24 is midnight and reads am");
        Eq(GameFacts.FormatHour(0), "--", "0 is out of range");
        Eq(GameFacts.FormatHour(25), "--", "25 is out of range");

        // Natural healing: 1 + clamp(Con - 14, 0, 5).
        Eq(GameFacts.NaturalHealingPerDay(3), 1, "Con 3 heals 1");
        Eq(GameFacts.NaturalHealingPerDay(14), 1, "Con 14 heals 1");
        Eq(GameFacts.NaturalHealingPerDay(15), 2, "Con 15 heals 2");
        Eq(GameFacts.NaturalHealingPerDay(19), 6, "Con 19 heals 6");
        Eq(GameFacts.NaturalHealingPerDay(25), 6, "the bonus caps at 5");
        for (int con = 3; con <= 25; con++)
            Check(GameFacts.NaturalHealingPerDay(con) is >= 1 and <= 6,
                  $"healing for Con {con} is inside 1..6");

        Eq(GameFacts.QuestCount, 12, "twelve quest scripts — four classes x three missions");
        Check(GameFacts.Controls.Count >= 20, "the control list is populated");
        Check(GameFacts.Tips.Count >= 10, "the tip list is populated");
        Check(GameFacts.EmulatorHints.Contains("dosbox"), "dosbox is an emulator hint");
    }

    // --- 4. opening hours -----------------------------------------------------

    private static void LocationHours()
    {
        Group("opening hours");
        Eq(LocationBook.Locations.Count, 18, "eighteen locations, as in the manual");
        Eq(LocationBook.Pubs.Count, 4, "four named pubs");

        // A missing location must not be substituted with `default`: default(LocationInfo) has
        // AlwaysOpen false and no hours, i.e. it reads as NeverOpen — so every follow-on
        // "is never open" assertion would PASS on the sentinel and the real breakage would show up as
        // one confusing failure instead of pinpointing itself. Throw instead.
        LocationInfo Find(string name)
        {
            foreach (var l in LocationBook.Locations) if (l.Name == name) return l;
            Check(false, $"location '{name}' is present");
            throw new InvalidOperationException($"LocationBook has no '{name}'");
        }

        var arena = Find("Arena");
        Check(arena.IsOpenAt(8) && arena.IsOpenAt(23), "arena open 8 am and 11 pm");
        Check(!arena.IsOpenAt(7) && !arena.IsOpenAt(24), "arena shut at 7 am and midnight");

        var bank = Find("Bank");
        Check(bank.IsOpenAt(8) && bank.IsOpenAt(15), "bank open 8 am to 3 pm");
        Check(!bank.IsOpenAt(16), "bank shut at 4 pm");

        // The pub range wraps past midnight: 5 pm to 7 am.
        var pub = Find("Pub");
        foreach (int h in new[] { 17, 20, 23, 24, 1, 5, 7 })
            Check(pub.IsOpenAt(h), $"pub open at hour {h}");
        foreach (int h in new[] { 8, 12, 16 })
            Check(!pub.IsOpenAt(h), $"pub shut at hour {h}");

        // So does the cemetery: midnight to 7 am.
        var cem = Find("Cemetary");
        Check(cem.IsOpenAt(24) && cem.IsOpenAt(3) && cem.IsOpenAt(7), "cemetery open in the small hours");
        Check(!cem.IsOpenAt(8) && !cem.IsOpenAt(23), "cemetery shut by day");

        // Never-open and always-open locations.
        foreach (var name in new[] { "Castle", "Haunted Mansion", "Jail" })
        {
            var l = Find(name);
            Check(l.NeverOpen, $"{name} never opens");
            Eq(l.Hours, "Never open", $"{name} hours text");
            for (int h = 1; h <= 24; h++) Check(!l.IsOpenAt(h), $"{name} shut at hour {h}");
        }
        foreach (var name in new[] { "Temple of Tempus", "Stable", "Fighter's Guild",
                                     "Mage's Guild", "Rogue's Guild", "Sewer" })
        {
            var l = Find(name);
            Check(l.AlwaysOpen, $"{name} is always open");
            for (int h = 1; h <= 24; h++) Check(l.IsOpenAt(h), $"{name} open at hour {h}");
        }

        // The manual's overlap claim: pubs and the daytime block barely coincide.
        var shopHours = new[] { "Bank", "Book store", "Magic shop", "Mages Tower", "Archery", "Healer" };
        foreach (var name in shopHours)
        {
            var l = Find(name);
            Check(l.IsOpenAt(9) && !l.IsOpenAt(20), $"{name} is a daytime location");
        }

        // OpenAt must agree with IsOpenAt for every hour.
        for (int h = 1; h <= 24; h++)
        {
            var set = LocationBook.OpenAt(h).ToList();
            foreach (var l in LocationBook.Locations)
                Check(set.Contains(l) == l.IsOpenAt(h), $"OpenAt({h}) agrees for {l.Name}");
        }

        // Three overland destinations are hidden-trail only.
        int hidden = LocationBook.Overland.Count(o => o.ReachedFrom.StartsWith("HIDDEN"));
        Eq(hidden, 3, "three hidden overland locations");
    }

    private static void ArenaTable()
    {
        Group("arena roster");
        Eq(ArenaBook.Opponents.Count, 8, "eight opponents");
        Eq(ArenaBook.Opponents.Count(o => o.TellShipped), 4,
           "the game ships four gossip tells");
        foreach (var o in ArenaBook.Opponents)
        {
            Check(!string.IsNullOrWhiteSpace(o.Name), "opponent has a name");
            Check(o.Tell.Length > 30, $"{o.Name} has a substantive tell");
        }
        Check(ArenaBook.Opponents.Any(o => o.Name.Contains("Taurus")), "Taurus is in the roster");
        Check(ArenaBook.MissionGates.Count >= 5, "mission gates are listed");
    }

    // --- 5. the text codec ----------------------------------------------------

    private static void TextCodecChecks()
    {
        Group("text codec");
        Eq(TextCodec.TableLength, 144, "table length");
        Eq(TextCodec.ShippedTable.Length, TextCodec.TableLength, "shipped table is the right length");

        // The last entry is 0x80, not a character — which is why the table is stored as bytes rather
        // than as a string literal. An ASCII round-trip would turn it into '?', and the constant
        // would then never compare equal to the table read out of a live game.
        Eq(TextCodec.ShippedTable[^1], (byte)0x80, "the final table byte is 0x80, carried verbatim");
        for (int i = 0; i < TextCodec.TableLength - 1; i++)
            Check(TextCodec.ShippedTable[i] is >= 0x20 and < 0x7F,
                  $"table byte {i} is printable ASCII");
        Eq(Encoding.ASCII.GetString(TextCodec.ShippedTable, 0, 16), " eotahnrsiuldygc",
           "the sixteen first characters match the locator's validator");
        Check(CharacterFormat.Validators.Any(
                  v => v.DgroupOffset == TextCodec.DgroupTableOffset &&
                       v.Bytes.AsSpan().SequenceEqual(TextCodec.ShippedTable.AsSpan(0, v.Bytes.Length))),
              "the codec-table validator is a genuine prefix of the shipped table");

        // The fifteen expansions the layout was solved against.
        var known = new (byte Code, string Expect)[]
        {
            (0x84, " s"), (0x89, "er"), (0x8B, "ea"), (0x94, "oo"), (0x9A, "to"),
            (0xA1, "an"), (0xAA, "hi"), (0xAC, "ho"), (0xAD, "ht"), (0xB8, "re"),
            (0xBC, "ri"), (0xC2, "se"), (0xCD, "ig"), (0xD9, "le"), (0xF6, "gi"),
        };
        foreach (var (code, expect) in known)
            Eq(TextCodec.Shipped.Expand(code), expect, $"0x{code:X2} expands");

        // Words whose plaintext is known from the game's own tables.
        Eq(TextCodec.Shipped.Decode(new byte[] { 0x54, 0xAA, 0x65, 0x66, 0x00 }), "Thief",
           "'Thief' decodes");
        Eq(TextCodec.Shipped.Decode(new byte[] { 0x46, 0xCD, 0xAD, 0x89, 0x00 }), "Fighter",
           "'Fighter' decodes");
        Eq(TextCodec.Shipped.Decode(new byte[] { 0x43, 0xD9, 0xBC, 0x63, 0x00 }), "Cleric",
           "'Cleric' decodes");
        Eq(TextCodec.Shipped.Decode(
               new byte[] { 0x4D, 0x61, 0xF6, 0x63, 0x2D, 0x55, 0xC2, 0x72, 0x00 }),
           "Magic-User", "'Magic-User' decodes");
        Eq(TextCodec.Shipped.Decode(new byte[] { 0x48, 0x8B, 0xD9, 0x72, 0x00 }), "Healer",
           "'Healer' decodes");
        Eq(TextCodec.Shipped.Decode(new byte[] { 0x42, 0xA1, 0x6B, 0x00 }), "Bank", "'Bank' decodes");
        Eq(TextCodec.Shipped.Decode(new byte[] { 0x42, 0x94, 0x6B, 0x84, 0x9A, 0xB8, 0x00 }),
           "Book store", "'Book store' decodes");

        // Control bytes: 0x0D is a break, 0x00 terminates, others are shown not dropped.
        Eq(TextCodec.Shipped.Decode(new byte[] { 0x41, 0x0D, 0x42, 0x00, 0x43 }), "A\nB",
           "0x0D breaks and 0x00 terminates");
        Eq(TextCodec.Shipped.Decode(new byte[] { 0x41, 0x01, 0x42 }), "A<01>B",
           "an unexpected control byte is shown");

        // FromMemory falls back to the shipped table on a bad read, and honours a good one.
        Check(ReferenceEquals(TextCodec.FromMemory(null), TextCodec.Shipped),
              "a null live table falls back to the shipped one");
        Check(ReferenceEquals(TextCodec.FromMemory(new byte[10]), TextCodec.Shipped),
              "a short live table falls back to the shipped one");
        var custom = (byte[])TextCodec.ShippedTable.Clone();
        custom[16] = (byte)'Z';
        Eq(TextCodec.FromMemory(custom).Expand(0x80), " Z", "a live table is honoured");

        // Bytes below 0x80 expand to themselves.
        Eq(TextCodec.Shipped.Expand((byte)'Q'), "Q", "an ASCII byte expands to itself");
    }

    // --- 6. record accessors and clamps ---------------------------------------

    /// <summary>
    /// Builds a valid synthetic record. Values are chosen to match the character the reverse
    /// engineering was confirmed against — Christopher, a level-5 human fighter.
    /// </summary>
    private static byte[] MakeRecord()
    {
        var rec = new byte[CharacterFormat.RecordLength];
        var r = new CharacterRecord(rec);
        r.Name = "Christopher";
        r.Strength = 18;
        r.StrengthPercentile = 22;
        r.Intelligence = 9;
        r.Wisdom = 15;
        r.Dexterity = 14;
        r.Constitution = 16;
        r.Charisma = 13;
        r.Alignment = 0;
        r.Age = 23;
        r.HitPointsMax = 42;
        r.HitPoints = 42;
        r.Gold = 590;
        r.Experience = 25000;
        r.Gender = 0;
        r.Race = 5;
        r.ClassMask = ClassBook.MaskFighter;
        r.FighterLevel = 5;
        r.Hour = 15;
        return rec;
    }

    private static void RecordAccessors()
    {
        Group("record accessors");
        var rec = MakeRecord();
        var r = new CharacterRecord(rec);

        Eq(r.Name, "Christopher", "name reads back");
        Eq(r.Strength, 18, "strength");
        Eq(r.StrengthPercentile, 22, "strength percentile");
        Eq(r.Intelligence, 9, "intelligence");
        Eq(r.Wisdom, 15, "wisdom");
        Eq(r.Dexterity, 14, "dexterity");
        Eq(r.Constitution, 16, "constitution");
        Eq(r.Charisma, 13, "charisma");
        Eq(r.Age, 23, "age");
        Eq(r.HitPoints, 42, "hit points");
        Eq(r.HitPointsMax, 42, "hit points max");
        Eq(r.Gold, 590u, "gold");
        Eq(r.Experience, 25000u, "experience");
        Eq(r.Race, 5, "race");
        Eq(r.Gender, 0, "gender");
        Eq(r.ClassName, "Fighter", "class name");
        Eq(r.Hour, 15, "hour");
        Eq(r.HourText, "3 pm", "hour text matches the screen");
        Eq(r.DisplayLevel, 5, "display level comes from the fighter slot");
        Check(CharacterFormat.LooksLikeRecord(rec), "the synthetic record passes the shape check");

        // Little-endian widths, checked against the raw bytes.
        Eq(BinaryPrimitives.ReadUInt32LittleEndian(rec.AsSpan(CharacterFormat.OffGold, 4)), 590u,
           "gold is a 32-bit LE word");
        Eq(BinaryPrimitives.ReadUInt32LittleEndian(rec.AsSpan(CharacterFormat.OffExperience, 4)),
           25000u, "experience is a 32-bit LE word");
        Eq(BinaryPrimitives.ReadUInt16LittleEndian(rec.AsSpan(CharacterFormat.OffAge, 2)), (ushort)23,
           "age is a 16-bit LE word");

        // Setting the mask must set the index too.
        Eq(rec[CharacterFormat.OffClassMask], (byte)0x22, "the mask is stored in both nibbles");
        Eq(rec[CharacterFormat.OffClassIndex], (byte)2, "the class index follows the mask");
        r.ClassMask = ClassBook.MaskCleric;
        Eq(rec[CharacterFormat.OffClassMask], (byte)0x88, "cleric mask");
        Eq(rec[CharacterFormat.OffClassIndex], (byte)0, "cleric index");
        // An illegal mask must be refused rather than stored.
        r.ClassMask = 0x9;
        Eq(rec[CharacterFormat.OffClassMask], (byte)0x88, "an illegal mask is refused");

        Group("record clamps");
        r.Strength = 99;
        Eq(r.Strength, CharacterFormat.MaxAbility, "strength clamps up");
        r.Strength = 0;
        Eq(r.Strength, CharacterFormat.MinAbility, "strength clamps down");
        r.StrengthPercentile = 500;
        Eq(r.StrengthPercentile, 100, "percentile clamps to 100");
        r.KnockRings = 1000;
        Eq(r.KnockRings, 99, "knock rings clamp to the game's cap");
        r.HealingPotions = -5;
        Eq(r.HealingPotions, 0, "potions clamp at zero");
        r.ArcheryLevel = 99;
        Eq(r.ArcheryLevel, 15, "archery clamps to the game's cap");
        r.Hour = 0;
        Eq(r.Hour, 1, "hour clamps to 1");
        r.Hour = 99;
        Eq(r.Hour, 24, "hour clamps to 24");
        r.HitPointsMax = 0;
        Eq(r.HitPointsMax, 1, "max hit points cannot be zero");
        r.HitPointsMax = 50;
        r.HitPoints = 200;
        Eq(r.HitPoints, 50, "current hit points clamp to the maximum");
        r.Race = 99;
        Eq(r.Race, RaceBook.Races.Count - 1, "race clamps into the table");
        r.Alignment = 99;
        Eq(r.Alignment, AlignmentBook.Alignments.Count - 1, "alignment clamps into the table");
        r.Gender = 7;
        Eq(r.Gender, 1, "gender clamps to 1");
        r.SetThiefSkill(0, 500);
        Eq(r.ThiefSkills[0], 99, "thief skills clamp to 99");
        // Out of range must be a no-op. Asserting ThiefSkills.Count would be meaningless — it is a
        // fresh array of a compile-time length every call — and offset 0x32 + 99 is still *inside* the
        // record, so an unguarded write would silently corrupt a state byte. Compare the whole record.
        Eq(r.ThiefSkills.Count, CharacterFormat.ThiefSkillCount, "thief-skill count");
        var beforeBadSkill = r.Bytes.ToArray();
        r.SetThiefSkill(99, 5);
        Check(r.Bytes.SequenceEqual(beforeBadSkill),
              "SetThiefSkill with an out-of-range index changes nothing");
        r.SetThiefSkill(-1, 5);
        Check(r.Bytes.SequenceEqual(beforeBadSkill),
              "SetThiefSkill with a negative index changes nothing");

        Group("bulk actions");
        var rec2 = MakeRecord();
        var r2 = new CharacterRecord(rec2);
        r2.HitPoints = 1;
        r2.HealFully();
        Eq(r2.HitPoints, r2.HitPointsMax, "HealFully");
        r2.MaxAbilities();
        foreach (var (name, got) in new (string, int)[]
                 {
                     ("strength", r2.Strength), ("intelligence", r2.Intelligence),
                     ("wisdom", r2.Wisdom), ("dexterity", r2.Dexterity),
                     ("constitution", r2.Constitution), ("charisma", r2.Charisma),
                 })
            Eq(got, CharacterFormat.MaxAbility, $"MaxAbilities sets {name}");
        r2.MaxConsumables();
        Eq(r2.KnockRings, 99, "MaxConsumables sets rings");
        Eq(r2.HealingPotions, 99, "MaxConsumables sets potions");

        // Levels are set only for the classes the character actually has.
        var rec3 = MakeRecord();
        var r3 = new CharacterRecord(rec3);
        r3.ClassMask = ClassBook.MaskFighter | ClassBook.MaskThief;   // FTR/TH
        r3.SetLevelsForOwnClasses(9);
        Eq(r3.FighterLevel, 9, "fighter level set");
        Eq(r3.ThiefLevel, 9, "thief level set");
        Eq(r3.ClericLevel, 0, "cleric level untouched");
        Eq(r3.MagicUserLevel, 0, "magic-user level untouched");
        Eq(r3.DisplayLevel, 9, "display level for a multi-class");

        Check(r3.Summary().Contains("FTR/TH"), "the summary names the class");
        Check(r3.Summary().Contains("Christopher"), "the summary names the character");
    }

    // --- 7. flush ranges ------------------------------------------------------

    private static void FlushRanges()
    {
        Group("flush ranges");
        var rec = MakeRecord();
        var flushes = new List<(int Off, int Len)>();
        var r = new CharacterRecord(rec, 0, (o, l) => flushes.Add((o, l)));

        void Only(Action act, int off, int len, string what)
        {
            flushes.Clear();
            act();
            Eq(flushes.Count, 1, $"{what} flushes once");
            if (flushes.Count == 1)
            {
                Eq(flushes[0].Off, off, $"{what} flush offset");
                Eq(flushes[0].Len, len, $"{what} flush length");
            }
        }

        // Every single-field setter, so the documented flush contract is actually covered rather than
        // sampled. A wrong offset here writes into the adjacent clock / per-hour-timer bytes, which is
        // the whole reason the contract exists.
        var singleFieldSetters = new (Action Act, int Off, int Len, string What)[]
        {
            (() => r.Strength = 17, CharacterFormat.OffStrength, 1, "strength"),
            (() => r.StrengthPercentile = 55, CharacterFormat.OffStrengthPercentile, 1, "strength percentile"),
            (() => r.Intelligence = 12, CharacterFormat.OffIntelligence, 1, "intelligence"),
            (() => r.Wisdom = 12, CharacterFormat.OffWisdom, 1, "wisdom"),
            (() => r.Dexterity = 12, CharacterFormat.OffDexterity, 1, "dexterity"),
            (() => r.Constitution = 12, CharacterFormat.OffConstitution, 1, "constitution"),
            (() => r.Charisma = 12, CharacterFormat.OffCharisma, 1, "charisma"),
            (() => r.Race = 2, CharacterFormat.OffRace, 1, "race"),
            (() => r.Gender = 1, CharacterFormat.OffGender, 1, "gender"),
            (() => r.Alignment = 5, CharacterFormat.OffAlignment, 1, "alignment"),
            (() => r.HitPoints = 7, CharacterFormat.OffHitPoints, 1, "hit points"),
            (() => r.ClericLevel = 6, CharacterFormat.OffLevelCleric, 1, "cleric level"),
            (() => r.MagicUserLevel = 6, CharacterFormat.OffLevelMagicUser, 1, "magic-user level"),
            (() => r.FighterLevel = 6, CharacterFormat.OffLevelFighter, 1, "fighter level"),
            (() => r.ThiefLevel = 6, CharacterFormat.OffLevelThief, 1, "thief level"),
            (() => r.KnockRings = 7, CharacterFormat.OffKnockRings, 1, "knock rings"),
            (() => r.HealingPotions = 7, CharacterFormat.OffHealingPotions, 1, "healing potions"),
            (() => r.ArcheryLevel = 4, CharacterFormat.OffArcheryLevel, 1, "archery level"),
            (() => r.Hour = 9, CharacterFormat.OffHour, 1, "hour"),
            (() => r.HealCountdown = 12, CharacterFormat.OffHealCountdown, 1, "heal countdown"),
            (() => r.SetThiefSkill(1, 44), CharacterFormat.OffThiefSkills + 1, 1, "thief skill 1"),
            (() => r.Age = 44, CharacterFormat.OffAge, 2, "age"),
            (() => r.Day = 3, CharacterFormat.OffDay, 2, "day"),
            (() => r.Gold = 4242, CharacterFormat.OffGold, 4, "gold"),
            (() => r.Experience = 4242, CharacterFormat.OffExperience, 4, "experience"),
            (() => r.Name = "Tim", CharacterFormat.OffName, CharacterFormat.NameFieldLength, "name"),
        };
        foreach (var (act, off, len, what) in singleFieldSetters) Only(act, off, len, what);

        // Every mutable property must be covered above. Reflect over CharacterRecord rather than
        // comparing two hand-written lists: a list-against-list check only fails if someone edits one
        // and not the other, so adding a brand-new setter would slip through it entirely — which is
        // the same self-satisfying shape this harness exists to avoid.
        var covered = new HashSet<string>(
            singleFieldSetters.Select(t => Canonical(t.What)), StringComparer.OrdinalIgnoreCase);

        // The two setters that deliberately flush more than one range; each is asserted on its own.
        var multiRange = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            nameof(CharacterRecord.HitPointsMax), nameof(CharacterRecord.ClassMask),
        };

        foreach (var prop in typeof(CharacterRecord).GetProperties(
                     BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite || prop.SetMethod is not { IsPublic: true }) continue;
            if (multiRange.Contains(prop.Name)) continue;
            Check(covered.Contains(Canonical(prop.Name)),
                  $"CharacterRecord.{prop.Name} has its flush range pinned");
        }
        foreach (var name in multiRange)
            Check(typeof(CharacterRecord).GetProperty(name)?.CanWrite == true,
                  $"the multi-range exemption '{name}' still names a real settable property");
        Check(covered.Contains(Canonical("thief skill 1")), "SetThiefSkill's flush range is pinned");

        // HitPointsMax flushes its own byte, plus the current-HP byte when it has to clamp it down.
        flushes.Clear();
        r.HitPointsMax = 200;
        r.HitPoints = 200;
        flushes.Clear();
        r.HitPointsMax = 150;   // above current? no - current is 200, so this must clamp
        Check(flushes.Any(f => f.Off == CharacterFormat.OffHitPointsMax && f.Len == 1),
              "lowering max flushes the max byte");
        Check(flushes.Any(f => f.Off == CharacterFormat.OffHitPoints && f.Len == 1),
              "lowering max below current also flushes the current byte");
        Eq(r.HitPoints, 150, "and clamps current down to the new max");
        flushes.Clear();
        r.HitPointsMax = 200;   // raising must not touch current
        Eq(flushes.Count, 1, "raising max flushes only the max byte");
        Eq(r.HitPoints, 150, "and leaves current alone");

        // The mask setter deliberately writes two separate bytes.
        flushes.Clear();
        r.ClassMask = ClassBook.MaskThief;
        Eq(flushes.Count, 2, "setting the class mask flushes mask and index");
        Check(flushes.Any(f => f.Off == CharacterFormat.OffClassMask && f.Len == 1),
              "the mask byte is flushed");
        Check(flushes.Any(f => f.Off == CharacterFormat.OffClassIndex && f.Len == 1),
              "the index byte is flushed");

        // Every flush must stay inside the record.
        flushes.Clear();
        r.MaxAbilities();
        r.MaxConsumables();
        r.HealFully();
        foreach (var (off, len) in flushes)
            Check(off >= 0 && off + len <= CharacterFormat.RecordLength,
                  $"flush ({off},{len}) stays inside the record");

        // A detached record must not throw when there is no flush delegate.
        var detached = new CharacterRecord(MakeRecord());
        detached.Gold = 1;
        Eq(detached.Gold, 1u, "a detached record accepts writes");

        // Bad construction must be rejected loudly, not silently mis-indexed.
        try
        {
            _ = new CharacterRecord(new byte[10]);
            Check(false, "a short buffer is rejected");
        }
        catch (ArgumentException) { Check(true, "a short buffer is rejected"); }
        try
        {
            _ = new CharacterRecord(new byte[CharacterFormat.RecordLength], 4);
            Check(false, "a bad start offset is rejected");
        }
        catch (ArgumentException) { Check(true, "a bad start offset is rejected"); }

        // A record at a non-zero offset must read the right bytes.
        var big = new byte[CharacterFormat.RecordLength * 2];
        Array.Copy(MakeRecord(), 0, big, CharacterFormat.RecordLength, CharacterFormat.RecordLength);
        var off2 = new CharacterRecord(big, CharacterFormat.RecordLength);
        Eq(off2.Name, "Christopher", "a record at an offset reads correctly");
    }

    private static void NameField()
    {
        Group("name field");
        var rec = MakeRecord();
        var r = new CharacterRecord(rec);

        // The whole field is rewritten, so no tail of the old name survives — this is what stops the
        // game inventing a filename like ZZTOPOPH.HIL from leftover bytes.
        r.Name = "ZZTOP";
        Eq(r.Name, "ZZTOP", "short name reads back");
        var field = rec.AsSpan(CharacterFormat.OffName, CharacterFormat.NameFieldLength);
        Eq(field[5], (byte)0, "a terminator follows the name");
        for (int i = 6; i < CharacterFormat.NameFieldLength - 1; i++)
            Eq(field[i], (byte)' ', $"byte {i} of the field is a space, not old text");
        Eq(field[CharacterFormat.NameFieldLength - 1], (byte)0, "the field's last byte is NUL");
        Check(!Encoding.ASCII.GetString(rec, CharacterFormat.OffName,
                                        CharacterFormat.NameFieldLength).Contains("opher"),
              "no tail of the previous name survives");

        // Truncation at 15, and non-ASCII replaced rather than written raw.
        r.Name = "ABCDEFGHIJKLMNOPQRSTU";
        Eq(r.Name, "ABCDEFGHIJKLMNO", "a long name truncates to 15");
        r.Name = "Bj\u00f6rn";
        Eq(r.Name, "Bj?rn", "a non-ASCII character is replaced");
        r.Name = "";
        Eq(r.Name, "", "an empty name is allowed");
        r.Name = "  spaced  ";
        Eq(r.Name, "spaced", "surrounding whitespace is trimmed");

        // The filename the game would choose.
        Eq(CharacterFile.SuggestFileName("Christopher"), "CHRISTO.HIL".Replace("CHRISTO", "CHRISTOP"),
           "8-character stem");
        Eq(CharacterFile.SuggestFileName("Tim"), "TIM.HIL", "short stem");
        Eq(CharacterFile.SuggestFileName("Magic User"), "MAGICUSE.HIL", "spaces are dropped");
        Eq(CharacterFile.SuggestFileName(""), "CHARACTR.HIL", "an empty name gets a fallback");
    }


    /// <summary>
    /// Normalises a setter label or property name for comparison — "hit points" and "HitPoints" are
    /// the same field. Lets the coverage check reflect over real properties while the table stays
    /// readable.
    /// </summary>
    private static string Canonical(string name) =>
        new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    // --- 8. lock picks --------------------------------------------------------

    private static void LockPicks()
    {
        Group("lock picks");
        Eq(LockPickSet.SlotCount, 12, "twelve slots");
        Eq(LockPickSet.SlotLength, 5, "five bytes per slot");
        Eq(LockPickSet.BlockLength, 60, "sixty-byte block");
        Eq(LockPickSet.ShapePairDelta, 20, "the shape pairs differ by 20");

        // A slot laid out like the shipped thieves' data: {a, b, b+20, a-20, state}.
        var rec = MakeRecord();
        void Put(int slot, byte a, byte b, byte state)
        {
            int at = CharacterFormat.OffLockPicks + slot * LockPickSet.SlotLength;
            rec[at] = a;
            rec[at + 1] = b;
            rec[at + 2] = (byte)(b + LockPickSet.ShapePairDelta);
            rec[at + 3] = (byte)(a - LockPickSet.ShapePairDelta);
            rec[at + 4] = state;
        }
        // These are the first two slots of TIM.HIL, verbatim.
        Put(0, 0x37, 0x1A, 0x00);
        Put(1, 0x38, 0x1A, 0x00);
        Put(2, 0x29, 0x27, 0x03);
        Put(3, 0x2D, 0x20, 0x02);

        var picks = LockPickSet.Read(rec);
        Eq(picks.Count, 12, "all twelve slots decode");
        Eq(picks[0].ShapeA, (byte)0x37, "slot 0 shape A");
        Eq(picks[0].ShapeC, (byte)(0x1A + 20), "slot 0 shape C is B+20");
        Eq(picks[0].ShapeD, (byte)(0x37 - 20), "slot 0 shape D is A-20");
        Check(picks[0].HasExpectedGeometry, "slot 0 geometry");
        Check(!picks[0].IsPresent, "slot 0 state 0 means absent");
        Check(picks[2].IsPresent, "slot 2 state 3 means present");
        Eq(LockPickSet.CountPresent(rec), 2, "two usable picks");
        Check(LockPickSet.GeometryLooksRight(rec), "the geometry check passes on good data");

        // Repair sets only slots that have geometry, and never invents shapes.
        var flushes = new List<(int, int)>();
        int changed = LockPickSet.RepairAll(rec, (o, l) => flushes.Add((o, l)));
        Eq(changed, 3, "three slots repaired (slot 2 was already good)");
        Eq(LockPickSet.CountPresent(rec), 4, "four usable picks after repair");
        Eq(flushes.Count, 3, "one flush per repaired slot");
        foreach (var (off, len) in flushes)
        {
            Eq(len, 1, "a repair flushes one byte");
            int rel = off - CharacterFormat.OffLockPicks;
            Eq(rel % LockPickSet.SlotLength, LockPickSet.StateOffset,
               "a repair writes only the state byte");
        }
        // Empty slots must stay empty — repairing must not create picks from nothing.
        for (int slot = 4; slot < LockPickSet.SlotCount; slot++)
        {
            int at = CharacterFormat.OffLockPicks + slot * LockPickSet.SlotLength;
            Eq(rec[at + LockPickSet.StateOffset], (byte)0, $"slot {slot} stays empty");
        }
        Eq(LockPickSet.RepairAll(rec), 0, "repairing again changes nothing");

        // Broken geometry is detected.
        var bad = MakeRecord();
        int b0 = CharacterFormat.OffLockPicks;
        bad[b0] = 0x37; bad[b0 + 1] = 0x1A; bad[b0 + 2] = 0x99; bad[b0 + 3] = 0x01; bad[b0 + 4] = 3;
        Check(!LockPickSet.GeometryLooksRight(bad), "bad geometry is rejected");

        // A short buffer must be handled, not crash.
        Eq(LockPickSet.Read(new byte[4]).Count, 0, "a short buffer yields no picks");
        Eq(LockPickSet.RepairAll(new byte[4]), 0, "repair on a short buffer is a no-op");
    }

    // --- 9. file round-trip ---------------------------------------------------

    private static void FileRoundTrip()
    {
        Group("character file round-trip");
        string dir = Path.Combine(Path.GetTempPath(), "hillsfar-formatcheck-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            // A file written from a record must read back byte-identically: the format is a raw dump,
            // so anything else would mean the trainer is inventing content.
            var original = MakeRecord();
            string path = Path.Combine(dir, "TESTER.HIL");
            CharacterFile.FromRecord(path, original).SaveAs(path);
            var bytes = File.ReadAllBytes(path);
            Eq(bytes.Length, CharacterFormat.RecordLength, "the written file is 188 bytes");
            Check(bytes.AsSpan().SequenceEqual(original), "the file matches the record byte-for-byte");

            var loaded = CharacterFile.Load(path);
            Eq(loaded.Record.Name, "Christopher", "the loaded file decodes");
            Check(loaded.LooksValid, "the loaded file passes the shape check");

            // Editing one field must leave every other byte alone.
            loaded.Record.Gold = 999999;
            loaded.Save();
            var after = File.ReadAllBytes(path);
            Eq(BinaryPrimitives.ReadUInt32LittleEndian(after.AsSpan(CharacterFormat.OffGold, 4)),
               999999u, "the edit landed");
            // Nothing outside the gold field may move. (Not every one of its four bytes need
            // differ — 590 and 999999 share a zero high byte — so assert the range, not a count.)
            for (int i = 0; i < CharacterFormat.RecordLength; i++)
            {
                bool inGold = i >= CharacterFormat.OffGold && i < CharacterFormat.OffGold + 4;
                if (!inGold)
                    Check(after[i] == original[i], $"byte 0x{i:X2} outside the gold field is untouched");
            }
            Check(!after.AsSpan(CharacterFormat.OffGold, 4)
                        .SequenceEqual(original.AsSpan(CharacterFormat.OffGold, 4)),
                  "the gold field itself changed");
            Check(File.Exists(path + ".bak"), "a one-shot backup was taken");
            var backup = File.ReadAllBytes(path + ".bak");
            Check(backup.AsSpan().SequenceEqual(original), "the backup holds the pre-edit bytes");

            // The backup must not be overwritten on a later save.
            loaded.Record.Gold = 1;
            loaded.Save();
            Check(File.ReadAllBytes(path + ".bak").AsSpan().SequenceEqual(original),
                  "the backup is one-shot");

            // A wrong-length file must be refused rather than mis-parsed.
            string badPath = Path.Combine(dir, "SHORT.HIL");
            File.WriteAllBytes(badPath, new byte[100]);
            try
            {
                CharacterFile.Load(badPath);
                Check(false, "a wrong-length file is refused");
            }
            catch (InvalidDataException) { Check(true, "a wrong-length file is refused"); }

            // LoadDirectory must skip the rubbish and keep the good file.
            var found = CharacterFile.LoadDirectory(dir);
            Eq(found.Count, 1, "LoadDirectory keeps only plausible character files");
            Eq(found[0].FileName, "TESTER.HIL", "and finds the right one");
            Eq(CharacterFile.LoadDirectory(Path.Combine(dir, "nope")).Count, 0,
               "a missing directory yields nothing");

            try
            {
                CharacterFile.FromRecord(path, new byte[10]);
                Check(false, "FromRecord rejects a short record");
            }
            catch (ArgumentException) { Check(true, "FromRecord rejects a short record"); }
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* best effort */ }
        }
    }

    // --- 10. the locator ------------------------------------------------------

    /// <summary>
    /// A synthetic address space: one region, with the anchors written at their true offsets from a
    /// chosen <c>DGROUP</c> base, and an optional unreadable page to exercise the salvage path.
    /// </summary>
    private sealed class FakeMemory : IMemorySource
    {
        private readonly byte[] _data;
        private readonly nuint _base;
        private readonly HashSet<int> _deadPages = new();

        public FakeMemory(int size, nuint baseAddress)
        {
            _data = new byte[size];
            _base = baseAddress;
        }

        public void Poison(int pageIndex) => _deadPages.Add(pageIndex);

        public void Write(int offset, ReadOnlySpan<byte> bytes) => bytes.CopyTo(_data.AsSpan(offset));

        public int DgroupOffset { get; set; }

        public void PlaceGame(int dgroupOffset, byte[] record, int validators = 4)
        {
            DgroupOffset = dgroupOffset;
            var a = CharacterFormat.PrimaryAnchor;
            Write(dgroupOffset + a.DgroupOffset, a.Bytes);
            for (int i = 0; i < validators && i < CharacterFormat.Validators.Length; i++)
            {
                var v = CharacterFormat.Validators[i];
                Write(dgroupOffset + v.DgroupOffset, v.Bytes);
            }
            Write(dgroupOffset + CharacterFormat.DgroupRecordOffset, record);
        }

        public IEnumerable<MemoryRegion> EnumerateRegions()
        {
            yield return new MemoryRegion(_base, (nuint)_data.Length);
        }

        public int Read(nuint address, byte[] buffer, int count)
        {
            if (address < _base) return 0;
            long off = (long)(address - _base);
            if (off < 0 || off + count > _data.Length) return 0;
            // All-or-nothing, exactly like ProcessMemory: any dead page fails the whole read.
            for (long p = off / 0x1000; p <= (off + count - 1) / 0x1000; p++)
                if (_deadPages.Contains((int)p)) return 0;
            Array.Copy(_data, off, buffer, 0, count);
            return count;
        }

        public byte[] Read(nuint address, int count)
        {
            var buf = new byte[count];
            return Read(address, buf, count) == count ? buf : Array.Empty<byte>();
        }
    }

    private static void LocatorChecks()
    {
        Group("locator");
        const nuint Base = 0x10000000;
        int span = 0x30000;   // enough to hold DGROUP plus every anchor

        // A clean placement is found, with the right address and every validator matched.
        var mem = new FakeMemory(span, Base);
        mem.PlaceGame(0x1000, MakeRecord());
        var found = GameLocator.Locate(mem);
        Check(found.Found, "a clean placement is found");
        Eq((ulong)found.DgroupAddress, (ulong)(Base + 0x1000), "the DGROUP address is right");
        Eq(found.ValidatorsMatched, 4, "all four validators matched");
        Eq((ulong)found.RecordAddress,
           (ulong)(Base + 0x1000 + CharacterFormat.DgroupRecordOffset), "the record address");
        Eq(new CharacterRecord(found.Record).Name, "Christopher", "the record came back");

        // Exactly MinValidators is accepted; one fewer is not.
        for (int n = 0; n <= CharacterFormat.Validators.Length; n++)
        {
            var m = new FakeMemory(span, Base);
            m.PlaceGame(0x1000, MakeRecord(), n);
            var r = GameLocator.Locate(m);
            Check(r.Found == (n >= CharacterFormat.MinValidators),
                  $"{n} validator(s): found should be {n >= CharacterFormat.MinValidators}");
        }

        // The anchors are there but the record is not plausible: that must be reported distinctly,
        // because the advice for the user is completely different.
        var noChar = new FakeMemory(span, Base);
        noChar.PlaceGame(0x1000, new byte[CharacterFormat.RecordLength]);
        var rej = GameLocator.Locate(noChar);
        Check(!rej.Found, "an implausible record is rejected");
        Check(rej.AnchorsMatchedButRecordDidNot, "and reported as 'game found, no character'");
        Eq((ulong)rej.RejectedAddress, (ulong)(Base + 0x1000), "the rejected address is reported");

        // Nothing at all.
        var empty = new FakeMemory(span, Base);
        var none = GameLocator.Locate(empty);
        Check(!none.Found, "an empty process finds nothing");
        Check(!none.AnchorsMatchedButRecordDidNot, "and does not claim the game was found");
        Eq(none.Method, "not found", "the method text says so");

        // The anchor split across every offset near a 1 MiB chunk seam must still be found. The
        // scanner reads with a needle-sized overlap precisely for this.
        var anchorLen = CharacterFormat.PrimaryAnchor.Bytes.Length;
        for (int delta = -anchorLen - 2; delta <= 2; delta++)
        {
            int seam = (1 << 20) + delta;
            int dgroup = seam - CharacterFormat.PrimaryAnchor.DgroupOffset;
            if (dgroup < 0) continue;
            int need = dgroup + 0xB200;
            var m = new FakeMemory(need + 0x2000, Base);
            m.PlaceGame(dgroup, MakeRecord());
            var r = GameLocator.Locate(m);
            Check(r.Found, $"anchor at seam offset {delta:+0;-0;0} is found");
            if (r.Found) Eq((ulong)r.DgroupAddress, (ulong)(Base + (nuint)dgroup),
                            $"seam offset {delta:+0;-0;0} address");
        }

        // An unreadable page must not abort the sweep — the rest of the region is still scanned.
        var poisoned = new FakeMemory(span, Base);
        poisoned.PlaceGame(0x8000, MakeRecord());
        poisoned.Poison(0);   // the very first page fails, forcing the page-by-page salvage
        var salvaged = GameLocator.Locate(poisoned);
        Check(salvaged.Found, "a poisoned page is salvaged around");

        // A record whose own page is unreadable must not be reported as 'no character'.
        var halfDead = new FakeMemory(span, Base);
        halfDead.PlaceGame(0x1000, MakeRecord());
        halfDead.Poison((0x1000 + CharacterFormat.DgroupRecordOffset) / 0x1000);
        var hd = GameLocator.Locate(halfDead);
        Check(!hd.Found, "an unreadable record window is not a hit");
        Check(!hd.AnchorsMatchedButRecordDidNot,
              "an unreadable window is not reported as 'no character loaded'");

        // No underflow when the anchor sits near address zero.
        var low = new FakeMemory(span, 0x100);
        low.PlaceGame(0x1000, MakeRecord());
        Check(GameLocator.Locate(low).Found, "a low base address still works");

        // Cancellation is honoured.
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        try
        {
            GameLocator.Locate(mem, cts.Token);
            Check(false, "cancellation is honoured");
        }
        catch (OperationCanceledException) { Check(true, "cancellation is honoured"); }

        // Reread and ReadTextTable.
        var buf = new byte[CharacterFormat.RecordLength];
        Check(GameLocator.Reread(mem, Base + 0x1000, buf), "Reread succeeds");
        Eq(new CharacterRecord(buf).Name, "Christopher", "Reread returns the record");
        Check(!GameLocator.Reread(mem, Base + 0x1000, new byte[4]), "Reread refuses a short buffer");
        Check(!GameLocator.Reread(mem, 0, buf), "Reread refuses a null DGROUP");

        var withTable = new FakeMemory(span, Base);
        withTable.PlaceGame(0x1000, MakeRecord());
        withTable.Write(0x1000 + TextCodec.DgroupTableOffset, TextCodec.ShippedTable);
        var table = GameLocator.ReadTextTable(withTable, Base + 0x1000);
        Check(table != null && table.AsSpan().SequenceEqual(TextCodec.ShippedTable),
              "the live text table is read back");

        // The shape check must reject the things it is there to reject.
        Group("record shape check");
        Check(!CharacterFormat.LooksLikeRecord(new byte[10]), "a short window is rejected");
        Check(!CharacterFormat.LooksLikeRecord(new byte[CharacterFormat.RecordLength]),
              "an all-zero window is rejected");
        var probe = MakeRecord();
        void Broken(int off, byte value, string what)
        {
            var copy = (byte[])probe.Clone();
            copy[off] = value;
            Check(!CharacterFormat.LooksLikeRecord(copy), what);
        }
        Broken(CharacterFormat.OffName, 0x01, "a non-letter first name byte is rejected");
        Broken(CharacterFormat.OffName + 2, 0x07, "a control byte in the name is rejected");
        Broken(CharacterFormat.OffName + CharacterFormat.NameFieldLength - 1, (byte)'X',
               "a missing final NUL is rejected");
        Broken(CharacterFormat.OffStrength, 99, "an out-of-range ability is rejected");
        Broken(CharacterFormat.OffStrengthPercentile, 200, "an out-of-range percentile is rejected");
        Broken(CharacterFormat.OffHitPointsMax, 0, "zero max hit points is rejected");
        Broken(CharacterFormat.OffHour, 0, "hour 0 is rejected");
        Broken(CharacterFormat.OffHour, 25, "hour 25 is rejected");
        Broken(CharacterFormat.OffRace, 9, "an out-of-range race is rejected");
        Broken(CharacterFormat.OffGender, 5, "an out-of-range gender is rejected");
        Broken(CharacterFormat.OffAlignment, 12, "an out-of-range alignment is rejected");
        Broken(CharacterFormat.OffClassMask, 0x99, "an illegal class mask is rejected");
        Broken(CharacterFormat.OffClassMask, 0x12, "mismatched mask nibbles are rejected");

        var hpOver = (byte[])probe.Clone();
        hpOver[CharacterFormat.OffHitPoints] = 200;
        hpOver[CharacterFormat.OffHitPointsMax] = 42;
        Check(!CharacterFormat.LooksLikeRecord(hpOver),
              "current hit points above the maximum is rejected");

        // Every legal class mask must pass, so the check cannot reject a real multi-class character.
        foreach (var c in ClassBook.Classes)
        {
            var copy = (byte[])probe.Clone();
            copy[CharacterFormat.OffClassMask] = ClassBook.PackMask(c.Mask);
            Check(CharacterFormat.LooksLikeRecord(copy), $"a {c.Name} record passes the shape check");
        }
    }

    // --- 11. view-models ------------------------------------------------------

    private sealed class FakeHost : ICharacterHost
    {
        public readonly List<(int Off, byte[] Bytes)> Writes = new();
        public string LastStatus = "";
        public bool FailWrites;

        public readonly List<nuint> Bases = new();

        public bool WriteBytes(nuint dgroupBase, int dgroupOffset, byte[] bytes)
        {
            if (FailWrites) return false;
            Bases.Add(dgroupBase);
            Writes.Add((dgroupOffset, bytes.ToArray()));
            return true;
        }

        public void ReportStatus(string message) => LastStatus = message;
    }

    private static void ViewModelChecks()
    {
        Group("character view-model");
        var host = new FakeHost();
        var record = MakeRecord();
        var found = new LocateResult(0x1000, record, "test", 4);
        var vm = new CharacterViewModel(host, found);

        Eq(vm.Name, "Christopher", "the view-model reads the name");
        Eq(vm.Hour, 15, "the view-model reads the hour");
        Eq(vm.HourText, "3 pm", "the view-model formats the hour");
        Check(vm.LiveSummary.Contains("Christopher"), "the live mirror summarises");
        Eq(vm.LockPicks.Count, LockPickSet.SlotCount, "the pick list is populated");

        // An edit writes through at the right DGROUP offset, with the right bytes.
        host.Writes.Clear();
        vm.Gold = 12345;
        Eq(host.Writes.Count, 1, "setting gold writes once");
        Eq(host.Writes[0].Off, CharacterFormat.DgroupRecordOffset + CharacterFormat.OffGold,
           "gold writes at the right DGROUP offset");
        Eq(BinaryPrimitives.ReadUInt32LittleEndian(host.Writes[0].Bytes), 12345u,
           "gold writes the right bytes");

        host.Writes.Clear();
        vm.KnockRings = 50;
        Eq(host.Writes.Count, 1, "setting rings writes once");
        Eq(host.Writes[0].Bytes.Length, 1, "rings write one byte");
        Eq(host.Writes[0].Off, CharacterFormat.DgroupRecordOffset + CharacterFormat.OffKnockRings,
           "rings write at the right offset");

        // Clamping happens before the write, so nothing out of range reaches the game.
        host.Writes.Clear();
        vm.KnockRings = 5000;
        Eq(vm.KnockRings, 99, "the view-model clamps rings");
        Eq(host.Writes[0].Bytes[0], (byte)99, "the clamped value is what gets written");

        // A failed write is reported rather than swallowed.
        host.FailWrites = true;
        host.LastStatus = "";
        vm.Gold = 7;
        Check(host.LastStatus.Contains("failed"), "a failed write is reported");
        host.FailWrites = false;

        // Bulk actions.
        vm.MaxAbilitiesCommand.Execute(null);
        Eq(vm.Strength, CharacterFormat.MaxAbility, "Max abilities");
        vm.MaxConsumablesCommand.Execute(null);
        Eq(vm.HealingPotions, 99, "Max consumables");
        vm.MaxArcheryCommand.Execute(null);
        Eq(vm.ArcheryLevel, 15, "Max archery");
        vm.HitPointsMax = 60;
        vm.HitPoints = 1;
        vm.HealCommand.Execute(null);
        Eq(vm.HitPoints, 60, "Heal to full");
        int before = vm.FighterLevel;
        vm.LevelUpCommand.Execute(null);
        Eq(vm.FighterLevel, before + 1, "Level up raises the fighter level");
        Eq(vm.ClericLevel, 0, "Level up leaves classes the character lacks alone");

        // The three repair outcomes must be distinguishable. The fixture has no pick data at all.
        host.LastStatus = "";
        vm.RepairPicksCommand.Execute(null);
        Check(host.LastStatus.Contains("no picks to repair"),
              "repairing a character with no picks says exactly that");
        Check(!host.LastStatus.Contains("already in good condition"),
              "and does not claim they are all fine");

        // Changing the class writes both bytes and keeps them consistent.
        host.Writes.Clear();
        vm.ClassChoiceIndex = 0;   // Thief
        Eq(host.Writes.Count, 2, "changing class writes mask and index");
        Eq(vm.ClassChoiceIndex, 0, "the class selection reads back");

        // OpenNow must agree with the location table at the live hour. Asserting only Length > 0 would
        // pass on the "(nothing open)" and "(clock not readable)" fallbacks too.
        vm.OnPolled();
        var expectedOpen = string.Join(", ",
            LocationBook.OpenAt(new CharacterRecord(vm.LiveBuffer).Hour).Select(l => l.Name));
        Eq(vm.OpenNow, expectedOpen, "OpenNow lists exactly what is open at the live hour");
        Check(vm.OpenNow.Contains("Arena"), "and the arena is open at the fixture's 3 pm");

        // Freeze: the value is re-applied only when the game has moved it.
        Group("freeze behaviour");
        var host2 = new FakeHost();
        var vm2 = new CharacterViewModel(host2, new LocateResult(0x1000, MakeRecord(), "test", 4));
        var gold = vm2.Freezes.First(f => f.Label.StartsWith("Gold"));
        gold.Value = 4242;
        gold.IsFrozen = true;

        host2.Writes.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(
            vm2.LiveBuffer.AsSpan(CharacterFormat.OffGold, 4), 4242);
        vm2.OnPolled();
        Eq(host2.Writes.Count, 0, "a frozen value already correct is not re-written");

        // Now let the "game" change it — the freeze must put it back.
        BinaryPrimitives.WriteUInt32LittleEndian(
            vm2.LiveBuffer.AsSpan(CharacterFormat.OffGold, 4), 11);
        vm2.OnPolled();
        Eq(host2.Writes.Count, 1, "a frozen value the game moved is re-written");
        Eq(BinaryPrimitives.ReadUInt32LittleEndian(host2.Writes[0].Bytes), 4242u,
           "the pinned value is written back");

        // Unfreezing stops the writes.
        gold.IsFrozen = false;
        host2.Writes.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(
            vm2.LiveBuffer.AsSpan(CharacterFormat.OffGold, 4), 5);
        vm2.OnPolled();
        Eq(host2.Writes.Count, 0, "an unfrozen value is left alone");

        // Reload copies the game's bytes over the editable copy without writing back.
        var vm3 = new CharacterViewModel(host2, new LocateResult(0x1000, MakeRecord(), "test", 4));
        vm3.LiveBuffer[CharacterFormat.OffStrength] = 7;
        host2.Writes.Clear();
        vm3.ReloadCommand.Execute(null);
        Eq(vm3.Strength, 7, "Reload picks up the game's value");
        Eq(host2.Writes.Count, 0, "Reload does not write back to the game");

        // SnapshotEdited returns a copy, not the live buffer.
        var snap = vm3.SnapshotEdited();
        Eq(snap.Length, CharacterFormat.RecordLength, "a snapshot is a full record");
        snap[0] = 0xEE;
        vm3.Gold = 1;
        Check(snap[0] == 0xEE, "the snapshot is independent of later edits");

        // A failed locate must not produce a view-model at all.
        try
        {
            _ = new CharacterViewModel(host, LocateResult.None);
            Check(false, "a failed locate is refused");
        }
        catch (ArgumentException) { Check(true, "a failed locate is refused"); }

        Group("reference view-model");
        var rvm = new ReferenceViewModel();
        Eq(rvm.Locations.Count, 18, "the reference lists all eighteen locations");
        rvm.Hour = 20;
        Eq(rvm.HourText, "8 pm", "the reference formats the hour");
        var pubRow = rvm.Locations.First(l => l.Name == "Pub");
        Check(pubRow.IsOpen, "the pub shows open at 8 pm");
        var bankRow = rvm.Locations.First(l => l.Name == "Bank");
        Check(!bankRow.IsOpen, "the bank shows shut at 8 pm");
        rvm.Hour = 9;
        Check(!pubRow.IsOpen && bankRow.IsOpen, "the flags flip at 9 am");
        rvm.Hour = 99;
        Eq(rvm.Hour, 24, "the reference hour clamps");
        rvm.Hour = 0;
        Eq(rvm.Hour, 1, "the reference hour clamps low");
        Check(rvm.ClockNote.Contains("122"), "the clock note quotes the real figure");

        Group("file editor view-model");
        string dir = Path.Combine(Path.GetTempPath(), "hillsfar-vm-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "EDITME.HIL");
            CharacterFile.FromRecord(path, MakeRecord()).SaveAs(path);
            string status = "";
            var fvm = new FileEditorViewModel(m => status = m);
            fvm.LoadFolder(dir);
            Eq(fvm.Files.Count, 1, "the editor finds the file");
            Check(fvm.HasSelection, "the editor selects it");
            Check(!fvm.IsDirty, "a freshly loaded file is clean");
            fvm.Gold = 777;
            Check(fvm.IsDirty, "an edit marks it dirty");
            fvm.SaveCommand.Execute(null);
            Check(!fvm.IsDirty, "saving clears the dirty flag");
            var reread = CharacterFile.Load(path);
            Eq(reread.Record.Gold, 777u, "the edit reached disk");
            fvm.Gold = 5;
            fvm.RevertCommand.Execute(null);
            Eq(fvm.Gold, 777u, "revert restores the saved value");
            fvm.LoadFolder(Path.Combine(dir, "missing"));
            Check(status.Contains("not found"), "a missing folder is reported");
            Eq(fvm.Files.Count, 0, "and clears the list");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* best effort */ }
        }
    }

    private static void FreezeEntryChecks()
    {
        Group("freeze entry ranges");

        // Values clamp into the target's range rather than being refused, so a ticked checkbox always
        // pins something the game accepts. This is the one write path that does not go through
        // CharacterRecord's setters, so it has to enforce the limits itself.
        var hp = new FreezeEntry(new FreezeTarget("hp", 0, 1, 1, 255), 42);
        Eq(hp.Value, 42L, "a freeze is seeded with the character's own value");
        hp.Value = 0;
        Eq(hp.Value, 1L, "hit points clamp up to the minimum of 1 — a freeze must never kill");
        hp.Value = 999;
        Eq(hp.Value, 255L, "and down to the byte maximum");
        Eq(hp.Bytes?.Length, 1, "a byte field yields one byte");
        Eq(hp.Bytes?[0], (byte)255, "and the clamped value is what would be written");

        var rings = new FreezeEntry(new FreezeTarget("rings", 0, 1, 0, CharacterFormat.MaxConsumable), 0);
        rings.Value = 250;
        Eq(rings.Value, (long)CharacterFormat.MaxConsumable,
           "knock rings clamp to the game's cap of 99, not to 255");

        var hour = new FreezeEntry(new FreezeTarget("hour", 0, 1, 1, CharacterFormat.HoursPerDay), 15);
        hour.Value = 0;
        Eq(hour.Value, 1L, "the hour clamps to 1");
        hour.Value = 200;
        Eq(hour.Value, 24L, "and to 24 — never a value LooksLikeRecord would reject");

        var two = new FreezeEntry(new FreezeTarget("w", 0, 2, 0, ushort.MaxValue), 0);
        two.Value = 70000;
        Eq(two.Value, 65535L, "a word field clamps at 2^16-1");
        Eq(two.Bytes?.Length, 2, "a word field yields two bytes");

        var four = new FreezeEntry(new FreezeTarget("d", 0, 4, 0, uint.MaxValue), 0);
        four.Value = 5_000_000_000;
        Eq(four.Value, 4294967295L, "a dword field clamps at 2^32-1");
        Eq(four.Bytes?.Length, 4, "a dword field yields four bytes");

        var bad = new FreezeEntry(new FreezeTarget("x", 0, 3, 0, 100), 1);
        Check(bad.Bytes is null, "an unsupported width yields nothing");

        // A seed outside the range is clamped too, so a corrupt record cannot produce an illegal pin.
        var seeded = new FreezeEntry(new FreezeTarget("hp", 0, 1, 1, 255), 0);
        Eq(seeded.Value, 1L, "an out-of-range seed is clamped");

        // Every freeze target must sit inside the record, use a supported width, and have a sane range.
        foreach (var t in CharacterViewModel.FreezeTargets)
        {
            Check(t.RecordOffset >= 0 && t.RecordOffset + t.Width <= CharacterFormat.RecordLength,
                  $"freeze target '{t.Label}' is inside the record");
            Check(t.Width is 1 or 2 or 4, $"freeze target '{t.Label}' has a supported width");
            Check(t.Min <= t.Max, $"freeze target '{t.Label}' has a non-empty range");
            long widthMax = t.Width switch { 1 => byte.MaxValue, 2 => ushort.MaxValue, _ => uint.MaxValue };
            Check(t.Max <= widthMax, $"freeze target '{t.Label}' fits its width");
            Check(t.Min >= 0, $"freeze target '{t.Label}' has a non-negative minimum");
        }

        // Hit points specifically must never be freezable to zero.
        var hpTarget = CharacterViewModel.FreezeTargets.First(t => t.Label.StartsWith("Hit points"));
        Eq(hpTarget.Min, 1L, "the hit-point freeze cannot be pinned to zero");
    }


    // --- 11b. bindability of everything the XAML puts in an ItemsSource -------

    /// <summary>
    /// Every type bound in a <c>DataGrid</c> / <c>ItemsControl</c> must expose its members as
    /// <b>properties</b>, because that is all WPF's binding engine can resolve.
    ///
    /// <para>This group exists because a <c>ValueTuple</c> list slipped into the Overland tab. Tuples
    /// expose <c>Item1</c>/<c>Item2</c>/... as <i>fields</i>, so every cell rendered blank with nothing
    /// but a binding error in the debug output — invisible to a headless harness that never builds the
    /// XAML. Checking the type model catches it without a UI.</para>
    /// </summary>
    private static void BindabilityChecks()
    {
        Group("bindability of bound item types");

        void Bindable(Type t, params string[] paths)
        {
            var props = TypeDescriptor.GetProperties(t);
            Check(props.Count > 0,
                  $"{t.Name} exposes properties to the binding engine (a tuple would expose none)");
            foreach (var path in paths)
                Check(props.Find(path, ignoreCase: false) != null, $"{t.Name}.{path} is bindable");
        }

        Bindable(typeof(OverlandInfo), nameof(OverlandInfo.Name), nameof(OverlandInfo.ReachedFrom),
                 nameof(OverlandInfo.Why), nameof(OverlandInfo.IsHidden));
        Bindable(typeof(ArenaOpponent), nameof(ArenaOpponent.Name), nameof(ArenaOpponent.Tell),
                 nameof(ArenaOpponent.TellShipped));
        Bindable(typeof(ControlInfo), nameof(ControlInfo.Context), nameof(ControlInfo.Key),
                 nameof(ControlInfo.Action));
        Bindable(typeof(ClassInfo), nameof(ClassInfo.Name));
        Bindable(typeof(MissionGate), nameof(MissionGate.Mission), nameof(MissionGate.Opponent));
        Bindable(typeof(LocationRow), nameof(LocationRow.Name), nameof(LocationRow.Hours),
                 nameof(LocationRow.Note), nameof(LocationRow.IsOpen));
        Bindable(typeof(FreezeEntry), nameof(FreezeEntry.Label), nameof(FreezeEntry.IsFrozen),
                 nameof(FreezeEntry.Value));
        Bindable(typeof(CharacterFile), nameof(CharacterFile.FileName),
                 nameof(CharacterFile.DisplayName));
        Bindable(typeof(ProcessEntry), nameof(ProcessEntry.Display));

        // And no bound collection may be of a tuple element type.
        foreach (var (name, item) in new (string, object)[]
                 {
                     ("LocationBook.Overland", LocationBook.Overland[0]),
                     ("ArenaBook.MissionGates", ArenaBook.MissionGates[0]),
                     ("GameFacts.Controls", GameFacts.Controls[0]),
                     ("ArenaBook.Opponents", ArenaBook.Opponents[0]),
                 })
        {
            var t = item.GetType();
            Check(!t.FullName!.StartsWith("System.ValueTuple", StringComparison.Ordinal),
                  $"{name} items are not ValueTuples");
            Check(TypeDescriptor.GetProperties(t).Count > 0, $"{name} items are bindable");
        }
    }

    // --- 11c. the behaviours added in response to review ---------------------

    private static void RegressionChecks()
    {
        Group("class index / mask agreement");
        // The index must never claim a different class from the mask. Mask 5 (MU/TH) has no slot in the
        // game's 16-byte table, and falling back to the mask value selected IndexToMask[5] == 0x04,
        // i.e. plain Magic-User.
        foreach (var c in ClassBook.Classes)
        {
            byte index = ClassBook.IndexForMask(c.Mask);
            Check(ClassBook.IndexAgreesWithMask(index, c.Mask),
                  $"{c.Name}: index {index} does not contradict mask 0x{c.Mask:X}");
        }
        Eq(ClassBook.IndexForMask(0x5), ClassBook.MagicUserThiefIndex,
           "MU/TH gets the out-of-table index, not Magic-User's");
        Check(ClassBook.MagicUserThiefIndex >= ClassBook.IndexToMask.Count,
              "and that index is outside the table, so it makes no competing claim");
        var muth = MakeRecord();
        var mr = new CharacterRecord(muth);
        mr.ClassMask = 0x5;
        Eq(mr.ClassMask, 0x5, "the MU/TH mask is stored");
        Eq(mr.ClassName, "MU/TH", "and names correctly");
        Check(ClassBook.IndexAgreesWithMask(mr.ClassIndex, mr.ClassMask),
              "the stored index and mask agree for MU/TH");

        Group("hit-point maximum clamps current down");
        var hpRec = MakeRecord();
        var hp = new CharacterRecord(hpRec);
        hp.HitPointsMax = 42;
        hp.HitPoints = 42;
        hp.HitPointsMax = 10;
        Eq(hp.HitPoints, 10, "lowering max brings current down with it");
        Check(CharacterFormat.LooksLikeRecord(hpRec),
              "so the record still passes the shape check the trainer itself uses");

        Group("advance own classes");
        var advRec = MakeRecord();
        var adv = new CharacterRecord(advRec);
        adv.ClassMask = ClassBook.MaskCleric | ClassBook.MaskFighter;   // CL/FTR
        adv.ClericLevel = 9;
        adv.FighterLevel = 3;
        adv.AdvanceOwnClasses();
        Eq(adv.ClericLevel, 10, "each owned class gains exactly one level");
        Eq(adv.FighterLevel, 4, "the lower class is not flattened up to the higher");
        Eq(adv.MagicUserLevel, 0, "unowned classes are untouched");
        Eq(adv.ThiefLevel, 0, "unowned classes are untouched");
        adv.ClericLevel = CharacterFormat.MaxByte;
        adv.AdvanceOwnClasses();
        Eq(adv.ClericLevel, CharacterFormat.MaxByte, "and it saturates rather than wrapping");

        Group("shape check accepts what the editor can write");
        // Every name the Name setter accepts must stay recognisable, or the trainer could rename a
        // character into something it then refused to find.
        foreach (var name in new[] { "1st Blade", "'Zog", "+Kerwin", "Bob", "Z", "a", "9" })
        {
            var rec = MakeRecord();
            new CharacterRecord(rec).Name = name;
            Check(CharacterFormat.LooksLikeRecord(rec), $"a character named {name} is recognised");
        }
        var trimmed = MakeRecord();
        new CharacterRecord(trimmed).Name = "   Spaced";
        Check(CharacterFormat.LooksLikeRecord(trimmed), "a name given with leading spaces is recognised");

        // Transferred Pool of Radiance characters can exceed the rolled maximum.
        var transferred = MakeRecord();
        transferred[CharacterFormat.OffStrength] = 22;
        Check(CharacterFormat.LooksLikeRecord(transferred),
              "an ability above 19 is still recognised (Pool of Radiance transfers)");
        transferred[CharacterFormat.OffStrength] = CharacterFormat.MaxPlausibleAbility + 1;
        Check(!CharacterFormat.LooksLikeRecord(transferred),
              "but an implausible ability is still rejected");

        Group("filename sanitising");
        foreach (var (input, expected) in new[]
                 {
                     ("Christopher", "CHRISTOP.HIL"),
                     ("Tim", "TIM.HIL"),
                     ("Magic User", "MAGICUSE.HIL"),
                     ("", "CHARACTR.HIL"),
                     ("CON", "CHARACTR.HIL"),
                     ("nul", "CHARACTR.HIL"),
                     ("...", "CHARACTR.HIL"),
                 })
            Eq(CharacterFile.SuggestFileName(input), expected, $"[{input}] maps to a safe filename");

        foreach (var hostile in HostileNames())
        {
            var suggested = CharacterFile.SuggestFileName(hostile);
            Check(suggested.IndexOfAny(Path.GetInvalidFileNameChars()) < 0,
                  $"a hostile name yields no invalid filename characters ({suggested})");
            Check(!Path.IsPathRooted(suggested), "a hostile name does not yield a rooted path");
            var folder = Path.Combine(Path.GetTempPath(), "hillsfar-folder");
            var combined = Path.Combine(folder, suggested);
            Check(combined.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                  $"a hostile name stays inside the chosen folder ({combined})");
        }

        Group("SaveAs backs up an existing target");
        string bdir = Path.Combine(Path.GetTempPath(), "hillsfar-backup-" + Guid.NewGuid());
        Directory.CreateDirectory(bdir);
        try
        {
            string path = Path.Combine(bdir, "TARGET.HIL");
            var first = MakeRecord();
            first[CharacterFormat.OffStrength] = 11;
            CharacterFile.FromRecord(path, first).SaveAs(path);
            Check(!File.Exists(path + ".bak"), "writing a new file takes no backup");

            var second = MakeRecord();
            second[CharacterFormat.OffStrength] = 17;
            bool overwrote = CharacterFile.FromRecord(path, second).SaveAs(path);
            Check(overwrote, "SaveAs reports that it overwrote an existing file");
            Check(File.Exists(path + ".bak"), "and backed it up first");
            Eq(File.ReadAllBytes(path + ".bak")[CharacterFormat.OffStrength], (byte)11,
               "the backup holds the pre-overwrite bytes");
            Eq(File.ReadAllBytes(path)[CharacterFormat.OffStrength], (byte)17,
               "and the target holds the new ones");

            var third = MakeRecord();
            third[CharacterFormat.OffStrength] = 19;
            CharacterFile.FromRecord(path, third).SaveAs(path);
            Eq(File.ReadAllBytes(path + ".bak")[CharacterFormat.OffStrength], (byte)11,
               "the backup is one-shot across SaveAs too");
            Check(!File.Exists(path + ".bak.tmp"), "no staging file is left behind");
        }
        finally
        {
            try { Directory.Delete(bdir, true); } catch { /* best effort */ }
        }

        Group("per-file dirty tracking");
        string ddir = Path.Combine(Path.GetTempPath(), "hillsfar-dirty-" + Guid.NewGuid());
        Directory.CreateDirectory(ddir);
        try
        {
            string a = Path.Combine(ddir, "AAA.HIL"), b = Path.Combine(ddir, "BBB.HIL");
            CharacterFile.FromRecord(a, MakeRecord()).SaveAs(a);
            CharacterFile.FromRecord(b, MakeRecord()).SaveAs(b);

            var vm = new FileEditorViewModel(_ => { });
            vm.LoadFolder(ddir);
            Eq(vm.Files.Count, 2, "both files load");
            var fileA = vm.Files.First(f => f.FileName == "AAA.HIL");
            vm.Selected = fileA;
            vm.Gold = 4242;
            Check(vm.IsDirty, "editing marks the file dirty");
            Check(fileA.DisplayName.EndsWith("*"), "and the list shows the marker");

            // Switch away and back: the edit and the ability to save it must both survive.
            vm.Selected = vm.Files.First(f => f.FileName == "BBB.HIL");
            Check(!vm.IsDirty, "the other file is clean");
            vm.Selected = fileA;
            Check(vm.IsDirty, "returning to the edited file still reports dirty");
            Check(vm.SaveCommand.CanExecute(null), "and Save is still available");
            Eq(vm.Gold, 4242u, "with the edit intact");
            vm.SaveCommand.Execute(null);
            Check(!vm.IsDirty, "saving clears it");
            Eq(CharacterFile.Load(a).Record.Gold, 4242u, "and the edit reached disk");

            // Summary must track a single-field edit, not only the bulk actions.
            vm.Name = "Zephyr";
            Check(vm.Summary.Contains("Zephyr"), "the summary refreshes after a single-field edit");
        }
        finally
        {
            try { Directory.Delete(ddir, true); } catch { /* best effort */ }
        }

        Group("region coalescing");
        var merged = GameLocator.Coalesce(new[]
        {
            new MemoryRegion(0x1000, 0x1000),
            new MemoryRegion(0x2000, 0x1000),   // contiguous with the previous
            new MemoryRegion(0x5000, 0x1000),   // separate
        }).ToList();
        Eq(merged.Count, 2, "two touching regions become one");
        Eq((ulong)merged[0].Base, 0x1000UL, "merged base");
        Eq((ulong)merged[0].Size, 0x2000UL, "merged size");
        Eq((ulong)merged[1].Base, 0x5000UL, "the disjoint region is kept separate");
        Eq(GameLocator.Coalesce(Array.Empty<MemoryRegion>()).Count(), 0, "no regions coalesce to none");
        Eq(GameLocator.Coalesce(new[] { new MemoryRegion(0x10, 0) }).Count(), 0,
           "an empty region is dropped");

        Group("day length rounds rather than truncating");
        Eq(GameFacts.RealMinutesPerGameDay, 49,
           "a game day is 49 real minutes (2928 s), not the 48 integer division gives");
        var refVm = new ReferenceViewModel();
        Check(refVm.ClockNote.Contains("49 real minutes"), "the clock note quotes 49");
        Check(GameFacts.Tips.Any(t => t.Contains("49 minutes")), "and the tips agree");

        Group("pick counts distinguish absent from already-good");
        var pickRec = MakeRecord();
        Eq(LockPickSet.CountWithGeometry(pickRec), 0, "a character with no picks has no geometry");
        int slot0 = CharacterFormat.OffLockPicks;
        pickRec[slot0] = 0x37;
        pickRec[slot0 + 1] = 0x1A;
        pickRec[slot0 + 2] = 0x1A + LockPickSet.ShapePairDelta;
        pickRec[slot0 + 3] = 0x37 - LockPickSet.ShapePairDelta;
        pickRec[slot0 + 4] = LockPickSet.MaxState;
        Eq(LockPickSet.CountWithGeometry(pickRec), 1, "a good pick counts as geometry");
        Eq(LockPickSet.RepairAll(pickRec), 0, "and needs no repair");

        Group("locate reports a build mismatch");
        const nuint Base = 0x20000000;
        var mem = new FakeMemory(0x30000, Base);
        mem.PlaceGame(0x1000, MakeRecord());
        var noTable = GameLocator.Locate(mem);
        Check(noTable.Found, "located without a codec table present");
        Check(noTable.TextTableMatchesShipped == false,
              "an absent/zeroed table is reported as a mismatch, not silently accepted");

        var withTable = new FakeMemory(0x30000, Base);
        withTable.PlaceGame(0x1000, MakeRecord());
        withTable.Write(0x1000 + TextCodec.DgroupTableOffset, TextCodec.ShippedTable);
        var matched = GameLocator.Locate(withTable);
        Check(matched.TextTableMatchesShipped == true, "the shipped table is recognised as a match");
    }

    /// <summary>
    /// Names that must not be allowed to shape a filename — path escapes, rooted paths, and characters
    /// Windows rejects. Built in code rather than as literals to keep the escaping legible.
    /// </summary>
    private static IEnumerable<string> HostileNames()
    {
        yield return ".." + BSTR + ".." + BSTR + "boom";
        yield return BSTR + "rooted";
        yield return "a/b";
        yield return "a:b";
        yield return "a|b";
        yield return "a*b";
        yield return "a?b";
        yield return "<hi>";
        yield return "CON";
        yield return "..";
    }

    /// <summary>A single backslash, spelled out so the literals above stay readable.</summary>
    private const string BSTR = "\\";


    /// <summary>Behaviours added or corrected in the second review round.</summary>
    private static void CrossReviewChecks()
    {
        Group("hit-point freeze respects the character's own maximum");
        // The static range is 1..255, but the real ceiling is HitPointsMax: pinning current above
        // maximum leaves a record LooksLikeRecord rejects, so the trainer could not find it again.
        var hpTarget = CharacterViewModel.FreezeTargets.First(t => t.Label.StartsWith("Hit points"));
        Eq(hpTarget.CeilingRecordOffset, CharacterFormat.OffHitPointsMax,
           "the hit-point freeze is capped by the maximum-hit-point byte");
        var rec = MakeRecord();                       // HP 42/42
        var entry = new FreezeEntry(hpTarget, 42);
        entry.Value = 200;
        Eq(entry.Value, 200L, "the static clamp still allows 200");
        Eq(entry.BytesFor(rec)?[0], (byte)42, "but the write is capped at the character's maximum");
        rec[CharacterFormat.OffHitPointsMax] = 90;
        Eq(entry.BytesFor(rec)?[0], (byte)90, "and follows the maximum when it changes");
        // The resulting record must stay recognisable.
        rec[CharacterFormat.OffHitPoints] = entry.BytesFor(rec)![0];
        Check(CharacterFormat.LooksLikeRecord(rec), "a frozen hit-point write keeps the record valid");
        // Targets without a ceiling are unaffected.
        var gold = CharacterViewModel.FreezeTargets.First(t => t.Label.StartsWith("Gold"));
        Eq(gold.CeilingRecordOffset, -1, "gold has no dynamic ceiling");

        Group("freeze value notifies even when clamped to the same number");
        var rings = CharacterViewModel.FreezeTargets.First(t => t.Label.StartsWith("Knock rings"));
        var re = new FreezeEntry(rings, CharacterFormat.MaxConsumable);
        int raised = 0;
        re.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FreezeEntry.Value)) raised++; };
        re.Value = 250;   // clamps back to 99, i.e. no change in the stored value
        Eq(re.Value, (long)CharacterFormat.MaxConsumable, "the value stays clamped");
        Check(raised > 0, "and the box is still told to re-read it, so it cannot show 250");

        Group("character file raises change notification");
        string dir = Path.Combine(Path.GetTempPath(), "hillsfar-notify-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "NOTIFY.HIL");
            CharacterFile.FromRecord(path, MakeRecord()).SaveAs(path);
            var file = CharacterFile.Load(path);

            var raisedNames = new List<string>();
            file.PropertyChanged += (_, e) => raisedNames.Add(e.PropertyName ?? "");
            Eq(file.DisplayName, "NOTIFY.HIL", "a clean file shows no marker");
            file.MarkDirty();
            Check(raisedNames.Contains(nameof(CharacterFile.IsDirty)), "IsDirty is raised");
            Check(raisedNames.Contains(nameof(CharacterFile.DisplayName)),
                  "and DisplayName too, or the list marker could never appear");
            Eq(file.DisplayName, "NOTIFY.HIL *", "the marker is shown");
            raisedNames.Clear();
            file.Save();
            Check(raisedNames.Contains(nameof(CharacterFile.DisplayName)),
                  "saving raises DisplayName so the marker clears");
            Eq(file.DisplayName, "NOTIFY.HIL", "and it is gone");

            // Save must still work when the file has vanished since it was loaded.
            var reloaded = CharacterFile.Load(path);
            reloaded.Record.Gold = 5;
            File.Delete(path);
            File.Delete(path + ".bak");
            reloaded.Save();
            Check(File.Exists(path), "Save recreates a file that disappeared rather than throwing");
            Eq(CharacterFile.Load(path).Record.Gold, 5u, "with the edits intact");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* best effort */ }
        }

        Group("bulk edits re-enable Save in the offline editor");
        string bdir = Path.Combine(Path.GetTempPath(), "hillsfar-bulk-" + Guid.NewGuid());
        Directory.CreateDirectory(bdir);
        try
        {
            string path = Path.Combine(bdir, "BULK.HIL");
            CharacterFile.FromRecord(path, MakeRecord()).SaveAs(path);
            var vm = new FileEditorViewModel(_ => { });
            vm.LoadFolder(bdir);
            Check(!vm.SaveCommand.CanExecute(null), "Save starts disabled");

            bool requeried = false;
            vm.SaveCommand.CanExecuteChanged += (_, _) => requeried = true;
            vm.MaxAbilitiesCommand.Execute(null);
            Check(vm.IsDirty, "a bulk action marks the file dirty");
            Check(requeried, "and re-queries the commands, so the Save button actually enables");
            Check(vm.SaveCommand.CanExecute(null), "Save is now available");

            // Lowering the maximum must refresh the current-hit-point box, not just the summary.
            vm.HitPointsMax = 200;
            vm.HitPoints = 200;
            var changed = new List<string>();
            vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");
            vm.HitPointsMax = 10;
            Check(changed.Contains(nameof(FileEditorViewModel.HitPoints)),
                  "lowering the maximum re-reads the current total");
            Eq(vm.HitPoints, 10, "which the record has already clamped");
        }
        finally
        {
            try { Directory.Delete(bdir, true); } catch { /* best effort */ }
        }

        Group("an empty name stays recognisable");
        // The Name setter accepts "" and writes the terminator into byte 0; LooksLikeRecord has to
        // agree, or clearing the name box would make the trainer lose the character.
        var empty = MakeRecord();
        new CharacterRecord(empty).Name = "";
        Eq(new CharacterRecord(empty).Name, "", "an empty name round-trips");
        Check(CharacterFormat.LooksLikeRecord(empty), "and the record is still recognised");
        // An all-zero window must still be rejected — the name test is not carrying that weight.
        Check(!CharacterFormat.LooksLikeRecord(new byte[CharacterFormat.RecordLength]),
              "an all-zero window is still rejected");

        Group("locator underflow guard is actually exercised");
        // Place the region base below the anchor offset so `hit < DgroupOffset` is genuinely reached.
        var low = new FakeMemory(0x20000, 0x100);
        low.Write(0x10, CharacterFormat.PrimaryAnchor.Bytes);   // hit at 0x110, below 0x0D1A
        var underflow = GameLocator.Locate(low);
        Check(!underflow.Found, "a banner too close to address zero is skipped, not wrapped");

        Group("poll revalidation");
        // Reread alone is not enough: DOSBox keeps guest RAM mapped across a game restart, so the old
        // address stays readable while holding something that is no longer a character.
        const nuint Base = 0x40000000;
        var mem = new FakeMemory(0x30000, Base);
        mem.PlaceGame(0x1000, MakeRecord());
        var buf = new byte[CharacterFormat.RecordLength];
        Check(GameLocator.Reread(mem, Base + 0x1000, buf), "Reread succeeds while the game is there");
        Check(CharacterFormat.LooksLikeRecord(buf), "and the window is a character");

        mem.Write(0x1000 + CharacterFormat.DgroupRecordOffset,
                  new byte[CharacterFormat.RecordLength]);   // game restarted; bytes now rubbish
        Check(GameLocator.Reread(mem, Base + 0x1000, buf),
              "Reread still succeeds — the memory is mapped, which is why a read test alone is unsafe");
        Check(!CharacterFormat.LooksLikeRecord(buf),
              "but the shape check catches it, which is what PollTick relies on");
    }

    // --- 12. the shipped corpus (skipped when absent) -------------------------

    /// <summary>The folder's full path when it holds at least one character file, else null.</summary>
    private static string? ValidCorpus(string dir)
    {
        try
        {
            var full = Path.GetFullPath(dir);
            if (Directory.Exists(full) &&
                (Directory.EnumerateFiles(full, "*.PRE").Any() ||
                 Directory.EnumerateFiles(full, "*.HIL").Any()))
                return full;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Not readable — treat as absent.
        }
        return null;
    }

    private static string? FindCorpus()
    {
        // The repo-relative .game\ folder first, then an explicit override. No machine-specific paths:
        // whether this group runs should not depend on one developer's directory layout.
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".game"),
        };
        var fromEnvironment = Environment.GetEnvironmentVariable("HILLSFAR_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnvironment)) candidates.Add(fromEnvironment);

        foreach (var dir in candidates)
        {
            var hit = ValidCorpus(dir);
            if (hit != null) return hit;
        }
        return null;
    }

    private static void ShippedCorpus(string? dir)
    {
        Group("shipped character files");
        if (dir == null)
        {
            Console.WriteLine("   SKIPPED — no .HIL/.PRE corpus found. These files are copyrighted and "
                              + "are not in the repository; drop a copy into .game\\ to run this group.");
            return;
        }

        Console.WriteLine($"   using {dir}");

        // Load every candidate directly rather than via LoadDirectory: that method only keeps files
        // where LooksValid is true, so asserting LooksValid on its output would be tautological — and a
        // genuine .HIL that stopped passing the shape check (exactly the regression this group exists
        // to catch) would be silently excluded while the group still reported PASS.
        var files = new List<CharacterFile>();
        foreach (var ext in new[] { CharacterFile.SavedExtension, CharacterFile.PreRolledExtension })
            foreach (var path in Directory.EnumerateFiles(dir, "*" + ext))
            {
                try
                {
                    files.Add(CharacterFile.Load(path));
                }
                catch (Exception e)
                {
                    Check(false, $"{Path.GetFileName(path)} loads: {e.Message}");
                }
            }
        Check(files.Count > 0, "at least one character file parsed");

        foreach (var f in files)
        {
            var r = f.Record;
            Check(f.LooksValid, $"{f.FileName} passes the shape check");
            Eq(f.Record.Bytes.Length, CharacterFormat.RecordLength, $"{f.FileName} is 188 bytes");
            Check(r.Name.Length is > 0 and <= CharacterFormat.MaxNameLength,
                  $"{f.FileName} has a plausible name");
            Check(ClassBook.IsLegalMask(r.ClassMask), $"{f.FileName} has a legal class mask");
            Check(r.HitPoints <= r.HitPointsMax, $"{f.FileName} hit points are sane");
            Check(r.Hour is >= 1 and <= 24, $"{f.FileName} hour is in range");
            Check(LockPickSet.GeometryLooksRight(r.Bytes),
                  $"{f.FileName} pick geometry follows the +20 pairing");

            // Round-trip: loading and re-saving must not change a byte.
            var original = r.Bytes.ToArray();
            string tmp = Path.Combine(Path.GetTempPath(), "hf-" + Guid.NewGuid() + ".HIL");
            try
            {
                CharacterFile.FromRecord(tmp, original).SaveAs(tmp);
                Check(File.ReadAllBytes(tmp).AsSpan().SequenceEqual(original),
                      $"{f.FileName} round-trips byte-for-byte");
            }
            finally
            {
                try { File.Delete(tmp); } catch { /* best effort */ }
            }
        }

        // The four shipped pre-rolled characters carry the class indices that pinned the index table.
        foreach (var (name, mask, index) in new (string, int, byte)[]
                 {
                     ("CLERIC.PRE", ClassBook.MaskCleric, 0),
                     ("FIGHTER.PRE", ClassBook.MaskFighter, 2),
                     ("MAGICUSE.PRE", ClassBook.MaskMagicUser, 5),
                     ("THIEF.PRE", ClassBook.MaskThief, 6),
                 })
        {
            var f = files.FirstOrDefault(
                x => string.Equals(x.FileName, name, StringComparison.OrdinalIgnoreCase));
            // Fail rather than continue: silently skipping turned a missing or unparsable shipped file
            // into zero assertions, which is the opposite of what this group is for.
            Check(f != null, $"{name} is present in the corpus");
            if (f == null) continue;
            Eq(f.Record.ClassMask, mask, $"{name} class mask");
            Eq((byte)f.Record.ClassIndex, index, $"{name} class index");
            Eq(f.Record.Strength, 18, $"{name} has Strength 18");
        }

        // Only fighters carry an exceptional-strength percentile.
        foreach (var f in files)
        {
            bool isFighter = ClassBook.ForMask(f.Record.ClassMask)?.IsFighter ?? false;
            if (f.Record.StrengthPercentile > 0)
                Check(isFighter, $"{f.FileName} has a percentile only because it is a fighter");
        }
    }
}
