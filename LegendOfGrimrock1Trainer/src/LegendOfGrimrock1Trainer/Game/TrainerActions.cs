using LegendOfGrimrock1Trainer.Lua;

namespace LegendOfGrimrock1Trainer.Game;

/// <summary>What a bulk action did, for the status line.</summary>
public readonly record struct ActionResult(int Applied, int Attempted, string Summary)
{
    /// <summary>Whether every write landed.</summary>
    public bool Complete => Attempted > 0 && Applied == Attempted;

    /// <summary>Nothing to do.</summary>
    public static ActionResult Nothing(string summary) => new(0, 0, summary);
}

/// <summary>
/// The edits the UI offers, expressed against a snapshot so each one is a read-validate-write pass
/// rather than a blind poke.
///
/// Two rules run through all of it. First, a value is only written when the slot it came from was
/// read back as a number this tick — the snapshot is the validation, and
/// <see cref="PartyReader"/> zeroes the slot of anything that is not a number, so there is no path
/// that writes a double over a string. Second, nothing writes a value the game cannot produce
/// itself: healing writes <c>value = max</c> rather than an arbitrary number, curing writes zero,
/// and an edit to a bar raises its cap to fit but never lowers it.
/// </summary>
public sealed class TrainerActions
{
    private readonly PartyReader _reader;

