using System.Windows.Input;
using MoriaTrainer.Game;

namespace MoriaTrainer.ViewModels;

/// <summary>
/// Backs the Reference tab: read-only references for the eight races, six classes, and 62 spells
/// (31 mage + 31 priest). Pure data — no live attach needed. Derived from the manual and the
/// <c>FEATURES.NEW</c> skill-gain table (Confirmed). The <see cref="CopySpellLetterCommand"/> copies a
/// spell's letter to the clipboard so it can be pasted into the in-game spell prompt.
/// </summary>
public sealed class ReferenceViewModel : ObservableObject
{
    public IReadOnlyList<RaceInfo> Races => RaceBook.Races;
    public IReadOnlyList<ClassInfo> Classes => ClassBook.Classes;
    public IReadOnlyList<SpellInfo> MageSpells => SpellBook.MageSpells;
    public IReadOnlyList<SpellInfo> PriestPrayers => SpellBook.PriestPrayers;

    public ICommand CopySpellLetterCommand { get; }

    public ReferenceViewModel()
    {
        // Copies a spell row's single-letter key to the clipboard so the user can paste it at the
        // in-game spell prompt (UMoria prompts "Enter spell letter:" then expects the letter + target).
        CopySpellLetterCommand = new RelayCommand(p =>
        {
            if (p is SpellInfo s)
            {
                try { System.Windows.Clipboard.SetText(s.Letter); } catch { /* best effort */ }
            }
        });
    }
}
