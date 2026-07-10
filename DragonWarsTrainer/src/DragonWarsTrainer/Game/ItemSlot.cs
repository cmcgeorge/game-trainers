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

    /// <summary>
    /// True when the item's magic-effect bytes mark it as consuming charges/uses, mirroring the
    /// <c>chargeable</c> logic of the dragonjars <c>Item</c> parser (byte 6): a cleared high bit
    /// means a spell/effect-on-use item, and among high-bit effects only 1, 2, 3 and the fallback
    /// are chargeable (0 = flag, 4 = restores power). Empty slots are never chargeable.
    /// </summary>
    public bool IsChargeable
    {
        get
        {
            if (IsEmpty) return false;
            byte one = U8(InventoryFormat.OffMagicEffect1);
            if ((one & 0x80) == 0) return true;
            int effect = one & 0x7F;
            return effect switch { 0 => false, 4 => false, _ => true };
        }
    }

    /// <summary>
    /// Overwrites this slot from a catalog prototype: copies the 11-byte header verbatim, then
    /// re-encodes the template name into the 12-byte name field (zero-padded), rewriting the whole
    /// 23-byte slot.
    /// </summary>
    public void Apply(ItemTemplate template)
    {
        Array.Copy(template.Header, 0, _bytes, _base, InventoryFormat.OffItemName);
        Name = template.Name;
    }

    /// <summary>Copies this slot's full 23 bytes over another slot in the same record buffer.</summary>
    public void CopyTo(ItemSlot destination) =>
        Array.Copy(_bytes, _base, destination._bytes, destination._base, InventoryFormat.SlotSize);

    /// <summary>Wipes the slot to empty (all zero bytes).</summary>
    public void Clear() => Array.Clear(_bytes, _base, InventoryFormat.SlotSize);
}
