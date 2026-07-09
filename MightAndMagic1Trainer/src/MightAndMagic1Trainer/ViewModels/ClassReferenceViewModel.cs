using System.Collections.ObjectModel;
using MightAndMagic1Trainer.Game;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>One class row in the Classes tab: the reference data the view binds to.</summary>
public sealed class ClassEntryViewModel
{
    private GameClass Class { get; }

    public ClassEntryViewModel(GameClass gameClass) => Class = gameClass;

    /// <summary>"1 · Knight" — the record id (offset 0x14) plus the class name.</summary>
    public string Title => $"{Class.Id} · {Class.Name}";
    public string PrimeStats => Class.PrimeStatsText;
    public string Requirement => Class.RequirementText;
    public string HitPointsPerLevel => Class.HitPointsPerLevel;
    public string Spells => Class.SpellText;
    public string Description => Class.Description;

    /// <summary>One-line summary beneath the requirement: prime stats and HP-per-level.</summary>
    public string MetaLine => $"Prime stats: {PrimeStats}   ·   HP per level: {HitPointsPerLevel}";
}

/// <summary>
/// Backs the Classes tab: the six classes (minimum prime-stat requirements, HP-per-level,
/// spell school, blurb) plus the shared experience-per-level table. Reference-only, like
/// <see cref="SpellReferenceViewModel"/> and <see cref="MapReferenceViewModel"/> — needs no
/// attached game.
/// </summary>
public sealed class ClassReferenceViewModel
{
    public ObservableCollection<ClassEntryViewModel> Classes { get; }
    public ObservableCollection<ExperienceStep> ExperienceTable { get; }

    /// <summary>The 12-point minimum each prime statistic must meet (shown in the tab's note).</summary>
    public int MinPrimeValue => ClassBook.MinPrimeValue;

    public ClassReferenceViewModel()
    {
        Classes = new ObservableCollection<ClassEntryViewModel>(
            ClassBook.Classes.Select(c => new ClassEntryViewModel(c)));
        ExperienceTable = new ObservableCollection<ExperienceStep>(ClassBook.ExperienceTable);
    }
}
