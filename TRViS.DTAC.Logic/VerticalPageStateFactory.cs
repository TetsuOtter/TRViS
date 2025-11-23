namespace TRViS.DTAC.Logic;

using TRViS.IO.Models;

/// <summary>
/// Factory for creating and updating VerticalPageState instances.
/// This class contains all the business logic for determining visibility
/// and state of various UI components based on train data and system state.
/// </summary>
public static class VerticalPageStateFactory
{
	/// <summary>
	/// Creates an initial VerticalPageState from train data.
	/// </summary>
	/// <param name="trainData">The train data to create state from</param>
	/// <param name="affectDate">The affect date string</param>
	/// <param name="isLocationServiceEnabled">Whether location service is enabled</param>
	/// <param name="pageHeight">The current page height</param>
	/// <param name="contentOtherThanTimetableHeight">Pre-calculated height of non-timetable content</param>
	/// <param name="isPhoneIdiom">Whether the device is a phone idiom</param>
	/// <returns>A new VerticalPageState instance</returns>
	public static VerticalPageState CreateStateFromTrainData(
		TrainData? trainData,
		string? affectDate,
		bool isLocationServiceEnabled,
		double pageHeight,
		double contentOtherThanTimetableHeight,
		bool isPhoneIdiom = false)
	{
		var state = new VerticalPageState();

		if (trainData == null)
		{
			state.LocationServiceState.IsEnabled = isLocationServiceEnabled;
			state.PageHeaderState.AffectDateLabelText = affectDate ?? string.Empty;
			state.PageHeaderState.IsLocationServiceEnabled = isLocationServiceEnabled;
			state.ScrollViewState.NonTimetableContentHeight = contentOtherThanTimetableHeight;
			state.ScrollViewState.ShouldFillContent = isPhoneIdiom;
			return state;
		}

		// Set destination
		UpdateDestinationState(state.Destination, trainData.Destination);

		// Set train info
		state.TrainInfoAreaState.TrainInfoText = trainData.TrainInfo ?? string.Empty;
		state.TrainInfoAreaState.BeforeDepartureText = trainData.BeforeDeparture ?? string.Empty;
		state.TrainInfoAreaState.FullHeight = 90; // TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT

		// Set next day indicator
		UpdateNextDayIndicatorState(state.NextDayIndicatorState, trainData.DayCount);

		// Set train display info
		state.TrainDisplayInfo.MaxSpeed = trainData.MaxSpeed ?? string.Empty;
		state.TrainDisplayInfo.SpeedType = trainData.SpeedType ?? string.Empty;
		state.TrainDisplayInfo.NominalTractiveCapacity = trainData.NominalTractiveCapacity ?? string.Empty;
		state.TrainDisplayInfo.BeginRemarks = trainData.BeginRemarks ?? string.Empty;

		// Set location service state
		state.LocationServiceState.IsEnabled = isLocationServiceEnabled;

		// Set page header state
		state.PageHeaderState.AffectDateLabelText = affectDate ?? string.Empty;
		state.PageHeaderState.IsLocationServiceEnabled = isLocationServiceEnabled;

		// Set scroll view state
		state.ScrollViewState.NonTimetableContentHeight = contentOtherThanTimetableHeight;
		state.ScrollViewState.ShouldFillContent = isPhoneIdiom;

		return state;
	}

	/// <summary>
	/// Updates the destination state based on a destination string.
	/// </summary>
	/// <param name="state">The destination state to update</param>
	/// <param name="destination">The destination string</param>
	public static void UpdateDestinationState(DestinationInfo state, string? destination)
	{
		state.OriginalValue = destination;

		var formatted = DestinationFormatter.FormatDestination(destination);
		if (formatted is null)
		{
			state.IsVisible = false;
			state.Text = null;
		}
		else
		{
			state.Text = formatted;
			state.IsVisible = true;
		}
	}

	/// <summary>
	/// Updates the next day indicator state based on day count.
	/// </summary>
	/// <param name="state">The next day indicator state to update</param>
	/// <param name="dayCount">The number of days</param>
	public static void UpdateNextDayIndicatorState(NextDayIndicatorState state, int dayCount)
	{
		state.DayCount = dayCount;
		state.IsVisible = TimetableDisplayLogic.ShouldShowNextDayIndicator(dayCount);
	}

	/// <summary>
	/// Updates the before departure area state to open or close with animation.
	/// </summary>
	/// <param name="state">The train info area state to update</param>
	/// <param name="isToOpen">Whether to open or close the area</param>
	public static void UpdateTrainInfoAreaOpenCloseState(TrainInfoAreaState state, bool isToOpen)
	{
		state.IsOpen = isToOpen;
		state.IsAnimationRunning = true;

		// The actual animation will be handled by the View, but we track the target state
		if (isToOpen)
		{
			state.IsVisible = true;
			// CurrentHeight will be animated from 0 to FullHeight
		}
		else
		{
			// CurrentHeight will be animated from FullHeight to 0
			// IsVisible will be set to false after animation completes
		}
	}

