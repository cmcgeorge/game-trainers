namespace DragonWarsTrainer.Game;

/// <summary>
/// Byte-level layout of the Dragon Wars per-character inventory, which occupies the tail of the
/// 512-byte character record: <see cref="SlotCount"/> fixed-size item slots
/// (<see cref="SlotSize"/> bytes each) starting at <see cref="OffInventory"/>. The block ends
/// exactly at the record boundary (0xEC + 12 × 23 = 0x200), which self-validates the geometry.
///
/// The slot structure was sourced from the open-source <c>fraterrisus/dragonjars</c>
/// reimplementation (its <c>Item</c> parser and <c>Lists.ITEM_TYPES</c> / <c>Lists.REQUIREMENTS</c>)
/// and the hitchhikerprod walkthrough. The captured dumps start with empty inventories, so the
/// field offsets below are transcribed from that source and are pending live-game confirmation.
/// </summary>
public static class InventoryFormat
{
    /// <summary>Offset of the first item slot within the character record.</summary>
    public const int OffInventory = 0xEC;

    /// <summary>Number of carried-item slots.</summary>
    public const int SlotCount = 12;

    /// <summary>Size of one item slot in bytes.</summary>
    public const int SlotSize = 23;     // 0x17

    // --- per-slot field offsets (relative to the slot's start) ----------------
    /// <summary>Byte 0: bit7 = equipped, bit6 = disposable, bits5-0 = charges/uses (max 63).</summary>
    public const int OffFlags = 0x00;
    /// <summary>Byte 1: bit7 = reduces AV, bit6 = reduces AC, bits5-0 = requirement skill index.</summary>
    public const int OffRequirement = 0x01;
    /// <summary>Byte 2: bits4-0 = minimum skill rank required to use the item.</summary>
    public const int OffMinRank = 0x02;
    /// <summary>Byte 3: hi nibble = AV modifier magnitude, lo nibble = AC modifier magnitude.</summary>
    public const int OffModifiers = 0x03;
    /// <summary>Byte 4: purchase price (encoded base-10 exponent).</summary>
    public const int OffPrice = 0x04;
    /// <summary>Byte 5: bits4-0 = item type index into <see cref="ItemTypeNames"/>.</summary>
    public const int OffItemType = 0x05;
    /// <summary>Byte 6: magic effect (spell id / taught spell / power restored).</summary>
    public const int OffMagicEffect1 = 0x06;
    /// <summary>Byte 7: magic effect factor (charges / power factor / spell tier).</summary>
    public const int OffMagicEffect2 = 0x07;
    /// <summary>Byte 8: primary weapon damage dice.</summary>
    public const int OffDamage1 = 0x08;
    /// <summary>Byte 9: secondary weapon damage dice.</summary>
    public const int OffDamage2 = 0x09;
    /// <summary>Byte 10: hi nibble = ammo type, lo nibble = range (in 10-foot units).</summary>
    public const int OffAmmoRange = 0x0A;
    /// <summary>Bytes 11-22: item name (12 bytes, high-bit encoding).</summary>
    public const int OffItemName = 0x0B;
    public const int ItemNameLength = 12;

    // --- byte-0 bit masks ----------------------------------------------------
    public const byte FlagEquipped = 0x80;
    public const byte FlagDisposable = 0x40;
    public const byte MaskCharges = 0x3F;

    // --- byte-1 bit masks ----------------------------------------------------
    public const byte FlagReduceAv = 0x80;
    public const byte FlagReduceAc = 0x40;
    public const byte MaskReqSkill = 0x3F;

    /// <summary>
    /// Item-type names, indexed by the low 5 bits of slot byte 5 (<c>Lists.ITEM_TYPES</c> from the
    /// dragonjars reimplementation).
    /// </summary>
    public static readonly string[] ItemTypeNames =
    {
        "General Item", "Shield", "Full Shield", "Axe",
        "Flail", "Sword", "Two-handed sword", "Mace",
        "Bow", "Crossbow", "Gun", "Thrown weapon",
        "Ammunition", "Gloves", "Mage Gloves", "Ammo Clip",
        "Cloth Armor", "Leather Armor", "Cuir Bouilli Armor", "Brigandine Armor",
        "Scale Armor", "Chain Armor", "Plate and Chain Armor", "Full Plate Armor",
        "Helmet", "Scroll", "Boots", "-",
        "-", "-", "-", "-"
    };

    public static string ItemTypeName(int type) =>
        type >= 0 && type < ItemTypeNames.Length ? ItemTypeNames[type] : $"Type {type}";
}
