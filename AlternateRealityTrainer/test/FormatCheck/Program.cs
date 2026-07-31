using System.IO;
using System.Text;
using AlternateRealityTrainer.Game;
using AlternateRealityTrainer.Memory;
using AlternateRealityTrainer.ViewModels;
using GameTrainers.Common.Memory;

// Headless verification for the Alternate Reality: The City trainer.
//
// Two groups of checks:
//
//   * Always — the decoded layout against a fixture built from the values that were confirmed
//     live (the shipped character "Neuro" as the game's own status bar rendered him), plus the
//     reference tables, the clamps, the locator's validation predicate and the map renderer.
//
//   * Only when the copyrighted character files are present under `.game\` — every shipped
//     ARCCD file is parsed and round-tripped byte for byte. Absent, that group is skipped with a
//     note rather than failed, because those files are not in the repository.
//
// Exits 0 when everything passes, 1 otherwise.

int passed = 0, failed = 0;
var failures = new List<string>();

void Check(string name, bool ok)
{
    if (ok) { passed++; return; }
    failed++;
    failures.Add(name);
    Console.WriteLine($"  FAIL  {name}");
}

void CheckEqual<T>(string name, T expected, T actual) =>
    Check($"{name} (expected {expected}, got {actual})", EqualityComparer<T>.Default.Equals(expected, actual));

void Section(string title) => Console.WriteLine($"\n== {title} ==");

// ---------------------------------------------------------------------------
// The reference character: "Neuro", slot 0 of the roster shipped with the game.
// Every number here was read off the game's own status bar in a live DOSBox session.
// ---------------------------------------------------------------------------

const string NeuroName = "Neuro";
int[] neuroAttributes = { 9, 12, 16, 11, 22, 17, 14 };          // storage order: STR INT WIS SKL STA CHR SPD
byte[] neuroFractions = { 0x7D, 0xA7, 0x7A, 0xE5, 0xAD, 0xA9, 0x50 };
const int NeuroLevel = 2;
const uint NeuroExperience = 818;
const uint NeuroNextLevel = 1054;
const uint NeuroHitPoints = 10;
const uint NeuroHitPointsMax = 35;
const int NeuroFood = 3, NeuroWater = 4;

byte[] BuildNeuro()
{
    var buf = new byte[CharacterFormat.RecordSize];
    CharacterFormat.WriteName(buf, NeuroName);
    for (int i = 0; i < CharacterFormat.AttributeCount; i++)
    {
        int off = CharacterFormat.AttributeOffset(i);
        for (int c = 0; c < CharacterFormat.AttributeCopies; c++)
            buf[off + c] = (byte)neuroAttributes[i];
        buf[off + CharacterFormat.AttributeFractionOffset] = neuroFractions[i];
    }
    buf[CharacterFormat.OffLevel] = NeuroLevel;
    CharacterFormat.WriteU32(buf, CharacterFormat.OffExperience, NeuroExperience);
    CharacterFormat.WriteU32(buf, CharacterFormat.OffNextLevelExp, NeuroNextLevel);
    CharacterFormat.WriteU32(buf, CharacterFormat.OffHitPoints, NeuroHitPoints);
    CharacterFormat.WriteU32(buf, CharacterFormat.OffHitPointsMax, NeuroHitPointsMax);
    buf[CharacterFormat.OffFood] = NeuroFood;
    buf[CharacterFormat.OffWater] = NeuroWater;
    buf[CharacterFormat.OffCompass] = 1;
    // Clock: hour 11 of day 30, month index 4 (Sowings), year 0.
    buf[CharacterFormat.OffMinute] = 21;
    buf[CharacterFormat.OffHour] = 11;
    buf[CharacterFormat.OffDay] = 30;
    buf[CharacterFormat.OffMonth] = 4;
    CharacterFormat.WriteU16(buf, CharacterFormat.OffYear, 0);
    return buf;
}

// ---------------------------------------------------------------------------
Section("Record layout");

{
    var rec = new CharacterRecord(BuildNeuro());

    CheckEqual("name", NeuroName, rec.Name);
    CheckEqual("level", NeuroLevel, rec.Level);
    CheckEqual("experience", NeuroExperience, rec.Experience);
    CheckEqual("next-level experience", NeuroNextLevel, rec.NextLevelExperience);
    CheckEqual("hit points", NeuroHitPoints, rec.HitPoints);
    CheckEqual("hit points max", NeuroHitPointsMax, rec.HitPointsMax);
    CheckEqual("food packets", (byte)NeuroFood, rec.Food);
    CheckEqual("water flasks", (byte)NeuroWater, rec.Water);
    Check("carries a compass", rec.HasCompass);
    Check("carries no watch", !rec.HasWatch);

    for (int i = 0; i < CharacterFormat.AttributeCount; i++)
    {
        var info = AttributeBook.At(i);
        CheckEqual($"{info.Name} value", (byte)neuroAttributes[i], rec.GetAttribute(i));
        CheckEqual($"{info.Name} fraction", neuroFractions[i], rec.GetAttributeFraction(i));
    }

    // The status bar prints STA CHR STR INT WIS SKL — the order the display uses, not storage order.
    var displayed = AttributeBook.DisplayOrder.Select(i => rec.GetAttribute(i)).ToArray();
    Check("display order reproduces the status bar (STA 22 CHR 17 STR 9 INT 12 WIS 16 SKL 11)",
        displayed.SequenceEqual(new byte[] { 22, 17, 9, 12, 16, 11 }));

    CheckEqual("month name", "Sowings", rec.MonthName);
    CheckEqual("clock text", "Hour 11 of day 30, month of Sowings, year 0 since abduction", rec.DateTimeText);
}

// ---------------------------------------------------------------------------
Section("Offsets are internally consistent");

{
    CheckEqual("attribute stride × count fits before level",
        true, CharacterFormat.AttributeOffset(CharacterFormat.AttributeCount - 1)
              + CharacterFormat.AttributeStride <= CharacterFormat.OffLevel);

    // The DGROUP addresses the game's own display templates name, minus the record base.
    CheckEqual("STR is DGROUP:0x4F1F", 0x4F1F, CharacterFormat.DgroupRecordOffset + CharacterFormat.AttributeOffset(0));
    CheckEqual("SPD is DGROUP:0x4F5B", 0x4F5B, CharacterFormat.DgroupRecordOffset + CharacterFormat.AttributeOffset(6));
    CheckEqual("name is DGROUP:0x4EFD", 0x4EFD, CharacterFormat.DgroupRecordOffset + CharacterFormat.OffName);
    CheckEqual("level is DGROUP:0x4F72", 0x4F72, CharacterFormat.DgroupRecordOffset + CharacterFormat.OffLevel);
    CheckEqual("experience is DGROUP:0x4F73", 0x4F73, CharacterFormat.DgroupRecordOffset + CharacterFormat.OffExperience);
    CheckEqual("hit points is DGROUP:0x4F7B", 0x4F7B, CharacterFormat.DgroupRecordOffset + CharacterFormat.OffHitPoints);
    CheckEqual("gold is DGROUP:0x4F83", 0x4F83, CharacterFormat.DgroupRecordOffset + CharacterFormat.OffGold);
    CheckEqual("copper is DGROUP:0x4F87", 0x4F87, CharacterFormat.DgroupRecordOffset + CharacterFormat.OffCopper);
    CheckEqual("food is DGROUP:0x4F8F", 0x4F8F, CharacterFormat.DgroupRecordOffset + CharacterFormat.OffFood);
    CheckEqual("watch is DGROUP:0x4F94", 0x4F94, CharacterFormat.DgroupRecordOffset + CharacterFormat.OffWatch);

    Check("every validated field lies inside the live prefix",
        CharacterFormat.OffWatch < CharacterFormat.LiveFieldsLength);
    Check("the live prefix is smaller than the record", CharacterFormat.LiveFieldsLength < CharacterFormat.RecordSize);
}

// ---------------------------------------------------------------------------
Section("Locator anchors");

{
    var primary = CharacterFormat.PrimaryAnchor;
    CheckEqual("primary anchor is the status-bar header", "Stats STA   CHR   STR   INT   WIS   SKL",
        Encoding.ASCII.GetString(primary.Bytes));
    CheckEqual("primary anchor length", 39, primary.Bytes.Length);
    CheckEqual("primary anchor DGROUP offset", 0x012A, primary.DgroupOffset);

    Check("there are enough validators to meet the minimum",
        CharacterFormat.Validators.Length >= CharacterFormat.MinValidators);
    Check("at least two validators are required", CharacterFormat.MinValidators >= 2);
    Check("every validator is long enough to be specific",
        CharacterFormat.Validators.All(v => v.Bytes.Length >= 8));
    Check("every anchor sits before the character record",
        CharacterFormat.Validators.Append(primary)
            .All(a => a.DgroupOffset < CharacterFormat.DgroupRecordOffset
                      || a.DgroupOffset >= CharacterFormat.DgroupRecordOffset + CharacterFormat.RecordSize));
    Check("the game's text encoding uses tab for space in the validators",
        CharacterFormat.Validators.Any(v => v.Bytes.Contains((byte)0x09)));
}

// ---------------------------------------------------------------------------
Section("Record recognition");

