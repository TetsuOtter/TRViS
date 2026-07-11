using System.ComponentModel;

using TR.Maui.AnchorPopover;

using TRViS.DTAC.Adapters;
using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Presenter;
using TRViS.IO.Models;
using TRViS.RootPages;
using TRViS.Services;
using TRViS.Utils;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class ViewHost : ContentPage
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public static readonly string NameOfThisClass = nameof(ViewHost);

	// Below this page width, QuickSwitchPopup falls back to the OS keyboard for train
	// search instead of the software numeric keypad (no room for keypad + results list
	// side by side) — covers iPhone portrait; tablets/landscape/desktop clear it easily.
	private const double NumericKeypadMinWidth = 500;

	DTACViewHostViewModel ViewModel { get; }

	private readonly ViewHostPresenter _presenter;
	private readonly DTACViewHostViewModel _dtacViewModel;
	private readonly AppViewModel _appViewModel;

	// 現在アクティブな通告バナーの Id → Entry。BannerRequested/BannerDismissed は
	// 複数の通告について前後して発火し得るため、最新の発火順ではなく「いま出すべき
	// 集合」を追跡し、表示中のものが消えたら残りのうち 1 件を表示し直す。UI スレッド専有。
	private readonly Dictionary<string, NotificationStore.Entry> _activeBanners = new();

	// 通告バナーの上下固定位置。既定は下部 (注意事項ヘッダーの少し上)。
	// 上/下スワイプ (NotificationBanner.SwipedUp/SwipedDown) で手動切り替えできる。
	private const double NotificationBannerSideMargin = 8;
	private const double NotificationBannerTopDockMargin = 8;
	private const double NotificationBannerBottomGapAboveRemarks = 8;
	private bool _isNotificationBannerDockedAtTop;
	private double _safeAreaBottom;

#if UI_TEST
	private AppViewModel? _testAppVm;