	/// <summary>
	/// Marks the train info area animation as completed.
	/// </summary>
	/// <param name="state">The train info area state to update</param>
	/// <param name="wasOpenAnimation">Whether this was an open animation</param>
	public static void CompleteTrainInfoAreaAnimation(TrainInfoAreaState state, bool wasOpenAnimation)
	{
		state.IsAnimationRunning = false;

		if (!wasOpenAnimation)
		{
			state.IsVisible = false;
			state.CurrentHeight = 0;
		}
		else
		{
			state.IsVisible = true;
			state.CurrentHeight = state.FullHeight;
		}
	}

	/// <summary>
	/// Updates the debug map state based on device orientation and easter egg settings.
	/// </summary>
	/// <param name="state">The debug map state to update</param>
	/// <param name="isEasterEggEnabled">Whether the easter egg is enabled</param>
	/// <param name="isLandscape">Whether the device is in landscape mode</param>
	public static void UpdateDebugMapState(DebugMapState state, bool isEasterEggEnabled, bool isLandscape)
	{
		state.IsEnabled = isEasterEggEnabled;
		state.IsLandscapeMode = isLandscape;
		state.IsVisible = isEasterEggEnabled && isLandscape;

		if (state.IsVisible)
		{
			state.ColumnWidth = 1; // Star unit (will be calculated as remaining space)
		}
		else
		{
			state.ColumnWidth = 0;
		}
	}

	/// <summary>
	/// Updates the timetable activity indicator state.
	/// </summary>
	/// <param name="state">The indicator state to update</param>
	/// <param name="isTimetableBusy">Whether the timetable is busy loading</param>
	public static void UpdateTimetableActivityIndicatorState(TimetableActivityIndicatorState state, bool isTimetableBusy)
	{
		state.IsBusy = isTimetableBusy;
		state.IsVisible = isTimetableBusy;

		if (isTimetableBusy)
		{
			state.Opacity = state.MaxOpacity;
		}
		else
		{
			state.Opacity = 0;
		}
	}

	/// <summary>
	/// Updates the scroll view height based on timetable height and other content.
	/// </summary>
	/// <param name="state">The scroll view state to update</param>
	/// <param name="timetableHeight">The height of the timetable view</param>
	/// <param name="pageHeight">The current page height</param>
	public static void UpdateScrollViewHeight(ScrollViewState state, double timetableHeight, double pageHeight)
	{
		double heightRequest = TimetableDisplayLogic.CalculateScrollViewHeight(
			pageHeight,
			state.NonTimetableContentHeight,
			timetableHeight
		);

		state.ContentHeightRequest = heightRequest;
	}

	/// <summary>
	/// Updates the timetable view state when location service capability changes.
	/// </summary>
	/// <param name="state">The timetable view state to update</param>
	/// <param name="canUseLocationService">Whether location service can be used</param>
	public static void UpdateTimetableLocationServiceCapability(TimetableViewState state, bool canUseLocationService)
	{
		state.CanUseLocationService = canUseLocationService;
	}

	/// <summary>
	/// Updates the run state in page header.
	/// </summary>
	/// <param name="state">The page header state to update</param>
	/// <param name="isRunning">Whether the run is active</param>
	public static void UpdatePageHeaderRunState(PageHeaderState state, bool isRunning)
	{
		state.IsRunning = isRunning;
	}

	/// <summary>
	/// Updates location service enabled state.
	/// </summary>
	/// <param name="pageState">The overall page state to update</param>
	/// <param name="isEnabled">Whether location service is enabled</param>
	public static void UpdateLocationServiceEnabledState(VerticalPageState pageState, bool isEnabled)
	{
		pageState.LocationServiceState.IsEnabled = isEnabled;
		pageState.PageHeaderState.IsLocationServiceEnabled = isEnabled;
		pageState.TimetableViewState.IsLocationServiceEnabled = isEnabled;
	}

	/// <summary>
	/// Updates GPS location data.
	/// </summary>
	/// <param name="state">The location service state to update</param>
	/// <param name="latitude">The GPS latitude</param>
	/// <param name="longitude">The GPS longitude</param>
	/// <param name="accuracy">The GPS accuracy in meters</param>
	public static void UpdateGpsLocation(LocationServiceState state, double latitude, double longitude, double? accuracy)
	{
		state.CurrentLatitude = latitude;
		state.CurrentLongitude = longitude;
		state.CurrentAccuracy = accuracy ?? 20;
	}

