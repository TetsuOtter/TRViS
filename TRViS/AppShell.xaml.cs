#if IOS
using System.Runtime.Versioning;
using CoreGraphics;
#endif

using System.Runtime.CompilerServices;

using TRViS.DTAC;
using TRViS.FirebaseWrapper;
using TRViS.Localization;
using TRViS.RootPages;
using TRViS.Services;
using TRViS.Utils;
using TRViS.ViewModels;

namespace TRViS;

public partial class AppShell : Shell
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	static public string AppVersionString
		=> $"Version: {AppInfo.Current.VersionString}-{AppInfo.Current.BuildString}";

	readonly FirebaseSettingViewModel FirebaseSettingViewModel = InstanceManager.FirebaseSettingViewModel;

	public AppShell()
	{
		logger.Trace("AppShell Creating");

		Routing.RegisterRoute(HorizontalTimetablePage.NameOfThisClass, typeof(HorizontalTimetablePage));
		logger.Info("Application Version: {0}", AppVersionString);

		EasterEggPageViewModel easterEggPageViewModel = InstanceManager.EasterEggPageViewModel;

		logger.Trace("Checking Firebase Setting");
		if (FirebaseSettingViewModel.IsEnabled)
		{
			logger.Trace("Firebase Applying...");
			FirebaseSettingViewModel.SaveAndApplySettings(false);
		}

		InitializeComponent();

#if ANDROID
		// MAUI #16927 mitigation: hosting DTAC as a cached ShellContent causes a
		// render-tree blank after navigation away. Remove the FlyoutItem, then
		// register ViewHost as a relative push route.
		// RegisterRoute MUST come after Items.Remove: AppShell.xaml's
		// FlyoutDTAC has Route="ViewHost", so InitializeComponent registers that
		// name into the Shell routing table. Items.Remove then un-registers it.
		// A RegisterRoute call placed before InitializeComponent would be silently
		// overridden by the XAML and then erased by Items.Remove.
		Items.Remove(FlyoutDTAC);
		Routing.RegisterRoute(TRViS.DTAC.ViewHost.NameOfThisClass, typeof(TRViS.DTAC.ViewHost));
#endif

		// Flyout/MenuItem Title binding refresh is unreliable in MAUI Shell, so
		// set them imperatively now and again whenever the UI language changes.
		ApplyLocalization();
		LocalizationResourceManager.Current.CultureChanged += (_, _) =>
			MainThread.BeginInvokeOnMainThread(ApplyLocalization);

		// Always launch into the Start/Home page. The Start screen handles the
		// privacy-policy-not-accepted case via an in-page banner + modal dialog
		// (PrivacyPolicyDialog), which also hosts the Firebase analytics opt-in.
		// The dedicated FirebaseSettingPage / Privacy / TPL flyout entries were
		// removed since Home now covers all three.
		// Fire-and-forget: the Shell ctor cannot be async; we discard the Task and
		// log via continuation so a navigation failure doesn't vanish.
		_ = GoToAsync("//" + nameof(StartHomePage)).ContinueWith(t =>
		{
			if (t.IsFaulted)
				logger.Error(t.Exception, "Initial GoToAsync(StartHomePage) failed");
		}, TaskScheduler.Default);
		InstanceManager.AnalyticsWrapper.Log(AnalyticsEvents.AppLaunched);

		// Always start with the flyout enabled. On Mac Catalyst the navigation
		// bar / flyout toggle button is created during Shell initialization based
		// on the current FlyoutBehavior — switching from Disabled→Flyout later
		// (when the user accepts privacy) does NOT re-create the navbar, leaving
		// the flyout unreachable for the rest of the session.
		// Privacy gating now happens at the *button* level inside StartHomePage
		// (Connect/SelectFile/Demo are disabled until accepted) and at Firebase
		// analytics opt-in, not at the Shell navigation level. Letting users tap
		// through to Settings / D-TAC before accepting is acceptable: D-TAC has
		// no committed selection so it shows nothing.
		FlyoutIcon = FlyoutIconImage;
		FlyoutBehavior = FlyoutBehavior.Flyout;

		this.BindingContext = easterEggPageViewModel;
		// EffectiveShellBackgroundColor/EffectiveShellTitleTextColor は、サーバーからの
		// HeaderColor コマンド (AppViewModel.HeaderColorOverride_RGB) と端末設定
		// (ShellBackgroundColor) を一本化した色源 (EasterEggPageViewModel 側で計算)。
		// DTAC の AppBar も同じ色源を参照するため、設定画面 (このネイティブタイトルバー)
		// と DTAC のヘッダ色は常に一致する (#310)。
		this.SetBinding(BackgroundColorProperty, static (EasterEggPageViewModel vm) => vm.EffectiveShellBackgroundColor);
		this.SetBinding(TitleColorProperty, static (EasterEggPageViewModel vm) => vm.EffectiveShellTitleTextColor);

		FlyoutIconImage.BindingContext = easterEggPageViewModel;
		FlyoutIconImage.SetBinding(FontImageSource.ColorProperty, static (EasterEggPageViewModel vm) => vm.EffectiveShellTitleTextColor);

		var appVm = InstanceManager.AppViewModel;

		// サーバーから NavigateToHome コマンドを受信したときに、ホーム画面へ遷移する。
		// WebSocket 受信スレッドから呼ばれるため MainThread に dispatch する。
		appVm.NavigateToHomeRequested += (_, _) =>
			MainThread.BeginInvokeOnMainThread(() =>
				_ = GoToAsync("//" + nameof(StartHomePage)).ContinueWith(t =>
				{
					if (t.IsFaulted)
						logger.Error(t.Exception, "NavigateToHome GoToAsync failed");
				}, TaskScheduler.Default));

		// サーバーから通告 (Notification) を受信し、未読と判定されたときにポップアップ表示する。
		// DisplayRequested は NotificationCenter 側で MainThread 上に発火するが、複数同時受信を
		// 直列化 (一度に 1 つだけモーダル表示) するためキューで管理する。
		appVm.NotificationCenter.DisplayRequested += OnNotificationDisplayRequested;
		// WebSocket 切断等で通告が一括破棄されたら、まだ表示していない待機列も空にする
		// (表示中のポップアップ自体は NotificationPopupPage が自分で閉じる)。
		appVm.NotificationCenter.Cleared += OnNotificationCenterCleared;
		// サーバーから個別の通告削除指示を受けたら、まだ表示していない待機列から該当 Id
		// だけを取り除く (表示中のポップアップ自体は NotificationPopupPage が自分で閉じる)。
		appVm.NotificationCenter.NotificationRemoved += OnNotificationCenterEntryRemoved;
		// 通告の受信音・接近音の再生要求 (#329)。NotificationCenter は MainThread 上で発火するが、
		// 実際の再生 (ネイティブ API 呼び出し) は失敗しても無音になるだけでよいため、
		// NotificationSoundPlayer 側ですべての例外を握りつぶす。
		appVm.NotificationCenter.SoundPlayRequested += (_, sound) => InstanceManager.NotificationSoundPlayer.Play(sound);

		InstanceManager.AppViewModel.WindowWidth = DeviceDisplay.Current.MainDisplayInfo.Width;
		InstanceManager.AppViewModel.WindowHeight = DeviceDisplay.Current.MainDisplayInfo.Height;
		logger.Trace("Display Width/Height: {0}x{1}", InstanceManager.AppViewModel.WindowWidth, InstanceManager.AppViewModel.WindowHeight);

		DeviceDisplay.Current.MainDisplayInfoChanged += (s, e) =>
		{
			InstanceManager.AppViewModel.WindowWidth = e.DisplayInfo.Width;
			InstanceManager.AppViewModel.WindowHeight = e.DisplayInfo.Height;
			logger.Trace("Display Width/Height Changed: {0}x{1}", InstanceManager.AppViewModel.WindowWidth, InstanceManager.AppViewModel.WindowHeight);
#if IOS
			UpdateSafeAreaMargin();
#endif
		};

#if IOS
		UpdateSafeAreaMargin();
#endif

		logger.Trace("AppShell Created");
	}

	// 通告ポップアップの表示直列化用。同時に複数受信しても一度に 1 つだけモーダル表示し、
	// 閉じられたら次を表示する。UI スレッド上でのみアクセスする。
	readonly Queue<Services.NotificationStore.Entry> _notificationQueue = new();
	bool _isShowingNotification;

	void OnNotificationCenterCleared(object? sender, EventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(() => _notificationQueue.Clear());
	}

	void OnNotificationCenterEntryRemoved(object? sender, string id)
	{
		// NotificationRemoved は UI スレッド上で発火する契約だが、キュー操作を確実に UI
		// スレッドで行うため dispatch する (二重 dispatch は無害)。
		MainThread.BeginInvokeOnMainThread(() =>
		{
			if (_notificationQueue.Count == 0)
				return;

			// Queue<T> は該当要素だけの削除を提供しないため、丸ごと作り直す。
			var remaining = new Queue<Services.NotificationStore.Entry>(_notificationQueue.Count);
			while (_notificationQueue.Count > 0)
			{
				var entry = _notificationQueue.Dequeue();
				if (entry.Id != id)
					remaining.Enqueue(entry);
			}
			while (remaining.Count > 0)
				_notificationQueue.Enqueue(remaining.Dequeue());
		});
	}

	void OnNotificationDisplayRequested(object? sender, Services.NotificationStore.Entry entry)
	{
		// DisplayRequested は MainThread 上で発火する契約だが、キュー操作を確実に UI
		// スレッドで行うため dispatch する (二重 dispatch は無害)。
		MainThread.BeginInvokeOnMainThread(() =>
		{
			_notificationQueue.Enqueue(entry);
			TryShowNextNotification();
		});
	}

	void TryShowNextNotification()
	{
		if (_isShowingNotification || _notificationQueue.Count == 0)
			return;

		_isShowingNotification = true;
		Services.NotificationStore.Entry entry = _notificationQueue.Dequeue();
		var page = new RootPages.NotificationPopupPage(entry, InstanceManager.AppViewModel.NotificationCenter);

		// 閉じられたら (受領 / 閉じる / OS ジェスチャ) 次の通告を表示する。
		void OnPopupDisappearing(object? s, EventArgs e)
		{
			page.Disappearing -= OnPopupDisappearing;
			_isShowingNotification = false;
			TryShowNextNotification();
		}
		page.Disappearing += OnPopupDisappearing;

		_ = PushNotificationModalAsync(page);
	}

	async Task PushNotificationModalAsync(Page page)
	{
		try
		{
			await Navigation.PushModalAsync(page);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Notification PushModalAsync failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "AppShell.PushNotificationModalAsync");
			// push に失敗すると Disappearing が来ないため、ここで表示状態を解除して次へ進む。
			// (失敗したページに残る Disappearing 購読は破棄されるので無害。)
			_isShowingNotification = false;
			TryShowNextNotification();
		}
	}

	/// <summary>
	/// Flyout / MenuItem のタイトルを現在の言語で再設定する。"D-TAC" は
	/// ブランド名のため翻訳しない。
	/// </summary>
	void ApplyLocalization()
	{
		// Firebase/Privacy/TPL のサイドバー項目は main 側のリファクタ
		// (7ece849) で削除済みのため、現存する Home / Settings のみ再設定する。
		FlyoutStartHome.Title = AppResources.Shell_Home;
		FlyoutSettings.Title = AppResources.Shell_Settings;
		MenuPrivacyPolicyOnline.Text = AppResources.Shell_PrivacyPolicyOnline;
	}

	protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		base.OnPropertyChanged(propertyName);

		switch (propertyName)
		{
			case nameof(Width):
				logger.Trace("Width: {0}", Width);
				InstanceManager.AppViewModel.WindowWidth = Width;
				break;
			case nameof(Height):
				logger.Trace("Height: {0}", Height);
				InstanceManager.AppViewModel.WindowHeight = Height;
				break;
		}
	}

	protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
	{
		InstanceManager.AppViewModel.WindowWidth = widthConstraint;
		InstanceManager.AppViewModel.WindowHeight = heightConstraint;
		logger.Trace("MeasureOverride: {0}x{1}", widthConstraint, heightConstraint);
		return base.MeasureOverride(widthConstraint, heightConstraint);
	}

	public event ValueChangedEventHandler<Thickness>? SafeAreaMarginChanged;
	Thickness _SafeAreaMargin;
	public Thickness SafeAreaMargin
	{
		get => _SafeAreaMargin;
		private set
		{
			if (_SafeAreaMargin == value)
				return;

			static string FormatThickness(Thickness t)
			{
				return $"(Left:{t.Left}, Top:{t.Top}, Right:{t.Right}, Bottom:{t.Bottom})";
			}
			logger.Info("SafeAreaMargin Changed: {0} -> {1}", FormatThickness(_SafeAreaMargin), FormatThickness(value));
			Thickness tmp = _SafeAreaMargin;
			_SafeAreaMargin = value;
			SafeAreaMarginChanged?.Invoke(this, tmp, value);
		}
	}

	/// <summary>
	/// Hooked up to <c>Microsoft.Maui.Controls.Window.SizeChanged</c> from
	/// App.xaml.cs. On iOS, MAUI's WindowHandler already KVO-observes
	/// UIWindowScene.EffectiveGeometry (in addition to UIWindow.Frame) and raises
	/// Window.SizeChanged whenever either changes -- this fires reliably for
	/// Stage Manager tile/float transitions and fullscreen toggles on iPadOS 26,
	/// unlike windowScene:didUpdateCoordinateSpace:, which requires MAUI's own
	/// UIWindowSceneDelegate to be active and never gets called because this app
	/// has no UIApplicationSceneManifest in Info.plist. Re-evaluating here keeps
	/// the window-control clearance current instead of only being set once at
	/// launch.
	/// </summary>
	public void NotifyWindowGeometryMayHaveChanged()
	{
#if IOS
		logger.Info("NotifyWindowGeometryMayHaveChanged (Window.SizeChanged)");
		UpdateSafeAreaMargin();
#endif
	}

