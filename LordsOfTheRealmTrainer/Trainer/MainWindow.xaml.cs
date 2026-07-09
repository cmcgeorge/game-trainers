using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LordsTrainer;

public partial class MainWindow : Window
{
    private readonly DosBoxMemory _memory = new();
    private readonly LordsGame _game;
    private readonly Scanner _scanner;
    private readonly Scanner _goldScan;                                 // dedicated scan-based treasury finder
    private readonly Scanner _moveScan;                                 // behaviour-based unit-movement finder
    private WatchEntry? _goldEntry;                                     // pinned + frozen scanned treasury
    private uint? _economyBase;                                        // located player economy block (materials + armoury)
    private readonly ObservableCollection<WatchEntry> _lords = new();   // named cheats
    private readonly ObservableCollection<CountyEntry> _counties = new(); // per-county resources
    private readonly ObservableCollection<WatchEntry> _globals = new(); // kingdom-wide materials
    private readonly ObservableCollection<WatchEntry> _watch = new();   // scanner finds
    private readonly ObservableCollection<WatchEntry> _movement = new(); // frozen army move counters
    private readonly DispatcherTimer _tick = new();

    private const int MakeRichAmount = 999_999;

    // Unit-movement finder. An army's per-turn "moves remaining" counter lives in the
    // per-match unit heap (no fixed offset), so it's located by behaviour: it DROPS when
    // the army moves and RESETS UP when the turn ends. Observed per-turn maxima were 5 and
    // 12 across units of different speed, so we freeze a comfortably-higher value that
    // still fits a byte. An army stacks a few unit speeds, so 2–3 addresses legitimately
    // survive the narrowing together — freezing is allowed only once it's down to a handful.
    private const int MovementFreezeValue = 30;
    private const int MovementMaxFreezeCandidates = 6;
    private const uint MoveScanRangeEnd = LordsGame.ConventionalMemoryBytes;

    // Re-verify the guest base every this many ticks (~1s at 120 ms) so we notice
    // DOSBox-X remapping guest RAM before we write to a stale host address.
    private const int VerifyEveryTicks = 8;
    private int _ticksSinceVerify;

    // True while a scan runs on a background thread; gates attach/detach and scanning.
    private bool _scanBusy;

    public MainWindow()
    {
        InitializeComponent();
        _game = new LordsGame(_memory);
        _scanner = new Scanner(_memory);
        _goldScan = new Scanner(_memory);
        _moveScan = new Scanner(_memory);
        DgLords.ItemsSource = _lords;
        DgCounties.ItemsSource = _counties;
        DgGlobals.ItemsSource = _globals;
        DgWatch.ItemsSource = _watch;
        DgMovement.ItemsSource = _movement;

        _tick.Interval = TimeSpan.FromMilliseconds(120);
        _tick.Tick += OnTick;

        Closed += (_, _) => { _tick.Stop(); _memory.Dispose(); };
        UpdateAttachUi();
    }

    private ValueType SelectedType => RbInt16.IsChecked == true ? ValueType.Int16 : ValueType.Int32;

    // ---- Attach -------------------------------------------------------------

    private void OnAttachClick(object sender, RoutedEventArgs e)
    {
        var candidates = DosBoxMemory.FindCandidates();
        if (candidates.Length == 0)
        {
            SetStatus("No DOSBox-X process found. Start the game first.", error: true);
            return;
        }

        int pid = candidates[0].Id;
        if (candidates.Length > 1)
        {
            foreach (var p in candidates)
                if (p.Title.Contains("LORDS", StringComparison.OrdinalIgnoreCase)) { pid = p.Id; break; }
        }

        try
        {
            _memory.Attach(pid);
        }
        catch (AttachException ex)
        {
            SetStatus(ex.Message, error: true);
            UpdateAttachUi();
            return;
        }

        // Try to recognise the game's data structures. It's fine if this fails
        // (e.g. still on a loader/menu screen) — the scanner still works.
        bool located = _game.Locate();
        if (located && _game.IsTreasuryValid)
        {
            LoadLords();
            SetStatus($"Attached to PID {_memory.ProcessId}. Game detected — DGROUP at " +
                      $"segment 0x{_game.DGroupSegment:X4}, {_lords.Count} lord(s), you are Lord {_game.HumanIndex() + 1}.",
                      error: false);
        }
        else if (located)
        {
            SetStatus($"Attached to PID {_memory.ProcessId}. Game data found but the treasury layout " +
                      "didn't validate for this version — use the Advanced scanner for gold.", error: false);
        }
        else
        {
            SetStatus($"Attached to PID {_memory.ProcessId}, but no active match detected. " +
                      "Load a game, then press 'Detect lords'. (The scanner still works.)", error: false);
        }
        _ticksSinceVerify = 0;
        _tick.Start();
        UpdateAttachUi();
    }

    private void OnDetachClick(object sender, RoutedEventArgs e) => Detach("Detached.");

    private void Detach(string statusMessage)
    {
        _tick.Stop();
        _memory.Dispose();
        _scanner.Reset();
        _goldScan.Reset();
        _moveScan.Reset();
        _goldEntry = null;
        _economyBase = null;
        _lords.Clear();
        _counties.Clear();
        _globals.Clear();
        _movement.Clear();
        _watch.Clear();          // guest addresses are session-relative; never carry frozen
                                 // rows into a fresh attach or they'd poke the wrong memory
        LvResults.ItemsSource = null;
        SetStatus(statusMessage, error: true);
        UpdateScanInfo();
        UpdateAttachUi();
    }

