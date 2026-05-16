using TRViS.Controls;
using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Formatters;
using TRViS.DTAC.Logic.Presenter;
using TRViS.DTAC.ViewModels;
using TRViS.IO.Models;
using TRViS.Services;
using TRViS.Utils;
using TRViS.ValueConverters;
using TRViS.ValueConverters.DTAC;

namespace TRViS.DTAC;

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

	VerticalTimetableView TimetableView { get; }
	MyMap? DebugMap = null;
	private bool _isLandscape;

	// When true, the whole page is wrapped in a single outer ScrollView (the
	// "full scroll" experience). When false, only the timetable area scrolls
	// while the header stays fixed. This used to be keyed off DeviceIdiom.Phone;
	// it is now an explicit mode so the full-scroll variant lives on its own
	// dedicated page (#155) and the embedded ViewHost copy carries no
	// idiom-special-casing.
	private readonly bool _fullScroll;

	private readonly VerticalStylePagePresenter _presenter;
	private bool _isTimetableViewBusy = false;

	// Outer ScrollView wrapper, only created in full-scroll mode. Captured so the
	// train-data scroll-reset at OnPresenterStateChanged(All) can scroll the
	// user-facing scrollview back to top — in full-scroll mode the inner
	// TimetableAreaScrollView is hidden and resetting only it leaves the
	// PageHeader (and the 横型時刻表 button) scrolled out of view after a Work
	// switch when this page instance is reused across navigations.
	private ScrollView? _fullScrollOuterScrollView;

	// 直近に ApplyPresenterState(All) で TimetableView に流し込んだ TrainData の参照。
	// OnViewBecameActive (横型時刻表ページから戻った時など) は常に All を投げてくるので、
	// ここで前回と同じ参照かを見て不要な行再構築・スクロールリセットを抑止する。
	private IO.Models.TrainData? _lastAppliedTrainData = null;

	// Parameterless ctor is the one MAUI XAML uses (ViewHost embeds this via
	// <local:VerticalStylePage/>). It is the non-full-scroll, shared-presenter
	// variant. The dedicated full-scroll page calls the (presenter, fullScroll)
	// ctor explicitly with the same shared presenter so run / location state
	// stays consistent between the two surfaces.
	public VerticalStylePage()
		: this(InstanceManager.VerticalStylePagePresenter, fullScroll: false)
	{
	}

	public VerticalStylePage(VerticalStylePagePresenter presenter, bool fullScroll)
	{
		logger.Trace("Creating... (fullScroll: {0})", fullScroll);

		_fullScroll = fullScroll;
		_presenter = presenter;
		TimetableView = new VerticalTimetableView(_presenter);
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

		_isLandscape = DeviceDisplay.Current.MainDisplayInfo.Orientation == DisplayOrientation.Landscape;

		DeviceDisplay.Current.MainDisplayInfoChanged += OnMainDisplayInfoChanged;

		InstanceManager.EasterEggPageViewModel.PropertyChanged += OnEasterEggSettingChanged;

		InstanceManager.LocationServiceGpsAdapter.OnGpsLocationUpdated += OnGpsLocationUpdated;

		if (_fullScroll)
		{
			logger.Info("FullScroll mode -> make it to fill-scrollable");
			this.Content.VerticalOptions = LayoutOptions.Start;
			_fullScrollOuterScrollView = new ScrollView()
			{
				// Inner TimetableAreaScrollView is hidden in full-scroll mode;
				// expose this outer wrapper under the same id so UI tests can
				// locate the active scroll container regardless of mode.
				AutomationId = "DTAC.TimetableScrollView",
				Content = this.Content,
			};
			Content = _fullScrollOuterScrollView;
			DTACElementStyles.DefaultBGColor.Apply(Content, BackgroundColorProperty);
		}

		TimetableView.IsBusyChanged += (s, isBusy) =>
		{
			if (s is not VerticalTimetableView)
				return;

			logger.Info("IsBusyChanged: {0}", isBusy);

			try
			{
				_isTimetableViewBusy = isBusy;
				UpdateTimetableActivityIndicator();

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
				Util.ExitWithAlertAsync(ex);
			}
		};

		TimetableView.SafeAreaEdges = SafeAreaEdges.Default;
		TimetableView.VerticalOptions = LayoutOptions.Start;

		PageHeaderArea.StartButtonTappedCallback = _presenter.OnStartButtonClicked;
		PageHeaderArea.LocationServiceButtonTappedCallback = _presenter.OnLocationServiceToggled;

		// The full-scroll experience is iPhone-only (#155): on tablets the
		// non-full-scroll page already shows everything without needing the
		// separated page. Don't offer it while already on the full-scroll page.
		bool isPhoneIdiom = DeviceInfo.Current.Idiom == DeviceIdiom.Phone
			|| DeviceInfo.Current.Idiom == DeviceIdiom.Unknown;
		PageHeaderArea.IsFullScrollButtonVisible = isPhoneIdiom && !_fullScroll;

		if (_fullScroll)
		{
			logger.Info("FullScroll mode -> set TimetableView directly into main grid");
			Grid.SetRow(TimetableView, Grid.GetRow(TimetableAreaScrollView));
			TimetableAreaScrollView.IsVisible = false;
			MainGrid.Add(TimetableView);
		}
		else
		{
			logger.Info("Non-full-scroll mode -> set TimetableView to TimetableAreaScrollView");
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

		TimetableView.ScrollRequested += async (_, e) =>
		{
			if (!_fullScroll)
			{
				logger.Debug("Non-full-scroll mode -> scroll from {0} to {1}",
					TimetableAreaScrollView.ScrollY,
					e.PositionY);
				await TimetableAreaScrollView.ScrollToAsync(TimetableAreaScrollView.ScrollX, e.PositionY, true);
			}
			else
			{
				// Pre-existing behavior: GPS auto-scroll is not propagated to the
				// outer ScrollView in full-scroll mode. Kept intentionally as-is
				// (out of scope for #155).
				logger.Debug("FullScroll mode -> ScrollRequested no-op");
			}
		};

		TimetableView.RowTappedCallback = rowIndex =>
		{
			logger.Debug("UserRowTapped: rowIndex={0}", rowIndex);
			_presenter.OnRowTapped(rowIndex);
		};

		MaxSpeedLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;
		SpeedTypeLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;
		NominalTractiveCapacityLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;
		BeginRemarksLabel.CurrentAppThemeColorBindingExtension = DTACElementStyles.DefaultTextColor;

		UpdateDebugMapVisibility();

		InstanceManager.AppViewModel.PropertyChanged += OnAppViewModelPropertyChanged;
		UpdateHasHorizontalTimetable(InstanceManager.AppViewModel.SelectedWork);

		logger.Trace("Created");
	}

	void UpdateHasHorizontalTimetable(IO.Models.Work? work)
	{
		bool hasHorizontalTimetable = HorizontalTimetableContentBuilder.HasHorizontalTimetable(work);
		logger.Info("UpdateHasHorizontalTimetable: {0}", hasHorizontalTimetable);
		PageHeaderArea.HasHorizontalTimetable = hasHorizontalTimetable;
	}

	/// <summary>
	/// Called by ViewHost when the vertical tab becomes active.
	/// </summary>
	public void OnViewBecameActive()
	{
		ApplyPresenterState(VerticalPageStateSection.All);
		UpdateDebugMapVisibility();
	}

	private void OnMainDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
	{
		logger.Debug("MainDisplayInfoChanged: {0}", e.DisplayInfo);
		_isLandscape = e.DisplayInfo.Orientation == DisplayOrientation.Landscape;
		UpdateDebugMapVisibility();
	}

	private void OnGpsLocationUpdated(object? sender, Microsoft.Maui.Devices.Sensors.Location? e)
	{
		if (DebugMap is null || e is null)
			return;
		logger.Debug("OnGpsLocationUpdated: {0}", e);
		DebugMap.SetCurrentLocation(e.Latitude, e.Longitude, e.Accuracy ?? 20);
	}

	private void OnAppViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(TRViS.ViewModels.AppViewModel.SelectedWork))
			UpdateHasHorizontalTimetable(InstanceManager.AppViewModel.SelectedWork);
	}

	private void OnEasterEggSettingChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(TRViS.ViewModels.EasterEggPageViewModel.ShowMapWhenLandscape):
				UpdateDebugMapVisibility();
				break;
			case nameof(TRViS.ViewModels.EasterEggPageViewModel.KeepScreenOnWhenRunning):
				bool isRunning = _presenter.CurrentState.TimetableViewState.IsRunStarted;
				bool keepOn = InstanceManager.EasterEggPageViewModel.KeepScreenOnWhenRunning;
				if (isRunning && keepOn)
					InstanceManager.ScreenWakeLockService.EnableWakeLock();
				else
					InstanceManager.ScreenWakeLockService.DisableWakeLock();
				break;
		}
	}

	private void UpdateDebugMapVisibility()
	{
		bool isVisible = InstanceManager.EasterEggPageViewModel.ShowMapWhenLandscape && _isLandscape;
		MainThread.BeginInvokeOnMainThread(() => ApplyDebugMapState(isVisible));
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

			if (state.PageHeaderState.IsRunning && InstanceManager.EasterEggPageViewModel.KeepScreenOnWhenRunning)
				InstanceManager.ScreenWakeLockService.EnableWakeLock();
			else
				InstanceManager.ScreenWakeLockService.DisableWakeLock();
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

		if ((changed & VerticalPageStateSection.ActivityIndicator) != 0)
		{
			UpdateTimetableActivityIndicator();
		}

		// Apply scroll position on All change (train data changed).
		// 「列車自体が切り替わったか」は TrainData の参照ではなく Id で判定する。
		// WebSocket 経由のリアルタイム編集では、同一列車でも CacheTimetableData が
		// 都度新しい TrainData インスタンスを作る (= 参照は毎回変わる) ため、参照だけで
		// 判定するとリアルタイム編集のたびに「列車切替扱い」になってスクロール位置が
		// 先頭に飛んでしまう。Id 一致なら同じ列車として扱い、ユーザーの閲覧位置を維持する。
		//
		// SetTrainData 自体は常に呼ぶ。ViewModel 内の TimetableRebuildPolicy が
		// 「同 Id + 同行数 → field 単位の in-place 更新 (行 UI は dispose しない)」/
		// 「列車切替 or 行数変化 → 全面再構築」を切り分けるので、ここでは
		// 「列車自体が切り替わった時だけスクロール位置と DebugMap をリセットする」だけを担う。
		if (changed == VerticalPageStateSection.All)
		{
			var currentTrainData = _presenter.CurrentTrainData;
			string? newId = currentTrainData?.Id;
			string? oldId = _lastAppliedTrainData?.Id;
			bool isTrainSwitch = !string.Equals(newId, oldId, StringComparison.Ordinal);
			_lastAppliedTrainData = currentTrainData;

			if (isTrainSwitch)
			{
				MainThread.BeginInvokeOnMainThread(() =>
				{
					TimetableAreaScrollView.ScrollToAsync(0, 0, false);
					// In full-scroll mode the inner TimetableAreaScrollView is
					// hidden behind _fullScrollOuterScrollView, which is the actual
					// user-facing scroller. Reset its position too so a Work switch
					// returns the PageHeader (and the 横型時刻表 button) to the top
					// of the viewport instead of inheriting the previous Work's
					// scroll offset on a cached page instance.
					_fullScrollOuterScrollView?.ScrollToAsync(0, 0, false);
				});
			}

			TimetableView.ViewModel.SetTrainData(currentTrainData);
			DebugMap?.SetTimetableRowList(currentTrainData?.Rows);
		}
	}

	private void UpdateTimetableActivityIndicator()
	{
		bool isBusy = _isTimetableViewBusy;
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
	}

	private void ApplyDebugMapState(bool isVisible)
	{
		if (isVisible)
		{
			if (DebugMap is not null)
				return;

			DebugMap = new MyMap();
			DebugMap.SetTimetableRowList(_presenter.CurrentTrainData?.Rows);
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
}
