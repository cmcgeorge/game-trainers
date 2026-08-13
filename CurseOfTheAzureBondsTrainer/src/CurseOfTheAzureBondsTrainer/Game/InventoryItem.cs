using System.Text;
using System.Text.RegularExpressions;

namespace CurseOfTheAzureBondsTrainer.Game;

/// <summary>
/// A real-mode <c>segment:offset</c> pointer as the game stores it — the guest is a 16-bit DOS
/// program, so every pointer it writes is a pair of words, not a flat address.
/// </summary>
public readonly record struct FarPointer(ushort Offset, ushort Segment)
{
    public bool IsNull => Offset == 0 && Segment == 0;

    /// <summary>The 20-bit physical address this resolves to inside the guest's RAM.</summary>
    public uint Linear => (uint)Segment * 16 + Offset;

    public static FarPointer Read(byte[] buf, int offset) =>
        offset + 4 <= buf.Length
            ? new FarPointer((ushort)(buf[offset] | buf[offset + 1] << 8),
                             (ushort)(buf[offset + 2] | buf[offset + 3] << 8))
            : default;

    public override string ToString() => $"{Segment:X4}:{Offset:X4}";
}

/// <summary>
/// One carried item: a fixed <b>63-byte (0x3F)</b> record stored in a character's
/// <c>CHRDATAn.ITM</c> file. Items are persisted as a flat array of these records; the character
/// record's item-count byte (<see cref="CoabFormat.OffNumberOfItems"/>) and the runtime item/equip
/// pointers (rebuilt on load) live in the .SAV.
///
/// The size and the layout are both confirmed from the game's own files. Every block of every
/// <c>ITEM*.DAX</c> archive — the item templates the game builds real items from — is an exact
/// multiple of 63 bytes, and each record within one begins with a Pascal-string item name. Decoding
/// those 58 base items with the offsets below reproduces the AD&amp;D 1st-edition equipment tables
/// outright: Plate Mail reads weight 450, value 400 gp; Chain Mail 300 and 75; Leather Armor 150 and
/// 5; a Two-Handed Sword 30 gp; a Composite Long Bow 100. A wrong offset does not produce a whole
/// price list. The stack-count byte is confirmed the same way — the archive's "10 Arrow" entry reads
/// count 10.
///
///   0x00      Pascal name string (length byte + up to 41 chars) — the game's cached render
///   0x2A..2D  far pointer (offset word, then segment word) to the <b>next</b> item the owner carries;
///             null on the last. This is what the game walks to draw an item list, so it — not any
///             adjacency in memory — is what makes a set of records one character's inventory
///   0x2E      item type byte (see <c>coab</c> ItemType enum)
///   0x32      plus / magical bonus (signed)
///   0x34      readied (equipped) flag
///   0x35      hidden-names flag: <b>0 = fully identified</b>; non-zero hides name parts ("*")
///   0x36      cursed flag
///   0x39      count (stack size) — for stackable ammunition (arrows, quarrels, darts); 0 = single item
///   0x3A..3B  value (UInt16)
///   0x3C..3E  three "affect" bytes copied from the base item: <b>0x3C = charges</b> (wands/staves/rods),
///             0x3D = spell/effect code, 0x3E = interpretation (0 = 0x3D is a plain spell code)
/// </summary>
public sealed class ItemEntry
{
    public const int RecordSize = 0x3F;   // 63

    public const int OffNextLink = 0x2A;   // far pointer to the owner's next item
    public const int OffType = 0x2E;
    public const int OffNamePart1 = 0x31;  // base name-part index; marks wands/staves/rods
    private const int OffPlus = 0x32;
    public const int OffReadied = 0x34;
    public const int OffHiddenNames = 0x35;
    public const int OffCursed = 0x36;
    public const int OffCount = 0x39;
    private const int OffValue = 0x3A;
    public const int OffCharges = 0x3C;    // Property3: current charges for wands/staves/rods
    public const int NameMax = 0x2A;       // 42-byte name field (Pascal: len + up to 41 chars)

    // NamePart1 indices (GB_UTIL_ITM, game 1) for the charge-bearing item classes. Public so
    // FormatCheck can pin them: IsChargedItem rests on these three values alone, and a fourth value
    // classifying as charged would send Recharge at the wrong byte.
    public const byte NamePartRod = 67;
    public const byte NamePartStave = 68;
    public const byte NamePartWand = 69;

