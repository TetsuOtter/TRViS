using DependencyPropertyGenerator;

using TRViS.Controls;
using TRViS.DTAC.Adapters;
using TRViS.DTAC.Logic.Presenter;
using TRViS.DTAC.ViewModels;
using TRViS.IO.Models;
using TRViS.Services;
using TRViS.Utils;
using TRViS.ValueConverters;
using TRViS.ValueConverters.DTAC;

namespace TRViS.DTAC;

[DependencyProperty<TrainData>("SelectedTrainData")]
[DependencyProperty<string>("AffectDate")]
public partial class VerticalStylePage : ContentView
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public const double DATE_AND_START_BUTTON_ROW_HEIGHT = 68;
	const double TRAIN_INFO_HEADER_ROW_HEIGHT = 58;
	const double TRAIN_INFO_ROW_HEIGHT = 58;
	const double TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT = DTACElementStyles.TRAIN_INFO_HEIGHT + DTACElementStyles.BEFORE_DEPARTURE_HEIGHT;
	const double CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT = 60;
	const double TIMETABLE_HEADER_ROW_HEIGHT = 65;

	RowDefinition TrainInfo_BeforeDepature_RowDefinition { get; } = new(0);
	ColumnDefinition MainColumnDefinition { get; } = new(new(1, GridUnitType.Star));
	ColumnDefinition DebugMapColumnDefinition { get; } = new(0);

	const double CONTENT_OTHER_THAN_TIMETABLE_HEIGHT
		= DATE_AND_START_BUTTON_ROW_HEIGHT
		+ TRAIN_INFO_HEADER_ROW_HEIGHT
		+ TRAIN_INFO_ROW_HEIGHT
		+ TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT
		+ CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT
		+ TIMETABLE_HEADER_ROW_HEIGHT;

	public static double TimetableViewActivityIndicatorBorderMaxOpacity { get; } = 0.6;

	VerticalTimetableView TimetableView { get; } = [];
	MyMap? DebugMap = null;

	private readonly VerticalStylePagePresenter _presenter;

	public VerticalStylePage()
	{
		logger.Trace("Creating...");

		// Build presenter - all InstanceManager references are inside PresenterFactory
		_presenter = PresenterFactory.Build();
		_presenter.StateChanged += OnPresenterStateChanged;

		InitializeComponent();

		DTACElementStyles.SetTimetableColumnWidthCollection(TrainBeforeRemarksArea);

		MainGrid.RowDefinitions = new(
			new(DATE_AND_START_BUTTON_ROW_HEIGHT),
			new(new(TRAIN_INFO_HEADER_ROW_HEIGHT)),
			new(new(TRAIN_INFO_ROW_HEIGHT)),
			TrainInfo_BeforeDepature_RowDefinition,
			new(new(CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT)),
			new(new(TIMETABLE_HEADER_ROW_HEIGHT)),
			new(new(1, GridUnitType.Star))
		);
		MainGrid.ColumnDefinitions = new(
			MainColumnDefinition,
			DebugMapColumnDefinition
		);

		DeviceDisplay.Current.MainDisplayInfoChanged += (_, e) =>
		{
			logger.Debug("MainDisplayInfoChanged: {0}", e.DisplayInfo);
			bool isLandscape = e.DisplayInfo.Orientation == DisplayOrientation.Landscape;
			_presenter.OnDeviceOrientationChanged(isLandscape);
		};

		// Initial orientation state
		_presenter.OnDeviceOrientationChanged(
			DeviceDisplay.Current.MainDisplayInfo.Orientation == DisplayOrientation.Landscape);

		InstanceManager.LocationServiceGpsAdapter.OnGpsLocationUpdated += (_, e) =>
		{
			if (DebugMap is null || e is null)
			{
				return;
			}
			logger.Debug("OnGpsLocationUpdated: {0}", e);
			DebugMap.SetCurrentLocation(e.Latitude, e.Longitude, e.Accuracy ?? 20);
		};

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
		{
			logger.Info("Device is Phone or Unknown -> make it to fill-scrollable");
			this.Content.VerticalOptions = LayoutOptions.Start;
			Content = new ScrollView()
			{
				Content = this.Content,
			};
			DTACElementStyles.DefaultBGColor.Apply(Content, BackgroundColorProperty);
		}

		TimetableView.IsBusyChanged += (s, isBusy) =>
		{
			if (s is not VerticalTimetableView v)
				return;

			logger.Info("IsBusyChanged: {0}", isBusy);

			try
			{
				if (isBusy)
				{
					TimetableViewActivityIndicatorBorder.IsVisible = true;
					TimetableViewActivityIndicatorBorder.FadeToAsync(TimetableViewActivityIndicatorBorderMaxOpacity);
				}
				else
				{
					TimetableViewActivityIndicatorBorder.FadeToAsync(0).ContinueWith((_) =>
					{
						MainThread.BeginInvokeOnMainThread(() =>
						{
							logger.Debug("TimetableViewActivityIndicatorBorder.FadeToAsync(0) completed");
							TimetableViewActivityIndicatorBorder.IsVisible = false;
						});
					});
				}

				// iPhoneにて、画面を回転させないとScrollViewのDesiredSizeが正常に更新されないバグに対応するため
				if (Content is ScrollView sv)
				{
					double heightRequest = CONTENT_OTHER_THAN_TIMETABLE_HEIGHT + Math.Max(0, TimetableView.HeightRequest);
					logger.Debug("set full-scrollable-ScrollView.HeightRequest -> Max(this.HeightRequest: {0}, heightRequest: {1})", this.HeightRequest, heightRequest);
					sv.Content.HeightRequest = Math.Max(this.Height, heightRequest);
				}

				_presenter.OnTimetableBusyChanged(isBusy, TimetableView.HeightRequest);
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				_presenter.CrashLog(ex, "VerticalStylePage.TimetableView.IsBusyChanged");
				Util.ExitWithAlertAsync(ex);
			}
		};

		TimetableView.SafeAreaEdges = SafeAreaEdges.Default;
		TimetableView.VerticalOptions = LayoutOptions.Start;

		PageHeaderArea.IsRunningChanged += (_, e) =>
		{
			logger.Info("IsRunningChanged: {0}", e.NewValue);
			_presenter.OnRunStartedChanged(e.NewValue);
		};

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
		{
			logger.Info("Device is Phone or Unknown -> set ScrollView to main grid");
			Grid.SetRow(TimetableView, Grid.GetRow(TimetableAreaScrollView));
			TimetableAreaScrollView.IsVisible = false;
			MainGrid.Add(TimetableView);
		}
		else
		{
			logger.Info("Device is not Phone nor Unknown -> set TimetableView to TimetableAreaScrollView");
			TimetableAreaScrollView.Content = TimetableView;
			TimetableAreaScrollView.PropertyChanged += (_, e) =>
			{
				// Bindingに失敗するため、代わり。
				if (e.PropertyName == nameof(TimetableView.Height))
				{
					logger.Debug("TimetableView.Height: {0}", TimetableView.HeightRequest);
					TimetableView.ScrollViewHeight = TimetableAreaScrollView.Height;
					_presenter.OnTimetableHeightChanged(TimetableView.HeightRequest, this.Height);
				}
			};
		}

		PageHeaderArea.IsLocationServiceEnabledChanged += OnIsLocationServiceEnabledChanged;

		TimetableView.ScrollRequested += async (_, e) =>
		{
			if (DeviceInfo.Current.Idiom != DeviceIdiom.Phone && DeviceInfo.Current.Idiom != DeviceIdiom.Unknown)
			{
				logger.Debug("Device is not Phone nor Unknown -> scroll from {0} to {1}",
					TimetableAreaScrollView.ScrollY,
					e.PositionY);
				await TimetableAreaScrollView.ScrollToAsync(TimetableAreaScrollView.ScrollX, e.PositionY, true);
			}
			else
			{
				logger.Debug("Device is Phone or Unknown -> do nothing");
			}
		};

		TimetableView.UserRowTapped += (_, e) =>
		{
			logger.Debug("UserRowTapped: rowIndex={0}, isInfoRow={1}, totalRowCount={2}", e.RowIndex, e.IsInfoRow, e.TotalRowCount);
			_presenter.OnRowTapped(e.RowIndex, e.IsInfoRow, e.TotalRowCount);
		};

		MaxSpeedLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;
		SpeedTypeLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;
		NominalTractiveCapacityLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;
		BeginRemarksLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;

		logger.Trace("Created");
	}

	private void OnPresenterStateChanged(object? sender, VerticalPageStateChangedEventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(() => ApplyPresenterState(e.Changed));
	}

	private void ApplyPresenterState(VerticalPageStateSection changed)
	{
		var state = _presenter.CurrentState;

		if ((changed & VerticalPageStateSection.Destination) != 0)
		{
			DestinationLabel.IsVisible = state.Destination.IsVisible;
			DestinationLabel.Text = state.Destination.Text;
		}

		if ((changed & VerticalPageStateSection.TrainDisplayInfo) != 0)
		{
			TrainNumberLabel.Text = TrainNumberConverter.Convert(state.TrainDisplayInfo.TrainNumber);
			bool hasCarCount = (state.TrainDisplayInfo.CarCount ?? 0) >= 1;
			CarCountBorder.IsVisible = hasCarCount;
			CarCountLabel.Text = hasCarCount ? state.TrainDisplayInfo.CarCount!.Value.ToString() : string.Empty;
			MaxSpeedLabel.Text = ToWideConverter.Convert(state.TrainDisplayInfo.MaxSpeed);
			SpeedTypeLabel.Text = ToWideConverter.Convert(state.TrainDisplayInfo.SpeedType);
			NominalTractiveCapacityLabel.Text = ToWideConverter.Convert(state.TrainDisplayInfo.NominalTractiveCapacity);
			BeginRemarksLabel.Text = state.TrainDisplayInfo.BeginRemarks;
		}

		if ((changed & VerticalPageStateSection.TrainInfoArea) != 0)
		{
			TrainInfo_BeforeDepartureArea.TrainInfoText = state.TrainInfoAreaState.TrainInfoText;
			TrainInfo_BeforeDepartureArea.BeforeDepartureText = state.TrainInfoAreaState.BeforeDepartureText;
		}

		if ((changed & VerticalPageStateSection.NextDayIndicator) != 0)
		{
			IsNextDayLabel.IsVisible = state.NextDayIndicatorState.IsVisible;
		}

		if ((changed & VerticalPageStateSection.PageHeader) != 0)
		{
			PageHeaderArea.AffectDateLabelText = state.PageHeaderState.AffectDateLabelText;
			PageHeaderArea.IsRunning = state.PageHeaderState.IsRunning;
			PageHeaderArea.IsLocationServiceEnabled = state.PageHeaderState.IsLocationServiceEnabled;
			PageHeaderArea.CanUseLocationService = state.PageHeaderState.CanUseLocationService;
		}

		if ((changed & VerticalPageStateSection.TimetableView) != 0)
		{
			TimetableView.ViewModel.IsRunStarted = state.TimetableViewState.IsRunStarted;
			TimetableView.ViewModel.IsLocationServiceEnabled = state.TimetableViewState.IsLocationServiceEnabled;
		}

		if ((changed & VerticalPageStateSection.LocationService) != 0)
		{
			DebugMap?.SetIsLocationServiceEnabled(state.LocationServiceState.IsEnabled);
			if (state.LocationServiceState.CurrentLatitude.HasValue && state.LocationServiceState.CurrentLongitude.HasValue)
			{
				DebugMap?.SetCurrentLocation(
					state.LocationServiceState.CurrentLatitude.Value,
					state.LocationServiceState.CurrentLongitude.Value,
					state.LocationServiceState.CurrentAccuracy ?? 20);
			}
		}

		if ((changed & VerticalPageStateSection.DebugMap) != 0)
		{
			ApplyDebugMapState(state.DebugMapState.IsVisible);
		}

		if ((changed & VerticalPageStateSection.RowStates) != 0)
		{
			ApplyRowStates(state);
		}

		// Apply scroll position on All change (train data changed)
		if (changed == VerticalPageStateSection.All)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				TimetableAreaScrollView.ScrollToAsync(0, 0, false);
			});
			TimetableView.ViewModel.SetTrainData(_lastTrainData);
			DebugMap?.SetTimetableRowList(_lastTrainData?.Rows);
		}
	}

	private void ApplyRowStates(TRViS.DTAC.Logic.VerticalPageState state)
	{
		// Find the active marker row
		int markerRow = -1;
		VerticalTimetableRowModel.LocationStates markerState = VerticalTimetableRowModel.LocationStates.Undefined;

		foreach (var kvp in state.RowStates)
		{
			if (kvp.Value.LocationState != 0)
			{
				markerRow = kvp.Key;
				markerState = kvp.Value.LocationState == 1
					? VerticalTimetableRowModel.LocationStates.AroundThisStation
					: VerticalTimetableRowModel.LocationStates.RunningToNextStation;
				break;
			}
		}

		TimetableView.ViewModel.LocationMarkerPosition = markerRow;
		TimetableView.ViewModel.LocationMarkerState = markerState;
	}

	private void ApplyDebugMapState(bool isVisible)
	{
		if (isVisible)
		{
			if (DebugMap is not null)
				return;

			DebugMap = new MyMap();
			DebugMap.SetTimetableRowList(_lastTrainData?.Rows);
			DebugMap.SetIsLocationServiceEnabled(PageHeaderArea.IsLocationServiceEnabled);
			double mainWidth = 768;
			MainColumnDefinition.Width = new(mainWidth);
			DebugMapColumnDefinition.Width = new(1, GridUnitType.Star);
			MainGrid.Add(DebugMap, 1, 0);
			MainGrid.SetRowSpan(DebugMap, MainGrid.RowDefinitions.Count);
		}
		else
		{
			if (DebugMap is not null)
			{
				DebugMap.IsEnabled = false;
				DebugMapColumnDefinition.Width = new(0);
				MainColumnDefinition.Width = new(1, GridUnitType.Star);
				MainGrid.Remove(DebugMap);
				DebugMap = null;
				logger.Debug("DebugMap removed");
			}
		}
	}

	TrainData? _lastTrainData = null;

	private void OnIsLocationServiceEnabledChanged(object? sender, ValueChangedEventArgs<bool> e)
	{
		logger.Info("IsLocationServiceEnabledChanged: {0}", e.NewValue);
		_presenter.OnLocationServiceEnabledChanged(e.NewValue);
	}

	partial void OnSelectedTrainDataChanged(TrainData? newValue)
	{
		_lastTrainData = newValue;
		_presenter.OnSelectedTrainDataChanged(newValue, AffectDate);
	}

	partial void OnAffectDateChanged(string? newValue)
	{
		_presenter.OnSelectedTrainDataChanged(SelectedTrainData, newValue);
	}

	const string DateAndStartButton_AnimationName = nameof(DateAndStartButton_AnimationName);
	void BeforeRemarks_TrainInfo_OpenCloseChanged(object sender, ValueChangedEventArgs<bool> e)
	{
		bool isToOpen = e.NewValue;
		(double start, double end) = isToOpen
			? (TrainInfo_BeforeDepature_RowDefinition.Height.Value, TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT)
			: (TrainInfo_BeforeDepature_RowDefinition.Height.Value, 0d);
		logger.Info("BeforeRemarks_TrainInfo_OpenCloseChanged: {0} -> {1} / pos {2} -> {3}",
			e.OldValue,
			e.NewValue,
			start,
			end
		);

		_presenter.OnTrainInfoOpenCloseToggled(isToOpen);

		if (this.AnimationIsRunning(DateAndStartButton_AnimationName))
		{
			logger.Debug("AbortAnimation({0})", DateAndStartButton_AnimationName);
			this.AbortAnimation(DateAndStartButton_AnimationName);
		}
		new Animation(
			v =>
			{
				if (!TrainInfo_BeforeDepartureArea.IsVisible)
				{
					logger.Debug("TrainInfo_BeforeDepartureArea.IsVisible set to true");
					TrainInfo_BeforeDepartureArea.IsVisible = true;
				}
				TrainInfo_BeforeDepature_RowDefinition.Height = v;
				TrainInfo_BeforeDepartureArea.HeightRequest = v;
				logger.Trace("v: {0}", v);
			},
			start,
			end,
			Easing.SinInOut
		)
			.Commit(
				this,
				DateAndStartButton_AnimationName,
				finished: (_, canceled) =>
				{
					_presenter.OnTrainInfoOpenCloseAnimationFinished(isToOpen, canceled);
					if (!isToOpen && !canceled)
					{
						logger.Debug("Animation Successfully finished to close");
						TrainInfo_BeforeDepartureArea.IsVisible = false;
					}
					else
					{
						logger.Debug("Animation Successfully finished to open or canceled");
					}
				}
			);
		logger.Debug("Animation started");
	}
}
