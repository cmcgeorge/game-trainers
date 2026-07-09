using System.Collections.ObjectModel;
using MightAndMagic1Trainer.Game;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>One numbered step in a walkthrough section ("1.  Do the thing").</summary>
public sealed class WalkthroughStepViewModel
{
    public WalkthroughStepViewModel(int number, string text)
    {
        Number = $"{number}.";
        Text = text;
    }

    public string Number { get; }
    public string Text { get; }
}

/// <summary>One titled section of the walkthrough plus its numbered steps.</summary>
public sealed class WalkthroughSectionViewModel
{
    public WalkthroughSectionViewModel(WalkthroughSection section)
    {
        Title = section.Title;
        Steps = new ObservableCollection<WalkthroughStepViewModel>(
            section.Steps.Select((s, i) => new WalkthroughStepViewModel(i + 1, s)));
    }

    public string Title { get; }
    public ObservableCollection<WalkthroughStepViewModel> Steps { get; }
}

/// <summary>
/// Backs the Walkthrough tab: the full sectioned solution guide as read-only reference data
/// (independent of any attached game, like <see cref="MapReferenceViewModel"/>).
/// </summary>
public sealed class WalkthroughViewModel
{
    public ObservableCollection<WalkthroughSectionViewModel> Sections { get; }

    public WalkthroughViewModel() =>
        Sections = new ObservableCollection<WalkthroughSectionViewModel>(
            Walkthrough.Sections.Select(s => new WalkthroughSectionViewModel(s)));
}
