using DependencyPropertyGenerator;

using TRViS.Controls;
using TRViS.DTAC.ViewModels;
using TRViS.IO.Models;
using TRViS.NetworkSyncService;
using TRViS.Services;
using TRViS.ValueConverters;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<TrainData>("SelectedTrainData")]
[DependencyProperty<string>("AffectDate")]
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

	VerticalTimetableView TimetableView { get; } = [];
	MyMap? DebugMap = null;

	DTACViewHostViewModel DTACViewHostViewModel { get; }
	TrainData? CurrentShowingTrainData { get; set; }

	public VerticalStylePage()
	{
		logger.Trace("Creating...");

		DTACViewHostViewModel = InstanceManager.DTACViewHostViewModel;
		DTACViewHostViewModel.PropertyChanged += (_, e) =>
		{
			try
			{
				switch (e.PropertyName)
				{
					case nameof(DTACViewHostViewModel.IsViewHostVisible):
					case nameof(DTACViewHostViewModel.IsVerticalViewMode):
						OnSelectedTrainDataChanged(SelectedTrainData);
						break;
				}
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalStylePage.DTACViewHostViewModel.PropertyChanged");
				Utils.ExitWithAlert(ex);
			}
		};

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

		InstanceManager.LocationService.OnGpsLocationUpdated += (_, e) =>
		{
			if (DebugMap is null || e is null)
			{
				return;
			}
			logger.Debug("OnGpsLocationUpdated: {0}", e);
			DebugMap.SetCurrentLocation(e.Latitude, e.Longitude, e.Accuracy ?? 20);
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

		TimetableView.IsBusyChanged += (s, _) =>
		{
			if (s is not VerticalTimetableView v)
				return;

			logger.Info("IsBusyChanged: {0}", v.IsBusy);

			try
			{
				if (v.IsBusy)
				{
					TimetableViewActivityIndicatorBorder.IsVisible = true;
					TimetableViewActivityIndicatorBorder.FadeTo(TimetableViewActivityIndicatorBorderMaxOpacity);
				}
				else
					TimetableViewActivityIndicatorBorder.FadeTo(0).ContinueWith((_) =>
					{
						logger.Debug("TimetableViewActivityIndicatorBorder.FadeTo(0) completed");
						TimetableViewActivityIndicatorBorder.IsVisible = false;
					});

				// iPhoneにて、画面を回転させないとScrollViewのDesiredSizeが正常に更新されないバグに対応するため
				if (Content is ScrollView sv)
				{
					double heightRequest = CONTENT_OTHER_THAN_TIMETABLE_HEIGHT + Math.Max(0, TimetableView.HeightRequest);
					logger.Debug("set full-scrollable-ScrollView.HeightRequest -> Max(this.HeightRequest: {0}, heightRequest: {1})", this.HeightRequest, heightRequest);
					sv.Content.HeightRequest = Math.Max(this.Height, heightRequest);
				}
			}
			catch (Exception ex)
			{
				logger.Fatal(ex, "Unknown Exception");
				InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalStylePage.TimetableView.IsBusyChanged");
				Utils.ExitWithAlert(ex);
			}
		};

		TimetableView.IgnoreSafeArea = false;
		TimetableView.VerticalOptions = LayoutOptions.Start;

		PageHeaderArea.IsRunningChanged += (_, e) =>
		{
			logger.Info("IsRunningChanged: {0}", e.NewValue);
			TimetableView.ViewModel.IsRunStarted = e.NewValue;
		};

		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone || DeviceInfo.Current.Idiom == DeviceIdiom.Unknown)
		{
			logger.Info("Device is Phone or Unknown -> set ScrollView to main grid");
			Grid.SetRow(TimetableView, Grid.GetRow(TimetableAreaScrollView));
			TimetableAreaScrollView.IsVisible = false;
			MainGrid.Add(TimetableView);
			TimetableView.ScrollView = Content as ScrollView;
		}
		else
		{
			logger.Info("Device is not Phone nor Unknown -> set TimetableView to TimetableAreaScrollView");
			TimetableAreaScrollView.Content = TimetableView;
			TimetableView.ScrollView = TimetableAreaScrollView;
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

		InstanceManager.LocationService.CanUseServiceChanged += (_, canUseLocationService) =>
		{
			logger.Info("CanUseLocationServiceChanged: {0}", canUseLocationService);
			PageHeaderArea.CanUseLocationService = canUseLocationService;
		};
		PageHeaderArea.CanUseLocationService = InstanceManager.LocationService.CanUseService;

		// WebSocket接続時に CanStart が true になったら自動で「運行開始」UI を反映する
		InstanceManager.LocationService.CanUseServiceChanged += (_, canUseService) =>
		{
			logger.Info("LocationService.CanUseServiceChanged: {0}", canUseService);
			// NetworkSyncService（WebSocket or HTTP）が使用されていて、かつ CanStart が true の場合
			if (InstanceManager.LocationService.NetworkSyncServiceCanStart)
			{
				logger.Info("CanStart is true and NetworkSyncService is being used -> automatically set IsRunning to true and enable location service");
				PageHeaderArea.IsRunning = true;
				TimetableView.ViewModel.IsLocationServiceEnabled = true;
			}
		};

		MaxSpeedLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;
		SpeedTypeLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;
		NominalTractiveCapacityLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;
		BeginRemarksLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;

		logger.Trace("Created");
	}

	private void OnIsLocationServiceEnabledChanged(object? sender, ValueChangedEventArgs<bool> e)
	{
		logger.Info("IsLocationServiceEnabledChanged: {0}", e.NewValue);
		PageHeaderArea.IsLocationServiceEnabled = e.NewValue;
		TimetableView.ViewModel.IsLocationServiceEnabled = e.NewValue;
		DebugMap?.SetIsLocationServiceEnabled(e.NewValue);
	}

	partial void OnSelectedTrainDataChanged(TrainData? newValue)
	{
		if (CurrentShowingTrainData == newValue)
		{
			logger.Debug("CurrentShowingTrainData == newValue -> do nothing");
			return;
		}
		if (!DTACViewHostViewModel.IsViewHostVisible || !DTACViewHostViewModel.IsVerticalViewMode)
		{
			logger.Debug("IsViewHostVisible: {0}, IsVerticalViewMode: {1} -> lazy load",
				DTACViewHostViewModel.IsViewHostVisible,
				DTACViewHostViewModel.IsVerticalViewMode
			);
			return;
		}

		try
		{
			CurrentShowingTrainData = newValue;
			logger.Info("SelectedTrainDataChanged: {0}", newValue);
			BindingContext = newValue;
			TimetableView.ViewModel.SetTrainData(newValue);
			InstanceManager.LocationService.SetTimetableRows(newValue?.Rows);
			MainThread.BeginInvokeOnMainThread(() =>
			{
				DebugMap?.SetTimetableRowList(newValue?.Rows);
			});
			PageHeaderArea.IsRunning = false;
			InstanceManager.DTACMarkerViewModel.IsToggled = false;

			MaxSpeedLabel.Text = ToWideConverter.Convert(newValue?.MaxSpeed);
			SpeedTypeLabel.Text = ToWideConverter.Convert(newValue?.SpeedType);
			NominalTractiveCapacityLabel.Text = ToWideConverter.Convert(newValue?.NominalTractiveCapacity);
			TrainInfo_BeforeDepartureArea.TrainInfoText = newValue?.TrainInfo ?? "";
			TrainInfo_BeforeDepartureArea.BeforeDepartureText = newValue?.BeforeDeparture ?? "";

			BeginRemarksLabel.Text = newValue?.BeginRemarks ?? "";

			SetDestinationString(newValue?.Destination);

			int dayCount = newValue?.DayCount ?? 0;
			this.IsNextDayLabel.IsVisible = dayCount > 0;
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "VerticalStylePage.OnSelectedTrainDataChanged");
			Utils.ExitWithAlert(ex);
		}
	}

	partial void OnAffectDateChanged(string? newValue)
	 => PageHeaderArea.AffectDateLabelText = newValue ?? "";

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

	string? _DestinationString = null;
	void SetDestinationString(string? value)
	{
		if (_DestinationString == value)
			return;

		_DestinationString = value;
		if (string.IsNullOrEmpty(value))
		{
			DestinationLabel.IsVisible = false;
			DestinationLabel.Text = null;
			return;
		}

		string dstStr = value;
		switch (value.Length)
		{
			case 1:
				dstStr = $"{Utils.SPACE_CHAR}{value}{Utils.SPACE_CHAR}";
				break;
			case 2:
				dstStr = $"{value[0]}{Utils.SPACE_CHAR}{value[1]}";
				break;
		}

		DestinationLabel.Text = $"（{dstStr}行）";
		DestinationLabel.IsVisible = true;
	}

	private void OnMayChangeDebugMapVisible()
	{
		bool isEnabled = InstanceManager.EasterEggPageViewModel.ShowMapWhenLandscape;
		bool isLandscape = DeviceDisplay.Current.MainDisplayInfo.Orientation == DisplayOrientation.Landscape;
		bool isVisible = isEnabled && isLandscape;
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
				DebugMapColumnDefinition.Width = new(0);
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
