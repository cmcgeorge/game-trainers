namespace DragonWarsTrainer.Game;

/// <summary>
/// A typed, mutable view over one 23-byte item slot inside a character record's inventory block.
/// It wraps the character's backing buffer at a fixed slot offset, so edits mutate the record in
/// place (ready to be written back to live memory). An empty slot is one whose name is blank
/// (the first name byte is 0).
/// </summary>
public sealed class ItemSlot
{
    private readonly byte[] _bytes;
    private readonly int _base;

    public ItemSlot(byte[] recordBytes, int slotOffset)
    {
        _bytes = recordBytes;
        _base = slotOffset;
    }

    private byte U8(int fieldOffset) => _bytes[_base + fieldOffset];
    private void U8(int fieldOffset, int value) => _bytes[_base + fieldOffset] = (byte)Math.Clamp(value, 0, 255);

    /// <summary>True when the slot holds no item (its name is blank).</summary>
    public bool IsEmpty => _bytes[_base + InventoryFormat.OffItemName] == 0x00;

    // --- name ----------------------------------------------------------------
    public string Name
    {
        get => DragonWarsText.Decode(_bytes, _base + InventoryFormat.OffItemName, InventoryFormat.ItemNameLength);
        set => DragonWarsText.Encode(_bytes, _base + InventoryFormat.OffItemName, InventoryFormat.ItemNameLength, value);
    }

    // --- byte 0: equipped / disposable / charges -----------------------------
    public bool Equipped
    {
        get => (U8(InventoryFormat.OffFlags) & InventoryFormat.FlagEquipped) != 0;
        set
        {
            int v = U8(InventoryFormat.OffFlags);
            v = value ? (v | InventoryFormat.FlagEquipped) : (v & ~InventoryFormat.FlagEquipped);
            U8(InventoryFormat.OffFlags, v);
        }
    }

    public bool Disposable => (U8(InventoryFormat.OffFlags) & InventoryFormat.FlagDisposable) != 0;

    public int Charges
    {
        get => U8(InventoryFormat.OffFlags) & InventoryFormat.MaskCharges;
        set
        {
            int keep = U8(InventoryFormat.OffFlags) & ~InventoryFormat.MaskCharges;
            U8(InventoryFormat.OffFlags, keep | (Math.Clamp(value, 0, InventoryFormat.MaskCharges)));
        }
    }

    // --- byte 1/2: requirements ----------------------------------------------
    public bool ReducesAv => (U8(InventoryFormat.OffRequirement) & InventoryFormat.FlagReduceAv) != 0;
    public bool ReducesAc => (U8(InventoryFormat.OffRequirement) & InventoryFormat.FlagReduceAc) != 0;
    public int RequirementSkill => U8(InventoryFormat.OffRequirement) & InventoryFormat.MaskReqSkill;
    public int MinRank => U8(InventoryFormat.OffMinRank) & 0x1F;

    // --- byte 3: AV / AC modifiers (signed by the reduce flags) --------------
    public int ArmorValueMod
    {
        get { int m = (U8(InventoryFormat.OffModifiers) >> 4) & 0x0F; return ReducesAv ? -m : m; }
    }
    public int ArmorClassMod
    {
        get { int m = U8(InventoryFormat.OffModifiers) & 0x0F; return ReducesAc ? -m : m; }
    }

    // --- byte 5: item type ---------------------------------------------------
    public int ItemType => U8(InventoryFormat.OffItemType) & 0x1F;
    public string TypeName => InventoryFormat.ItemTypeName(ItemType);

    /// <summary>Wipes the slot to empty (all zero bytes).</summary>
    public void Clear() => Array.Clear(_bytes, _base, InventoryFormat.SlotSize);
}