#endif

	public ViewHost()
	{
		logger.Trace("Creating...");

		_presenter = PresenterFactory.BuildViewHostPresenter(
			out AppViewModel vm,
			out _,
			out DTACViewHostViewModel dtacViewModel);

		_dtacViewModel = dtacViewModel;
		_appViewModel = vm;

#if UI_TEST
		_testAppVm = vm;
#endif

		_presenter.StateChanged += OnPresenterStateChanged;
		// On iOS/Windows ViewHost is a cached ShellContent (DataTemplate) — the same
		// instance is reused on every navigation. Disposing or unsubscribing on Unloaded
		// would permanently sever event subscriptions, so the second visit would freeze
		// the clock and stop updating the title (#240).
		// On Android (MAUI #16927 mitigation) ViewHost is a route-created page — a fresh
		// instance per navigation. An Unloaded cleanup is added under #if ANDROID to break
		// the references this instance holds to long-lived singletons (Shell.Navigated,
		// AppShell.SafeAreaMarginChanged, DTACViewHostViewModel.PropertyChanged) so old
		// instances can be GC'd after each visit.
		// HorizontalTimetablePage uses the same factory and is always RegisterRoute'd
		// (fresh per visit on all platforms), so its Unloaded+Dispose is unconditional.
		_appViewModel.OpenTimetableViewRequested += OnOpenTimetableViewRequested;

		// 通告 (Notification) の小型バナー。AppShell の DisplayRequested (大きいモーダル)
		// とは別に、NotificationCenter が直接発火する BannerRequested/BannerDismissed を
		// ここで購読する。同一シングルトンを AppShell も参照している。
		_appViewModel.NotificationCenter.BannerRequested += OnNotificationBannerRequested;
		_appViewModel.NotificationCenter.BannerDismissed += OnNotificationBannerDismissed;
		// 大型ポップアップ表示中は、小型バナーの受領ボタンの点滅を一時停止する。
		_appViewModel.NotificationCenter.PopupVisibilityChanged += OnNotificationPopupVisibilityChanged;

#if ANDROID
		Unloaded += (_, _) =>
		{
			Shell.Current.Navigated -= OnShellNavigated;
			_dtacViewModel.PropertyChanged -= OnDtacViewModelPropertyChanged;
			_appViewModel.OpenTimetableViewRequested -= OnOpenTimetableViewRequested;
			_appViewModel.NotificationCenter.BannerRequested -= OnNotificationBannerRequested;
			_appViewModel.NotificationCenter.BannerDismissed -= OnNotificationBannerDismissed;
			_appViewModel.NotificationCenter.PopupVisibilityChanged -= OnNotificationPopupVisibilityChanged;
			NotificationBanner.Tapped -= OnNotificationBannerTapped;
			NotificationBanner.AcknowledgeClicked -= OnNotificationBannerAcknowledgeClicked;
			NotificationBanner.SwipedUp -= OnNotificationBannerSwipedUp;
			NotificationBanner.SwipedDown -= OnNotificationBannerSwipedDown;
			HakoRemarksView.RemarksIsOpenChanged -= OnRemarksIsOpenChanged;
			VerticalStylePageRemarksView.RemarksIsOpenChanged -= OnRemarksIsOpenChanged;
			if (Shell.Current is AppShell appShellForCleanup)
				appShellForCleanup.SafeAreaMarginChanged -= AppShell_SafeAreaMarginChanged;
			_presenter.Dispose();
		};
#endif

		Shell.SetNavBarIsVisible(this, false);

		InitializeComponent();

		NotificationBanner.Tapped += OnNotificationBannerTapped;
		NotificationBanner.AcknowledgeClicked += OnNotificationBannerAcknowledgeClicked;
		NotificationBanner.SwipedUp += OnNotificationBannerSwipedUp;
		NotificationBanner.SwipedDown += OnNotificationBannerSwipedDown;
		HakoRemarksView.RemarksIsOpenChanged += OnRemarksIsOpenChanged;
		VerticalStylePageRemarksView.RemarksIsOpenChanged += OnRemarksIsOpenChanged;

		var state = _presenter.CurrentState;
		AppBarView.Title = state.TitleText;
		Title = state.TitleText;
		AppBarView.TimeLabelText = state.TimeLabelText;
		AppBarView.LeftButtonClicked += MenuButton_Clicked;
		AppBarView.TitleTapped += TitleLabel_Tapped;

		ViewModel = dtacViewModel;
		BindingContext = ViewModel;

		Shell.Current.Navigated += OnShellNavigated;

		_dtacViewModel.PropertyChanged += OnDtacViewModelPropertyChanged;

		HakoRemarksView.SetBinding(WithRemarksView.RemarksDataProperty, BindingBase.Create(static (AppViewModel vm) => vm.SelectedWork, source: vm));
		VerticalStylePageRemarksView.SetBinding(WithRemarksView.RemarksDataProperty, BindingBase.Create(static (AppViewModel vm) => vm.SelectedTrainData, source: vm));

		ApplyTabVisibility();

		if (Shell.Current is AppShell appShell)
		{
			appShell.SafeAreaMarginChanged += AppShell_SafeAreaMarginChanged;
			AppShell_SafeAreaMarginChanged(appShell, new(), appShell.SafeAreaMargin);
		}

		DTACElementStyles.DefaultBGColor.Apply(this, BackgroundColorProperty);

#if UI_TEST
		AddTestNavigateHomeSeam();
		AddTestStateSeams();
		ApplyTestStateSeams(_presenter.CurrentState);
		AddTestIsInfoRowTransitionSeam();
		AddTestConnectionStateSeams();
#endif

		logger.Trace("Created");
	}

