using BardsTaleTrilogyTrainer.Memory;

namespace BardsTaleTrilogyTrainer.Game;

/// <summary>
/// A typed live view over one <c>BardsTale.Character</c> object in the game process. Unlike
/// the DOS trainers (which keep a local byte buffer and sync it), this record reads and writes
/// straight through to the object's address, so every property is the game's current value.
/// </summary>
public sealed class CharacterRecord
{
    private readonly IMemorySource _mem;

    /// <summary>Absolute address of the IL2CPP character object in the target process.</summary>
    public nuint Address { get; }

    /// <summary>Index in <c>Party.m_members</c>.</summary>
    public int Slot { get; }

    public CharacterRecord(IMemorySource mem, nuint address, int slot)
    {
        _mem = mem;
        Address = address;
        Slot = slot;
    }

    // --- primitive accessors ----------------------------------------------------
    public int ReadI32(int offset) => _mem.ReadI32(Address + (nuint)offset);
    public bool WriteI32(int offset, int value) => _mem.WriteI32(Address + (nuint)offset, value);
    public long ReadI64(int offset) => _mem.ReadI64(Address + (nuint)offset);
    public bool WriteI64(int offset, long value) => _mem.WriteI64(Address + (nuint)offset, value);

    // --- identity ---------------------------------------------------------------
    /// <summary>Display name, read fresh from the managed string each time.</summary>
    public string Name => _mem.ReadManagedString(_mem.ReadPtr(Address + (nuint)CharacterFormat.OffName));

    public int Race
    {
        get => ReadI32(CharacterFormat.OffRace);
        set => WriteI32(CharacterFormat.OffRace, value);
    }

    public int Class
    {
        get => ReadI32(CharacterFormat.OffClass);
        set => WriteI32(CharacterFormat.OffClass, value);
    }

    public int Gender
    {
        get => ReadI32(CharacterFormat.OffGender);
        set => WriteI32(CharacterFormat.OffGender, value);
    }

    /// <summary>0 = Okay, 3 = Dead … see <see cref="CharacterFormat.Conditions"/>.</summary>
    public int Condition
    {
        get => ReadI32(CharacterFormat.OffCondition);
        set => WriteI32(CharacterFormat.OffCondition, value);
    }

    // --- progression ------------------------------------------------------------
    /// <summary>Experience points — a 64-bit field in the remaster.</summary>
    public long Experience
    {
        get => ReadI64(CharacterFormat.OffExperience);
        set => WriteI64(CharacterFormat.OffExperience, value);
    }

    /// <summary>Gold carried by this character (the party purse is separate).</summary>
    public long Gold
    {
        get => ReadI64(CharacterFormat.OffGold);
        set => WriteI64(CharacterFormat.OffGold, value);
    }

    public int Level
    {
        get => ReadI32(CharacterFormat.OffLevel);
        set => WriteI32(CharacterFormat.OffLevel, value);
    }

    /// <summary>Level before drain; the game restores <see cref="Level"/> towards this.</summary>
    public int RealLevel
    {
        get => ReadI32(CharacterFormat.OffRealLevel);
        set => WriteI32(CharacterFormat.OffRealLevel, value);
    }

    // --- vitals -----------------------------------------------------------------
    public int HpCur
    {
        get => ReadI32(CharacterFormat.OffHpCur);
        set => WriteI32(CharacterFormat.OffHpCur, value);
    }

    public int HpMax
    {
        get => ReadI32(CharacterFormat.OffHpMax);
        set => WriteI32(CharacterFormat.OffHpMax, value);
    }

    public int SpCur
    {
        get => ReadI32(CharacterFormat.OffSpCur);
        set => WriteI32(CharacterFormat.OffSpCur, value);
    }

    public int SpMax
    {
        get => ReadI32(CharacterFormat.OffSpMax);
        set => WriteI32(CharacterFormat.OffSpMax, value);
    }

    // --- attributes -------------------------------------------------------------
    /// <summary>Attribute by index: 0 = Strength … 4 = Luck.</summary>
    public int GetStat(int index) => ReadI32(CharacterFormat.OffStrength + index * 4);

    public void SetStat(int index, int value) => WriteI32(CharacterFormat.OffStrength + index * 4, value);

