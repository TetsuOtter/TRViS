#if IOS
using System.Runtime.Versioning;
#endif

using System.Runtime.CompilerServices;
using TRViS.RootPages;
using TRViS.ViewModels;

namespace TRViS;

public partial class AppShell : Shell
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	static public string AppVersionString
		=> $"Version: {AppInfo.Current.VersionString}-{AppInfo.Current.BuildString}";

	readonly AppCenterSettingViewModel AppCenterSettingViewModel =  InstanceManager.AppCenterSettingViewModel;

	public AppShell()
	{
		logger.Trace("AppShell Creating");
		logger.Info("Application Version: {0}", AppVersionString);

		EasterEggPageViewModel easterEggPageViewModel = InstanceManager.EasterEggPageViewModel;

		logger.Trace("Checking AppCenter Setting");
		if (AppCenterSettingViewModel.IsEnabled)
		{
			logger.Trace("AppCenter Applying...");
			AppCenterSettingViewModel.SaveAndApplySettings(false).ConfigureAwait(false);
		}

		InitializeComponent();

		if (AppCenterSettingViewModel.IsEnabled)
		{
			GoToAsync("//" + nameof(SelectTrainPage)).ConfigureAwait(false);
		}
		else
		{
			GoToAsync("//" + nameof(AppCenterSettingPage)).ConfigureAwait(false);
		}

		AppCenterSettingViewModel.IsEnabledChanged += ApplyFlyoutBhavior;
		ApplyFlyoutBhavior(this, false, AppCenterSettingViewModel.IsEnabled);

		SetBinding(Shell.BackgroundColorProperty, new Binding() { Source = easterEggPageViewModel, Path = nameof(EasterEggPageViewModel.ShellBackgroundColor) });
		SetBinding(Shell.TitleColorProperty, new Binding() { Source = easterEggPageViewModel, Path = nameof(EasterEggPageViewModel.ShellTitleTextColor) });

		FlyoutIconImage.SetBinding(FontImageSource.ColorProperty, new Binding() { Source = easterEggPageViewModel, Path = nameof(EasterEggPageViewModel.ShellTitleTextColor) });
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

	public event ValueChangedEventHandler<Thickness>? SafeAreaMarginChanged;
	Thickness _SafeAreaMargin;
	public Thickness SafeAreaMargin
	{
		get => _SafeAreaMargin;
		private set
		{
			if (_SafeAreaMargin == value)
				return;

			logger.Info("SafeAreaMargin Changed: {0} -> {1}", _SafeAreaMargin, value);
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

