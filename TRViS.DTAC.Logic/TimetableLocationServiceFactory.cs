namespace TRViS.DTAC.Logic;

/// <summary>
/// Factory for creating and mutating TimetableLocationServiceState.
/// Contains all location service tracking logic, completely separated from UI framework.
/// </summary>
public static class TimetableLocationServiceFactory
{
  /// <summary>
  /// Creates an empty initial state for location service.
  /// </summary>
  public static TimetableLocationServiceState CreateEmptyState()
    => new();

  /// <summary>
  /// Updates the run started state.
  /// </summary>
  /// <param name="state">The state to update</param>
  /// <param name="isRunStarted">Whether the run has started</param>
  public static void UpdateRunStartedState(TimetableLocationServiceState state, bool isRunStarted)
  {
    state.IsRunStarted = isRunStarted;

    if (!isRunStarted)
    {
      // When run stops, clear location tracking
      state.IsLocationServiceEnabled = false;
      ClearCurrentLocationMarker(state);
      state.CurrentRunningRow.RowIndex = -1;
      state.CurrentRunningRow.LocationState = TimetableLocationServiceState.LocationStates.Undefined;
      state.CurrentRunningRow.StationName = string.Empty;
      state.CurrentRunningRow.IsLastRow = false;
      state.DoubleTapDetection.LastTappedRowIndex = -1;

      // Reset all row states
      foreach (var rowState in state.RowStates.Values)
      {
        rowState.LocationState = 0; // Undefined
      }

      state.CurrentRunningRowView = null;
    }
  }

  /// <summary>
  /// Sets the current running row view (from UI layer).
  /// </summary>
  /// <param name="state">The state to update</param>
  /// <param name="rowView">The row view object</param>
  public static void SetCurrentRunningRowView(TimetableLocationServiceState state, object? rowView)
  {
    state.CurrentRunningRowView = rowView;

    // If setting a row, also update the location state
    // This will be called from View's CurrentRunningRow setter
  }

  /// <summary>
  /// Initializes the location service state with timetable row count.
  /// </summary>
  /// <param name="state">The state to update</param>
  /// <param name="totalRows">Total number of timetable rows</param>
  public static void InitializeTotalRows(TimetableLocationServiceState state, int totalRows)
  {
    state.TotalRows = Math.Max(0, totalRows);

    // Initialize row states dictionary
    state.RowStates.Clear();
    for (int i = 0; i < totalRows; i++)
    {
      state.RowStates[i] = new VerticalTimetableRowState();
    }
  }

  /// <summary>
  /// Updates the state when location service enabled status changes.
  /// </summary>
  /// <param name="state">The state to update</param>
  /// <param name="isEnabled">Whether location service is now enabled</param>
  public static void UpdateLocationServiceEnabled(TimetableLocationServiceState state, bool isEnabled)
  {
    state.IsLocationServiceEnabled = isEnabled;

    if (!isEnabled)
    {
      // When disabling, clear all location tracking
      ClearCurrentLocationMarker(state);
      state.CurrentRunningRow.RowIndex = -1;
      state.CurrentRunningRow.LocationState = TimetableLocationServiceState.LocationStates.Undefined;
      state.CurrentRunningRow.StationName = string.Empty;
      state.CurrentRunningRow.IsLastRow = false;
      state.DoubleTapDetection.LastTappedRowIndex = -1;

      // Reset all row states
      foreach (var rowState in state.RowStates.Values)
      {
        rowState.LocationState = 0; // Undefined
      }
    }
  }  /// <summary>
     /// Updates the state when location service capability changes.
     /// </summary>
     /// <param name="state">The state to update</param>
     /// <param name="canUseLocationService">Whether location service can be used</param>
  public static void UpdateLocationServiceCapability(TimetableLocationServiceState state, bool canUseLocationService)
  {
    state.CanUseLocationService = canUseLocationService;
  }