	/// <summary>
	/// Resets the entire page state (typically when train data changes or page visibility changes).
	/// </summary>
	/// <returns>A new empty VerticalPageState</returns>
	public static VerticalPageState CreateEmptyState()
	{
		return new VerticalPageState
		{
			Destination = new(),
			TrainInfoAreaState = new(),
			NextDayIndicatorState = new(),
			DebugMapState = new(),
			TimetableActivityIndicatorState = new(),
			TimetableViewState = new(),
			ScrollViewState = new(),
			LocationServiceState = new(),
			PageHeaderState = new(),
			TrainDisplayInfo = new(),
			RowStates = new()
		};
	}

	/// <summary>
	/// Initializes row states for all rows in the timetable.
	/// </summary>
	/// <param name="pageState">The page state to update</param>
	/// <param name="rowCount">The number of rows in the timetable</param>
	public static void InitializeRowStates(VerticalPageState pageState, int rowCount)
	{
		pageState.RowStates.Clear();
		for (int i = 0; i < rowCount; i++)
		{
			pageState.RowStates[i] = new VerticalTimetableRowState();
		}
	}

	/// <summary>
	/// Updates the location state of a specific row.
	/// </summary>
	/// <param name="rowState">The row state to update</param>
	/// <param name="locationState">The new location state (0: Undefined, 1: AroundThisStation, 2: RunningToNextStation)</param>
	/// <param name="isLastRow">Whether this is the last row (prevents transition to RunningToNextStation)</param>
	/// <returns>True if state was updated, false if update was prevented</returns>
	public static bool UpdateRowLocationState(VerticalTimetableRowState rowState, int locationState, bool isLastRow = false)
	{
		// Prevent undefined state if currently defined
		if (locationState == 0)
		{
			rowState.LocationState = 0;
			return true;
		}

		// Prevent RunningToNextStation on last row
		if (isLastRow && locationState == 2)
		{
			return false;
		}

		// Update location state
		if (rowState.LocationState != locationState)
		{
			rowState.LocationState = locationState;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Advances the location state to the next state (used for manual row selection when location service is disabled).
	/// </summary>
	/// <param name="rowState">The row state to update</param>
	/// <param name="isLastRow">Whether this is the last row</param>
	public static void AdvanceRowLocationState(VerticalTimetableRowState rowState, bool isLastRow = false)
	{
		if (rowState.LocationState == 0)
		{
			rowState.LocationState = 1; // Undefined -> AroundThisStation
		}
		else if (rowState.LocationState == 1 && !isLastRow)
		{
			rowState.LocationState = 2; // AroundThisStation -> RunningToNextStation
		}
		else
		{
			rowState.LocationState = 0; // Reset to Undefined
		}
	}

	/// <summary>
	/// Resets all row location states to Undefined.
	/// </summary>
	/// <param name="pageState">The page state to update</param>
	public static void ResetAllRowLocationStates(VerticalPageState pageState)
	{
		foreach (var rowState in pageState.RowStates.Values)
		{
			rowState.LocationState = 0; // Undefined
		}
	}

	/// <summary>
	/// Determines if train data should be applied based on ViewHost visibility.
	/// </summary>
	/// <param name="trainData">The train data to check</param>
	/// <param name="isViewHostVisible">Whether the ViewHost is visible</param>
	/// <param name="isVerticalViewMode">Whether vertical view mode is active</param>
	/// <returns>True if should apply, false if should lazy load</returns>
	public static bool ShouldApplyTrainData(TrainData? trainData, bool isViewHostVisible, bool isVerticalViewMode)
	{
		return trainData != null && isViewHostVisible && isVerticalViewMode;
	}

	/// <summary>
	/// Gets train data information for state creation.
	/// </summary>
	/// <param name="trainData">The train data object</param>
	/// <returns>A tuple of (destination, trainInfo, beforeDeparture, dayCount)</returns>
	public static (string? Destination, string TrainInfo, string BeforeDeparture, int DayCount) GetTrainDataInfo(TrainData? trainData)
	{
		if (trainData == null)
		{
			return (null, string.Empty, string.Empty, 0);
		}

		var destination = trainData.Destination;
		var trainInfo = trainData.TrainInfo ?? string.Empty;
		var beforeDeparture = trainData.BeforeDeparture ?? string.Empty;
		var dayCount = trainData.DayCount;

		return (destination, trainInfo, beforeDeparture, dayCount);
	}

	/// <summary>
	/// Gets the rows from train data.
	/// </summary>
	/// <param name="trainData">The train data object</param>
	/// <returns>The rows, or null if not available</returns>
	public static TimetableRow[]? GetTrainDataRows(TrainData? trainData)
	{
		return trainData?.Rows;
	}
}

