using DependencyPropertyGenerator;

using TRViS.Services;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsRunStarted")]
[DependencyProperty<bool>("IsLocationServiceEnabled")]
public partial class VerticalTimetableView : Grid
{
	readonly LocationService LocationService = InstanceManager.LocationService;
	public event EventHandler<bool>? CanUseLocationServiceChanged
	{
		add => LocationService.CanUseServiceChanged += value;
		remove => LocationService.CanUseServiceChanged -= value;
	}
	public bool CanUseLocationService => LocationService.CanUseService;

	public event EventHandler<ValueChangedEventArgs<bool>>? IsLocationServiceEnabledChanged;
	partial void OnIsLocationServiceEnabledChanged(bool newValue)
	{
		logger.Info("IsLocationServiceEnabled is changed to {0}", newValue);

		try
		{
			LocationService.IsEnabled = newValue;
			IsLocationServiceEnabledChanged?.Invoke(this, new(!newValue, newValue));
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnIsLocationServiceEnabledChanged");
			Utils.ExitWithAlert(ex);
		}
	}

	private void LocationService_LocationStateChanged(object? sender, LocationStateChangedEventArgs e)
	{
		if (!IsLocationServiceEnabled)
		{
			logger.Debug("IsLocationServiceEnabled is false -> Do nothing");
			return;
		}
		if (e.NewStationIndex < 0)
		{
			logger.Warn("e.NewStationIndex is less than 0 -> Disable LocationService");
			IsLocationServiceEnabled = false;
			return;
		}
		if (RowViewList.Count <= e.NewStationIndex)
		{
			logger.Warn("RowViewList.Count is less than e.NewStationIndex -> Disable LocationService");
			IsLocationServiceEnabled = false;
			return;
		}

		logger.Info("LocationStateChanged: [{0}](State:{1}) Rows[{2}](IsRunningToNextStation: {3})",
			CurrentRunningRowIndex,
			CurrentRunningRow?.LocationState,
			e.NewStationIndex,
			e.IsRunningToNextStation
		);

		try
		{
			if (CurrentRunningRow is not null)
				CurrentRunningRow.LocationState = VerticalTimetableRow.LocationStates.Undefined;
			VerticalTimetableRow rowView = RowViewList[e.NewStationIndex];
			UpdateCurrentRunningLocationVisualizer(rowView, e.IsRunningToNextStation
				? VerticalTimetableRow.LocationStates.RunningToNextStation
				: VerticalTimetableRow.LocationStates.AroundThisStation
			);
			_CurrentRunningRow = rowView;
			CurrentRunningRowIndex = e.NewStationIndex;
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.LocationService_LocationStateChanged");
			Utils.ExitWithAlert(ex);
		}

		logger.Debug("process finished");
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
		{
			logger.Error("value is not match with element at given index: {0}", index);
			throw new ArgumentException("value is not match with element at given index", nameof(value));
		}

		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
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
				}
				else
				{
					logger.Debug("value is null -> set CurrentRunningRowIndex to -1");
					CurrentRunningRowIndex = -1;
				}
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.SetCurrentRunningRow");
				Utils.ExitWithAlert(ex);
			}
		});
	}

	static bool IsHapticEnabled { get; set; } = true;
	void UpdateCurrentRunningLocationVisualizer(VerticalTimetableRow row, VerticalTimetableRow.LocationStates states)
	{
		if (!MainThread.IsMainThread)
		{
			logger.Debug("MainThread is not current thread -> invoke UpdateCurrentRunningLocationVisualizer on MainThread");
			MainThread.BeginInvokeOnMainThread(() => UpdateCurrentRunningLocationVisualizer(row, states));
			return;
		}

		try
		{
			row.LocationState = states;
			logger.Info("UpdateCurrentRunningLocationVisualizer: Row[{0}] ... Requested:{1}, Actual:{2}", row.RowIndex, states, row.LocationState);

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

			logger.Debug("CurrentLocationBoxView.IsVisible: {0}, CurrentLocationLine.IsVisible: {1}", CurrentLocationBoxView.IsVisible, CurrentLocationLine.IsVisible);
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.UpdateCurrentRunningLocationVisualizer");
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

		if (row.LocationState != VerticalTimetableRow.LocationStates.Undefined)
		{
			logger.Debug("value.LocationState is not Undefined -> invoke ScrollRequested");
			try
			{
				ScrollRequested?.Invoke(this, new(Math.Max(row.RowIndex - 1, 0) * RowHeight.Value));
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.UpdateCurrentRunningLocationVisualizer.ScrollRequested");
				Utils.ExitWithAlert(ex);
			}
		}
		else
		{
			logger.Debug("value.LocationState is Undefined -> do nothing");
		}
	}
}
