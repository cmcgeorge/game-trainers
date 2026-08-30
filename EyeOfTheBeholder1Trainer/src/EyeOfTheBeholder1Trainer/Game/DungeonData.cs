namespace EyeOfTheBeholder1Trainer.Game;

public static class DungeonData
{
    private const int N = GameFacts.LevelGridSize;

    public static readonly IReadOnlyList<DungeonLevel> Levels = Build();

    private static IReadOnlyList<DungeonLevel> Build() =>
    [
        Create(0, "Sewers", "Entry from the Yawning Tavern. Follow the northern passage to descend.", 1, [
            (16, 29, "Entry", "Arrival from the Yawning Tavern."),
            (16, 2, "Stairs Down", "Descends to the Dwarven Ruins.")]),
        Create(1, "Dwarven Ruins", "Ancient halls with a lever puzzle and routes to the deeper crypts.", 2, [
            (16, 29, "Stairs Up", "Returns to the Sewers."),
            (28, 16, "Stairs Down", "Descends to the Skeleton Crypts."),
            (5, 9, "Dwarven Key", "A key needed for sealed dwarven doors.")]),
        Create(2, "Skeleton Crypts", "Undead-filled burial chambers ruled by the Skeleton King.", 3, [
            (3, 16, "Stairs Up", "Returns to the Dwarven Ruins."),
            (16, 2, "Stairs Down", "Descends to the Drow Outpost."),
            (16, 16, "Skeleton King", "The ruler of these crypts guards the central chamber.")]),
        Create(3, "Drow Outpost", "A fortified dark elf outpost with patrols and guarded passages.", 4, [
            (16, 29, "Stairs Up", "Returns to the Skeleton Crypts."),
            (28, 16, "Stairs Down", "Descends to the Lower Dwarven Ruins."),
            (7, 23, "Important Item", "A cache of magical equipment." )]),
        Create(4, "Lower Dwarven Ruins", "Deeper halls haunted by dwarven ghosts.", 5, [
            (3, 16, "Stairs Up", "Returns to the Drow Outpost."),
            (16, 2, "Stairs Down", "Descends to the Hall of the Dead."),
            (24, 8, "Secret", "A concealed side chamber.")]),
        Create(5, "Hall of the Dead", "Wights, ghouls, and a portal to a hidden level lie below.", 6, [
            (16, 29, "Stairs Up", "Returns to the Lower Dwarven Ruins."),
            (28, 16, "Stairs Down", "Descends to the Catacombs."),
            (5, 9, "Secret Portal", "A magical portal leads to the Secret Level.")]),
        Create(6, "Catacombs", "A maze of tombs and teleporters leading toward the mind flayers.", 7, [
            (3, 16, "Stairs Up", "Returns to the Hall of the Dead."),
            (16, 2, "Stairs Down", "Descends to the Mind Flayer Tunnels."),
            (16, 16, "Teleport Maze", "Teleporters complicate the central catacombs.")]),
        Create(7, "Mind Flayer Tunnels", "Twisting tunnels controlled by mind flayers.", 8, [
            (16, 29, "Stairs Up", "Returns to the Catacombs."),
            (28, 16, "Stairs Down", "Descends toward Xanathar."),
            (7, 23, "Important Item", "Magical weapons are found in this area.")]),
        Create(8, "Xanathar's Approach", "Beholder-kin guard the approach to Xanathar's inner lairs.", 9, [
            (3, 16, "Stairs Up", "Returns to the Mind Flayer Tunnels."),
            (16, 2, "Stairs Down", "Descends to the Deeper Lairs."),
            (24, 8, "Scroll of Xanathar", "A vital clue to the final confrontation.")]),
        Create(9, "Deeper Lairs", "Death kisses and deadly traps guard the final descent.", 10, [
            (16, 29, "Stairs Up", "Returns to Xanathar's Approach."),
            (28, 16, "Stairs Down", "Descends to Xanathar's Lair."),
            (5, 9, "Secret", "A hidden cache beyond a false wall.")]),
        Create(10, "Xanathar's Lair", "The final lair of Xanathar the beholder.", 11, [
            (3, 16, "Stairs Up", "Returns to the Deeper Lairs."),
            (16, 16, "Xanathar", "Defeat Xanathar to complete Khelben's quest."),
            (24, 8, "Piergeiron", "The paladin leader is a key ally in Waterdeep.")]),
        Create(11, "Secret Level", "A hidden level reached only through the magical portal on level 6.", 12, [
            (16, 29, "Portal", "Returns to the Hall of the Dead."),
            (7, 23, "Secret", "A hidden treasure chamber."),
            (24, 8, "Important Item", "Rare magical equipment.")]),
    ];

    private static DungeonLevel Create(int index, string name, string description, int layout,
        (int x, int y, string name, string description)[] pois)
    {
        var rows = CreateAsciiGrid(layout, pois);
        var grid = new CellKind[N, N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                grid[x, y] = rows[y][x] == '#' ? CellKind.Wall : CellKind.Floor;
        return new DungeonLevel(index, name, description, grid,
            pois.Select(p => new DungeonPoi(p.x, p.y, p.name, p.description)).ToList());
    }

    private static string[] CreateAsciiGrid(int layout,
        IEnumerable<(int x, int y, string name, string description)> pois)
    {
        var cells = new char[N, N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                cells[x, y] = x == 0 || y == 0 || x == N - 1 || y == N - 1 ? '#' : '.';

        for (int x = 4; x < N - 4; x += 6)
            for (int y = 2; y < N - 2; y++)
                if ((y + layout) % 9 is not 3 and not 4) cells[x, y] = '#';
        for (int y = 5; y < N - 4; y += 7)
            for (int x = 2; x < N - 2; x++)
                if ((x + layout * 2) % 8 is not 2 and not 3) cells[x, y] = '#';

        foreach (var poi in pois)
        {
            cells[poi.x, poi.y] = poi.name switch
            {
                "Stairs Up" => 'U',
                "Stairs Down" => 'D',
                "Portal" or "Secret Portal" => 'P',
                "Xanathar" or "Skeleton King" => 'B',
                "Secret" => 'S',
                "Piergeiron" => 'N',
                _ => 'I',
            };
        }

        return Enumerable.Range(0, N)
            .Select(y => new string(Enumerable.Range(0, N).Select(x => cells[x, y]).ToArray()))
            .ToArray();
    }
}
