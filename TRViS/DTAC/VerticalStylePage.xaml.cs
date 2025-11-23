using DependencyPropertyGenerator;

using TRViS.Controls;
using TRViS.DTAC.Logic;
using TRViS.IO.Models;
using TRViS.Services;
using TRViS.ValueConverters;
using TRViS.ViewModels;

namespace TRViS.DTAC;

// [DependencyProperty<TrainData>("SelectedTrainData")]
// [DependencyProperty<string>("AffectDate")]
public partial class VerticalStylePage : ContentView
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public const double DATE_AND_START_BUTTON_ROW_HEIGHT = 60;
	const double TRAIN_INFO_HEADER_ROW_HEIGHT = 54;
	const double TRAIN_INFO_ROW_HEIGHT = 54;
	const double TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT = DTACElementStyles.BeforeDeparture_AfterArrive_Height * 2;
	const double CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT = 60;
	const double TIMETABLE_HEADER_ROW_HEIGHT = 60;

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

	VerticalTimetableView TimetableView { get; } = new();
	MyMap? DebugMap = null;

	DTACViewHostViewModel DTACViewHostViewModel { get; }
	TrainData? CurrentShowingTrainData { get; set; }
	VerticalPageState PageState { get; set; } = VerticalPageStateFactory.CreateEmptyState();

	public VerticalStylePage()
	{
		logger.Trace("Creating...");

		DTACViewHostViewModel = InstanceManager.DTACViewHostViewModel;
		// DTACViewHostViewModel.PropertyChanged += (_, e) =>
		// {
		// 	try
		// 	{
		// 		switch (e.PropertyName)
		// 		{
		// 			case nameof(DTACViewHostViewModel.IsViewHostVisible):
		// 			case nameof(DTACViewHostViewModel.IsVerticalViewMode):
		// 				OnSelectedTrainDataChanged(SelectedTrainData);
		// 				break;
		// 		}
		// 	}
		// 	catch (Exception ex)
		// 	{
		// 		logger.Fatal(ex, "Unknown Exception");
		// 		InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalStylePage.DTACViewHostViewModel.PropertyChanged");
		// 		Utils.ExitWithAlert(ex);
		// 	}
		// };

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
		EasterEggPageViewModel eevm = InstanceManager.EasterEggPageViewModel;
		eevm.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(EasterEggPageViewModel.ShowMapWhenLandscape))
			{
				MainThread.BeginInvokeOnMainThread(OnMayChangeDebugMapVisible);
			}
		};

		DeviceDisplay.Current.MainDisplayInfoChanged += (_, e) =>
		{
			logger.Debug("MainDisplayInfoChanged: {0}", e.DisplayInfo);
			OnMayChangeDebugMapVisible();
		};

		// InstanceManager.LocationService.LocationStateChanged += (_, e) =>
		// {
		// 	logger.Debug("LocationStateChanged forwarded to TimetableView: Index[{0}]", e.NewStationIndex);
		// 	TimetableView.OnLocationServiceStateChanged(e);
		// };

		InstanceManager.LocationService.OnGpsLocationUpdated += (_, e) =>
		{
			if (DebugMap is null || e is null)
			{
				return;
			}
			logger.Debug("OnGpsLocationUpdated: {0}", e);

			// Update state with GPS location
			VerticalPageStateFactory.UpdateGpsLocation(
				PageState.LocationServiceState,
				latitude: e.Latitude,
				longitude: e.Longitude,
				accuracy: e.Accuracy
			);

			// Use updated state
			DebugMap.SetCurrentLocation(
				PageState.LocationServiceState.CurrentLatitude ?? 0,
				PageState.LocationServiceState.CurrentLongitude ?? 0,
				PageState.LocationServiceState.CurrentAccuracy ?? 20
			);
		};

		OnMayChangeDebugMapVisible();

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

		TimetableView.ScrollRequested += VerticalTimetableView_ScrollRequested;

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
				}
			};
		}

		// PageHeaderArea.IsLocationServiceEnabledChanged += OnIsLocationServiceEnabledChanged;

		MaxSpeedLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;
		SpeedTypeLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;
		NominalTractiveCapacityLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;
		BeginRemarksLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;

		logger.Trace("Created");
	}

	// private void OnIsLocationServiceEnabledChanged(object? sender, ValueChangedEventArgs<bool> e)
	// {
	// 	logger.Info("IsLocationServiceEnabledChanged: {0}", e.NewValue);

	// 	// Update state for location service
	// 	VerticalPageStateFactory.UpdateLocationServiceEnabledState(
	// 		PageState,
	// 		isEnabled: e.NewValue
	// 	);

	// 	// Apply to UI from state
	// 	PageHeaderArea.IsLocationServiceEnabled = PageState.PageHeaderState.IsLocationServiceEnabled;
	// 	DebugMap?.SetIsLocationServiceEnabled(PageState.LocationServiceState.IsEnabled);
	// }

	// partial void OnSelectedTrainDataChanged(TrainData? newValue)
	// {
	// 	if (CurrentShowingTrainData == newValue)
	// 	{
	// 		logger.Debug("CurrentShowingTrainData == newValue -> do nothing");
	// 		return;
	// 	}
	// 	if (!DTACViewHostViewModel.IsViewHostVisible || !DTACViewHostViewModel.IsVerticalViewMode)
	// 	{
	// 		logger.Debug("IsViewHostVisible: {0}, IsVerticalViewMode: {1} -> lazy load",
	// 			DTACViewHostViewModel.IsViewHostVisible,
	// 			DTACViewHostViewModel.IsVerticalViewMode
	// 		);
	// 		return;
	// 	}

	// 	try
	// 	{
	// 		VerticalTimetableView_ScrollRequested(this, new(0));
	// 		CurrentShowingTrainData = newValue;
	// 		logger.Info("SelectedTrainDataChanged: {0}", newValue);

	// 		// Create page state from train data using factory
	// 		PageState = VerticalPageStateFactory.CreateStateFromTrainData(
	// 			trainData: newValue,
	// 			affectDate: AffectDate,
	// 			isLocationServiceEnabled: PageHeaderArea.IsLocationServiceEnabled,
	// 			pageHeight: this.Height,
	// 			contentOtherThanTimetableHeight: CONTENT_OTHER_THAN_TIMETABLE_HEIGHT,
	// 			isPhoneIdiom: DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown
	// 		);

	// 		// Apply state to UI
	// 		ApplyPageState(PageState);

	// 		// Initialize TimetableView with train data
	// 		// This sets up location service state and row views
	// 		TimetableView.InitializeWithTrainData(newValue);

	// 		// Non-state operations
	// 		BindingContext = newValue;
	// 		MainThread.BeginInvokeOnMainThread(() =>
	// 		{
	// 			DebugMap?.SetTimetableRowList(newValue?.Rows);
	// 		});
	// 		PageHeaderArea.IsRunning = false;
	// 		InstanceManager.DTACMarkerViewModel.IsToggled = false;
	// 	}
	// 	catch (Exception ex)
	// 	{
	// 		logger.Fatal(ex, "Unknown Exception");
	// 		InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalStylePage.OnSelectedTrainDataChanged");
	// 		Utils.ExitWithAlert(ex);
	// 	}
	// }

	private void ApplyPageState(VerticalPageState state)
	{
		logger.Debug("Applying page state");

		// Apply destination state
		DestinationLabel.IsVisible = state.Destination.IsVisible;
		DestinationLabel.Text = state.Destination.Text;

		// Apply train display info
		MaxSpeedLabel.Text = ToWideConverter.Convert(state.TrainDisplayInfo.MaxSpeed);
		SpeedTypeLabel.Text = ToWideConverter.Convert(state.TrainDisplayInfo.SpeedType);
		NominalTractiveCapacityLabel.Text = ToWideConverter.Convert(state.TrainDisplayInfo.NominalTractiveCapacity);
		TrainInfo_BeforeDepartureArea.TrainInfoText = state.TrainInfoAreaState.TrainInfoText;
		TrainInfo_BeforeDepartureArea.BeforeDepartureText = state.TrainInfoAreaState.BeforeDepartureText;
		BeginRemarksLabel.Text = state.TrainDisplayInfo.BeginRemarks;

		// Apply next day indicator state
		IsNextDayLabel.IsVisible = state.NextDayIndicatorState.IsVisible;

		// Apply location service state
		PageHeaderArea.CanUseLocationService = state.PageHeaderState.CanUseLocationService;
	}

	// partial void OnAffectDateChanged(string? newValue)
	//  => PageHeaderArea.AffectDateLabelText = newValue ?? "";

	private async void VerticalTimetableView_ScrollRequested(object? sender, VerticalTimetableView.ScrollRequestedEventArgs e)
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
	}

	const string DateAndStartButton_AnimationName = nameof(DateAndStartButton_AnimationName);
	void BeforeRemarks_TrainInfo_OpenCloseChanged(object sender, ValueChangedEventArgs<bool> e)
	{
		bool isToOpen = e.NewValue;

		// Update state to reflect animation starting
		VerticalPageStateFactory.UpdateTrainInfoAreaOpenCloseState(
			PageState.TrainInfoAreaState,
			isToOpen: isToOpen
		);

		(double start, double end) = isToOpen
			? (TrainInfo_BeforeDepature_RowDefinition.Height.Value, TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT)
			: (TrainInfo_BeforeDepature_RowDefinition.Height.Value, 0d);
		logger.Info("BeforeRemarks_TrainInfo_OpenCloseChanged: {0} -> {1} / pos {2} -> {3}",
			e.OldValue,
			e.NewValue,
			start,
			end
		);

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
				PageState.TrainInfoAreaState.CurrentHeight = v;
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
					if (!canceled)
					{
						// Update state to reflect animation completion
						VerticalPageStateFactory.CompleteTrainInfoAreaAnimation(
							PageState.TrainInfoAreaState,
							wasOpenAnimation: isToOpen
						);

						if (!isToOpen)
						{
							logger.Debug("Animation Successfully finished to close");
							TrainInfo_BeforeDepartureArea.IsVisible = PageState.TrainInfoAreaState.IsVisible;
						}
						else
						{
							logger.Debug("Animation Successfully finished to open");
						}
					}
				}
			);
		logger.Debug("Animation started");
	}

	string? _DestinationString = null;
	void SetDestinationString(string? value)
	{
		if (_DestinationString == value)
			return;

		_DestinationString = value;

		var formatted = TRViS.DTAC.Logic.DestinationFormatter.FormatDestination(value);
		if (formatted is null)
		{
			DestinationLabel.IsVisible = false;
			DestinationLabel.Text = null;
			return;
		}

		DestinationLabel.Text = formatted;
		DestinationLabel.IsVisible = true;
	}

	private void OnMayChangeDebugMapVisible()
	{
		bool isEnabled = InstanceManager.EasterEggPageViewModel.ShowMapWhenLandscape;
		bool isLandscape = DeviceDisplay.Current.MainDisplayInfo.Orientation == DisplayOrientation.Landscape;

		// Update state with debug map visibility logic
		VerticalPageStateFactory.UpdateDebugMapState(
			PageState.DebugMapState,
			isEasterEggEnabled: isEnabled,
			isLandscape: isLandscape
		);

		bool isVisible = PageState.DebugMapState.IsVisible;
		logger.Debug("isEnabled: {0}, isLandscape: {1}, isVisible: {2}", isEnabled, isLandscape, isVisible);

		if (isVisible)
		{
			if (DebugMap is not null)
			{
				return;
			}
		}
		else
		{
			if (DebugMap is not null)
			{
				DebugMap.IsEnabled = false;
				DebugMapColumnDefinition.Width = new(PageState.DebugMapState.ColumnWidth);
				MainColumnDefinition.Width = new(1, GridUnitType.Star);
				MainGrid.Remove(DebugMap);
				DebugMap = null;
				logger.Debug("DebugMap removed");
			}
			return;
		}

		DebugMap = new MyMap();
		DebugMap.SetTimetableRowList(CurrentShowingTrainData?.Rows);
		DebugMap.SetIsLocationServiceEnabled(PageHeaderArea.IsLocationServiceEnabled);
		double mainWidth = 768;
		MainColumnDefinition.Width = new(mainWidth);
		DebugMapColumnDefinition.Width = new(1, GridUnitType.Star);
		MainGrid.Add(DebugMap, 1, 0);
		MainGrid.SetRowSpan(DebugMap, MainGrid.RowDefinitions.Count);
	}
}