  /// <summary>
  /// Processes a location state change from the location service.
  /// This updates the current running row and visual marker state.
  /// </summary>
  /// <param name="state">The state to update</param>
  /// <param name="newStationIndex">Index of the station location service is reporting</param>
  /// <param name="isRunningToNextStation">Whether train is running to next station vs around current station</param>
  /// <param name="stationName">Station name for logging</param>
  /// <returns>Whether the update was successful</returns>
  public static bool ProcessLocationStateChanged(
    TimetableLocationServiceState state,
    int newStationIndex,
    bool isRunningToNextStation,
    string stationName = "")
  {
    // Validation
    if (!state.IsLocationServiceEnabled)
      return false;

    if (newStationIndex < 0)
      return false; // Invalid index, should disable location service

    if (newStationIndex >= state.TotalRows)
      return false; // Index out of bounds, should disable location service

    // Update current running row
    var oldRowIndex = state.CurrentRunningRow.RowIndex;
    state.CurrentRunningRow.RowIndex = newStationIndex;
    state.CurrentRunningRow.StationName = stationName;
    state.CurrentRunningRow.LocationState = isRunningToNextStation
      ? TimetableLocationServiceState.LocationStates.RunningToNextStation
      : TimetableLocationServiceState.LocationStates.AroundThisStation;

    // Update marker visualization
    UpdateLocationMarkerForCurrentRow(state);

    return true;
  }

  /// <summary>
  /// Updates the location marker visual state based on current running row.
  /// </summary>
  private static void UpdateLocationMarkerForCurrentRow(TimetableLocationServiceState state)
  {
    if (!state.CurrentRunningRow.IsValid)
    {
      ClearCurrentLocationMarker(state);
      return;
    }

    state.LocationMarker.MarkerRowIndex = state.CurrentRunningRow.RowIndex;
    state.LocationMarker.BoxIsVisible = state.CurrentRunningRow.LocationState is
      TimetableLocationServiceState.LocationStates.AroundThisStation or
      TimetableLocationServiceState.LocationStates.RunningToNextStation;
    state.LocationMarker.LineIsVisible = state.CurrentRunningRow.LocationState is
      TimetableLocationServiceState.LocationStates.RunningToNextStation;

    // Adjust marker margin for running to next station
    if (state.CurrentRunningRow.LocationState is TimetableLocationServiceState.LocationStates.RunningToNextStation)
      state.LocationMarker.MarkerTopMargin = -(state.LocationMarker.RowHeight / 2);
    else
      state.LocationMarker.MarkerTopMargin = 0;
  }

  /// <summary>
  /// Sets the current running row manually (e.g., from user tap or manual selection).
  /// </summary>
  /// <param name="state">The state to update</param>
  /// <param name="rowIndex">Index of the row to set as current</param>
  /// <param name="stationName">Station name for logging</param>
  /// <param name="isLastRow">Whether this is the last row in timetable</param>
  /// <param name="newLocationState">The location state to set</param>
  public static void SetCurrentRunningRow(
    TimetableLocationServiceState state,
    int rowIndex,
    string stationName,
    bool isLastRow,
    TimetableLocationServiceState.LocationStates newLocationState = TimetableLocationServiceState.LocationStates.AroundThisStation)
  {
    // Validation
    if (rowIndex < -1 || rowIndex >= state.TotalRows)
      return;

    // If unsetting current row
    if (rowIndex < 0)
    {
      state.CurrentRunningRow.RowIndex = -1;
      state.CurrentRunningRow.LocationState = TimetableLocationServiceState.LocationStates.Undefined;
      state.CurrentRunningRow.IsLastRow = false;
      state.CurrentRunningRow.StationName = string.Empty;
      ClearCurrentLocationMarker(state);
      return;
    }

    // Prevent advancing past last row
    if (isLastRow && newLocationState is TimetableLocationServiceState.LocationStates.RunningToNextStation)
      return; // Cannot run to next station from last row

    state.CurrentRunningRow.RowIndex = rowIndex;
    state.CurrentRunningRow.StationName = stationName;
    state.CurrentRunningRow.IsLastRow = isLastRow;
    state.CurrentRunningRow.LocationState = newLocationState;

    UpdateLocationMarkerForCurrentRow(state);
  }

  /// <summary>
  /// Clears the current location marker visual state.
  /// </summary>
  private static void ClearCurrentLocationMarker(TimetableLocationServiceState state)
  {
    state.LocationMarker.BoxIsVisible = false;
    state.LocationMarker.LineIsVisible = false;
    state.LocationMarker.MarkerRowIndex = -1;
    state.LocationMarker.MarkerTopMargin = 0;
  }