#if UI_TEST
	// UI_TEST-only seam: invisible 24×24 button placed at the bottom-left corner
	// of the page (under any DTAC content but above the page background — the
	// last child in MainGrid means highest z-order). Tapping it issues
	// Shell.Current.GoToAsync("//StartHomePage") directly so shared-session
	// fixtures can return to Home from DTAC without the Shell flyout, which
	// is unreliable on Android once the VerticalView tab has locked
	// orientation to Landscape (CI run 25727806170: the MenuButton click
	// dispatches 200 OK but the NavigationView never attaches to the
	// DrawerLayout, so WaitForFlyoutItem times out 30 s later). GoToAsync away
	// from ViewHost triggers OnDisappearing which also unlocks the orientation.
	// Added in code-behind (not XAML) so production builds carry no seam at
	// all — important here because DTAC's bottom-left corner can be reached by
	// the user (no element occupies it in the test fixtures' state, but a
	// loaded real timetable could), and a transparent no-op button would
	// silently swallow taps in a production build.
	private void AddTestNavigateHomeSeam()
	{
		var seam = new Button
		{
			AutomationId = AutomationIdValueForTestNavigateHome,
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.End,
			WidthRequest = 24,
			HeightRequest = 24,
			BackgroundColor = Colors.Transparent,
			BorderColor = Colors.Transparent,
			Padding = 0,
			Margin = 0,
		};
		seam.Clicked += TestNavigateHomeButton_Clicked;
		Grid.SetRow(seam, 2);
		MainGrid.Children.Add(seam);
	}

	// Mirrors AutomationIds.DTAC.TestNavigateHomeButton in the test project
	// (which is the consumer). Inlined here to avoid a project reference.
	private const string AutomationIdValueForTestNavigateHome = "DTAC.TestNavigateHomeButton";

	// UI_TEST-only seams for the AppBar WebSocket status indicator (#266).
	// Three invisible 24×24 buttons stacked up the bottom-left strip above
	// NavigateHome(0)/TimeSeam(28)/TitleSeam(56). They mutate the singleton
	// AppViewModel's connection flags directly so the test can drive the
	// indicator through Connected→Disconnected→Reconnecting while on DTAC
	// (the only place the AppBar is shown) without a real WebSocket server.
	private const string AutomationIdValueForTestWsConnected = "DTAC.TestWsConnectedButton";
	private const string AutomationIdValueForTestWsDisconnected = "DTAC.TestWsDisconnectedButton";
	private const string AutomationIdValueForTestWsReconnecting = "DTAC.TestWsReconnectingButton";

	private void AddTestConnectionStateSeams()
	{
		MainGrid.Children.Add(BuildConnectionStateSeam(
			AutomationIdValueForTestWsConnected, bottomMarginPx: 84, (vm) =>
			{
				vm.IsServerReconnecting = false;
				vm.IsServerConnectionLost = false;
			}));
		MainGrid.Children.Add(BuildConnectionStateSeam(
			AutomationIdValueForTestWsDisconnected, bottomMarginPx: 112, (vm) =>
			{
				vm.IsServerReconnecting = false;
				vm.IsServerConnectionLost = true;
			}));
		MainGrid.Children.Add(BuildConnectionStateSeam(
			AutomationIdValueForTestWsReconnecting, bottomMarginPx: 140, (vm) =>
			{
				vm.IsServerReconnecting = true;
			}));
	}

	private static Button BuildConnectionStateSeam(string automationId, double bottomMarginPx, Action<AppViewModel> apply)
	{
		var seam = new Button
		{
			AutomationId = automationId,
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.End,
			WidthRequest = 24,
			HeightRequest = 24,
			BackgroundColor = Colors.Transparent,
			BorderColor = Colors.Transparent,
			Padding = 0,
			Margin = new Thickness(0, 0, 0, bottomMarginPx),
		};
		seam.Clicked += (_, _) =>
		{
			try
			{
				apply(InstanceManager.AppViewModel);
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Connection-state seam {0} failed", automationId);
			}
		};
		Grid.SetRow(seam, 2);
		return seam;
	}

	// UI_TEST-only state seams. The AppBar's TitleLabel / TimeLabel are MAUI
	// Labels; iOS only surfaces a Label in the accessibility tree when its
	// text is non-empty, and TimeLabel additionally hides itself on narrow
	// screens (width threshold in AppBar). Both make assertions over the
	// presenter's state flaky on iPhone-portrait. These seam labels mirror
	// state.TitleText and state.TimeLabelText with a sentinel prefix so they
	// are always non-empty (always findable), and are kept invisible to the
	// user via transparent text + zero size + InputTransparent. Tests strip
	// the sentinel before asserting.
	private const string AutomationIdValueForTestTitleSeam = "DTAC.TestTitleSeam";
	private const string AutomationIdValueForTestTimeSeam = "DTAC.TestTimeSeam";
	private const string TestTitleSeamPrefix = "T:";
	private const string TestTimeSeamPrefix = "C:";
	private Label _testTitleSeamLabel = null!;
	private Label _testTimeSeamLabel = null!;

	private void AddTestStateSeams()
	{
		// Stack above the existing TestNavigateHomeButton in the bottom-LEFT
		// corner. The bottom-left of row 2 is already established as the
		// reserved test-seam region (see AddTestNavigateHomeSeam) so production
		// controls (NextTrainButton at bottom-right of the timetable, the AppBar
		// AppIcon/Theme/Time stack on the right) are guaranteed not to compete
		// with these labels. TestNavigateHomeButton sits at margin 0; offset
		// these by 28/56 px so the three seams form a non-overlapping vertical
		// strip up the left edge.
		_testTimeSeamLabel = BuildSeamLabel(
			AutomationIdValueForTestTimeSeam,
			TestTimeSeamPrefix,
			bottomMarginPx: 28);
		_testTitleSeamLabel = BuildSeamLabel(
			AutomationIdValueForTestTitleSeam,
			TestTitleSeamPrefix,
			bottomMarginPx: 56);
		Grid.SetRow(_testTimeSeamLabel, 2);
		Grid.SetRow(_testTitleSeamLabel, 2);
		MainGrid.Children.Add(_testTimeSeamLabel);
		MainGrid.Children.Add(_testTitleSeamLabel);
	}

	private static Label BuildSeamLabel(string automationId, string initialText, double bottomMarginPx)
	{
		// iOS XCUITest sets accessible="true" only when isAccessibilityElement
		// is YES, which UILabel computes from frame size + text presence + alpha.
		// 1×1 with transparent text falls below the threshold and the element is
		// returned as accessible="false" (FindElement skips it). Match the
		// existing TestNavigateHomeButton 24×24 footprint with a non-zero
		// FontSize so the text drives a11y presence; TextColor=Transparent +
		// InputTransparent keep it invisible and click-through.
		return new Label
		{
			AutomationId = automationId,
			Text = initialText,
			TextColor = Colors.Transparent,
			BackgroundColor = Colors.Transparent,
			InputTransparent = true,
			FontSize = 8,
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.End,
			WidthRequest = 24,
			HeightRequest = 24,
			Margin = new Thickness(0, 0, 0, bottomMarginPx),
			Padding = 0,
		};
	}

	private void ApplyTestStateSeams(ViewHostPageState state)
	{
		_testTitleSeamLabel.Text = TestTitleSeamPrefix + (state.TitleText ?? string.Empty);
		_testTimeSeamLabel.Text = TestTimeSeamPrefix + (state.TimeLabelText ?? string.Empty);
	}

	// UI_TEST-only seam: invisible 24×24 button at the top-right of the main content area.
	// Tapping it modifies the first TimetableRow of the currently selected train from a
	// station row (IsInfoRow=false) to an info row (IsInfoRow=true), then re-sets
	// AppViewModel.SelectedTrainData with the modified clone. This exercises the same
	// WebSocket soft-update code path (same train ID → ApplyPositionAlignedDiff →
	// ApplyRowToExistingModel → PropertyChanged("IsInfoRow") → UpdateAllComponents).
	// Used to reproduce and verify the fix for "non-InfoRow components remain visible
	// after IsInfoRow false→true transition via WebSocket edit".
	private void AddTestIsInfoRowTransitionSeam()
	{
		var seam = new Button
		{
			AutomationId = AutomationIdValueForTestIsInfoRowTransition,
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Start,
			WidthRequest = 24,
			HeightRequest = 24,
			BackgroundColor = Colors.Transparent,
			BorderColor = Colors.Transparent,
			Padding = 0,
			Margin = 0,
		};
		seam.Clicked += TestIsInfoRowTransitionButton_Clicked;
		Grid.SetRow(seam, 2);
		MainGrid.Children.Add(seam);
	}

	private const string AutomationIdValueForTestIsInfoRowTransition = "DTAC.TestSeedIsInfoRowTransitionButton";

	void TestIsInfoRowTransitionButton_Clicked(object? sender, EventArgs e)
	{
		if (_testAppVm?.SelectedTrainData is not TrainData current || current.Rows is not { Length: > 0 } rows)
			return;

		// Find the first non-InfoRow and change it to IsInfoRow=true.
		int target = -1;
		for (int i = 0; i < rows.Length; i++)
		{
			if (!rows[i].IsInfoRow)
			{
				target = i;
				break;
			}
		}
		if (target < 0)
			return;

		TimetableRow[] modified = (TimetableRow[])rows.Clone();
		modified[target] = rows[target] with { IsInfoRow = true };

		// Re-assign via AppViewModel so the presenter soft-update path is exercised
		// (same train ID → canSoftUpdate=true → VerticalTimetableViewModel.SetTrainData).
		_testAppVm.SelectedTrainData = current with { Rows = modified };
		logger.Debug("TestIsInfoRowTransitionButton: changed row {0} to IsInfoRow=true", target);
	}
