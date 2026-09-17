using Space1889Trainer.Game;

namespace Space1889Trainer.Files;

/// <summary>What an annotation marks.</summary>
public enum AnnotationKind { Exit, Service, Entrance, Npc, DigSite, Hidden }

/// <summary>A labelled square (or the top-left square of a run of identical trigger tiles).</summary>
public sealed record MapAnnotation(int Row, int Column, string Label, AnnotationKind Kind, string Description);

/// <summary>A scripted spot M.EXE hard-codes: a dig site, a drop spot, or a tile a quest reveals.</summary>
public sealed record ScriptedSpot(int MapId, int Row, int Column, AnnotationKind Kind, string Label, string Description);

/// <summary>
/// Labels for a map: its exits, shops and entrances (read from the tiles), its NPCs (from ?.NPC), and the
/// scripted dig and drop spots M.EXE hard-codes (dig table DS:0x3364, the map-load patches at 1000:2AF8, the
/// tablet altars and the Stonehenge/Silbury Hill digs).
/// </summary>
public static class MapAnnotations
{
    /// <summary>The scripted spots, from M.EXE's tables. [Static]</summary>
    public static readonly IReadOnlyList<ScriptedSpot> ScriptedSpots = new ScriptedSpot[]
    {
        new(150, 37, 49, AnnotationKind.DigSite, "DIG: tomb", "Dig here (14 paces south of the Eye of the Desert) to open the stairs to Tutankhamen's tomb."),
        new(157, 9, 9, AnnotationKind.DigSite, "DIG: chamber", "Dig here (8 paces north and 1 west of the stairs) to open the burial chamber."),
        new(420, 16, 29, AnnotationKind.DigSite, "DIG: lost city", "Dig at the tail of the scorpion to open the underground city of the Moab."),
        new(470, 32, 36, AnnotationKind.DigSite, "DIG: Worm Cult", "Dig here to open the Worm Cult's hidden entrance (Worm Cult map)."),
        new(431, 18, 23, AnnotationKind.DigSite, "DIG: lair", "Dig here (the steps from the cave entrance) to open the Lurkers' lair where Emilie Van Warren is held."),
        new(115, 15, 11, AnnotationKind.DigSite, "DIG: chalice", "Dig 5 paces east of the Prophet's tomb for the RUBY CHALICE (once)."),
        new(110, 30, 61, AnnotationKind.DigSite, "DIG: day 30", "At Stonehenge, dig here on a day divisible by 30 for the GOLD SHIELD (once)."),
        new(163, 13, 17, AnnotationKind.DigSite, "DROP tablet", "DROP one Teotihuacan tablet here (the other on the second altar)."),
        new(163, 13, 21, AnnotationKind.DigSite, "DROP tablet", "DROP one Teotihuacan tablet here (the other on the first altar)."),
        new(100, 2, 52, AnnotationKind.Hidden, "Inner Earth", "The Inner Earth entrance appears here after the Europa sequence."),
        new(160, 35, 76, AnnotationKind.Hidden, "Atlantis", "The entrance to the Atlantis caves (map 1) appears here once both tablets are on their altars."),
    };

    /// <summary>Every annotation for <paramref name="map"/>.</summary>
    /// <param name="map">The decoded map.</param>
    /// <param name="npcs">Its NPCs (may be empty).</param>
    public static IReadOnlyList<MapAnnotation> For(GameMap map, IReadOnlyList<NpcInfo> npcs)
    {
        ArgumentNullException.ThrowIfNull(map);
        var list = new List<MapAnnotation>();
        if (map.Id != 0 || map.HeaderName != "TEMP") AddTriggers(map, list);
        foreach (var npc in npcs ?? Array.Empty<NpcInfo>())
        {
            if (!map.Contains(npc.Row, npc.Column)) continue;
            string loot = npc.LootItemId > 0 ? $"; drops {ItemBook.NameOf(npc.LootItemId)}" : "";
            list.Add(new MapAnnotation(npc.Row, npc.Column, npc.Name, AnnotationKind.Npc, $"{npc.Name} (NPC {npc.Id}){loot}"));
        }
        foreach (var spot in ScriptedSpots)
            if (spot.MapId == map.Id && map.Contains(spot.Row, spot.Column))
                list.Add(new MapAnnotation(spot.Row, spot.Column, spot.Label, spot.Kind, spot.Description));
        return list;
    }

    private static void AddTriggers(GameMap map, List<MapAnnotation> list)
    {
        bool overland = map.Area == 0 && map.Id is not (992 or 999);
        var seen = new bool[map.Cells.Length];
        for (int i = 0; i < map.Cells.Length; i++)
        {
            if (seen[i]) continue;
            var cell = map.Cells[i];
            if (TileRules.TriggerLabel(cell, overland) is not { } label) continue;

            // One label per connected run of the same tile: a shop sign is several squares wide.
            var stack = new Stack<int>();
            stack.Push(i);
            seen[i] = true;
            int minR = int.MaxValue, minC = int.MaxValue;
            while (stack.Count > 0)
            {
                int j = stack.Pop();
                int r = j / map.Columns, c = j % map.Columns;
                minR = Math.Min(minR, r);
                minC = Math.Min(minC, c);
                Visit(r - 1, c); Visit(r + 1, c); Visit(r, c - 1); Visit(r, c + 1);
            }

            var kind = TileRules.Classify(cell) switch
            {
                TileClass.Exit => AnnotationKind.Exit,
                TileClass.Service => AnnotationKind.Service,
                _ => AnnotationKind.Entrance,
            };
            int target = cell.SheetColumn - 10;
            string description = kind switch
            {
                AnnotationKind.Exit => "Exit to the area's main map (or the planet map)",
                AnnotationKind.Service => TileRules.Services[cell.SheetColumn],
                _ when overland => "Entrance to " + PlaceBook.AreaName(map.Planet, target),
                _ => $"Stairs or door to map {target}",
            };
            if (overland && kind == AnnotationKind.Entrance) label = PlaceBook.AreaName(map.Planet, target);
            list.Add(new MapAnnotation(minR, minC, label, kind, $"{description} (row {minR}, column {minC})"));

            void Visit(int rr, int cc)
            {
                if (!map.Contains(rr, cc)) return;
                int k = rr * map.Columns + cc;
                if (seen[k] || map.Cells[k] != cell) return;
                seen[k] = true;
                stack.Push(k);
            }
        }
    }
}
