namespace Space1889Trainer.Game;

/// <summary>One 5-byte inventory entry.</summary>
/// <param name="ItemId">ITEMS.DAT index + 1; 0 = empty.</param>
/// <param name="AmmoIndex">Loaded ammunition as a 0-based ITEMS.DAT index (SHOT 68 .. SHRAPNEL 71).</param>
/// <param name="Rounds">Reserve rounds (ROUNDS:), u16.</param>
/// <param name="InGun">Shots left in the weapon (IN GUN:).</param>
public readonly record struct InventoryEntry(int ItemId, int AmmoIndex, int Rounds, int InGun)
{
    public bool IsEmpty => ItemId == 0;

    public byte[] ToBytes() => new[]
    {
        (byte)ItemId, (byte)AmmoIndex, (byte)Rounds, (byte)(Rounds >> 8), (byte)InGun,
    };

    public static InventoryEntry FromBytes(byte[] b, int offset) =>
        new(b[offset], b[offset + 1], b[offset + 2] | (b[offset + 3] << 8), b[offset + 4]);
}

/// <summary>
/// A typed view over one character record in a <see cref="GameState"/>. Every setter writes only the bytes
/// its field owns and returns false (leaving the cache as it was) when the target refuses.
/// </summary>
public sealed class CharacterRecord
{
    private readonly GameState _state;

