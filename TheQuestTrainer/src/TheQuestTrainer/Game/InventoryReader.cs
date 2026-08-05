using TheQuestTrainer.Memory;

namespace TheQuestTrainer.Game;

/// <summary>One carried item, resolved through its type.</summary>
public sealed record CarriedItem
{
    /// <summary>Position in the character's item vector.</summary>
    public required int Index { get; init; }

    /// <summary>Address of the 16-byte item object.</summary>
    public required uint Address { get; init; }

    /// <summary>The shared type this item points at.</summary>
    public required ItemType Type { get; init; }

    /// <summary>The item's one mutable word, as stored.</summary>
    public required int Meter { get; init; }

    /// <summary>
    /// What that word can be filled to — the type's maximum condition, or a wand's full charge
    /// count read out of its enchantment. Zero when the item has no meter to fill.
    /// </summary>
    public required int MeterMax { get; init; }

    /// <summary>Which body slot holds this item, or null when it is not equipped.</summary>
    public required int? EquippedSlot { get; init; }

    /// <summary>Which weapon set it is equipped in, when it is equipped.</summary>
    public required int? EquippedSet { get; init; }

    /// <summary>Whether the item is worn or wielded right now.</summary>
    public bool IsEquipped => EquippedSlot is not null;

    /// <summary>Whether "Restore" has anything to do to this item.</summary>
    public bool CanRestore => MeterMax > 0 && Meter < MeterMax;

    /// <summary>
    /// The meter as the game's own item panel would put it: a wear band for condition, a fraction
    /// for charges, a count for ammunition, and nothing at all for a book or a potion.
    /// </summary>
    public string MeterLabel => Type.Meter switch
    {
        ItemMeter.Condition when MeterMax > 0 =>
            $"{ItemTables.ConditionBand(Meter, MeterMax)} ({Meter:N0}/{MeterMax:N0})",
        ItemMeter.Charges when MeterMax > 0 => $"{Meter:N0}/{MeterMax:N0} charges",
        ItemMeter.Charges => $"{Meter:N0} charges",
        ItemMeter.Units => $"{Meter:N0} unit{(Meter == 1 ? "" : "s")}",
        _ => "",
    };
}

/// <summary>The character's whole pack, read in one pass.</summary>
public sealed record InventorySnapshot
{
    /// <summary>Address of the character record it was read from.</summary>
    public required uint Record { get; init; }

    /// <summary>The engine object the record is embedded in — the catalog sweep needs it.</summary>
    public required uint Engine { get; init; }

    /// <summary>Carried items in the game's own order.</summary>
    public required IReadOnlyList<CarriedItem> Items { get; init; }

    /// <summary>Total weight in hundredths of a unit, summed the way the encumbrance check does.</summary>
    public required int TotalWeight { get; init; }

    /// <summary>Total weight the way the game prints one.</summary>
    public string TotalWeightLabel => $"{TotalWeight / 100}.{TotalWeight % 100 / 10}";
}

