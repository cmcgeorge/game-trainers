using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using WastelandTrainer.Game;
using WastelandTrainer.Memory;

namespace WastelandTrainer.ViewModels;

/// <summary>
/// Backs the Save Editor tab: opens a Wasteland GAME1/GAME2 file, decodes its savegame block, and lets
/// the user edit the party's position (the one true teleport — live-memory teleport is impossible, see
/// <see cref="MapBook"/> / RE notes §5), the in-game clock, and every occupied character record (reusing
/// the same <see cref="CharacterViewModel"/> the live Party tab uses). Saving re-encrypts just that block
/// back into a copy of the file, bumps the serial so the game loads the edited file next, and leaves a
/// one-time <c>.bak</c> of the original.
///
/// <para>Implements <see cref="ICharacterHost"/> so a <see cref="CharacterViewModel"/> writes its edits
/// straight into the decoded payload buffer instead of a live process: <see cref="IsAttached"/> is true
/// whenever a save is loaded, and <see cref="WriteBytes"/> copies the changed bytes into the payload at
/// the record's payload offset (which the view-model passes as its "address").</para>
/// </summary>
public sealed class SaveEditorViewModel : ObservableObject, ICharacterHost
{
    private SaveGame? _save;

    public ObservableCollection<CharacterViewModel> Characters { get; } = new();

    public IReadOnlyList<TeleportTarget> TeleportTargets => MapBook.TeleportTargets;

    private CharacterViewModel? _selectedCharacter;
    public CharacterViewModel? SelectedCharacter
    {
        get => _selectedCharacter;
        set => SetField(ref _selectedCharacter, value);
    }

    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ApplyTeleportCommand { get; }

    public SaveEditorViewModel()
    {
        OpenCommand = new RelayCommand(_ => PickAndOpen());
        SaveCommand = new RelayCommand(_ => SaveToDisk(), _ => IsLoaded);
        ApplyTeleportCommand = new RelayCommand(t => ApplyTeleport(t as TeleportTarget), _ => IsLoaded);
        TryAutoOpen();
    }

    // --- state ---------------------------------------------------------------
    public bool IsLoaded => _save != null;

    private string _status =
        "Open a Wasteland GAME1 or GAME2 save file to edit the party's position, the clock, and every ranger.";
    public string Status { get => _status; private set => SetField(ref _status, value); }

    /// <summary>Full path of the loaded file (shown in the header), or an empty string.</summary>
    public string LoadedPath => _save?.Path ?? "";

    // --- header / position editor -------------------------------------------
    public int PartyX
    {
        get => _save?.Header.PartyX ?? 0;
        set { if (_save == null) return; _save.Header.PartyX = value; RaisePosition(); }
    }

    public int PartyY
    {
        get => _save?.Header.PartyY ?? 0;
        set { if (_save == null) return; _save.Header.PartyY = value; RaisePosition(); }
    }

    public int MapId
    {
        get => _save?.Header.MapId ?? 0;
        set { if (_save == null) return; _save.Header.MapId = value; RaisePosition(); }
    }

    public int Hour
    {
        get => _save?.Header.Hour ?? 0;
        set { if (_save == null) return; _save.Header.Hour = value; OnPropertyChanged(); OnPropertyChanged(nameof(HeaderSummary)); }
    }

    public int Minute
    {
        get => _save?.Header.Minute ?? 0;
        set { if (_save == null) return; _save.Header.Minute = value; OnPropertyChanged(); OnPropertyChanged(nameof(HeaderSummary)); }
    }

    public long Serial => _save?.Header.Serial ?? 0;

    public string HeaderSummary => _save == null
        ? ""
        : $"map {_save.Header.MapId}  ·  ({_save.Header.PartyX},{_save.Header.PartyY})  ·  "
          + $"{_save.Header.Hour:00}:{_save.Header.Minute:00}  ·  members {_save.Header.CurrentMembers}  ·  serial {_save.Header.Serial}";

