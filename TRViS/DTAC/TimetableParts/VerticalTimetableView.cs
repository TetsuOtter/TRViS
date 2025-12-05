using DependencyPropertyGenerator;

using System.ComponentModel;

using TRViS.DTAC.TimetableParts;
using TRViS.DTAC.ViewModels;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsBusy")]
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

	public VerticalTimetableViewModel ViewModel { get; } = new();

	public ScrollView? ScrollView { get; set; }

	CancellationTokenSource? _currentSetRowViewsCancellationTokenSource = null;
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

	static bool IsHapticEnabled { get; set; } = true;
	void UpdateCurrentRunningLocationVisualizer(VerticalTimetableRow row, VerticalTimetableRowModel.LocationStates states)
	{
		int rowCount = row.Model.RowIndex;

		Grid.SetRow(CurrentLocationBoxView, rowCount);
		Grid.SetRow(CurrentLocationLine, rowCount);

		CurrentLocationBoxView.IsVisible = states
			is VerticalTimetableRowModel.LocationStates.AroundThisStation
			or VerticalTimetableRowModel.LocationStates.RunningToNextStation;
		CurrentLocationLine.IsVisible = states is VerticalTimetableRowModel.LocationStates.RunningToNextStation;

		CurrentLocationBoxView.Margin = states
			is VerticalTimetableRowModel.LocationStates.RunningToNextStation
			? new(0, -(RowHeight.Value / 2)) : new(0);

		try
		{
			if (IsHapticEnabled)
				HapticFeedback.Default.Perform(HapticFeedbackType.Click);
		}
		catch (FeatureNotSupportedException)
		{
			IsHapticEnabled = false;
		}
		catch (Exception ex)
		{
			IsHapticEnabled = false;
			logger.Error(ex, "HapticFeedback Failed");
		}

		if (states != VerticalTimetableRowModel.LocationStates.Undefined)
		{
			logger.Debug("LocationState is not Undefined -> invoke ScrollRequested");
			try
			{
				ScrollRequested?.Invoke(this, new(Math.Max(row.Model.RowIndex - 1, 0) * RowHeight.Value));
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
			logger.Debug("LocationState is Undefined -> do nothing");
		}
	}

	private void RowTapped(object? sender, EventArgs e) // ViewModel側で処理する
	{
		if (sender is not VerticalTimetableRow row)
			return;

		if (!ViewModel.IsRunStarted || !IsEnabled)
		{
			logger.Debug("IsRunStarted({0}) is false or IsEnabled({1}) is false -> do nothing", ViewModel.IsRunStarted, IsEnabled);
			return;
		}

		try
		{
			// Handle row tap through ViewModel
			ViewModel.HandleRowTappedWithDoubleTapDetection(row, RowViewList.Count);
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
			ViewModel.IsMarkingMode = MarkerViewModel.IsToggled;
			foreach (var row in RowViewList)
			{
				row.Model.IsMarkingMode = MarkerViewModel.IsToggled;
			}
		}
	}

	private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(ViewModel.CurrentRows):
				await OnViewModelCurrentRowsChangedAsync();
				break;
			case nameof(ViewModel.IsRunStarted):
				OnViewModelIsRunStartedChanged();
				break;
			case nameof(ViewModel.LocationMarkerState):
				OnViewModelLocationMarkerStateChanged();
				break;
			case nameof(ViewModel.LocationMarkerPosition):
				OnViewModelLocationMarkerPositionChanged();
				break;
			case nameof(ViewModel.CurrentRunningRow):
				OnViewModelCurrentRunningRowChanged();
				break;
			case nameof(ViewModel.AfterRemarksText):
				OnViewModelAfterRemarksTextChanged();
				break;
			case nameof(ViewModel.AfterArriveText):
				OnViewModelAfterArriveTextChanged();
				break;
			case nameof(ViewModel.NextTrainId):
				OnViewModelNextTrainIdChanged();
				break;
		}
	}

	private async Task OnViewModelCurrentRowsChangedAsync()
	{
		logger.Trace("CurrentRows is changed");

		// Cancel previous SetRowViews operation
		_currentSetRowViewsCancellationTokenSource?.Cancel();
		_currentSetRowViewsCancellationTokenSource = new CancellationTokenSource();
		try
		{
			await SetRowViewsAsync([.. ViewModel.CurrentRows], _currentSetRowViewsCancellationTokenSource.Token);
		}
		catch (OperationCanceledException)
		{
			logger.Debug("SetRowViewsAsync operation was canceled");
			return;
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnViewModelCurrentRowsChanged.SetRowViewsAsync");
			await Utils.ExitWithAlert(ex);
		}
	}

	private async void OnCurrentRowsCollectionChangedAsync(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => await OnViewModelCurrentRowsChangedAsync();

	private void OnViewModelIsRunStartedChanged()
	{
		try
		{
			bool newValue = ViewModel.IsRunStarted;
			if (!newValue)
			{
				logger.Info("IsRunStarted is changed to false -> disable location service, and hide CurrentLocation");
				CurrentLocationBoxView.IsVisible = CurrentLocationLine.IsVisible = false;
				ViewModel.CurrentRunningRow = null;
			}
			else
			{
				// 既に CurrentRunningRow が設定されている場合はそれを保持する
				if (ViewModel.CurrentRunningRow is not null)
				{
					logger.Info("IsRunStarted is changed to true and CurrentRunningRow is already set -> keep current row {0}", ViewModel.CurrentRunningRow.Model.RowIndex);
					return;
				}

				logger.Info("IsRunStarted is changed to true -> set CurrentRunningRow to first row");
				VerticalTimetableRow? firstRow = RowViewList.FirstOrDefault();
				if (firstRow is not null)
				{
					ViewModel.SetCurrentRunningRow(0, firstRow);
					ViewModel.LocationMarkerState = VerticalTimetableRowModel.LocationStates.AroundThisStation;
				}
				else
				{
					logger.Debug("RowViewList is empty -> defer setting CurrentRunningRow");
					ViewModel.CurrentRunningRow = null;
				}
			}
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnViewModelIsRunStartedChanged");
			Utils.ExitWithAlert(ex);
		}
	}

	private void OnViewModelLocationMarkerStateChanged()
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
				if (ViewModel.CurrentRunningRow is not null)
				{
					UpdateCurrentRunningLocationVisualizer(ViewModel.CurrentRunningRow, ViewModel.LocationMarkerState);
				}
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnViewModelLocationMarkerStateChanged");
				Utils.ExitWithAlert(ex);
			}
		});
	}

	private void OnViewModelLocationMarkerPositionChanged()
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
				ViewModel.SetCurrentRunningRowFromLocationMarkerPosition(RowViewList);
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnViewModelLocationMarkerPositionChanged");
				Utils.ExitWithAlert(ex);
			}
		});
	}

	private void OnViewModelCurrentRunningRowChanged()
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
				if (ViewModel.CurrentRunningRow is not null)
				{
					int rowIndex = RowViewList.IndexOf(ViewModel.CurrentRunningRow);
					if (rowIndex >= 0)
					{
						ViewModel.LocationMarkerPosition = rowIndex;
						UpdateCurrentRunningLocationVisualizer(ViewModel.CurrentRunningRow, ViewModel.LocationMarkerState);
					}
				}
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnViewModelCurrentRunningRowChanged");
				Utils.ExitWithAlert(ex);
			}
		});
	}

	private void OnViewModelAfterRemarksTextChanged()
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
				bool hasAfterRemarks = ViewModel.AfterRemarksText is not null;
				SetRowDefinitions(RowsCount, ViewModel.AfterArriveText is not null, ViewModel.NextTrainId is not null);
				AddSeparatorLines();
				AfterRemarks.SetRow(RowsCount);
				AfterArrive.SetRow(RowsCount + 1);
				Grid.SetRow(NextTrainButton, ViewModel.AfterArriveText is not null ? RowsCount + 2 : RowsCount + 1);

				if (ViewModel.AfterRemarksText is not null)
				{
					AfterRemarks.Text = ViewModel.AfterRemarksText;
					AfterRemarks.AddToParent();
				}
				else
				{
					AfterRemarks.RemoveFromParent();
				}
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnViewModelAfterRemarksTextChanged");
				Utils.ExitWithAlert(ex);
			}
		});
	}

	private void OnViewModelAfterArriveTextChanged()
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
				bool hasAfterRemarks = ViewModel.AfterRemarksText is not null;
				SetRowDefinitions(RowsCount, ViewModel.AfterArriveText is not null, ViewModel.NextTrainId is not null);
				AddSeparatorLines();
				AfterArrive.SetRow(RowsCount + 1);
				Grid.SetRow(NextTrainButton, ViewModel.AfterArriveText is not null ? RowsCount + 2 : RowsCount + 1);

				if (ViewModel.AfterArriveText is not null)
				{
					AfterArrive.Text = ViewModel.AfterArriveText;
					AfterArrive.AddToParent();
				}
				else
				{
					AfterArrive.RemoveFromParent();
				}
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnViewModelAfterArriveTextChanged");
				Utils.ExitWithAlert(ex);
			}
		});
	}

	private void OnViewModelNextTrainIdChanged()
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
				bool hasAfterRemarks = ViewModel.AfterRemarksText is not null;
				SetRowDefinitions(RowsCount, ViewModel.AfterArriveText is not null, ViewModel.NextTrainId is not null);
				AddSeparatorLines();
				Grid.SetRow(NextTrainButton, ViewModel.AfterArriveText is not null ? RowsCount + 2 : RowsCount + 1);

				if (ViewModel.NextTrainId is not null)
				{
					NextTrainButton.NextTrainId = ViewModel.NextTrainId;
					this.Children.Add(NextTrainButton);
				}
				else
				{
					this.Children.Remove(NextTrainButton);
				}
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnViewModelNextTrainIdChanged");
				Utils.ExitWithAlert(ex);
			}
		});
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
