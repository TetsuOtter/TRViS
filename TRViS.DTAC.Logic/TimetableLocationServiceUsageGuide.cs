namespace TRViS.DTAC.Logic;

/// <summary>
/// Usage guide for TimetableLocationServiceState and factory.
/// This demonstrates best practices for using the location service state model.
/// </summary>
public static class TimetableLocationServiceUsageGuide
{
	/// <summary>
	/// Example: Complete workflow for location service management in a timetable view.
	/// </summary>
	public static void ExampleCompleteWorkflow()
	{
		// 1. Create initial state
		var locationState = TimetableLocationServiceFactory.CreateEmptyState();

		// 2. Initialize when timetable rows are loaded
		var rowCount = 50; // From TrainData?.Rows?.Length
		TimetableLocationServiceFactory.InitializeTotalRows(locationState, rowCount);

		// 3. Set row height for proper marker positioning
		const double ROW_HEIGHT = 60;
		TimetableLocationServiceFactory.SetRowHeight(locationState, ROW_HEIGHT);

		// 4. When location service capability changes (e.g., GPS available)
		TimetableLocationServiceFactory.UpdateLocationServiceCapability(locationState, canUseLocationService: true);

		// 5. When user enables/disables location service
		TimetableLocationServiceFactory.UpdateLocationServiceEnabled(locationState, isEnabled: true);

		// 6. When location service reports current station
		// (This comes from ILocationService.LocationStateChanged event)
		var processResult = TimetableLocationServiceFactory.ProcessLocationStateChanged(
			locationState,
			newStationIndex: 10,
			isRunningToNextStation: false,
			stationName: "Tokyo Station"
		);

		if (processResult)
		{
			// Update UI based on state:
			// - Set CurrentLocationBoxView.IsVisible = locationState.LocationMarker.BoxIsVisible
			// - Set CurrentLocationLine.IsVisible = locationState.LocationMarker.LineIsVisible
			// - Set Grid.SetRow(marker, locationState.LocationMarker.MarkerRowIndex)
			// - Set marker.Margin = new(0, locationState.LocationMarker.MarkerTopMargin)
		}

		// 7. When user taps a row
		var tapTime = DateTime.Now;
		var rowIndex = 5;

		// Check for double-tap first
		bool isDoubleTap = TimetableLocationServiceFactory.RecordTapForDoubleTapDetection(
			locationState, rowIndex, tapTime);

		if (isDoubleTap)
		{
			// User double-tapped - force location service update
			TimetableLocationServiceFactory.ClearDoubleTapDetection(locationState);
			// Call LocationService.ForceSetLocationInfo(rowIndex, false);
		}
		else if (locationState.IsLocationServiceEnabled)
		{
			// Location service is enabled, so we don't manually set row
			// Just record the tap and wait for location service update
		}
		else
		{
			// Location service disabled - allow manual row selection
			TimetableLocationServiceFactory.SetCurrentRunningRow(
				locationState,
				rowIndex: rowIndex,
				stationName: "Station Name",
				isLastRow: false
			);

			// When user taps again to advance state
			TimetableLocationServiceFactory.AdvanceLocationState(locationState, locationState.CurrentRunningRow);
		}

		// 8. When run starts
		// Keep location service enabled if it was, it will track automatically

		// 9. When run ends
		TimetableLocationServiceFactory.UpdateLocationServiceEnabled(locationState, isEnabled: false);
		// UI should clear markers when disabled
	}

	/// <summary>
	/// Example: Integration pattern for VerticalTimetableView.
	/// </summary>
	public static void ExampleVerticalTimetableViewIntegration()
	{
		// In OnSelectedTrainDataChanged():
		// 1. Get new row count from trainData?.Rows?.Length
		// 2. Call TimetableLocationServiceFactory.InitializeTotalRows(state, rowCount)

		// In constructor:
		// 1. Create state: locationState = TimetableLocationServiceFactory.CreateEmptyState()
		// 2. Subscribe to LocationService.LocationStateChanged
		// 3. When event fires, call ProcessLocationStateChanged()

		// In LocationService_LocationStateChanged() handler:
		// var processResult = TimetableLocationServiceFactory.ProcessLocationStateChanged(
		//     locationState,
		//     e.NewStationIndex,
		//     e.IsRunningToNextStation,
		//     rowName);
		//
		// if (!processResult)
		//     TimetableLocationServiceFactory.UpdateLocationServiceEnabled(locationState, false);

		// In RowTapped() handler:
		// if (locationState.IsLocationServiceEnabled)
		// {
		//     bool isDoubleTap = TimetableLocationServiceFactory.RecordTapForDoubleTapDetection(
		//         locationState, rowIndex, DateTime.Now);
		//     if (isDoubleTap)
		//         LocationService.ForceSetLocationInfo(rowIndex, false);
		// }
		// else
		// {
		//     TimetableLocationServiceFactory.SetCurrentRunningRow(...);
		//     // OR cycle through states:
		//     TimetableLocationServiceFactory.AdvanceLocationState(locationState, locationState.CurrentRunningRow);
		// }
	}

	/// <summary>
	/// Example: Error handling and edge cases.
	/// </summary>
	public static void ExampleErrorHandling()
	{
		var state = TimetableLocationServiceFactory.CreateEmptyState();
		TimetableLocationServiceFactory.InitializeTotalRows(state, 50);

		// Case 1: Location service reports invalid index
		var result = TimetableLocationServiceFactory.ProcessLocationStateChanged(state, -1, false, "");
		if (!result)
		{
			// Location service reported invalid index
			// Should disable location service and alert user
			TimetableLocationServiceFactory.UpdateLocationServiceEnabled(state, false);
		}

		// Case 2: Index out of bounds
		result = TimetableLocationServiceFactory.ProcessLocationStateChanged(state, 100, false, "");
		if (!result)
		{
			// Train passed all stations (likely test run end)
			TimetableLocationServiceFactory.UpdateLocationServiceEnabled(state, false);
		}

		// Case 3: Haptic feedback failed on device
		TimetableLocationServiceFactory.SetHapticEnabled(state, false);
		// Don't attempt haptic feedback anymore

		// Case 4: Last row special handling
		TimetableLocationServiceFactory.SetCurrentRunningRow(state, 49, "Last Station", isLastRow: true);
		// Attempting to advance will be blocked by factory
		TimetableLocationServiceFactory.AdvanceLocationState(state, state.CurrentRunningRow);
		// LocationState will remain AroundThisStation, not advance to RunningToNextStation
	}

	/// <summary>
	/// Example: Debug and logging patterns.
	/// </summary>
	public static void ExampleDebugging()
	{
		var state = TimetableLocationServiceFactory.CreateEmptyState();

		// Check complete state
		var summary = state.ToString();
		// Output: "LocationService:Off CanUse:False Current:Row[-1](...) Marker:Box:False Line:False Row:-1 Haptic:True"

		// Check current row
		if (state.CurrentRunningRow.IsValid)
		{
			// Has a valid row selected
			var rowInfo = state.CurrentRunningRow.ToString();
			// Output: "Row[10](Tokyo Station) State:AroundThisStation"
		}

		// Check marker state
		var markerInfo = state.LocationMarker.ToString();
		// Output: "Box:True Line:False Row:10"

		// Check double-tap state
		bool hasPendingTap = state.DoubleTapDetection.HasPendingTap;
	}
}
