using TRViS.Controls;
using TRViS.DTAC.Adapters;
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

	// issue #41: 縦に短い画面 (iPad マルチタスク下段・小型端末横向き等) では
	// ヘッダ系の行を縮め、時刻表の表示領域を確保する。旧 feature/support-smartphone
	// ブランチの DTACRowDefinitionsProvider の Low モード (しきい値 800px / 各行の
	// 減少量) を、main の const ベース RowDefinitions 構成に移植したもの。
	const double SHORT_SCREEN_HEIGHT_THRESHOLD = 800;
	const double DATE_AND_START_BUTTON_ROW_HEIGHT_LOW = DATE_AND_START_BUTTON_ROW_HEIGHT - 6;
	const double TRAIN_INFO_HEADER_ROW_HEIGHT_LOW = TRAIN_INFO_HEADER_ROW_HEIGHT - 18;
	const double CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT_LOW = CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT - 12;
	const double TIMETABLE_HEADER_ROW_HEIGHT_LOW = TIMETABLE_HEADER_ROW_HEIGHT - 18;

	RowDefinition DateAndStartButtonRowDefinition { get; } = new(DATE_AND_START_BUTTON_ROW_HEIGHT);
	RowDefinition TrainInfoHeaderRowDefinition { get; } = new(TRAIN_INFO_HEADER_ROW_HEIGHT);
	RowDefinition CarCountRowDefinition { get; } = new(CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT);
	RowDefinition TimetableHeaderRowDefinition { get; } = new(TIMETABLE_HEADER_ROW_HEIGHT);
	RowDefinition TrainInfo_BeforeDepature_RowDefinition { get; } = new(0);
	ColumnDefinition MainColumnDefinition { get; } = new(new(1, GridUnitType.Star));
	ColumnDefinition DebugMapColumnDefinition { get; } = new(0);

	// issue #41: 列車情報ヘッダ (列車/最高速度/速度種別/けん引定数) を狭幅で可変化する。
	// 旧 main は Style_VerticalView.xaml の固定 ColumnDefinitionCollection を 2 つの
	// Grid で共有していたが、幅が狭いと最高速度/速度種別/けん引定数が見切れるため、
	// それらを 0 幅へ畳んで列車番号を Star 拡張する。表示判定は単一の真実源
	// (TimetableView.ColumnVisibilityState) を経由するので他の列と食い違わない。
	const double TRAIN_INFO_TRAIN_NUMBER_COLUMN_WIDTH = 270;
	const double TRAIN_INFO_MAX_SPEED_COLUMN_WIDTH = 130;
	const double TRAIN_INFO_NOMINAL_TRACTIVE_COLUMN_WIDTH = 168;
	ColumnDefinition[]? _trainInfoHeaderColumns;
	ColumnDefinition[]? _trainInfoValueColumns;

	enum ViewHeightMode { Normal, Low }
	ViewHeightMode _currentViewHeightMode = ViewHeightMode.Normal;

	// 全スクロール ScrollView の高さ算出に使う「時刻表以外の高さ」。
	// Low モードで縮んだ行高さを反映する (出発前行は従来どおり full 高で見積もる)。
	double ContentOtherThanTimetableHeight
		=> DateAndStartButtonRowDefinition.Height.Value
		+ TrainInfoHeaderRowDefinition.Height.Value
		+ TRAIN_INFO_ROW_HEIGHT
		+ TRAIN_INFO_BEFORE_DEPARTURE_ROW_HEIGHT
		+ CarCountRowDefinition.Height.Value
		+ TimetableHeaderRowDefinition.Height.Value;

	public static double TimetableViewActivityIndicatorBorderMaxOpacity { get; } = 0.6;

	VerticalTimetableView TimetableView { get; }
	MyMap? DebugMap = null;
	private bool _isLandscape;

	private readonly VerticalStylePagePresenter _presenter;
	private bool _isTimetableViewBusy = false;

	// Phone-only outer ScrollView wrapper. Captured so the train-data scroll-reset
	// at OnPresenterStateChanged(All) can scroll the user-facing scrollview back
	// to top — on phone the inner TimetableAreaScrollView is hidden and resetting
	// only it leaves the PageHeader (and the 横型時刻表 button) scrolled out of view
	// after a Work switch when this page instance is reused across navigations.
	private ScrollView? _phoneOuterScrollView;

	// 直近に ApplyPresenterState(All) で TimetableView に流し込んだ TrainData の参照。
	// OnViewBecameActive (横型時刻表ページから戻った時など) は常に All を投げてくるので、
	// ここで前回と同じ参照かを見て不要な行再構築・スクロールリセットを抑止する。
	private IO.Models.TrainData? _lastAppliedTrainData = null;

	public VerticalStylePage()
	{
		logger.Trace("Creating...");

		// Build presenter - all InstanceManager references are inside PresenterFactory
		_presenter = PresenterFactory.Build();
		TimetableView = new VerticalTimetableView(_presenter);
		_presenter.StateChanged += OnPresenterStateChanged;

		InitializeComponent();

		DTACElementStyles.SetTimetableColumnWidthCollection(TrainBeforeRemarksArea);

		MainGrid.RowDefinitions = new(
			DateAndStartButtonRowDefinition,
			TrainInfoHeaderRowDefinition,
			new(new(TRAIN_INFO_ROW_HEIGHT)),
			TrainInfo_BeforeDepature_RowDefinition,
			CarCountRowDefinition,
			TimetableHeaderRowDefinition,
			new(new(1, GridUnitType.Star))
		);

		// issue #41: ビュー高さに応じて Low/Normal の行高さを切り替える
		ApplyRowHeights(Height);
		SizeChanged += (_, _) => ApplyRowHeights(Height);

		// issue #41: 列車情報ヘッダの 4 列を可変化。表示判定は時刻表側と同じ
		// ColumnVisibilityState (単一の真実源) を購読する。
		_trainInfoHeaderColumns = BuildTrainInfoColumns();
		TrainInfoHeaderGrid.ColumnDefinitions = [
			_trainInfoHeaderColumns[0],
			_trainInfoHeaderColumns[1],
			_trainInfoHeaderColumns[2],
			_trainInfoHeaderColumns[3]
		];
		_trainInfoValueColumns = BuildTrainInfoColumns();
		TrainInfoValueGrid.ColumnDefinitions = [
			_trainInfoValueColumns[0],
			_trainInfoValueColumns[1],
			_trainInfoValueColumns[2],
			_trainInfoValueColumns[3]
		];
		TimetableView.ColumnVisibilityState.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(VerticalTimetableColumnVisibilityState.MaxSpeed))
				ApplyTrainInfoHeaderLayout(TimetableView.ColumnVisibilityState.MaxSpeed);
		};
		ApplyTrainInfoHeaderLayout(TimetableView.ColumnVisibilityState.MaxSpeed);