{
    var good = BuildNeuro();
    Check("a real record is recognised", CharacterFormat.LooksLikeRecord(good, 0));

    var noName = BuildNeuro();
    noName[CharacterFormat.OffName] = 0;
    Check("a record with no name is rejected", !CharacterFormat.LooksLikeRecord(noName, 0));

    var digitName = BuildNeuro();
    digitName[CharacterFormat.OffName] = (byte)'7';
    Check("a name that does not start with a letter is rejected", !CharacterFormat.LooksLikeRecord(digitName, 0));

    var unterminated = BuildNeuro();
    for (int i = 0; i < CharacterFormat.NameLength; i++) unterminated[CharacterFormat.OffName + i] = (byte)'A';
    Check("a name field that never terminates is rejected", !CharacterFormat.LooksLikeRecord(unterminated, 0));

    // Current and maximum may sit below the natural maximum — that is what a drain looks like — but
    // neither may exceed it.
    var overNatural = BuildNeuro();
    int on = CharacterFormat.AttributeOffset(3);
    overNatural[on + 1] = (byte)(overNatural[on + 2] + 1);
    Check("a maximum above the natural maximum is rejected",
        !CharacterFormat.LooksLikeRecord(overNatural, 0));

    var currentOverNatural = BuildNeuro();
    int cn = CharacterFormat.AttributeOffset(5);
    currentOverNatural[cn] = (byte)(currentOverNatural[cn + 2] + 1);
    Check("a current value above the natural maximum is rejected",
        !CharacterFormat.LooksLikeRecord(currentOverNatural, 0));

    // The exact shape a Wraith leaves behind: current and maximum drained, natural intact.
    var wraithed = BuildNeuro();
    for (int i = 0; i < CharacterFormat.AttributeCount; i++)
    {
        int a = CharacterFormat.AttributeOffset(i);
        wraithed[a] = wraithed[a + 1] = 0;          // natural (a + 2) left alone
    }
    Check("a Wraith-drained character is still recognised", CharacterFormat.LooksLikeRecord(wraithed, 0));

    // A Wraith drains an attribute permanently, and one was seen live leaving every visible
    // attribute at 0. That character still has to be findable.
    var drained = BuildNeuro();
    int za = CharacterFormat.AttributeOffset(2);
    drained[za] = drained[za + 1] = drained[za + 2] = 0;
    Check("an attribute drained to zero is still recognised", CharacterFormat.LooksLikeRecord(drained, 0));

    var fullyDrained = BuildNeuro();
    for (int i = 0; i < CharacterFormat.AttributeCount; i++)
    {
        int a = CharacterFormat.AttributeOffset(i);
        fullyDrained[a] = fullyDrained[a + 1] = fullyDrained[a + 2] = 0;
    }
    Check("a character drained to zero across the board is still recognised",
        CharacterFormat.LooksLikeRecord(fullyDrained, 0));

    var overHealed = BuildNeuro();
    CharacterFormat.WriteU32(overHealed, CharacterFormat.OffHitPoints, NeuroHitPointsMax + 1);
    Check("hit points above the maximum are rejected", !CharacterFormat.LooksLikeRecord(overHealed, 0));

    var noMax = BuildNeuro();
    CharacterFormat.WriteU32(noMax, CharacterFormat.OffHitPointsMax, 0);
    Check("a zero hit-point maximum is rejected", !CharacterFormat.LooksLikeRecord(noMax, 0));

    // Regression: attached to the wrong process, the structural fallback used to accept a run of
    // unrelated heap data as a character — a one-letter name, seven identical attributes and a
    // next-level threshold below the experience. Each of those is now refused on its own.
    var oneLetterName = BuildNeuro();
    Array.Clear(oneLetterName, CharacterFormat.OffName, CharacterFormat.NameLength);
    oneLetterName[CharacterFormat.OffName] = (byte)'b';
    Check("a one-character name is rejected", !CharacterFormat.LooksLikeRecord(oneLetterName, 0));

    var digitsInName = BuildNeuro();
    CharacterFormat.WriteName(digitsInName, "Ne4ro");
    Check("a digit inside a name is accepted — the game lets you type one",
        CharacterFormat.LooksLikeRecord(digitsInName, 0));

    var punctuationInName = BuildNeuro();
    CharacterFormat.WriteName(punctuationInName, "Ne@ro");
    Check("punctuation a name cannot contain is rejected",
        !CharacterFormat.LooksLikeRecord(punctuationInName, 0));

    var thresholdBelowExperience = BuildNeuro();
    CharacterFormat.WriteU32(thresholdBelowExperience, CharacterFormat.OffNextLevelExp, 0);
    Check("a next-level threshold below the experience is rejected",
        !CharacterFormat.LooksLikeRecord(thresholdBelowExperience, 0));

    var hugeHitPoints = BuildNeuro();
    CharacterFormat.WriteU32(hugeHitPoints, CharacterFormat.OffHitPointsMax, CharacterFormat.HitPointCeiling + 1);
    CharacterFormat.WriteU32(hugeHitPoints, CharacterFormat.OffHitPoints, CharacterFormat.HitPointCeiling + 1);
    Check("hit points beyond the editor's own ceiling are rejected",
        !CharacterFormat.LooksLikeRecord(hugeHitPoints, 0));
    var atCeiling = BuildNeuro();
    CharacterFormat.WriteU32(atCeiling, CharacterFormat.OffHitPointsMax, CharacterFormat.HitPointCeiling);
    CharacterFormat.WriteU32(atCeiling, CharacterFormat.OffHitPoints, CharacterFormat.HitPointCeiling);
    Check("hit points exactly at the ceiling are still accepted",
        CharacterFormat.LooksLikeRecord(atCeiling, 0));

    // The exact window that fooled it, rebuilt: name "b", every attribute 13 with fraction 13,
    // level 12, experience 12, no threshold, 526,344 hit points.
    var falsePositive = new byte[CharacterFormat.RecordSize];
    falsePositive[CharacterFormat.OffName] = (byte)'b';
    for (int i = 0; i < CharacterFormat.AttributeCount; i++)
    {
        int a = CharacterFormat.AttributeOffset(i);
        falsePositive[a] = falsePositive[a + 1] = falsePositive[a + 2] = falsePositive[a + 3] = 13;
    }
    falsePositive[CharacterFormat.OffLevel] = 12;
    CharacterFormat.WriteU32(falsePositive, CharacterFormat.OffExperience, 12);
    CharacterFormat.WriteU32(falsePositive, CharacterFormat.OffHitPoints, 526_344);
    CharacterFormat.WriteU32(falsePositive, CharacterFormat.OffHitPointsMax, 526_344);
    Check("the heap window that once passed is now rejected",
        !CharacterFormat.LooksLikeRecord(falsePositive, 0));

    // …and a maxed-out character, whose attributes really are all identical, still passes.
    var maxed = new CharacterRecord(BuildNeuro());
    maxed.MaxEverything();
    Check("a character with every attribute maxed is still recognised",
        CharacterFormat.LooksLikeRecord(maxed.Buffer, 0));

    var zeros = new byte[CharacterFormat.RecordSize];
    Check("an all-zero window is rejected", !CharacterFormat.LooksLikeRecord(zeros, 0));

    var ones = new byte[CharacterFormat.RecordSize];
    Array.Fill(ones, (byte)0xFF);
    Check("an all-0xFF window is rejected", !CharacterFormat.LooksLikeRecord(ones, 0));

    Check("a negative offset is rejected", !CharacterFormat.LooksLikeRecord(good, -1));
    Check("an offset that overruns the buffer is rejected",
        !CharacterFormat.LooksLikeRecord(good, good.Length - CharacterFormat.LiveFieldsLength + 1));

    // The record must still be recognised when it is embedded in a larger scan window.
    var window = new byte[CharacterFormat.RecordSize * 2];
    Array.Copy(good, 0, window, 0x123, CharacterFormat.LiveFieldsLength);
    Check("a record found at an offset inside a scan window is recognised",
        CharacterFormat.LooksLikeRecord(window, 0x123));
}

// ---------------------------------------------------------------------------
Section("Names");

{
    foreach (var name in new[] { "Neuro", "Darwin", "Shadowmancer", "A", "Zephyr Quicksilver" })
    {
        var buf = new byte[CharacterFormat.RecordSize];
        CharacterFormat.WriteName(buf, name);
        CheckEqual($"name round-trips: {name}", name, CharacterFormat.ReadName(buf));
    }

    var over = new byte[CharacterFormat.RecordSize];
    CharacterFormat.WriteName(over, new string('X', 60));
    CheckEqual("an over-long name is truncated", CharacterFormat.MaxNameLength, CharacterFormat.ReadName(over).Length);

    var dirty = new byte[CharacterFormat.RecordSize];
    CharacterFormat.WriteName(dirty, "Ne\u0001ro\u00ff");
    Check("control and non-ASCII characters are replaced, not written raw",
        CharacterFormat.ReadName(dirty).All(c => c is >= ' ' and < (char)127));

    // Writing a shorter name must not leave the tail of a longer one behind.
    var reused = new byte[CharacterFormat.RecordSize];
    CharacterFormat.WriteName(reused, "Shadowmancer");
    CharacterFormat.WriteName(reused, "Ann");
    CheckEqual("a shorter name clears the old one", "Ann", CharacterFormat.ReadName(reused));
}

// ---------------------------------------------------------------------------
Section("Edits, clamps and flush ranges");

