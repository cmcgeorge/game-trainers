using System.Text;
using TheQuestTrainer.Memory;

namespace TheQuestTrainer.Game;

/// <summary>
/// One shared item type — the read-mostly object every carried item points at, holding the name,
/// the category, the weight and the ceilings the game derives an item's numbers from.
/// </summary>
public sealed record ItemType
{
    /// <summary>Where the type object lives. This is what gets written into an item to retype it.</summary>
    public required uint Address { get; init; }

    /// <summary>The game's internal id, e.g. <c>base_shield_smallwooden</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The name the game shows, e.g. <c>Small Wooden Shield</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Category, 1..15.</summary>
    public required int Category { get; init; }

    /// <summary>Sub-type within the category.</summary>
    public required int Subtype { get; init; }

    /// <summary>Whether a category-1 weapon is a light one — which weapon skill applies.</summary>
    public required bool IsLightWeapon { get; init; }

    /// <summary>Weight in hundredths of a unit, as the game stores it.</summary>
    public required int Weight { get; init; }

    /// <summary>Minimum damage, for weapons.</summary>
    public required int DamageMin { get; init; }

    /// <summary>Maximum damage.</summary>
    public required int DamageMax { get; init; }

    /// <summary>Full condition, or 0 for a type that does not wear out.</summary>
    public required int MaxCondition { get; init; }

    /// <summary>Enchantment the item can hold.</summary>
    public required int EnchantStorage { get; init; }

    /// <summary>Alignment demanded: 1 good, 2 evil, 0 either.</summary>
    public required int Alignment { get; init; }

    /// <summary>The type's built-in enchantment vector, or 0.</summary>
    public required uint Enchantments { get; init; }

    /// <summary>What an item of this type keeps in its one mutable word.</summary>
    public ItemMeter Meter => ItemTables.MeterFor(Category, Subtype);

    /// <summary>Category name as the game's own item panel writes it.</summary>
    public string CategoryLabel => ItemTables.Describe(Category, Subtype, IsLightWeapon);

    /// <summary>Weight the way the game prints it — <c>300</c> is <c>3.0</c>.</summary>
    public string WeightLabel => $"{Weight / 100}.{Weight % 100 / 10}";

    /// <summary>Name plus category, for a picker that is 1,000 entries long.</summary>
    public string PickerLabel => $"{Name}  ·  {CategoryLabel}";
}

/// <summary>
/// Reads and validates an item type at an address.
///
/// The validation is what makes the whole inventory feature safe, because an item's type pointer is
/// the one field the trainer overwrites. Four checks, and a candidate has to pass all of them:
///
/// <list type="number">
/// <item>Its first dword is the engine object — every type carries that back-pointer, and no other
///   structure the sweep walks past does.</item>
/// <item>Its second dword points into the game module, where a vtable belongs.</item>
/// <item>Its category is one of the game's fifteen.</item>
/// <item>Its id and name are both readable, non-empty, printable C strings.</item>
/// </list>
///
/// Taken together those are strong enough that the heap sweep in <see cref="ItemCatalog"/> can find
/// the game's entire item table with no address baked into the trainer at all.
/// </summary>
public static class ItemTypeReader
{
    /// <summary>Longest C string this reader will pull out of the target. Ids and names are short.</summary>
    public const int MaxTextLength = 96;

    /// <summary>
    /// Reads the type at <paramref name="address"/>, or null when it does not validate against
    /// <paramref name="engine"/>.
    /// </summary>
    public static ItemType? Read(IMemorySource source, uint address, uint engine)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (address == 0 || (address & 3) != 0) return null;

        var buffer = new byte[ItemLayout.TypeBytes];
        if (source.Read(address, buffer, buffer.Length) != buffer.Length) return null;
        return Parse(source, buffer, 0, address, engine);
    }

    /// <summary>
    /// Validates and decodes a type whose bytes are already in <paramref name="buffer"/> at
    /// <paramref name="offset"/>. The sweep uses this so it can test a candidate without a second
    /// read of memory it has just pulled in.
    /// </summary>
    public static ItemType? Parse(IMemorySource source, byte[] buffer, int offset, uint address, uint engine)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || offset + ItemLayout.TypeBytes > buffer.Length) return null;

        if (BitConverter.ToUInt32(buffer, offset + (int)ItemLayout.TypeEngine) != engine) return null;

        uint vtable = BitConverter.ToUInt32(buffer, offset + (int)ItemLayout.TypeVTable);
        long rva = (long)vtable - source.ModuleBase;
        if (rva < 0 || rva >= source.ModuleSize) return null;

        int category = buffer[offset + (int)ItemLayout.TypeCategory];
        if (category < 1 || category > ItemTables.MaxCategory) return null;

        string? id = ReadText(source, BitConverter.ToUInt32(buffer, offset + (int)ItemLayout.TypeId));
        string? name = ReadText(source, BitConverter.ToUInt32(buffer, offset + (int)ItemLayout.TypeName));
        if (id is null || name is null) return null;

        return new ItemType
        {
            Address = address,
            Id = id,
            Name = name,
            Category = category,
            Subtype = buffer[offset + (int)ItemLayout.TypeSubtype],
            IsLightWeapon = (buffer[offset + (int)ItemLayout.TypeFlags] & ItemLayout.FlagLightWeapon) != 0,
            Weight = BitConverter.ToUInt16(buffer, offset + (int)ItemLayout.TypeWeight),
            DamageMin = BitConverter.ToUInt16(buffer, offset + (int)ItemLayout.TypeDamageMin),
            DamageMax = BitConverter.ToUInt16(buffer, offset + (int)ItemLayout.TypeDamageMax),
            MaxCondition = BitConverter.ToUInt16(buffer, offset + (int)ItemLayout.TypeMaxCondition),
            EnchantStorage = BitConverter.ToUInt16(buffer, offset + (int)ItemLayout.TypeEnchantStorage),
            Alignment = buffer[offset + (int)ItemLayout.TypeAlignment],
            Enchantments = BitConverter.ToUInt32(buffer, offset + (int)ItemLayout.TypeEnchantments),
        };
    }

    /// <summary>
    /// Reads a NUL-terminated C string, or null when the pointer does not lead to a short run of
    /// printable characters followed by a terminator.
    ///
    /// "Printable" here really is ASCII, unlike the character name in <see cref="StdString"/>: these
    /// are the game's own asset ids and item names out of its data files, not free text a player
    /// typed, and every one of the 1,084 types in the shipped game is plain ASCII. Being strict is
    /// the point — this check is load-bearing for the sweep, which is deciding whether an arbitrary
    /// run of heap bytes is an item type.
    /// </summary>
    public static string? ReadText(IMemorySource source, uint pointer)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (pointer == 0) return null;

        var buffer = new byte[MaxTextLength];
        int got = source.Read(pointer, buffer, buffer.Length);
        if (got != buffer.Length) return null;

        int length = Array.IndexOf(buffer, (byte)0);
        if (length <= 0) return null;

        for (int i = 0; i < length; i++)
            if (buffer[i] < 0x20 || buffer[i] > 0x7E) return null;

        return Encoding.ASCII.GetString(buffer, 0, length);
    }
}
