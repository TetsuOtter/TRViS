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
			}
		}
		else
		{
			// For iOS < 16, force orientation change
			UIDevice.CurrentDevice.SetValueForKey(
				Foundation.NSNumber.FromInt32((int)GetPreferredOrientation(orientation)),
				new Foundation.NSString("orientation")
			);
		}
	}

	private static UIInterfaceOrientation GetPreferredOrientation(AppDisplayOrientation orientation)
	{
		return orientation switch
		{
			AppDisplayOrientation.Portrait => UIInterfaceOrientation.Portrait,
			AppDisplayOrientation.Landscape => UIInterfaceOrientation.LandscapeLeft,
			AppDisplayOrientation.All => UIInterfaceOrientation.Unknown,
			_ => UIInterfaceOrientation.Unknown
		};
	}
}