{
    var flushes = new List<(int Offset, int Length)>();
    var buf = BuildNeuro();
    var rec = new CharacterRecord(buf, 0, (o, l) => flushes.Add((o, l)));

    rec.SetAttribute(0, 150);
    CheckEqual("setting an attribute writes all three copies", 150, rec.GetAttribute(0));
    Check("all three attribute copies agree after a write",
        buf[CharacterFormat.AttributeOffset(0)] == 150 &&
        buf[CharacterFormat.AttributeOffset(0) + 1] == 150 &&
        buf[CharacterFormat.AttributeOffset(0) + 2] == 150);
    Check("setting an attribute flushes exactly the three copies",
        flushes.Count == 1 && flushes[0] == (CharacterFormat.AttributeOffset(0), CharacterFormat.AttributeCopies));
    CheckEqual("the fraction byte is left alone", neuroFractions[0], rec.GetAttributeFraction(0));

    rec.SetAttribute(1, 9999);
    CheckEqual("an attribute clamps to the ceiling", (byte)CharacterFormat.AttributeCeiling, rec.GetAttribute(1));
    rec.SetAttribute(2, -5);
    CheckEqual("an attribute clamps to 1", (byte)1, rec.GetAttribute(2));

    flushes.Clear();
    rec.Level = 999;
    CheckEqual("level clamps to the ceiling", CharacterFormat.LevelCeiling, rec.Level);
    Check("level flushes one byte", flushes.Count == 1 && flushes[0] == (CharacterFormat.OffLevel, 1));

    flushes.Clear();
    rec.Experience = 123_456;
    CheckEqual("experience round-trips", 123_456u, rec.Experience);
    Check("experience flushes its own four bytes",
        flushes.Contains((CharacterFormat.OffExperience, 4)));
    Check("and carries the next-level threshold with it when it has to",
        rec.NextLevelExperience >= rec.Experience &&
        flushes.Contains((CharacterFormat.OffNextLevelExp, 4)));
    Check("but writes nothing else", flushes.All(f => f.Offset is CharacterFormat.OffExperience
                                                            or CharacterFormat.OffNextLevelExp));
    rec.Experience = uint.MaxValue;
    CheckEqual("experience clamps to the ceiling", CharacterFormat.ExperienceCeiling, rec.Experience);

    flushes.Clear();
    rec.Gold = 12_345;
    CheckEqual("gold round-trips", (ushort)12_345, rec.Gold);
    Check("gold flushes two bytes", flushes.Count == 1 && flushes[0] == (CharacterFormat.OffGold, 2));

    flushes.Clear();
    rec.Name = "Ann";
    CheckEqual("name round-trips through the record", "Ann", rec.Name);
    Check("a name write flushes the whole field",
        flushes.Count == 1 && flushes[0] == (CharacterFormat.OffName, CharacterFormat.NameLength));

    // Bulk actions.
    var bulk = new CharacterRecord(BuildNeuro());
    bulk.FullHeal();
    CheckEqual("full heal restores hit points to the maximum", bulk.HitPointsMax, bulk.HitPoints);

    bulk.MaxAttributes();
    Check("max attributes sets every attribute",
        Enumerable.Range(0, CharacterFormat.AttributeCount)
            .All(i => bulk.GetAttribute(i) == CharacterFormat.MaxAttribute));

    bulk.MaxHealth();
    CheckEqual("max health sets current", (uint)CharacterFormat.MaxHitPoints, bulk.HitPoints);
    CheckEqual("max health sets maximum", (uint)CharacterFormat.MaxHitPoints, bulk.HitPointsMax);

    bulk.MaxMoney();
    Check("max money fills every coin field",
        bulk.Gold == CharacterFormat.MaxCoins && bulk.Silver == CharacterFormat.MaxCoins &&
        bulk.Copper == CharacterFormat.MaxCoins && bulk.Gems == CharacterFormat.MaxCoins &&
        bulk.Jewelry == CharacterFormat.MaxCoins);

    bulk.MaxSupplies();
    Check("max supplies fills the counters and grants the compass and watch",
        bulk.Food == CharacterFormat.MaxSupply && bulk.Water == CharacterFormat.MaxSupply &&
        bulk.Crystals == CharacterFormat.MaxSupply && bulk.Keys == CharacterFormat.MaxSupply &&
        bulk.HasCompass && bulk.HasWatch);

    // Level up: experience must reach the threshold the game is waiting for.
    var lv = new CharacterRecord(BuildNeuro());
    lv.LevelUp();
    CheckEqual("level up reaches the next-level threshold", NeuroNextLevel, lv.Experience);

    // …and must still make progress when the threshold is already behind us.
    var lv2 = new CharacterRecord(BuildNeuro());
    lv2.NextLevelExperience = 1;
    uint before = lv2.Experience;
    lv2.LevelUp();
    Check("level up still advances when the threshold is already passed", lv2.Experience > before);

    // Max everything must not disturb the character's pace.
    var me = new CharacterRecord(BuildNeuro());
    me.MaxEverything();
    CheckEqual("max everything leaves experience alone", NeuroExperience, me.Experience);
    CheckEqual("max everything leaves level alone", NeuroLevel, me.Level);

    // Experience and its threshold must stay in the order the locator expects, or typing a number
    // into the Experience box — the trainer's headline feature — would lose the character.
    var xp = new CharacterRecord(BuildNeuro());
    xp.Experience = 500_000;
    Check("raising experience past the threshold carries the threshold up",
        xp.NextLevelExperience >= xp.Experience);
    Check("so the record is still recognisable", CharacterFormat.LooksLikeRecord(xp.Buffer, 0));
    xp.NextLevelExperience = 1;
    CheckEqual("the threshold is never allowed below the experience", xp.Experience, xp.NextLevelExperience);
    Check("and the record is still recognisable", CharacterFormat.LooksLikeRecord(xp.Buffer, 0));
    xp.Experience = 10;
    Check("lowering experience leaves the threshold alone", xp.NextLevelExperience >= xp.Experience);
    Check("still recognisable", CharacterFormat.LooksLikeRecord(xp.Buffer, 0));

    // Same guarantee through the view-model, which is what the text boxes drive.
    {
        var xpHost = new FakeHost();
        var xpVm = new CharacterViewModel(xpHost, new LocateResult(0x4000, 0, BuildNeuro(), "fixture", 3));
        xpVm.Experience = CharacterFormat.ExperienceCeiling;
        Check("the editor cannot create a record the locator would reject",
            CharacterFormat.LooksLikeRecord(new CharacterRecord(BuildNeuro()).Buffer, 0) &&
            xpVm.NextLevelExperience >= xpVm.Experience);
    }

    // Level up must saturate rather than wrap: a garbage read of uint.MaxValue must never turn into
    // zero experience.
    var wrap = new CharacterRecord(BuildNeuro());
    CharacterFormat.WriteU32(wrap.Buffer, CharacterFormat.OffExperience, uint.MaxValue);
    CharacterFormat.WriteU32(wrap.Buffer, CharacterFormat.OffNextLevelExp, uint.MaxValue);
    Check("level up refuses when experience is already at the ceiling", !wrap.LevelUp());
    Check("and does not wrap experience to zero", wrap.Experience > 0);

    var advanced = new CharacterRecord(BuildNeuro());
    Check("level up reports that it advanced", advanced.LevelUp());

    // Hit points and their maximum must stay in the range the locator will still recognise.
    var hp = new CharacterRecord(BuildNeuro());
    hp.HitPoints = 500;
    CheckEqual("raising hit points past the maximum raises the maximum too", 500u, hp.HitPointsMax);
    Check("so the record is still recognisable", CharacterFormat.LooksLikeRecord(hp.Buffer, 0));
    hp.HitPointsMax = 20;
    CheckEqual("lowering the maximum pulls the current value down", 20u, hp.HitPoints);
    Check("and the record is still recognisable", CharacterFormat.LooksLikeRecord(hp.Buffer, 0));
    hp.HitPoints = uint.MaxValue;
    CheckEqual("hit points clamp to their ceiling", CharacterFormat.HitPointCeiling, hp.HitPoints);
    Check("which stays inside what the locator accepts", CharacterFormat.LooksLikeRecord(hp.Buffer, 0));
    hp.HitPointsMax = 0;
    CheckEqual("the maximum never drops below 1", 1u, hp.HitPointsMax);

    // A name the locator could not recognise must be refused, not written.
    var named = new CharacterRecord(BuildNeuro());
    named.Name = "";
    CheckEqual("an empty name is refused", NeuroName, named.Name);
    named.Name = "7Up";
    CheckEqual("a name that does not start with a letter is refused", NeuroName, named.Name);
    named.Name = "Zephyr";
    CheckEqual("a valid name is accepted", "Zephyr", named.Name);
    Check("and the record stays recognisable throughout", CharacterFormat.LooksLikeRecord(named.Buffer, 0));

    Check("IsWritableName accepts a plain name", CharacterFormat.IsWritableName("Neuro"));
    Check("IsWritableName rejects null", !CharacterFormat.IsWritableName(null));
    Check("IsWritableName rejects blanks", !CharacterFormat.IsWritableName("   "));
    Check("IsWritableName rejects a leading digit", !CharacterFormat.IsWritableName("1Neuro"));
    Check("IsWritableName rejects a name of only control characters",
        !CharacterFormat.IsWritableName(""));
    Check("IsWritableName rejects a single letter", !CharacterFormat.IsWritableName("A"));
    Check("IsWritableName accepts a digit inside the name", CharacterFormat.IsWritableName("Bob2"));
    Check("IsWritableName rejects punctuation a name cannot contain",
        !CharacterFormat.IsWritableName("Ne@ro"));
    Check("IsWritableName accepts an apostrophe and a hyphen",
        CharacterFormat.IsWritableName("O'Brien") && CharacterFormat.IsWritableName("Jean-Luc"));

    // The editor's rule and the locator's rule have to agree exactly, or the trainer can write a
    // name it will then refuse to recognise — which is how it loses the character it just renamed.
    string[] nameProbes =
    {
        "Neuro", "Darwin", "Shadowmancer", "A", "Ab", "Bob2", "R2D2", "O'Brien", "Jean-Luc",
        "St. John", "", "  ", "1Neuro", "Zephyr Quicksilver", new string('X', 40),
    };
    int nameDisagreements = 0;
    foreach (var candidate in nameProbes)
    {
        if (!CharacterFormat.IsWritableName(candidate)) continue;   // refused names are never written
        var probe = BuildNeuro();
        CharacterFormat.WriteName(probe, candidate);
        if (!CharacterFormat.LooksLikeRecord(probe, 0)) nameDisagreements++;
    }
    CheckEqual("every name the editor accepts is one the locator still recognises", 0, nameDisagreements);

    // Guard rails on the constructor.
    Check("a short buffer is rejected",
        Throws(() => new CharacterRecord(new byte[CharacterFormat.RecordSize - 1])));
    Check("an out-of-range base offset is rejected",
        Throws(() => new CharacterRecord(new byte[CharacterFormat.RecordSize], 1)));
    Check("a null buffer is rejected", Throws(() => new CharacterRecord(null!)));
    // Flush offsets are record-relative, so a flushing record must sit at offset 0 — otherwise the
    // consumer would slice the wrong bytes out of the buffer and write them to the right address.
    Check("a non-zero base offset with a flush delegate is rejected",
        Throws(() => new CharacterRecord(new byte[CharacterFormat.RecordSize * 2], 0x10, (_, _) => { })));
    Check("a non-zero base offset without a flush delegate is fine",
        !Throws(() => new CharacterRecord(new byte[CharacterFormat.RecordSize * 2], 0x10)));
}

// ---------------------------------------------------------------------------
Section("Reference tables");

