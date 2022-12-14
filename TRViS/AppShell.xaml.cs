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
}

