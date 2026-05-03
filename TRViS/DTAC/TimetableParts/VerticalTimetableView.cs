using System.Collections.ObjectModel;
using System.ComponentModel;

using DependencyPropertyGenerator;

using Microsoft.Maui.Controls.Shapes;

using TRViS.DTAC.Adapters;
using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Formatters;
using TRViS.DTAC.Logic.Presenter;
using TRViS.DTAC.TimetableParts;
using TRViS.DTAC.ViewModels;
using TRViS.Services;
using TRViS.Utils;
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

	private readonly VerticalTimetableViewPresenter _presenter;
	private readonly LocationServiceAdapter _locationServiceAdapter;

	#endregion

	#region Properties

	public DTACMarkerViewModel MarkerViewModel { get; } = PresenterFactory.GetDTACMarkerViewModel();

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
				logger.Trace("IsBusy is changed to {0}", _isBusy);
				IsBusyChanged?.Invoke(this, _isBusy);
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				_presenter.LogException(ex, "VerticalTimetableView.OnIsBusyChanged");
				Util.ExitWithAlertAsync(ex);
			}
		}
	}

	#endregion

	#region Events

	public event EventHandler<bool>? IsBusyChanged;
	public event EventHandler<ScrollRequestedEventArgs>? ScrollRequested;

	public class UserRowTappedEventArgs(int rowIndex, bool isInfoRow, int totalRowCount) : EventArgs
	{
		public int RowIndex { get; } = rowIndex;
		public bool IsInfoRow { get; } = isInfoRow;
		public int TotalRowCount { get; } = totalRowCount;
	}

	public event EventHandler<UserRowTappedEventArgs>? UserRowTapped;

	#endregion

	#region Constructor

	public VerticalTimetableView()
	{
		logger.Trace("Creating...");

		_presenter = PresenterFactory.BuildVerticalTimetableViewPresenter();
		_locationServiceAdapter = PresenterFactory.GetLocationServiceAdapter();

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

		// Setup location service error handling via adapter (no InstanceManager)
		_locationServiceAdapter.ExceptionThrown += (s, e) =>
		{
			MainThread.BeginInvokeOnMainThread(() => Shell.Current.DisplayAlertAsync("Location Service Error", e.ToString(), "OK"));
		};

		// Subscribe to events
		_presenter.StateChanged += OnPresenterStateChanged;
		_presenter.ScrollRequested += OnPresenterScrollRequested;
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
			UserRowTapped?.Invoke(this, new UserRowTappedEventArgs(
				row.Model.RowIndex,
				row.Model.IsInfoRow,
				RowViewList.Count));
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			_presenter.LogException(ex, "VerticalTimetableView.RowTapped");
			Util.ExitWithAlertAsync(ex);
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
			_presenter.LogException(ex, "VerticalTimetableView.OnMarkerBoxClicked");
			Util.ExitWithAlertAsync(ex);
		}
	}

	#endregion

	#region Event Handlers - ViewModel Property Changes

	private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(ViewModel.CurrentRows):
				await OnViewModelCurrentRowsChangedAsync();
				break;
			case nameof(ViewModel.LocationMarkerState):
				_presenter.OnLocationMarkerStateChanged(ToTimetableLocationState(ViewModel.LocationMarkerState));
				break;
			case nameof(ViewModel.LocationMarkerPosition):
				_presenter.OnLocationMarkerPositionChanged(ViewModel.LocationMarkerPosition);
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
			_presenter.LogException(ex, "VerticalTimetableView.OnViewModelCurrentRowsChanged.SetRowViewsAsync");
			await Util.ExitWithAlertAsync(ex);
		}
	}

	private async void OnCurrentRowsCollectionChangedAsync(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		=> await OnViewModelCurrentRowsChangedAsync();

	private void OnViewModelAfterRemarksTextChanged()
	{
		_presenter.OnAfterRemarksTextChanged(ViewModel.AfterRemarksText is not null);

		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
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
				_presenter.LogException(ex, "VerticalTimetableView.OnViewModelAfterRemarksTextChanged");
				Util.ExitWithAlertAsync(ex);
			}
		});
	}

	private void OnViewModelAfterArriveTextChanged()
	{
		_presenter.OnAfterArriveTextChanged(ViewModel.AfterArriveText is not null);

		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
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
				_presenter.LogException(ex, "VerticalTimetableView.OnViewModelAfterArriveTextChanged");
				Util.ExitWithAlertAsync(ex);
			}
		});
	}

	private void OnViewModelNextTrainIdChanged()
	{
		_presenter.OnNextTrainIdChanged(ViewModel.NextTrainId is not null);

		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
				if (ViewModel.NextTrainId is not null)
				{
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
				_presenter.LogException(ex, "VerticalTimetableView.OnViewModelNextTrainIdChanged");
				Util.ExitWithAlertAsync(ex);
			}
		});
	}

	#endregion

	#region Event Handlers - Presenter

	private void OnPresenterStateChanged(object? sender, VerticalTimetableViewStateChangedEventArgs e)
		=> ApplyPresenterState(_presenter.CurrentState);

	private void OnPresenterScrollRequested(object? sender, int rowIndex)
	{
		if (rowIndex < 0)
			return;
		double positionY = rowIndex == 0
			? 0
			: (rowIndex - 1) * RowHeight.Value;
		ScrollRequested?.Invoke(this, new(positionY));
	}

	private void ApplyPresenterState(VerticalTimetableViewPageState state)
	{
		ViewModel.IsMarkingMode = state.IsMarkingMode;

		EnsureRowDefinitions();
		AddSeparatorLines();
		AfterRemarks.SetRow(state.AfterArriveRowIndex - 1);
		AfterArrive.SetRow(state.AfterArriveRowIndex);
		Grid.SetRow(NextTrainButton, state.NextTrainButtonRowIndex);

		bool prevBoxVisible = CurrentLocationBoxView.IsVisible;
		bool prevLineVisible = CurrentLocationLine.IsVisible;
		int prevRow = Grid.GetRow(CurrentLocationBoxView);

		CurrentLocationBoxView.IsVisible = state.Marker.IsBoxVisible;
		CurrentLocationLine.IsVisible = state.Marker.IsLineVisible;

		if (state.Marker.IsLineVisible)
			CurrentLocationBoxView.Margin = new(0, -(RowHeight.Value / 2));
		else
			CurrentLocationBoxView.Margin = new(0);

		int markerRow = Math.Max(0, state.Marker.MarkerRow);
		Grid.SetRow(CurrentLocationBoxView, markerRow);
		Grid.SetRow(CurrentLocationLine, markerRow);

		bool shouldHaptic = state.Marker.IsBoxVisible
			&& (prevBoxVisible != state.Marker.IsBoxVisible
				|| prevLineVisible != state.Marker.IsLineVisible
				|| (state.Marker.MarkerRow >= 0 && prevRow != state.Marker.MarkerRow));
		if (shouldHaptic)
			Util.PerformHaptic(HapticFeedbackType.Click);
	}

	private static TimetableLocationState ToTimetableLocationState(VerticalTimetableRowModel.LocationStates state)
		=> state switch
		{
			VerticalTimetableRowModel.LocationStates.AroundThisStation => TimetableLocationState.AroundThisStation,
			VerticalTimetableRowModel.LocationStates.RunningToNextStation => TimetableLocationState.RunningToNextStation,
			_ => TimetableLocationState.Undefined,
		};

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
					_presenter.LogException(ex, "VerticalTimetableView.SetRowViews");
					Util.ExitWithAlertAsync(ex);
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

					_presenter.OnRowsChanged(
						newValue?.Select(r => r.IsInfoRow).ToList() ?? [],
						ViewModel.AfterRemarksText is not null,
						ViewModel.AfterArriveText is not null,
						ViewModel.NextTrainId is not null);
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
				_presenter.LogException(ex, "VerticalTimetableView.SetRowViews (SetRowDefinitions etc failed)");
				await Util.ExitWithAlertAsync(ex);
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
				_presenter.LogException(ex, "VerticalTimetableView.SetRowViews (AddNewRow failed)");
				await Util.ExitWithAlertAsync(ex);
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
				_presenter.LogException(ex, "VerticalTimetableView.AddNewRow");
				Util.ExitWithAlertAsync(ex);
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
			_presenter.LogException(ex, "VerticalTimetableView.AddSeparatorLines");
			Util.ExitWithAlertAsync(ex);
		}
	}

	private void EnsureRowDefinitions()
	{
		int currentCount = RowDefinitions.Count;
		int rowCount = ViewModel.CurrentRows.Count;
		bool hasAfterArrive = ViewModel.AfterArriveText is not null;
		bool hasNextTrainButton = ViewModel.NextTrainId is not null;
		bool isPhone = DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown;
		logger.Debug("Count {0} -> {1}", currentCount, rowCount);

		if (rowCount < 0)
			throw new ArgumentOutOfRangeException(nameof(rowCount), "count must be 0 or more");

		int newCount = TimetableLayoutCalculator.CalculateRowDefinitionCount(
			rowCount,
			false,
			hasAfterArrive,
			hasNextTrainButton,
			isPhone,
			ScrollViewHeight,
			RowHeight.Value);

		HeightRequest = TimetableLayoutCalculator.CalculateGridHeightRequest(newCount, RowHeight.Value);
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
				_presenter.LogException(ex, "VerticalTimetableView.OnScrollViewHeightChanged(MainThread)");
				Util.ExitWithAlertAsync(ex);
			}
		});
	}

	#endregion
}
