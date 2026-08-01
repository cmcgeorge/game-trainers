namespace HillsfarTrainer.Game;

/// <summary>One legal class combination, as the game names it.</summary>
/// <param name="Mask">The class bitmask stored at <see cref="CharacterFormat.OffClassMask"/>.</param>
/// <param name="Name">The name from the game's own class-name table.</param>
public readonly record struct ClassInfo(int Mask, string Name)
{
    /// <summary>True when this combination includes the Cleric class.</summary>
    public bool IsCleric => (Mask & ClassBook.MaskCleric) != 0;

    /// <summary>True when this combination includes the Magic-User class.</summary>
    public bool IsMagicUser => (Mask & ClassBook.MaskMagicUser) != 0;

    /// <summary>True when this combination includes the Fighter class.</summary>
    public bool IsFighter => (Mask & ClassBook.MaskFighter) != 0;

    /// <summary>True when this combination includes the Thief class.</summary>
    public bool IsThief => (Mask & ClassBook.MaskThief) != 0;

    /// <inheritdoc/>
    public override string ToString() => Name;
}

/// <summary>
/// The character classes, read out of the game's own tables.
///
/// <para>Two representations coexist in the record and both have to be kept consistent. The
/// <b>bitmask</b> at <see cref="CharacterFormat.OffClassMask"/> is what the code actually tests —
/// it is the single most-referenced byte in the record — and the game's class-<i>name</i> table at
/// <c>DGROUP:0x3DB0</c> is indexed directly by it, which is how this list was recovered. The
/// <b>index</b> at <see cref="CharacterFormat.OffClassIndex"/> is a character-creation menu index
/// that the game maps to a mask through a 16-byte table at <c>DGROUP:0x91DC</c>.</para>
///
/// <para>Because the mask is authoritative, <see cref="IndexForMask"/> returns the game's own index
/// where the table has one and <see cref="MagicUserThiefIndex"/> for the single combination it does
/// not cover. It must never fall back to the mask value itself — mask 5 read as an index selects
/// plain Magic-User, so the record would claim two different classes at once. Always write the mask;
/// <see cref="CharacterRecord.ClassMask"/> writes both bytes together.</para>
/// </summary>
public static class ClassBook
{
    /// <summary>Bit 0 — Thief.</summary>
    public const int MaskThief = 0x1;

    /// <summary>Bit 1 — Fighter.</summary>
    public const int MaskFighter = 0x2;

    /// <summary>Bit 2 — Magic-User.</summary>
    public const int MaskMagicUser = 0x4;

    /// <summary>Bit 3 — Cleric.</summary>
    public const int MaskCleric = 0x8;

    /// <summary>
    /// The eleven legal combinations, in mask order. Masks 0, 9, 11 and 13 have empty strings in the
    /// game's table — every pairing of Cleric with Thief is illegal, as in AD&amp;D.
    /// </summary>
    public static readonly IReadOnlyList<ClassInfo> Classes = new[]
    {
        new ClassInfo(0x1, "Thief"),
        new ClassInfo(0x2, "Fighter"),
        new ClassInfo(0x3, "FTR/TH"),
        new ClassInfo(0x4, "Magic-User"),
        new ClassInfo(0x5, "MU/TH"),
        new ClassInfo(0x6, "FTR/MU"),
        new ClassInfo(0x7, "FTR/MU/TH"),
        new ClassInfo(0x8, "Cleric"),
        new ClassInfo(0xA, "CL/FTR"),
        new ClassInfo(0xC, "CL/MU"),
        new ClassInfo(0xE, "CL/FTR/MU"),
    };

    /// <summary>
    /// The game's index-to-mask table from <c>DGROUP:0x91DC</c>, verbatim. <c>0xFF</c> marks a slot
    /// the game does not use. The four shipped <c>.PRE</c> files carry indices 0, 2, 5 and 6, which
    /// is what pinned this table's alignment.
    /// </summary>
    public static readonly IReadOnlyList<byte> IndexToMask = new byte[]
    {
        0x08, 0xFF, 0x02, 0xFF, 0xFF, 0x04, 0x01, 0xFF,
        0x0A, 0x0E, 0xFF, 0x0C, 0xFF, 0x06, 0x03, 0x07,
    };

    /// <summary>True when <paramref name="mask"/> is one of the combinations the game allows.</summary>
    public static bool IsLegalMask(int mask)
    {
        foreach (var c in Classes) if (c.Mask == mask) return true;
        return false;
    }

    /// <summary>
    /// The game's name for a class mask. Accepts the byte as stored (mask duplicated across both
    /// nibbles) or a bare low nibble. Returns <c>"(none)"</c> for an illegal combination rather
    /// than throwing — a locator candidate may well hold rubbish here.
    /// </summary>
    public static string NameForMask(int mask)
    {
        int low = mask & 0x0F;
        foreach (var c in Classes) if (c.Mask == low) return c.Name;
        return "(none)";
    }

    /// <summary>The <see cref="ClassInfo"/> for a mask, or null when the mask is illegal.</summary>
    public static ClassInfo? ForMask(int mask)
    {
        int low = mask & 0x0F;
        foreach (var c in Classes) if (c.Mask == low) return c;
        return null;
    }

    /// <summary>
    /// Packs a mask into the byte the game stores: the same value in both nibbles.
    /// </summary>
    public static byte PackMask(int mask)
    {
        int low = mask & 0x0F;
        return (byte)((low << 4) | low);
    }

    /// <summary>
    /// The index of the Magic-User/Thief slot, which lies just past the 16 bytes read out of the game.
    ///
    /// <para>Mask 5 is the one legal combination with no entry inside the table, and the byte
    /// immediately after it in the game's data is <c>0x05</c> — so the table is very likely 17 entries
    /// long. That is <b>Inferred</b>, not Confirmed, which is why <see cref="IndexForMask"/> documents
    /// it and the harness pins the invariant rather than the value.</para>
    /// </summary>
    public const byte MagicUserThiefIndex = 16;

    /// <summary>
    /// The class index the game would use for a mask, from <see cref="IndexToMask"/>.
    ///
    /// <para>Magic-User/Thief (mask 5) has no slot inside the 16 bytes and gets
    /// <see cref="MagicUserThiefIndex"/>. It must not fall back to the mask value itself: mask 5 read
    /// as an index selects <c>IndexToMask[5] == 0x04</c>, i.e. plain <b>Magic-User</b> — so the record
    /// would end up claiming two different classes in its two representations, which is exactly what
    /// <see cref="CharacterRecord.ClassMask"/> writes both bytes to prevent.</para>
    /// </summary>
    public static byte IndexForMask(int mask)
    {
        int low = mask & 0x0F;
        for (int i = 0; i < IndexToMask.Count; i++)
            if (IndexToMask[i] == low) return (byte)i;
        return MagicUserThiefIndex;
    }

    /// <summary>
    /// True when <paramref name="index"/> does not contradict <paramref name="mask"/> — either the
    /// table maps it to that mask, or it is outside the table and so makes no competing claim. This is
    /// the invariant the two class representations must satisfy.
    /// </summary>
    public static bool IndexAgreesWithMask(int index, int mask)
    {
        if (index < 0 || index >= IndexToMask.Count) return true;
        byte entry = IndexToMask[index];
        return entry == 0xFF || entry == (mask & 0x0F);
    }
}