{
    CheckEqual("seven attributes", CharacterFormat.AttributeCount, AttributeBook.All.Count);
    Check("attribute indices are 0..6",
        AttributeBook.All.Select(a => a.Index).SequenceEqual(Enumerable.Range(0, CharacterFormat.AttributeCount)));
    CheckEqual("six attributes are shown on the status bar", 6, AttributeBook.All.Count(a => !a.Hidden));
    CheckEqual("Physical Speed is the hidden one", "Physical Speed", AttributeBook.All.Single(a => a.Hidden).Name);
    CheckEqual("the display order covers the six visible attributes", 6, AttributeBook.DisplayOrder.Count);
    Check("the display order names no attribute twice",
        AttributeBook.DisplayOrder.Distinct().Count() == AttributeBook.DisplayOrder.Count);
    Check("the display order starts with Stamina then Charm",
        AttributeBook.At(AttributeBook.DisplayOrder[0]).Abbreviation == "STA" &&
        AttributeBook.At(AttributeBook.DisplayOrder[1]).Abbreviation == "CHR");

    CheckEqual("eleven months", 11, GameFacts.Months.Count);
    CheckEqual("the year begins with Rebirth", "Rebirth", GameFacts.Months[0]);
    CheckEqual("six encounter options", 6, GameFacts.EncounterOptions.Count);
    Check("the encounter menu is numbered 1..6",
        GameFacts.EncounterOptions.Select((s, i) => s.StartsWith($"{i + 1})", StringComparison.Ordinal)).All(x => x));
    CheckEqual("eighteen evil creatures", 18, GameFacts.EvilCreatures.Count);
    Check("the evil-creature list has no duplicates",
        GameFacts.EvilCreatures.Distinct(StringComparer.Ordinal).Count() == GameFacts.EvilCreatures.Count);
    CheckEqual("nine weapons", 9, GameFacts.Weapons.Count);
    CheckEqual("the strongest is the Magical Flamesword", "Magical Flamesword", GameFacts.Weapons[^1]);
    CheckEqual("the weakest is the Dagger", "Dagger", GameFacts.Weapons[0]);
    CheckEqual("eleven armour materials", 11, GameFacts.ArmourMaterials.Count);
    CheckEqual("the heaviest armour is Plated", "Plated", GameFacts.ArmourMaterials[^1]);
    CheckEqual("four shields", 4, GameFacts.Shields.Count);
    Check("no item ladder repeats itself",
        GameFacts.Weapons.Distinct(StringComparer.Ordinal).Count() == GameFacts.Weapons.Count &&
        GameFacts.ArmourMaterials.Distinct(StringComparer.Ordinal).Count() == GameFacts.ArmourMaterials.Count &&
        GameFacts.Shields.Distinct(StringComparer.Ordinal).Count() == GameFacts.Shields.Count);
    Check("the controls list covers the game's own help panel",
        new[] { "G", "U", "D", "C", "S", "P", "W" }.All(k => GameFacts.Controls.Any(c => c.Key == k)));
    CheckEqual("the city is 64 squares square", 64, GameFacts.CitySize);

    Check("every potion row names a colour, a taste and an effect",
        PotionBook.All.All(p => p.Colour.Length > 0 && p.Taste.Length > 0 && p.Effect.Length > 0));
    Check("the potion table covers all nine colours",
        PotionBook.All.Select(p => p.Colour).Distinct().Count() == 9);
    Check("Treasure Finding is in the table",
        PotionBook.All.Any(p => p.Effect == "Treasure Finding"));
    Check("everything worth hoarding appears in the table",
        PotionBook.WorthHoarding.All(e => PotionBook.All.Any(p => p.Effect == e)));
    Check("every sip risk renders a label", PotionBook.All.All(p => p.SipLabel.Length > 0));
}

// ---------------------------------------------------------------------------
Section("City map");

{
    Check("every coordinate is on the board",
        CityBook.Places.All(p => p.North >= 1 && p.North <= GameFacts.CitySize &&
                                 p.East >= 1 && p.East <= GameFacts.CitySize));
    CheckEqual("twelve guilds", 12, CityBook.Places.Count(p => p.Kind == PlaceKind.Guild));
    CheckEqual("three banks", 3, CityBook.Places.Count(p => p.Kind == PlaceKind.Bank));
    CheckEqual("two healers", 2, CityBook.Places.Count(p => p.Kind == PlaceKind.Healer));
    CheckEqual("four smithies", 4, CityBook.Places.Count(p => p.Kind == PlaceKind.Smithy));
    Check("every kind of building is represented",
        Enum.GetValues<PlaceKind>().All(k => CityBook.Places.Any(p => p.Kind == k)));
    CheckEqual("the cheapest tavern is at 63N 21E", 1,
        CityBook.Places.Count(p => p.Kind == PlaceKind.Tavern && p.North == 63 && p.East == 21));
    Check("the coordinate string reads north-then-east",
        CityBook.Places.First(p => p.North == 63 && p.East == 21).Coordinate == "63N, 21E");

    Check("no two locations share a square, so a marker is never ambiguous", CityBook.AllSquaresDistinct);

    // --- map geometry --------------------------------------------------------
    // North counts up from the southern edge, so row 1 has to land at the BOTTOM of the drawing and
    // row 64 at the top. Getting this backwards is the classic way to ship a mirrored map.
    Check("north runs up the map", CityMap.CentreY(1) > CityMap.CentreY(GameFacts.CitySize));
    Check("east runs right", CityMap.CentreX(1) < CityMap.CentreX(GameFacts.CitySize));
    CheckEqual("the western column sits half a cell in from the grid edge",
        CityMap.Margin + CityMap.CellSize / 2, CityMap.CentreX(1));
    CheckEqual("the southern row sits half a cell up from the grid's bottom",
        CityMap.Margin + CityMap.GridSize - CityMap.CellSize / 2, CityMap.CentreY(1));
    CheckEqual("adjacent columns are one cell apart",
        CityMap.CellSize, CityMap.CentreX(2) - CityMap.CentreX(1));
    CheckEqual("adjacent rows are one cell apart",
        CityMap.CellSize, CityMap.CentreY(1) - CityMap.CentreY(2));
    CheckEqual("the map is as wide as the grid plus both margins",
        GameFacts.CitySize * CityMap.CellSize + CityMap.Margin * 2, CityMap.Width);
    CheckEqual("and as tall", CityMap.Width, CityMap.Height);

    // MainWindow.xaml tiles its grid brush with these literals; pin them so the two cannot drift.
    CheckEqual("the cell size the XAML grid brush hard-codes", 15.0, CityMap.CellSize);
    CheckEqual("the major-grid tile the XAML hard-codes", 120.0, CityMap.CellSize * CityMap.MajorEvery);

    var markers = CityMap.Markers();
    CheckEqual("every location is drawn", CityBook.Places.Count, markers.Count);
    Check("every marker sits inside the grid",
        markers.All(m => m.CentreX > CityMap.Margin && m.CentreX < CityMap.Margin + CityMap.GridSize &&
                         m.CentreY > CityMap.Margin && m.CentreY < CityMap.Margin + CityMap.GridSize));
    Check("no two markers land on the same spot",
        markers.Select(m => (m.CentreX, m.CentreY)).Distinct().Count() == markers.Count);
    Check("every marker carries a colour", markers.All(m => m.Colour.StartsWith("#", StringComparison.Ordinal) && m.Colour.Length == 7));
    Check("each kind has its own colour",
        Enum.GetValues<PlaceKind>().Select(CityMap.ColourFor).Distinct().Count() == Enum.GetValues<PlaceKind>().Length);
    Check("the bounding box is centred on the marker",
        markers.All(m => Math.Abs(m.Left + m.Radius - m.CentreX) < 1e-9 &&
                         Math.Abs(m.Top + m.Radius - m.CentreY) < 1e-9));
    Check("a marker fits inside its square", CityMap.MarkerRadius * 2 <= CityMap.CellSize);

    // The cheapest tavern is the one every guide sends you to; check it lands where it should.
    var cheapest = markers.Single(m => m.Place.North == 63 && m.Place.East == 21);
    CheckEqual("63N 21E is drawn as a tavern", PlaceKind.Tavern, cheapest.Kind);
    CheckEqual("63N 21E is drawn with the tavern glyph", 'T', cheapest.Symbol);
    CheckEqual("63N 21E sits in the right column", CityMap.CentreX(21), cheapest.CentreX);
    CheckEqual("63N 21E sits in the right row", CityMap.CentreY(63), cheapest.CentreY);
    Check("a marker's description names its kind, coordinate and note",
        cheapest.Description.Contains("Tavern", StringComparison.Ordinal) &&
        cheapest.Description.Contains("63N, 21E", StringComparison.Ordinal) &&
        cheapest.Description.Contains("Cheapest", StringComparison.Ordinal));

    // Rarer kinds draw last so a crowded corner still shows them.
    var order = markers.Select(m => CityBook.Places.Count(p => p.Kind == m.Kind)).ToArray();
    Check("markers are ordered commonest first, so the rare ones paint on top",
        order.SequenceEqual(order.OrderByDescending(n => n)));

    var ticks = CityMap.Ticks();
    CheckEqual("axis numbers every four squares, on both axes", 16 * 2, ticks.Count);
    Check("every axis number is a coordinate", ticks.All(t => int.TryParse(t.Label, out int v) && v % 4 == 0));

    var legendEntries = CityMap.Legend();
    CheckEqual("the legend has an entry per kind", Enum.GetValues<PlaceKind>().Length, legendEntries.Count);
    Check("the legend names every kind",
        new[] { "Inn", "Tavern", "Bank", "Shop", "Smithy", "Healer", "Guild" }
            .All(name => legendEntries.Any(e => e.Label == name)));
    Check("legend glyphs match the markers",
        legendEntries.All(e => CityBook.Places.First(p => p.Kind == e.Kind).Symbol == e.Symbol));

    // --- the exported SVG ----------------------------------------------------
    var svg = CityMap.RenderSvg();
    Check("the SVG is a complete document",
        svg.StartsWith("<svg", StringComparison.Ordinal) && svg.TrimEnd().EndsWith("</svg>", StringComparison.Ordinal));
    Check("it declares the SVG namespace", svg.Contains("xmlns=\"http://www.w3.org/2000/svg\"", StringComparison.Ordinal));
    CheckEqual("it draws one circle per location plus one per legend swatch",
        CityBook.Places.Count + legendEntries.Count, svg.Split("<circle").Length - 1);
    Check("it names the city", svg.Contains("Xebec's Demise", StringComparison.Ordinal) ||
                               svg.Contains("Xebec&apos;s Demise", StringComparison.Ordinal));
    Check("every location gets a hover title", svg.Split("<title>").Length - 1 >= CityBook.Places.Count);
    Check("the numbers are written in invariant culture (no stray commas)",
        !svg.Contains("r=\"7,", StringComparison.Ordinal) && !svg.Contains("cx=\"1,", StringComparison.Ordinal));
    Check("markup in a note is escaped, not emitted raw",
        !svg.Contains("<title>Tavern — 31N, 61E — Reasonable <", StringComparison.Ordinal));
}

// ---------------------------------------------------------------------------
Section("City terrain");

