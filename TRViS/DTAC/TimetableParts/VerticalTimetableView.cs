using System.Collections.ObjectModel;
using System.ComponentModel;

using DependencyPropertyGenerator;

using Microsoft.Maui.Controls.Shapes;

using TRViS.DTAC.TimetableParts;
using TRViS.DTAC.ViewModels;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<double>("ScrollViewHeight", DefaultValue = 0)]
public partial class VerticalTimetableView : Grid
{
	#region Nested Types

	public class ScrollRequestedEventArgs(double PositionY) : EventArgs
	{
		public double PositionY { get; } = PositionY;
	}

	#endregion

	#region Constants

	public static readonly Color CURRENT_LOCATION_MARKER_COLOR = new(0x00, 0x88, 0x00);
	public static readonly GridLength RowHeight = new(65);

	#endregion

	#region Fields

	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	private CancellationTokenSource? _currentSetRowViewsCancellationTokenSource = null;

	private bool _isBusy = false;
	private readonly BoxView CurrentLocationBoxView;
	private readonly BoxView CurrentLocationLine;
	private readonly AfterRemarks AfterRemarks;
	private readonly BeforeDeparture_AfterArrive AfterArrive;
	private readonly NextTrainButton NextTrainButton = [];
	private readonly List<VerticalTimetableRow> RowViewList = [];
	private readonly Line TopSeparatorLine;
	private readonly List<Line> SeparatorLines = [];

	#endregion

	#region Properties

	public DTACMarkerViewModel MarkerViewModel { get; } = InstanceManager.DTACMarkerViewModel;

	public VerticalTimetableColumnVisibilityState ColumnVisibilityState { get; } = new((int)DeviceDisplay.MainDisplayInfo.Width);

	public VerticalTimetableViewModel ViewModel { get; } = new();

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

	#endregion

	#region Events

	public event EventHandler<bool>? IsBusyChanged;
	public event EventHandler<ScrollRequestedEventArgs>? ScrollRequested;

	#endregion

	#region Constructor

	public VerticalTimetableView()
	{
		logger.Trace("Creating...");

		// Initialize location marker views
		CurrentLocationBoxView = new()
		{
			IsVisible = false,
			HeightRequest = RowHeight.Value,
			WidthRequest = DTACElementStyles.RUN_TIME_COLUMN_WIDTH,
			Margin = new(0),
			VerticalOptions = LayoutOptions.End,
			HorizontalOptions = LayoutOptions.Start,
			Color = CURRENT_LOCATION_MARKER_COLOR,
			InputTransparent = true,
			ZIndex = DTACElementStyles.TimetableRowLocationBoxZIndex,
		};

		CurrentLocationLine = new()
		{
			IsVisible = false,
			HeightRequest = 4,
			Margin = new(0, -2),
			VerticalOptions = LayoutOptions.End,
			Color = CURRENT_LOCATION_MARKER_COLOR,
			InputTransparent = true,
			ZIndex = DTACElementStyles.TimetableRowLocationBoxZIndex,
		};

		// Initialize after-row components
		AfterArrive = new(this, "着後");
		AfterRemarks = new(this);

		// Initialize separator line
		TopSeparatorLine = DTACElementStyles.TimetableRowHorizontalSeparatorLineStyle();

		// Setup grid layout
		Grid.SetColumnSpan(NextTrainButton, 8);
		DTACElementStyles.SetTimetableColumnWidthCollection(this);
		Grid.SetColumnSpan(CurrentLocationLine, 8);

		// Add views to children
		Children.Add(CurrentLocationBoxView);
		Children.Add(CurrentLocationLine);

		// Setup location service error handling
		InstanceManager.LocationService.ExceptionThrown += (s, e) =>
		{
			MainThread.BeginInvokeOnMainThread(() => Shell.Current.DisplayAlert("Location Service Error", e.ToString(), "OK"));
		};

		// Subscribe to events
		MarkerViewModel.PropertyChanged += OnMarkerViewModelPropertyChanged;
		ViewModel.PropertyChanged += OnViewModelPropertyChanged;
		ViewModel.CurrentRows.CollectionChanged += OnCurrentRowsCollectionChangedAsync;

		logger.Trace("Created");
	}

