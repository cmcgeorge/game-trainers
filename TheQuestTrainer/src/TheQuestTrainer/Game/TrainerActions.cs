using TheQuestTrainer.Memory;

namespace TheQuestTrainer.Game;

/// <summary>
/// Outcome of a write. <see cref="Message"/> is always worth showing; it explains refusals.
///
/// <see cref="Written"/> is the value that actually landed in the game, which is not always the
/// value that was asked for — every write clamps to the field it is going into. The caller needs it
/// to put the editor back in step: a box left showing 9,999,999,999 after the game took 999,999,999
/// is exactly as wrong as a box left showing a value that was refused outright.
/// </summary>
public readonly record struct ActionResult(bool Ok, string Message, long? Written = null)
{
    /// <summary>A successful write, optionally carrying the value that landed.</summary>
    public static ActionResult Success(string message = "", long? written = null) => new(true, message, written);

    /// <summary>A refused or failed write.</summary>
    public static ActionResult Failure(string message) => new(false, message);
}

/// <summary>
/// Every edit the trainer can make, as read-validate-write.
///
/// Two rules hold for all of them.
///
/// <b>Nothing is written to an address that has not just re-validated.</b> The player can save,
/// load, die or start a new game between two ticks of the refresh timer, and any of those replaces
/// the character record. Re-running <see cref="CharacterLocator.Validate"/> immediately before each
/// write costs one page read and turns "wrote 999999 gold into a freed heap block" into a refusal
/// with a reason.
///
/// <b>Every value is clamped to the field it is going into.</b> Health, mana, attributes and skills
/// are unsigned words and gold is an unsigned dword; the game derives armour, damage, carry weight
/// and spell cost from them in 16-bit arithmetic, so the clamps in <see cref="GameFacts"/> are
/// deliberately well inside what the fields could hold.
/// </summary>
public sealed class TrainerActions
{
    private readonly IMemorySource _source;
    private readonly PeImage? _image;

