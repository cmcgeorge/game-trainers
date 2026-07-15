namespace BattleTech1Trainer.Game;

/// <summary>One row of the 'Mech roster reference.</summary>
public readonly record struct MechInfo(
    string Name,
    string Chassis,
    int Tons,
    string Armor,
    string Jump,
    string Weapons,
    string Role);

/// <summary>
/// The read-only 'Mech roster the trainer surfaces so a player can compare chassis without leaving the
/// game. 'Mech <b>names</b> are [Confirmed] from the <c>0x1D9C8</c> name table in <c>BTECH.EXE</c>
/// (see <c>.docs/ReverseEngineering.md</c> §3.2); the tonnage/armor/role columns are [Corroborated]
/// from public sources (§4). The in-EXE record stride is non-uniform and not yet decoded, so this is a
/// reference table only — the trainer does not read or write live 'Mech structs.
/// </summary>
public static class MechReference
{
    /// <summary>Number of named 'Mech chassis recovered from the EXE (excluding the spectator placeholder).</summary>
    public const int Count = 7;

    /// <summary>The roster, in EXE name-table order.</summary>
    public static readonly IReadOnlyList<MechInfo> Mechs = Array.AsReadOnly(new MechInfo[]
    {
        new("Locust",    "LCT-1V", 20, "64", "—",     "Med Laser, 2x MG",                       "Fast scout (player start)"),
        new("Wasp",      "WSP-1A", 20, "48", "180 m", "Med Laser, SRM-2",                       "Recon; fragile"),
        new("Stinger",   "STG-3R", 20, "48", "180 m", "Med Laser, 2x MG",                       "Mobile harasser"),
        new("Commando",  "COM-2D", 25, "64", "—",     "SRM-6, SRM-4, Med Laser",                "Missile striker"),
        new("Chameleon", "TRA-6",  50, "—",  "—",     "Large + 2 Med + 4 Small Laser, 2x MG",   "Training 'Mech (unmodifiable)"),
        new("Jenner",    "JR7-D",  35, "—",  "jets",  "4x Med Laser, SRM-4",                    "Kurita raider (enemy)"),
        new("UrbanMech", "UM-R60", 30, "96", "60 m",  "AC/10, Small Laser",                     "Slow; heaviest ballistic in-game"),
    });
}