    // --- open ----------------------------------------------------------------
    private void PickAndOpen()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open a Wasteland save file (GAME1 or GAME2)",
            Filter = "Wasteland saves (game1;game2)|game1;game2|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog() != true) return;
        Open(dlg.FileName);
    }

    private void Open(string path)
    {
        try
        {
            _save = SaveGame.Load(path);
        }
        catch (Exception ex)
        {
            _save = null;
            RebuildCharacters();
            RaiseAll();
            Status = $"Could not open {Path.GetFileName(path)}: {ex.Message}";
            return;
        }

        RebuildCharacters();
        RaiseAll();
        var names = Characters.Select(c => c.Record.Name);
        Status = $"Loaded {_save.Tag} from {_save.Path} — {Characters.Count} ranger(s): {string.Join(", ", names)}. "
               + "Edit, then Save to write it back (the game loads the higher-serial file).";
    }

    /// <summary>On start-up, if the shipped <c>.game</c> folder is beside the app, quietly open whichever
    /// of GAME1/GAME2 has the higher save serial (the one the game itself would load next).</summary>
    private void TryAutoOpen()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            string gameDir = Path.Combine(dir.FullName, ".game");
            if (!Directory.Exists(gameDir)) continue;

            SaveGame? best = null;
            foreach (var name in new[] { "game1", "game2" })
            {
                string path = Path.Combine(gameDir, name);
                if (!File.Exists(path)) continue;
                try
                {
                    var sg = SaveGame.Load(path);
                    if (best == null || sg.Header.Serial > best.Header.Serial) best = sg;
                }
                catch { /* not a decodable save; ignore */ }
            }
            if (best == null) continue;

            _save = best;
            RebuildCharacters();
            RaiseAll();
            Status = $"Auto-loaded {_save.Tag} from {_save.Path} (highest serial). "
                   + "Edit, then Save to write it back.";
            return;
        }
    }

    private void RebuildCharacters()
    {
        Characters.Clear();
        if (_save != null)
        {
            for (int slot = 0; slot < CharacterFormat.MaxSlots; slot++)
            {
                var rec = _save.Characters[slot];
                if (!rec.IsOccupied) continue;
                var located = new LocatedCharacter((nuint)SaveFormat.CharacterOffset(slot), slot, rec);
                Characters.Add(new CharacterViewModel(this, located));
            }
        }
        SelectedCharacter = Characters.FirstOrDefault();
    }

    // --- teleport ------------------------------------------------------------
    private void ApplyTeleport(TeleportTarget? target)
    {
        if (_save == null || target == null) return;
        _save.Header.SetPosition(target.X, target.Y, target.MapId);
        RaisePosition();
        Status = $"Party set to {target.Name} — map {target.MapId} at ({target.X},{target.Y}). Save to apply.";
    }

    // --- save ----------------------------------------------------------------
    private void SaveToDisk()
    {
        if (_save == null) return;
        try
        {
            _save.Save();
            OnPropertyChanged(nameof(Serial));
            OnPropertyChanged(nameof(HeaderSummary));
            Status = $"Saved {_save.Path} (serial now {_save.Header.Serial}). "
                   + "The original was backed up once as .bak. Load this save in-game to see the changes.";
        }
        catch (Exception ex)
        {
            Status = $"Save failed: {ex.Message}";
        }
    }

    // --- ICharacterHost ------------------------------------------------------
    /// <summary>A character view-model is "attached" whenever a save is open — its edits flow into the
    /// decoded payload rather than a live process.</summary>
    public bool IsAttached => _save != null;

    /// <summary>Copies an edited range of a character record straight into the decoded payload buffer at
    /// the record's payload offset (passed as <paramref name="recordAddress"/>), so the change is baked in
    /// when the block is re-encoded on save.</summary>
    public bool WriteBytes(nuint recordAddress, byte[] source, int offset, int length)
    {
        if (_save == null) return false;
        int dest = (int)recordAddress + offset;
        if (dest < 0 || offset < 0 || length < 0 || dest + length > _save.Payload.Length || offset + length > source.Length)
            return false;
        Array.Copy(source, offset, _save.Payload, dest, length);
        return true;
    }

    // --- notification helpers ------------------------------------------------
    private void RaisePosition()
    {
        OnPropertyChanged(nameof(PartyX));
        OnPropertyChanged(nameof(PartyY));
        OnPropertyChanged(nameof(MapId));
        OnPropertyChanged(nameof(HeaderSummary));
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(IsLoaded));
        OnPropertyChanged(nameof(LoadedPath));
        OnPropertyChanged(nameof(PartyX));
        OnPropertyChanged(nameof(PartyY));
        OnPropertyChanged(nameof(MapId));
        OnPropertyChanged(nameof(Hour));
        OnPropertyChanged(nameof(Minute));
        OnPropertyChanged(nameof(Serial));
        OnPropertyChanged(nameof(HeaderSummary));
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ApplyTeleportCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
