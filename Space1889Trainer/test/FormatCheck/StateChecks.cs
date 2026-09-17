using System.IO;
using System.Text;
using Space1889Trainer.Game;

namespace Space1889Trainer.FormatCheck;

/// <summary>A synthetic state block shaped like the shipped default party.</summary>
internal static class Synthetic
{
    public static readonly string[] Names = { "PROF WELLS", "LADY MARIE", "ELIZABETH", "SYDNEY WEBB", "SIR WALTER" };

    public static byte[] Block()
    {
        var b = new byte[StateFormat.BlockLength];
        for (int slot = 0; slot < StateFormat.CharacterSlots; slot++)
        {
            int rec = StateFormat.RecordOffset(slot);
            Put32(b, rec + StateFormat.WealthOffset, 24000 * (slot + 1));
            Put32(b, rec + StateFormat.IncomeOffset, 800 * (slot + 1));
            Put32(b, rec + StateFormat.BodyWeightOffset, 200);
            b[rec + StateFormat.MaxHealthOffset] = 9;
            b[rec + StateFormat.HealthOffset] = 8;
            var attrs = new byte[] { 5, 5, 4, 5, 4, 4 };
            Array.Copy(attrs, 0, b, rec + StateFormat.AttributesOffset, 6);
            for (int s = 0; s < StateFormat.SkillCount; s++) b[rec + StateFormat.SkillsOffset + s] = (byte)(s % 4);
            b[rec + StateFormat.Career1IdOffset] = 26;
            Ascii(b, rec + StateFormat.Career1NameOffset, "INVENTOR");
            Ascii(b, rec + StateFormat.NameOffset, Names[slot]);
            b[rec + StateFormat.SexOffset] = (byte)(slot is 1 or 2 ? 'f' : 'm');
        }
        Ascii(b, StateFormat.PartyAccountNameOffset, StateFormat.PartyAccountName);
        for (int i = 0; i < 5; i++) b[StateFormat.MarchingOrderOffset + i] = (byte)i;
        b[StateFormat.PlanetOffset] = 1;
        b[StateFormat.AreaOffset] = 1;
        b[StateFormat.MapOffset] = 3;
        Put16(b, StateFormat.FoodOffset, 100);
        Put16(b, StateFormat.RowOffset, 58);
        Put16(b, StateFormat.ColumnOffset, 36);
        Put16(b, StateFormat.LitOffset, 1);
        Ascii(b, StateFormat.AreaNameOffset, "LONDON");
        return b;
    }

    public static void Put16(byte[] b, int o, int v) { b[o] = (byte)v; b[o + 1] = (byte)(v >> 8); }

    public static void Put32(byte[] b, int o, int v) { Put16(b, o, v); Put16(b, o + 2, v >> 16); }

    public static void Ascii(byte[] b, int o, string s) => Encoding.ASCII.GetBytes(s).CopyTo(b, o);
}

/// <summary>A target that counts the writes it receives.</summary>
internal sealed class RecordingTarget : IStateTarget
{
    private readonly BufferTarget _inner;

    public RecordingTarget(byte[] bytes) => _inner = new BufferTarget(bytes);

    public List<(int Offset, int Length)> Writes { get; } = new();

    /// <summary>The 0-based number of the one write to refuse (−1 refuses none) — for testing a failure mid-sequence.</summary>
    public int RefuseWriteNumber { get; set; } = -1;

    public BufferTarget Inner => _inner;

    public bool IsAvailable => true;

    public byte[]? Read(int offset, int count) => _inner.Read(offset, count);

    public bool Write(int offset, byte[] data)
    {
        Writes.Add((offset, data.Length));
        if (Writes.Count - 1 == RefuseWriteNumber) return false;
        return _inner.Write(offset, data);
    }
}

