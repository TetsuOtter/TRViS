using DependencyPropertyGenerator;

using TRViS.DTAC.Logic;
using TRViS.Services;

namespace TRViS.DTAC;

public partial class VerticalTimetableView : Grid
{
	// Location service state model
	TimetableLocationServiceState LocationServiceState { get; } = TimetableLocationServiceFactory.CreateEmptyState();

	static bool IsHapticEnabled { get; set; } = true;

	void UpdateLocationMarkerFromState()
	{
		if (!MainThread.IsMainThread)
		{
			logger.Debug("MainThread is not current thread -> invoke UpdateLocationMarkerFromState on MainThread");
			MainThread.BeginInvokeOnMainThread(UpdateLocationMarkerFromState);
			return;
		}

		try
		{
			var marker = LocationServiceState.LocationMarker;
			var currentRow = LocationServiceState.CurrentRunningRow;

			// Update marker visibility and position
			CurrentLocationBoxView.IsVisible = marker.BoxIsVisible;
			CurrentLocationLine.IsVisible = marker.LineIsVisible;

			if (currentRow.IsValid)
			{
				Grid.SetRow(CurrentLocationBoxView, currentRow.RowIndex);
				Grid.SetRow(CurrentLocationLine, currentRow.RowIndex);
				CurrentLocationBoxView.Margin = new(0, marker.MarkerTopMargin);

				// Update visual representation in row
				// TODO: RowViewの方に実装されるべき処理
				// var rowView = RowViewList.ElementAtOrDefault(currentRow.RowIndex);
				// if (rowView is not null)
				// {
				// 	rowView.LocationState = currentRow.LocationState switch
				// 	{
				// 		TimetableLocationServiceState.LocationStates.Undefined => VerticalTimetableRow.LocationStates.Undefined,
				// 		TimetableLocationServiceState.LocationStates.AroundThisStation => VerticalTimetableRow.LocationStates.AroundThisStation,
				// 		TimetableLocationServiceState.LocationStates.RunningToNextStation => VerticalTimetableRow.LocationStates.RunningToNextStation,
				// 		_ => VerticalTimetableRow.LocationStates.Undefined
				// 	};

				// 	logger.Info("UpdateLocationMarkerFromState: Row[{0}] LocationState:{1}",
				// 		currentRow.RowIndex,
				// 		currentRow.LocationState
				// 	);
				// }
			}

			logger.Debug("CurrentLocationBoxView.IsVisible: {0}, CurrentLocationLine.IsVisible: {1}",
				CurrentLocationBoxView.IsVisible,
				CurrentLocationLine.IsVisible
			);
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.UpdateLocationMarkerFromState");
			Utils.ExitWithAlert(ex);
		}

		try
		{
			if (IsHapticEnabled)
				HapticFeedback.Default.Perform(HapticFeedbackType.Click);
		}
		catch (FeatureNotSupportedException)
		{
			logger.Warn("HapticFeedback is not supported");
			IsHapticEnabled = false;
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Failed to perform HapticFeedback");
			IsHapticEnabled = false;
		}

		// Scroll to current location if needed
		if (LocationServiceState.CurrentRunningRow.IsValid && LocationServiceState.ShouldScrollToCurrentLocation)
		{
			logger.Debug("CurrentRunningRow is valid -> invoke ScrollRequested");
			try
			{
				ScrollRequested?.Invoke(this, new(Math.Max(LocationServiceState.CurrentRunningRow.RowIndex - 1, 0) * RowHeight.Value));
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.UpdateLocationMarkerFromState.ScrollRequested");
				Utils.ExitWithAlert(ex);
			}
		}
		else
		{
			logger.Debug("CurrentRunningRow is invalid or should not scroll -> do nothing");
		}
	}

	// private void LocationService_LocationStateChanged(object? sender, LocationStateChangedEventArgs e)
	// {
	// 	logger.Info("LocationStateChanged: Index[{0}] IsRunningToNextStation:{1}",
	// 		e.NewStationIndex,
	// 		e.IsRunningToNextStation
	// 	);

	// 	try
	// 	{
	// 		// Get station name from row list for logging
	// 		string stationName = RowViewList.ElementAtOrDefault(e.NewStationIndex)?.RowData.StationName ?? "Unknown";

	// 		// Let Logic process and update state
	// 		bool success = TimetableLocationServiceFactory.ProcessLocationStateChanged(
	// 			LocationServiceState,
	// 			e.NewStationIndex,
	// 			e.IsRunningToNextStation,
	// 			stationName
	// 		);

	// 		if (!success)
	// 		{
	// 			logger.Warn("ProcessLocationStateChanged failed");
	// 			return;
	// 		}

	// 		// View reads from state and updates UI
	// 		RefreshUIFromState();
	// 	}
	// 	catch (Exception ex)
	// 	{
	// 		logger.Fatal(ex, "Unknown Exception");
	// 		InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.LocationService_LocationStateChanged");
	// 		Utils.ExitWithAlert(ex);
	// 	}

	// 	logger.Debug("process finished");
	// }

	private void RefreshUIFromState()
	{
		try
		{
			// Let Logic update row states based on current location
			TimetableLocationServiceFactory.UpdateRowStatesFromCurrentLocation(LocationServiceState, LocationServiceState.TotalRows);

			// Apply state to all row views (UI update)
			foreach (var row in RowViewList)
			{
				if (LocationServiceState.RowStates.TryGetValue(row.RowIndex, out var rowState))
				{
					row.RowState = rowState;
				}
			}

			// Update location marker visualization
			UpdateLocationMarkerFromState();
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.RefreshUIFromState");
			Utils.ExitWithAlert(ex);
		}
	}
}

