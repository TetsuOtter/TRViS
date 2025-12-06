using DependencyPropertyGenerator;

using System.ComponentModel;

using TRViS.DTAC.TimetableParts;
using TRViS.DTAC.ViewModels;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<double>("ScrollViewHeight", DefaultValue = 0)]
public partial class VerticalTimetableView : Grid
{
	public class ScrollRequestedEventArgs(double PositionY) : EventArgs
	{
		public double PositionY { get; } = PositionY;
	}

	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	static public readonly GridLength RowHeight = new(60);

	public event EventHandler<bool>? IsBusyChanged;
	public event EventHandler<ScrollRequestedEventArgs>? ScrollRequested;

	public DTACMarkerViewModel MarkerViewModel { get; } = InstanceManager.DTACMarkerViewModel;

	public VerticalTimetableColumnVisibilityState ColumnVisibilityState { get; } = new((int)DeviceDisplay.MainDisplayInfo.Width);

	public VerticalTimetableViewModel ViewModel { get; } = new();

	private bool _isBusy = false;
	private bool IsBusy
	{
		get => _isBusy;
		set
		{
			if (_isBusy == value)
				return;
			_isBusy = value;
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
	}

	CancellationTokenSource? _currentSetRowViewsCancellationTokenSource = null;

	private void RowTapped(object? sender, EventArgs e)
	{
		if (sender is not VerticalTimetableRow row)
			return;

		try
		{
			ViewModel.HandleRowTappedWithDoubleTapDetection(row.Model, RowViewList.Count);
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
		switch (e.PropertyName)
		{
			case nameof(DTACMarkerViewModel.IsToggled):
				ViewModel.IsMarkingMode = MarkerViewModel.IsToggled;
				break;
		}
	}

	private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(ViewModel.CurrentRows):
				await OnViewModelCurrentRowsChangedAsync();
				break;
			case nameof(ViewModel.LocationMarkerState):
				OnViewModelLocationMarkerStateChanged();
				break;
			case nameof(ViewModel.LocationMarkerPosition):
				OnViewModelLocationMarkerPositionChanged();
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
			await SetRowViewsAsync(ViewModel.CurrentRows, _currentSetRowViewsCancellationTokenSource.Token);
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

	private void OnViewModelLocationMarkerStateChanged()
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
				switch (ViewModel.LocationMarkerState)
				{
					case VerticalTimetableRowModel.LocationStates.Undefined:
						CurrentLocationBoxView.IsVisible = CurrentLocationLine.IsVisible = false;
						break;
					case VerticalTimetableRowModel.LocationStates.AroundThisStation:
						CurrentLocationBoxView.IsVisible = true;
						CurrentLocationBoxView.Margin = new(0);
						CurrentLocationLine.IsVisible = false;
						Utils.PerformHaptic(HapticFeedbackType.Click);
						break;

					case VerticalTimetableRowModel.LocationStates.RunningToNextStation:
						CurrentLocationBoxView.IsVisible = true;
						CurrentLocationBoxView.Margin = new(0, -(RowHeight.Value / 2));
						CurrentLocationLine.IsVisible = true;
						Utils.PerformHaptic(HapticFeedbackType.Click);
						break;
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
				if (0 <= ViewModel.LocationMarkerPosition)
				{
					Grid.SetRow(CurrentLocationBoxView, ViewModel.LocationMarkerPosition);
					Grid.SetRow(CurrentLocationLine, ViewModel.LocationMarkerPosition);
				}
				else
				{
					Grid.SetRow(CurrentLocationBoxView, 0);
					Grid.SetRow(CurrentLocationLine, 0);
				}

				if (0 == ViewModel.LocationMarkerPosition)
				{
					ScrollRequested?.Invoke(this, new(ViewModel.LocationMarkerPosition * RowHeight.Value));
				}
				else if (1 <= ViewModel.LocationMarkerPosition)
				{
					ScrollRequested?.Invoke(this, new((ViewModel.LocationMarkerPosition - 1) * RowHeight.Value));
				}
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnViewModelLocationMarkerPositionChanged");
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
				EnsureRowDefinitions();
				AddSeparatorLines();

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
				int rowsCount = ViewModel.CurrentRows.Count;
				EnsureRowDefinitions();
				AddSeparatorLines();
				AfterArrive.SetRow(rowsCount + 1);
				Grid.SetRow(NextTrainButton, ViewModel.AfterArriveText is not null ? rowsCount + 2 : rowsCount + 1);
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
				EnsureRowDefinitions();
				AddSeparatorLines();
				int rowsCount = ViewModel.CurrentRows.Count;
				Grid.SetRow(NextTrainButton, ViewModel.AfterArriveText is not null ? rowsCount + 2 : rowsCount + 1);

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
			row.Model.MarkerBoxTapped(MarkerViewModel.SelectedColor, MarkerViewModel.SelectedText);
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnMarkerBoxClicked");
			Utils.ExitWithAlert(ex);
		}
	}
}