	#endregion

	#region Event Handlers - Row Interaction

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

	#endregion

	#region Event Handlers - ViewModel Property Changes

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

	private async void OnCurrentRowsCollectionChangedAsync(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		=> await OnViewModelCurrentRowsChangedAsync();

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
					Utils.PerformHaptic(HapticFeedbackType.Click);
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
					Children.Add(NextTrainButton);
				}
				else
				{
					Children.Remove(NextTrainButton);
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

	#endregion

	#region Row Management

	private async Task SetRowViewsAsync(ObservableCollection<VerticalTimetableRowModel>? newValue, CancellationToken cancellationToken)
	{
		logger.Info("Setting RowViews... (Current RowViewList.Count: {0})", RowViewList.Count);

		try
		{
			logger.Trace("Starting ClearOldRowViews Task...");
			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				try
				{
					logger.Trace("MainThread: Clearing old RowViews...");
					IsBusy = true;

					foreach (var rowView in RowViewList)
					{
						rowView.Dispose();
					}
					RowViewList.Clear();

					logger.Trace("MainThread: Clearing old RowViews Complete");
				}
				catch (Exception ex)
				{
					logger.Fatal(ex, "Unknown Exception");
					InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.SetRowViews");
					Utils.ExitWithAlert(ex);
				}
			});
			logger.Trace("ClearOldRowViews Task Complete");

			if (newValue is null || cancellationToken.IsCancellationRequested)
			{
				logger.Info("RowViews cleared, but newValue is null or operation was canceled -> exiting...");
				return;
			}

			int newCount = newValue?.Count ?? 0;
			logger.Debug("newCount: {0}", newCount);
			try
			{
				await MainThread.InvokeOnMainThreadAsync(() =>
				{
					EnsureRowDefinitions();
					AddSeparatorLines();
					AfterRemarks.SetRow(newCount);
					AfterArrive.SetRow(newCount + 1);
					Grid.SetRow(NextTrainButton, ViewModel.AfterArriveText is not null ? newCount + 2 : newCount + 1);
				});

				logger.Trace("Starting RowViewInit Task...");
			}
			catch (OperationCanceledException)
			{
				logger.Debug("SetRowViews was cancelled during SetRowDefinitions or AddSeparatorLines");
				return;
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.SetRowViews (SetRowDefinitions etc failed)");
				await Utils.ExitWithAlert(ex);
			}

			if (0 < PerformanceHelper.DelayBeforeSettingRowsMs)
				await Task.Delay(PerformanceHelper.DelayBeforeSettingRowsMs, cancellationToken);

			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				logger.Trace("Task: Finding last Station Row...");
				int lastTimetableRowIndex = 0;
				for (int i = 0; i < newCount; i++)
				{
					if (newValue is not null && !newValue[i].IsInfoRow)
						lastTimetableRowIndex = i;
				}

				logger.Trace("Task: last Station row is {0}, so Adding new RowViews...", lastTimetableRowIndex);
				int renderDelayMs = PerformanceHelper.RowRenderDelayMs;
				for (int i = 0; i < newCount; i++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					await AddNewRowAsync(newValue![i], i, i == lastTimetableRowIndex);
					await Task.Delay(renderDelayMs, cancellationToken);
				}
				logger.Trace("Task: RowViewInit Complete");
			}
			catch (OperationCanceledException)
			{
				logger.Debug("SetRowViews was cancelled during AddNewRow");
				return;
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.SetRowViews (AddNewRow failed)");
				await Utils.ExitWithAlert(ex);
			}
			cancellationToken.ThrowIfCancellationRequested();
			logger.Trace("RowViewInit Task Complete");