    /// <summary>Called by the global exception handler: stop writing and detach for safety.</summary>
    internal void HandleFatalError()
    {
        try { Detach("Detached after an unexpected error. Re-attach when you're ready."); }
        catch { /* last-ditch: never throw out of the crash handler */ }
    }

    private void SetStatus(string text, bool error)
    {
        TxtStatus.Text = text;
        TxtStatus.Foreground = error
            ? System.Windows.Media.Brushes.IndianRed
            : (System.Windows.Media.Brush)Resources["Accent"];
    }

    private void UpdateAttachUi()
    {
        bool a = _memory.IsAttached;
        bool g = a && _game.Located && _game.IsTreasuryValid;
        bool idle = !_scanBusy;
        BtnAttach.IsEnabled = !a && idle;
        BtnDetach.IsEnabled = a && idle;
        BtnLoadLords.IsEnabled = a && idle;
        BtnFindGold.IsEnabled = a && idle;
        BtnGoldNewScan.IsEnabled = a && idle && _goldScan.HasScan;
        // "Set to 999,999" only lights up once the finder has narrowed to a single aligned
        // address, so it can never write 999,999 into an ambiguous (possibly non-treasury) match.
        BtnMakeRich.IsEnabled = a && idle && _goldScan.HasScan && _goldScan.AlignedSummary(4).Count == 1;
        BtnBankruptRivals.IsEnabled = g;
        // County resources: listing/maxing needs an attached process whose county-goods
        // layout validated (the same gate the anchor-county cheat used). The select/max
        // buttons additionally need a populated grid to act on.
        bool countiesReady = a && idle && _game.Located && _game.IsGoodsValid;
        bool haveCounties = countiesReady && _counties.Count > 0;
        BtnListCounties.IsEnabled = countiesReady;
        BtnSelectAllCounties.IsEnabled = haveCounties;
        BtnClearCountySelection.IsEnabled = haveCounties;
        BtnMaxSelectedCounties.IsEnabled = haveCounties;
        // Materials are found by their live values (a conventional-memory search), so the
        // finder only needs an attached process and an idle scan slot — not a validated
        // DGROUP layout — just like the treasury finder.
        BtnFindMaterials.IsEnabled = a && idle;
        // The armoury lives at fixed offsets inside the same block, so "Max weapons" only
        // lights up once "Find & max materials" has pinned that block this session.
        BtnMaxWeapons.IsEnabled = a && idle && _economyBase is not null;
        // Unit-movement finder: needs only an attached, idle process (a raw behaviour scan).
        // The narrowing buttons unlock once a snapshot exists; freezing only once it's down
        // to a handful of candidates.
        BtnMoveStart.IsEnabled = a && idle;
        BtnMoveMoved.IsEnabled = a && idle && _moveScan.HasScan;
        BtnMoveEndedTurn.IsEnabled = a && idle && _moveScan.HasScan;
        int moveCandidates = _moveScan.HasScan ? _moveScan.Count : 0;
        BtnFreezeMovement.IsEnabled = a && idle && moveCandidates is >= 1 and <= MovementMaxFreezeCandidates;
        BtnMoveNewScan.IsEnabled = a && idle && (_moveScan.HasScan || _movement.Count > 0);
        BtnFirstScan.IsEnabled = a && idle;
        BtnUnknownScan.IsEnabled = a && idle;
        BtnNewScan.IsEnabled = a && idle;
        UpdateNextButtons();
    }

    private void UpdateNextButtons()
    {
        bool ready = _memory.IsAttached && _scanner.HasScan && !_scanBusy;
        BtnNextExact.IsEnabled = ready;
        BtnInc.IsEnabled = ready;
        BtnDec.IsEnabled = ready;
        BtnUnch.IsEnabled = ready;
    }

    // ---- Cheats -------------------------------------------------------------

    private int _humanIndex;

    private void OnLoadLords(object sender, RoutedEventArgs e)
    {
        if (!_game.Located && !_game.Locate())
        {
            TxtCheatInfo.Text = "No active match found — load a game first.";
            return;
        }
        if (!_game.IsTreasuryValid)
        {
            TxtCheatInfo.Text = "Treasury layout didn't validate — use the Advanced scanner for gold.";
            return;
        }
        LoadLords();
        UpdateAttachUi();
    }

