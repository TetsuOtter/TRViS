namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Represents the UI-oriented state of the Hako page.
/// This POCO is produced by HakoPresenter and consumed by Hako.xaml.cs.
/// </summary>
public class HakoPageState
{
	/// <summary>
	/// The formatted affect-date label text (includes prefix).
	/// </summary>
	public string AffectDateText { get; set; } = string.Empty;

	/// <summary>
	/// The formatted work-info label text (workName + newline + workSpaceName).
	/// </summary>
	public string WorkInfoText { get; set; } = string.Empty;
}