internal static partial class Program
{
    private static void CheckFormatConstants()
    {
        Section("format constants");
        CheckEqual(20198, StateFormat.BlockLength, "block length");
        CheckEqual(0x5ED, StateFormat.PartyAccountNameOffset, "PARTY ACCT. name offset");
        CheckEqual(0x4FB, StateFormat.PartyAccountOffset, "party account offset");
        CheckEqual(0x5FA, StateFormat.MarchingOrderOffset, "records end where the marching order begins");
        CheckEqual(StateFormat.RecordSize * 6, StateFormat.MarchingOrderOffset, "six records");
        CheckEqual(StateFormat.Career1NameOffset, StateFormat.InventoryOffset + StateFormat.InventorySlots * StateFormat.InventoryEntrySize,
            "inventory runs up to the first career name");
        CheckEqual(StateFormat.GroundObjectsOffset, StateFormat.InUseFlagsOffset + StateFormat.CharacterSlots * StateFormat.InventorySlots,
            "in-use flags run up to the ground objects");
        CheckEqual(StateFormat.SkillsOffset, StateFormat.AttributesOffset + StateFormat.AttributeCount, "skills follow attributes");
        CheckEqual(StateFormat.NameOffset, StateFormat.Career2NameOffset + StateFormat.CareerNameLength, "name follows career 2");
        CheckEqual(StateFormat.RecordSize - 1, StateFormat.SexOffset, "sex is the last record byte");
        CheckEqual(StateFormat.NpcProgressOffset, StateFormat.NpcRemovedBitsOffset + 125 + 265, "NPC tables");
        CheckEqual(StateFormat.RowOffset, StateFormat.NpcHitPointsOffset + StateFormat.NpcIdCount, "NPC hit points end at the position");
        CheckEqual(StateFormat.EnemyShipOffset, StateFormat.FlyerValueOffset + 4, "enemy ship follows the flyer value");
        CheckEqual(StateFormat.FlyerValueOffset, StateFormat.FlyerOffset + StateFormat.FlyerRecordSize, "flyer value follows the flyer");
        CheckEqual(StateFormat.BlockLength, StateFormat.MapChangeCountOffset + 2, "map-change count ends the block");
        CheckEqual(StateFormat.MapChangeCountOffset, StateFormat.MapChangeLogOffset + 512 * 6, "map-change log size");
        CheckEqual(StateFormat.InUseFlagOffset(4, 20), StateFormat.GroundObjectsOffset - 1, "last in-use flag");
        CheckEqual(4, StateFormat.UnconsciousThreshold(9, 5, 4), "HEALTH 8/4 for max 9, STR 5 END 4");
        CheckEqual(5, StateFormat.UnconsciousThreshold(11, 5, 6), "HEALTH 10/5 for max 11, STR 5 END 6");
        CheckEqual(7, StateFormat.UnconsciousThreshold(12, 5, 4), "live poke: max 12 showed /7");
        CheckEqual("£100 0s 0d", GameFacts.FormatMoney(24000), "24000d = £100");
        CheckEqual("£53 6s 8d", GameFacts.FormatMoney(12800), "12800d = £53 6s 8d");
        CheckEqual("£0 1s 5d", GameFacts.FormatMoney(17), "17d");
    }