    private void LoadLords()
    {
        // Preserve any freeze the user armed (e.g. rivals held at 0) across a rebuild,
        // keyed by guest address. Addresses are stable while DGROUP is unchanged, so a
        // re-detect keeps holding the same slots instead of silently releasing them;
        // if DGROUP moved, the old addresses won't match and the stale freezes drop.
        var frozen = _lords.Where(l => l.Freeze).ToDictionary(l => l.Address, l => l.FreezeValue);
        _lords.Clear();
        _humanIndex = _game.HumanIndex();
        int count = _game.DetectLordCount();
        for (int i = 0; i < count; i++)
        {
            uint addr = _game.TreasuryAddress(i);
            long gold = _game.ReadTreasury(i);
            bool you = i == _humanIndex;
            bool wasFrozen = frozen.TryGetValue(addr, out long fv);
            _lords.Add(new WatchEntry
            {
                Address = addr,
                // The human's slot also mirrors the display cache so the on-screen
                // "crowns" number tracks whatever we set/freeze.
                MirrorAddress = you ? _game.CacheAddress : null,
                Type = ValueType.Int32,
                Value = gold,
                FreezeValue = wasFrozen ? fv : gold,
                Freeze = wasFrozen,
                Description = you ? $"Lord {i + 1} (you)" : $"Lord {i + 1} (rival)"
            });
        }
        // Keep the county grid (if the user has already listed it) anchored to the current
        // DGROUP, preserving each row's freeze/selection by index — the same rebuild-after-
        // reload path the goods table used. If it's empty the user hasn't opened it, so the
        // reads are skipped entirely.
        if (_counties.Count > 0) LoadCounties();
        // The materials/armoury block is a per-match heap allocation pinned by a value
        // scan, not a DGROUP offset that re-anchors here. A re-detect means the match may
        // have been reloaded and that cached base has gone stale, so drop it and its
        // frozen rows rather than keep enforcing them against a possibly-freed address.
        // The user re-runs "Find & max materials" for the current match — the same
        // re-establish-after-reload flow the treasury finder and goods table already use.
        _economyBase = null;
        _globals.Clear();
        TxtCheatInfo.Text = $"{_lords.Count} lord(s). Auto-picked Lord {_humanIndex + 1} as you — verify its gold " +
                            "matches your on-screen crowns; if not, open the in-game treasury screen and press 'Detect lords' again.";
    }

    // ---- County resources ---------------------------------------------------
    //
    // Every county keeps its own grain/cattle/sheep/wool in a per-county record (stride
    // 0x168). The grid lists every live county (see LordsGame.AllCounties); the user ticks
    // the ones to cheat and "Max selected counties" fills — and freezes — their four goods,
    // so any subset of provinces can be maxed at once, not just the anchor or the one open.

    /// <summary>Lists (or refreshes) every live county and its four goods.</summary>
    private void OnListCounties(object sender, RoutedEventArgs e)
    {
        if (!_game.Located && !_game.Locate())
        {
            TxtCheatInfo.Text = "No active match found — load a game first.";
            return;
        }
        if (!_game.IsGoodsValid)
        {
            TxtCheatInfo.Text = "County goods layout didn't validate for this version — use the Advanced scanner.";
            return;
        }
        LoadCounties();
        UpdateAttachUi();
    }

    /// <summary>
    /// (Re)builds the county grid from the current DGROUP anchor, preserving each row's
    /// freeze/selection by county index so a rebuild after a re-detect keeps holding the same
    /// provinces instead of releasing them. County addresses are stable while DGROUP is
    /// unchanged; if the game was reloaded, re-anchoring here refreshes them to the new match.
    /// </summary>
    private void LoadCounties()
    {
        var frozen = _counties.Where(c => c.Freeze).Select(c => c.Index).ToHashSet();
        var selected = _counties.Where(c => c.Selected).Select(c => c.Index).ToHashSet();
        _counties.Clear();
        foreach (var county in _game.AllCounties())
        {
            var entry = new CountyEntry
            {
                Index = county.Index,
                IsViewed = county.IsViewed,
                Goods = county.Goods,
                Freeze = frozen.Contains(county.Index),
                Selected = selected.Contains(county.Index),
            };
            for (int i = 0; i < county.Goods.Count; i++)
                entry.SetLive(i, county.Goods[i].Value);
            _counties.Add(entry);
        }
        TxtCheatInfo.Text = _counties.Count == 0
            ? "No counties found yet — open a match with owned provinces, then list again."
            : $"Listed {_counties.Count} county record(s). Tick the ones to cheat, then 'Max selected counties'.";
    }

    /// <summary>Ticks every listed county for the next bulk max.</summary>
    private void OnSelectAllCounties(object sender, RoutedEventArgs e)
    {
        foreach (var c in _counties) c.Selected = true;
    }

    /// <summary>Clears every county's tick.</summary>
    private void OnClearCountySelection(object sender, RoutedEventArgs e)
    {
        foreach (var c in _counties) c.Selected = false;
    }

    /// <summary>
    /// Fills grain, cattle, sheep and wool to their maxima for every ticked county and freezes
    /// them, so the provinces stay stocked instead of the game eating the goods back over the
    /// coming seasons. Freezing mirrors the old anchor-county cheat; the live tick re-writes any
    /// frozen good that drifts.
    /// </summary>
    private void OnMaxSelectedCounties(object sender, RoutedEventArgs e)
    {
        var chosen = _counties.Where(c => c.Selected).ToList();
        if (chosen.Count == 0)
        {
            TxtCheatInfo.Text = "Tick the counties you want to max first (or 'Select all').";
            return;
        }
        foreach (var c in chosen)
        {
            for (int i = 0; i < c.Goods.Count; i++)
            {
                var g = c.Goods[i];
                _memory.WriteInt16(g.Address, (short)g.CheatTo);
                c.SetLive(i, g.CheatTo);
            }
            c.Freeze = true;   // hold the county stocked
        }
        TxtCheatInfo.Text = $"Maxed and froze grain, cattle, sheep & wool for {chosen.Count} county(ies). " +
                            "Re-open a county screen to see it.";
    }