#if UI_TEST
		// issue #41: 幅→列表示の追従パスが動いていること & 表示判定が幅判定と
		// 食い違わないことを E2E で検証できるよう、現在の ViewWidthMode と各
		// 可視フラグを不可視ラベルにミラーする (TestTitleSeam と同じ手法)。
		AddColumnVisibilityTestSeam();
		TimetableView.ColumnVisibilityState.PropertyChanged += (_, _) => RefreshColumnVisibilityTestSeam();
		RefreshColumnVisibilityTestSeam();
#endif

		MainGrid.ColumnDefinitions = new(
			MainColumnDefinition,
			DebugMapColumnDefinition
		);

		_isLandscape = DeviceDisplay.Current.MainDisplayInfo.Orientation == DisplayOrientation.Landscape;

		DeviceDisplay.Current.MainDisplayInfoChanged += (_, e) =>
		{
			logger.Debug("MainDisplayInfoChanged: {0}", e.DisplayInfo);
			_isLandscape = e.DisplayInfo.Orientation == DisplayOrientation.Landscape;
			UpdateDebugMapVisibility();
		};

		InstanceManager.EasterEggPageViewModel.PropertyChanged += OnEasterEggSettingChanged;

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
			_phoneOuterScrollView = new ScrollView()
			{
				// Inner TimetableAreaScrollView is hidden on Phone; expose this
				// outer wrapper under the same id so UI tests can locate the
				// active scroll container regardless of idiom.
				AutomationId = "DTAC.TimetableScrollView",
				Content = this.Content,
			};
			Content = _phoneOuterScrollView;
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
					double heightRequest = ContentOtherThanTimetableHeight + Math.Max(0, TimetableView.HeightRequest);
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

		var appVm = InstanceManager.AppViewModel;
		appVm.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(TRViS.ViewModels.AppViewModel.SelectedWork))
				UpdateHasHorizontalTimetable(appVm.SelectedWork);
		};
		UpdateHasHorizontalTimetable(appVm.SelectedWork);

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
					// On phone the inner TimetableAreaScrollView is hidden behind
					// _phoneOuterScrollView, which is the actual user-facing scroller.
					// Reset its position too so a Work switch returns the PageHeader
					// (and the 横型時刻表 button) to the top of the viewport instead
					// of inheriting the previous Work's scroll offset on a cached
					// page instance.
					_phoneOuterScrollView?.ScrollToAsync(0, 0, false);
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

	// issue #41: ビュー高さがしきい値未満なら Low モードでヘッダ系の行を縮める。
	// 出発前行 (TrainInfo_BeforeDepature) は開閉アニメーションが高さを制御するため
	// ここでは触らない。モードが変わった時だけ反映し、レイアウト連鎖を抑える。
	void ApplyRowHeights(double viewHeight)
	{
		if (viewHeight <= 0)
			return;

		ViewHeightMode next = viewHeight < SHORT_SCREEN_HEIGHT_THRESHOLD
			? ViewHeightMode.Low
			: ViewHeightMode.Normal;
		if (next == _currentViewHeightMode)
			return;
		_currentViewHeightMode = next;
		logger.Debug("ViewHeightMode -> {0} (viewHeight={1})", next, viewHeight);

		bool low = next == ViewHeightMode.Low;
		DateAndStartButtonRowDefinition.Height = new(low ? DATE_AND_START_BUTTON_ROW_HEIGHT_LOW : DATE_AND_START_BUTTON_ROW_HEIGHT);
		TrainInfoHeaderRowDefinition.Height = new(low ? TRAIN_INFO_HEADER_ROW_HEIGHT_LOW : TRAIN_INFO_HEADER_ROW_HEIGHT);
		CarCountRowDefinition.Height = new(low ? CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT_LOW : CAR_COUNT_AND_BEFORE_REMARKS_ROW_HEIGHT);
		TimetableHeaderRowDefinition.Height = new(low ? TIMETABLE_HEADER_ROW_HEIGHT_LOW : TIMETABLE_HEADER_ROW_HEIGHT);
	}

	static ColumnDefinition[] BuildTrainInfoColumns() =>
	[
		new(new(TRAIN_INFO_TRAIN_NUMBER_COLUMN_WIDTH)),
		new(new(TRAIN_INFO_MAX_SPEED_COLUMN_WIDTH)),
		new(new(1, GridUnitType.Star)),
		new(new(TRAIN_INFO_NOMINAL_TRACTIVE_COLUMN_WIDTH)),
	];

	// issue #41: 列車情報ヘッダを表示できる幅が無い時は、最高速度/速度種別/けん引定数の
	// 列を 0 幅へ畳み、見出し・値・区切り線を非表示にして列車番号を Star 拡張する。
	void ApplyTrainInfoHeaderLayout(bool visible)
	{
		if (_trainInfoHeaderColumns is null || _trainInfoValueColumns is null)
			return;

		foreach (ColumnDefinition[] cols in new[] { _trainInfoHeaderColumns, _trainInfoValueColumns })
		{
			cols[0].Width = visible ? new(TRAIN_INFO_TRAIN_NUMBER_COLUMN_WIDTH) : new(1, GridUnitType.Star);
			cols[1].Width = visible ? new(TRAIN_INFO_MAX_SPEED_COLUMN_WIDTH) : new(0);
			cols[2].Width = visible ? new(1, GridUnitType.Star) : new(0);
			cols[3].Width = visible ? new(TRAIN_INFO_NOMINAL_TRACTIVE_COLUMN_WIDTH) : new(0);
		}

		MaxSpeedHeaderLabel.IsVisible = SpeedTypeHeaderLabel.IsVisible = NominalTractiveHeaderLabel.IsVisible = visible;
		MaxSpeedLabel.IsVisible = SpeedTypeLabel.IsVisible = NominalTractiveCapacityLabel.IsVisible = visible;
		TrainInfoHeaderSep0.IsVisible = TrainInfoHeaderSep1.IsVisible = TrainInfoHeaderSep2.IsVisible = visible;
		TrainInfoValueSep0.IsVisible = TrainInfoValueSep1.IsVisible = TrainInfoValueSep2.IsVisible = visible;
		logger.Debug("TrainInfoHeader layout -> visible={0}", visible);
	}

