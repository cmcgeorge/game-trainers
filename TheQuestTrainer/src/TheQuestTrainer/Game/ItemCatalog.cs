using TheQuestTrainer.Memory;

namespace TheQuestTrainer.Game;

/// <summary>
/// Every item type the loaded game knows about, found by sweeping the heap.
///
/// The game does keep an indexed table of item types, but reaching it means walking a chain of
/// manager objects off a second static pointer — one more build-specific address, and one more thing
/// to be wrong on an expansion or a patch. The sweep needs none of that: an item type is recognisable
/// from its own bytes (see <see cref="ItemTypeReader"/>), and the strongest of those checks is a
/// back-pointer to the engine object the trainer has already located. So the search is "find every
/// dword in the heap equal to the engine address, and test what follows it", which in a live session
/// is one pass over a few hundred megabytes and takes about a third of a second.
///
/// What comes back is the game's real catalog, expansions included: the shipped v1.9.10 session this
/// was built against yields 1,084 types across all fifteen categories, from <c>base_weap_dagger</c>
/// to <c>isle_repair_hammermaster2</c>.
/// </summary>
public static class ItemCatalog
{
    /// <summary>Bytes read per region. Regions larger than this are read in overlapping slices.</summary>
    private const int SliceBytes = 4 * 1024 * 1024;

    /// <summary>
    /// Upper bound on types collected. Far above the shipped game's 1,084, and there only so a
    /// pathological session cannot make the picker unusable.
    /// </summary>
    private const int MaxTypes = 20_000;

    /// <summary>
    /// Sweeps the target for item types belonging to <paramref name="engine"/>, in address order.
    ///
    /// Duplicates cannot arise — an address is tested once — but the same <i>name</i> legitimately
    /// appears more than once (the game ships several "Lots of gold coins"), so the caller should
    /// key on <see cref="ItemType.Address"/> and not on the name.
    /// </summary>
    public static IReadOnlyList<ItemType> Sweep(IMemorySource source, uint engine, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var found = new List<ItemType>();
        if (engine == 0) return found;

        var wanted = BitConverter.GetBytes(engine);
        var buffer = new byte[SliceBytes];
        const int overlap = ItemLayout.TypeBytes;

        foreach (var region in source.Regions())
        {
            ct.ThrowIfCancellationRequested();
            if (region.Size < ItemLayout.TypeBytes) continue;

            long offset = 0;
            while (offset < region.Size)
            {
                ct.ThrowIfCancellationRequested();
                int want = (int)Math.Min(SliceBytes, region.Size - offset);
                uint sliceBase = region.Base + (uint)offset;

                if (source.Read(sliceBase, buffer, want) == want)
                {
                    // A type object is dword-aligned and starts with the engine pointer, so the
                    // scan only has to look at every fourth byte and only has to parse the handful
                    // of places that match all four bytes of it.
                    for (int i = 0; i + ItemLayout.TypeBytes <= want; i += 4)
                    {
                        if (buffer[i] != wanted[0] || buffer[i + 1] != wanted[1] ||
                            buffer[i + 2] != wanted[2] || buffer[i + 3] != wanted[3]) continue;

                        var type = ItemTypeReader.Parse(source, buffer, i, sliceBase + (uint)i, engine);
                        if (type is null) continue;

                        found.Add(type);
                        if (found.Count >= MaxTypes) return found;
                    }
                }

                if (want <= overlap) break;
                offset += want - overlap;
            }
        }

        return found;
    }

    /// <summary>
    /// Whether a type is safe to stamp onto a carried item.
    ///
    /// One rule, and it comes from the game rather than from caution: the item panel prints wear as
    /// <c>condition × 100 / maxCondition</c>, so a type whose category shows a condition but whose
    /// maximum is zero would divide by zero the moment the player looked at it. No type in the
    /// shipped game is like that, which is exactly why the check is cheap to keep.
    /// </summary>
    public static bool CanReplaceWith(ItemType type, out string why)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.Meter == ItemMeter.Condition && type.MaxCondition <= 0)
        {
            why = $"“{type.Name}” shows a condition but has no maximum, so the game would divide by zero drawing it.";
            return false;
        }

        why = "";
        return true;
    }
}