    /// <summary>
    /// Refreshes each listed county's live goods and re-enforces any that are frozen. Reads are
    /// skipped entirely when the grid is empty (the user hasn't listed counties), so this costs
    /// nothing until the feature is used.
    /// </summary>
    private void RefreshCounties()
    {
        foreach (var c in _counties)
        {
            var goods = c.Goods;
            for (int i = 0; i < goods.Count; i++)
            {
                var g = goods[i];
                bool ok = _memory.TryReadInt16(g.Address, out short cur);
                // Re-write a frozen good only when it has drifted (or couldn't be confirmed),
                // saving a write in the common already-held case — mirrors EnforceAndRefresh.
                if (c.Freeze && (!ok || cur != g.CheatTo))
                {
                    _memory.WriteInt16(g.Address, (short)g.CheatTo);
                    cur = (short)g.CheatTo;
                    ok = true;
                }
                if (ok) c.SetLive(i, cur);
            }
        }
    }

    /// <summary>
    /// Finds the kingdom-wide economy block by the Iron/Stone/Wood amounts the user has
    /// on screen, then maxes and freezes the three materials. Like the treasury, the
    /// block lives in a per-match heap allocation whose address changes each game, so
    /// it's located by value rather than a fixed offset — the doubled signature of the
    /// three amounts is unique in memory. The pinned base is cached so "Max weapons" can
    /// reach the armoury (at fixed offsets in the same block) without re-searching — which
    /// matters because maxing the materials overwrites the very values used to find them.
    /// Each material is written to both its authoritative slot and its mirror copy.
    /// </summary>
    private async void OnFindMaterials(object sender, RoutedEventArgs e)
    {
        if (_scanBusy) return;
        if (!_memory.IsAttached) { TxtCheatInfo.Text = "Attach first."; return; }
        // Values are stored as Int16, so anything above short.MaxValue can't be a real
        // amount and would overflow the signature bytes (e.g. 40000 -> -25536) and never
        // match — reject it up front rather than silently searching for the wrong value.
        if (!int.TryParse(TxtIron.Text.Trim(), out int iron) ||
            !int.TryParse(TxtStone.Text.Trim(), out int stone) ||
            !int.TryParse(TxtWood.Text.Trim(), out int wood) ||
            iron < 0 || stone < 0 || wood < 0 ||
            iron > short.MaxValue || stone > short.MaxValue || wood > short.MaxValue)
        {
            TxtCheatInfo.Text = "Enter your current Iron, Stone and Wood (whole numbers 0–32,767) exactly as shown in-game.";
            return;
        }

        // Run the 640 KB conventional-memory scan off the UI thread (like the treasury
        // finder) so the window stays responsive; _scanBusy gates attach/scan controls.
        _scanBusy = true;
        UpdateAttachUi();
        uint? found;
        try
        {
            found = await Task.Run(() => _game.FindEconomyBlock(iron, stone, wood));
        }
        finally
        {
            _scanBusy = false;
        }

        if (found is null)
        {
            _economyBase = null;
            TxtCheatInfo.Text = "Couldn't pin your resources from those amounts. Make sure Iron/Stone/Wood " +
                                "match your goods screen exactly; if it stays ambiguous, change one in-game and try again.";
            UpdateAttachUi();
            return;
        }
        _economyBase = found;

        // Rebuild the materials rows (drop any previous material rows, keep armoury rows).
        RemoveGlobals(m => m.DisplayScale == 1);
        foreach (var m in _game.MaterialsAt(found.Value))
        {
            var row = new WatchEntry
            {
                Address = m.Address,
                MirrorAddress = m.MirrorAddress,   // the doubled copy, kept in sync
                Type = ValueType.Int16,
                Value = m.Value,
                FreezeValue = m.CheatTo,
                Freeze = true,                     // hold them stocked, like the goods cheats
                Description = m.Name
            };
            _globals.Add(row);
            Poke(row, row.FreezeValue);
        }
        TxtCheatInfo.Text = $"Maxed and froze Iron, Stone and Wood at {LordsGame.MaterialCheatTo:N0}. " +
                            "Now press 'Max weapons' for the armoury. Re-open your goods screen to see it.";
        UpdateAttachUi();
    }

    /// <summary>
    /// Fills the armoury — swords, axes, crossbows, spears, maces, long bows and armor —
    /// to a high count and freezes them, using the economy block pinned by "Find &amp; max
    /// materials". Weapons are stored in batches of 50 (in-game count = stored × 50), so
    /// the rows show and freeze the real in-game numbers via WatchEntry.DisplayScale. The
    /// weapons-total field is also refreshed so the overview stays consistent.
    /// </summary>
    private void OnMaxWeapons(object sender, RoutedEventArgs e)
    {
        if (_economyBase is not uint b)
        {
            TxtCheatInfo.Text = "Press 'Find & max materials' first — that pins the block the armoury lives in.";
            return;
        }

        var armoury = _game.WeaponsAt(b);
        RemoveGlobals(m => m.DisplayScale != 1);   // replace any previous armoury rows
        foreach (var w in armoury.Weapons)
        {
            var row = new WatchEntry
            {
                Address = w.Address,
                Type = ValueType.Int16,
                DisplayScale = w.Scale,   // 50 — grid shows the in-game count, not the batch
                Value = w.Value,
                FreezeValue = w.CheatTo,
                Freeze = true,
                Description = $"{w.Name} (armoury)"
            };
            _globals.Add(row);
            Poke(row, row.FreezeValue);
        }
        // Keep the total field in step with the maxed batches (one-time, not frozen — the
        // game recomputes it when weapons are next bought/used).
        _memory.WriteInt16(armoury.TotalAddress, (short)armoury.TotalCheatTo);
        int shown = LordsGame.WeaponCheatTo * LordsGame.WeaponScale;
        TxtCheatInfo.Text = $"Maxed and froze all seven armoury weapons at {shown:N0} each. " +
                            "Re-open the armoury/weapons screen to see it.";
    }

