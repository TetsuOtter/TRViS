using TRViS.Services;

using UIKit;

namespace TRViS.Platforms.iOS;

/// <summary>
/// iOS-specific implementation of the orientation service.
/// </summary>
public class OrientationService : IOrientationService
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	/// <summary>
	/// Gets the currently requested orientation mask for iOS.
	/// </summary>
	public static UIInterfaceOrientationMask CurrentOrientationMask { get; private set; } = UIInterfaceOrientationMask.All;

	public void SetOrientation(AppDisplayOrientation orientation)
	{
		UIInterfaceOrientationMask mask = orientation switch
		{
			AppDisplayOrientation.Portrait => UIInterfaceOrientationMask.Portrait | UIInterfaceOrientationMask.PortraitUpsideDown,
			AppDisplayOrientation.Landscape => UIInterfaceOrientationMask.Landscape,
			AppDisplayOrientation.All => UIInterfaceOrientationMask.All,
			_ => UIInterfaceOrientationMask.All
		};

		logger.Debug("Setting orientation to {0} (iOS mask: {1})", orientation, mask);
		CurrentOrientationMask = mask;

		// Request geometry update to apply the new orientation
		if (OperatingSystem.IsIOSVersionAtLeast(16))
		{
			var windowScene = UIApplication.SharedApplication.ConnectedScenes
				.OfType<UIWindowScene>()
				.FirstOrDefault();

			if (windowScene is not null)
			{
				var geometryPreferences = new UIWindowSceneGeometryPreferencesIOS(mask);
				windowScene.RequestGeometryUpdate(geometryPreferences, error =>
				{
					if (error is not null)
					{
						logger.Warn("Failed to update geometry: {0}", error.LocalizedDescription);
					}
				});

				// Also update the view controller to notify it of the new supported orientations
				var keyWindow = UIApplication.SharedApplication.KeyWindow;
				if (keyWindow?.RootViewController is not null)
				{
					keyWindow.RootViewController.SetNeedsUpdateOfSupportedInterfaceOrientations();
				}
			}
		}
		else
		{
			// For iOS < 16, we rely on the AppDelegate's GetSupportedInterfaceOrientations
			// method which returns CurrentOrientationMask. Trigger re-evaluation of orientation.
#pragma warning disable CA1422 // Call site reachable on all platforms - AttemptRotationToDeviceOrientation is deprecated on iOS 16+
			UIViewController.AttemptRotationToDeviceOrientation();
#pragma warning restore CA1422
		}
	}
}
