using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Space1889Trainer.Game;
using Space1889Trainer.Memory;
using Space1889Trainer.ViewModels;

namespace Space1889Trainer.FormatCheck;

/// <summary>An <see cref="IStateHost"/> over a synthetic block that records what the editors do.</summary>
internal sealed class FakeHost : IStateHost
{
    public FakeHost(IStateTarget target)
    {
        State = new GameState(target);
        State.Refresh();
    }

    public GameState? State { get; }
    public bool CanEdit { get; set; } = true;
    public bool SuppressWriteBack { get; set; }
    public List<string> Reports { get; } = new();
    public int AfterWrites { get; private set; }

    public void Report(string message) => Reports.Add(message);
    public void AfterWrite() => AfterWrites++;
}

internal static partial class Program
{
    private static void CheckViewModels()
    {
        Section("character editor over a fake host");
        var target = new RecordingTarget(Synthetic.Block());
        var host = new FakeHost(target);
        var vm = new CharacterViewModel(host, 0);
        vm.Reload();
        CheckEqual(0, target.Writes.Count, "loading writes nothing");
        CheckEqual("PROF WELLS", vm.Name, "name loaded");
        CheckEqual("HEALTH: 8/4", vm.HealthText, "health line as the game prints it");
        CheckEqual("£100 0s 0d", vm.WealthText, "money text");
        CheckEqual(5, vm.Attributes[0].Value, "STR loaded");
        CheckEqual(3, vm.Skills[3].Value, "Trimsman loaded");
        Check(vm.CanEdit, "editable");

        vm.Wealth = 12345;
        CheckEqual((StateFormat.WealthOffset, 4), target.Writes.LastOrDefault(), "wealth edit writes exactly its four bytes");
        CheckEqual(12345u, host.State!.GetUInt32(0), "wealth stored");
        Check(host.AfterWrites > 0, "AfterWrite raised (freezes re-seed from it)");

        int before = target.Writes.Count;
        vm.Skills[12].Value = 6;
        CheckEqual((StateFormat.SkillsOffset + 12, 1), target.Writes.LastOrDefault(), "skill edit writes one byte");
        vm.Skills[12].Value = 6;
        CheckEqual(before + 1, target.Writes.Count, "setting the same value again writes nothing");

        vm.Name = "wells";
        CheckEqual("WELLS", new CharacterRecord(host.State, 0).Name, "name edit upper-cased and stored");
        vm.Name = "  ";
        CheckEqual("WELLS", new CharacterRecord(host.State, 0).Name, "an empty name is refused and reloaded");

        host.SuppressWriteBack = true;
        before = target.Writes.Count;
        vm.Health = 1;
        CheckEqual(before, target.Writes.Count, "no write while the host repopulates");
        host.SuppressWriteBack = false;
        vm.Reload();

        vm.NewItemId = 17;
        vm.NewItemRounds = 40;
        vm.AddItemCommand.Execute(null);
        CheckEqual(17, vm.Inventory[0].ItemId, "Add item appends");
        CheckEqual(40, vm.Inventory[0].Rounds, "with the requested rounds");
        vm.Inventory[0].UseCommand.Execute(null);
        CheckEqual("in hand", vm.Inventory[0].Status, "Equip puts it in hand");
        vm.Inventory[0].InGun = 2;
        CheckEqual(2, new CharacterRecord(host.State, 0).GetEntry(0).InGun, "IN GUN edit");
        vm.Inventory[0].RemoveCommand.Execute(null);
        Check(!vm.Inventory[0].IsOccupied, "Remove empties the slot");
        CheckEqual(0, new CharacterRecord(host.State, 0).WeaponSlot, "and clears the weapon slot");

        vm.MaxEverythingCommand.Execute(null);
        CheckEqual(6, vm.Attributes[5].Value, "max everything reloads the editor");

        vm.Attributes[1].Value = 0;
        CheckEqual(1, vm.Attributes[1].Value, "an attribute row clamps to 1, as the record stores it");
        CheckEqual(1, new CharacterRecord(host.State, 0).GetAttribute(1), "and 1 is what was written");
        vm.Skills[1].Value = 0;
        CheckEqual(0, vm.Skills[1].Value, "a skill row can be 0");
        vm.Attributes[0].Value = 1;
        vm.Attributes[2].Value = 1;
        CheckEqual($"HEALTH: {vm.Health}/{new CharacterRecord(host.State, 0).UnconsciousThreshold}", vm.HealthText,
                   "editing STR or END refreshes the HEALTH line's threshold");

        vm.NewItemId = 17;
        vm.NewItemRounds = 20;
        vm.AddItemCommand.Execute(null);
        vm.Inventory[0].UseCommand.Execute(null);
        vm.Inventory[0].ItemId = ItemBook.Lantern;
        CheckEqual(new InventoryEntry(ItemBook.Lantern, 0, 0, 0), new CharacterRecord(host.State, 0).GetEntry(0),
                   "changing a row's item goes through ReplaceItem (ammunition bytes cleared)");
        Check(vm.Inventory[0].Status != "in hand" && new CharacterRecord(host.State, 0).WeaponSlot == 0, "and a lantern is not left in hand");
        vm.Inventory[0].InUse = true;
        CheckEqual("in use", vm.Inventory[0].Status, "ticking IN USE refreshes the row's status");

        var enablement = new FakeHost(new BufferTarget(Synthetic.Block()));
        var watched = new CharacterViewModel(enablement, 0);
        watched.Reload();
        var raised = new List<string?>();
        watched.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        enablement.CanEdit = false;
        watched.Reload();
        Check(!watched.CanEdit && raised.Contains(nameof(CharacterViewModel.CanEdit)), "a host losing CanEdit is announced on reload");

        var drifting = new BufferTarget(Synthetic.Block());
        var driftHost = new FakeHost(drifting);
        var drifter = new CharacterViewModel(driftHost, 0);
        drifter.Reload();
        drifter.FullHealCommand.Execute(null);
        drifting.Bytes[StateFormat.RecordOffset(0) + StateFormat.HealthOffset] = 2;   // the game wounds him after the last poll
        drifter.FullHealCommand.Execute(null);
        CheckEqual((byte)9, drifting.Bytes[StateFormat.RecordOffset(0) + StateFormat.HealthOffset],
                   "a quick action re-reads first, so a heal is not skipped against a stale snapshot");
        drifter.Attributes[5].Value = 6;
        CheckEqual("Aristocracy", drifter.SocialClass, "a SOC edit refreshes the social class");

        var empty = new FakeHost(new BufferTarget(Synthetic.Block()));
        empty.State!.SetBytes(StateFormat.RecordOffset(4) + StateFormat.NameOffset, new byte[12]);
        var ghost = new CharacterViewModel(empty, 4);
        ghost.Reload();
        Check(ghost.IsEmpty && !ghost.CanEdit && !ghost.HasCharacter, "an empty slot is not editable and its panels are hidden");
        Check(ghost.Title.Contains("empty"), "and says so");

        var refusing = new BufferTarget(Synthetic.Block());
        var refusingHost = new FakeHost(refusing);
        var stubborn = new CharacterViewModel(refusingHost, 0);
        stubborn.Reload();
        refusing.RefuseWrites = true;
        stubborn.Wealth = 1;
        CheckEqual(24000L, stubborn.Wealth, "a refused edit snaps back to the stored value");
        Check(refusingHost.Reports.Any(r => r.Contains("refused")), "and reports it");

        Section("world editor over a fake host");
        var wt = new RecordingTarget(Synthetic.Block());
        var whost = new FakeHost(wt);
        var world = new WorldViewModel(whost);
        world.Reload();
        CheckEqual(0, wt.Writes.Count, "loading writes nothing");
        CheckEqual("PROF WELLS", world.Leader, "leader from the marching order");
        CheckEqual(29, world.Row, "teleport box starts at the party's row");
        world.Food = 70000;
        CheckEqual(StateFormat.MaxFood, (int)whost.State!.GetUInt16(StateFormat.FoodOffset), "food clamps");
        world.Row = 5;
        world.Column = 6;
        world.TeleportCommand.Execute(null);
        CheckEqual((5, 6), (new WorldRecord(whost.State).Row, new WorldRecord(whost.State).Column), "teleport command");
        world.TeleportCheck = (_, row, _) => row > 40 ? "a wall" : null;
        world.Row = 45;
        world.TeleportCommand.Execute(null);
        CheckEqual(5, new WorldRecord(whost.State).Row, "a square the map check rejects is not written");
        Check(whost.Reports.LastOrDefault()?.Contains("Teleport refused: a wall") == true, "and the reason is reported");
        world.StoryFlags[3].IsSet = true;
        Check(new WorldRecord(whost.State).HasStoryFlag(3), "story-flag checkbox");
        Check(world.CanEdit, "world editor enabled while the host can edit");
        whost.State.SetByte(StateFormat.HasFlyerOffset, 1);
        world.Reload();
        world.Hull = 17;
        Check(world.FlyerSummary.Contains("hull 17"), "a flyer edit refreshes the summary");
        whost.CanEdit = false;
        world.Reload();
        Check(!world.CanEdit, "world editor disabled when the host cannot edit");
        world.StoryFlags[4].IsSet = true;
        Check(!new WorldRecord(whost.State).HasStoryFlag(4), "nothing is written while it cannot edit");
        whost.CanEdit = true;
        world.Reload();
        world.TopGun = WorldViewModel.GunChoices[3].Index;
        CheckEqual(WorldViewModel.GunChoices[3].Index, new WorldRecord(whost.State).GetFlyer(StateFormat.FlyerTopGun), "gun mount");
        CheckEqual(18, WorldViewModel.GunChoices.Count, "none + 17 guns");
    }