    /// <summary>Removes matching global rows (unfreezing them by dropping them from the
    /// enforced set), so a re-run replaces its own rows without disturbing the others.</summary>
    private void RemoveGlobals(Func<WatchEntry, bool> predicate)
    {
        foreach (var row in _globals.Where(predicate).ToList())
            _globals.Remove(row);
    }

    private void OnSetSelectedGlobal(object sender, RoutedEventArgs e)
    {
        if (DgGlobals.SelectedItem is WatchEntry mat && EnsureFits(mat, TxtCheatInfo))
            Poke(mat, mat.FreezeValue);
    }

    /// <summary>
    /// Re-anchors DGROUP and rebuilds the lord table when the game has been reloaded
    /// (DGROUP moved) or no table exists yet, so the treasury cheats always target the
    /// current game instead of a snapshot cached at attach time. Restarting the game
    /// inside the same DOSBox-X keeps the guest RAM base intact, so VerifyStillAttached
    /// never fires — without this refresh the cheats would keep poking stale addresses.
    /// Returns false (after setting a status message) if there's no valid treasury layout
    /// right now (e.g. you're on a loader/menu screen).
    /// </summary>
    private bool RefreshLords()
    {
        if (!_game.Locate())
        {
            TxtCheatInfo.Text = "No active game found — load a game first.";
            return false;
        }
        if (!_game.IsTreasuryValid)
        {
            TxtCheatInfo.Text = "Treasury layout didn't validate — use the Advanced scanner for gold.";
            return false;
        }
        // Always re-anchor and re-detect the human from the *current* display cache, so a
        // cheat clicked after the game state changed (new game, income collected, a
        // different lord in the human slot) targets the right lord instead of a snapshot
        // cached earlier. LoadLords preserves any freezes the user armed, so re-detecting
        // here doesn't release held rivals.
        LoadLords();
        UpdateAttachUi();
        return true;
    }

    /// <summary>Upper end of the conventional-memory window the treasury finder scans.</summary>
    private const uint GoldScanRangeEnd = LordsGame.ConventionalMemoryBytes;

    /// <summary>
    /// Scan-based treasury finder: the reliable identifier of the human's gold is the
    /// number on screen, since the fixed DGROUP offsets are re-allocated (and go stale)
    /// on every game reload. First click snapshots every address equal to the entered
    /// crowns; each later click narrows the survivors to the new amount. Once a single
    /// aligned address remains, "Set to 999,999" is unlocked. The scan runs off the UI
    /// thread (a 640 KB byte-granular pass) so the window stays responsive.
    /// </summary>
    private async void OnFindGold(object sender, RoutedEventArgs e)
    {
        if (_scanBusy) return;
        if (!_memory.IsAttached) { TxtCheatInfo.Text = "Attach first."; return; }
        if (!int.TryParse(TxtGoldNow.Text.Trim(), out int v) || v < 0)
        {
            TxtCheatInfo.Text = "Enter your current crowns (a whole number) exactly as shown on the treasury screen.";
            return;
        }

        bool first = !_goldScan.HasScan;
        _scanBusy = true;
        UpdateAttachUi();
        try
        {
            await Task.Run(() =>
            {
                if (first) _goldScan.FirstScan(v, ValueType.Int32, 0, GoldScanRangeEnd);
                else _goldScan.NextScanExact(v);
            });
        }
        finally
        {
            _scanBusy = false;
        }

        var (count, addr) = _goldScan.AlignedSummary(4);
        if (count == 0)
        {
            _goldScan.Reset();   // dead end — start a fresh search on the next click
            TxtCheatInfo.Text = $"Nothing holds {v:N0}. Make sure it matches your on-screen crowns exactly, then Find again.";
        }
        else if (count == 1)
        {
            TxtCheatInfo.Text = $"Pinned your treasury at 0x{addr:X5}. Click 'Set to 999,999'.";
        }
        else
        {
            TxtCheatInfo.Text = $"{count} possible matches. Change your gold in-game (advance a season or buy/sell), " +
                                "type the new amount, and Find again to narrow it.";
        }
        UpdateAttachUi();
    }

    /// <summary>
    /// Clears the treasury search (and releases the pinned/frozen row). The scanned address
    /// is only valid for the match it was found in: an in-game restart re-allocates the
    /// treasury without moving DGROUP or the guest RAM base, so nothing can auto-detect the
    /// staleness — the user presses this after restarting/loading a game, then Finds again.
    /// </summary>
    private void OnGoldNewScan(object sender, RoutedEventArgs e)
    {
        _goldScan.Reset();
        if (_goldEntry is not null) { _watch.Remove(_goldEntry); _goldEntry = null; }
        TxtCheatInfo.Text = "Cleared the treasury search. Type your current crowns and Find again " +
                            "(do this after restarting or loading a game).";
        UpdateAttachUi();
    }

