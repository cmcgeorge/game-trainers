namespace TheQuestTrainer.Game;

/// <summary>
/// Where the character's carried items live, and what an item is made of.
///
/// The Quest does not keep items inside the character record the way most of the games in this
/// repository do. The record holds a <c>std::vector&lt;SItem*&gt;</c>, and each element is a small
/// heap object that is almost entirely a pointer to a shared, read-mostly <b>item type</b> — the
/// thing that owns the name, the weight, the damage dice and the maximum condition. Two Loaves of
/// Bread are two 16-byte allocations pointing at one <c>base_com_bread</c> type.
///
/// That split is the single most important fact here, and it is what makes the trainer's edits
/// small:
///
/// <list type="bullet">
/// <item>Per-item state is one word (<see cref="ItemCondition"/>). Everything else is the type.</item>
/// <item>Giving the player a different item is a <i>pointer</i> write — swap the type. Nothing has
///   to be allocated in the game's heap, which the trainer has no safe way to do.</item>
/// </list>
///
/// Offsets were read out of <c>TheQuest.exe</c> v1.9.10 with Ghidra and then confirmed against a
/// live session; <c>docs/ReverseEngineering.md</c> §15 derives each one. As in
/// <see cref="QuestLayout"/>, sizes are restated as arithmetic so a mistyped constant fails a
/// harness check instead of quietly reading the wrong field.
/// </summary>
public static class ItemLayout
{
    // ---- the inventory vector, inside the character record ---------------------------------

    /// <summary>
    /// First of the three pointers of the carried-items <c>std::vector&lt;SItem*&gt;</c>, measured
    /// from the character record. The game's encumbrance check walks exactly this pair —
    /// <c>(end - begin) / 4</c> elements, each an item whose type's weight it sums.
    /// </summary>
    public const uint InventoryBegin = 0x320;

    /// <summary>One past the last element.</summary>
    public const uint InventoryEnd = InventoryBegin + 4;

    /// <summary>End of the allocation. Read for validation only — the trainer never grows the vector.</summary>
    public const uint InventoryCapacity = InventoryEnd + 4;

    /// <summary>
    /// The most carried items the reader will walk. The game has no inventory cap of its own (it
    /// caps by weight), so this is the trainer's own guard against a garbage vector: a plausible
    /// pack is dozens of items, and a length beyond this means the two pointers are not a vector.
    /// </summary>
    public const int MaxItems = 512;

    // ---- the equipment slots, also inside the record ----------------------------------------

    /// <summary>
    /// Pointers to the equipped items, one per body slot. An item is "equipped" precisely when its
    /// pointer appears here; there is no flag on the item itself. Slot 0 is unused, the same
    /// convention the attribute and skill arrays follow.
    /// </summary>
    public const uint EquipmentSlots = 0x334;

    /// <summary>Body slots per weapon set, including the unused slot 0.</summary>
    public const int EquipmentSlotCount = 14;

    /// <summary>
    /// The second weapon set — the loadout <c>keySwitchWeapons</c> flips to. Same shape, and the
    /// game reads whichever <see cref="ActiveWeaponSet"/> selects, so the trainer has to search both
    /// before it can say an item is not equipped.
    /// </summary>
    public const uint EquipmentSlotsSet2 = EquipmentSlots + EquipmentSlotCount * 4;

    /// <summary>Which of the two sets is live: 0 for the first, non-zero for the second.</summary>
    public const uint ActiveWeaponSet = EquipmentSlotsSet2 + EquipmentSlotCount * 4;

    // ---- the item object --------------------------------------------------------------------

    /// <summary>Pointer to the shared item type. This is the whole identity of the item.</summary>
    public const uint ItemType = 0x00;

    /// <summary>
    /// Pointer to the item's own enchantment vector, or 0 when it has none of its own and inherits
    /// the type's (<see cref="TypeEnchantments"/>). Read to find a wand's maximum charges; never
    /// written.
    /// </summary>
    public const uint ItemEnchantments = ItemType + 4;

