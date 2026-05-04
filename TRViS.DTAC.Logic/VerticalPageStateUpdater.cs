namespace TRViS.DTAC.Logic;

using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Formatters;
using TRViS.DTAC.Logic.Layout;

/// <summary>
/// Provides methods for updating existing VerticalPageState instances.
/// </summary>
internal static class VerticalPageStateUpdater
{
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
	/// Resets all row location states to Undefined.
	/// </summary>
	/// <param name="pageState">The page state to update</param>
	public static void ResetAllRowLocationStates(VerticalPageState pageState)
	{
		foreach (var rowState in pageState.RowStates.Values)
		{
			rowState.LocationState = TimetableLocationState.Undefined;
		}
	}
}