    private static void CheckBooks()
    {
        Section("reference books");
        CheckEqual(ItemBook.Count, ItemBook.All.Count, "item count");
        for (int i = 0; i < ItemBook.All.Count; i++)
            if (ItemBook.All[i].Id != i + 1) { Check(false, $"item {i} id"); break; }
        CheckEqual(6, ItemBook.All.Count(i => i.IsEmptyRecord), "six empty item records");
        Check(new[] { 91, 142, 167, 168, 169, 170 }.All(id => ItemBook.ById(id)!.IsEmptyRecord), "empty records at 91, 142 and 167-170");
        CheckEqual("LIGHT REVOLVER", ItemBook.NameOf(17), "id 17 (poked live)");
        CheckEqual("SHOT", ItemBook.NameOf(ItemBook.Shot), "SHOT id");
        CheckEqual("SHRAPNEL", ItemBook.NameOf(ItemBook.Shrapnel), "SHRAPNEL id");
        CheckEqual("AMMONIA", ItemBook.NameOf(ItemBook.Ammonia), "AMMONIA id");
        CheckEqual("GLOW CRYSTAL", ItemBook.NameOf(ItemBook.GlowCrystal), "GLOW CRYSTAL id");
        CheckEqual("MINERS HAT", ItemBook.NameOf(ItemBook.MinersHat), "MINERS HAT id");
        CheckEqual("LANTERN", ItemBook.NameOf(ItemBook.Lantern), "LANTERN id");
        CheckEqual("ELECTRIC LAMP", ItemBook.NameOf(ItemBook.ElectricLamp), "ELECTRIC LAMP id");
        CheckEqual("WATER BREATHER", ItemBook.NameOf(ItemBook.WaterBreather), "WATER BREATHER id");
        CheckEqual(4, ItemBook.AmmunitionTypes.Count(), "four ammunition types");
        CheckEqual(17, ItemBook.ShipGuns.Count(), "17 ship guns");
        Check(ItemBook.ShipGuns.All(g => g.Index >= 170), "ship guns are the table's last records");
        CheckEqual("(empty)", ItemBook.NameOf(0), "empty name");
        CheckEqual("#200", ItemBook.NameOf(200), "out-of-range name");
        Check(ItemBook.ById(17)!.UsesAmmunition && ItemBook.ById(17)!.Shots == 6, "revolver loads 6");
        CheckEqual("Earth, Mars", ItemBook.ById(7)!.Shops, "shovel shops");
        CheckEqual(23, ItemBook.TypeNames.Count, "23 item types");

        CheckEqual(24, SkillBook.Skills.Count, "24 skills");
        CheckEqual(24, SkillBook.SkillUses.Count, "24 skill uses");
        CheckEqual("Observation", SkillBook.Skills[12], "INT group starts with Observation");
        CheckEqual("Medicine", SkillBook.Skills[23], "last skill is Medicine");
        CheckEqual(3, SkillBook.AttributeOf(15), "Gunnery belongs to INT");
        CheckEqual("Gentry", SkillBook.ClassFor(4), "SOC 4 = Gentry");
        CheckEqual("Aristocracy", SkillBook.ClassFor(6), "SOC 6 = Aristocracy");
        CheckEqual(40, CareerBook.Names.Count, "40 built-in careers");
        CheckEqual("Inventor", CareerBook.NameOf(26), "career 26");
        CheckEqual("custom #43", CareerBook.NameOf(43), "user career");
        CheckEqual("London", PlaceBook.AreaName(1, 1), "London");
        CheckEqual("Boreo Syrtis", PlaceBook.AreaName(4, 7), "Boreo Syrtis");
        CheckEqual(110, PlaceBook.MapId(1, 1, 0), "London map id");
        CheckEqual(12, StoryFlagBook.All.Count, "12 story flags");
        Check(StoryFlagBook.All.Select(f => f.Bit).SequenceEqual(Enumerable.Range(0, 12)), "story flags in bit order");
    }