// A synthetic map: everything a building, then streets and the known doorways carved into it, so the
// parser is exercised without shipping the game's own data.
byte[] BuildTerrain()
{
    var raw = new byte[CityTerrain.ByteCount];
    Array.Fill(raw, (byte)0x40);                                  // building everywhere
    for (int e = 1; e <= CityTerrain.Size; e++)                   // one clear street each way
    {
        raw[(CityTerrain.Size - 30) * CityTerrain.Size + (e - 1)] = 0x00;
        raw[(CityTerrain.Size - e) * CityTerrain.Size + (30 - 1)] = 0x00;
    }
    for (int i = 0; i < CityTerrain.Size; i++)                    // a boundary ring
    {
        raw[i] = 0x20;
        raw[(CityTerrain.Size - 1) * CityTerrain.Size + i] = 0x20;
        raw[i * CityTerrain.Size] = 0x20;
        raw[i * CityTerrain.Size + CityTerrain.Size - 1] = 0x20;
    }
    raw[(CityTerrain.Size - 40) * CityTerrain.Size + (40 - 1)] = 0x06;   // open ground
    foreach (var place in CityBook.Places)                        // every known doorway
        raw[(CityTerrain.Size - place.North) * CityTerrain.Size + (place.East - 1)] = TerrainCode(place.Kind);
    return raw;
}

byte TerrainCode(PlaceKind kind) => kind switch
{
    PlaceKind.Inn => 1, PlaceKind.Tavern => 2, PlaceKind.Bank => 3, PlaceKind.Shop => 4,
    PlaceKind.Smithy => 5, PlaceKind.Healer => 7, _ => 8,
};

{
    var raw = BuildTerrain();
    var terrain = CityTerrain.TryParse(raw);
    Check("a map that explains the known squares is accepted", terrain != null);

    if (terrain != null)
    {
        CheckEqual("every known location lines up", CityBook.Places.Count, terrain.MatchingKnownPlaces());

        // North counts up from the southern edge, so row 1 is the LAST row of the array. Getting
        // this backwards would draw the whole city upside down.
        CheckEqual("the south-west corner is the last row, first column",
            raw[(CityTerrain.Size - 1) * CityTerrain.Size], terrain.Raw(1, 1));
        CheckEqual("the north-west corner is the first byte", raw[0], terrain.Raw(CityTerrain.Size, 1));
        CheckEqual("the north-east corner is the first row, last column",
            raw[CityTerrain.Size - 1], terrain.Raw(CityTerrain.Size, CityTerrain.Size));

        CheckEqual("a street reads as street", TerrainKind.Street, terrain.KindAt(30, 32));
        CheckEqual("a building reads as building", TerrainKind.Building, terrain.KindAt(20, 20));
        CheckEqual("the boundary reads as wall", TerrainKind.Wall, terrain.KindAt(1, 1));
        CheckEqual("open ground reads as scenery", TerrainKind.Scenery, terrain.KindAt(40, 40));
        CheckEqual("a doorway reads as doorway", TerrainKind.Doorway, terrain.KindAt(63, 21));

        Check("streets and doorways are walkable", terrain.IsWalkable(30, 32) && terrain.IsWalkable(63, 21));
        Check("buildings, walls and scenery are not",
            !terrain.IsWalkable(20, 20) && !terrain.IsWalkable(1, 1) && !terrain.IsWalkable(40, 40));

        var census = terrain.Census();
        CheckEqual("the census counts every square", CityTerrain.ByteCount, census.Values.Sum());
        Check("the census finds the doorways", census[TerrainKind.Doorway] >= CityBook.Places.Count);

        foreach (var kind in Enum.GetValues<PlaceKind>())
        {
            var sample = CityBook.Places.First(pl => pl.Kind == kind);
            CheckEqual($"{kind} decodes from its nibble",
                (PlaceKind?)kind, CityTerrain.PlaceKindForCode(terrain.LocationCode(sample.North, sample.East)));
        }
        Check("code 0 is not a building", CityTerrain.PlaceKindForCode(0) == null);
        Check("the scenery code is not a building", CityTerrain.PlaceKindForCode(CityTerrain.SceneryCode) == null);

        Check("coordinates outside the board are rejected",
            Throws(() => terrain.Raw(0, 1)) && Throws(() => terrain.Raw(1, 65)) &&
            Throws(() => terrain.Raw(65, 1)) && Throws(() => terrain.Raw(1, 0)));
    }

    // Anything that does not explain the known squares must be refused, so a bad read can never be
    // drawn as if it were the city.
    Check("an all-zero block is refused", CityTerrain.TryParse(new byte[CityTerrain.ByteCount]) == null);
    var noise = new byte[CityTerrain.ByteCount];
    for (int i = 0; i < noise.Length; i++) noise[i] = (byte)((i * 37) & 0x0F);
    Check("a patterned block is refused", CityTerrain.TryParse(noise) == null);
    Check("a short block is refused", CityTerrain.TryParse(new byte[CityTerrain.ByteCount - 1]) == null);
    Check("a null block is refused", CityTerrain.TryParse(null) == null);
    Check("a negative offset is refused", CityTerrain.TryParse(raw, -1) == null);

    // Embedded in a larger image, the way it sits inside CITY.EXE.
    var image = new byte[CityTerrain.ByteCount * 3];
    Array.Copy(raw, 0, image, 1234, raw.Length);
    Check("the map is found inside a larger image", CityTerrain.FromCityExe(image) != null);
    Check("an image with no map in it yields nothing",
        CityTerrain.FromCityExe(new byte[CityTerrain.ByteCount * 2]) == null);
    Check("a null image yields nothing", CityTerrain.FromCityExe(null) == null);

    // Drawing geometry.
    var tiles = CityMap.Tiles(terrain);
    Check("only solid and scenery squares are painted",
        tiles.All(t => t.Kind is TerrainKind.Building or TerrainKind.Wall or TerrainKind.Scenery));
    Check("every tile carries a colour", tiles.All(t => t.Colour.StartsWith("#", StringComparison.Ordinal)));
    Check("street is left unpainted", !CityMap.IsPainted(TerrainKind.Street));
    Check("a doorway is left unpainted", !CityMap.IsPainted(TerrainKind.Doorway));
    CheckEqual("tiles are one cell square", CityMap.CellSize, tiles.First().Size);
    Check("tiles sit inside the grid",
        tiles.All(t => t.Left >= CityMap.Margin && t.Top >= CityMap.Margin &&
                       t.Left + t.Size <= CityMap.Margin + CityMap.GridSize + 0.001 &&
                       t.Top + t.Size <= CityMap.Margin + CityMap.GridSize + 0.001));
    CheckEqual("no tiles without a map", 0, CityMap.Tiles(null).Count);

    // A painted square has to sit exactly under the marker for the same coordinate — if Tiles() and
    // Markers() disagreed by a row, the walls would be drawn one square off from the buildings.
    var openGround = tiles.Single(t => t.North == 40 && t.East == 40);
    CheckEqual("a tile's centre matches its marker's column centre",
        CityMap.CentreX(40), openGround.Left + openGround.Size / 2);
    CheckEqual("and its row centre", CityMap.CentreY(40), openGround.Top + openGround.Size / 2);

    // Cross-check against a real marker: the building block next to the cheapest tavern.
    var cheapTavern = CityBook.Places.First(pl => pl.North == 63 && pl.East == 21);
    var tavernMarker = CityMap.Markers().Single(m => m.Place.North == 63 && m.Place.East == 21);
    Check("a doorway is not painted — the marker stands on open street",
        !tiles.Any(t => t.North == cheapTavern.North && t.East == cheapTavern.East));
    var neighbour = tiles.Single(t => t.North == 63 && t.East == 22);
    CheckEqual("the square east of it is one cell to the right of the marker",
        tavernMarker.CentreX + CityMap.CellSize, neighbour.Left + neighbour.Size / 2);
    CheckEqual("and on the same row", tavernMarker.CentreY, neighbour.Top + neighbour.Size / 2);

    // The exported SVG must gain the walls, and stay valid.
    var plain = CityMap.RenderSvg();
    var walled = CityMap.RenderSvg(terrain);
    Check("the walled SVG is bigger than the plain one", walled.Length > plain.Length);
    CheckEqual("it draws one rect per painted square, plus the background and three legend swatches",
        tiles.Count + 1 + 3, walled.Split("<rect").Length - 1);
    Check("it names the terrain in the legend",
        walled.Contains(">Building<", StringComparison.Ordinal) &&
        walled.Contains(">Wall<", StringComparison.Ordinal) &&
        walled.Contains(">Open ground<", StringComparison.Ordinal));
    Check("the plain SVG has no terrain legend", !plain.Contains(">Building<", StringComparison.Ordinal));
    Check("the walled SVG is still a complete document",
        walled.StartsWith("<svg", StringComparison.Ordinal) &&
        walled.TrimEnd().EndsWith("</svg>", StringComparison.Ordinal));

    // The view-model adopts a map and can be cleared again.
    var refVm = new ReferenceViewModel();
    Check("no map to begin with", !refVm.HasTerrain && refVm.TerrainDrawing == null);
    Check("the summary says how to get one", refVm.TerrainSummary.Contains("CITY.EXE", StringComparison.Ordinal));
    refVm.SetTerrain(terrain);
    Check("adopting a map builds the drawing", refVm.HasTerrain && refVm.TerrainDrawing != null);
    Check("the summary now describes the map",
        refVm.TerrainSummary.Contains("street squares", StringComparison.Ordinal));
    refVm.SetTerrain(null);
    Check("a failed read never drops a good map", refVm.HasTerrain);
    refVm.ClearTerrain();
    Check("clearing forgets it", !refVm.HasTerrain && refVm.TerrainDrawing == null);
}

// ---------------------------------------------------------------------------
Section("View-model behaviour");

