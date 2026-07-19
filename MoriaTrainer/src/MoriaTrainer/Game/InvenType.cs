namespace MoriaTrainer.Game;

/// <summary>
/// One live inventory slot read from the <c>inventory[34]</c> global array. The struct layout is
/// <b>Candidate</b> — derived from the UMoria 5.5.2 source (<c>types.h</c> <c>inven_type</c>),
/// compiled under DJGPP v2.01 for 32-bit protected-mode DOS. Fields have been cross-checked against
/// in-game observations but not yet confirmed against a Ghidra analysis of the live binary. See
/// <c>.docs/ReverseEngineering.md</c> §7 for the open leads.
/// </summary>
public sealed class InvenSlot
{
    /// <summary>Slot index in <c>inventory[0..33]</c> (0..21 = pack, 22..30 = equipment).</summary>
    public int Index { get; }

    /// <summary>Item type (the TV_* constant). 0 = empty slot.</summary>
    public byte Tval { get; set; }

    /// <summary>Item subtype (sval). Together with tval uniquely identifies the base item kind.</summary>
    public byte SubVal { get; set; }

    /// <summary>Number of items in this stack (1 for most unique items, > 1 for arrows etc.).</summary>
    public byte Number { get; set; }

    /// <summary>
    /// Charges for wands and staves; bonus P1 for weapons/armor/rings/amulets. Stored as int16.
    /// </summary>
    public short P1 { get; set; }

    /// <summary>Item weight (in tenths of a pound).</summary>
    public ushort Weight { get; set; }

    /// <summary>Whether the slot is occupied (tval != 0).</summary>
    public bool IsOccupied => Tval != 0;

    /// <summary>Human-readable category name from <see cref="ItemBook"/>.</summary>
    public string CategoryName => ItemBook.ByTval(Tval)?.Category ?? (Tval == 0 ? "(empty)" : $"tval 0x{Tval:X2}");

    public InvenSlot(int index) => Index = index;

    /// <summary>Slot label: pack slots show "a"–"v", equipment slots show their name.</summary>
    public string SlotLabel => Index < PlayerFormat.InvenPack
        ? ((char)('a' + Index)).ToString()
        : Index switch
        {
            PlayerFormat.InvenWield => "Wielded",
            PlayerFormat.InvenHead  => "Head",
            PlayerFormat.InvenNeck  => "Neck",
            PlayerFormat.InvenBody  => "Body",
            PlayerFormat.InvenArm   => "Shield",
            PlayerFormat.InvenHands => "Hands",
            PlayerFormat.InvenHand  => "Ring L",
            PlayerFormat.InvenAux   => "Ring R",
            PlayerFormat.InvenFeet  => "Feet",
            _ => $"slot {Index}",
        };
}

/// <summary>
/// Byte offsets within the DJGPP-compiled <c>inven_type</c> struct and the struct size. All values
/// are <b>Candidate</b> (see <c>.docs/ReverseEngineering.md</c> §7). Natural-alignment rules for
/// DJGPP: uint8 aligns to 1, int16/uint16 to 2, int32/uint32/pointer to 4.
/// </summary>
public static class InvenTypeFormat
{
    // --- struct field offsets (Candidate) -----------------------------------
    /// <summary><c>tval</c> (uint8): item category. 0 = empty.</summary>
    public const int TvalOff    = 0;

    /// <summary><c>namelbl</c> (uint8): index into the name label table.</summary>
    public const int NameLblOff = 1;

    // [2 bytes padding, then 4-byte pointer to name string]

    /// <summary><c>p1</c> (int16): charges (wand/staff) or bonus (weapon/armor/ring).</summary>
    public const int P1Off      = 8;

    // [2 bytes padding]

    /// <summary><c>cost</c> (int32): base store cost.</summary>
    public const int CostOff    = 12;

    /// <summary><c>subval</c> (uint8): item subtype.</summary>
    public const int SubValOff  = 16;

    /// <summary><c>number</c> (uint8): stack count.</summary>
    public const int NumberOff  = 17;

    /// <summary><c>weight</c> (uint16): weight in tenths of a pound.</summary>
    public const int WeightOff  = 18;

    /// <summary><c>tohit</c> (int16): plus to hit.</summary>
    public const int ToHitOff   = 20;

    /// <summary><c>todam</c> (int16): plus to damage.</summary>
    public const int ToDamOff   = 22;

    /// <summary><c>ac</c> (int16): armor class.</summary>
    public const int AcOff      = 24;

    /// <summary><c>toac</c> (int16): plus to armor class.</summary>
    public const int ToAcOff    = 26;

    // damage[7] at 28, level at 35, ident at 36, [3 pad], name2 ptr at 40, flags at 44, flags2 at 48

    /// <summary><c>ident</c> (uint8): identification flags (bit 0 = identified).</summary>
    public const int IdentOff   = 36;

    /// <summary><c>flags</c> (uint32): special item flags (TR_* bitmask).</summary>
    public const int FlagsOff   = 44;

    /// <summary>
    /// Total size of one <c>inven_type</c> record in DJGPP 32-bit (Candidate). The value 52 is
    /// inferred from the struct layout with natural alignment; confirm via Ghidra before trusting.
    /// </summary>
    public const int StructSize = 52;
}