    /// <summary>Freeze/unfreeze the found treasury from the visible Cheats row, so the user
    /// doesn't have to open the Advanced watch list to release it.</summary>
    private void OnFreezeGoldToggled(object sender, RoutedEventArgs e)
    {
        if (_goldEntry is null) return;
        _goldEntry.Freeze = ChkFreezeGold.IsChecked == true;
        if (_goldEntry.Freeze) Poke(_goldEntry, _goldEntry.FreezeValue);
    }

    private void OnMakeRich(object sender, RoutedEventArgs e)
    {
        var (count, addr) = _goldScan.HasScan ? _goldScan.AlignedSummary(4) : (0, 0u);
        if (count != 1)
        {
            TxtCheatInfo.Text = "Narrow to a single address first with 'Find my treasury'.";
            return;
        }

        // Reuse a single watch row for the found treasury. Freezing (the default) holds it at
        // the target so seasonal income / the game writing a display copy back can't erode it;
        // the visible 'Freeze' checkbox lets the user turn that off without the Advanced panel.
        if (_goldEntry is null || _goldEntry.Address != addr)
        {
            if (_goldEntry is not null) _watch.Remove(_goldEntry);
            _goldEntry = new WatchEntry
            {
                Address = addr, Type = ValueType.Int32,
                Value = MakeRichAmount, Description = "Your treasury (scanned)"
            };
            _watch.Add(_goldEntry);
        }
        bool freeze = ChkFreezeGold.IsChecked == true;
        _goldEntry.FreezeValue = MakeRichAmount;
        _goldEntry.Freeze = freeze;
        Poke(_goldEntry, MakeRichAmount);
        TxtCheatInfo.Text = freeze
            ? $"Set your treasury (0x{addr:X5}) to {MakeRichAmount:N0} and froze it. Re-open the treasury screen to see it."
            : $"Set your treasury (0x{addr:X5}) to {MakeRichAmount:N0}. Re-open the treasury screen to see it.";
    }

    private void OnBankruptRivals(object sender, RoutedEventArgs e)
    {
        if (!RefreshLords()) return;
        int n = 0;
        foreach (var lord in _lords)
        {
            if (lord.Address == _game.TreasuryAddress(_humanIndex)) continue;   // never bankrupt yourself
            lord.FreezeValue = 0;
            lord.Freeze = true;      // hold them at zero
            Poke(lord, 0);
            n++;
        }
        TxtCheatInfo.Text = n > 0 ? $"Bankrupted and froze {n} rival lord(s) at 0 gold."
                                  : "No rival lords to bankrupt.";
    }

    private void OnSetSelectedLord(object sender, RoutedEventArgs e)
    {
        if (DgLords.SelectedItem is WatchEntry lord && EnsureFits(lord, TxtCheatInfo))
            Poke(lord, lord.FreezeValue);
    }

    // ---- Unit movement ------------------------------------------------------
    //
    // An army's per-turn "moves remaining" counter can't be a fixed offset — it lives in
    // the per-match unit heap that the game re-allocates each game (confirmed by a 4-dump
    // differential: a 12-space move exhausted it 3→0, a 1-space move on a fresh turn ran
    // it 5→4). So it's found the way the treasury/economy are: by behaviour. The counter
    // DROPS when the army moves and RESETS UP when the turn ends — a fingerprint almost
    // nothing else in memory shares — so the flow is snapshot → narrow-on-move → narrow-on-
    // turn-end → freeze. Freezing it high stops the army ever running out of moves.

    /// <summary>Snapshots every aligned Int16 in conventional memory as the movement
    /// finder's baseline. Restart-safe: a fresh Start after a reload re-establishes it.</summary>
    private async void OnMovementStart(object sender, RoutedEventArgs e)
    {
        if (_scanBusy) return;
        if (!_memory.IsAttached) { TxtMoveInfo.Text = "Attach first."; return; }
        await RunMoveScan(() => _moveScan.FirstScanUnknown(ValueType.Int16, 0, MoveScanRangeEnd));
        TxtMoveInfo.Text = $"Snapshot of {_moveScan.Count:N0} values. Move the army one space in-game, then click 'I moved'.";
    }

    /// <summary>Narrows to values that DROPPED — the fingerprint of spending a move.</summary>
    private async void OnMovementMoved(object sender, RoutedEventArgs e)
    {
        if (_scanBusy || !_moveScan.HasScan) return;
        await RunMoveScan(() => _moveScan.NextScan(ScanCompare.Decreased));
        ReportMovementNarrowed("dropped");
    }

    /// <summary>Narrows to values that ROSE — the fingerprint of a per-turn reset, which is
    /// what separates the move counter from anything that merely drifts downward.</summary>
    private async void OnMovementEndedTurn(object sender, RoutedEventArgs e)
    {
        if (_scanBusy || !_moveScan.HasScan) return;
        await RunMoveScan(() => _moveScan.NextScan(ScanCompare.Increased));
        ReportMovementNarrowed("reset");
    }