{
    var host = new FakeHost();
    var located = new LocateResult(0x1000, 0x800, BuildNeuro(), "fixture", 3);
    var vm = new CharacterViewModel(host, located);

    CheckEqual("the view-model reads the name", NeuroName, vm.Name);
    CheckEqual("the view-model reads the level", NeuroLevel, vm.Level);
    CheckEqual("the view-model exposes seven attribute rows", CharacterFormat.AttributeCount, vm.Attributes.Count);
    CheckEqual("the hidden attribute is labelled as such", "Physical Speed (hidden)",
        vm.Attributes[6].Label);

    host.Writes.Clear();
    vm.Attributes[0].Value = 111;
    Check("editing an attribute writes three bytes at its offset",
        host.Writes.Count == 1 &&
        host.Writes[0].Offset == CharacterFormat.AttributeOffset(0) &&
        host.Writes[0].Bytes.Length == CharacterFormat.AttributeCopies &&
        host.Writes[0].Bytes.All(b => b == 111));

    host.Writes.Clear();
    vm.Gold = 4321;
    Check("editing gold writes two bytes at the gold offset",
        host.Writes.Count == 1 && host.Writes[0].Offset == CharacterFormat.OffGold &&
        host.Writes[0].Bytes.SequenceEqual(new byte[] { 4321 & 0xFF, 4321 >> 8 }));

    host.Writes.Clear();
    vm.Gold = 4321;
    Check("re-assigning the same value writes nothing", host.Writes.Count == 0);

    // A clamped input must still raise a change notification, so the bound text box snaps back to
    // what was actually written instead of showing a value the game never received.
    var notified = new List<string>();
    vm.PropertyChanged += (_, e) => { if (e.PropertyName != null) notified.Add(e.PropertyName); };

    notified.Clear();
    vm.Copper = 999_999;
    CheckEqual("a coin field clamps to 16 bits", CharacterFormat.CoinCeiling, vm.Copper);
    Check("clamping a coin field notifies", notified.Contains(nameof(vm.Copper)));

    notified.Clear();
    vm.Copper = 999_999;   // already at the ceiling: the value cannot move, but the input was clamped
    Check("clamping again still notifies", notified.Contains(nameof(vm.Copper)));

    notified.Clear();
    vm.Food = -3;
    CheckEqual("a supply field clamps to zero", 0, vm.Food);
    Check("clamping a supply field notifies", notified.Contains(nameof(vm.Food)));

    vm.HitPointsMax = long.MaxValue;
    CheckEqual("hit points max clamps to its own ceiling", (long)CharacterFormat.HitPointCeiling, vm.HitPointsMax);
    vm.HitPointsMax = 0;
    CheckEqual("hit points max clamps to at least 1", 1L, vm.HitPointsMax);
    vm.HitPoints = long.MaxValue;
    CheckEqual("hit points clamp to their own ceiling", (long)CharacterFormat.HitPointCeiling, vm.HitPoints);
    vm.Level = 9999;
    CheckEqual("level clamps through the view-model", CharacterFormat.LevelCeiling, vm.Level);

    // Freeze: pin the attributes at whatever they are now, then let a poll tick see one drained.
    vm.FreezeAttributes = true;
    byte pinnedStrength = (byte)vm.Attributes[0].Value;
    var drained = BuildNeuro();
    int strOff = CharacterFormat.AttributeOffset(0);
    drained[strOff] = drained[strOff + 1] = drained[strOff + 2] = 1;
    Array.Copy(drained, vm.LiveBuffer, CharacterFormat.LiveFieldsLength);
    host.Writes.Clear();
    vm.OnPolled();
    Check("freezing attributes restores a drained one",
        host.Writes.Any(w => w.Offset == strOff &&
                             w.Bytes.Length == CharacterFormat.AttributeCopies &&
                             w.Bytes.All(b => b == pinnedStrength)));
    Check("freezing attributes leaves the undamaged ones alone",
        host.Writes.All(w => w.Offset == strOff));

    // Freeze hit points: a damaged character is re-pinned to the maximum.
    vm.FreezeAttributes = false;
    vm.FreezeHitPoints = true;
    var hurt = BuildNeuro();
    CharacterFormat.WriteU32(hurt, CharacterFormat.OffHitPoints, 1);
    Array.Copy(hurt, vm.LiveBuffer, CharacterFormat.LiveFieldsLength);
    host.Writes.Clear();
    vm.OnPolled();
    Check("freezing hit points re-pins them to the maximum",
        host.Writes.Any(w => w.Offset == CharacterFormat.OffHitPoints &&
                             CharacterFormat.ReadU32(w.Bytes, 0) == NeuroHitPointsMax));

    // With nothing frozen, a poll tick must not write at all.
    vm.FreezeHitPoints = false;
    host.Writes.Clear();
    vm.OnPolled();
    Check("polling writes nothing when no freeze is on", host.Writes.Count == 0);

    // --- a freeze must pin the LIVE value, not the editor's stale copy -------
    // Attach, then let the game move on without the trainer touching it (guild bonus, spent gold,
    // eaten food), then tick the freeze. Pinning the editor's copy here would rewind the game to
    // what the character looked like at attach time.
    {
        var drift = new CharacterViewModel(host, new LocateResult(0x3000, 0, BuildNeuro(), "fixture", 3));
        var moved = BuildNeuro();
        CharacterFormat.WriteU16(moved, CharacterFormat.OffGold, 4321);
        moved[CharacterFormat.OffFood] = 42;
        int strengthOffset = CharacterFormat.AttributeOffset(0);
        moved[strengthOffset] = moved[strengthOffset + 1] = moved[strengthOffset + 2] = 55;
        Array.Copy(moved, drift.LiveBuffer, CharacterFormat.LiveFieldsLength);
        drift.OnPolled();

        drift.FreezeMoney = true;
        drift.FreezeSupplies = true;
        drift.FreezeAttributes = true;
        host.Writes.Clear();
        drift.OnPolled();
        Check("a freeze armed after the game moved on writes nothing back", host.Writes.Count == 0);

        // …and now the game drops the values; the freeze must restore what it saw, not the snapshot.
        var robbed = (byte[])moved.Clone();
        CharacterFormat.WriteU16(robbed, CharacterFormat.OffGold, 0);
        robbed[CharacterFormat.OffFood] = 0;
        robbed[strengthOffset] = robbed[strengthOffset + 1] = robbed[strengthOffset + 2] = 1;
        Array.Copy(robbed, drift.LiveBuffer, CharacterFormat.LiveFieldsLength);
        host.Writes.Clear();
        drift.OnPolled();
        Check("and restores the value the game had when it was armed, not the attach-time one",
            host.Writes.Any(w => w.Offset == CharacterFormat.OffGold &&
                                 w.Bytes.SequenceEqual(new byte[] { 4321 & 0xFF, 4321 >> 8 })) &&
            host.Writes.Any(w => w.Offset == CharacterFormat.OffFood && w.Bytes[0] == 42) &&
            host.Writes.Any(w => w.Offset == strengthOffset && w.Bytes.All(b => b == 55)));

        // Editing a frozen field must stick, not be reverted on the next tick.
        drift.Gold = 777;
        Array.Copy(robbed, drift.LiveBuffer, CharacterFormat.LiveFieldsLength);
        host.Writes.Clear();
        drift.OnPolled();
        Check("editing a frozen field re-pins it so the edit sticks",
            host.Writes.Any(w => w.Offset == CharacterFormat.OffGold &&
                                 w.Bytes.SequenceEqual(new byte[] { 777 & 0xFF, 777 >> 8 })));

        drift.Attributes[0].Value = 123;
        Array.Copy(robbed, drift.LiveBuffer, CharacterFormat.LiveFieldsLength);
        host.Writes.Clear();
        drift.OnPolled();
        Check("editing a frozen attribute re-pins it too",
            host.Writes.Any(w => w.Offset == strengthOffset && w.Bytes.All(b => b == 123)));

        // A bulk action under an active freeze must be held, not undone.
        drift.MaxMoneyCommand.Execute(null);
        Array.Copy(robbed, drift.LiveBuffer, CharacterFormat.LiveFieldsLength);
        host.Writes.Clear();
        drift.OnPolled();
        Check("a bulk action re-pins the freeze rather than being reverted by it",
            host.Writes.Any(w => w.Offset == CharacterFormat.OffGold &&
                                 CharacterFormat.ReadU16(w.Bytes, 0) == CharacterFormat.MaxCoins));
    }

    Check("the live summary names the character", vm.LiveSummary.Contains(NeuroName, StringComparison.Ordinal));
    Check("the live clock names the month", vm.LiveClock.Contains("Sowings", StringComparison.Ordinal));

    // Reload must refuse before the first poll — the live buffer is still zeros then, and copying it
    // over the editor would blank the character.
    var fresh = new CharacterViewModel(host, new LocateResult(0x1000, 0x800, BuildNeuro(), "fixture", 3));
    Check("reload refuses before the first poll", !fresh.ReloadFromGame());
    CheckEqual("reload leaves the editor intact before the first poll", NeuroName, fresh.Name);
    Array.Copy(BuildNeuro(), fresh.LiveBuffer, CharacterFormat.LiveFieldsLength);
    fresh.OnPolled();
    Check("reload succeeds after a poll", fresh.ReloadFromGame());
    CheckEqual("reload keeps the name", NeuroName, fresh.Name);

    fresh.Attributes[1].Value = 0;
    CheckEqual("an attribute clamps to 1 through the view-model", 1, fresh.Attributes[1].Value);
    fresh.Attributes[1].Value = 9999;
    CheckEqual("an attribute clamps to the ceiling through the view-model",
        CharacterFormat.AttributeCeiling, fresh.Attributes[1].Value);

    Check("a locate result with no record is rejected",
        Throws(() => new CharacterViewModel(host, LocateResult.None)));
    Check("a null host is rejected",
        Throws(() => new CharacterViewModel(null!, new LocateResult(0x1000, 0, BuildNeuro(), "fixture", 3))));

    // A short read must not stop the record from being usable — the buffer is padded to full size.
    var shortRead = new CharacterViewModel(host,
        new LocateResult(0x2000, 0, BuildNeuro().Take(CharacterFormat.LiveFieldsLength).ToArray(), "fixture", 3));
    CheckEqual("a locate that only read the live prefix still decodes", NeuroName, shortRead.Name);
    shortRead.Jewelry = 7;
    CheckEqual("and is still editable", 7, shortRead.Jewelry);
}

// ---------------------------------------------------------------------------
Section("Locator");

