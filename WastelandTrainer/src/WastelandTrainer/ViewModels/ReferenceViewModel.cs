using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using WastelandTrainer.Game;

namespace WastelandTrainer.ViewModels;

/// <summary>
/// Backs the References tab's read-only sub-tabs (Attributes, Skills, Items, Paragraphs, Strategy).
/// Attributes, skills, items and the strategy notes are static reference tables from the <c>Game/</c>
/// layer (items grouped by category). Paragraphs are loaded at runtime from the game's own <c>paragraphs.txt</c>
/// — the trainer never embeds the copyrighted booklet text. Drives no memory writes.
/// </summary>
public sealed class ReferenceViewModel : ObservableObject
{
    public IReadOnlyList<AttributeInfo> Attributes => AttributeBook.Attributes;
    public IReadOnlyList<SkillInfo> Skills => SkillBook.Skills;
    public IReadOnlyList<WalkthroughSection> Strategy => Walkthrough.Sections;
    public ICollectionView Items { get; }

    public ObservableCollection<ParagraphEntry> Paragraphs { get; } = new();

    private string _paragraphStatus = "Load the game folder (containing paragraphs.txt) to list the paragraph book.";
    public string ParagraphStatus { get => _paragraphStatus; private set => SetField(ref _paragraphStatus, value); }

    public ICommand LoadParagraphsCommand { get; }

    public ReferenceViewModel()
    {
        // Exclude the id-0 "(empty)" sentinel from the reference list — it only belongs in the
        // inventory-editor drop-down, and its blank Category would render an empty-titled group.
        Items = new CollectionViewSource { Source = ItemCatalog.Items.Where(i => i.Id != 0).ToList() }.View;
        Items.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ItemInfo.Category)));

        LoadParagraphsCommand = new RelayCommand(_ => PickAndLoadParagraphs());
        TryAutoLoadParagraphs();
    }

    private void PickAndLoadParagraphs()
    {
        var dlg = new OpenFolderDialog { Title = "Select the Wasteland game folder (containing paragraphs.txt)" };
        if (dlg.ShowDialog() != true) return;
        LoadParagraphsFrom(dlg.FolderName);
    }

    private bool LoadParagraphsFrom(string folder)
    {
        bool ok = ParagraphBook.TryLoadFromFolder(folder, out var entries, out var status);
        Paragraphs.Clear();
        foreach (var e in entries) Paragraphs.Add(e);
        ParagraphStatus = status;
        return ok;
    }

    /// <summary>Walks up from the app directory looking for a sibling <c>.game</c> folder.</summary>
    private void TryAutoLoadParagraphs()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, ".game");
            if (Directory.Exists(candidate) && LoadParagraphsFrom(candidate)) return;
        }
    }
}