    private void ReportMovementNarrowed(string how)
    {
        int n = _moveScan.Count;
        TxtMoveInfo.Text = n == 0
            ? "Nothing matched — the counter was likely missed. Click 'New search' and start again."
            : n <= MovementMaxFreezeCandidates
                ? $"{n} candidate(s) after '{how}'. Click 'Freeze movement', or narrow once more to be sure."
                : $"{n:N0} candidates after '{how}'. Move again / end another turn and narrow until only a few remain.";
        UpdateAttachUi();
    }

    /// <summary>
    /// Freezes every surviving candidate at a high move count. An army stacks units of
    /// different speeds, so a few addresses survive together; holding them all keeps the
    /// whole army moving. Rows go into the movement table and are re-written each tick.
    /// </summary>
    private void OnFreezeMovement(object sender, RoutedEventArgs e)
    {
        int n = _moveScan.HasScan ? _moveScan.Count : 0;
        if (n is < 1 or > MovementMaxFreezeCandidates)
        {
            TxtMoveInfo.Text = "Narrow to a few candidates first: move the army ('I moved'), then end the turn ('I ended turn').";
            return;
        }
        _movement.Clear();
        int i = 1;
        foreach (var r in _moveScan.Snapshot(MovementMaxFreezeCandidates))
        {
            var row = new WatchEntry
            {
                Address = r.Address, Type = ValueType.Int16,
                Value = r.Value, FreezeValue = MovementFreezeValue, Freeze = true,
                Description = $"Move slot {i++} ({r.AddressHex})"
            };
            _movement.Add(row);
            Poke(row, MovementFreezeValue);
        }
        TxtMoveInfo.Text = $"Froze {_movement.Count} move slot(s) at {MovementFreezeValue}. The army can now move freely each turn. " +
                           "Untick a row (or 'New search') to release.";
        UpdateAttachUi();
    }

    /// <summary>Clears the movement search and releases any frozen move slots. Guest
    /// addresses are per-match, so do this after loading/restarting a game before starting
    /// again — the same re-establish-after-reload flow the other finders use.</summary>
    private void OnMovementNewScan(object sender, RoutedEventArgs e)
    {
        _moveScan.Reset();
        _movement.Clear();
        TxtMoveInfo.Text = "Cleared the movement search. Select the army to free, then click 'Start'.";
        UpdateAttachUi();
    }

    private void OnSetSelectedMovement(object sender, RoutedEventArgs e)
    {
        if (DgMovement.SelectedItem is WatchEntry w && EnsureFits(w, TxtMoveInfo))
            Poke(w, w.FreezeValue);
    }

    // Runs a movement scan off the UI thread (like RunScan, but for the movement finder,
    // which has its own status text rather than the shared scanner results list).
    private async Task RunMoveScan(Action scan)
    {
        _scanBusy = true;
        UpdateAttachUi();
        try { await Task.Run(scan); }
        finally { _scanBusy = false; UpdateAttachUi(); }
    }

    // ---- Scanner ------------------------------------------------------------

    private async void OnFirstScan(object sender, RoutedEventArgs e)
    {
        if (_scanBusy) return;
        if (!TryGetValue(out long v)) return;
        if (!TryGetRange(out uint start, out uint end)) return;
        var type = SelectedType;   // read UI state on the UI thread, before backgrounding
        await RunScan(() => _scanner.FirstScan(v, type, start, end));
    }

    private async void OnUnknownScan(object sender, RoutedEventArgs e)
    {
        if (_scanBusy) return;
        if (!TryGetRange(out uint start, out uint end)) return;
        var type = SelectedType;
        await RunScan(() => _scanner.FirstScanUnknown(type, start, end));
    }

    private void OnNewScan(object sender, RoutedEventArgs e)
    {
        if (_scanBusy) return;
        _scanner.Reset();
        LvResults.ItemsSource = null;
        UpdateScanInfo();
        UpdateNextButtons();
    }

    private async void OnNextExact(object sender, RoutedEventArgs e)
    {
        if (_scanBusy) return;
        if (!TryGetValue(out long v)) return;
        await RunScan(() => _scanner.NextScanExact(v));
    }

    private async void OnNextInc(object sender, RoutedEventArgs e)
    { if (!_scanBusy) await RunScan(() => _scanner.NextScan(ScanCompare.Increased)); }
    private async void OnNextDec(object sender, RoutedEventArgs e)
    { if (!_scanBusy) await RunScan(() => _scanner.NextScan(ScanCompare.Decreased)); }
    private async void OnNextUnch(object sender, RoutedEventArgs e)
    { if (!_scanBusy) await RunScan(() => _scanner.NextScan(ScanCompare.Unchanged)); }

    // Runs a (potentially up-to-1 MB) scan off the UI thread so the window stays
    // responsive, disabling attach/detach/scan controls while it runs.
    private async Task RunScan(Action scan)
    {
        _scanBusy = true;
        UpdateAttachUi();
        try
        {
            await Task.Run(scan);
        }
        finally
        {
            _scanBusy = false;
            RefreshResults();
            UpdateAttachUi();
        }
    }

    private void RefreshResults()
    {
        LvResults.ItemsSource = _scanner.Count <= Scanner.MaxDisplayResults ? _scanner.Snapshot() : null;
        UpdateScanInfo();
        UpdateNextButtons();
    }

