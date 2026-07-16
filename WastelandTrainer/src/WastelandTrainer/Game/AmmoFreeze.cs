namespace WastelandTrainer.Game;

/// <summary>
/// Applies the trainer's Freeze Ammo action. Every poll tick it tops each <em>ammo-bearing</em>
/// inventory slot — a weapon that fires, or a clip/shell/power pack (see
/// <see cref="ItemCatalog.IsAmmoItem"/>) — up to <see cref="CharacterFormat.MaxAmmo"/>, so a ranger
/// never runs low, and it <b>clears the jammed-weapon flag</b> (the quantity byte's high bit) so a
/// frozen weapon can't stay jammed. A count already at or above the max is never reduced (only the
/// jam bit is cleared), and non-ammo items (melee weapons, armor, gear and quest items, whose second
/// byte is unused or a status byte) are skipped so the freeze can't corrupt them.
///
/// Clearing the jam is deliberate: without it a weapon that jams in combat while sitting at max ammo
/// gets stuck — the old "already full, skip" fast-path never rewrote the slot, so nothing (freeze on
/// <i>or</i> off) could clear bit 7. Treating Freeze Ammo as "unlimited &amp; never jams" removes that
/// trap and matches what the toggle is for.
///
/// Stateless: the current bytes are the only input, so there is no snapshot to seed and nothing to
/// reset when the toggle flips. Kept in the <c>Game/</c> layer so <c>FormatCheck</c> can test it.
/// </summary>
public static class AmmoFreeze
{
    /// <summary>
    /// Tops every ammo-bearing slot in <paramref name="record"/> up to
    /// <see cref="CharacterFormat.MaxAmmo"/> and clears its jammed-weapon flag (quantity byte bit 7),
    /// mutating the inventory bytes in place. Returns <c>true</c> when at least one slot changed — its
    /// count was raised or its jam bit was cleared — (so the caller pokes those quantity bytes back to
    /// memory and refreshes the rows). When <paramref name="toppedSlots"/> is supplied it is cleared
    /// and filled with the indices changed, so only those rows need refreshing. Item ids are read,
    /// never written; a count already above the max is kept as-is (never lowered) apart from clearing
    /// the jam bit.
    /// </summary>
    public static bool TopUp(CharacterRecord record, List<int>? toppedSlots = null)
    {
        toppedSlots?.Clear();
        bool changed = false;
        for (int slot = 0; slot < CharacterFormat.ItemSlots; slot++)
        {
            int id = record.GetItemId(slot);
            if (id == 0 || !ItemCatalog.IsAmmoItem(id)) continue;

            int rawQty = record.GetItemQty(slot);
            int count = rawQty & CharacterFormat.InventoryCountMask;   // low 7 bits; bit 7 = jammed flag

            // Raise the count to the max but never lower it, and drop the jam bit (bit 7 cleared) so a
            // frozen weapon can't stay jammed.
            int newQty = count < CharacterFormat.MaxAmmo ? CharacterFormat.MaxAmmo : count;
            if (newQty == rawQty) continue;   // already full and un-jammed — nothing to do

            record.SetItem(slot, id, newQty);
            changed = true;
            toppedSlots?.Add(slot);
        }
        return changed;
    }
}