    // --- spell levels -----------------------------------------------------------
    /// <summary>
    /// Reads a spell-class level out of <c>m_spellLevel</c>. The array is indexed by class id,
    /// so pass 6 for Conjurer through 12 for Geomancer.
    /// </summary>
    public int GetSpellLevel(int classId)
    {
        nuint array = _mem.ReadPtr(Address + (nuint)CharacterFormat.OffSpellLevels);
        if (array == 0 || classId < 0 || classId >= _mem.ReadArrayLength(array)) return 0;
        return _mem.ReadI32(array + (nuint)(Il2Cpp.ArrayHeaderSize + classId * 4));
    }

    public bool SetSpellLevel(int classId, int level)
    {
        nuint array = _mem.ReadPtr(Address + (nuint)CharacterFormat.OffSpellLevels);
        if (array == 0 || classId < 0 || classId >= _mem.ReadArrayLength(array)) return false;
        return _mem.WriteI32(array + (nuint)(Il2Cpp.ArrayHeaderSize + classId * 4), level);
    }

    /// <summary>Raises every caster class to the highest spell level the game grants.</summary>
    public bool LearnAllClassSpells()
    {
        bool ok = false;
        foreach (var (classId, _) in CharacterFormat.CasterClasses)
            ok |= SetSpellLevel(classId, CharacterFormat.MaxSpellLevel);
        return ok;
    }

    /// <summary>
    /// The whole <c>m_spellLevel</c> array, indexed by class id so it can be handed straight
    /// to <see cref="ClassBook"/>. Returns an all-zero array when it cannot be read.
    /// </summary>
    public int[] ReadSpellLevels()
    {
        var levels = new int[CharacterFormat.SpellLevelSlots];
        nuint array = _mem.ReadPtr(Address + (nuint)CharacterFormat.OffSpellLevels);
        if (array == 0) return levels;
        int count = Math.Min(_mem.ReadArrayLength(array), levels.Length);
        for (int i = 0; i < count; i++)
            levels[i] = _mem.ReadI32(array + (nuint)(Il2Cpp.ArrayHeaderSize + i * 4));
        return levels;
    }

    /// <summary>Writes one class's spell level, clamped to what the game itself grants.</summary>
    public bool SetSpellLevelClamped(int classId, int level) =>
        SetSpellLevel(classId, Math.Clamp(level, 0, CharacterFormat.MaxSpellLevel));

    // --- learnt spells ----------------------------------------------------------
    /// <summary>The character's <c>m_learntSpells</c> list object.</summary>
    private nuint LearntSpellsList => _mem.ReadPtr(Address + (nuint)CharacterFormat.OffLearntSpells);

    /// <summary>
    /// The spells this character was taught outright. <c>Character.KnowsSpell</c> checks this
    /// list before it looks at any school level, so anything here is castable regardless of class.
    /// </summary>
    public SpellId[] ReadLearntSpells() =>
        _mem.ReadListInt32(LearntSpellsList).Select(v => (SpellId)v).ToArray();

    /// <summary>True when the spell is in the learnt list — the list only, not school levels.</summary>
    public bool HasLearntSpell(SpellId id) => Array.IndexOf(ReadLearntSpells(), id) >= 0;

    /// <summary>
    /// The full <c>KnowsSpell</c> test, mirroring the game's: the learnt list first, then the
    /// school level, and never the school level for a spell whose level is 0.
    /// </summary>
    public bool KnowsSpell(SpellId id, SpellCatalog catalog)
    {
        if (HasLearntSpell(id)) return true;

        var entry = catalog.Find(id);
        return entry is { IsSpecial: false } && GetSpellLevel(entry.ClassId) >= entry.Level;
    }

    /// <summary>How far <see cref="GrantSpell"/> had to go to teach a spell.</summary>
    public enum GrantOutcome
    {
        /// <summary>The character already held it; nothing was written.</summary>
        AlreadyKnown,

        /// <summary>Appended into the spare capacity the list already had — plain memory writes.</summary>
        AppendedInPlace,

        /// <summary>The list was full, so the game was asked to allocate a bigger backing array.</summary>
        GrewList,

        /// <summary>The list was full and growing it was not available or did not work.</summary>
        Failed,
    }

    /// <summary>The result of a grant, with something worth showing the user.</summary>
    public readonly record struct GrantResult(GrantOutcome Outcome, string Detail)
    {
        public bool Success => Outcome != GrantOutcome.Failed;
    }