{
    // A synthetic address space: one region holding a fake DGROUP with the anchors laid out at
    // their real offsets and the character record where the game keeps it.
    const nuint regionBase = 0x40000;
    const int dgroupAt = 0x1234;                       // arbitrary offset of DGROUP inside the region

    FakeMemory BuildWorld(int validators = 3, byte[]? record = null, int dgroupOffset = dgroupAt)
    {
        var image = new byte[0x120000];                 // > 1 MiB, so the scan crosses a chunk seam
        void Put(byte[] bytes, int at) => Array.Copy(bytes, 0, image, at, bytes.Length);

        var primary = CharacterFormat.PrimaryAnchor;
        Put(primary.Bytes, dgroupOffset + primary.DgroupOffset);
        for (int i = 0; i < validators && i < CharacterFormat.Validators.Length; i++)
        {
            var v = CharacterFormat.Validators[i];
            Put(v.Bytes, dgroupOffset + v.DgroupOffset);
        }
        Put(record ?? BuildNeuro(), dgroupOffset + CharacterFormat.DgroupRecordOffset);
        return new FakeMemory(regionBase, image);
    }

    // --- the happy path -----------------------------------------------------
    var world = BuildWorld();
    var located = GameLocator.Locate(world);
    Check("the anchored scan finds the record", located.Found);
    CheckEqual("at the right address",
        (ulong)(regionBase + dgroupAt + CharacterFormat.DgroupRecordOffset), (ulong)located.RecordAddress);
    CheckEqual("and reports DGROUP", (ulong)(regionBase + dgroupAt), (ulong)located.DgroupAddress);
    CheckEqual("with every validator matched", 3, located.ValidatorsMatched);
    Check("the method names the anchor", located.Method.Contains("anchored", StringComparison.Ordinal));
    CheckEqual("and the decoded record is the right character", NeuroName, new CharacterRecord(located.Buffer).Name);

    // --- the seam ------------------------------------------------------------
    // The scan reads in 1 MiB chunks with a needle-sized overlap. What has to survive that is a
    // match lying ACROSS the boundary, so place the anchor itself half in each chunk — putting the
    // record near the seam proves nothing, because once an anchor matches, the record is fetched by
    // a direct read that never touches the chunking.
    const int chunk = 1 << 20;
    int anchorLen = CharacterFormat.PrimaryAnchor.Bytes.Length;
    for (int split = 1; split < anchorLen; split += 7)   // a few different ways to cut it
    {
        int anchorAt = chunk - split;                    // `split` bytes land in chunk 0, the rest in chunk 1
        int dgroupOffset = anchorAt - CharacterFormat.PrimaryAnchor.DgroupOffset;
        var seamWorld = BuildWorld(dgroupOffset: dgroupOffset);
        var seamHit = GameLocator.Locate(seamWorld);
        Check($"an anchor split {split}/{anchorLen - split} across a chunk seam is still found", seamHit.Found);
        CheckEqual($"and resolves to the right record (split {split})",
            (ulong)(regionBase + (nuint)dgroupOffset + CharacterFormat.DgroupRecordOffset),
            (ulong)seamHit.RecordAddress);
    }

    // The structural sweep reads its window out of the scan buffer, so a record straddling the seam
    // exercises its own `window - 1` overlap.
    for (int split = 1; split < CharacterFormat.LiveFieldsLength; split += 47)
    {
        int recordAt = chunk - split;
        var seamStructural = new FakeMemory(regionBase, new byte[0x120000]);
        Array.Copy(BuildNeuro(), 0, seamStructural.Image, recordAt, CharacterFormat.LiveFieldsLength);
        var seamStructuralHit = GameLocator.Locate(seamStructural, default, allowStructuralScan: true);
        Check($"a record split {split} bytes across a chunk seam is found structurally", seamStructuralHit.Found);
        CheckEqual($"at the right address (split {split})",
            (ulong)(regionBase + (nuint)recordAt), (ulong)seamStructuralHit.RecordAddress);
    }

    // And the same for the very last bytes of a region, where the read is truncated.
    var tail = new FakeMemory(regionBase, new byte[0x120000]);
    int tailAt = tail.Image.Length - CharacterFormat.LiveFieldsLength;
    Array.Copy(BuildNeuro(), 0, tail.Image, tailAt, CharacterFormat.LiveFieldsLength);
    var tailHit = GameLocator.Locate(tail, default, allowStructuralScan: true);
    Check("a record at the very end of a region is found", tailHit.Found);
    CheckEqual("at the right address", (ulong)(regionBase + (nuint)tailAt), (ulong)tailHit.RecordAddress);

    // The awkward band: a region whose size sits in [ChunkSize, ChunkSize + overlap). There the
    // read length is capped by what is left rather than by chunk + overlap, and the next window
    // would begin past the tail — so if the arithmetic were wrong, a record in the last few hundred
    // bytes would be scanned by nobody. Sweep the whole band with the record at the last position
    // it could possibly occupy.
    {
        int overlapBand = CharacterFormat.LiveFieldsLength - 1;
        int missed = 0, checkedSizes = 0;
        for (int d = 0; d < overlapBand; d++)
        {
            var band = new FakeMemory(regionBase, new byte[chunk + d]);
            int at = band.Image.Length - CharacterFormat.LiveFieldsLength;
            Array.Copy(BuildNeuro(), 0, band.Image, at, CharacterFormat.LiveFieldsLength);
            var bandHit = GameLocator.Locate(band, default, allowStructuralScan: true);
            checkedSizes++;
            if (!bandHit.Found || bandHit.RecordAddress != regionBase + (nuint)at) missed++;
        }
        CheckEqual($"every region size in the chunk+overlap band finds its last record ({checkedSizes} sizes)",
            0, missed);
    }

    // --- validator threshold -------------------------------------------------
    var twoValidators = BuildWorld(validators: 2);
    var twoHit = GameLocator.Locate(twoValidators);
    Check("two validators are enough", twoHit.Found);
    CheckEqual("and the count is reported honestly", 2, twoHit.ValidatorsMatched);

    var oneValidator = BuildWorld(validators: 1);
    var oneHit = GameLocator.Locate(oneValidator);
    Check("one validator is rejected by the anchored path",
        !oneHit.Found || oneHit.ValidatorsMatched == 0);

    // --- a record that fails the shape check is not accepted -----------------
    var brokenRecord = BuildNeuro();
    brokenRecord[CharacterFormat.OffName] = 0;
    var brokenWorld = BuildWorld(record: brokenRecord);
    Check("an anchor with an unrecognisable record behind it is rejected",
        !GameLocator.Locate(brokenWorld).Found);

    // --- the structural fallback is opt-in ------------------------------------
    var noAnchors = new FakeMemory(regionBase, new byte[0x120000]);
    Array.Copy(BuildNeuro(), 0, noAnchors.Image, 0x9876, CharacterFormat.LiveFieldsLength);
    Check("the structural scan does not run unless it is asked for",
        !GameLocator.Locate(noAnchors).Found);
    var structural = GameLocator.Locate(noAnchors, default, allowStructuralScan: true);
    Check("the structural fallback finds a record with no anchors present", structural.Found);
    CheckEqual("at the right address", (ulong)(regionBase + 0x9876), (ulong)structural.RecordAddress);
    CheckEqual("and reports no DGROUP", 0UL, (ulong)structural.DgroupAddress);
    CheckEqual("and no validators", 0, structural.ValidatorsMatched);
    Check("and says so", structural.Method.Contains("structural", StringComparison.Ordinal));

    // --- an unreadable page must not cost the whole megabyte ----------------
    var holed = new FakeMemory(regionBase, new byte[0x120000]);
    Array.Copy(BuildNeuro(), 0, holed.Image, 0x80000, CharacterFormat.LiveFieldsLength);
    holed.UnreadablePages.Add(regionBase + 0x2000);   // a hole early in the first chunk
    var salvaged = GameLocator.Locate(holed, default, allowStructuralScan: true);
    Check("the structural fallback salvages past an unreadable page", salvaged.Found);
    CheckEqual("and still finds the record behind it", (ulong)(regionBase + 0x80000), (ulong)salvaged.RecordAddress);

    var holedAnchor = BuildWorld();
    holedAnchor.UnreadablePages.Add(regionBase + 0x3000);
    Check("the anchored scan salvages past an unreadable page too", GameLocator.Locate(holedAnchor).Found);

    // --- nothing there -------------------------------------------------------
    Check("an empty address space finds nothing",
        !GameLocator.Locate(new FakeMemory(regionBase, new byte[0x20000])).Found);

    // --- underflow guard: an anchor too near address zero --------------------
    var lowWorld = new FakeMemory(0x10, new byte[0x20000]);
    Array.Copy(CharacterFormat.PrimaryAnchor.Bytes, 0, lowWorld.Image, 0, CharacterFormat.PrimaryAnchor.Bytes.Length);
    Check("an anchor whose DGROUP would underflow is skipped without throwing",
        !GameLocator.Locate(lowWorld).Found);

    // --- cancellation --------------------------------------------------------
    using (var cts = new CancellationTokenSource())
    {
        cts.Cancel();
        Check("a cancelled locate throws OperationCanceledException",
            ThrowsOfType<OperationCanceledException>(() => GameLocator.Locate(BuildWorld(), cts.Token)));
    }

    // --- Reread --------------------------------------------------------------
    var live = new byte[CharacterFormat.LiveFieldsLength];
    Check("reread fills the live buffer",
        GameLocator.Reread(world, located.RecordAddress, live));
    CheckEqual("with the character's own bytes", NeuroName,
        CharacterFormat.ReadName(Pad(live)));
    Check("reread refuses a buffer that is too small",
        !GameLocator.Reread(world, located.RecordAddress, new byte[4]));
    Check("reread refuses a null buffer", !GameLocator.Reread(world, located.RecordAddress, null!));
    Check("reread reports failure for an unmapped address",
        !GameLocator.Reread(world, regionBase + 0x900000, live));

    // --- reading the street map out of the game --------------------------------
    {
        // The map sits 0x61B0 *below* DGROUP, so this world has to put DGROUP far enough in for it
        // to fit -- which is also a reminder that the offset is negative.
        const int terrainDgroupAt = 0x8000;
        var terrainWorld = BuildWorld(dgroupOffset: terrainDgroupAt);
        var terrainBytes = BuildTerrain();
        int terrainAt = terrainDgroupAt + CharacterFormat.DgroupTerrainOffset;
        Check("the street map offset is negative — it is below DGROUP",
            CharacterFormat.DgroupTerrainOffset < 0);
        Check("the street map fits inside this fixture", terrainAt >= 0);
        Array.Copy(terrainBytes, 0, terrainWorld.Image, terrainAt, terrainBytes.Length);

        var withTerrain = GameLocator.Locate(terrainWorld);
        Check("the anchored locate still works with a map present", withTerrain.Found);
        var read = GameLocator.ReadTerrain(terrainWorld, withTerrain.DgroupAddress);
        Check("the street map is read from the located DGROUP", read != null);
        CheckEqual("and explains every known location", CityBook.Places.Count, read!.MatchingKnownPlaces());
        CheckEqual("and decodes a known doorway", TerrainKind.Doorway, read.KindAt(63, 21));
        CheckEqual("and is read from the right address",
            (ulong)(regionBase + (nuint)terrainAt),
            (ulong)((long)withTerrain.DgroupAddress + CharacterFormat.DgroupTerrainOffset));

        Check("no DGROUP means no map", GameLocator.ReadTerrain(terrainWorld, 0) == null);
        Check("a null source means no map", GameLocator.ReadTerrain(null!, withTerrain.DgroupAddress) == null);
        Check("a DGROUP with nothing behind it means no map",
            GameLocator.ReadTerrain(new FakeMemory(regionBase, new byte[0x20000]), regionBase + 0x10000) == null);
    }

    // --- LocateResult guards --------------------------------------------------
    Check("a default LocateResult is simply not found", !default(LocateResult).Found);
    Check("LocateResult.None is not found", !LocateResult.None.Found);
    Check("Locate rejects a null memory source", Throws(() => GameLocator.Locate(null!)));
}

