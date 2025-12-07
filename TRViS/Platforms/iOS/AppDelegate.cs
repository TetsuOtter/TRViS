using Foundation;

using TRViS.Platforms.iOS;

using UIKit;

namespace TRViS;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
	{
		if (!string.IsNullOrEmpty(url.AbsoluteString))
			App.SetAppLinkUri(url.AbsoluteString);
		return base.OpenUrl(application, url, options);
	}

	[Export("application:supportedInterfaceOrientationsForWindow:")]
	public UIInterfaceOrientationMask GetSupportedInterfaceOrientations(UIApplication application, UIWindow? forWindow)
	{
		return OrientationService.CurrentOrientationMask;
	}
}
