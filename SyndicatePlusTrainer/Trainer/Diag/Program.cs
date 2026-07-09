using SyndicateTrainer;

// Verify the fix through the shipped API: the weapon array should now include flamers,
// and FreezeAmmo should top them up.
const long FLAMER0_AMMO = 0x75798 + 0xC;   // first flamer record ammo field

var procs = GameConnection.FindDosBoxProcesses();
if (procs.Count == 0) { Console.WriteLine("No DOSBox process."); return; }
using var game = new GameConnection();
if (!game.Attach(procs[0], Console.WriteLine)) { Console.WriteLine("Attach failed."); return; }
Console.WriteLine();

int ReadFlamer()
{
    var b = new byte[2];
    game.TryReadGame(FLAMER0_AMMO, b, 2);
    return b[0] | (b[1] << 8);
}

Console.WriteLine($"LocateWeaponArray: {game.LocateWeaponArray()}  weaponCount={game.WeaponCount}  (was 8, expect 12)");
Console.WriteLine($"flamer[0] ammo BEFORE freeze: {ReadFlamer()}");
int frozen = game.FreezeAmmo(GameConnection.AmmoFreezeTarget);
Console.WriteLine($"FreezeAmmo -> topped {frozen} weapon(s) to {GameConnection.AmmoFreezeTarget}");
Console.WriteLine($"flamer[0] ammo AFTER  freeze: {ReadFlamer()}");
Console.WriteLine("\nDone.");