    public CharacterRecord(GameState state, int slot)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (slot < 0 || slot >= StateFormat.CharacterSlots) throw new ArgumentOutOfRangeException(nameof(slot));
        _state = state;
        Slot = slot;
        Base = StateFormat.RecordOffset(slot);
    }

    /// <summary>Smallest attribute value the editors write (the game rolls 1..6).</summary>
    public const int MinAttribute = 1;

    /// <summary>Largest attribute or skill value the editors write.</summary>
    public const int MaxValue = 99;

    public int Slot { get; }

    /// <summary>Block offset of the record.</summary>
    public int Base { get; }

    public GameState State => _state;

    /// <summary>An empty slot (never filled, or the character died — death zeroes the record).</summary>
    public bool IsEmpty => StateFormat.IsEmptySlot(_state.Snapshot, Base);

    // ---- identity ---------------------------------------------------------------------------

    public string Name => _state.GetString(Base + StateFormat.NameOffset, StateFormat.NameLength);

    public bool SetName(string value)
    {
        var name = (value ?? "").Trim();
        if (name.Length == 0) return false;   // an empty name would turn the slot into an empty one
        return _state.SetString(Base + StateFormat.NameOffset, StateFormat.NameLength, name);
    }

    public bool IsFemale => _state.GetByte(Base + StateFormat.SexOffset) == (byte)'f';

    public bool SetFemale(bool female) => _state.SetByte(Base + StateFormat.SexOffset, female ? 'f' : 'm');

    public int Portrait => _state.GetByte(Base + StateFormat.PortraitOffset);

    public string Career1 => _state.GetString(Base + StateFormat.Career1NameOffset, StateFormat.CareerNameLength);

    public string Career2 => _state.GetString(Base + StateFormat.Career2NameOffset, StateFormat.CareerNameLength);

    public int Career1Id => _state.GetByte(Base + StateFormat.Career1IdOffset);

    public int Career2Id => _state.GetByte(Base + StateFormat.Career2IdOffset);

    public string SocialClass => SkillBook.ClassFor(GetAttribute(5));

    // ---- money --------------------------------------------------------------------------------

    public long Wealth => _state.GetUInt32(Base + StateFormat.WealthOffset);

    public bool SetWealth(long pennies) => _state.SetUInt32(Base + StateFormat.WealthOffset, pennies);

    public long MonthlyIncome => _state.GetUInt32(Base + StateFormat.IncomeOffset);

    public bool SetMonthlyIncome(long pennies) => _state.SetUInt32(Base + StateFormat.IncomeOffset, pennies);

    // ---- condition ----------------------------------------------------------------------------

    public int BodyWeight => (int)_state.GetUInt32(Base + StateFormat.BodyWeightOffset);

    public bool SetBodyWeight(int pounds) => _state.SetUInt32(Base + StateFormat.BodyWeightOffset, pounds);

    public int MaxHealth => (sbyte)_state.GetByte(Base + StateFormat.MaxHealthOffset);

    public bool SetMaxHealth(int value) => _state.SetByte(Base + StateFormat.MaxHealthOffset, Math.Clamp(value, 1, 127));

    public int Health => (sbyte)_state.GetByte(Base + StateFormat.HealthOffset);

    public bool SetHealth(int value) => _state.SetByte(Base + StateFormat.HealthOffset, Math.Clamp(value, 0, 127));

    public int Fatigue => (sbyte)_state.GetByte(Base + StateFormat.FatigueOffset);

    public bool SetFatigue(int value) => _state.SetByte(Base + StateFormat.FatigueOffset, Math.Clamp(value, 0, 127));

    public int Mental => (sbyte)_state.GetByte(Base + StateFormat.MentalOffset);

    public bool SetMental(int value) => _state.SetByte(Base + StateFormat.MentalOffset, Math.Clamp(value, 0, 127));

    public bool IsDead => _state.GetByte(Base + StateFormat.DeadOffset) != 0;

    public bool HasHorse => _state.GetByte(Base + StateFormat.HorseOffset) != 0;

    public bool SetHorse(bool value) => _state.SetByte(Base + StateFormat.HorseOffset, value ? 1 : 0);

    /// <summary>The "b" of "HEALTH: a/b": health at or below this is out of action.</summary>
    public int UnconsciousThreshold => StateFormat.UnconsciousThreshold(MaxHealth, GetAttribute(0), GetAttribute(2));

    /// <summary>Out of action by wounds, or by fatigue reaching STR, AGI or END (M.EXE 1000:094C).</summary>
    public bool IsOutOfAction =>
        Health <= UnconsciousThreshold ||
        Fatigue >= GetAttribute(0) || Fatigue >= GetAttribute(1) || Fatigue >= GetAttribute(2);

    // ---- attributes and skills ------------------------------------------------------------------

    public int GetAttribute(int index)
    {
        if ((uint)index >= StateFormat.AttributeCount) throw new ArgumentOutOfRangeException(nameof(index));
        return _state.GetByte(Base + StateFormat.AttributesOffset + index);
    }

    public bool SetAttribute(int index, int value)
    {
        if ((uint)index >= StateFormat.AttributeCount) throw new ArgumentOutOfRangeException(nameof(index));
        return _state.SetByte(Base + StateFormat.AttributesOffset + index, Math.Clamp(value, MinAttribute, MaxValue));
    }

    public int GetSkill(int index)
    {
        if ((uint)index >= StateFormat.SkillCount) throw new ArgumentOutOfRangeException(nameof(index));
        return _state.GetByte(Base + StateFormat.SkillsOffset + index);
    }

    public bool SetSkill(int index, int value)
    {
        if ((uint)index >= StateFormat.SkillCount) throw new ArgumentOutOfRangeException(nameof(index));
        return _state.SetByte(Base + StateFormat.SkillsOffset + index, Math.Clamp(value, 0, MaxValue));
    }

    // ---- equipment ----------------------------------------------------------------------------

    /// <summary>1-based slot of the weapon in hand, 0 = fists.</summary>
    public int WeaponSlot => _state.GetByte(Base + StateFormat.WeaponSlotOffset);

    /// <summary>1-based slot of the armour worn, 0 = none.</summary>
    public int ArmorSlot => _state.GetByte(Base + StateFormat.ArmorSlotOffset);

    public InventoryEntry GetEntry(int entry)
    {
        if ((uint)entry >= StateFormat.InventorySlots) throw new ArgumentOutOfRangeException(nameof(entry));
        return InventoryEntry.FromBytes(_state.Snapshot, StateFormat.InventoryEntryOffset(Slot, entry));
    }

    public bool SetEntry(int entry, InventoryEntry value)
    {
        if ((uint)entry >= StateFormat.InventorySlots) throw new ArgumentOutOfRangeException(nameof(entry));
        return _state.SetBytes(StateFormat.InventoryEntryOffset(Slot, entry), value.ToBytes());
    }

    public bool IsInUse(int entry) => _state.GetByte(StateFormat.InUseFlagOffset(Slot, entry)) != 0;

    public bool SetInUse(int entry, bool value) => _state.SetByte(StateFormat.InUseFlagOffset(Slot, entry), value ? 1 : 0);

    /// <summary>Occupied entries. The game keeps them packed from slot 0.</summary>
    public int ItemCount
    {
        get
        {
            int n = 0;
            while (n < StateFormat.InventorySlots && !GetEntry(n).IsEmpty) n++;
            return n;
        }
    }

    /// <summary>CARRIED WEIGHT: the sum of the items' sixteenths of a pound, divided by 16.</summary>
    public int CarriedWeight
    {
        get
        {
            int total = 0;
            for (int i = 0; i < StateFormat.InventorySlots; i++)
            {
                var e = GetEntry(i);
                if (e.IsEmpty) break;
                total += ItemBook.ById(e.ItemId)?.Weight16 ?? 0;
            }
            return total / 16;
        }
    }

    /// <summary>
    /// Appends an item at the first free slot, the way TAKE does. A firearm arrives loaded with SHOT and
    /// <paramref name="rounds"/> in reserve. Returns the 0-based slot, or −1 if the pack is full or a write failed.
    /// </summary>
    public int AddItem(int itemId, int rounds = 0)
    {
        var item = ItemBook.ById(itemId);
        if (item is null || item.IsEmptyRecord) return -1;
        int slot = ItemCount;
        if (slot >= StateFormat.InventorySlots) return -1;
        var entry = item.UsesAmmunition
            ? new InventoryEntry(itemId, ItemBook.Shot - 1, Math.Clamp(rounds, 0, ushort.MaxValue), item.Shots)
            : new InventoryEntry(itemId, 0, 0, 0);
        if (!SetEntry(slot, entry)) return -1;
        return SetInUse(slot, false) ? slot : -1;
    }

    /// <summary>
    /// Removes an item the way DROP does: later entries and their "in use" flags shift down one, and the weapon
    /// and armour slot numbers are cleared (if they pointed at it) or renumbered (if they pointed past it).
    /// </summary>
    public bool RemoveItem(int entry)
    {
        int count = ItemCount;
        if (entry < 0 || entry >= count) return false;

        var entries = new byte[StateFormat.InventorySlots * StateFormat.InventoryEntrySize];
        var flags = new byte[StateFormat.InventorySlots];
        int e = 0;
        for (int i = 0; i < count; i++)
        {
            if (i == entry) continue;
            Array.Copy(GetEntry(i).ToBytes(), 0, entries, e * StateFormat.InventoryEntrySize, StateFormat.InventoryEntrySize);
            flags[e] = (byte)(IsInUse(i) ? 1 : 0);
            e++;
        }
        // Two separate regions, so two writes. If the second is refused, put the first back: shifted entries
        // with unshifted flags would leave every later item's "in use" state on the wrong entry.
        int entriesOffset = StateFormat.InventoryEntryOffset(Slot, 0);
        var originalEntries = _state.GetBytes(entriesOffset, entries.Length);
        if (!_state.SetBytes(entriesOffset, entries)) return false;
        if (!_state.SetBytes(StateFormat.InUseFlagOffset(Slot, 0), flags))
        {
            // Put the entries back so items and flags stay paired; if even that is refused, re-read the block so
            // the cache at least shows what the target really holds.
            if (!_state.SetBytes(entriesOffset, originalEntries)) _state.Refresh();
            return false;
        }

        return FixSlotReference(StateFormat.WeaponSlotOffset, entry) & FixSlotReference(StateFormat.ArmorSlotOffset, entry);
    }

    /// <summary>
    /// Swaps the item in an occupied slot for another, keeping the entry consistent with the new item: a firearm gets
    /// a valid ammunition type and at most a full load, anything else gets its ammunition bytes cleared, and the
    /// weapon-in-hand or armour-worn reference is dropped if the new item cannot fill it. The "in use" flag survives only
    /// on a slot that is still wielded or worn (a lit lantern swapped for a rifle is not a rifle in hand).
    /// </summary>
    public bool ReplaceItem(int entry, int itemId)
    {
        if ((uint)entry >= StateFormat.InventorySlots) return false;
        var current = GetEntry(entry);
        var item = ItemBook.ById(itemId);
        if (current.IsEmpty || item is null || item.IsEmptyRecord) return false;
        if (current.ItemId == itemId) return true;

        InventoryEntry next;
        if (item.UsesAmmunition)
        {
            int ammo = IsAmmunitionIndex(current.AmmoIndex) ? current.AmmoIndex : ItemBook.Shot - 1;
            next = new InventoryEntry(itemId, ammo, current.Rounds, Math.Min(current.InGun, item.Shots));
        }
        else
        {
            next = new InventoryEntry(itemId, 0, 0, 0);
        }
        if (!SetEntry(entry, next)) return false;

        bool ok = true;
        bool wielded = WeaponSlot == entry + 1, worn = ArmorSlot == entry + 1;
        if (wielded && !item.IsHandWeapon) { ok &= _state.SetByte(Base + StateFormat.WeaponSlotOffset, 0); wielded = false; }
        if (worn && item.Kind != ItemType.Armor) { ok &= _state.SetByte(Base + StateFormat.ArmorSlotOffset, 0); worn = false; }
        if (!wielded && !worn && IsInUse(entry)) ok &= SetInUse(entry, false);
        return ok;
    }

    private static bool IsAmmunitionIndex(int index) => index >= ItemBook.Shot - 1 && index <= ItemBook.Shrapnel - 1;

    private bool FixSlotReference(int fieldOffset, int removedEntry)
    {
        int oneBased = _state.GetByte(Base + fieldOffset);
        if (oneBased == 0) return true;
        int zeroBased = oneBased - 1;
        if (zeroBased == removedEntry) return _state.SetByte(Base + fieldOffset, 0);
        if (zeroBased > removedEntry) return _state.SetByte(Base + fieldOffset, oneBased - 1);
        return true;
    }

    /// <summary>Puts an item in hand the way USE does for a weapon (types 4-9): slot number set, "in use" set.</summary>
    public bool Wield(int entry)
    {
        if ((uint)entry >= StateFormat.InventorySlots) return false;
        var e = GetEntry(entry);
        if (e.IsEmpty || ItemBook.ById(e.ItemId) is not { IsHandWeapon: true }) return false;
        int previous = WeaponSlot;
        // The stored slot is a raw byte: only trust it as an index when it is one, or a damaged save would have
        // us clear a flag belonging to another character (or to the ground-object table).
        if (previous is > 0 and <= StateFormat.InventorySlots && previous - 1 != entry && !SetInUse(previous - 1, false)) return false;
        return _state.SetByte(Base + StateFormat.WeaponSlotOffset, entry + 1) && SetInUse(entry, true);
    }

    /// <summary>Wears armour the way USE does for type 12.</summary>
    public bool Wear(int entry)
    {
        if ((uint)entry >= StateFormat.InventorySlots) return false;
        var e = GetEntry(entry);
        if (e.IsEmpty || ItemBook.ById(e.ItemId) is not { Kind: ItemType.Armor }) return false;
        int previous = ArmorSlot;
        if (previous is > 0 and <= StateFormat.InventorySlots && previous - 1 != entry && !SetInUse(previous - 1, false)) return false;
        return _state.SetByte(Base + StateFormat.ArmorSlotOffset, entry + 1) && SetInUse(entry, true);
    }

    // ---- quick actions -------------------------------------------------------------------------

    /// <summary>Health to maximum, fatigue and insanity to zero. Returns false if any write failed.</summary>
    public bool FullHeal() =>
        SetHealth(MaxHealth) & SetFatigue(0) & SetMental(0);

    /// <summary>
    /// Raises every attribute below <paramref name="value"/> to it (higher ones are left alone, like
    /// <see cref="MaxSkills"/>); maximum health is raised to STR + END if that is now higher, and health rises by the
    /// same amount. (The knock-out line is max − ⌈(STR + END) ÷ 2⌉, so raising only the maximum could leave a wounded
    /// character out of action.)
    /// </summary>
    public bool MaxAttributes(int value = SkillBook.MaxRolledAttribute)
    {
        bool ok = true;
        for (int i = 0; i < StateFormat.AttributeCount; i++)
            if (GetAttribute(i) < value) ok &= SetAttribute(i, value);
        int max = GetAttribute(0) + GetAttribute(2);
        if (MaxHealth < max)
        {
            int gain = max - MaxHealth;
            int health = Math.Min(Health + gain, max);
            ok &= SetMaxHealth(max);
            ok &= SetHealth(health);
        }
        return ok;
    }

    /// <summary>Every skill to <paramref name="value"/> (the rules cap a skill at its attribute; the game does not check).</summary>
    public bool MaxSkills(int value = SkillBook.MaxRolledAttribute)
    {
        bool ok = true;
        for (int i = 0; i < StateFormat.SkillCount; i++)
            if (GetSkill(i) < value) ok &= SetSkill(i, value);
        return ok;
    }

    /// <summary>Tops up every firearm's reserve to <paramref name="rounds"/> and its IN GUN to a full load.</summary>
    public bool RefillAmmunition(int rounds = 999)
    {
        bool ok = true;
        for (int i = 0; i < StateFormat.InventorySlots; i++)
        {
            var e = GetEntry(i);
            if (e.IsEmpty) break;
            if (ItemBook.ById(e.ItemId) is not { UsesAmmunition: true } item) continue;
            int ammo = IsAmmunitionIndex(e.AmmoIndex) ? e.AmmoIndex : ItemBook.Shot - 1;
            var full = e with { AmmoIndex = ammo, Rounds = Math.Max(e.Rounds, rounds), InGun = Math.Max(e.InGun, item.Shots) };
            if (full != e) ok &= SetEntry(i, full);
        }
        return ok;
    }
}