    private static void CheckSaveEditor()
    {
        Section("save editor (unsaved changes are not discarded silently)");
        string dir = TempDirectory();
        try
        {
            string first = Path.Combine(dir, "FIRST.SAV"), second = Path.Combine(dir, "SECOND.SAV");
            File.WriteAllBytes(first, Synthetic.Block());
            File.WriteAllBytes(second, Synthetic.Block());
            using var main = new MainViewModel();
            var editor = main.SaveEditor;
            editor.SelectedFile = first;
            editor.OpenCommand.Execute(null);
            Check(editor.HasSave && editor.LoadedName == "FIRST.SAV", "opened");
            editor.Characters[0].Reload();
            editor.Characters[0].Wealth = 1;
            Check(editor.LoadedName.Contains("unsaved"), "an edit marks the save dirty");

            editor.SelectedFile = second;
            editor.OpenCommand.Execute(null);
            Check(editor.LoadedName.StartsWith("FIRST.SAV") && editor.Status.Contains("unsaved changes"), "the first Open only warns");
            editor.OpenCommand.Execute(null);
            CheckEqual("SECOND.SAV", editor.LoadedName, "a second Open discards and opens");

            editor.Characters[0].Wealth = 2;
            editor.RevertCommand.Execute(null);
            CheckEqual(24000L, editor.Characters[0].Wealth, "Revert reloads the copy on disk without asking twice");

            editor.SelectedFile = Path.Combine(dir, "MISSING.SAV");
            editor.OpenCommand.Execute(null);
            CheckEqual("SECOND.SAV", editor.LoadedName, "a failed open leaves the current save open");

            editor.Characters[0].Wealth = 3;
            File.SetLastWriteTimeUtc(second, DateTime.UtcNow.AddMinutes(5));   // the game saves over it meanwhile
            editor.SaveCommand.Execute(null);
            Check(editor.Status.Contains("changed on disk") && WealthOnDisk(second) == 24000, "the first Save over a newer file only warns");
            editor.SaveCommand.Execute(null);
            CheckEqual(3L, WealthOnDisk(second), "a second Save overwrites it");

            main.GameFolder = "";
            var probe = new GameState(new BufferTarget(Synthetic.Block()));
            probe.Refresh();
            Check(main.Maps.CheckTeleport(probe, 500, 3)?.Contains("beyond the largest map") == true, "no game folder: a square past every map is refused");
            Check(main.Maps.CheckTeleport(probe, 10, 3)?.Contains("game folder is not set") == true, "no game folder: other squares need the override");
            main.Maps.AllowUnsafeTeleport = true;
            Check(main.Maps.CheckTeleport(probe, 10, 3) is null, "which allows them");
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
    }

    private static long WealthOnDisk(string path)
    {
        var state = new GameState(new BufferTarget(File.ReadAllBytes(path)));
        state.Refresh();
        return new CharacterRecord(state, 0).Wealth;
    }

    /// <summary>Collects WPF data-binding errors (a mistyped path is otherwise only a line in the debugger output).</summary>
    private sealed class BindingErrorListener : TraceListener
    {
        public List<string> Errors { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message) { if (!string.IsNullOrEmpty(message)) Errors.Add(message); }
    }

    private static void CheckWindow()
    {
        Section("XAML smoke test (the real window)");
        var listener = new BindingErrorListener();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        try
        {
            if (Application.Current is null) new App().InitializeComponent();
            var window = new MainWindow();
            var main = (MainViewModel)window.DataContext;
            main.SelectedCharacter = main.Characters[0];
            main.SaveEditor.SelectedCharacter = main.SaveEditor.Characters[0];
            window.Show();
            var tabs = FindTabControls(window).FirstOrDefault();
            Check(tabs is not null, "tab control found");
            if (tabs is not null)
            {
                CheckEqual(6, tabs.Items.Count, "six tabs");
                var seen = new HashSet<TabControl>();
                int visited = 0;
                void Walk(TabControl tc)
                {
                    if (!seen.Add(tc)) return;
                    for (int i = 0; i < tc.Items.Count; i++)
                    {
                        tc.SelectedIndex = i;
                        window.UpdateLayout();
                        visited++;
                        foreach (var inner in FindTabControls(tc).Where(t => t != tc && !seen.Contains(t)).ToList()) Walk(inner);
                    }
                }
                Walk(tabs);
                CheckEqual(12, visited, "every tab and nested tab laid out (6 top-level, 6 nested)");
            }
            window.Close();
            CheckEqual(0, listener.Errors.Count, "no data-binding errors" + (listener.Errors.Count > 0 ? ": " + listener.Errors[0] : ""));
        }
        catch (Exception ex)
        {
            Check(false, "window: " + ex.GetType().Name + ": " + ex.Message);
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
        }
    }

    private static IEnumerable<TabControl> FindTabControls(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TabControl tc) yield return tc;
            foreach (var hit in FindTabControls(child)) yield return hit;
        }
    }

    private static void CheckLive()
    {
        Section("live (read-only) against a running DOSBox");
        var emulators = GameLocator.FindEmulators();
        if (emulators.Count == 0) { Console.WriteLine("    no DOSBox running - skipped"); return; }
        int located = 0;
        foreach (var p in emulators)
        {
            using (p)
            {
                ProcessMemory mem;
                try { mem = ProcessMemory.Open(p.Id); }
                catch (Exception ex) { Console.WriteLine($"    {p.ProcessName} {p.Id}: cannot open ({ex.Message}) - skipped"); continue; }
                using (mem)
                {
                    var found = GameLocator.Find(new ProcessMemorySource(mem), out string status);
                    Console.WriteLine($"    {p.ProcessName} {p.Id}: {status}");
                    if (found is null) continue;
                    located++;
                    Check(found.ElapsedMilliseconds < 5000, "locate under five seconds");
                    Check(found.Witness != PointerWitness.None, "a running program vouches for the block");
                    using var target = new LiveStateTarget(mem, found.BlockHost);
                    Check(target.IsAvailable, "the emulator process is seen as running");
                    var state = new GameState(target);
                    Check(state.Refresh() && state.LooksValid(), "live block reads and validates");
                    var world = new WorldRecord(state);
                    Check(world.Planet is >= 0 and <= 10, $"planet {world.Planet}");
                    int people = Enumerable.Range(0, 5).Count(i => !new CharacterRecord(state, i).IsEmpty);
                    Check(people > 0, $"{people} character(s): " + string.Join(", ", Enumerable.Range(0, 5)
                        .Select(i => new CharacterRecord(state, i)).Where(r => !r.IsEmpty).Select(r => r.Name)));
                    Console.WriteLine($"    {world.Describe}, day {world.Day}, row {world.Row}, column {world.Column}, food {world.Food}");
                }
            }
        }
        // --live was asked for and an emulator is running: not finding the game there is a failure, not a skip.
        Check(located > 0, $"Space 1889 located in one of the {emulators.Count} running emulator(s)");
    }
}
