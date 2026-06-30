#if IOS
using System.Runtime.Versioning;
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

		AutoAcceptPrivacyPolicy();

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
		this.SetBinding(BackgroundColorProperty, static (EasterEggPageViewModel vm) => vm.ShellBackgroundColor);
		this.SetBinding(TitleColorProperty, static (EasterEggPageViewModel vm) => vm.ShellTitleTextColor);

		FlyoutIconImage.BindingContext = easterEggPageViewModel;
		FlyoutIconImage.SetBinding(FontImageSource.ColorProperty, static (EasterEggPageViewModel vm) => vm.ShellTitleTextColor);

		// サーバーから HeaderColor コマンドを受信したときに、タイトルバー色を上書きする。
		// null (= ResetToDefault) の場合は EasterEgg の設定にフォールバックする。
		var appVm = InstanceManager.AppViewModel;
		appVm.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(AppViewModel.HeaderColorOverride_RGB))
				ApplyHeaderColorOverride(appVm.HeaderColorOverride_RGB, easterEggPageViewModel);
		};
		// 起動時の値も反映する (通常は null)
		ApplyHeaderColorOverride(appVm.HeaderColorOverride_RGB, easterEggPageViewModel);

		// サーバーから NavigateToHome コマンドを受信したときに、ホーム画面へ遷移する。
		// WebSocket 受信スレッドから呼ばれるため MainThread に dispatch する。
		appVm.NavigateToHomeRequested += (_, _) =>
			MainThread.BeginInvokeOnMainThread(() =>
				_ = GoToAsync("//" + nameof(StartHomePage)).ContinueWith(t =>
				{
					if (t.IsFaulted)
						logger.Error(t.Exception, "NavigateToHome GoToAsync failed");
				}, TaskScheduler.Default));

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

	static void AutoAcceptPrivacyPolicy()
	{
		var fvm = InstanceManager.FirebaseSettingViewModel;
		if (fvm.IsPrivacyPolicyAccepted)
			return;
		logger.Info("Internal build: auto-accepting privacy policy (no Firebase)");
		fvm.LastAcceptedPrivacyPolicyRevision = Constants.PRIVACY_POLICY_REVISION;
		AppPreferenceService.Set(AppPreferenceKeys.LastAcceptedPrivacyPolicyRevision, fvm.LastAcceptedPrivacyPolicyRevision);
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

	/// <summary>
	/// サーバー指示の色 (0xRRGGBB) でタイトルバーを上書きする。null の場合は
	/// EasterEgg ベースのバインディングを再有効化して端末設定に戻す。
	/// WebSocket 受信スレッドから呼ばれうるため、UI 操作は必ず MainThread に dispatch する。
	/// </summary>
	void ApplyHeaderColorOverride(int? rgbOrNull, EasterEggPageViewModel easterEggPageViewModel)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			if (rgbOrNull is int rgb)
			{
				byte r = (byte)((rgb >> 16) & 0xff);
				byte g = (byte)((rgb >> 8) & 0xff);
				byte b = (byte)(rgb & 0xff);
				this.RemoveBinding(BackgroundColorProperty);
				BackgroundColor = Color.FromRgb(r, g, b);
			}
			else
			{
				// バインディングを再設定して既定挙動に戻す
				this.SetBinding(BackgroundColorProperty, static (EasterEggPageViewModel vm) => vm.ShellBackgroundColor);
			}
		});
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
			SafeAreaMargin = new(
				UIWindow.SafeAreaInsets.Left.Value,
				UIWindow.SafeAreaInsets.Top.Value,
				UIWindow.SafeAreaInsets.Right.Value,
				UIWindow.SafeAreaInsets.Bottom.Value
			);
		}
	}
#endif
}