#endif

	private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
	{
		bool isCurrentPage = Shell.Current.CurrentPage is ViewHost;
		_dtacViewModel.IsViewHostVisible = isCurrentPage;
		if (isCurrentPage && _dtacViewModel.IsVerticalViewMode)
			MainThread.BeginInvokeOnMainThread(VerticalStylePageView.OnViewBecameActive);
	}

	private void AppShell_SafeAreaMarginChanged(object? sender, Thickness oldValue, Thickness newValue)
	{
		AppBarView.UpdateSafeAreaMargin(oldValue, newValue);
		_safeAreaBottom = newValue.Bottom;
		ApplyNotificationBannerDockPosition();
	}

	private void MenuButton_Clicked(object? sender, EventArgs e)
	{
		Shell.Current.FlyoutIsPresented = !Shell.Current.FlyoutIsPresented;
		logger.Debug("FlyoutIsPresented is changed to {0}", Shell.Current.FlyoutIsPresented);
	}

	// ---------- DTACViewModel event handling (tab visibility, orientation) ----------

	private void OnDtacViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(DTACViewHostViewModel.IsHakoMode):
			case nameof(DTACViewHostViewModel.IsVerticalViewMode):
			case nameof(DTACViewHostViewModel.IsWorkAffixMode):
				MainThread.BeginInvokeOnMainThread(ApplyTabVisibility);
				break;
			case nameof(DTACViewHostViewModel.TabMode):
				MainThread.BeginInvokeOnMainThread(UpdateOrientation);
				if (_dtacViewModel.IsVerticalViewMode)
					MainThread.BeginInvokeOnMainThread(VerticalStylePageView.OnViewBecameActive);
				break;
		}
	}

	private void ApplyTabVisibility()
	{
		HakoRemarksView.IsVisible = _dtacViewModel.IsHakoMode;
		VerticalStylePageRemarksView.IsVisible = _dtacViewModel.IsVerticalViewMode;
		WorkAffixView.IsVisible = _dtacViewModel.IsWorkAffixMode;

		if (!_dtacViewModel.IsHakoMode && HakoRemarksView.IsOpen)
			HakoRemarksView.IsOpen = false;
		if (!_dtacViewModel.IsVerticalViewMode && VerticalStylePageRemarksView.IsOpen)
			VerticalStylePageRemarksView.IsOpen = false;
	}

	private void UpdateOrientation()
	{
		if (DeviceInfo.Current.Idiom != DeviceIdiom.Phone)
		{
			InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.All);
			return;
		}

		AppDisplayOrientation desired = _dtacViewModel.TabMode switch
		{
			DTACViewHostViewModel.Mode.Hako => AppDisplayOrientation.Portrait,
			DTACViewHostViewModel.Mode.VerticalView => AppDisplayOrientation.Landscape,
			_ => AppDisplayOrientation.All,
		};
		InstanceManager.OrientationService.SetOrientation(desired);
	}

	// ---------- Notification banner handling ----------

	// BannerRequested は UI スレッド上で発火する契約だが、購読側の防御として
	// MainThread.BeginInvokeOnMainThread 経由で処理する (二重 dispatch は無害)。
	private void OnNotificationBannerRequested(object? sender, NotificationStore.Entry entry)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			if (entry.Id is string id && !string.IsNullOrEmpty(id))
				_activeBanners[id] = entry;

			// Id 無し・複数同時アクティブのいずれの場合も、直近の発火が最も新しいので
			// それをそのまま前面に出す (most-recent-wins)。
			NotificationBanner.Configure(entry);
			NotificationBanner.IsVisible = true;
		});
	}

	private void OnNotificationBannerDismissed(object? sender, string id)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			_activeBanners.Remove(id);

			if (NotificationBanner.CurrentId != id)
				return;

			// 表示中だったものが消えたので、残っているアクティブな通告があればそれに
			// 差し替える。無ければバナー自体を隠す。
			if (_activeBanners.Count > 0)
			{
				var next = _activeBanners.Values.First();
				NotificationBanner.Configure(next);
			}
			else
			{
				NotificationBanner.IsVisible = false;
			}
		});
	}

	/// <summary>
	/// <see cref="NotificationCenterViewModel.GetCurrentBanners"/> を引いて、いま表示すべき
	/// 通告バナーの集合を作り直す。OnAppearing から呼ぶことで、購読が途切れていた間
	/// (ViewHost 未生成中・Android の破棄区間) に発火した BannerRequested/BannerDismissed を
	/// 取りこぼしても、D-TAC 表示のたびに ViewModel 側の最新状態と再同期される。
	/// </summary>
	private void SyncBannersFromViewModel()
	{
		_activeBanners.Clear();
		NotificationStore.Entry? last = null;
		foreach (var entry in _appViewModel.NotificationCenter.GetCurrentBanners())
		{
			if (entry.Id is string id && !string.IsNullOrEmpty(id))
				_activeBanners[id] = entry;
			last = entry;
		}

		if (last is NotificationStore.Entry current)
		{
			NotificationBanner.Configure(current);
			NotificationBanner.IsVisible = true;
		}
		else
		{
			NotificationBanner.IsVisible = false;
		}

		// 購読が途切れていた間の PopupVisibilityChanged 取りこぼしにも備え、現在の状態を
		// 都度取り直して点滅の一時停止/再開を復元する。
		NotificationBanner.SetAcknowledgeBlinkPaused(_appViewModel.NotificationCenter.IsPopupVisible);
	}

	private void OnNotificationPopupVisibilityChanged(object? sender, bool isVisible)
	{
		MainThread.BeginInvokeOnMainThread(() => NotificationBanner.SetAcknowledgeBlinkPaused(isVisible));
	}

	/// <summary>
	/// 通告バナーを現在の固定位置 (<see cref="_isNotificationBannerDockedAtTop"/>) に配置する。
	/// 下部固定時は注意事項ヘッダー (<see cref="Remarks.HEADER_HEIGHT"/>) の少し上に浮かせる。
	/// スワイプでの手動切り替え・セーフエリア変化のいずれでも呼び直すため、常に
	/// TranslationY を 0 にリセットしてから Margin/VerticalOptions を確定させる
	/// (OnRemarksIsOpenChanged が付けた追従オフセットを引き継がないようにする)。
	/// </summary>
	private void ApplyNotificationBannerDockPosition()
	{
		NotificationBanner.TranslationY = 0;

		if (_isNotificationBannerDockedAtTop)
		{
			NotificationBanner.VerticalOptions = LayoutOptions.Start;
			NotificationBanner.Margin = new Thickness(
				NotificationBannerSideMargin, NotificationBannerTopDockMargin, NotificationBannerSideMargin, 0);
		}
		else
		{
			NotificationBanner.VerticalOptions = LayoutOptions.End;
			double bottomMargin = Remarks.HEADER_HEIGHT + NotificationBannerBottomGapAboveRemarks + _safeAreaBottom;
			NotificationBanner.Margin = new Thickness(
				NotificationBannerSideMargin, 0, NotificationBannerSideMargin, bottomMargin);
		}
	}

	private void OnNotificationBannerSwipedUp(object? sender, EventArgs e)
	{
		_isNotificationBannerDockedAtTop = true;
		ApplyNotificationBannerDockPosition();
	}

	private void OnNotificationBannerSwipedDown(object? sender, EventArgs e)
	{
		_isNotificationBannerDockedAtTop = false;
		ApplyNotificationBannerDockPosition();
	}

	/// <summary>
	/// アクティブなタブの注意事項が開閉されたとき、下部固定中の通告バナーを追従させる。
	/// 上部固定中 (手動でスワイプ済み) は既に注意事項と重ならないため何もしない。
	/// HakoRemarksView / VerticalStylePageRemarksView は非表示のタブでも IsOpen=false を
	/// 強制されて発火し得るので (ApplyTabVisibility 参照)、発火元が現在表示中かどうかで
	/// 実際に追従が必要かを絞り込む。
	/// </summary>
	private void OnRemarksIsOpenChanged(object? sender, bool isOpen)
	{
		if (_isNotificationBannerDockedAtTop)
			return;
		if (sender is not WithRemarksView view || !view.IsVisible)
			return;

		double targetTranslationY = isOpen ? -view.RemarksContentAreaHeight : 0;
		_ = NotificationBanner.TranslateToAsync(0, targetTranslationY, length: 250, easing: Easing.SinInOut);
	}

	private async void OnNotificationBannerTapped(object? sender, NotificationStore.Entry entry)
	{
		try
		{
			await Navigation.PushModalAsync(new NotificationPopupPage(entry, _appViewModel.NotificationCenter));
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Notification banner PushModalAsync failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "ViewHost.OnNotificationBannerTapped");
		}
	}

	private async void OnNotificationBannerAcknowledgeClicked(object? sender, NotificationStore.Entry entry)
	{
		// 受領後の非表示/切り替えは NotificationCenterViewModel が BannerDismissed /
		// BannerRequested の再発火で駆動するため、ここではバナーを自分で隠さない。
		try
		{
			await _appViewModel.NotificationCenter.AcknowledgeAsync(entry);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Notification banner AcknowledgeAsync failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "ViewHost.OnNotificationBannerAcknowledgeClicked");
		}
	}

	// ---------- Presenter state change handling ----------

	private void OnPresenterStateChanged(object? sender, ViewHostStateChangedEventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(() => ApplyPresenterState(e.Changed));
	}

	private void ApplyPresenterState(ViewHostStateSection changed)
	{
		var state = _presenter.CurrentState;

#if UI_TEST
		ApplyTestStateSeams(state);
#endif

		if ((changed & ViewHostStateSection.TitleText) != 0)
		{
			AppBarView.Title = state.TitleText;
			Title = state.TitleText;
		}

		if ((changed & ViewHostStateSection.TimeLabel) != 0)
		{
			AppBarView.TimeLabelText = state.TimeLabelText;
		}
	}

	// ---------- MAUI lifecycle overrides ----------

	protected override void OnAppearing()
	{
		base.OnAppearing();
		UpdateOrientation();
		if (_appViewModel.ConsumeOpenTimetableTabSwitchPending())
			_dtacViewModel.TabMode = DTACViewHostViewModel.Mode.VerticalView;

		// ViewHost が存在しない間 (Android は毎回破棄・再生成、そもそも D-TAC を開く前) に
		// BannerRequested/BannerDismissed が発火しても、購読者がいなければ通告バナーは
		// 表示されないまま失われてしまう。ここで ViewModel 側の最新状態
		// (GetCurrentBanners) を都度取り直して再構築することで、見逃しを防ぐ。
		SyncBannersFromViewModel();
	}

	// サーバーから OpenTimetable コマンドを受信し、かつ ViewHost が既にアクティブな場合に
	// 即座に時刻表タブへ切り替える。ホーム画面側から遷移してくる場合は OnAppearing で処理する。
	private void OnOpenTimetableViewRequested(object? sender, EventArgs _)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			if (!ReferenceEquals(Shell.Current?.CurrentPage, this))
				return;
			if (_appViewModel.ConsumeOpenTimetableTabSwitchPending())
				_dtacViewModel.TabMode = DTACViewHostViewModel.Mode.VerticalView;
		});
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone)
			InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.All);
		InstanceManager.ScreenWakeLockService.DisableWakeLock();
	}