    /// <summary>When set, every write is refused. The UI exposes it as a safety catch.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Binds the actions to a memory source and the parsed header used for validation.</summary>
    public TrainerActions(IMemorySource source, PeImage? image)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _image = image;
    }

    // ---- simple scalars -------------------------------------------------------------------

    /// <summary>Sets current health. The game shows a current above the derived maximum quite happily.</summary>
    public ActionResult SetHealth(uint record, int value) =>
        WriteWord(record, QuestLayout.Health, value, 0, GameFacts.MaxHealthOrMana, "Health");

    /// <summary>Sets current mana.</summary>
    public ActionResult SetMana(uint record, int value) =>
        WriteWord(record, QuestLayout.Mana, value, 0, GameFacts.MaxHealthOrMana, "Mana");

    /// <summary>Sets gold.</summary>
    public ActionResult SetGold(uint record, long value) =>
        WriteDword(record, QuestLayout.Gold, value, 0, GameFacts.MaxGold, "Gold");

    /// <summary>Sets total experience. The level follows on the game's next experience award.</summary>
    public ActionResult SetExperience(uint record, long value) =>
        WriteDword(record, QuestLayout.Experience, value, 0, int.MaxValue, "Experience");

    /// <summary>Sets outstanding crime. Zero clears the bounty guards are collecting.</summary>
    public ActionResult SetCrime(uint record, long value) =>
        WriteDword(record, QuestLayout.Crime, value, 0, int.MaxValue, "Crime");

    /// <summary>Sets fame, -100 (Demonic) to +100 (Saint).</summary>
    public ActionResult SetFame(uint record, int value)
    {
        if (!Ready(record, out string why)) return ActionResult.Failure(why);
        short clamped = (short)Math.Clamp(value, GameFacts.MinFame, GameFacts.MaxFame);
        return _source.Write(record + QuestLayout.Fame, BitConverter.GetBytes(clamped))
            ? ActionResult.Success($"Fame set to {clamped} ({GameTables.FameBand(clamped)}).", clamped)
            : ActionResult.Failure("Fame write failed.");
    }

    /// <summary>Sets unspent attribute points.</summary>
    public ActionResult SetAttributePoints(uint record, int value) =>
        WriteWord(record, QuestLayout.AttributePoints, value, 0, GameFacts.MaxPoints, "Attribute points");

    /// <summary>Sets unspent skill points.</summary>
    public ActionResult SetSkillPoints(uint record, int value) =>
        WriteWord(record, QuestLayout.SkillPoints, value, 0, GameFacts.MaxPoints, "Skill points");

    // ---- attributes and skills ------------------------------------------------------------

    /// <summary>Sets the base value of attribute <paramref name="id"/> (1..5).</summary>
    public ActionResult SetAttribute(uint record, int id, int value)
    {
        if (GameTables.Attribute(id) is not { } info)
            return ActionResult.Failure($"Attribute id {id} is not one of the game's five.");
        return WriteWord(record, QuestLayout.BaseAttributes + (uint)id * 2, value,
            GameFacts.MinAttribute, GameFacts.MaxAttributeOrSkill, info.Name);
    }

    /// <summary>
    /// Sets the base value of skill <paramref name="id"/> (1..20). What the skills screen shows is
    /// this plus racial and equipment bonuses, so a Derth's Attack Magic reads back ten higher than
    /// what was written here.
    /// </summary>
    public ActionResult SetSkill(uint record, int id, int value)
    {
        if (GameTables.Skill(id) is not { } info)
            return ActionResult.Failure($"Skill id {id} is not one of the game's twenty.");
        return WriteWord(record, QuestLayout.BaseSkills + (uint)id * 2, value,
            0, GameFacts.MaxAttributeOrSkill, info.Name);
    }

    /// <summary>
    /// Raises every skill to the game's own ceiling — twice the base value of its governing
    /// attribute — and never lowers one that is already above it.
    ///
    /// The two race-locked schools are honoured: Undead Magic is only raised for a Rasvim, and
    /// Healing Magic is left alone for one, because those are the restrictions the game's own skill
    /// descriptions state.
    /// </summary>
    public ActionResult MaxSkills(uint record)
    {
        var snapshot = CharacterReader.Read(_source, record);
        if (snapshot is null) return ActionResult.Failure("Could not read the character before raising skills.");

        if (!Ready(record, out string why)) return ActionResult.Failure(why);

        int raised = 0, skipped = 0;
        foreach (var skill in GameTables.Skills)
        {
            if (!SkillAvailableTo(skill.Id, snapshot.RaceId)) { skipped++; continue; }

            int governing = snapshot.Attributes[skill.GoverningAttribute];
            int target = GameFacts.SkillCapFor(governing);
            if (snapshot.Skills[skill.Id] >= target) continue;

            var result = SetSkill(record, skill.Id, target);
            if (!result.Ok) return result;
            raised++;
        }

        string note = skipped > 0 ? $" ({skipped} race-locked school(s) left alone)" : "";
        return ActionResult.Success($"Raised {raised} skill(s) to twice their governing attribute{note}.");
    }

    /// <summary>
    /// Whether the game lets <paramref name="race"/> learn skill <paramref name="skillId"/>.
    /// Only the two undead-related schools are restricted.
    /// </summary>
    public static bool SkillAvailableTo(int skillId, uint race)
    {
        const uint Rasvim = 1;
        return skillId switch
        {
            8 => race != Rasvim,    // Healing Magic — "Cannot be learned by Undead (Rasvim)."
            12 => race == Rasvim,   // Undead Magic  — "Can only be learned by Undead (Rasvim)."
            _ => true,
        };
    }

    // ---- level ----------------------------------------------------------------------------

    /// <summary>
    /// Sets the character's level and leaves the three experience fields consistent with it.
    ///
    /// The game caches the next level's threshold rather than recomputing it, so writing the level
    /// alone would leave a level-40 character still needing 4,000 experience. This writes the level,
    /// raises experience to at least what that level requires, and rewrites the cached threshold
    /// from the record's own table — never from a table baked into the trainer.
    ///
    /// Everything is read and computed <i>before</i> the validation, so the three writes go out
    /// back to back with nothing between them: this is the one edit that has to leave three fields
    /// agreeing with each other, and a re-validation in the middle would only widen the window in
    /// which it could be interrupted half-applied.
    /// </summary>
    public ActionResult SetLevel(uint record, int level)
    {
        var snapshot = CharacterReader.Read(_source, record);
        if (snapshot is null) return ActionResult.Failure("Could not read the character before setting the level.");

        level = Math.Clamp(level, 1, GameFacts.MaxLevel);

        long floor = snapshot.ThresholdForLevel(level);
        if (floor < 0) return ActionResult.Failure($"Level {level} is not in this build's experience table.");

        long nextThreshold = level < GameFacts.MaxLevel ? snapshot.ThresholdForLevel(level + 1) : floor;
        if (nextThreshold < 0) return ActionResult.Failure("The record's experience table is shorter than expected.");

        if (!Ready(record, out string why)) return ActionResult.Failure(why);

        if (!_source.Write(record + QuestLayout.Level, BitConverter.GetBytes((ushort)level)))
            return ActionResult.Failure("Level write failed.");

        if (snapshot.Experience < floor &&
            !_source.Write(record + QuestLayout.Experience, BitConverter.GetBytes((uint)floor)))
            return ActionResult.Failure("Level was set but experience could not be raised to match it.");

        if (!_source.Write(record + QuestLayout.ExperienceForNextLevel, BitConverter.GetBytes((uint)nextThreshold)))
            return ActionResult.Failure("Level was set but the next-level threshold could not be updated.");

        return ActionResult.Success($"Level {level}; next level at {nextThreshold:N0} experience.", level);
    }

    // ---- items ------------------------------------------------------------------------------

    /// <summary>
    /// Fills one item's meter — repairs worn gear, recharges a wand, refills a quiver.
    ///
    /// This is the game's own "repair" and "recharge" outcome without the hammer, the skill check or
    /// the shop: both of those end by writing the same word this does.
    /// </summary>
    public ActionResult RestoreItem(uint record, uint item)
    {
        if (!FindItem(record, item, out var carried, out string why)) return ActionResult.Failure(why);

        if (carried.MeterMax <= 0)
            return ActionResult.Failure($"“{carried.Type.Name}” has nothing to restore.");

        return WriteMeter(record, carried, carried.MeterMax);
    }

    /// <summary>
    /// Fills the meter of every carried item that has one.
    ///
    /// The inventory is read once and then walked, but each write still re-validates: restoring
    /// thirty items is thirty chances for the player to sell one, and a stale pointer here would be
    /// a write into a freed heap block.
    /// </summary>
    public ActionResult RestoreAllItems(uint record)
    {
        var inventory = InventoryReader.Read(_source, record);
        if (inventory is null) return ActionResult.Failure("Could not read the inventory.");

        int restored = 0;
        foreach (var item in inventory.Items)
        {
            if (!item.CanRestore) continue;
            var result = RestoreItem(record, item.Address);
            if (!result.Ok) return result;
            restored++;
        }

        return restored == 0
            ? ActionResult.Success("Everything is already at full condition and charge.")
            : ActionResult.Success($"Restored {restored} item(s) to full.");
    }

    /// <summary>Sets one item's meter explicitly, for a value the "restore" buttons will not produce.</summary>
    public ActionResult SetItemMeter(uint record, uint item, int value)
    {
        if (!FindItem(record, item, out var carried, out string why)) return ActionResult.Failure(why);
        return WriteMeter(record, carried, value);
    }

    /// <summary>
    /// Turns a carried item into a different one by pointing it at another item type, then fills the
    /// new item's meter so it arrives in mint condition.
    ///
    /// This is how the trainer "gives" an item. It cannot add one: an item is a heap allocation, and
    /// the trainer has no safe way to make the game allocate. Re-pointing an item it already owns
    /// costs one dword and reuses an object the game will free correctly, because the only thing
    /// that distinguishes a Loaf of Bread from a King's Longsword is which shared type the item
    /// points at.
    ///
    /// <b>An equipped item is refused.</b> The equipment slots hold raw pointers, so retyping in
    /// place would leave, say, the helm slot holding a longsword — a state the game has no reason to
    /// cope with, and the one part of this that was not confirmed against a live session. Unequipping
    /// in the game first costs the player one click.
    /// </summary>
    public ActionResult ReplaceItem(uint record, uint item, ItemType type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!FindItem(record, item, out var carried, out string why)) return ActionResult.Failure(why);

        if (carried.IsEquipped)
            return ActionResult.Failure(
                $"“{carried.Type.Name}” is equipped — unequip it in the game first, then replace it.");

        if (!ItemCatalog.CanReplaceWith(type, out string reason)) return ActionResult.Failure(reason);

        // Re-validate the replacement type itself. It came from a sweep that may be minutes old, and
        // a type object that no longer reads back as one means the game unloaded the module it
        // belonged to — an expansion area left behind, say.
        if (ItemTypeReader.Read(_source, type.Address, record - QuestLayout.RecordInEngine) is null)
            return ActionResult.Failure($"“{type.Name}” is no longer loaded — rescan the catalog.");

        if (!Ready(record, out why)) return ActionResult.Failure(why);

        if (!_source.Write(carried.Address + ItemLayout.ItemType, BitConverter.GetBytes(type.Address)))
            return ActionResult.Failure($"Could not write the new item type for “{carried.Type.Name}”.");

        // The meter that came with the old item means nothing to the new one — 2,853 of a Fur Boot's
        // 3,000 is a broken longsword. Read the new maximum through the type that is now in place.
        int max = InventoryReader.MeterMax(_source, type, 0);
        if (max > 0 && !_source.Write(carried.Address + ItemLayout.ItemCondition, BitConverter.GetBytes((ushort)Math.Min(max, GameFacts.MaxItemMeter))))
            return ActionResult.Failure($"“{type.Name}” was placed but its condition could not be set.");

        return ActionResult.Success($"“{carried.Type.Name}” is now “{type.Name}”.", max);
    }

    /// <summary>
    /// Re-reads the inventory and finds <paramref name="item"/> in it, so a write only ever goes to
    /// a pointer the game still holds.
    ///
    /// Searching by address rather than by index is the point. Items are heap objects the game frees
    /// when the player drops, sells, eats or breaks one, and the vector closes up behind them, so an
    /// index captured when the row was drawn can name a different item — or none — a tick later.
    /// </summary>
    private bool FindItem(uint record, uint item, out CarriedItem carried, out string why)
    {
        carried = null!;

        if (ReadOnly) { why = "Read-only mode is on; nothing was written."; return false; }

        var inventory = InventoryReader.Read(_source, record);
        if (inventory is null) { why = "Could not read the inventory."; return false; }

        foreach (var candidate in inventory.Items)
        {
            if (candidate.Address != item) continue;
            carried = candidate;
            why = "";
            return true;
        }

        why = "That item is no longer in the character's pack — press Refresh.";
        return false;
    }

    /// <summary>Clamps and writes an item's meter word.</summary>
    private ActionResult WriteMeter(uint record, CarriedItem item, int value)
    {
        if (!Ready(record, out string why)) return ActionResult.Failure(why);

        int clamped = Math.Clamp(value, 0, GameFacts.MaxItemMeter);
        if (!_source.Write(item.Address + ItemLayout.ItemCondition, BitConverter.GetBytes((ushort)clamped)))
            return ActionResult.Failure($"Could not write to “{item.Type.Name}”.");

        string what = item.Type.Meter switch
        {
            ItemMeter.Charges => "charges",
            ItemMeter.Units => "units",
            _ => "condition",
        };
        return ActionResult.Success(
            Describe($"“{item.Type.Name}” {what}", value, clamped, 0, GameFacts.MaxItemMeter), clamped);
    }

    // ---- plumbing -------------------------------------------------------------------------

    /// <summary>
    /// Whether a write may proceed: not read-only, and the record still validates at this address.
    /// </summary>
    private bool Ready(uint record, out string why)
    {
        if (ReadOnly) { why = "Read-only mode is on; nothing was written."; return false; }
        if (!CharacterLocator.Validate(_source, _image, record, out string reason))
        {
            why = $"The character record moved or went away ({reason}) — press Attach again.";
            return false;
        }
        why = "";
        return true;
    }

    private ActionResult WriteWord(uint record, uint offset, int value, int min, int max, string label)
    {
        if (!Ready(record, out string why)) return ActionResult.Failure(why);
        int clamped = Math.Clamp(value, min, max);
        bool ok = _source.Write(record + offset, BitConverter.GetBytes((ushort)clamped));
        return ok
            ? ActionResult.Success(Describe(label, value, clamped, min, max), clamped)
            : ActionResult.Failure($"{label} write failed.");
    }

    private ActionResult WriteDword(uint record, uint offset, long value, long min, long max, string label)
    {
        if (!Ready(record, out string why)) return ActionResult.Failure(why);
        long clamped = Math.Clamp(value, min, max);
        bool ok = _source.Write(record + offset, BitConverter.GetBytes((uint)clamped));
        return ok
            ? ActionResult.Success(Describe(label, value, clamped, min, max), clamped)
            : ActionResult.Failure($"{label} write failed.");
    }

    private static string Describe(string label, long asked, long written, long min, long max) =>
        asked == written
            ? $"{label} set to {written:N0}."
            : $"{label} set to {written:N0} (asked for {asked:N0}; the field takes {min:N0}..{max:N0}).";
}