    /// <summary>Wraps the reader whose snapshots the actions are applied against.</summary>
    public TrainerActions(PartyReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    /// <summary>Restores a champion's health and energy to their current maxima.</summary>
    public ActionResult Restore(ChampionSnapshot champion)
    {
        int applied = 0, attempted = 0;
        foreach (var name in GameTables.ResourceStats)
        {
            var stat = champion.Stat(name);
            if (stat is null || stat.MaxSlot == 0 || stat.ValueSlot == 0) continue;
            attempted++;
            if (_reader.Write(stat.ValueSlot, stat.Max)) applied++;
        }
        return new ActionResult(applied, attempted, $"restored {champion.Name}");
    }

    /// <summary>Fills a champion's food bar.</summary>
    public ActionResult Feed(ChampionSnapshot champion)
    {
        if (champion.FoodSlot == 0) return ActionResult.Nothing("no food slot");
        bool ok = _reader.Write(champion.FoodSlot, GameFacts.MaxFood);
        return new ActionResult(ok ? 1 : 0, 1, $"fed {champion.Name}");
    }

    /// <summary>Sets a champion's food to an exact value.</summary>
    public ActionResult SetFood(ChampionSnapshot champion, double food)
    {
        if (champion.FoodSlot == 0) return ActionResult.Nothing("no food slot");
        double clamped = Math.Clamp(food, 0, GameFacts.MaxFood);
        bool ok = _reader.Write(champion.FoodSlot, clamped);
        return new ActionResult(ok ? 1 : 0, 1, $"{champion.Name} food = {clamped:0}");
    }

    /// <summary>
    /// Clears every harmful condition. Burdened and overloaded are included even though the game
    /// recomputes them from carried weight each frame — clearing them is harmless and the pair
    /// re-asserts itself immediately, which is the honest behaviour to show.
    /// </summary>
    public ActionResult Cure(ChampionSnapshot champion)
    {
        int applied = 0, attempted = 0;
        foreach (var condition in champion.Conditions)
        {
            if (condition.Kind != ConditionKind.Harmful) continue;
            if (condition.Value == 0 && condition.Timer == 0) continue;
            // Counted per slot, not per condition: a condition whose value did not resolve this tick
            // has nothing to write, and reporting it as applied would claim a cure that never landed.
            if (condition.ValueSlot != 0)
            {
                attempted++;
                if (_reader.Write(condition.ValueSlot, 0)) applied++;
            }
            if (condition.TimerSlot != 0)
            {
                attempted++;
                if (_reader.Write(condition.TimerSlot, 0)) applied++;
            }
        }
        return attempted == 0
            ? ActionResult.Nothing($"{champion.Name} has no harmful conditions")
            : new ActionResult(applied, attempted, $"cured {champion.Name}");
    }

    /// <summary>
    /// Sets every beneficial condition for <paramref name="seconds"/>. Grimrock stores a condition as
    /// a non-zero <c>value</c> plus a <c>timer</c> it counts down, so both are written.
    /// </summary>
    public ActionResult Bless(ChampionSnapshot champion, double seconds)
    {
        if (seconds <= 0) return ActionResult.Nothing("duration must be positive");

        int applied = 0, attempted = 0;
        foreach (var condition in champion.Conditions)
        {
            if (condition.Kind != ConditionKind.Beneficial) continue;
            if (!GameTables.TimedConditions.Contains(condition.Name)) continue;
            if (condition.ValueSlot != 0)
            {
                attempted++;
                if (_reader.Write(condition.ValueSlot, 1)) applied++;
            }
            if (condition.TimerSlot != 0)
            {
                attempted++;
                if (_reader.Write(condition.TimerSlot, seconds)) applied++;
            }
        }
        return attempted == 0
            ? ActionResult.Nothing($"{champion.Name} has no writable conditions")
            : new ActionResult(applied, attempted, $"blessed {champion.Name} for {seconds:0}s");
    }

    /// <summary>
    /// Sets a stat's value, and its cap alongside where that is what the game's own model means.
    ///
    /// Health and energy are bars: the cap is raised to keep the value inside it, but never
    /// <i>lowered</i>. Writing the cap down would be destructive in a way nothing in the game can
    /// undo — dropping a champion's current health to 30 must not throw away a maximum of 300 — and
    /// Grimrock autosaves, so the mistake would outlive the session. Every other stat is a score,
    /// which Grimrock stores with the same number in both fields, so those move together.
    /// </summary>
    public ActionResult SetStat(ChampionSnapshot champion, string statName, double value)
    {
        var stat = champion.Stat(statName);
        if (stat is null) return ActionResult.Nothing($"{champion.Name} has no {statName}");

        double clamped = Math.Clamp(value, 0, GameFacts.MaxStatValue);
        bool isBar = GameTables.ResourceStats.Contains(statName);
        double newMax = isBar ? Math.Max(stat.Max, clamped) : clamped;

        int applied = 0, attempted = 0;

        if (stat.MaxSlot != 0)
        {
            attempted++;
            // A bar keeps a floor of 1 so its track never has zero length; a score does not, because
            // 0 protection and 0 evasion are ordinary values the game writes itself.
            if (_reader.Write(stat.MaxSlot, isBar ? Math.Max(newMax, 1) : newMax)) applied++;
        }
        if (stat.ValueSlot != 0)
        {
            attempted++;
            if (_reader.Write(stat.ValueSlot, clamped)) applied++;
        }

        return new ActionResult(applied, attempted, $"{champion.Name} {stat.UiName} = {clamped:0}");
    }

    /// <summary>
    /// Raises every stat that is currently below <paramref name="target"/> to it, leaving anything
    /// already higher alone. A bar whose cap is above the target is filled to that cap rather than cut
    /// down to it — the point of the button is to make the party stronger, and lowering a maximum the
    /// player earned is the one thing <see cref="SetStat"/> refuses to do.
    /// </summary>
    public ActionResult MaxStats(ChampionSnapshot champion, double target)
    {
        int applied = 0, attempted = 0;
        foreach (var stat in champion.Stats)
        {
            if (stat.Value >= target && stat.Max >= target) continue;
            var r = SetStat(champion, stat.Name, Math.Max(target, Math.Max(stat.Value, stat.Max)));
            applied += r.Applied;
            attempted += r.Attempted;
        }
        return attempted == 0
            ? ActionResult.Nothing($"{champion.Name} is already at {target:0}")
            : new ActionResult(applied, attempted, $"maxed {champion.Name} to {target:0}");
    }

    /// <summary>Sets one condition's <c>value</c> — non-zero means the champion has it.</summary>
    public bool SetConditionValue(ConditionSnapshot condition, double value) =>
        _reader.Write(condition.ValueSlot, value);

    /// <summary>Sets one condition's remaining duration, in seconds.</summary>
    public bool SetConditionTimer(ConditionSnapshot condition, double seconds) =>
        _reader.Write(condition.TimerSlot, Math.Max(0, seconds));

    /// <summary>Sets a champion's unspent skill points, and the sheet's badge alongside.</summary>
    public ActionResult SetSkillPoints(ChampionSnapshot champion, int points)
    {
        if (champion.SkillPointsSlot == 0) return ActionResult.Nothing("no skill-point slot");

        int clamped = Math.Clamp(points, 0, MaxSkillPoints);
        int applied = 0, attempted = 1;
        if (_reader.Write(champion.SkillPointsSlot, clamped)) applied++;

        // The character sheet's "Level Up" badge is a separate condition the game keeps in step with
        // the point count, so move it too or the sheet disagrees with itself until the next level.
        // It is counted, because a badge that failed to move is exactly the thing this write exists
        // to prevent and reporting 1/1 would hide it.
        var badge = champion.Condition("unused_skill_points");
        if (badge is { ValueSlot: not 0 })
        {
            attempted++;
            if (_reader.Write(badge.ValueSlot, clamped > 0 ? 1 : 0)) applied++;
        }

        return new ActionResult(applied, attempted, $"{champion.Name} has {clamped} skill point(s)");
    }

    /// <summary>Ceiling the trainer puts on a skill-point grant. A UI guard, not a rule of the game.</summary>
    public const int MaxSkillPoints = 999;

    /// <summary>Sets one trained skill's level.</summary>
    public ActionResult SetSkill(SkillSnapshot skill, int level)
    {
        if (skill.LevelSlot == 0) return ActionResult.Nothing("no skill slot");
        int clamped = Math.Clamp(level, 0, GameFacts.MaxSkillLevel);
        bool ok = _reader.Write(skill.LevelSlot, clamped);
        return new ActionResult(ok ? 1 : 0, 1, $"{skill.UiName} = {clamped}");
    }

    /// <summary>
    /// Sets a champion's level. Experience is a separate field with its own setter — Grimrock does
    /// not derive one from the other, and quietly rewriting a player's experience total to match a
    /// level edit would lose progress they did not ask to lose.
    /// </summary>
    public ActionResult SetLevel(ChampionSnapshot champion, int level)
    {
        if (champion.LevelSlot == 0) return ActionResult.Nothing("no level slot");
        int clamped = Math.Clamp(level, 1, GameFacts.MaxChampionLevel);
        bool ok = _reader.Write(champion.LevelSlot, clamped);
        return new ActionResult(ok ? 1 : 0, 1, $"{champion.Name} is level {clamped}");
    }

    /// <summary>Sets a champion's accumulated experience.</summary>
    public ActionResult SetExperience(ChampionSnapshot champion, double experience)
    {
        if (champion.ExperienceSlot == 0) return ActionResult.Nothing("no experience slot");
        double clamped = Math.Max(0, experience);
        bool ok = _reader.Write(champion.ExperienceSlot, clamped);
        return new ActionResult(ok ? 1 : 0, 1, $"{champion.Name} has {clamped:0} XP");
    }

    /// <summary>Runs an action over every living champion in the party.</summary>
    public ActionResult ForEachChampion(PartySnapshot party, Func<ChampionSnapshot, ActionResult> action, string label)
    {
        int applied = 0, attempted = 0;
        foreach (var champion in party.Champions)
        {
            if (!champion.Enabled) continue;
            var r = action(champion);
            applied += r.Applied;
            attempted += r.Attempted;
        }
        return attempted == 0 ? ActionResult.Nothing($"nothing to do for {label}") : new ActionResult(applied, attempted, label);
    }

    // --- party-level edits ----------------------------------------------------------------------------

    /// <summary>Whether a tile is somewhere the party could legally stand.</summary>
    public bool IsWalkable(MapSnapshot map, int x, int y)
    {
        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height) return false;
        var bits = _reader.ReadCell(map, x, y);
        if (bits is null) return false;
        long mask = (long)bits.Value;
        return (mask & GrimrockLayout.CellBits.Blocking) == 0;
    }