/// <summary>
/// Reads the carried-items vector out of a validated character record.
///
/// Two things make this more careful than a normal array read. The vector's elements are pointers
/// into the heap, so every one is followed and validated rather than trusted; and an item is only
/// "equipped" by virtue of its pointer appearing in one of the two equipment arrays, so those are
/// read first and the items matched against them.
/// </summary>
public static class InventoryReader
{
    /// <summary>
    /// Snapshots the inventory of the record at <paramref name="record"/>. Returns null when the
    /// vector could not be read or does not look like one — the caller shows nothing rather than a
    /// list built out of whatever the bytes happened to be.
    /// </summary>
    public static InventorySnapshot? Read(IMemorySource source, uint record)
    {
        ArgumentNullException.ThrowIfNull(source);

        uint engine = record - QuestLayout.RecordInEngine;

        if (!TryReadUInt32(source, record + ItemLayout.InventoryBegin, out uint begin)) return null;
        if (!TryReadUInt32(source, record + ItemLayout.InventoryEnd, out uint end)) return null;

        int count = VectorLength(begin, end);
        if (count < 0) return null;

        var equipment = ReadEquipment(source, record);

        var pointers = new byte[count * 4];
        if (count > 0 && source.Read(begin, pointers, pointers.Length) != pointers.Length) return null;

        var items = new List<CarriedItem>(count);
        int totalWeight = 0;

        for (int i = 0; i < count; i++)
        {
            uint address = BitConverter.ToUInt32(pointers, i * 4);
            if (address == 0) continue;

            var item = new byte[ItemLayout.ItemBytes];
            if (source.Read(address, item, item.Length) != item.Length) continue;

            uint typeAddress = BitConverter.ToUInt32(item, (int)ItemLayout.ItemType);
            var type = ItemTypeReader.Read(source, typeAddress, engine);
            if (type is null) continue;

            uint enchantments = BitConverter.ToUInt32(item, (int)ItemLayout.ItemEnchantments);
            int meter = BitConverter.ToUInt16(item, (int)ItemLayout.ItemCondition);

            equipment.TryGetValue(address, out var where);
            totalWeight += type.Weight;

            items.Add(new CarriedItem
            {
                Index = i,
                Address = address,
                Type = type,
                Meter = meter,
                MeterMax = MeterMax(source, type, enchantments),
                EquippedSlot = where.Slot,
                EquippedSet = where.Set,
            });
        }

        return new InventorySnapshot
        {
            Record = record,
            Engine = engine,
            Items = items,
            TotalWeight = totalWeight,
        };
    }

    /// <summary>
    /// What an item of this type can be filled to. Condition comes from the type; charges come from
    /// the first entry of the item's own enchantment vector, or the type's when it has none of its
    /// own — which is exactly the fallback the game's "recharge the wand" code does.
    /// </summary>
    public static int MeterMax(IMemorySource source, ItemType type, uint itemEnchantments)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(type);

        switch (type.Meter)
        {
            case ItemMeter.Condition:
                return type.MaxCondition;

            case ItemMeter.Charges:
                uint vector = itemEnchantments != 0 ? itemEnchantments : type.Enchantments;
                if (vector == 0) return 0;
                if (!TryReadUInt32(source, vector, out uint first)) return 0;
                if (!TryReadUInt32(source, vector + 4, out uint last)) return 0;
                if (VectorLength(first, last) <= 0) return 0;
                if (!TryReadUInt32(source, first, out uint enchantment) || enchantment == 0) return 0;
                var word = new byte[2];
                return source.Read(enchantment + 4, word, 2) == 2 ? BitConverter.ToUInt16(word, 0) : 0;

            default:
                return 0;
        }
    }

    /// <summary>
    /// Maps every equipped item pointer to the slot and weapon set holding it. Both sets are read:
    /// the game swaps between them, and an item equipped in the inactive one is still equipped.
    /// </summary>
    private static Dictionary<uint, (int? Slot, int? Set)> ReadEquipment(IMemorySource source, uint record)
    {
        var map = new Dictionary<uint, (int?, int?)>();
        var slots = new byte[ItemLayout.EquipmentSlotCount * 4];

        for (int set = 0; set < 2; set++)
        {
            uint at = ItemLayout.EquipmentSlot(record, set, 0);
            if (source.Read(at, slots, slots.Length) != slots.Length) continue;

            for (int slot = 0; slot < ItemLayout.EquipmentSlotCount; slot++)
            {
                uint pointer = BitConverter.ToUInt32(slots, slot * 4);
                if (pointer != 0) map.TryAdd(pointer, (slot, set));
            }
        }

        return map;
    }

    /// <summary>
    /// Elements in a <c>std::vector</c> of dwords, or -1 when the two pointers are not a plausible
    /// one — misordered, misaligned, or longer than <see cref="ItemLayout.MaxItems"/>.
    /// </summary>
    private static int VectorLength(uint begin, uint end)
    {
        if (begin == 0 && end == 0) return 0;
        if (begin == 0 || end < begin) return -1;
        uint bytes = end - begin;
        if (bytes % 4 != 0) return -1;
        uint count = bytes / 4;
        return count > ItemLayout.MaxItems ? -1 : (int)count;
    }

    private static bool TryReadUInt32(IMemorySource source, uint address, out uint value)
    {
        var word = new byte[4];
        if (source.Read(address, word, 4) != 4) { value = 0; return false; }
        value = BitConverter.ToUInt32(word, 0);
        return true;
    }
}
