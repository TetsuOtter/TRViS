#if IOS
using System.Runtime.Versioning;
#endif

using System.Runtime.CompilerServices;

using TRViS.DTAC;
using TRViS.FirebaseWrapper;
using TRViS.RootPages;
using TRViS.Services;
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

		if (FirebaseSettingViewModel.IsEnabled)
		{
			GoToAsync("//" + nameof(SelectTrainPage)).ConfigureAwait(false);
		}
		else
		{
			GoToAsync("//" + nameof(FirebaseSettingPage)).ConfigureAwait(false);
		}
		InstanceManager.AnalyticsWrapper.Log(AnalyticsEvents.AppLaunched);

		FirebaseSettingViewModel.IsEnabledChanged += ApplyFlyoutBhavior;
		ApplyFlyoutBhavior(this, false, FirebaseSettingViewModel.IsEnabled);

		this.BindingContext = easterEggPageViewModel;
		this.SetBinding(BackgroundColorProperty, static (EasterEggPageViewModel vm) => vm.ShellBackgroundColor);
		this.SetBinding(TitleColorProperty, static (EasterEggPageViewModel vm) => vm.ShellTitleTextColor);

		FlyoutIconImage.BindingContext = easterEggPageViewModel;
		FlyoutIconImage.SetBinding(FontImageSource.ColorProperty, static (EasterEggPageViewModel vm) => vm.ShellTitleTextColor);

		InstanceManager.AppViewModel.WindowWidth = DeviceDisplay.Current.MainDisplayInfo.Width;
		InstanceManager.AppViewModel.WindowHeight = DeviceDisplay.Current.MainDisplayInfo.Height;
		logger.Trace("Display Width/Height: {0}x{1}", InstanceManager.AppViewModel.WindowWidth, InstanceManager.AppViewModel.WindowHeight);

		logger.Trace("AppShell Created");
	}

	void ApplyFlyoutBhavior(object? sender, bool oldValue, bool newValue)
	{
		logger.Trace("{0} -> {1}", oldValue, newValue);
		if (newValue == true)
		{
			FlyoutIcon = FlyoutIconImage;
			FlyoutBehavior = FlyoutBehavior.Flyout;
		}
		else
		{
			FlyoutIcon = null;
			FlyoutBehavior = FlyoutBehavior.Disabled;
			FlyoutIsPresented = false;
		}
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
			return;

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

		base.OnSizeAllocated(width, height);
	}
#endif
}

