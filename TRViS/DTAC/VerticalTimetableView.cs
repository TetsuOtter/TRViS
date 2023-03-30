using DependencyPropertyGenerator;

using TRViS.IO.Models;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsBusy")]
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

	partial void OnIsRunStartedChanged(bool newValue)
	{
		CurrentRunningRow = newValue ? RowViewList.FirstOrDefault() : null;

		if (!newValue)
			IsLocationServiceEnabled = false;
	}

	const double DOUBLE_TAP_DETECT_MS = 500;
	(VerticalTimetableRow row, DateTime time)? _lastTappInfo = null;
	private void RowTapped(object? sender, EventArgs e)
	{
		if (sender is not VerticalTimetableRow row)
			return;

		if (!IsRunStarted || !IsEnabled)
			return;

		if (IsLocationServiceEnabled)
		{
			DateTime dateTimeNow = DateTime.Now;
			if (_lastTappInfo is null
				|| _lastTappInfo.Value.row != row
				|| dateTimeNow.AddMilliseconds(DOUBLE_TAP_DETECT_MS) < _lastTappInfo.Value.time)
			{
				_lastTappInfo = (row, dateTimeNow);
				return;
			}
		}

		_lastTappInfo = null;
		IsLocationServiceEnabled = false;

		switch (row.LocationState)
		{
			case VerticalTimetableRow.LocationStates.Undefined:
				CurrentRunningRow = row;
				break;
			case VerticalTimetableRow.LocationStates.AroundThisStation:
				row.LocationState = VerticalTimetableRow.LocationStates.RunningToNextStation;
				break;
			case VerticalTimetableRow.LocationStates.RunningToNextStation:
				row.LocationState = VerticalTimetableRow.LocationStates.AroundThisStation;
				break;
		}

		CurrentLocationBoxView.IsVisible = CurrentLocationLine.IsVisible
			= row.LocationState == VerticalTimetableRow.LocationStates.RunningToNextStation;
	}
}
