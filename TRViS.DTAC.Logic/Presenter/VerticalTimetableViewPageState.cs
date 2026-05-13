using TRViS.DTAC.Logic.Formatters;

namespace TRViS.DTAC.Logic.Presenter;

/// <summary>
/// Display state for the current-location marker.
/// </summary>
public class LocationMarkerDisplayState
{
	/// <summary>Whether the marker box is visible.</summary>
	public bool IsBoxVisible { get; set; } = false;

	/// <summary>Whether the connecting line below the box is visible.</summary>
	public bool IsLineVisible { get; set; } = false;

	/// <summary>Grid row the marker occupies (-1 = not set).</summary>
	public int MarkerRow { get; set; } = -1;
}

/// <summary>
/// Aggregate view-state for <see cref="VerticalTimetableViewPresenter"/>.
/// </summary>
public class VerticalTimetableViewPageState
{
	/// <summary>Number of Grid row definitions to maintain.</summary>
	public int RowDefinitionCount { get; set; } = 0;

	/// <summary>Grid row index for the AfterArrive row.</summary>
	public int AfterArriveRowIndex { get; set; } = 1;

	/// <summary>Grid row index for the NextTrainButton row.</summary>
	public int NextTrainButtonRowIndex { get; set; } = 1;

	/// <summary>Current location marker display state.</summary>
	public LocationMarkerDisplayState Marker { get; set; } = new();

	/// <summary>Whether the view is in marker/highlight mode.</summary>
	public bool IsMarkingMode { get; set; } = false;
}
