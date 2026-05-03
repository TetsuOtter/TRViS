using System.ComponentModel;

using TRViS.IO.Models;

namespace TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Provides access to application-level business state.
/// </summary>
public interface IAppViewModelProvider
{
    /// <summary>
    /// The currently selected work group.
    /// </summary>
    WorkGroup? SelectedWorkGroup { get; }

    /// <summary>
    /// The currently selected work.
    /// </summary>
    Work? SelectedWork { get; }

    /// <summary>
    /// The currently selected train data.
    /// </summary>
    TrainData? SelectedTrainData { get; }

    /// <summary>
    /// The current application theme.
    /// Setting this writes the value back to the underlying view model.
    /// </summary>
    AppTheme CurrentAppTheme { get; set; }

    /// <summary>
    /// Whether the background app icon is visible.
    /// </summary>
    bool IsBgAppIconVisible { get; set; }

    /// <summary>
    /// Fired when any property changes.
    /// </summary>
    event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Fired when the current app theme changes.
    /// </summary>
    event EventHandler<AppTheme>? CurrentAppThemeChanged;
}