    /// <summary>
    /// The one mutable word on an item, and the only thing the trainer writes here. What it means
    /// depends on the type's category: wear for anything with a maximum condition, remaining charges
    /// for a wand, and the count held for a quiver or a stack of throwing weapons.
    /// </summary>
    public const uint ItemCondition = ItemEnchantments + 4;

    /// <summary>
    /// Bytes the game's own code ever touches on an item. The allocation is 16 bytes wide, but
    /// <c>+0x0A</c> onwards is slack: it reads zero in items allocated from fresh heap pages and
    /// holds leftovers in ones from recycled blocks, and nothing in the disassembly reads it.
    /// </summary>
    public const int ItemBytes = 12;

    // ---- the item type ----------------------------------------------------------------------

    /// <summary>
    /// Back-pointer to the engine object. Every item type carries it, which makes it the cheapest
    /// and strongest thing to validate a candidate type against — see <see cref="ItemCatalog"/>.
    /// </summary>
    public const uint TypeEngine = 0x00;

    /// <summary>The type's vtable, in the image's read-only data.</summary>
    public const uint TypeVTable = TypeEngine + 4;

    /// <summary>Pointer to the internal id, e.g. <c>base_shield_smallwooden</c>. A plain C string.</summary>
    public const uint TypeId = TypeVTable + 4;

    /// <summary>Pointer to the display resource id, e.g. <c>bres_helm_helm</c>.</summary>
    public const uint TypeResourceId = TypeId + 8;

    /// <summary>Pointer to the name the game shows, e.g. <c>Small Wooden Shield</c>.</summary>
    public const uint TypeName = TypeResourceId + 4;

    /// <summary>
    /// The type's built-in enchantment vector, used when the item has none of its own. Its first
    /// entry's <c>+4</c> word is a wand's full charge count.
    /// </summary>
    public const uint TypeEnchantments = 0x28;

    /// <summary>Weight in hundredths of a unit — the game prints it as <c>Weight: %u.%u</c>.</summary>
    public const uint TypeWeight = 0x32;

    /// <summary>Minimum damage, for weapons.</summary>
    public const uint TypeDamageMin = 0x36;

    /// <summary>Maximum damage.</summary>
    public const uint TypeDamageMax = TypeDamageMin + 2;

    /// <summary>How much enchantment the item can hold. Shown as "Enchant storage".</summary>
    public const uint TypeEnchantStorage = 0x3C;

    /// <summary>
    /// Full condition. The game shows wear as <c>condition * 100 / this</c>, so a type whose
    /// category displays condition must never have a zero here — which is why
    /// <see cref="ItemCatalog"/> refuses to offer one that does.
    /// </summary>
    public const uint TypeMaxCondition = TypeEnchantStorage + 2;

    /// <summary>Item category, 1..15 — see <see cref="ItemTables.CategoryName"/>.</summary>
    public const uint TypeCategory = 0x45;

    /// <summary>Sub-type within the category, e.g. category 2 sub-type 4 is a Helm.</summary>
    public const uint TypeSubtype = TypeCategory + 1;

    /// <summary>Alignment the item demands: 1 good, 2 evil, 0 either.</summary>
    public const uint TypeAlignment = TypeSubtype + 1;

    /// <summary>Flag byte. Bit 1 separates a light weapon from a heavy one.</summary>
    public const uint TypeFlags = TypeAlignment + 1;

    /// <summary>Bit of <see cref="TypeFlags"/> that marks a category-1 weapon as <i>light</i>.</summary>
    public const byte FlagLightWeapon = 0x02;

    /// <summary>Size of the type object. Types sit back-to-back in the heap at this plus a header.</summary>
    public const int TypeBytes = 0x50;

    /// <summary>Address of carried-item slot <paramref name="index"/> in the vector at <paramref name="begin"/>.</summary>
    public static uint ItemSlot(uint begin, int index) => begin + (uint)index * 4;

    /// <summary>Address of equipment slot <paramref name="slot"/> of the given set in <paramref name="record"/>.</summary>
    public static uint EquipmentSlot(uint record, int set, int slot) =>
        record + (set == 0 ? EquipmentSlots : EquipmentSlotsSet2) + (uint)slot * 4;
}
