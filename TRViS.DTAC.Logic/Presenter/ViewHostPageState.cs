namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Represents the UI-oriented state of the ViewHost page.
/// This POCO is produced by ViewHostPresenter and consumed by ViewHost.xaml.cs.
/// </summary>
public class ViewHostPageState
{
    /// <summary>
    /// The page title text (SelectedWork.Name).
    /// </summary>
    public string TitleText { get; set; } = string.Empty;

    /// <summary>
    /// The formatted time label text.
    /// </summary>
    public string TimeLabelText { get; set; } = "00:00:00";

    /// <summary>
    /// Whether the background app icon is currently visible.
    /// Used by View to update button appearance.
    /// </summary>
    public bool IsBgAppIconVisible { get; set; } = true;
}