    /// <summary>What <see cref="SetIdentified"/> writes back when re-hiding an item that was already
    /// identified when we first read it, so there is no original value to restore. 6 is the value real
    /// saves use for an unidentified magic item (seen on a Ring of Protection and a Shield +1).</summary>
    private const byte DefaultHiddenNames = 6;

    /// <summary>The verbatim 63 record bytes; edits mutate in place for write-back.</summary>
    public byte[] Raw { get; }

    /// <summary>The hidden-names flag this record had when it was read, so un-ticking "ID'd" restores
    /// the item's original masking rather than inventing one.</summary>
    private readonly byte _originalHiddenNames;

    public ItemEntry(byte[] record, int offset = 0)
    {
        Raw = new byte[RecordSize];
        int n = Math.Min(RecordSize, record.Length - offset);
        if (n > 0) Array.Copy(record, offset, Raw, 0, n);
        _originalHiddenNames = Raw[OffHiddenNames] != 0 ? Raw[OffHiddenNames] : DefaultHiddenNames;
    }

    /// <summary>The next item on the owner's list. Null on the last item.</summary>
    public FarPointer NextLink => FarPointer.Read(Raw, OffNextLink);

    public byte Type => Raw[OffType];
    public sbyte Plus => (sbyte)Raw[OffPlus];
    public bool Readied => Raw[OffReadied] != 0;
    public bool Identified => Raw[OffHiddenNames] == 0;
    public bool Cursed => Raw[OffCursed] != 0;
    public int Count => Raw[OffCount];
    public int Value => Raw[OffValue] | (Raw[OffValue + 1] << 8);

    /// <summary>A wand, staff, or rod — the item classes whose usable resource is a <b>charge</b>
    /// count stored at <see cref="OffCharges"/> (0x3C), not the stack-count byte. Detected by the
    /// base name-part index (0x31), which is the item's authoritative name class and is set even
    /// while the item is unidentified. We deliberately do not add a secondary <see cref="Type"/>
    /// check: the type byte's wand/staff/rod ranges aren't verified here, so a mismatched guard could
    /// misclassify a real wand as a stackable and reintroduce the count-byte cloning bug this
    /// distinction exists to prevent.</summary>
    public bool IsChargedItem =>
        Raw[OffNamePart1] is NamePartRod or NamePartStave or NamePartWand;

    /// <summary>Current charge count (0x3C) for wands/staves/rods; meaningless for other items.</summary>
    public int Charges => Raw[OffCharges];

    /// <summary>Charge count as a display string: the number for wands/staves/rods, blank for
    /// other items (whose 0x3C byte holds unrelated spell/effect data, not charges).</summary>
    public string ChargesDisplay => IsChargedItem ? Charges.ToString() : "";

    /// <summary>Can this item's usable resource be topped up? Wands/staves/rods (charges at 0x3C) and
    /// stacked ammunition (arrows, quarrels, darts — count &gt; 1 at 0x39). Single items (weapons,
    /// armour, rings, a worn shield) are neither, so they are never bumped (which would clone them).</summary>
    public bool IsRechargeable => IsChargedItem || Count > 1;

    /// <summary>The single byte "recharge" writes for this item: the charges byte (0x3C) for
    /// wands/staves/rods, otherwise the ammunition stack-count byte (0x39).</summary>
    public int RechargeOffset => IsChargedItem ? OffCharges : OffCount;

    /// <summary>The current rechargeable value: charges for wands/staves/rods, else the stack count.</summary>
    public int RechargeValue => IsChargedItem ? Charges : Count;

    /// <summary>
    /// The item's name as the game itself last rendered it — the Pascal string at 0x00, collapsed to
    /// single spaces.
    ///
    /// <para>That cached string is the game's whole inventory <i>line</i>, not just a name: it starts
    /// with the READY column ("No" / "Yes"), which this strips, because the list already shows that as
    /// the Rdy checkbox and "No Long Sword" reads like part of the item's name. Anything else the game
    /// put there is kept verbatim — notably the trailing count on stacked pseudo-items ("Jewelry 3" is
    /// three pieces of jewelry, and the count lives only in this text, not in the count byte).</para>
    ///
    /// <para>The cache is only rewritten by the game when it next draws the item list, so an item that
    /// has just been identified from here keeps its unidentified appearance until then — see
    /// <see cref="Identify"/>.</para>
    /// </summary>
    public string DisplayName
    {
        get
        {
            int len = Math.Clamp((int)Raw[0], 0, NameMax - 1);
            var sb = new StringBuilder(len);
            for (int i = 1; i <= len; i++) { byte b = Raw[i]; if (b != 0) sb.Append((char)b); }
            string s = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
            s = ReadyColumn.Replace(s, "", 1);
            return string.IsNullOrEmpty(s) ? $"item 0x{Type:X2}" : s;
        }
    }