    private static void CheckSyntheticState()
    {
        Section("state validation");
        var b = Synthetic.Block();
        Check(StateFormat.LooksLikeState(b), "synthetic block validates");
        Check(!StateFormat.LooksLikeState(new byte[100]), "wrong length rejected");

        var noAccount = Synthetic.Block(); noAccount[StateFormat.PartyAccountNameOffset] = (byte)'X';
        Check(!StateFormat.LooksLikeState(noAccount), "missing PARTY ACCT. rejected");
        var badSex = Synthetic.Block(); badSex[StateFormat.RecordOffset(2) + StateFormat.SexOffset] = (byte)'x';
        Check(!StateFormat.LooksLikeState(badSex), "bad sex byte rejected");
        var badName = Synthetic.Block(); badName[StateFormat.RecordOffset(1) + StateFormat.NameOffset + 2] = 0x07;
        Check(!StateFormat.LooksLikeState(badName), "control character in a name rejected");
        var unterminated = Synthetic.Block();
        Synthetic.Ascii(unterminated, StateFormat.RecordOffset(3) + StateFormat.NameOffset, "ABCDEFGHIJKL");
        Check(!StateFormat.LooksLikeState(unterminated), "12-character unterminated name rejected");
        var badPlanet = Synthetic.Block(); badPlanet[StateFormat.PlanetOffset] = 0x40;
        Check(!StateFormat.LooksLikeState(badPlanet), "planet out of range rejected");
        var allEmpty = Synthetic.Block();
        for (int s = 0; s < 5; s++) allEmpty[StateFormat.RecordOffset(s) + StateFormat.NameOffset] = 0;
        Check(!StateFormat.LooksLikeState(allEmpty), "a party with nobody rejected");
        var oneDead = Synthetic.Block();
        Array.Clear(oneDead, StateFormat.RecordOffset(4), StateFormat.RecordSize);
        oneDead[StateFormat.RecordOffset(4) + StateFormat.DeadOffset] = 1;
        Check(StateFormat.LooksLikeState(oneDead), "a dead (zeroed) character still validates");
        var maxed = Synthetic.Block();
        for (int i = 0; i < 30; i++) maxed[StateFormat.AttributesOffset + i] = 99;
        Check(StateFormat.LooksLikeState(maxed), "maxed-out values still validate");

        Section("game state cache");
        var rec = new RecordingTarget(Synthetic.Block());
        var state = new GameState(rec);
        Check(state.Refresh() && state.IsLoaded, "refresh");
        Check(state.SetUInt32(0, 24000), "unchanged u32");
        CheckEqual(0, rec.Writes.Count, "an unchanged edit sends nothing");
        Check(state.SetUInt16(StateFormat.FoodOffset, 500), "food write");
        CheckEqual(1, rec.Writes.Count, "one write");
        CheckEqual((StateFormat.FoodOffset, 2), rec.Writes[0], "food writes exactly its two bytes");
        Check(state.SetString(StateFormat.NameOffset, StateFormat.NameLength, "wells jr"), "string write");
        CheckEqual("WELLS JR", state.GetString(StateFormat.NameOffset, StateFormat.NameLength), "names are upper-cased");
        Check(state.SetString(StateFormat.NameOffset, StateFormat.NameLength, "A VERY LONG NAME"), "long name");
        CheckEqual("A VERY LONG", state.GetString(StateFormat.NameOffset, StateFormat.NameLength), "long names keep their terminator");
        CheckEqual(0, (int)state.GetByte(StateFormat.NameOffset + 11), "terminator byte");

        var refusing = new BufferTarget(Synthetic.Block()) { RefuseWrites = true };
        var locked = new GameState(refusing);
        locked.Refresh();
        Check(!locked.SetUInt32(0, 1), "refused write reports failure");
        CheckEqual(24000u, locked.GetUInt32(0), "refused write leaves the cache alone");
    }

