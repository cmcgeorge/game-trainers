using System.Collections.ObjectModel;
using BardsTale1Trainer.Game;

namespace BardsTale1Trainer.ViewModels;

/// <summary>Read-only class &amp; race reference shown in the 🛡 Classes tab.</summary>
public sealed class ClassReferenceViewModel
{
    public ObservableCollection<ClassInfo> Classes { get; } = new(ClassBook.Classes);
    public ObservableCollection<RaceInfo> Races { get; } = new(ClassBook.Races);
}
