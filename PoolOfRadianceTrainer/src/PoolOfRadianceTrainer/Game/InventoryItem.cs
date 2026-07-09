using System.Text;
using System.Text.RegularExpressions;

namespace PoolOfRadianceTrainer.Game;

/// <summary>
/// One carried item: a fixed <b>63-byte (0x3F)</b> record stored in a character's
/// <c>CHRDATAn.ITM</c> file. Items are persisted as a flat array of these records; the character
/// record's item-count byte (<see cref="PorFormat.OffNumberOfItems"/>) and the runtime item/equip
/// pointers (rebuilt on load) live in the .SAV.
///
/// The layout was verified two ways that agree byte-for-byte: the open-source <c>coab</c>
/// reimplementation's <c>Item.cs</c> (<c>StructSize = 0x3F</c>) and a hex read of real
/// <c>CHRDATAn.ITM</c> bytes (e.g. a Sling at type 0x2F; an unidentified Ring of Protection with
/// <c>hidden_names_flag = 6</c>).
///
///   0x00      Pascal name string (length byte + up to 41 chars) — the game's cached render
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

    public const int OffType = 0x2E;
    private const int OffNamePart1 = 0x31; // base name-part index; marks wands/staves/rods
    private const int OffPlus = 0x32;
    public const int OffReadied = 0x34;
    public const int OffHiddenNames = 0x35;
    public const int OffCursed = 0x36;
    public const int OffCount = 0x39;
    private const int OffValue = 0x3A;
    public const int OffCharges = 0x3C;    // Property3: current charges for wands/staves/rods
    public const int NameMax = 0x2A;       // 42-byte name field (Pascal: len + up to 41 chars)

    // NamePart1 indices (GB_UTIL_ITM, game 1) for the charge-bearing item classes.
    private const byte NamePartRod = 67;
    private const byte NamePartStave = 68;
    private const byte NamePartWand = 69;

    /// <summary>The verbatim 63 record bytes; edits mutate in place for write-back.</summary>
    public byte[] Raw { get; }

    public ItemEntry(byte[] record, int offset = 0)
    {
        Raw = new byte[RecordSize];
        int n = Math.Min(RecordSize, record.Length - offset);
        if (n > 0) Array.Copy(record, offset, Raw, 0, n);
    }

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

    /// <summary>Can this item's usable resource be topped up? Wands/staves/rods (charges at 0x3C) and
    /// stacked ammunition (arrows, quarrels, darts — count &gt; 1 at 0x39). Single items (weapons,
    /// armour, rings, a worn shield) are neither, so they are never bumped (which would clone them).</summary>
    public bool IsRechargeable => IsChargedItem || Count > 1;

    /// <summary>The single byte "recharge" writes for this item: the charges byte (0x3C) for
    /// wands/staves/rods, otherwise the ammunition stack-count byte (0x39).</summary>
    public int RechargeOffset => IsChargedItem ? OffCharges : OffCount;

    /// <summary>The current rechargeable value: charges for wands/staves/rods, else the stack count.</summary>
    public int RechargeValue => IsChargedItem ? Charges : Count;

    /// <summary>The game's cached rendered name (Pascal string at 0x00), collapsed to single spaces.
    /// It embeds the game's own inventory markers: a leading "No"/"Yes" (readied) column and a "*"
    /// for an unidentified item, so it reads like the game's item line.</summary>
    public string DisplayName
    {
        get
        {
            int len = Math.Clamp((int)Raw[0], 0, NameMax - 1);
            var sb = new StringBuilder(len);
            for (int i = 1; i <= len; i++) { byte b = Raw[i]; if (b != 0) sb.Append((char)b); }
            string s = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
            return string.IsNullOrEmpty(s) ? $"item 0x{Type:X2}" : s;
        }
    }

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

    /// <summary>Reveal every part of the name (fully identify). Returns true if it changed.</summary>
    public bool Identify()
    {
        if (Raw[OffHiddenNames] == 0) return false;
        Raw[OffHiddenNames] = 0;
        return true;
    }

    /// <summary>Overwrite this item's whole 63-byte record from <paramref name="src"/> — an in-place
    /// duplicate. The caller writes the buffer back to the game's memory (or the .ITM file). The link
    /// pointer that follows the record in memory lives outside these 63 bytes, so it is left intact.</summary>
    public void CopyFrom(ItemEntry src) => Array.Copy(src.Raw, Raw, RecordSize);

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
    /// byte (<see cref="RechargeOffset"/>) is touched. Returns true if it changed.</summary>
    public bool Recharge(int value) => IsChargedItem ? SetCharges(value) : SetCount(value);

    public ItemEntry Clone() => new(Raw);
}
