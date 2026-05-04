namespace TRViS.DTAC.Logic;

using TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Represents the complete state of the D-TAC vertical page display.
/// This model encapsulates all display-related flags and properties that control
/// the visibility and behavior of various UI components.
/// </summary>
public class VerticalPageState
{
	/// <summary>
	/// Information about the destination display
	/// </summary>
	public DestinationInfo Destination { get; set; } = new();

	/// <summary>
	/// Information about the train info and before departure area
	/// </summary>
	public TrainInfoAreaState TrainInfoAreaState { get; set; } = new();

	/// <summary>
	/// Information about the next day indicator
	/// </summary>
	public NextDayIndicatorState NextDayIndicatorState { get; set; } = new();

	/// <summary>
	/// Information about the timetable view
	/// </summary>
	public TimetableViewState TimetableViewState { get; set; } = new();

	/// <summary>
	/// Information about location service
	/// </summary>
	public LocationServiceState LocationServiceState { get; set; } = new();

	/// <summary>
	/// Information about the page header (run start/stop, location service toggle, open/close)
	/// </summary>
	public PageHeaderState PageHeaderState { get; set; } = new();

	/// <summary>
	/// Train-specific display information
	/// </summary>
	public TrainDisplayInfo TrainDisplayInfo { get; set; } = new();

	/// <summary>
	/// State information for each timetable row, keyed by row index
	/// </summary>
	public Dictionary<int, VerticalTimetableRowState> RowStates { get; set; } = new();

}

/// <summary>
/// Represents the destination display information
/// </summary>
public class DestinationInfo
{
	/// <summary>
	/// Whether the destination label should be visible
	/// </summary>
	public bool IsVisible { get; set; } = false;

	/// <summary>
	/// The formatted destination text to display
	/// </summary>
	public string? Text { get; set; } = null;

	/// <summary>
	/// The original destination string (before formatting)
	/// </summary>
	public string? OriginalValue { get; set; } = null;
}

/// <summary>
/// Represents the state of the train info and before departure area
/// </summary>
public class TrainInfoAreaState
{
	/// <summary>
	/// The train info text to display
	/// </summary>
	public string TrainInfoText { get; set; } = string.Empty;

	/// <summary>
	/// The before departure text to display
	/// </summary>
	public string BeforeDepartureText { get; set; } = string.Empty;
}

/// <summary>
/// Represents the state of the next day indicator
/// </summary>
public class NextDayIndicatorState
{
	/// <summary>
	/// Whether the next day label should be visible
	/// </summary>
	public bool IsVisible { get; set; } = false;

	/// <summary>
	/// The day count (0 for same day, &gt; 0 for next days)
	/// </summary>
	public int DayCount { get; set; } = 0;
}

/// <summary>
/// Represents the state of the timetable view
/// </summary>
public class TimetableViewState
{
	/// <summary>
	/// Whether the run has started
	/// </summary>
	public bool IsRunStarted { get; set; } = false;

	/// <summary>
	/// Whether location service is enabled for the timetable
	/// </summary>
	public bool IsLocationServiceEnabled { get; set; } = false;

	/// <summary>
	/// Whether location service can be used
	/// </summary>
	public bool CanUseLocationService { get; set; } = false;
}

/// <summary>
/// Represents the state of location service
/// </summary>
public class LocationServiceState
{
	/// <summary>
	/// Whether location service is enabled
	/// </summary>
	public bool IsEnabled { get; set; } = false;

	/// <summary>
	/// The current GPS latitude
	/// </summary>
	public double? CurrentLatitude { get; set; } = null;

	/// <summary>
	/// The current GPS longitude
	/// </summary>
	public double? CurrentLongitude { get; set; } = null;

	/// <summary>
	/// The GPS accuracy in meters
	/// </summary>
	public double? CurrentAccuracy { get; set; } = null;
}

/// <summary>
/// Represents the state of the page header
/// </summary>
public class PageHeaderState
{
	/// <summary>
	/// Whether the run is currently active
	/// </summary>
	public bool IsRunning { get; set; } = false;

	/// <summary>
	/// Whether location service is enabled in the header
	/// </summary>
	public bool IsLocationServiceEnabled { get; set; } = false;

	/// <summary>
	/// Whether location service can be used
	/// </summary>
	public bool CanUseLocationService { get; set; } = false;

	/// <summary>
	/// The affect date label text
	/// </summary>
	public string AffectDateLabelText { get; set; } = string.Empty;
}

/// <summary>
/// Represents train-specific display information
/// </summary>
public class TrainDisplayInfo
{
	/// <summary>
	/// The train number (列番)
	/// </summary>
	public string TrainNumber { get; set; } = string.Empty;

	/// <summary>
	/// The number of cars (両数)
	/// </summary>
	public int? CarCount { get; set; } = null;

	/// <summary>
	/// The maximum speed of the train
	/// </summary>
	public string MaxSpeed { get; set; } = string.Empty;

	/// <summary>
	/// The speed type (e.g., "営団日比谷線", etc.)
	/// </summary>
	public string SpeedType { get; set; } = string.Empty;

	/// <summary>
	/// The nominal tractive capacity
	/// </summary>
	public string NominalTractiveCapacity { get; set; } = string.Empty;

	/// <summary>
	/// The begin remarks text
	/// </summary>
	public string BeginRemarks { get; set; } = string.Empty;
}

/// <summary>
/// Represents the state of a single timetable row
/// </summary>
public class VerticalTimetableRowState
{
	/// <summary>
	/// The location state of this row
	/// </summary>
	public TimetableLocationState LocationState { get; set; } = TimetableLocationState.Undefined;

	/// <summary>
	/// Whether the row is currently enabled for user interaction
	/// </summary>
	public bool IsEnabled { get; set; } = true;
}


