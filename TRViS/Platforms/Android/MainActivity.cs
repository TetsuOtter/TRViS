using Android.App;
using Android.Content.PM;
using Android.Views;

namespace TRViS;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	public override void OnWindowFocusChanged(bool hasFocus)
	{
		base.OnWindowFocusChanged(hasFocus);

		if (hasFocus && Window is not null)
		{
			// ref: https://developer.android.com/training/system-ui/immersive
			SystemUiFlags flags
				= SystemUiFlags.Immersive
				| SystemUiFlags.HideNavigation
				| SystemUiFlags.Fullscreen;

			Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(int)flags;
		}
	}
}
