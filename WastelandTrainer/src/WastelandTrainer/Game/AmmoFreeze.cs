namespace WastelandTrainer.Game;

/// <summary>
/// Applies the trainer's Freeze Ammo action. Every poll tick it tops each <em>ammo-bearing</em>
/// inventory slot — a weapon that fires, or a clip/shell/power pack (see
/// <see cref="ItemCatalog.IsAmmoItem"/>) — up to <see cref="CharacterFormat.MaxAmmo"/>, so a ranger
/// never runs low. Only the ammo count (the low 7 bits of the quantity byte) is raised; the high bit
/// — Wasteland's jammed-weapon flag — is left as-is, a count already at or above the max is never
/// reduced, and non-ammo items (melee weapons, armor, gear and quest items, whose second byte is
/// unused or a status byte) are skipped so the freeze can't corrupt them.
///
/// Stateless: the current bytes are the only input, so there is no snapshot to seed and nothing to
/// reset when the toggle flips. Kept in the <c>Game/</c> layer so <c>FormatCheck</c> can test it.
/// </summary>
public static class AmmoFreeze
{
    /// <summary>
    /// Tops every ammo-bearing slot in <paramref name="record"/> up to
    /// <see cref="CharacterFormat.MaxAmmo"/>, mutating the inventory bytes in place. Returns
    /// <c>true</c> when at least one slot was raised (so the caller pokes those quantity bytes back to
    /// memory and refreshes the rows). When <paramref name="toppedSlots"/> is supplied it is cleared
    /// and filled with the indices raised, so only those rows need refreshing. Item ids are read, never
    /// written.
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
            if (count >= CharacterFormat.MaxAmmo) continue;            // already full — never reduce

            // Raise the count to the max, preserving the current jammed-weapon flag (bit 7).
            record.SetItem(slot, id, (rawQty & CharacterFormat.InventoryJammedFlag) | CharacterFormat.MaxAmmo);
            changed = true;
            toppedSlots?.Add(slot);
        }
        return changed;
    }
}
