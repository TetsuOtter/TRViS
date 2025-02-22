using DependencyPropertyGenerator;

using TRViS.IO.Models;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsBusy")]
[DependencyProperty<TrainData>("SelectedTrainData")]
[DependencyProperty<double>("ScrollViewHeight", DefaultValue = 0)]
public partial class VerticalTimetableView : Grid
{
	public class ScrollRequestedEventArgs(double PositionY) : EventArgs
	{
		public double PositionY { get; } = PositionY;
	}

	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	static public readonly GridLength RowHeight = new(60);

	public event EventHandler? IsBusyChanged;

	public event EventHandler<ScrollRequestedEventArgs>? ScrollRequested;

	public DTACMarkerViewModel MarkerViewModel { get; } = InstanceManager.DTACMarkerViewModel;

	partial void OnSelectedTrainDataChanged(TrainData? newValue)
	{
		try
		{
			logger.Trace("SelectedTrainData is changed to {0}", newValue?.TrainNumber);
			SetRowViews(newValue, newValue?.Rows);
			IsRunStarted = false;
			LocationService.SetTimetableRows(newValue?.Rows);
			ScrollRequested?.Invoke(this, new(0));
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnSelectedTrainDataChanged");
			Utils.ExitWithAlert(ex);
		}
	}

	partial void OnIsBusyChanged()
	{
		try
		{
			logger.Trace("IsBusy is changed to {0}", IsBusy);
			IsBusyChanged?.Invoke(this, new());
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnIsBusyChanged");
			Utils.ExitWithAlert(ex);
		}
	}

	int CurrentRunningRowIndex = -1;

	VerticalTimetableRow? _CurrentRunningRow = null;
	VerticalTimetableRow? CurrentRunningRow
	{
		get => _CurrentRunningRow;
		set
		{
			if (_CurrentRunningRow == value)
			{
				logger.Trace("CurrentRunningRow is already {0}, so skipping...", value?.RowIndex);
				return;
			}

			logger.Info("CurrentRunningRow is changed to {0}", value?.RowIndex);
			try
			{
				SetCurrentRunningRow(value);
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.CurrentRunningRow");
				Utils.ExitWithAlert(ex);
			}
		}
	}

	partial void OnIsRunStartedChanged(bool newValue)
	{
		CurrentRunningRow = newValue ? RowViewList.FirstOrDefault() : null;

		if (!newValue)
		{
			logger.Info("IsRunStarted is changed to false -> disable location service, and hide CurrentLocation");
			IsLocationServiceEnabled = false;
			CurrentLocationBoxView.IsVisible = CurrentLocationLine.IsVisible = false;
		}
		else
		{
			logger.Info("IsRunStarted is changed to true -> do nothing");
		}
	}

	const double DOUBLE_TAP_DETECT_MS = 500;
	(VerticalTimetableRow row, DateTime time)? _lastTappInfo = null;
	private void RowTapped(object? sender, EventArgs e)
	{
		if (sender is not BoxView boxView || boxView.BindingContext is not VerticalTimetableRow row)
			return;

		if (!IsRunStarted || !IsEnabled)
		{
			logger.Debug("IsRunStarted({0}) is false or IsEnabled({1}) is false -> do nothing", IsRunStarted, IsEnabled);
			return;
		}

		try
		{
			if (IsLocationServiceEnabled)
			{
				logger.Trace("IsLocationServiceEnabled is true");
				DateTime dateTimeNow = DateTime.Now;
				if (_lastTappInfo is null
					|| _lastTappInfo.Value.row != row
					|| dateTimeNow.AddMilliseconds(DOUBLE_TAP_DETECT_MS) < _lastTappInfo.Value.time)
				{
					logger.Debug("Tapped {0} -> LocationService is enabled and first tap detected -> record it to detect double tapping", row.RowIndex);
					_lastTappInfo = (row, dateTimeNow);
					return;
				}
			}
			else
			{
				logger.Trace("LocationService is not enabled");
			}

			_lastTappInfo = null;
			if (IsLocationServiceEnabled)
			{
				logger.Info("New LocationInfo is set because of double tapping (row:{0})", row.RowIndex);
				LocationService.ForceSetLocationInfo(row.RowIndex, false);
				return;
			}

			logger.Info("Tapped {0} -> set CurrentRunningRow to {0}", row.RowIndex);
			switch (row.LocationState)
			{
				case VerticalTimetableRow.LocationStates.Undefined:
					logger.Debug("Current LocationState is Undefined -> set LocationState to AroundThisStation");
					CurrentRunningRow = row;
					break;
				case VerticalTimetableRow.LocationStates.AroundThisStation:
					logger.Debug("Current LocationState is AroundThisStation -> set LocationState to RunningToNextStation");
					UpdateCurrentRunningLocationVisualizer(row, VerticalTimetableRow.LocationStates.RunningToNextStation);
					break;
				case VerticalTimetableRow.LocationStates.RunningToNextStation:
					logger.Debug("Current LocationState is RunningToNextStation -> set LocationState to AroundThisStation");
					UpdateCurrentRunningLocationVisualizer(row, VerticalTimetableRow.LocationStates.AroundThisStation);
					break;
			}
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.RowTapped");
			Utils.ExitWithAlert(ex);
		}
	}
}
