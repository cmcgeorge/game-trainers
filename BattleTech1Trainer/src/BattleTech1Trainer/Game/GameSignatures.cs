using System.Text;

namespace BattleTech1Trainer.Game;

/// <summary>
/// Verbatim byte signatures the game carries in <c>BTECH.EXE</c>, used to confirm BattleTech is the
/// process attached to. Because the executable is a real-mode DOS image that DOSBox maps into guest
/// RAM essentially unchanged, these static ASCII runs appear in the emulator's memory and can be found
/// with <c>BytePatternScanner</c> (see <c>.docs/ReverseEngineering.md</c> §3, §5.2). They are
/// <b>read-only detection anchors</b> — the mutable game state (C-Bills, health, skills) is not stored
/// next to them, so the trainer still relies on the value scanner to reach editable values.
/// </summary>
public static class GameSignatures
{
    /// <summary>The window/title string (Confirmed @ file offset <c>0x1EDCE</c>).</summary>
    public const string GameName = "BattleTech: The Crescent Hawk's Inception";

    /// <summary>One named detection signature and the bytes to scan for.</summary>
    public readonly record struct Signature(string Name, byte[] Pattern);

    /// <summary>
    /// The Inspect-Character field block (Confirmed @ <c>0x1E882</c>):
    /// <c>"Name  :\rWeapon:\rArmor :"</c>. This is the most distinctive anchor — a long, structured run
    /// unlikely to collide with unrelated data.
    /// </summary>
    public static byte[] InspectFields => new byte[]
    {
        0x4E, 0x61, 0x6D, 0x65, 0x20, 0x20, 0x3A, 0x0D, // "Name  :\r"
        0x57, 0x65, 0x61, 0x70, 0x6F, 0x6E, 0x3A, 0x0D, // "Weapon:\r"
        0x41, 0x72, 0x6D, 0x6F, 0x72, 0x20, 0x3A,       // "Armor :"
    };

    /// <summary>The title string as ASCII bytes (Confirmed).</summary>
    public static byte[] Title => Encoding.ASCII.GetBytes(GameName);

    /// <summary>
    /// The detection signatures, most-distinctive first. The detector returns on the first hit, so the
    /// Inspect-field block (structured, low false-positive) is tried before the title string.
    /// </summary>
    public static IReadOnlyList<Signature> All => Array.AsReadOnly(new[]
    {
        new Signature("Inspect-Character fields", InspectFields),
        new Signature("Title string", Title),
    });
}