    /// <summary>
    /// Teaches the character a spell by putting it in <c>m_learntSpells</c>, which is the only
    /// way to hold a spell no school level grants — ZZGO and NUKE among them.
    ///
    /// <para>Appending in place is tried first and needs nothing but memory writes. It only works
    /// while the list's backing array has room, and a character who was never taught a spell has
    /// a zero-length one, so <paramref name="runtime"/> is what makes the feature work in the
    /// common case: it asks the game itself to allocate a larger array. Without it the shortfall
    /// is reported rather than papered over.</para>
    /// </summary>
    public GrantResult GrantSpell(SpellId id, Il2CppRuntime? runtime)
    {
        nuint list = LearntSpellsList;
        if (list == 0)
            return new GrantResult(GrantOutcome.Failed, "the character has no learnt-spell list.");

        if (HasLearntSpell(id))
            return new GrantResult(GrantOutcome.AlreadyKnown, "already in the learnt-spell list");

        if (_mem.TryAppendInt32(list, (int)id))
            return new GrantResult(GrantOutcome.AppendedInPlace, "appended to the existing list");

        if (runtime == null)
        {
            return new GrantResult(GrantOutcome.Failed,
                "the learnt-spell list is full and growing it needs the game to allocate, " +
                "which is turned off.");
        }

        return GrowAndAppend(list, id, runtime);
    }

    /// <summary>
    /// Replaces the list's backing array with a larger one the game allocated, then appends.
    ///
    /// <para>Write order matters: the new array is filled completely before it is published to
    /// <c>_items</c>, and <c>_size</c> is raised only afterwards. At every point the game could
    /// look, the list describes a consistent run of elements.</para>
    /// </summary>
    private GrantResult GrowAndAppend(nuint list, SpellId id, Il2CppRuntime runtime)
    {
        nuint items = _mem.ReadListItems(list);
        int count = _mem.ReadListCount(list);
        if (items == 0)
            return new GrantResult(GrantOutcome.Failed, "the list has no backing array to take a type from.");

        // Matches List<T>'s own growth: double, with 4 as the floor an empty list starts at.
        int capacity = Math.Max(4, count * 2);

        nuint replacement = runtime.AllocateArrayLike(items, capacity, out string error);
        if (replacement == 0)
            return new GrantResult(GrantOutcome.Failed, $"the game would not allocate a bigger list — {error}");

        try
        {
            for (int i = 0; i < count; i++)
            {
                int value = _mem.ReadI32(items + (nuint)(Il2Cpp.ArrayHeaderSize + i * 4));
                if (!_mem.WriteI32(replacement + (nuint)(Il2Cpp.ArrayHeaderSize + i * 4), value))
                    return new GrantResult(GrantOutcome.Failed, "could not copy the existing spells across.");
            }

            if (!_mem.WriteI32(replacement + (nuint)(Il2Cpp.ArrayHeaderSize + count * 4), (int)id))
                return new GrantResult(GrantOutcome.Failed, "could not write the new spell.");

            if (!_mem.WritePtr(list + Il2Cpp.ListItemsOffset, replacement))
                return new GrantResult(GrantOutcome.Failed, "could not attach the new list storage.");

            if (!_mem.WriteI32(list + Il2Cpp.ListSizeOffset, count + 1))
                return new GrantResult(GrantOutcome.Failed, "could not update the list count.");

            _mem.WriteI32(list + Il2Cpp.ListVersionOffset, _mem.ReadI32(list + Il2Cpp.ListVersionOffset) + 1);
            return new GrantResult(GrantOutcome.GrewList,
                $"list grown from {count} to {capacity} slots by the game");
        }
        finally
        {
            // The collector was left disabled to keep the new array alive until it was reachable.
            runtime.ResumeCollection(out _);
        }
    }

    /// <summary>
    /// Takes a spell back out of the learnt list. Only affects spells granted outright — one the
    /// character earns through a school level stays known until that level is lowered.
    /// </summary>
    public bool RevokeSpell(SpellId id) => _mem.TryRemoveInt32(LearntSpellsList, (int)id);

    // --- class-specific scores --------------------------------------------------
    /// <summary>
    /// Reads the per-class ability scores the game keeps as real fields — the Rogue's three
    /// bonuses, the Hunter's critical chance, the Bard's song counters and the melee attack
    /// count. There is no armour-class field: the game derives armour class from equipment
    /// when it needs it.
    /// </summary>
    public ClassScores ReadClassScores() => new(
        Attacks: ReadI32(CharacterFormat.OffAttacks),
        DisarmTrapBonus: ReadI32(CharacterFormat.OffDisarmTrapBonus),
        IdentifyBonus: ReadI32(CharacterFormat.OffIdentifyBonus),
        HideInShadowsBonus: ReadI32(CharacterFormat.OffHideInShadowsBonus),
        CriticalHit: ReadI32(CharacterFormat.OffCriticalHit),
        SongsRemaining: ReadI32(CharacterFormat.OffSongsRemaining),
        SongsKnown: ReadI32(CharacterFormat.OffSongsKnown));

