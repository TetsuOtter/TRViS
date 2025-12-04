using DependencyPropertyGenerator;

using System.ComponentModel;

using TRViS.DTAC.TimetableParts;
using TRViS.DTAC.ViewModels;
using TRViS.IO.Models;
using TRViS.Services;
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

	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	static public readonly GridLength RowHeight = new(60);

	public event EventHandler? IsBusyChanged;

	public event EventHandler<ScrollRequestedEventArgs>? ScrollRequested;

	public DTACMarkerViewModel MarkerViewModel { get; } = InstanceManager.DTACMarkerViewModel;

	public VerticalTimetableColumnVisibilityState ColumnVisibilityState { get; } = new((int)DeviceDisplay.MainDisplayInfo.Width);

	VerticalTimetableRowModel.LocationStates CurrentLocationState = VerticalTimetableRowModel.LocationStates.Undefined;

	CancellationTokenSource? _currentSetRowViewsCancellationTokenSource = null;

	partial void OnSelectedTrainDataChanged(TrainData? newValue)
	{
		try
		{
			logger.Trace("SelectedTrainData is changed to {0}", newValue?.TrainNumber);
			// Cancel previous SetRowViews operation
			_currentSetRowViewsCancellationTokenSource?.Cancel();
			_currentSetRowViewsCancellationTokenSource = new CancellationTokenSource();
			Task.Run(async () =>
			{
				try
				{
					await SetRowViewsAsync(newValue, newValue?.Rows, _currentSetRowViewsCancellationTokenSource.Token);
				}
				catch (OperationCanceledException)
				{
					logger.Debug("SetRowViewsAsync operation was canceled");
					return;
				}
				catch (Exception ex)
				{
					logger.Fatal(ex, "Unknown Exception");
					InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnSelectedTrainDataChanged.SetRowViewsAsync");
					await Utils.ExitWithAlert(ex);
				}
			});
			IsRunStarted = false;
			LocationService.SetTimetableRows(newValue?.Rows);
			ScrollRequested?.Invoke(this, new(0));
		}
		catch (OperationCanceledException)
		{
			logger.Debug("OnSelectedTrainDataChanged operation was canceled");
			return;
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
				logger.Trace("CurrentRunningRow is already {0}, so skipping...", value?.Model.RowIndex);
				return;
			}

			logger.Info("CurrentRunningRow is changed to {0}", value?.Model.RowIndex);
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
		if (!newValue)
		{
			logger.Info("IsRunStarted is changed to false -> disable location service, and hide CurrentLocation");
			IsLocationServiceEnabled = false;
			CurrentLocationBoxView.IsVisible = CurrentLocationLine.IsVisible = false;
			CurrentRunningRow = null;
		}
		else
		{
			// 既に CurrentRunningRow が設定されている場合はそれを保持する
			if (CurrentRunningRow is not null)
			{
				logger.Info("IsRunStarted is changed to true and CurrentRunningRow is already set -> keep current row {0}", CurrentRunningRow.Model.RowIndex);
				return;
			}

			logger.Info("IsRunStarted is changed to true -> set CurrentRunningRow to first row");
			VerticalTimetableRow? firstRow = RowViewList.FirstOrDefault();
			if (firstRow is not null)
			{
				SetCurrentRunningRow(firstRow);
			}
			else
			{
				logger.Debug("RowViewList is empty -> defer setting CurrentRunningRow");
				CurrentRunningRow = null;
			}
		}
	}

	const double DOUBLE_TAP_DETECT_MS = 500;
	(VerticalTimetableRow row, DateTime time)? _lastTapInfo = null;
	private void RowTapped(object? sender, EventArgs e)
	{
		if (sender is not VerticalTimetableRow row)
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
				if (_lastTapInfo is null
					|| _lastTapInfo.Value.row != row
					|| dateTimeNow.AddMilliseconds(DOUBLE_TAP_DETECT_MS) < _lastTapInfo.Value.time)
				{
					logger.Debug("Tapped {0} -> LocationService is enabled and first tap detected -> record it to detect double tapping", row.Model.RowIndex);
					_lastTapInfo = (row, dateTimeNow);
					return;
				}
			}
			else
			{
				logger.Trace("LocationService is not enabled");
			}

			_lastTapInfo = null;
			if (IsLocationServiceEnabled)
			{
				logger.Info("New LocationInfo is set because of double tapping (row:{0})", row.Model.RowIndex);
				LocationService.ForceSetLocationInfo(row.Model.RowIndex, false);
				return;
			}

			// 異なる駅をタップした場合
			if (CurrentRunningRow != row)
			{
				logger.Info("Tapped different row {0} -> set CurrentRunningRow to {0} with AroundThisStation", row.Model.RowIndex);
				SetCurrentRunningRow(row);
				return;
			}

			logger.Info("Tapped {0} -> cycle LocationState", row.Model.RowIndex);
			switch (CurrentLocationState)
			{
				case VerticalTimetableRowModel.LocationStates.Undefined:
					logger.Debug("Current LocationState is Undefined -> set LocationState to AroundThisStation");
					SetCurrentRunningRow(row);
					break;
				case VerticalTimetableRowModel.LocationStates.AroundThisStation:
					// 最後の行の場合はRunningToNextStationに遷移させない
					if (row.Model.RowIndex == RowViewList.Count - 1)
					{
						logger.Debug("Current row is last row -> do nothing");
					}
					else
					{
						logger.Debug("Current LocationState is AroundThisStation -> set LocationState to RunningToNextStation");
						UpdateCurrentRunningLocationVisualizer(row, VerticalTimetableRowModel.LocationStates.RunningToNextStation);
					}
					break;
				case VerticalTimetableRowModel.LocationStates.RunningToNextStation:
					logger.Debug("Current LocationState is RunningToNextStation -> cycle back to AroundThisStation");
					UpdateCurrentRunningLocationVisualizer(row, VerticalTimetableRowModel.LocationStates.AroundThisStation);
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

	private void OnMarkerViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(DTACMarkerViewModel.IsToggled))
		{
			foreach (var row in RowViewList)
			{
				row.Model.IsMarkingMode = MarkerViewModel.IsToggled;
			}
		}
	}

	private void OnMarkerBoxClicked(object? sender, EventArgs e)
	{
		if (sender is not VerticalTimetableRow row || !row.Model.IsMarkingMode)
			return;

		try
		{
			if (row.Model.MarkerColor is null)
			{
				row.Model.MarkerColor = MarkerViewModel.SelectedColor;
				row.Model.MarkerText = MarkerViewModel.SelectedText ?? string.Empty;
			}
			else
			{
				row.Model.MarkerColor = null;
				row.Model.MarkerText = null;
			}
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnMarkerBoxClicked");
			Utils.ExitWithAlert(ex);
		}
	}
}
