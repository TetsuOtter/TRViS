namespace TRViS.DTAC.Logic;

/// <summary>
/// Represents the complete state of location service tracking in the timetable view.
/// This includes current running row information, visual indicators, and location state tracking.
/// Completely separable from UI framework - pure data model.
/// </summary>
public class TimetableLocationServiceState
{
  /// <summary>
  /// Represents the visual state of a timetable row relative to the train's current location.
  /// </summary>
  public enum LocationStates
  {
    /// <summary>Train is not at or approaching this station</summary>
    Undefined,

    /// <summary>Train is around/at this station</summary>
    AroundThisStation,

    /// <summary>Train is running to the next station after this one</summary>
    RunningToNextStation,
  }

  /// <summary>
  /// Information about the currently running/selected row.
  /// </summary>
  public class CurrentRunningRowInfo
  {
    /// <summary>Index of the current running row in the timetable</summary>
    public int RowIndex { get; set; } = -1;

    /// <summary>Current location state of the running row</summary>
    public LocationStates LocationState { get; set; } = LocationStates.Undefined;

    /// <summary>Whether this is the last row in the timetable</summary>
    public bool IsLastRow { get; set; } = false;

    /// <summary>Station name of the current row (for logging)</summary>
    public string StationName { get; set; } = string.Empty;

    /// <summary>Check if a valid row is selected</summary>
    public bool IsValid => RowIndex >= 0;

    public override string ToString()
      => $"Row[{RowIndex}]({StationName}) State:{LocationState}";
  }

  /// <summary>
  /// Visual indicator state for the location marker in the timetable.
  /// </summary>
  public class LocationMarkerState
  {
    /// <summary>Whether the box indicator is visible</summary>
    public bool BoxIsVisible { get; set; } = false;

    /// <summary>Whether the line indicator is visible</summary>
    public bool LineIsVisible { get; set; } = false;

    /// <summary>Row index where the marker should be displayed</summary>
    public int MarkerRowIndex { get; set; } = -1;

    /// <summary>Top margin for the marker (when running to next station)</summary>
    public double MarkerTopMargin { get; set; } = 0;

    /// <summary>Height of each timetable row for margin calculations</summary>
    public double RowHeight { get; set; } = 60;

    public override string ToString()
      => $"Box:{BoxIsVisible} Line:{LineIsVisible} Row:{MarkerRowIndex}";
  }

  /// <summary>
  /// State for double-tap detection in location service.
  /// Used to distinguish between manual row selection and location service updates.
  /// </summary>
  public class DoubleTapDetectionState
  {
    /// <summary>Milliseconds to consider taps as double-tap</summary>
    public const double DOUBLE_TAP_DETECT_MS = 500;

    /// <summary>Index of the last tapped row</summary>
    public int LastTappedRowIndex { get; set; } = -1;

    /// <summary>Timestamp of the last tap</summary>
    public DateTime LastTapTime { get; set; } = DateTime.MinValue;

    /// <summary>Whether the last tap was recorded</summary>
    public bool HasPendingTap => LastTappedRowIndex >= 0;

    /// <summary>Check if a new tap on the same row is a double-tap</summary>
    public bool IsDoubleTap(int rowIndex, DateTime tapTime)
    {
      if (LastTappedRowIndex != rowIndex)
        return false;

      TimeSpan elapsed = tapTime - LastTapTime;
      return elapsed.TotalMilliseconds < DOUBLE_TAP_DETECT_MS;
    }
  }

  /// <summary>
  /// Overall timetable location service state.
  /// </summary>

  /// <summary>Whether the run has started</summary>
  public bool IsRunStarted { get; set; } = false;

  /// <summary>The currently selected/running row view (from View layer)</summary>
  public object? CurrentRunningRowView { get; set; } = null;

  /// <summary>Whether location service is enabled by user</summary>
  public bool IsLocationServiceEnabled { get; set; } = false;

  /// <summary>Whether location service can be used (has required data)</summary>
  public bool CanUseLocationService { get; set; } = false;

  /// <summary>Total number of rows in the timetable</summary>
  public int TotalRows { get; set; } = 0;

  /// <summary>Information about the current running row</summary>
  public CurrentRunningRowInfo CurrentRunningRow { get; } = new();

  /// <summary>Visual state of the location marker</summary>
  public LocationMarkerState LocationMarker { get; } = new();

  /// <summary>State for detecting double taps</summary>
  public DoubleTapDetectionState DoubleTapDetection { get; } = new();

  /// <summary>Whether haptic feedback is enabled (can fail on some devices)</summary>
  public bool IsHapticEnabled { get; set; } = true;

  /// <summary>Whether to scroll to the current location when it updates</summary>
  public bool ShouldScrollToCurrentLocation { get; set; } = true;

  /// <summary>
  /// State information for each timetable row, keyed by row index.
  /// This allows individual row location states to be tracked separately from the overall location service.
  /// </summary>
  public Dictionary<int, VerticalTimetableRowState> RowStates { get; set; } = new();

  public override string ToString()
    => $"LocationService:{(IsLocationServiceEnabled ? "On" : "Off")} CanUse:{CanUseLocationService} " +
       $"Current:{CurrentRunningRow} Marker:{LocationMarker} Haptic:{IsHapticEnabled}";
}