  /// <summary>
  /// Advances the location state of the current row (e.g., user tap cycles through states).
  /// </summary>
  /// <param name="state">The state to update</param>
  /// <param name="currentRow">Current row information</param>
  public static void AdvanceLocationState(TimetableLocationServiceState state, TimetableLocationServiceState.CurrentRunningRowInfo currentRow)
  {
    if (!currentRow.IsValid)
      return;

    var newState = currentRow.LocationState switch
    {
      TimetableLocationServiceState.LocationStates.Undefined
        => TimetableLocationServiceState.LocationStates.AroundThisStation,

      TimetableLocationServiceState.LocationStates.AroundThisStation
        => TimetableLocationServiceState.LocationStates.RunningToNextStation,

      TimetableLocationServiceState.LocationStates.RunningToNextStation
        => TimetableLocationServiceState.LocationStates.AroundThisStation,

      _ => TimetableLocationServiceState.LocationStates.Undefined
    };

    // Prevent advancing past last row
    if (currentRow.IsLastRow && newState is TimetableLocationServiceState.LocationStates.RunningToNextStation)
      return;

    currentRow.LocationState = newState;
    UpdateLocationMarkerForCurrentRow(state);
  }

  /// <summary>
  /// Records a tap for double-tap detection.
  /// </summary>
  /// <param name="state">The state to update</param>
  /// <param name="rowIndex">Index of tapped row</param>
  /// <param name="tapTime">Time of the tap</param>
  /// <returns>Whether a double-tap was detected</returns>
  public static bool RecordTapForDoubleTapDetection(TimetableLocationServiceState state, int rowIndex, DateTime tapTime)
  {
    if (!state.DoubleTapDetection.IsDoubleTap(rowIndex, tapTime))
    {
      // Record as pending tap
      state.DoubleTapDetection.LastTappedRowIndex = rowIndex;
      state.DoubleTapDetection.LastTapTime = tapTime;
      return false;
    }

    // Double-tap detected
    state.DoubleTapDetection.LastTappedRowIndex = -1;
    return true;
  }

  /// <summary>
  /// Clears the double-tap detection state (e.g., after processing double-tap).
  /// </summary>
  public static void ClearDoubleTapDetection(TimetableLocationServiceState state)
  {
    state.DoubleTapDetection.LastTappedRowIndex = -1;
    state.DoubleTapDetection.LastTapTime = DateTime.MinValue;
  }

  /// <summary>
  /// Updates haptic feedback capability (e.g., if it fails on device).
  /// </summary>
  public static void SetHapticEnabled(TimetableLocationServiceState state, bool isEnabled)
  {
    state.IsHapticEnabled = isEnabled;
  }

  /// <summary>
  /// Updates individual row location states based on the current running row information.
  /// This should be called by Logic after CurrentRunningRow is updated.
  /// </summary>
  /// <param name="state">The state to update</param>
  /// <param name="totalRows">Total number of rows (used to determine last row)</param>
  public static void UpdateRowStatesFromCurrentLocation(TimetableLocationServiceState state, int totalRows)
  {
    // Reset all row states first
    foreach (var rs in state.RowStates.Values)
    {
      rs.LocationState = 0; // Undefined
    }

    // Update the current row state based on CurrentRunningRow
    if (!state.CurrentRunningRow.IsValid || state.CurrentRunningRow.RowIndex < 0)
      return;

    int currentRowIndex = state.CurrentRunningRow.RowIndex;
    bool isRunningToNextStation = state.CurrentRunningRow.LocationState == TimetableLocationServiceState.LocationStates.RunningToNextStation;
    bool isLastRow = currentRowIndex == totalRows - 1;

    if (state.RowStates.TryGetValue(currentRowIndex, out var currentRowState))
    {
      // Set current row to AroundThisStation (1)
      if (!isLastRow || !isRunningToNextStation)
      {
        currentRowState.LocationState = 1; // AroundThisStation
      }

      // If running to next station and there's a next row, mark it
      if (isRunningToNextStation && currentRowIndex + 1 < totalRows)
      {
        if (state.RowStates.TryGetValue(currentRowIndex + 1, out var nextRowState))
        {
          nextRowState.LocationState = 2; // RunningToNextStation
        }
      }
    }
  }

  /// <summary>
  /// Sets the row height used for marker margin calculations.
  /// </summary>
  public static void SetRowHeight(TimetableLocationServiceState state, double rowHeight)
  {
    if (rowHeight > 0)
      state.LocationMarker.RowHeight = rowHeight;
  }
}
