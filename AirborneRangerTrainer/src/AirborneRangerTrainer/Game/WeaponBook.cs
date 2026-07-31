namespace AirborneRangerTrainer.Game;

/// <summary>One weapon the ranger can have selected.</summary>
/// <param name="Code">The value the game stores at <c>DGROUP:0xC891</c>.</param>
/// <param name="Name">Display name.</param>
/// <param name="Note">What it is good for.</param>
public readonly record struct WeaponInfo(int Code, string Name, string Note);

/// <summary>
/// The five weapon codes, read straight out of the game's command dispatcher: it writes 0, 1, 2 and
/// 4 to <c>DGROUP:0xC891</c> for the four selectable weapons, and 3 when a time bomb is armed.
/// </summary>
public static class WeaponBook
{
    /// <summary>Every weapon code, in numeric order.</summary>
    public static readonly IReadOnlyList<WeaponInfo> All = new[]
    {
        new WeaponInfo(0, "Carbine",
            "30 rounds per magazine; effective against unarmoured troops. The ranger reverts to it " +
            "automatically after any other weapon is used."),
        new WeaponInfo(1, "Hand grenade",
            "Throw range grows with how long the fire button is held. Good against troops, machine-gun " +
            "nests, wooden doors and light armour."),
        new WeaponInfo(2, "LAW rocket",
            "Single-shot disposable launcher, effective against nearly all troops and defences — and " +
            "the only real answer to a minitank."),
        new WeaponInfo(3, "Time bomb",
            "Armed with a 5-, 10- or 15-second fuse. Effective against everything, and the tool for " +
            "armoured structures like a pipeline pumping station."),
        new WeaponInfo(4, "Knife",
            "Melee only, unlimited, and silent — it does not alert anyone, which is what makes the " +
            "stealth-scored missions winnable."),
    };

    /// <summary>Highest valid weapon code.</summary>
    public static int MaxCode => All[^1].Code;

    /// <summary>Name for <paramref name="code"/>, or a placeholder for an unknown value.</summary>
    public static string Name(int code)
    {
        foreach (var w in All)
            if (w.Code == code) return w.Name;
        return $"(weapon {code})";
    }
}