    private static void CheckCharacterRecord()
    {
        Section("character record");
        var target = new BufferTarget(Synthetic.Block());
        var state = new GameState(target);
        state.Refresh();
        var wells = new CharacterRecord(state, 0);

        CheckEqual("PROF WELLS", wells.Name, "name");
        Check(!wells.IsFemale && new CharacterRecord(state, 1).IsFemale, "sex");
        CheckEqual(24000L, wells.Wealth, "wealth");
        CheckEqual(800L, wells.MonthlyIncome, "income");
        CheckEqual(4, wells.UnconsciousThreshold, "threshold");
        Check(!wells.IsOutOfAction, "able");
        CheckEqual("Gentry", wells.SocialClass, "class from SOC");
        CheckEqual("INVENTOR", wells.Career1, "career name");

        Check(wells.SetWealth(5_000_000_000), "wealth clamps");
        CheckEqual((long)uint.MaxValue, wells.Wealth, "wealth ceiling");
        Check(wells.SetHealth(3), "health");
        Check(wells.IsOutOfAction, "at or below the threshold is out of action");
        Check(wells.SetHealth(9) && !wells.IsOutOfAction, "healed");
        Check(wells.SetFatigue(5), "fatigue = STR");
        Check(wells.IsOutOfAction, "fatigue equal to STR is out of action");
        Check(wells.SetFatigue(6) && wells.IsOutOfAction, "fatigue above an attribute is out of action");
        Check(wells.FullHeal(), "full heal");
        Check(wells.Health == 9 && wells.Fatigue == 0 && wells.Mental == 0, "full heal restores health, clears fatigue and mental");
        Check(!wells.SetName("   "), "an empty name is refused");
        Check(wells.SetAttribute(0, 200) && wells.GetAttribute(0) == 99, "attribute ceiling");
        Check(wells.SetSkill(23, 7) && target.Bytes[StateFormat.SkillsOffset + 23] == 7, "skill 23 is the record's last skill byte");
        Check(wells.SetAttribute(0, 5) && wells.SetAttribute(4, 9), "STR back to 5, CHA above the maximum roll");
        Check(wells.MaxAttributes(), "max attributes");
        Check(Enumerable.Range(0, 6).All(i => wells.GetAttribute(i) == (i == 4 ? 9 : 6)), "attributes raised to 6, a higher one left alone");
        Check(wells.SetAttribute(4, 6), "CHA back to 6");
        CheckEqual(12, wells.MaxHealth, "max health raised to STR + END");
        Check(wells.SetAttribute(0, 5) && wells.SetAttribute(2, 4) && wells.SetMaxHealth(9) && wells.SetHealth(5), "wounded: 5/4, able");
        Check(!wells.IsOutOfAction && wells.MaxAttributes(), "max attributes on a wounded character");
        Check(wells.MaxHealth == 12 && wells.Health == 8 && !wells.IsOutOfAction,
              "health rises with max health, so raising attributes cannot knock a wounded character out (8/6)");
        Check(wells.MaxSkills() && Enumerable.Range(0, 24).All(i => wells.GetSkill(i) >= 6), "skills at least 6");
        Check(wells.SetHorse(true) && target.Bytes[StateFormat.HorseOffset] == 1, "horse byte");

        Section("inventory");
        CheckEqual(0, wells.ItemCount, "empty pack");
        CheckEqual(0, wells.AddItem(17, 50), "revolver into slot 0");
        var e = wells.GetEntry(0);
        CheckEqual(new InventoryEntry(17, ItemBook.Shot - 1, 50, 6), e, "firearm arrives loaded with SHOT");
        Check(target.Bytes.AsSpan(StateFormat.InventoryOffset, 5).SequenceEqual(new byte[] { 0x11, 0x44, 0x32, 0x00, 0x06 }),
              "entry bytes match the live poke (11 44 32 00 06)");
        CheckEqual(1, wells.AddItem(ItemBook.Lantern), "lantern into slot 1");
        CheckEqual(2, wells.AddItem(62), "shield into slot 2");
        CheckEqual(new InventoryEntry(ItemBook.Lantern, 0, 0, 0), wells.GetEntry(1), "non-firearm has no ammunition");
        Check(wells.Wield(0) && wells.WeaponSlot == 1 && wells.IsInUse(0), "wield the revolver");
        Check(wells.Wear(2) && wells.ArmorSlot == 3 && wells.IsInUse(2), "wear the shield");
        Check(!wells.Wield(1), "a lantern cannot be wielded");
        Check(wells.SetInUse(1, true), "light the lantern");
        CheckEqual(-1, wells.AddItem(91), "an empty record cannot be added");
        CheckEqual(7, wells.CarriedWeight, "carried weight: (24 + 32 + 64) sixteenths / 16 = 7 lb");

        Check(wells.RemoveItem(0), "drop the revolver");
        CheckEqual(ItemBook.Lantern, wells.GetEntry(0).ItemId, "lantern shifted down");
        Check(wells.IsInUse(0), "its in-use flag shifted with it");
        CheckEqual(0, wells.WeaponSlot, "weapon slot cleared when its item is dropped");
        CheckEqual(2, wells.ArmorSlot, "armour slot renumbered");
        CheckEqual(0, wells.GetEntry(2).ItemId, "tail cleared");
        Check(!wells.RemoveItem(5), "removing an empty slot fails");

        for (int i = wells.ItemCount; i < StateFormat.InventorySlots; i++) wells.AddItem(ItemBook.Shot);
        CheckEqual(21, wells.ItemCount, "full pack");
        CheckEqual(-1, wells.AddItem(ItemBook.Shot), "a full pack refuses more");

        var gun = new CharacterRecord(state, 1);
        gun.AddItem(17, 0);
        gun.SetEntry(0, new InventoryEntry(17, 0, 3, 0));
        Check(gun.RefillAmmunition(), "refill");
        CheckEqual(new InventoryEntry(17, ItemBook.Shot - 1, 999, 6), gun.GetEntry(0), "refill fixes the ammo type and fills both counts");
        Check(gun.RefillAmmunition(rounds: 0) && gun.GetEntry(0).Rounds == 999, "freeze-style refill leaves reserve rounds alone");

        Section("inventory swaps and bounds");
        var sw = new CharacterRecord(state, 2);
        var pistol = ItemBook.ById(17)!;
        var shortGun = ItemBook.All.First(i => i.UsesAmmunition && i.IsHandWeapon && i.Shots is > 0 and < 6);
        var armour = ItemBook.All.Where(i => i.Kind == ItemType.Armor && !i.IsEmptyRecord).Select(i => i.Id).ToList();
        CheckEqual(0, sw.AddItem(pistol.Id, 40), "revolver for the swap tests");
        Check(sw.SetEntry(0, new InventoryEntry(pistol.Id, ItemBook.Grapeshot - 1, 40, 6)) && sw.Wield(0), "wielded, loaded with GRAPESHOT");
        Check(sw.ReplaceItem(0, shortGun.Id), "swap for a firearm with a smaller load");
        CheckEqual(new InventoryEntry(shortGun.Id, ItemBook.Grapeshot - 1, 40, shortGun.Shots), sw.GetEntry(0),
                   "a firearm swap keeps the ammunition type and reserve, and clamps IN GUN to the new load");
        Check(sw.WeaponSlot == 1 && sw.IsInUse(0), "and it stays in hand");
        Check(sw.ReplaceItem(0, ItemBook.Lantern), "swap the gun in hand for a lantern");
        CheckEqual(new InventoryEntry(ItemBook.Lantern, 0, 0, 0), sw.GetEntry(0), "a non-firearm gets no ammunition bytes");
        Check(sw.WeaponSlot == 0 && !sw.IsInUse(0), "and the weapon reference and in-use flag are dropped");
        CheckEqual(1, sw.AddItem(armour[0]), "armour for the swap tests");
        Check(sw.Wear(1) && sw.ReplaceItem(1, armour[1]), "swap worn armour for other armour");
        Check(sw.ArmorSlot == 2 && sw.IsInUse(1), "still worn");
        Check(sw.ReplaceItem(1, pistol.Id) && sw.ArmorSlot == 0 && !sw.IsInUse(1), "armour swapped for a gun is no longer worn");
        Check(!sw.ReplaceItem(5, pistol.Id), "an empty slot cannot be swapped");
        Check(!sw.ReplaceItem(0, 91), "nor swapped to an empty item record");
        Check(!sw.ReplaceItem(StateFormat.InventorySlots, pistol.Id) && !sw.ReplaceItem(-1, pistol.Id), "swap bounds");
        Check(!sw.Wield(StateFormat.InventorySlots) && !sw.Wield(-1) && !sw.Wear(StateFormat.InventorySlots), "wield/wear bounds");

        var rt = new RecordingTarget(Synthetic.Block());
        var rts = new GameState(rt);
        rts.Refresh();
        var rb = new CharacterRecord(rts, 0);
        rb.AddItem(pistol.Id, 10);
        rb.AddItem(ItemBook.Lantern);
        rb.SetInUse(1, true);
        var entriesBefore = rt.Inner.Bytes.AsSpan(StateFormat.InventoryEntryOffset(0, 0), StateFormat.InventorySlots * 5).ToArray();
        rt.RefuseWriteNumber = rt.Writes.Count + 1;   // the entries shift lands, the in-use flag shift is refused
        Check(!rb.RemoveItem(0), "a remove whose flag write is refused fails");
        Check(rt.Inner.Bytes.AsSpan(StateFormat.InventoryEntryOffset(0, 0), entriesBefore.Length).SequenceEqual(entriesBefore),
              "and the entries are rolled back, so items and flags stay paired");
        Check(rb.GetEntry(0).ItemId == pistol.Id && rb.IsInUse(1), "the cache agrees");

        var refusing = new BufferTarget(Synthetic.Block()) { RefuseWrites = true };
        var rs = new GameState(refusing);
        rs.Refresh();
        var r = new CharacterRecord(rs, 0);
        CheckEqual(-1, r.AddItem(17), "add refused");
        Check(!r.FullHeal(), "heal refused");
        CheckEqual(8, r.Health, "refused heal leaves health");
    }