#if UI_TEST
	async void TestNavigateHomeButton_Clicked(object? sender, EventArgs e)
	{
		logger.Info("TestNavigateHomeButton clicked: GoToAsync StartHomePage (bypassing flyout)");
		try
		{
			await Shell.Current.GoToAsync("//" + StartHomePage.NameOfThisClass);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestNavigateHomeButton failed");
		}
	}
#endif

	async void TitleLabel_Tapped(object? sender, EventArgs e)
	{
		try
		{
			logger.Info("TitleLabel tapped - showing QuickSwitchPopup");

			// Narrow screens (e.g. iPhone portrait) don't have room for a search-results
			// list plus a numeric keypad side by side, so fall back to the OS keyboard
			// there; wider screens (tablet, landscape, desktop) get the keypad.
			bool useNumericKeypad = Width >= NumericKeypadMinWidth;
			QuickSwitchPopup popup = new(useNumericKeypad);
			var popover = AnchorPopover.Create();
			// 検索結果確定時にポップオーバー自身を閉じられるよう参照を渡す。
			popup.SetPopover(popover);

			var options = new PopoverOptions
			{
				PreferredWidth = useNumericKeypad ? 420 : 320,
				PreferredHeight = 400,
				DismissOnTapOutside = true
			};

			await popover.ShowAsync(popup, AppBarView, options);
			logger.Trace("QuickSwitchPopup shown");
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			await Util.ExitWithAlertAsync(ex);
		}
	}
}
