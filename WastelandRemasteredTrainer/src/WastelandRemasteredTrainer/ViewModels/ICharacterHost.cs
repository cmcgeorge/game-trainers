namespace WastelandRemasteredTrainer.ViewModels;

/// <summary>Host callback for character view models to report messages and status.</summary>
public interface ICharacterHost
{
    /// <summary>Shows a message in the status line.</summary>
    void OnMessage(string message);

    /// <summary>Re-reads the selected character, after a change that reshapes a list.</summary>
    void RefreshSelected();
}
