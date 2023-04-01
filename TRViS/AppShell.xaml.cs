using TRViS.ViewModels;

namespace TRViS;

public partial class AppShell : Shell
{
	static public string AppVersionString
		=> $"Version: {AppInfo.Current.VersionString}-{AppInfo.Current.BuildString}";

	public AppShell(EasterEggPageViewModel easterEggPageViewModel)
	{
		InitializeComponent();

		SetBinding(Shell.BackgroundColorProperty, new Binding() { Source = easterEggPageViewModel, Path = nameof(EasterEggPageViewModel.ShellBackgroundColor) });
		SetBinding(Shell.TitleColorProperty, new Binding() { Source = easterEggPageViewModel, Path = nameof(EasterEggPageViewModel.ShellTitleTextColor) });

		FlyoutIconImage.SetBinding(FontImageSource.ColorProperty, new Binding() { Source = easterEggPageViewModel, Path = nameof(EasterEggPageViewModel.ShellTitleTextColor) });
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

			Thickness tmp = _SafeAreaMargin;
			_SafeAreaMargin = value;
			SafeAreaMarginChanged?.Invoke(this, tmp, value);
		}
	}

#if IOS
	UIKit.UIWindow? UIWindow = null;

	protected override void OnSizeAllocated(double width, double height)
	{
		// SafeAreaInsets ref: https://stackoverflow.com/questions/46829840/get-safe-area-inset-top-and-bottom-heights
		// ios15 >= ref: https://zenn.dev/paraches/articles/windows_was_depricated_in_ios15
		if (UIWindow is null)
		{
			if (OperatingSystem.IsIOSVersionAtLeast(13, 0))
			{
				if (UIKit.UIApplication.SharedApplication.ConnectedScenes.ToArray().FirstOrDefault(v => v is UIKit.UIWindowScene) is UIKit.UIWindowScene scene)
					UIWindow = scene.Windows.FirstOrDefault();
			}
			else
				UIWindow = UIKit.UIApplication.SharedApplication.Windows.FirstOrDefault();
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

