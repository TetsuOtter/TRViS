using DependencyPropertyGenerator;

using TRViS.IO.Models;
using TRViS.Services;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsRunStarted")]
[DependencyProperty<bool>("IsLocationServiceEnabled")]
public partial class VerticalTimetableView : Grid
{
	LocationService LocationService { get; } = new();

	public event EventHandler<ValueChangedEventArgs<bool>>? IsLocationServiceEnabledChanged;
	partial void OnIsLocationServiceEnabledChanged(bool newValue)
	{
		if (newValue)
		{
			logger.Info("IsLocationServiceEnabled is changed to true -> set NearbyCheckInfo");
			SetNearbyCheckInfo(CurrentRunningRow);
		}
		else
		{
			logger.Info("IsLocationServiceEnabled is changed to false");
		}

		LocationService.IsEnabled = newValue;
		IsLocationServiceEnabledChanged?.Invoke(this, new(!newValue, newValue));
	}

	private void LocationService_IsNearbyChanged(object? sender, bool oldValue, bool newValue)
	{
		if (!IsRunStarted || !IsEnabled || CurrentRunningRow is null || !LocationService.IsEnabled)
		{
			logger.Info(
				"!IsRunStarted: {0} || "
				+ "!IsEnabled: {1} || "
				+ "CurrentRunningRow is null: {2} || "
				+ "!LocationService.IsEnabled: {3}"
				+ "-> do nothing",
				IsRunStarted,
				IsEnabled,
				CurrentRunningRow is null,
				LocationService.IsEnabled
			);

			return;
		}

		if (newValue)
		{
			logger.Info("IsNearby is changed to true (= Around Current Station) -> set CurrentRunningRow to NextRunningRow({0})", NextRunningRow?.RowIndex ?? -1);
			SetCurrentRunningRow(NextRunningRow);
		}
		else if (CurrentRunningRow is not null)
		{
			logger.Info("IsNearby is changed to false (= Running to next station)"
			+ " -> set CurrentRunningRow.LocationState to RunningToNextStation and Update NearbyCheckInfo");
			CurrentRunningRow.LocationState = VerticalTimetableRow.LocationStates.RunningToNextStation;

			SetNearbyCheckInfo(NextRunningRow);
		}
	}

	private void SetNearbyCheckInfo(VerticalTimetableRow? nextRunningRow)
	{
		if (nextRunningRow?.RowData is TimetableRow nextRowData)
		{
			LocationService.NearbyCenter
				= nextRowData.Location is LocationInfo
				{
					Latitude_deg: double lat,
					Longitude_deg: double lon
				}
					? new Location(lat, lon)
					: null;

			logger.Info(
				"Set NearbyCenter to {0}, Radius: {1}, (Row[{2}].StationName: {3})",
				LocationService.NearbyCenter,
				nextRowData.Location.OnStationDetectRadius_m,
				nextRunningRow.RowIndex,
				nextRowData.StationName
			);

			LocationService.NearbyRadius_m = nextRowData.Location.OnStationDetectRadius_m ?? LocationService.DefaultNearbyRadius_m;
		}
		else
		{
			logger.Debug("nextRunningRow is null or nextRunningRow.RowData is null -> do nothing");
		}
	}

	public void SetCurrentRunningRow(int index)
		=> SetCurrentRunningRow(index, RowViewList.ElementAtOrDefault(index));

	public void SetCurrentRunningRow(VerticalTimetableRow? value)
		=> SetCurrentRunningRow(value is null ? -1 : RowViewList.IndexOf(value), value);

	void SetCurrentRunningRow(int index, VerticalTimetableRow? value)
	{
		if (CurrentRunningRowIndex == index || CurrentRunningRow == value)
		{
			logger.Trace("CurrentRunningRowIndex is already {0} or CurrentRunningRow is already {1}, so skipping...", index, value?.RowIndex);
			return;
		}

		if (RowViewList.ElementAtOrDefault(index) != value)
			throw new ArgumentException("value is not match with element at given index", nameof(value));

		MainThread.BeginInvokeOnMainThread(() =>
		{
			if (_CurrentRunningRow is not null)
			{
				logger.Debug("CurrentRunningRow[{0}: {1}] is not null -> set LocationState to Undefined",
					_CurrentRunningRow.RowIndex,
					_CurrentRunningRow.RowData.StationName
				);

				_CurrentRunningRow.LocationState = VerticalTimetableRow.LocationStates.Undefined;
			}

			logger.Info("CurrentRunningRow is changed from {0}: `{1}` to {2}: `{3}`",
				CurrentRunningRow?.RowIndex,
				CurrentRunningRow?.RowData.StationName,
				index,
				value?.RowData.StationName
			);
			_CurrentRunningRow = value;

			if (value is not null)
			{
				CurrentRunningRowIndex = index;
				UpdateCurrentRunningLocationVisualizer(value, VerticalTimetableRow.LocationStates.AroundThisStation);

				if (value.LocationState != VerticalTimetableRow.LocationStates.Undefined)
				{
					logger.Debug("value.LocationState is not Undefined -> invoke ScrollRequested");
					ScrollRequested?.Invoke(this, new(Math.Max(value.RowIndex - 1, 0) * RowHeight.Value));
				}
				else
				{
					logger.Debug("value.LocationState is Undefined -> do nothing");
				}
			}
			else
			{
				logger.Debug("value is null -> set CurrentRunningRowIndex to -1");
				CurrentRunningRowIndex = -1;
			}

			NextRunningRow = RowViewList.ElementAtOrDefault(index + 1);
			logger.Debug("NextRunningRow is set to {0}: `{1}`",
				NextRunningRow?.RowIndex,
				NextRunningRow?.RowData.StationName
			);
		});
	}

	void UpdateCurrentRunningLocationVisualizer(VerticalTimetableRow row, VerticalTimetableRow.LocationStates states)
	{
		logger.Info("UpdateCurrentRunningLocationVisualizer: {0} ... {1}", row.RowIndex, states);
		row.LocationState = states;

		int rowCount = row.RowIndex;

		Grid.SetRow(CurrentLocationBoxView, rowCount);
		Grid.SetRow(CurrentLocationLine, rowCount);

		CurrentLocationBoxView.IsVisible = row.LocationState
			is VerticalTimetableRow.LocationStates.AroundThisStation
			or VerticalTimetableRow.LocationStates.RunningToNextStation;
		CurrentLocationLine.IsVisible = row.LocationState is VerticalTimetableRow.LocationStates.RunningToNextStation;

		CurrentLocationBoxView.Margin = row.LocationState
			is VerticalTimetableRow.LocationStates.RunningToNextStation
			? new(0, -(RowHeight.Value / 2)) : new(0);
	}
}