    /// <summary>The leading readied column the game renders in front of an item's name. Anchored and
    /// requiring a following space so it can only ever eat that whole column, never the start of a
    /// real name.</summary>
    private static readonly Regex ReadyColumn = new(@"^(No|Yes) ", RegexOptions.Compiled);

    /// <summary>Short flag summary for the item list, e.g. "equipped · unidentified".</summary>
    public string Tags
    {
        get
        {
            var parts = new List<string>();
            if (Readied) parts.Add("equipped");
            if (!Identified) parts.Add("unidentified");
            if (Cursed) parts.Add("cursed");
            if (IsChargedItem) parts.Add($"{Charges} charges");
            else if (Count > 1) parts.Add($"x{Count}");
            return parts.Count == 0 ? "" : string.Join(" · ", parts);
        }
    }

    /// <summary>Reveal every part of the name (fully identify). Returns true if it changed.
    /// Only the hidden-names flag moves: the rendered name cached at 0x00 is the game's, and the game
    /// rewrites it the next time it draws the item list, so the full name appears in-game (and here
    /// after a Re-scan) rather than the moment this is called.</summary>
    public bool Identify() => SetIdentified(true);

    /// <summary>Identify or re-hide this item. Re-hiding restores the record's original hidden-names
    /// value where there was one. Returns true if the flag changed.</summary>
    public bool SetIdentified(bool identified)
    {
        byte want = identified ? (byte)0 : _originalHiddenNames;
        if (Raw[OffHiddenNames] == want) return false;
        Raw[OffHiddenNames] = want;
        return true;
    }

    /// <summary>Overwrite this item's record from <paramref name="src"/> — an in-place duplicate. The
    /// caller writes the buffer back to the game's memory (or the .ITM file). This slot's own
    /// next-item link (0x2A) is deliberately kept: it is what holds the owner's list together, and
    /// copying the source's link over it would splice this character's inventory onto wherever the
    /// source sat in its own list.</summary>
    public void CopyFrom(ItemEntry src)
    {
        var link = Raw[OffNextLink..(OffNextLink + 4)];
        Array.Copy(src.Raw, Raw, RecordSize);
        Array.Copy(link, 0, Raw, OffNextLink, link.Length);
    }

    /// <summary>Set the ammunition stack-count byte (0x39), clamped to 1..255. Returns true if the
    /// byte changed. Use <see cref="Recharge"/> to top up any rechargeable item correctly — writing
    /// the count byte on a single item (a wand) would clone it into a stack.</summary>
    public bool SetCount(int value)
    {
        byte v = (byte)Math.Clamp(value, 1, 255);
        if (Raw[OffCount] == v) return false;
        Raw[OffCount] = v;
        return true;
    }

    /// <summary>Set this item's charge count (0x3C), clamped to 1..255. Returns true if it changed.</summary>
    public bool SetCharges(int value)
    {
        byte v = (byte)Math.Clamp(value, 1, 255);
        if (Raw[OffCharges] == v) return false;
        Raw[OffCharges] = v;
        return true;
    }

    /// <summary>Top up this item's usable resource to <paramref name="value"/> (clamped 1..255):
    /// wand/staff/rod charges at 0x3C, or an ammunition stack count at 0x39. Only the correct single
    /// byte (<see cref="RechargeOffset"/>) is touched. Returns true if it changed.
    ///
    /// <para>An item with nothing to top up is left alone and false is returned. This is the guard,
    /// not the caller's <see cref="IsRechargeable"/> check: on a single item — a sword, a ring, a
    /// worn shield — the "recharge" byte is the stack count, and writing it would turn one sword
    /// into ninety-nine. The class documentation points every consumer at this method, so it has to
    /// be safe to call on anything.</para></summary>
    public bool Recharge(int value)
    {
        if (!IsRechargeable) return false;
        return IsChargedItem ? SetCharges(value) : SetCount(value);
    }

    public ItemEntry Clone() => new(Raw);
}
