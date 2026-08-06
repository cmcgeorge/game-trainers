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

    // ---- conditions -------------------------------------------------------------------------

    /// <summary>
    /// Cures poison, disease, curse and paralysis — the four adverse conditions the game itself
    /// names — and returns what it took away.
    ///
    /// <b>This is the game's own cure, not a shortcut past it.</b> A condition is not a flag: poison,
    /// curse and paralysis are lists of heap-allocated effect objects, and the game's "Cure poison",
    /// "Remove curse" and "Cure paralysis" all end in one function that erases from the matching list
    /// every entry whose source byte says a cure may take it. This reproduces that function exactly,
    /// including which sources it leaves alone — an effect granted by a worn item or by the
    /// character's race is not an affliction and is not removed.
    ///
    /// The one thing it cannot reproduce is the <c>delete</c>: the trainer has no safe way to free a
    /// block in the game's heap, so each cured effect leaks its twenty bytes. The vector's own buffer
    /// is untouched and the game still owns and frees it; nothing is left dangling, because nothing
    /// is freed. In exchange, curing costs one or two writes instead of a call into the game.
    ///
    /// Disease is handled the way the game handles it: the list is a vector of pointers to *shared*
    /// type objects, so emptying it costs nothing at all, and the effects the diseases were granting
    /// are then stripped from every group — which is what the game does immediately after curing one.
    /// </summary>
    public ActionResult CureConditions(uint record)
    {
        if (ReadOnly) return ActionResult.Failure("Read-only mode is on; nothing was written.");

        var before = ConditionReader.Read(_source, record);
        if (before is null) return ActionResult.Failure("Could not read the character's conditions.");

        if (!before.AnyCurable)
            return ActionResult.Success(before.Any
                ? "Nothing a cure removes — what is left comes from equipment, race or a disease."
                : "Nothing adverse to cure.");

        if (!Ready(record, out string why)) return ActionResult.Failure(why);

        var cured = new List<string>();

        foreach (var condition in ConditionTables.All)
        {
            if (condition == Condition.Disease) continue;

            var group = ConditionReader.ReadGroupOf(_source, record, condition);
            if (group is null)
                return ActionResult.Failure(
                    $"Could not read where this build keeps {ConditionTables.Noun(condition)}.");

            if (!EraseEffects(group, e => e.IsCurable, out int erased, out why))
                return ActionResult.Failure(why);

            if (erased > 0) cured.Add(ConditionTables.Noun(condition));
        }

        if (before.Diseases.Count > 0)
        {
            if (!CureDiseases(record, out why)) return ActionResult.Failure(why);
            cured.Add(before.Diseases.Count == 1
                ? $"“{before.Diseases[0]}”"
                : $"{before.Diseases.Count} disease(s)");
        }

        return cured.Count == 0
            ? ActionResult.Success("Nothing adverse to cure.")
            : ActionResult.Success($"Cured {Join(cured)}.");
    }

    /// <summary>
    /// Empties the disease list and strips what the diseases were granting.
    ///
    /// Both halves are needed and the second is the one that is easy to miss: a disease's penalties
    /// are ordinary effects sitting in the same groups everything else does, tagged as having come
    /// from a disease, and nothing re-derives them on its own. Dropping the list without stripping
    /// them would leave the penalties on the character permanently — which is exactly why the game
    /// rebuilds them the moment the list changes.
    /// </summary>
    private bool CureDiseases(uint record, out string why)
    {
        if (!TryReadUInt32(record + ConditionLayout.DiseasesBegin, out uint begin))
        {
            why = "Could not read the character's disease list.";
            return false;
        }

        if (!_source.Write(record + ConditionLayout.DiseasesEnd, BitConverter.GetBytes(begin)))
        {
            why = "Could not clear the character's disease list.";
            return false;
        }

        for (int index = ConditionLayout.FirstEffectGroup; index <= ConditionLayout.LastEffectGroup; index++)
        {
            var group = ConditionReader.ReadGroup(_source, record, index);
            if (group is null)
            {
                why = $"The disease list was cleared but effect group {index} could not be read.";
                return false;
            }

            if (!EraseEffects(group, e => e.Source == ConditionLayout.SourceDisease, out _, out why))
                return false;
        }

        why = "";
        return true;
    }

    /// <summary>
    /// Removes from one effect group every entry <paramref name="remove"/> accepts, compacting the
    /// vector the same way the game's own erase does.
    ///
    /// The survivors are written before the vector is shortened, deliberately. In the instant
    /// between the two writes the vector holds one duplicated pointer rather than a short vector
    /// with a removed effect still inside it, so a game reading it mid-cure sees an effect twice for
    /// a frame instead of seeing the poison it was told had gone. Neither ordering can produce a
    /// dangling pointer, because nothing here is freed.
    /// </summary>
    private bool EraseEffects(EffectGroup group, Func<ActiveEffect, bool> remove, out int erased, out string why)
    {
        erased = 0;
        why = "";

        var survivors = group.Effects.Where(e => !remove(e)).Select(e => e.Address).ToArray();
        if (survivors.Length == group.Effects.Count) return true;

        if (survivors.Length > 0)
        {
            var bytes = new byte[survivors.Length * 4];
            for (int i = 0; i < survivors.Length; i++)
                BitConverter.GetBytes(survivors[i]).CopyTo(bytes, i * 4);

            if (!_source.Write(group.Begin, bytes))
            {
                why = $"Could not rewrite effect group {group.Index}.";
                return false;
            }
        }

        uint end = group.Begin + (uint)survivors.Length * 4;
        if (!_source.Write(group.Slot + 4, BitConverter.GetBytes(end)))
        {
            why = $"Could not shorten effect group {group.Index}.";
            return false;
        }

        erased = group.Effects.Count - survivors.Length;
        return true;
    }

    // ---- position ---------------------------------------------------------------------------

    /// <summary>
    /// Moves the player to tile (<paramref name="localX"/>, <paramref name="localY"/>) of the map
    /// they are already on.
    ///
    /// <b>This is one pair of writes and the game does the rest.</b> The engine reads the player's
    /// window position every frame, so the camera, the compass and the automap all follow within a
    /// frame — there is no step to take afterwards and nothing else to keep in step. The
    /// world-absolute pair the world object caches is recomputed by the engine from this one, so it
    /// is deliberately left alone.
    ///
    /// <b>The target is confined to the current map, and that is a real restriction rather than
    /// caution.</b> Outdoors the tile window holds a three-by-three block, so a coordinate outside
    /// the middle map lands the player on a genuine, drawn tile of a neighbour — and the engine goes
    /// on believing they are on the map they left, because only its own movement code reassigns that.
    /// Everything downstream of it is then wrong: the automap draws the wrong map, the world-absolute
    /// position is computed from the wrong cell, and walking further takes them off the end of what
    /// is loaded. Confirmed by doing it. Walk across the boundary in the game instead.
    ///
    /// The position is re-read here rather than taken from the caller's snapshot, for the same reason
    /// every other write re-validates: the player can walk, enter a building or load a save between
    /// the row being drawn and the button being pressed, and each of those changes which map the
    /// coordinates mean.
    /// </summary>
    public ActionResult Teleport(uint record, int localX, int localY)
    {
        if (!Ready(record, out string why)) return ActionResult.Failure(why);

        var where = MapReader.Read(_source, record);
        if (where is null)
            return ActionResult.Failure("Could not read where the player is — is a game loaded rather than the menu?");

        var map = where.Here;
        if (localX < 0 || localY < 0 || localX >= map.Width || localY >= map.Height)
            return ActionResult.Failure(
                $"({localX}, {localY}) is outside “{map.Name}”, which is {map.Width}×{map.Height} tiles. " +
                "Teleport only moves you within the map you are on — walk across the boundary in the game.");

        int windowX = localX + where.Origin;
        int windowY = localY + where.Origin;
        if (windowX < 0 || windowY < 0 || windowX >= where.WindowSize || windowY >= where.WindowSize)
            return ActionResult.Failure(
                $"({localX}, {localY}) falls outside the engine's {where.WindowSize}×{where.WindowSize} tile window.");

        if (!_source.Write(where.Manager + MapLayout.PlayerX, BitConverter.GetBytes(windowX)))
            return ActionResult.Failure("Teleport failed: the position could not be written.");

        // The two coordinates are written separately because they are two fields, so there is one
        // frame in which the player is at the new column and the old row. That is a legal position —
        // the engine clamps nothing and draws whatever tile it is given — so the worst case is a
        // single frame somewhere else on the same map, not a state the game has to cope with.
        if (!_source.Write(where.Manager + MapLayout.PlayerY, BitConverter.GetBytes(windowY)))
            return ActionResult.Failure(
                $"Teleport half-applied: moved to column {localX} but the row could not be written.");

        string denied = (map.Flags & MapLayout.FlagTeleportDenied) != 0
            ? " (the game's own Teleport magic is denied on this map; this is not the game's spell)"
            : "";
        return ActionResult.Success($"Moved to ({localX}, {localY}) on “{map.Name}”{denied}.");
    }

    /// <summary>"a", "a and b", "a, b and c" — the cure's message lists whatever it took away.</summary>
    private static string Join(IReadOnlyList<string> parts) => parts.Count switch
    {
        1 => parts[0],
        2 => $"{parts[0]} and {parts[1]}",
        _ => $"{string.Join(", ", parts.Take(parts.Count - 1))} and {parts[^1]}",
    };

    private bool TryReadUInt32(uint address, out uint value)
    {
        var word = new byte[4];
        if (_source.Read(address, word, 4) != 4) { value = 0; return false; }
        value = BitConverter.ToUInt32(word, 0);
        return true;
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
