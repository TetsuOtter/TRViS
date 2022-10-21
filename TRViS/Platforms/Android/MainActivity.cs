using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace TRViS;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);

		if (this.Window is null)
			return;

		// fullscreen ref: https://kurosawa0626.wordpress.com/2017/05/29/xamarin%E3%81%A7android%E3%82%A2%E3%83%97%E3%83%AA%E3%82%92%E3%83%95%E3%83%AB%E3%82%B9%E3%82%AF%E3%83%AA%E3%83%BC%E3%83%B3%E3%81%A7%E8%A1%A8%E7%A4%BA%E3%81%95%E3%81%9B%E3%82%8B/
		SystemUiFlags flags
			= SystemUiFlags.LayoutStable
			| SystemUiFlags.LayoutHideNavigation
			| SystemUiFlags.HideNavigation
			| SystemUiFlags.Fullscreen
			| SystemUiFlags.ImmersiveSticky;

		this.Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(int)flags;

		if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
		{
			this.Window.AddFlags(WindowManagerFlags.Fullscreen);
			this.Window.AddFlags(WindowManagerFlags.KeepScreenOn);
		}
	}
}