#if UI_TEST
	// UI_TEST-only seam: mirrors the timetable's responsive state (issue #41)
	// into an invisible always-non-empty Label so an Appium test can assert,
	// device-independently, that the width→visibility path ran and that the
	// flags never drift from the ViewWidthMode. Same invisibility technique as
	// ViewHost's TestTitleSeam (transparent text + tiny size + InputTransparent).
	private const string TestColumnVisibilitySeamId = "DTAC.TestColumnVisibilitySeam";
	private const string TestColumnVisibilitySeamPrefix = "RV41|";
	private Label? _testColumnVisibilitySeamLabel;

	private void AddColumnVisibilityTestSeam()
	{
		_testColumnVisibilitySeamLabel = new Label
		{
			AutomationId = TestColumnVisibilitySeamId,
			Text = TestColumnVisibilitySeamPrefix,
			TextColor = Colors.Transparent,
			BackgroundColor = Colors.Transparent,
			InputTransparent = true,
			FontSize = 8,
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.End,
			WidthRequest = 24,
			HeightRequest = 24,
			Margin = new Thickness(0, 0, 0, 84),
			Padding = 0,
		};
		Grid.SetRow(_testColumnVisibilitySeamLabel, MainGrid.RowDefinitions.Count - 1);
		MainGrid.Children.Add(_testColumnVisibilitySeamLabel);
	}

	private void RefreshColumnVisibilityTestSeam()
	{
		if (_testColumnVisibilitySeamLabel is null)
			return;
		var s = TimetableView.ColumnVisibilityState;
		static int B(bool v) => v ? 1 : 0;
		// w = the exact width that drove CurrentMode (same VerticalTimetableView
		// instance whose SizeChanged calls ColumnVisibilityState.UpdateState).
		// The E2E asserts ClassifyWidth(w) == mode, so a stuck/seed-only mode
		// (the #41 regression) is detectable independently of the device.
		_testColumnVisibilitySeamLabel.Text =
			$"{TestColumnVisibilitySeamPrefix}mode={s.CurrentMode}|w={(int)TimetableView.Width}" +
			$"|rt={B(s.RunTime)}|rl={B(s.RunInOutLimit)}|rm={B(s.Remarks)}" +
			$"|mk={B(s.Marker)}|snn={B(s.IsStationNameNarrow)}|tnn={B(s.IsTrackNameNarrow)}";
	}
#endif

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