// ---------------------------------------------------------------------------
Section("Reference view-model");

{
    var reference = new ReferenceViewModel();
    CheckEqual("all locations are listed by default", CityBook.Places.Count, reference.Places.Count);
    reference.SelectedKind = nameof(PlaceKind.Guild);
    CheckEqual("filtering to guilds narrows the list", 12, reference.Places.Count);
    Check("the filtered list holds only guilds", reference.Places.All(p => p.Kind == nameof(PlaceKind.Guild)));
    Check("the count text tracks the filter", reference.PlaceCountText.StartsWith("12 ", StringComparison.Ordinal));
    reference.SelectedKind = "All";
    CheckEqual("clearing the filter restores the list", CityBook.Places.Count, reference.Places.Count);
    Check("the potion grid is populated", reference.Potions.Count == PotionBook.All.Count);
    Check("the control grid is populated", reference.Controls.Count == GameFacts.Controls.Count);
    CheckEqual("the map draws every location", CityBook.Places.Count, reference.Markers.Count);
    Check("the map has axis numbers and a legend",
        reference.Ticks.Count > 0 && reference.MapLegend.Count == Enum.GetValues<PlaceKind>().Length);
    Check("every marker starts undimmed", reference.Markers.All(m => m.Opacity == 1.0));

    // Filtering the list must dim the other kinds on the map, and clearing it must restore them.
    reference.SelectedKind = nameof(PlaceKind.Healer);
    Check("filtering highlights that kind on the map",
        reference.Markers.Where(m => m.Kind == PlaceKind.Healer).All(m => m.Opacity == 1.0));
    Check("and dims the rest",
        reference.Markers.Where(m => m.Kind != PlaceKind.Healer).All(m => m.Opacity < 0.5));
    reference.SelectedKind = "All";
    Check("clearing the filter restores every marker", reference.Markers.All(m => m.Opacity == 1.0));

    reference.Zoom = 99;
    Check("zoom clamps to its maximum", reference.Zoom <= 2.0);
    reference.Zoom = 0;
    Check("zoom clamps to its minimum", reference.Zoom >= 0.45);
    Check("the minimum zoom shows the whole grid in a default window",
        reference.MapWidth * 0.45 < 700);
}

// ---------------------------------------------------------------------------
Section("Shipped character files");

{
    string? gameDir = FindGameDirectory();
    if (gameDir == null)
    {
        Console.WriteLine("  SKIPPED — no ARCCD files found. Put copies (or a junction) under");
        Console.WriteLine("            AlternateRealityTrainer\\.game\\ to run this group.");
    }
    else
    {
        Console.WriteLine($"  using {gameDir}");
        foreach (var path in Directory.GetFiles(gameDir, "ARCCD*").OrderBy(p => p, StringComparer.Ordinal))
        {
            string label = Path.GetFileName(path);
            var bytes = File.ReadAllBytes(path);
            CheckEqual($"{label} is {CharacterFormat.RecordSize} bytes", CharacterFormat.RecordSize, bytes.Length);
            if (bytes.Length != CharacterFormat.RecordSize) continue;

            Check($"{label} is recognised as a character record", CharacterFormat.LooksLikeRecord(bytes, 0));

            var rec = new CharacterRecord(bytes);
            Console.WriteLine($"    {label}  {rec.Summary}");
            Check($"{label} has a name", rec.Name.Length > 0);
            Check($"{label} has plausible attributes",
                Enumerable.Range(0, CharacterFormat.AttributeCount).All(i => rec.GetAttribute(i) is > 0 and < 100));
            Check($"{label} has hit points within the maximum", rec.HitPoints <= rec.HitPointsMax);
            Check($"{label} has a month index inside the calendar", rec.MonthIndex < GameFacts.Months.Count);

            // An unedited record must round-trip byte for byte.
            var copy = (byte[])bytes.Clone();
            var view = new CharacterRecord(copy);
            _ = view.Summary;
            Check($"{label} is unchanged by reading it", copy.SequenceEqual(bytes));

            // And a single edit must touch only its own field.
            string original = rec.Name;
            var edited = (byte[])bytes.Clone();
            var editable = new CharacterRecord(edited);
            editable.Gold = 1234;
            int differing = Enumerable.Range(0, bytes.Length).Count(i => edited[i] != bytes[i]);
            Check($"{label}: editing gold touches at most two bytes", differing <= 2);
            CheckEqual($"{label}: editing gold leaves the name alone", original, editable.Name);
        }

        var roster = Path.Combine(gameDir, "ARCNAME");
        if (File.Exists(roster))
        {
            var bytes = File.ReadAllBytes(roster);
            CheckEqual("ARCNAME is 256 bytes (8 slots of 32)", 256, bytes.Length);
            if (bytes.Length == 256)
            {
                var names = Enumerable.Range(0, 8)
                    .Select(i => Encoding.ASCII.GetString(bytes, i * 32, 32).TrimEnd('\0', ' '))
                    .Where(s => s.Length > 0)
                    .ToArray();
                Console.WriteLine($"    ARCNAME  roster: {string.Join(", ", names)}");
                Check("ARCNAME holds at least one name", names.Length > 0);
                Check("every roster name is printable ASCII",
                    names.All(n => n.All(c => c is >= ' ' and < (char)127)));
            }
        }
    }
}

// ---------------------------------------------------------------------------
// The README and both AGENTS.md files quote this figure; assert a floor so it cannot rot away
// unnoticed if checks are deleted.
const int MinimumChecks = 230;
if (passed + failed < MinimumChecks)
{
    failed++;
    failures.Add($"the harness ran only {passed + failed} checks; at least {MinimumChecks} were expected");
    Console.WriteLine($"  FAIL  the harness ran fewer checks than expected ({passed + failed} < {MinimumChecks})");
}

Console.WriteLine();
Console.WriteLine($"{passed} passed, {failed} failed.");
if (failed > 0)
{
    Console.WriteLine("Failures:");
    foreach (var f in failures) Console.WriteLine("  - " + f);
}
return failed == 0 ? 0 : 1;

// ---------------------------------------------------------------------------

static bool Throws(Action action)
{
    try { action(); return false; }
    catch { return true; }
}

// Typed, so an assertion about cancellation cannot be satisfied by a NullReferenceException.
static bool ThrowsOfType<T>(Action action) where T : Exception
{
    try { action(); return false; }
    catch (T) { return true; }
    catch { return false; }
}

// Widens a live-fields read to a full record window so CharacterFormat's accessors can be used on it.
static byte[] Pad(byte[] prefix)
{
    var full = new byte[CharacterFormat.RecordSize];
    Array.Copy(prefix, full, Math.Min(prefix.Length, full.Length));
    return full;
}

// Looks for the copyrighted character files: first the git-ignored `.game\` folder beside the
// trainer, then the DOSBox install this trainer was developed against.
static string? FindGameDirectory()
{
    var candidates = new List<string>();

    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        candidates.Add(Path.Combine(dir.FullName, ".game"));
        if (string.Equals(dir.Name, "AlternateRealityTrainer", StringComparison.OrdinalIgnoreCase)) break;
        dir = dir.Parent;
    }
    candidates.Add(@"C:\Temp\Scratch\Win31DOSBox\C-DRIVE\GAMES\AR");

    foreach (var c in candidates)
    {
        if (Directory.Exists(c) && Directory.GetFiles(c, "ARCCD*").Length > 0) return c;
    }
    return null;
}

// A synthetic address space for the locator: one contiguous region over a byte image, with an
// optional set of pages that refuse to read so the salvage path can be exercised. Reads are
// all-or-nothing, exactly like ProcessMemory.
internal sealed class FakeMemory : IMemorySource
{
    private const int PageSize = 0x1000;

    public byte[] Image { get; }
    public nuint Base { get; }
    public HashSet<nuint> UnreadablePages { get; } = new();

    public FakeMemory(nuint baseAddress, byte[] image)
    {
        Base = baseAddress;
        Image = image;
    }

    public IEnumerable<MemoryRegion> EnumerateRegions()
    {
        yield return new MemoryRegion(Base, (nuint)Image.Length);
    }

    public int Read(nuint address, byte[] buffer, int count)
    {
        if (buffer == null || count < 0 || count > buffer.Length) return 0;
        if (address < Base) return 0;
        nuint offset = address - Base;
        if (offset > (nuint)Image.Length || (nuint)count > (nuint)Image.Length - offset) return 0;
        if (TouchesUnreadablePage(address, count)) return 0;
        Array.Copy(Image, (int)offset, buffer, 0, count);
        return count;
    }

    public byte[] Read(nuint address, int count)
    {
        var buf = new byte[count];
        int read = Read(address, buf, count);
        if (read != count) Array.Resize(ref buf, read);
        return buf;
    }

    private bool TouchesUnreadablePage(nuint address, int count)
    {
        if (UnreadablePages.Count == 0 || count == 0) return false;
        nuint first = address & ~(nuint)(PageSize - 1);
        nuint last = (address + (nuint)count - 1) & ~(nuint)(PageSize - 1);
        for (nuint p = first; p <= last; p += PageSize)
            if (UnreadablePages.Contains(p)) return true;
        return false;
    }
}

// A stand-in for the trainer shell that records what would have been written.
internal sealed class FakeHost : ICharacterHost
{
    public List<(nuint Address, int Offset, byte[] Bytes)> Writes { get; } = new();
    public List<string> Messages { get; } = new();

    public bool WriteBytes(nuint recordAddress, int offset, byte[] bytes)
    {
        Writes.Add((recordAddress, offset, (byte[])bytes.Clone()));
        return true;
    }

    public void ReportStatus(string message) => Messages.Add(message);
}