    private static void CheckWorldRecord()
    {
        Section("world record");
        var target = new BufferTarget(Synthetic.Block());
        var state = new GameState(target);
        state.Refresh();
        var w = new WorldRecord(state);
        CheckEqual(1, w.Planet, "planet");
        CheckEqual(113, w.MapId, "map id for London map 3");
        CheckEqual(29, w.Row, "row halves the stored value");
        CheckEqual(18, w.Column, "column halves the stored value");
        CheckEqual("LONDON", w.AreaNameText, "area name");
        Check(w.SetFood(99999) && w.Food == StateFormat.MaxFood, "food clamps to 32767");
        Check(w.SetDay(123456) && w.Day == 123456, "day is 32-bit");
        Check(w.SetPartyAccount(31200) && target.Bytes[StateFormat.PartyAccountOffset] == 0xE0, "party account bytes");
        Check(w.Teleport(10, 20), "teleport");
        Check(target.Bytes[StateFormat.RowOffset] == 20 && target.Bytes[StateFormat.ColumnOffset] == 40, "teleport stores doubled row/col");
        Check(target.Bytes[StateFormat.PreviousRowOffset] == 20 && target.Bytes[StateFormat.PreviousColumnOffset] == 40, "teleport also sets the previous square");
        Check(!w.Teleport(-1, 5), "negative teleport refused");
        Check(w.SetStoryFlag(5, true) && w.HasStoryFlag(5) && w.StoryFlags == 0x20, "set story flag");
        Check(w.SetStoryFlag(11, true) && w.StoryFlags == 0x820, "second story flag");
        Check(w.SetStoryFlag(5, false) && w.StoryFlags == 0x800, "clear story flag");
        Check(w.SetFlyer(StateFormat.FlyerTopGun, 172) && target.Bytes[StateFormat.FlyerOffset + 7] == 172, "flyer top gun");
        Synthetic.Put16(target.Bytes, StateFormat.FlyerHullHitsOffset, 20);
        Synthetic.Put16(target.Bytes, StateFormat.FlyerDamageBitsOffset, 5);
        state.Refresh();
        Check(w.RepairFlyer() && w.HullHits == 0 && w.DamageBits == 0, "repair");

        target.Bytes[StateFormat.MapOffset] = 2;
        state.Refresh();
        CheckEqual(992, w.MapId, "a city's pub is the shared map 992");
        target.Bytes[StateFormat.MapOffset] = 9;
        state.Refresh();
        CheckEqual(999, w.MapId, "a city's inn is the shared map 999");
        target.Bytes[StateFormat.AreaOffset] = 0;
        target.Bytes[StateFormat.MapOffset] = 0;
        state.Refresh();
        CheckEqual(100, w.MapId, "the Earth map");
        CheckEqual("Earth (planet map)", w.Describe, "planet map description");
    }

