namespace DarkDesigns1Trainer.ViewModels;

/// <summary>
/// The carried pack an <see cref="ItemSlotViewModel"/> belongs to, so a slot can copy its item
/// into a free slot without knowing whether it is editing live memory or a loaded save file.
/// </summary>
public interface IItemPack
{
    /// <summary>True when at least one carried slot is empty.</summary>
    bool HasFreeSlot { get; }

    /// <summary>
    /// Puts <paramref name="itemId"/> in the first empty carried slot, writes it through, and
    /// refreshes the slot editors. Returns false when the pack is full.
    /// </summary>
    bool TryAddItem(int itemId);
}