			logger.Info("RowViews are set");
		}
		finally
		{
			await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
		}
	}

	private Task AddNewRowAsync(VerticalTimetableRowModel? model, int index, bool isLastRow)
	{
		if (model is null)
		{
			logger.Trace("model is null -> skipping...");
			return Task.CompletedTask;
		}

		return MainThread.InvokeOnMainThreadAsync(() =>
		{
			logger.Debug("MainThread: Adding new Row (index: {0}, isLastRow: {1}, isInfoRow: {2}, Text: {3})",
				index,
				isLastRow,
				model.IsInfoRow,
				model.InfoText ?? model.StationName
			);

			try
			{
				VerticalTimetableRow rowView = new(this, model, ColumnVisibilityState, MarkerViewModel, isLastRow);
				rowView.RowTapped += RowTapped;
				rowView.MarkerBoxClicked += OnMarkerBoxClicked;

				RowViewList.Add(rowView);
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.AddNewRow");
				Utils.ExitWithAlert(ex);
			}
		});
	}

	#endregion

	#region Layout Management

	private void AddSeparatorLines()
	{
		logger.Trace("MainThread: Insert Separator Lines");

		try
		{
			bool isChildrenCleared = !Children.Contains(TopSeparatorLine);
			int initialSeparatorLinesListLength = SeparatorLines.Count;
			for (int i = initialSeparatorLinesListLength; i < RowDefinitions.Count; i++)
			{
				SeparatorLines.Add(DTACElementStyles.TimetableRowHorizontalSeparatorLineStyle());
			}
			for (int i = initialSeparatorLinesListLength - 1; RowDefinitions.Count <= i; i--)
			{
				Line line = SeparatorLines[i];
				SeparatorLines.RemoveAt(i);
				Children.Remove(line);
			}

			if (isChildrenCleared)
			{
				TopSeparatorLine.VerticalOptions = LayoutOptions.Start;
				DTACElementStyles.AddHorizontalSeparatorLineStyle(this, TopSeparatorLine, 0);
				for (int i = 0; i < SeparatorLines.Count; i++)
				{
					DTACElementStyles.AddHorizontalSeparatorLineStyle(this, SeparatorLines[i], i);
				}
			}
			else
			{
				for (int i = initialSeparatorLinesListLength; i < RowDefinitions.Count; i++)
				{
					DTACElementStyles.AddHorizontalSeparatorLineStyle(this, SeparatorLines[i], i);
				}
			}

			logger.Trace("MainThread: Insert Separator Lines Complete");
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.AddSeparatorLines");
			Utils.ExitWithAlert(ex);
		}
	}

	private void EnsureRowDefinitions()
	{
		int currentCount = RowDefinitions.Count;
		int newCount = ViewModel.CurrentRows.Count;
		bool hasAfterArrive = ViewModel.AfterArriveText is not null;
		bool hasNextTrainButton = ViewModel.NextTrainId is not null;
		logger.Debug("Count {0} -> {1}", currentCount, newCount);

		if (newCount < 0)
			throw new ArgumentOutOfRangeException(nameof(newCount), "count must be 0 or more");

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
		{
			// AfterRemarks
			newCount += 1;
			if (hasAfterArrive)
				newCount += 1;
			if (hasNextTrainButton)
				newCount += 1;
		}
		else
		{
			int minCount = (int)Math.Floor(ScrollViewHeight / RowHeight.Value);
			int additionalRowsCount = Math.Max(2, (int)Math.Ceiling(ScrollViewHeight / RowHeight.Value) - 2);
			logger.Debug("additionalRowsCount: {0}", additionalRowsCount);

			newCount += additionalRowsCount;
			newCount = Math.Max(minCount, newCount);
		}

		HeightRequest = newCount * RowHeight.Value;
		logger.Debug("HeightRequest: {0}", HeightRequest);

		if (newCount <= 0)
			RowDefinitions.Clear();
		else if (currentCount < newCount)
		{
			for (int i = RowDefinitions.Count; i < newCount; i++)
				RowDefinitions.Add(new(RowHeight));
		}
		else if (newCount < currentCount)
		{
			for (int i = RowDefinitions.Count - 1; i >= newCount; i--)
				RowDefinitions.RemoveAt(i);
		}
	}

	partial void OnScrollViewHeightChanged(double newValue)
	{
		logger.Debug("ScrollViewHeight: {0}", newValue);

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
			return;

		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
				EnsureRowDefinitions();
				AddSeparatorLines();
				logger.Debug("RowDefinitions.Count changed to: {0}", RowDefinitions.Count);
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalTimetableView.OnScrollViewHeightChanged(MainThread)");
				Utils.ExitWithAlert(ex);
			}
		});
	}

	#endregion
}