    /// <summary>Writes the editable class-specific scores back to the character.</summary>
    public void WriteClassScores(ClassScores scores)
    {
        WriteI32(CharacterFormat.OffAttacks, scores.Attacks);
        WriteI32(CharacterFormat.OffDisarmTrapBonus, scores.DisarmTrapBonus);
        WriteI32(CharacterFormat.OffIdentifyBonus, scores.IdentifyBonus);
        WriteI32(CharacterFormat.OffHideInShadowsBonus, scores.HideInShadowsBonus);
        WriteI32(CharacterFormat.OffCriticalHit, scores.CriticalHit);
        WriteI32(CharacterFormat.OffSongsRemaining, scores.SongsRemaining);
        WriteI32(CharacterFormat.OffSongsKnown, scores.SongsKnown);
    }

    // --- class change -----------------------------------------------------------
    /// <summary>
    /// Changes the character's class. When the new class casts and
    /// <paramref name="grantSpellLevel"/> is set, its school is raised to the level the game
    /// would grant at this character level — <c>Mathf.Min(7, (level + 1) / 2)</c>, the same
    /// formula the Review Board uses — so the character can actually cast afterwards.
    /// </summary>
    public string ChangeClass(int newClass, bool grantSpellLevel = true)
    {
        string from = ClassName;
        Class = newClass;

        var info = ClassBook.Find(newClass);
        if (!grantSpellLevel || info is not { IsCaster: true })
            return $"class changed from {from} to {ClassBook.ClassName(newClass)}";

        int granted = ClassBook.SpellLevelForLevel(Level);
        if (GetSpellLevel(newClass) >= granted)
            return $"class changed from {from} to {info.Name}";

        SetSpellLevelClamped(newClass, granted);
        return $"class changed from {from} to {info.Name}, {info.Name} spell level set to {granted}";
    }

    // --- inventory --------------------------------------------------------------
    /// <summary>The character's <c>Item[]</c>, or 0 when the inventory is not set up yet.</summary>
    private nuint ItemArray
    {
        get
        {
            nuint inventory = _mem.ReadPtr(Address + (nuint)CharacterFormat.OffInventory);
            return inventory == 0 ? 0 : _mem.ReadPtr(inventory + (nuint)CharacterFormat.InventoryItems);
        }
    }

    /// <summary>Charge count of every carried item; null for an empty slot.</summary>
    public int?[] ReadItemCharges()
    {
        nuint items = ItemArray;
        int count = _mem.ReadArrayLength(items);
        if (count <= 0 || count > GameFacts.CharacterInventorySize) count = 0;

        var result = new int?[count];
        for (int i = 0; i < count; i++)
        {
            nuint item = _mem.ReadArrayRef(items, i);
            result[i] = item == 0 ? null : _mem.ReadI32(item + (nuint)CharacterFormat.ItemCharges);
        }
        return result;
    }

    /// <summary>
    /// Zeroes the charge count of every carried item. <c>Character.UseItemCharge</c> bails out
    /// on a zero count instead of decrementing, so those items are never consumed.
    /// </summary>
    public bool SetAllItemsInfinite()
    {
        nuint items = ItemArray;
        int count = _mem.ReadArrayLength(items);
        if (count <= 0 || count > GameFacts.CharacterInventorySize) return false;

        bool wrote = false;
        for (int i = 0; i < count; i++)
        {
            nuint item = _mem.ReadArrayRef(items, i);
            if (item == 0) continue;
            wrote |= _mem.WriteI32(item + (nuint)CharacterFormat.ItemCharges, 0);
        }
        return wrote;
    }

    // --- display helpers --------------------------------------------------------
    public string ClassName => CharacterFormat.ClassName(Class);
    public string RaceName => CharacterFormat.RaceName(Race);
    public string ConditionName => CharacterFormat.ConditionName(Condition);

    /// <summary>True when this slot holds a real party member rather than a summon or a blank.</summary>
    public bool IsOccupied
    {
        get
        {
            if (Address == 0) return false;
            var buf = new byte[CharacterFormat.ProbeSize];
            if (_mem.Read(Address, buf, buf.Length) != buf.Length) return false;
            return CharacterFormat.LooksLikeCharacter(buf);
        }
    }

    public override string ToString() => $"{Slot}: {Name} (L{Level} {RaceName} {ClassName})";
}
