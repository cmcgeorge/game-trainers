namespace BattleTech1Trainer.Game;

/// <summary>One weapon name from the <c>BTECH.EXE</c> table, tagged with the scale it belongs to.</summary>
public readonly record struct WeaponInfo(string Name, string Scale, string Notes);

/// <summary>
/// The read-only list of weapon names recovered from the <b>Confirmed</b> 17-byte weapon table
/// (see <c>.docs/ReverseEngineering.md</c> §3.1), in table order. The names are byte-verified; the
/// short notes are [Corroborated] usage hints from the strategy guide, surfaced so a player can weigh
/// loadouts without leaving the trainer. Nothing here is written to memory.
/// </summary>
public static class WeaponReference
{
    /// <summary>Personal (on-foot) weapons, in table order (Cudgel … Inferno).</summary>
    public static readonly IReadOnlyList<WeaponInfo> Personal = Array.AsReadOnly(new WeaponInfo[]
    {
        new("Cudgel",     "Small-arms", "Starter melee"),
        new("Knife",      "Small-arms", "Melee"),
        new("Sword",      "Small-arms", "Melee"),
        new("VibroBlade", "Small-arms", "Best melee"),
        new("Shortbow",   "Small-arms", "Silent ranged"),
        new("Longbow",    "Small-arms", "Silent ranged"),
        new("Crossbow",   "Small-arms", "Silent ranged"),
        new("Pistol",     "Ballistic",  "Sidearm"),
        new("Rifle",      "Ballistic",  "Ranged"),
        new("MachineGun", "Ballistic",  "Rapid fire"),
        new("SR Missile", "Ballistic",  "Handheld launcher — one-shots foot troops"),
        new("Inferno",    "Ballistic",  "No damage; spikes a 'Mech's heat"),
    });

    /// <summary>'Mech-scale weapons, in table order (LaserPistl … Kick).</summary>
    public static readonly IReadOnlyList<WeaponInfo> Mech = Array.AsReadOnly(new WeaponInfo[]
    {
        new("LaserPistl", "'Mech-scale", "No ammo"),
        new("LaserRifle", "'Mech-scale", "No ammo"),
        new("Flamer",     "'Mech-scale", "Heat / anti-infantry"),
        new("SmallLaser", "'Mech-scale", "No ammo"),
        new("Med Laser",  "'Mech-scale", "No ammo — mainstay"),
        new("LargeLaser", "'Mech-scale", "No ammo"),
        new("PPC",        "'Mech-scale", "Heavy energy"),
        new("AutoCann/2", "'Mech-scale", "Ballistic — uses ammo"),
        new("AutoCann/5", "'Mech-scale", "Ballistic — uses ammo"),
        new("AutoCann10", "'Mech-scale", "Ballistic — UrbanMech gun"),
        new("AutoCann20", "'Mech-scale", "Ballistic — heaviest"),
        new("MachineGun", "'Mech-scale", "Anti-infantry"),
        new("LRMissile5", "'Mech-scale", "Long-range missiles"),
        new("LRMissil10", "'Mech-scale", "Long-range missiles"),
        new("LRMissil15", "'Mech-scale", "Long-range missiles"),
        new("LRMissil20", "'Mech-scale", "Long-range missiles"),
        new("SRMissile2", "'Mech-scale", "Short-range missiles"),
        new("SRMissile4", "'Mech-scale", "Short-range missiles"),
        new("SRMissile6", "'Mech-scale", "Short-range missiles"),
        new("Kick",       "'Mech-scale", "Physical attack — no heat"),
    });

    /// <summary>All weapons (personal first, then 'Mech-scale) for the reference grid.</summary>
    public static readonly IReadOnlyList<WeaponInfo> All =
        Array.AsReadOnly(Personal.Concat(Mech).ToArray());
}