#if IOS
	UIKit.UIWindow? UIWindow = null;

	[SupportedOSPlatform("ios13.0")]
	static UIKit.UIWindow? GetUIWindowOnIOS13OrLater()
	{
		if (UIKit.UIApplication.SharedApplication.ConnectedScenes.ToArray().FirstOrDefault(v => v is UIKit.UIWindowScene) is UIKit.UIWindowScene scene)
			return scene.Windows.FirstOrDefault();
		else
			return null;
	}

	[SupportedOSPlatform("ios")]
	[UnsupportedOSPlatform("ios15.0")]
	static UIKit.UIWindow? GetUIWindow()
	{
		return UIKit.UIApplication.SharedApplication.Windows.FirstOrDefault();
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		bool isIOS = OperatingSystem.IsIOS();
		logger.Info("OnSizeAllocated: {0}x{1} / ios:{2}", width, height, isIOS);
		if (!isIOS)
		{
			base.OnSizeAllocated(width, height);
			return;
		}

		UpdateSafeAreaMargin();
		base.OnSizeAllocated(width, height);
	}

	private void UpdateSafeAreaMargin()
	{
		// SafeAreaInsets ref: https://stackoverflow.com/questions/46829840/get-safe-area-inset-top-and-bottom-heights
		// ios15 >= ref: https://zenn.dev/paraches/articles/windows_was_depricated_in_ios15
		if (UIWindow is null)
		{
			UIWindow = OperatingSystem.IsIOSVersionAtLeast(13)
				? GetUIWindowOnIOS13OrLater()
				: GetUIWindow()
			;
			logger.Info("UIWindow: {0}", UIWindow is null ? "null" : UIWindow.ToString());
		}

		if (UIWindow is not null)
		{
			double left = UIWindow.SafeAreaInsets.Left.Value;
			double flyoutTopGap = 0;

			// iPadOS 26 draws macOS-style window controls (close/fullscreen/minimize) in
			// the top-left corner of windowed/resizable scenes. UIWindow.SafeAreaInsets
			// does not grow to avoid them; the corner-adaptation variants of the new
			// UIViewLayoutRegion API do, but they also report a few points of clearance
			// even when the scene fills the whole display and no controls are drawn at
			// all (confirmed on-device: a full-bleed screenshot showed a perfectly square
			// corner while the API still returned >0). There is no public API to ask "are
			// the controls currently visible", so only trust the corner-adaptation inset
			// while the window doesn't fill the screen (i.e. it's plausibly
			// tiled/floating) to avoid nudging the UI in the common full-screen case.
			// Compare by area, not width/height, since UIScreen.Bounds does not
			// necessarily rotate with the window's current orientation.
			// ref: https://developer.apple.com/videos/play/wwdc2025/282 (Make your UIKit app more flexible)
			if (OperatingSystem.IsIOSVersionAtLeast(26))
			{
				CGSize windowSize = UIWindow.Frame.Size;
				CGSize screenSize = (UIWindow.WindowScene?.Screen ?? UIKit.UIScreen.MainScreen).Bounds.Size;
				bool isTiledOrFloating = Math.Abs(windowSize.Width * windowSize.Height - screenSize.Width * screenSize.Height) > 1.0;
				double cornerAwareLeft = 0, cornerAwareTop = 0;
				if (isTiledOrFloating)
				{
					cornerAwareLeft = UIWindow.GetEdgeInsets(
						UIKit.UIViewLayoutRegion.CreateSafeAreaLayoutRegion(UIKit.UIViewLayoutRegionAdaptivityAxis.Horizontal)
					).Left;
					left = Math.Max(left, cornerAwareLeft);

					// The Flyout menu's "Home" entry starts right at the top of the
					// flyout panel, which puts it directly under the same window
					// controls. Nudge the flyout content down by the vertical
					// corner-adaptation clearance so it doesn't sit underneath them.
					cornerAwareTop = UIWindow.GetEdgeInsets(
						UIKit.UIViewLayoutRegion.CreateSafeAreaLayoutRegion(UIKit.UIViewLayoutRegionAdaptivityAxis.Vertical)
					).Top;
					flyoutTopGap = cornerAwareTop;
				}
				// TEMP diagnostic for #313 dynamic-update follow-up: remove once
				// confirmed on-device that this hook fires and these values are sane
				// across tile/float/fullscreen transitions. Info, not Debug/Trace,
				// because release builds' file logger filters below Info
				// (LoggerService.cs) and this needs to survive in a release-build
				// device log.
				logger.Info(
					"iOS26 window-control probe: windowSize={0}x{1} screenSize={2}x{3} isTiledOrFloating={4} cornerAwareLeft={5} cornerAwareTop={6}",
					windowSize.Width, windowSize.Height, screenSize.Width, screenSize.Height, isTiledOrFloating, cornerAwareLeft, cornerAwareTop
				);
			}

			if (FlyoutTopSpacer.HeightRequest != flyoutTopGap)
			{
				logger.Debug("FlyoutTopSpacer.HeightRequest: {0} -> {1}", FlyoutTopSpacer.HeightRequest, flyoutTopGap);
				FlyoutTopSpacer.HeightRequest = flyoutTopGap;
			}

			SafeAreaMargin = new(
				left,
				UIWindow.SafeAreaInsets.Top.Value,
				UIWindow.SafeAreaInsets.Right.Value,
				UIWindow.SafeAreaInsets.Bottom.Value
			);
		}
	}
#endif
}

