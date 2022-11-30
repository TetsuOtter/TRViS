using Android.App;
using Android.Content.PM;
using Android.Views;
using AndroidX.Core.View;

namespace TRViS;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	public override void OnWindowFocusChanged(bool hasFocus)
	{
		base.OnWindowFocusChanged(hasFocus);

		if (!hasFocus || Window is null)
			return;

		if (
			OperatingSystem.IsAndroidVersionAtLeast(30)
			&& Window.DecorView.WindowInsetsController is IWindowInsetsController windowInsetsController
		)
		{
			// ref: https://developer.android.com/develop/ui/views/layout/immersive
			windowInsetsController.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
			windowInsetsController.Hide(WindowInsetsCompat.Type.SystemBars());
		}
		else
		{
			// ref: https://developer.android.com/training/system-ui/immersive?hl=ja#java
			SystemUiFlags flags
				= SystemUiFlags.ImmersiveSticky
				| SystemUiFlags.HideNavigation
				| SystemUiFlags.Fullscreen;

			Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(int)flags;
		}
	}
}
