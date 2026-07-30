using System.Text;

namespace SwordOfAragonTrainer.Game;

/// <summary>
/// A typed, mutable view over one 100-byte roster record inside a loaded
/// <c>ARAGON.HR&lt;letter&gt;</c> buffer. Nothing is copied: every property reads and writes the
/// backing array in place, so the surrounding 7,900 bytes are untouched.
///
/// Writes are deliberately conservative. Setting a value clamps it to a range the game itself
/// accepts, and the two fields the game mirrors elsewhere in the record (level and type each have a
/// byte-wide copy at 0x60/0x61) are always written as a pair, so those two can never disagree.
/// See <see cref="RecomputeDerived"/> for which equipment-derived figures are recalculated and which
/// are left for the game to recompute.
/// </summary>
public sealed class RosterRecord
{
    private readonly byte[] _data;
    private readonly int _base;

    /// <summary>Slot index 0..79 within the roster.</summary>
    public int Slot { get; }

    internal RosterRecord(byte[] data, int slot)
    {
        _data = data;
        Slot = slot;
        _base = RosterFormat.RecordOffset(slot);
    }

    /// <summary>True if this is one of the 20 character slots rather than a troop-unit slot.</summary>
    public bool IsCharacterSlot => RosterFormat.IsCharacterSlot(Slot);

    /// <summary>
    /// True if the slot holds a real record. The game blanks the name and zeroes the type of an
    /// unused slot, so a recognised type code is the reliable test.
    /// </summary>
    public bool IsOccupied => UnitBook.Type(TypeCode) != null;

    // --- name -------------------------------------------------------------------
    /// <summary>
    /// The unit or character name, 16 bytes space-padded in the file. Setting it truncates to 16
    /// characters, replaces anything outside printable ASCII with a space (the game's own font is
    /// code-page 437 and its input routine only accepts typeable characters), and pads the rest.
    /// </summary>
    public string Name
    {
        get => Encoding.ASCII
            .GetString(_data, _base + RosterFormat.OffName, RosterFormat.NameLength)
            .TrimEnd();
        set
        {
            string text = value ?? string.Empty;
            for (int i = 0; i < RosterFormat.NameLength; i++)
            {
                char c = i < text.Length ? text[i] : ' ';
                if (c < 0x20 || c > 0x7E) c = ' ';
                _data[_base + RosterFormat.OffName + i] = (byte)c;
            }
        }
    }

    // --- identity ---------------------------------------------------------------
    /// <summary>Type/class code 1–10 (see <see cref="UnitBook.Types"/>). Writes the byte mirror too.</summary>
    public int TypeCode
    {
        get => ReadInt16(RosterFormat.OffType);
        set
        {
            int code = Math.Clamp(value, 0, UnitBook.Types.Count);
            WriteInt16(RosterFormat.OffType, code);
            _data[_base + RosterFormat.OffPackedType] = (byte)code;
        }
    }

    /// <summary>The game's name for <see cref="TypeCode"/>.</summary>
    public string TypeName => UnitBook.TypeName(TypeCode);

    /// <summary>
    /// Experience points, stored as a QuickBASIC (MBF) single. A non-finite input becomes 0 —
    /// <c>Math.Clamp</c> passes NaN through, and <see cref="Mbf.Write"/> would then store MBF zero
    /// anyway, so this just makes the intent explicit.
    /// </summary>
    public double Experience
    {
        get => Mbf.ToDouble(_data, _base + RosterFormat.OffExperience);
        set => Mbf.Write(_data.AsSpan(),
                         double.IsFinite(value) ? Math.Clamp(value, 0, GameFacts.MaxWealth) : 0,
                         _base + RosterFormat.OffExperience);
    }

    /// <summary>Experience level. Writes the byte mirror at 0x60 too.</summary>
    public int Level
    {
        get => ReadInt16(RosterFormat.OffLevel);
        set
        {
            int level = Math.Clamp(value, 0, RosterFormat.MaxLevel);
            WriteInt16(RosterFormat.OffLevel, level);
            _data[_base + RosterFormat.OffPackedLevel] = (byte)level;
        }
    }

    /// <summary>Figures in the unit. A character is always 1.</summary>
    public int Men
    {
        get => ReadInt16(RosterFormat.OffMen);
        set => WriteInt16(RosterFormat.OffMen, Math.Clamp(value, 0, RosterFormat.MaxMen));
    }

    /// <summary>World-map column, 0..23.</summary>
    public int X
    {
        get => ReadInt16(RosterFormat.OffX);
        set => WriteInt16(RosterFormat.OffX, Math.Clamp(value, 0, GameFacts.MapSize - 1));
    }

    /// <summary>World-map row, 0..23.</summary>
    public int Y
    {
        get => ReadInt16(RosterFormat.OffY);
        set => WriteInt16(RosterFormat.OffY, Math.Clamp(value, 0, GameFacts.MapSize - 1));
    }

    /// <summary>Total hit points for the whole unit.</summary>
    public int Hits
    {
        get => ReadInt16(RosterFormat.OffHits);
        set => WriteInt16(RosterFormat.OffHits, Math.Clamp(value, 0, short.MaxValue));
    }

    /// <summary>Movement allowance for the month.</summary>
    public int MoveMax
    {
        get => ReadInt16(RosterFormat.OffMoveMax);
        set
        {
            int move = Math.Clamp(value, 0, short.MaxValue);
            WriteInt16(RosterFormat.OffMoveMax, move);
        }
    }

    /// <summary>Movement still unspent this month.</summary>
    public int MoveLeft
    {
        get => ReadInt16(RosterFormat.OffMoveLeft);
        set => WriteInt16(RosterFormat.OffMoveLeft, Math.Clamp(value, 0, short.MaxValue));
    }