    /// <summary>
    /// Moves the party to another tile on the level it is already on.
    ///
    /// The write is three values, not one: the party's <c>x</c>/<c>y</c>, plus the
    /// <see cref="GrimrockLayout.CellBits.DynamicObstacle"/> bit moved from the tile being left to
    /// the tile being entered. That bit is what <c>Party:occupyCell</c> sets, and monsters path
    /// around it — teleporting without moving it leaves a phantom body behind. Confirmed live by
    /// stepping a party from (2,8) to (2,7) and back, watching the bit follow both ways.
    ///
    /// Cross-level travel is deliberately not offered: a level change in Grimrock also tears down and
    /// rebuilds the map, and writing <c>party.level</c> alone would leave the party pointing at a map
    /// it is no longer standing on.
    /// </summary>
    public ActionResult Teleport(PartySnapshot party, MapSnapshot map, int x, int y)
    {
        if (map.Level != party.Level)
            return ActionResult.Nothing("teleporting between levels is not supported — use the stairs");
        if (!map.HasPlausibleSize)
            return ActionResult.Nothing($"{map.Name} reads as {map.Width}x{map.Height}, which is not a level");
        if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
            return ActionResult.Nothing($"({x}, {y}) is outside the {map.Width}x{map.Height} map");
        if (!IsWalkable(map, x, y))
            return ActionResult.Nothing($"({x}, {y}) is a wall or an obstacle");
        if (party.XSlot == 0 || party.YSlot == 0)
            return ActionResult.Nothing("party position is not readable");
        if (party.X < 0 || party.Y < 0 || party.X >= map.Width || party.Y >= map.Height)
            return ActionResult.Nothing($"the party reads as being at ({party.X}, {party.Y}), which is off this map");
        if (party.X == x && party.Y == y)
            return ActionResult.Nothing($"the party is already at ({x}, {y})");

        var from = _reader.ReadCell(map, party.X, party.Y);
        var to = _reader.ReadCell(map, x, y);
        if (from is null || to is null) return ActionResult.Nothing("could not read the tiles");

        int applied = 0;
        if (_reader.WriteCell(map, party.X, party.Y, (long)from.Value & ~GrimrockLayout.CellBits.DynamicObstacle)) applied++;
        if (_reader.WriteCell(map, x, y, (long)to.Value | GrimrockLayout.CellBits.DynamicObstacle)) applied++;
        if (_reader.Write(party.XSlot, x)) applied++;
        if (_reader.Write(party.YSlot, y)) applied++;

        return new ActionResult(applied, 4, $"party moved to ({x}, {y}) on {map.Name}");
    }