    private static void CheckSaveGame()
    {
        Section("save files");
        string dir = TempDirectory();
        try
        {
            string path = Path.Combine(dir, "TEST.SAV");
            File.WriteAllBytes(path, Synthetic.Block());
            File.WriteAllBytes(Path.Combine(dir, "START.OBJ"), Synthetic.Block());   // even at save size it is not a save
            File.WriteAllBytes(Path.Combine(dir, "BAD.SAV"), new byte[StateFormat.BlockLength]);

            Check(SaveGame.Load(Path.Combine(dir, "START.OBJ"), out string err1) is null && err1.Contains("ground objects"), "START.OBJ explained");
            Check(SaveGame.Load(Path.Combine(dir, "BAD.SAV"), out string err2) is null && err2.Contains("PARTY ACCT."), "zeroed file rejected");
            var save = SaveGame.Load(path, out _);
            Check(save is not null, "valid save loads");
            if (save is null) return;
            Check(!save.ChangedOnDisk, "a freshly opened save matches the file");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));
            Check(save.ChangedOnDisk, "the game saving over the file is noticed");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-5));
            Check(SaveGame.FindSaves(dir).Select(Path.GetFileName).SequenceEqual(new[] { "BAD.SAV", "TEST.SAV" }), "FindSaves lists .SAV files");
            new CharacterRecord(save.State, 0).SetWealth(1);
            Check(save.Save(out _), "save");
            Check(File.Exists(save.BackupPath), "backup written");
            CheckEqual(24000u, BitConverter.ToUInt32(File.ReadAllBytes(save.BackupPath), 0), "backup holds the original");
            CheckEqual(1u, BitConverter.ToUInt32(File.ReadAllBytes(path), 0), "file holds the edit");
            new CharacterRecord(save.State, 0).SetWealth(2);
            Check(save.Save(out _), "second save");
            CheckEqual(24000u, BitConverter.ToUInt32(File.ReadAllBytes(save.BackupPath), 0), "backup is never overwritten");
            Check(!File.Exists(path + ".tmp"), "no temp file left behind");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
