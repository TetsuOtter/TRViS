using DependencyPropertyGenerator;

using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsBusy")]
[DependencyProperty<bool>("IsRunStarted")]
[DependencyProperty<bool>("IsLocationServiceEnabled")]
[DependencyProperty<TrainData>("SelectedTrainData")]
public partial class VerticalTimetableView : Grid
{
	public class ScrollRequestedEventArgs : EventArgs
	{
		public double PositionY { get; }

		public ScrollRequestedEventArgs(double PositionY)
		{
			this.PositionY = PositionY;
		}
	}

	LocationService LocationService { get; } = new();

	static readonly GridLength RowHeight = new(60);

	public event EventHandler? IsBusyChanged;

	public event EventHandler<ScrollRequestedEventArgs>? ScrollRequested;

	public DTACMarkerViewModel MarkerViewModel { get; init; } = new();

	partial void OnSelectedTrainDataChanged(TrainData? newValue)
	{
		SetRowViews(newValue, newValue?.Rows);
	}

	partial void OnIsBusyChanged()
		=> IsBusyChanged?.Invoke(this, new());

	int CurrentRunningRowIndex = -1;

	VerticalTimetableRow? NextRunningRow = null;

	VerticalTimetableRow? _CurrentRunningRow = null;
	VerticalTimetableRow? CurrentRunningRow
	{
		get => _CurrentRunningRow;
		set
		{
			if (_CurrentRunningRow == value)
				return;

			SetCurrentRunningRow(value);
		}
	}

	private void RowTapped(object? sender, EventArgs e)
	{
		if (sender is not VerticalTimetableRow row)
			return;

		if (!IsRunStarted || !IsEnabled)
			return;

		switch (row.LocationState)
		{
			case VerticalTimetableRow.LocationStates.Undefined:
				CurrentRunningRow = row;
				break;
			case VerticalTimetableRow.LocationStates.AroundThisStation:
				row.LocationState = VerticalTimetableRow.LocationStates.RunningToNextStation;
				SetNearbyCheckInfo(NextRunningRow);
				break;
			case VerticalTimetableRow.LocationStates.RunningToNextStation:
				row.LocationState = VerticalTimetableRow.LocationStates.AroundThisStation;
				SetNearbyCheckInfo(CurrentRunningRow);
				break;
		}

		CurrentLocationBoxView.IsVisible = CurrentLocationLine.IsVisible
			= row.LocationState == VerticalTimetableRow.LocationStates.RunningToNextStation;
	}

	partial void OnIsRunStartedChanged(bool newValue)
	{
		CurrentRunningRow = newValue ? RowViewList.FirstOrDefault() : null;
	}

	partial void OnIsLocationServiceEnabledChanged(bool newValue)
	{
		LocationService.IsEnabled = newValue;
	}

	private void LocationService_IsNearbyChanged(object? sender, bool oldValue, bool newValue)
	{
		if (!IsRunStarted || !IsEnabled || CurrentRunningRow is null)
			return;

		if (newValue)
		{
			SetCurrentRunningRow(NextRunningRow);
		}
		else if (CurrentRunningRow is not null)
		{
			CurrentRunningRow.LocationState = VerticalTimetableRow.LocationStates.RunningToNextStation;

			SetNearbyCheckInfo(NextRunningRow);
		}
	}

	private void SetNearbyCheckInfo(VerticalTimetableRow? nextRunningRow)
	{
		if (nextRunningRow?.BindingContext is TimetableRow nextRowData)
		{
			LocationService.NearbyCenter
				= nextRowData.Location is LocationInfo
				{
					Latitude_deg: double lat,
					Longitude_deg: double lon
				}
					? new Location(lat, lon)
					: null;

			LocationService.NearbyRadius_m = nextRowData.Location.OnStationDetectRadius_m ?? 300;
		}
	}

	public void SetCurrentRunningRow(int index)
		=> SetCurrentRunningRow(index, RowViewList.ElementAtOrDefault(index));

	public void SetCurrentRunningRow(VerticalTimetableRow? value)
		=> SetCurrentRunningRow(value is null ? -1 : RowViewList.IndexOf(value), value);

	void SetCurrentRunningRow(int index, VerticalTimetableRow? value)
	{
		if (CurrentRunningRowIndex == index || CurrentRunningRow == value)
			return;

		if (RowViewList.ElementAtOrDefault(index) != value)
			throw new ArgumentException("value is not match with element at given index", nameof(value));

		if (_CurrentRunningRow is not null)
			_CurrentRunningRow.LocationState = VerticalTimetableRow.LocationStates.Undefined;

		_CurrentRunningRow = value;

		if (value is not null)
		{
			CurrentRunningRowIndex = index;
			value.LocationState = VerticalTimetableRow.LocationStates.AroundThisStation;

			int rowCount = Grid.GetRow(value);

			Grid.SetRow(CurrentLocationBoxView, rowCount + 1);
			Grid.SetRow(CurrentLocationLine, rowCount);

			CurrentLocationBoxView.IsVisible = false;
			CurrentLocationLine.IsVisible = false;

			if (value.LocationState != VerticalTimetableRow.LocationStates.Undefined)
			{
				ScrollRequested?.Invoke(this, new(Math.Max(rowCount - 1, 0) * RowHeight.Value));
			}
		}
		else
			CurrentRunningRowIndex = -1;

		NextRunningRow = RowViewList.ElementAtOrDefault(index + 1);

		SetNearbyCheckInfo(value);
	}
}