    /// <summary>Turns the party to face a compass direction (0 north, 1 east, 2 south, 3 west).</summary>
    public ActionResult SetFacing(PartySnapshot party, int facing)
    {
        if (party.FacingSlot == 0) return ActionResult.Nothing("facing is not readable");
        int f = ((facing % 4) + 4) % 4;
        bool ok = _reader.Write(party.FacingSlot, f);
        return new ActionResult(ok ? 1 : 0, 1, $"party faces {GameTables.FacingNames[f]}");
    }

    /// <summary>
    /// Fills in the automap for one level, tile by tile.
    ///
    /// Two things make this safe rather than a blanket OR. First, only the five automap bits are ever
    /// set: each tile is read, its geometry, occupancy and trigger bits are preserved, and the result
    /// is written back — so revealing a map cannot dissolve a wall or trip a pressure plate. Second,
    /// the four seen-wall bits are decided <i>per side</i> from the neighbouring tile's own
    /// <see cref="GrimrockLayout.CellBits.Wall"/> bit rather than all being set at once. Setting all
    /// four on open floor would claim walls the level does not have, and the automap would draw a box
    /// around every tile instead of the actual floor plan.
    ///
    /// A map whose dimensions do not describe a level LuaJIT could even hold is refused outright
    /// rather than swept: <c>width</c> and <c>height</c> come straight from the game and a torn read
    /// — a table rehashed between the header read and the field read — would otherwise be a run of
    /// syscalls on the UI thread with no way out. Grimrock's own levels are 32×32.
    /// </summary>
    public ActionResult RevealMap(MapSnapshot map)
    {
        if (!map.HasCells) return ActionResult.Nothing($"{map.Name} has no readable cell array");

        int width = map.Width, height = map.Height;
        if (!map.HasPlausibleSize)
            return ActionResult.Nothing(
                $"{map.Name} reads as {width}x{height}, which is not a level — refusing to sweep it");

        // Read the level once so neighbour lookups do not cost a syscall each, and so the wall bits
        // are decided against a single consistent view of the level rather than a moving one.
        var terrain = new long?[width, height];
        int readable = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var bits = _reader.ReadCell(map, x, y);
                if (bits is null) continue;
                terrain[x, y] = (long)bits.Value;
                readable++;
            }
        }

        if (readable == 0) return ActionResult.Nothing($"{map.Name}'s tiles could not be read");

        // Out-of-bounds counts as solid, so the level's outer edge is drawn as a wall.
        bool IsSolid(int x, int y) =>
            x < 0 || y < 0 || x >= width || y >= height ||
            terrain[x, y] is not { } t || (t & GrimrockLayout.CellBits.Wall) != 0;

        int applied = 0, attempted = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (terrain[x, y] is not { } mask) continue;
                if ((mask & GrimrockLayout.CellBits.Wall) != 0) continue;

                long seen = GrimrockLayout.CellBits.MapFloor;
                if (IsSolid(x, y - 1)) seen |= GrimrockLayout.CellBits.MapWallNorth;
                if (IsSolid(x + 1, y)) seen |= GrimrockLayout.CellBits.MapWallEast;
                if (IsSolid(x, y + 1)) seen |= GrimrockLayout.CellBits.MapWallSouth;
                if (IsSolid(x - 1, y)) seen |= GrimrockLayout.CellBits.MapWallWest;

                long updated = mask | seen;
                if (updated == mask) continue;
                attempted++;
                if (_reader.WriteCell(map, x, y, updated)) applied++;
            }
        }

        return attempted == 0
            ? ActionResult.Nothing($"{map.Name} is already mapped")
            : new ActionResult(applied, attempted, $"revealed {map.Name}");
    }
}
