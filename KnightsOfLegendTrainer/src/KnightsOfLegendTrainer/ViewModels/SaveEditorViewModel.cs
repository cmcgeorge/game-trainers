using System.IO;

namespace KnightsOfLegendTrainer.ViewModels;

/// <summary>
/// One row in the save editor's quest-status grid: the quest name and its current
/// status code (0-3), editable via a dropdown.
/// </summary>
public sealed class QuestStatusViewModel : ObservableObject
{
    private readonly SaveEditorViewModel _owner;
    public int Index { get; }
    public string QuestName { get; }

    private int _status;
    public int Status
    {
        get => _status;
        set
        {
            if (SetField(ref _status, value))
                _owner.OnQuestChanged(Index, value);
        }
    }

    public string StatusLabel => SaveFormat.StatusLabels[_status];

    public IReadOnlyList<string> StatusOptions => SaveFormat.StatusLabels;

    public QuestStatusViewModel(SaveEditorViewModel owner, int index, string questName, int status)
    {
        _owner = owner;
        Index = index;
        QuestName = questName;
        _status = status;
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusLabel));
    }
}

/// <summary>
/// View-model for the chardata save editor. Loads a chardata file, displays the 24
/// quest statuses, and writes changes back. A one-shot .bak backup is taken before
/// the first write. [Manual]
/// </summary>
public sealed class SaveEditorViewModel : ObservableObject
{
    private byte[]? _data;
    private string? _filePath;
    private bool _backupTaken;
    private bool _dirty;

    public ObservableCollection<QuestStatusViewModel> Quests { get; } = new();

    public bool HasFile => _data != null;

    public string FilePath => _filePath ?? "(no file loaded)";

    public bool Dirty { get => _dirty; private set => SetField(ref _dirty, value); }

    private string _statusText = "Load a chardata file to edit quest statuses.";
    public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }

    public IReadOnlyList<string> StatusLabels => SaveFormat.StatusLabels;

    public void Load(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        if (!SaveFormat.IsValidChardata(data))
        {
            _data = null;
            _filePath = null;
            _backupTaken = false;
            Dirty = false;
            Quests.Clear();
            StatusText = $"File too small ({data.Length} bytes) — chardata must be at least " +
                         $"{SaveFormat.QuestStatusOffset + SaveFormat.QuestStatusLength} bytes.";
            OnPropertyChanged(nameof(HasFile));
            OnPropertyChanged(nameof(FilePath));
            return;
        }

        _data = data;
        _filePath = path;
        _backupTaken = false;
        Dirty = false;

        Quests.Clear();
        for (int i = 0; i < SaveFormat.QuestCount; i++)
        {
            var quest = QuestBook.ById(i);
            int status = SaveFormat.ReadQuestStatus(_data, i);
            Quests.Add(new QuestStatusViewModel(this, i, quest?.Name ?? $"Quest {i + 1}", status));
        }

        StatusText = $"Loaded {Path.GetFileName(path)} ({_data.Length} bytes, {Quests.Count} quests).";
        OnPropertyChanged(nameof(HasFile));
        OnPropertyChanged(nameof(FilePath));
    }

    internal void OnQuestChanged(int questIndex, int newStatus)
    {
        if (_data == null) return;
        SaveFormat.WriteQuestStatus(_data, questIndex, newStatus);
        Quests[questIndex].Refresh();
        Dirty = true;
    }

    public void Save()
    {
        if (_data == null || _filePath == null) return;
        try
        {
            if (!_backupTaken)
            {
                string bak = _filePath + ".bak";
                if (!File.Exists(bak))
                    File.Copy(_filePath, bak);
                _backupTaken = true;
            }

            File.WriteAllBytes(_filePath, _data);
            Dirty = false;
            StatusText = $"Saved to {Path.GetFileName(_filePath)}.";
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed: {ex.Message}";
        }
    }

    public void SetAllQuests(int status)
    {
        if (_data == null) return;
        for (int i = 0; i < SaveFormat.QuestCount; i++)
        {
            Quests[i].Status = status;
        }
        Dirty = true;
        StatusText = $"All quests set to {SaveFormat.StatusLabels[status]}.";
    }
}
