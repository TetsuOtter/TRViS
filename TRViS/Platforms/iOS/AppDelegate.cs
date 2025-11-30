using Foundation;

using ObjCRuntime;

using UIKit;

namespace TRViS;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	static AppDelegate()
	{
		// Register exception handler BEFORE app initialization to suppress TraitCollectionDidChange crash
		ObjCRuntime.Runtime.MarshalManagedException += (object sender, MarshalManagedExceptionEventArgs args) =>
		{
			if (args.Exception is NullReferenceException &&
				args.Exception.StackTrace?.Contains("TraitCollectionDidChange") == true)
			{
				// Suppress TraitCollectionDidChange crashes - known MAUI 10.0.11 bug
				System.Diagnostics.Debug.WriteLine("Suppressing TraitCollectionDidChange NullReferenceException");
				args.ExceptionMode = MarshalManagedExceptionMode.UnwindNativeCode;
			}
		};
	}

	protected override MauiApp CreateMauiApp()
	{
		// Setup NavigationPage customization before creating the app
		NavigationPageCustomizationSetup.SetupNavigationPageHandler();
		return MauiProgram.CreateMauiApp();
	}

	public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary? options)
	{
		if (!string.IsNullOrEmpty(url.AbsoluteString))
			App.SetAppLinkUri(url.AbsoluteString);
		return base.OpenUrl(application, url, options ?? new NSDictionary());
	}
}



