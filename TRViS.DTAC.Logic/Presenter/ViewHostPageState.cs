using TRViS.DTAC.Logic.Abstractions;

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
    /// The workspace name (SelectedWorkGroup.Name).
    /// </summary>
    public string WorkSpaceName { get; set; } = string.Empty;

    /// <summary>
    /// The formatted affect date string.
    /// </summary>
    public string AffectDateText { get; set; } = string.Empty;

    /// <summary>
    /// The formatted time label text.
    /// </summary>
    public string TimeLabelText { get; set; } = "00:00:00";

    /// <summary>
    /// Whether the Hako tab content is visible.
    /// </summary>
    public bool IsHakoVisible { get; set; }

    /// <summary>
    /// Whether the Timetable (VerticalStylePage) tab content is visible.
    /// </summary>
    public bool IsTimetableVisible { get; set; }

    /// <summary>
    /// Whether the WorkAffix tab content is visible.
    /// </summary>
    public bool IsWorkAffixVisible { get; set; }

    /// <summary>
    /// The desired screen orientation. Logic emits this; View applies it via OS API.
    /// </summary>
    public DesiredOrientation DesiredOrientation { get; set; } = DesiredOrientation.All;

    /// <summary>
    /// Whether the background app icon is currently visible.
    /// Used by View to update button appearance.
    /// </summary>
    public bool IsBgAppIconVisible { get; set; } = true;
}