    private void UpdateScanInfo()
    {
        if (!_scanner.HasScan) { TxtScanInfo.Text = "No scan yet."; return; }
        TxtScanInfo.Text = _scanner.Count switch
        {
            0 => "No matches. Try New Scan.",
            > Scanner.MaxDisplayResults => $"{_scanner.Count:N0} candidates — narrow further before the list shows.",
            _ => $"{_scanner.Count:N0} candidate(s)."
        };
    }

    private void OnResultDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (LvResults.SelectedItem is not ScanResult r) return;
        if (_watch.Any(w => w.Address == r.Address && w.Type == _scanner.Type)) return;
        _watch.Add(new WatchEntry
        {
            Address = r.Address, Type = _scanner.Type, Value = r.Value,
            FreezeValue = r.Value, Description = "value"
        });
    }

    private void OnRemoveWatch(object sender, RoutedEventArgs e)
    {
        if (DgWatch.SelectedItem is WatchEntry w) _watch.Remove(w);
    }

    private void OnClearWatch(object sender, RoutedEventArgs e) => _watch.Clear();

    private void OnSetSelected(object sender, RoutedEventArgs e)
    {
        if (DgWatch.SelectedItem is WatchEntry w && EnsureFits(w, TxtScanInfo))
            Poke(w, w.FreezeValue);
    }

    // ---- Live tick ----------------------------------------------------------

    private void OnTick(object? sender, EventArgs e)
    {
        if (!_memory.IsAttached) { _tick.Stop(); UpdateAttachUi(); return; }
        if (!_memory.IsProcessAlive())
        {
            Detach("DOSBox-X has exited — detached. Re-attach when the game is running again.");
            return;
        }
        if (++_ticksSinceVerify >= VerifyEveryTicks)
        {
            _ticksSinceVerify = 0;
            if (!_memory.VerifyStillAttached())
            {
                Detach("Lost the emulated PC's RAM (DOSBox-X may have reset or been reconfigured). Re-attach.");
                return;
            }
        }
        EnforceAndRefresh(_lords);
        RefreshCounties();
        EnforceAndRefresh(_globals);
        EnforceAndRefresh(_movement);
        EnforceAndRefresh(_watch);
    }

    private void EnforceAndRefresh(IEnumerable<WatchEntry> entries)
    {
        foreach (var w in entries)
        {
            bool ok = _memory.TryReadValue(w.Address, w.Type, out long cur);
            // Re-write a frozen entry only when it has drifted (or we couldn't confirm it),
            // saving a write syscall + allocation each tick in the common already-held case.
            if (w.Freeze && (!ok || cur != w.FreezeValue)) Poke(w, w.FreezeValue);
            if (ok && cur != w.Value) w.Value = cur;
        }
    }

    private void Poke(WatchEntry w, long value)
    {
        if (w.Type == ValueType.Int32)
        {
            int v = (int)Math.Clamp(value, int.MinValue, int.MaxValue);
            _memory.WriteInt32(w.Address, v);
            if (w.MirrorAddress is uint m) _memory.WriteInt32(m, v);
        }
        else
        {
            short v = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
            _memory.WriteInt16(w.Address, v);
            if (w.MirrorAddress is uint m) _memory.WriteInt16(m, v);
        }
    }

    // ---- Parsing helpers ----------------------------------------------------

    private static bool FitsType(long value, ValueType type) =>
        type == ValueType.Int32
            ? value is >= int.MinValue and <= int.MaxValue
            : value is >= short.MinValue and <= short.MaxValue;

    // Warns (rather than silently wrapping) when the user's "Set to" value can't fit the
    // target width — e.g. 40000 into an Int16 good would otherwise become -25536.
    private static bool EnsureFits(WatchEntry w, TextBlock status)
    {
        if (FitsType(w.FreezeValue, w.Type)) return true;
        status.Text = $"{w.FreezeValue:N0} doesn't fit a {w.TypeLabel} value — enter a smaller number.";
        return false;
    }

    private bool TryGetValue(out long value)
    {
        if (!TryParseSigned(TxtValue.Text, out value))
        {
            TxtScanInfo.Text = $"'{TxtValue.Text.Trim()}' is not a valid number.";
            return false;
        }
        value = SelectedType == ValueType.Int16 ? unchecked((short)value) : unchecked((int)value);
        return true;
    }

    private bool TryGetRange(out uint start, out uint end)
    {
        start = 0; end = 0;
        if (!TryParseUInt(TxtRangeStart.Text, out start) || !TryParseUInt(TxtRangeEnd.Text, out end))
        {
            TxtScanInfo.Text = "Range must be numbers (hex like 0xA0000 or decimal).";
            return false;
        }
        if (end <= start || end - start > 0x100000)
        {
            TxtScanInfo.Text = "Range must be increasing and at most 1 MB wide.";
            return false;
        }
        long guest = _memory.GuestSize;
        if (guest > 0 && end > guest)
        {
            TxtScanInfo.Text = $"Range must stay within the emulated RAM (0x0–0x{guest:X}).";
            return false;
        }
        return true;
    }

    private static bool TryParseSigned(string text, out long value)
    {
        text = text.Trim();
        return HasHexPrefix(text)
            ? long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
            : long.TryParse(text, NumberStyles.Integer | NumberStyles.AllowLeadingSign,
                            CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseUInt(string text, out uint value)
    {
        text = text.Trim();
        return HasHexPrefix(text)
            ? uint.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
            : uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool HasHexPrefix(string text) =>
        text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
}