    // --- read-only derived figures ---------------------------------------------
    /// <summary>Cost to raise this configuration, as stored by the game.</summary>
    public int MakeCost => ReadInt16(RosterFormat.OffMakeCost);

    /// <summary>Cost to train this configuration one step, as stored by the game.</summary>
    public int TrainCost => ReadInt16(RosterFormat.OffTrainCost);

    /// <summary>Upkeep in tenths of a gold piece per figure per month, as stored by the game.</summary>
    public int MaintTenths => ReadInt16(RosterFormat.OffMaintTenths);

    /// <summary>Upkeep in gold pieces per figure per month.</summary>
    public double MaintGold => MaintTenths / 10.0;

    /// <summary>Stacking size points per figure, as stored by the game.</summary>
    public int SizePoints => ReadInt16(RosterFormat.OffSize);

    /// <summary>Armour class against hand attacks (lower is better).</summary>
    public int ArmorClassHand => ReadInt16(RosterFormat.OffArmorClassHand);

    /// <summary>Armour class against missile attacks (lower is better).</summary>
    public int ArmorClassMissile => ReadInt16(RosterFormat.OffArmorClassMissile);

    /// <summary>Hand-to-hand damage figure.</summary>
    public int HandDamage => ReadInt16(RosterFormat.OffHandDamage);

    /// <summary>Hand-to-hand special bonus (spear +2, lance +2, halberd +4, pike +6, casters innate).</summary>
    public int HandBonus => ReadInt16(RosterFormat.OffHandBonus);

    /// <summary>Total stacking cost of the whole unit — <see cref="Men"/> × <see cref="SizePoints"/>.</summary>
    public int StackingCost => Men * SizePoints;

    // --- equipment --------------------------------------------------------------
    /// <summary>Reads the item index in one equipment slot.</summary>
    public int GetEquipment(EquipmentSlot slot) =>
        ReadInt16(RosterFormat.EquipmentOffsets[(int)slot]);

    /// <summary>
    /// Sets the item index in one equipment slot, clamped to that slot's table. Does <b>not</b>
    /// recompute the derived cost fields — call <see cref="RecomputeDerived"/> once after a batch of
    /// changes so the game sees a self-consistent record.
    /// </summary>
    public void SetEquipment(EquipmentSlot slot, int index) =>
        WriteInt16(RosterFormat.EquipmentOffsets[(int)slot],
                   Math.Clamp(index, 0, UnitBook.MaxIndex(slot)));

    /// <summary>All eight equipment indices in record order.</summary>
    public int[] Equipment()
    {
        var values = new int[UnitBook.SlotCount];
        for (int i = 0; i < values.Length; i++) values[i] = ReadInt16(RosterFormat.EquipmentOffsets[i]);
        return values;
    }

    /// <summary>
    /// Recomputes the four equipment-derived fields whose formulas are Confirmed — make cost
    /// (<c>0x28</c>), train cost (<c>0x2A</c>), upkeep (<c>0x2C</c>) and stacking size (<c>0x48</c>) —
    /// using the tables in <see cref="UnitBook"/> and the discount granted by
    /// <paramref name="playerClassCode"/>.
    ///
    /// It deliberately does <b>not</b> touch the record's other equipment-derived figures — armour
    /// class (<c>0x40</c>/<c>0x42</c>), hand damage (<c>0x4C</c>), the hand special bonus
    /// (<c>0x50</c>) or hits (<c>0x3E</c>). Those are read out of the file and displayed, but their
    /// formulas are not among the Confirmed findings in <c>docs/RE.md</c>, and guessing at them would
    /// write numbers the game never would. They stay at whatever the game last computed until it
    /// recomputes them itself, which it does when the unit is next equipped or trained in-game — so
    /// after a big equipment change through the trainer, expect those columns to lag by one
    /// Equip/Train.
    /// </summary>
    public void RecomputeDerived(int playerClassCode)
    {
        if (UnitBook.Type(TypeCode) == null) return;      // unknown type: leave the record alone

        var equipment = Equipment();
        var costs = UnitBook.ComputeCosts(TypeCode, equipment, playerClassCode);
        WriteInt16(RosterFormat.OffMakeCost, costs.Make);
        WriteInt16(RosterFormat.OffTrainCost, costs.Train);
        WriteInt16(RosterFormat.OffMaintTenths, costs.MaintTenths);
        WriteInt16(RosterFormat.OffSize, UnitBook.SizePoints(equipment[(int)EquipmentSlot.Horse]));
    }

    /// <summary>Highest equipment index the unit's level permits in a slot (0 if none is allowed).</summary>
    public int HighestAllowedEquipment(EquipmentSlot slot)
    {
        int best = 0;
        foreach (var item in UnitBook.Items(slot))
            if (item.Index > 0 && item.MinLevel <= Level) best = Math.Max(best, item.Index);
        return best;
    }

    /// <summary>A one-line summary for list views.</summary>
    public string Summary => IsOccupied
        ? $"{Name} — {TypeName}, level {Level}, {Men} men at ({X},{Y})"
        : "(empty slot)";

    // --- primitives -------------------------------------------------------------
    private int ReadInt16(int offset) =>
        (short)(_data[_base + offset] | (_data[_base + offset + 1] << 8));

    private void WriteInt16(int offset, int value)
    {
        short v = (short)value;
        _data[_base + offset] = (byte)(v & 0xFF);
        _data[_base + offset + 1] = (byte)((v >> 8) & 0xFF);
    }
}
