using Space1889Trainer.Memory;

namespace Space1889Trainer.Game;

/// <summary>One of the Space 1889 programs that keeps a far pointer to the game-state block.</summary>
/// <param name="FileName">The executable, as <c>1889.COM</c> chains it.</param>
/// <param name="IdentityText">A string unique to this program's data group.</param>
/// <param name="IdentityOffset">Where <paramref name="IdentityText"/> sits in the data group.</param>
/// <param name="StatePointerOffset">The data-group offset of the <c>offset:segment</c> far pointer to the block.</param>
/// <param name="Witness">How the locator reports a pointer found here.</param>
public sealed record GameModule(
    string FileName,
    string IdentityText,
    int IdentityOffset,
    int StatePointerOffset,
    PointerWitness Witness);

/// <summary>
/// Build facts about the shipped DOS release (Paragon Software, 1990) that the locator and the
/// documents rely on. Every executable is Microsoft EXEPACK-compressed Turbo C 2.0; the data-group
/// offsets below are measured on the unpacked images, and they are what the running program uses,
/// because EXEPACK restores the image byte for byte before jumping to it.
/// </summary>
public static class GameFacts
{
    /// <summary>The Turbo C runtime banner, at <c>DS:0004</c> in every Space 1889 program.</summary>
    public const string TurboCBanner = "Turbo-C - Copyright (c) 1988 Borland Intl.";

    /// <summary>Data-group offset of <see cref="TurboCBanner"/>.</summary>
    public const int TurboCBannerOffset = 0x0004;

    /// <summary>The programs that hold a state pointer, with where to find it. [Confirmed-static; M.EXE confirmed live]</summary>
    public static readonly IReadOnlyList<GameModule> Modules = new[]
    {
        // M.EXE saves with write(fd, *(far*)DS:0x5737, 0x4EE6); live, DS:0x5737 held 0208:0000,
        // the address the anchor found.
        new GameModule("M.EXE", "PARTY ACCT.", 0x0968, 0x5737, PointerWitness.GroundGame),
        // S.EXE saves with the same call through DS:0x2DD5.
        new GameModule("S.EXE", "UNCHARTED SPACE", 0x0580, 0x2DD5, PointerWitness.SpaceGame),
        // CG.EXE clears the block with memset(*(far*)DS:0x1DF5, 0, 0x4EE6) before building a party.
        new GameModule("CG.EXE", "CHARACTER POOL", 0x14E3, 0x1DF5, PointerWitness.CharacterGenerator),
    };

    /// <summary>Emulated video mode: 320 x 200, 16 colours (EGA/VGA mode 0Dh).</summary>
    public const int ScreenWidth = 320;

    /// <summary>Emulated video height.</summary>
    public const int ScreenHeight = 200;

    /// <summary>Pennies in a pound sterling (the game's own conversion: "ENTER AMOUNT IN POUNDS (240 PENNIES)").</summary>
    public const int PenniesPerPound = 240;

    /// <summary>Pennies in a shilling.</summary>
    public const int PenniesPerShilling = 12;

    /// <summary>Formats a penny amount as pounds, shillings and pence (e.g. 24000 → "£100 0s 0d").</summary>
    public static string FormatMoney(long pennies)
    {
        if (pennies < 0) return "-" + FormatMoney(-pennies);
        long pounds = pennies / PenniesPerPound;
        long rest = pennies % PenniesPerPound;
        return $"£{pounds:N0} {rest / PenniesPerShilling}s {rest % PenniesPerShilling}d";
    }
}
