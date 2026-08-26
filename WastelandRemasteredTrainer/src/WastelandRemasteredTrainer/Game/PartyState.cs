using WastelandRemasteredTrainer.Memory;

namespace WastelandRemasteredTrainer.Game;

/// <summary>
/// The party's live position and clock, read from the game's save-data block.
///
/// <para><b>Unverified.</b> Unlike the character offsets — which come from the IL2CPP
/// field-offset table and are exercised by the harness — the route to this block
/// (<c>Wasteland.m_instance.m_partyManager.m_saveData</c>, and the field offsets inside
/// <c>CoreSave</c>) has not been watched against a running game. It is therefore read-only and
/// presented as "unconfirmed" in the UI: a wrong offset shows a nonsense number, which is
/// harmless, whereas writing to a wrong offset would not be.</para>
///
/// <para>The DOS Wasteland trainer found its equivalent position header to be a write-only
/// shadow that the game never reads back, so live teleport is deliberately not offered here
/// until someone confirms the remaster behaves differently.</para>
/// </summary>
public sealed record PartyState(int MapX, int MapY, int CurrentMap, int NumberInParty, int Clock)
{
    public string PositionText => $"map {CurrentMap} at ({MapX}, {MapY})";

    public string PartyText => NumberInParty == 1 ? "1 ranger" : $"{NumberInParty} rangers";
}

/// <summary>Reads <see cref="PartyState"/> out of the running game, when it can be reached.</summary>
public static class PartyStateReader
{
    /// <summary>
    /// Follows the singletons to the save-data block and reads the position/clock fields.
    /// Returns null when any link in the chain is missing or unreadable — which is the normal
    /// case before a game is loaded, and also what happens if the unverified offsets are wrong
    /// in a way that leaves a null pointer.
    /// </summary>
    public static PartyState? Read(IMemorySource mem, GameClasses classes)
    {
        nuint saveData = FindSaveData(mem, classes);
        if (saveData == 0) return null;

        if (!mem.TryReadByte(saveData + (nuint)CharacterFormat.CoreSaveMapX, out byte x)) return null;
        if (!mem.TryReadByte(saveData + (nuint)CharacterFormat.CoreSaveMapY, out byte y)) return null;
        if (!mem.TryReadByte(saveData + (nuint)CharacterFormat.CoreSaveCurrentMap, out byte map)) return null;
        if (!mem.TryReadByte(saveData + (nuint)CharacterFormat.CoreSaveNumberInParty, out byte count)) return null;
        if (!mem.TryReadI32(saveData + (nuint)CharacterFormat.CoreSaveClock, out int clock)) return null;

        return new PartyState(x, y, map, count, clock);
    }

    /// <summary>
    /// Reaches <c>m_saveData</c> either straight off the <c>PartyManager</c> singleton or
    /// through the <c>Wasteland</c> singleton that owns it, whichever class the sweep found.
    /// </summary>
    private static nuint FindSaveData(IMemorySource mem, GameClasses classes)
    {
        if (classes.PartyManager != 0)
        {
            nuint manager = mem.ReadStaticRef(classes.PartyManager, CharacterFormat.PartyManagerInstanceStatic);
            if (manager != 0)
            {
                nuint save = mem.ReadPtr(manager + (nuint)CharacterFormat.PartyManagerSaveData);
                if (save != 0) return save;
            }
        }

        if (classes.Wasteland != 0)
        {
            nuint game = mem.ReadStaticRef(classes.Wasteland, CharacterFormat.WastelandInstanceStatic);
            if (game != 0)
            {
                nuint manager = mem.ReadPtr(game + (nuint)CharacterFormat.WastelandPartyManager);
                if (manager != 0)
                {
                    nuint save = mem.ReadPtr(manager + (nuint)CharacterFormat.PartyManagerSaveData);
                    if (save != 0) return save;
                }
            }
        }

        return 0;
    }
}
