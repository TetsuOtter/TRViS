namespace TRViS.DTAC.Logic;

using TRViS.IO.Models;
using TRViS.DTAC.Logic.Abstractions;

/// <summary>
/// Factory for creating VerticalPageState instances.
/// </summary>
internal static class VerticalPageStateFactory
{
	/// <summary>
	/// Creates an initial VerticalPageState from train data.
	/// </summary>
	/// <param name="trainData">The train data to create state from</param>
	/// <param name="affectDate">The affect date string</param>
	/// <param name="isLocationServiceEnabled">Whether location service is enabled</param>
	/// <returns>A new VerticalPageState instance</returns>
	public static VerticalPageState CreateStateFromTrainData(
		TrainData? trainData,
		string? affectDate,
		bool isLocationServiceEnabled)
	{
		var state = new VerticalPageState();

		if (trainData == null)
		{
			state.LocationServiceState.IsEnabled = isLocationServiceEnabled;
			state.PageHeaderState.AffectDateLabelText = affectDate ?? string.Empty;
			state.PageHeaderState.IsLocationServiceEnabled = isLocationServiceEnabled;
			return state;
		}

		// Set destination
		VerticalPageStateUpdater.UpdateDestinationState(state.Destination, trainData.Destination);

		// Set train info
		state.TrainInfoAreaState.TrainInfoText = trainData.TrainInfo ?? string.Empty;
		state.TrainInfoAreaState.BeforeDepartureText = trainData.BeforeDeparture ?? string.Empty;

		// Set next day indicator
		VerticalPageStateUpdater.UpdateNextDayIndicatorState(state.NextDayIndicatorState, trainData.DayCount);

		// Set train display info
		state.TrainDisplayInfo.TrainNumber = trainData.TrainNumber ?? string.Empty;
		state.TrainDisplayInfo.CarCount = trainData.CarCount;
		state.TrainDisplayInfo.MaxSpeed = trainData.MaxSpeed ?? string.Empty;
		state.TrainDisplayInfo.SpeedType = trainData.SpeedType ?? string.Empty;
		state.TrainDisplayInfo.NominalTractiveCapacity = trainData.NominalTractiveCapacity ?? string.Empty;
		state.TrainDisplayInfo.BeginRemarks = trainData.BeginRemarks ?? string.Empty;

		// Set location service state
		state.LocationServiceState.IsEnabled = isLocationServiceEnabled;

		// Set page header state
		state.PageHeaderState.AffectDateLabelText = affectDate ?? string.Empty;
		state.PageHeaderState.IsLocationServiceEnabled = isLocationServiceEnabled;

		return state;
	}

	/// <summary>
	/// Creates an empty VerticalPageState.
	/// </summary>
	/// <returns>A new empty VerticalPageState</returns>
	public static VerticalPageState CreateEmptyState()
	{
		return new VerticalPageState
		{
			Destination = new(),
			TrainInfoAreaState = new(),
			NextDayIndicatorState = new(),
			TimetableViewState = new(),
			LocationServiceState = new(),
			PageHeaderState = new(),
			TrainDisplayInfo = new(),
			RowStates = new()
		};
	}

	/// <summary>
	/// Initializes row states from the timetable rows, preserving per-row IsInfoRow.
	/// </summary>
	public static void InitializeRowStates(VerticalPageState pageState, TimetableRow[] rows)
	{
		pageState.RowStates.Clear();
		for (int i = 0; i < rows.Length; i++)
		{
			pageState.RowStates[i] = new VerticalTimetableRowState { IsInfoRow = rows[i].IsInfoRow };
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
