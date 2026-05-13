using System.Runtime.Versioning;

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

	[SupportedOSPlatform("ios12.2")]
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

		try
		{
			if (OperatingSystem.IsIOSVersionAtLeast(16))
			{
				ApplyOrientationIOS16Plus(mask);
			}
			else
			{
				ApplyOrientationLegacy();
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Exception while setting orientation: {0}", ex.Message);
		}
	}

	/// <summary>
	/// Apply orientation changes for iOS 16 and later.
	/// </summary>
	[SupportedOSPlatform("ios16.0")]
	private void ApplyOrientationIOS16Plus(UIInterfaceOrientationMask mask)
	{
		var windowScene = UIApplication.SharedApplication.ConnectedScenes
			.OfType<UIWindowScene>()
			.FirstOrDefault();

		if (windowScene is null)
		{
			logger.Warn("WindowScene not found");
			return;
		}

		// First, notify view controller of supported orientations
		NotifyViewControllerOfOrientationChange(windowScene);

		// Use main thread to request geometry update
		if (MainThread.IsMainThread)
		{
			RequestGeometryUpdate(windowScene, mask);
		}
		else
		{
			MainThread.BeginInvokeOnMainThread(() => RequestGeometryUpdate(windowScene, mask));
		}
	}

	/// <summary>
	/// Request geometry update on the main thread.
	/// </summary>
	[SupportedOSPlatform("ios16.0")]
	private void RequestGeometryUpdate(UIWindowScene windowScene, UIInterfaceOrientationMask mask)
	{
		try
		{
			var geometryPreferences = new UIWindowSceneGeometryPreferencesIOS(mask);

			windowScene.RequestGeometryUpdate(geometryPreferences, error =>
			{
				if (error is not null)
				{
					logger.Warn("Failed to update geometry: {0}", error.LocalizedDescription);
				}
				else
				{
					logger.Debug("Geometry update successful");
					// Force layout update on MAUI side
					InvalidateMAUILayout();
				}
			});
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Exception during geometry update: {0}", ex.Message);
		}
	}

	/// <summary>
	/// Invalidate MAUI layout to force re-layout after orientation change.
	/// </summary>
	private void InvalidateMAUILayout()
	{
		try
		{
			// Workaround for iOS 26 MAUI layout bug (#34273, #34369)
			// Invalidate layout at both Window and Page levels to force recalculation
			if (Application.Current?.Windows.Count > 0)
			{
				var window = Application.Current.Windows[0];

				// Invalidate the page
				if (window?.Page is VisualElement page)
				{
					page.InvalidateMeasure();
					logger.Debug("Page layout invalidated for iOS 26 MAUI rotation bug workaround");
				}
			}
		}
		catch (Exception ex)
		{
			logger.Debug("Exception invalidating MAUI layout: {0}", ex.Message);
		}
	}

	/// <summary>
	/// Apply orientation changes for iOS 15 and earlier.
	/// </summary>
	[SupportedOSPlatform("ios12.2")]
	private void ApplyOrientationLegacy()
	{
		try
		{
			UIViewController.AttemptRotationToDeviceOrientation();
			InvalidateMAUILayout();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Exception during legacy orientation update: {0}", ex.Message);
		}
	}

	/// <summary>
	/// Notify the view controller hierarchy of orientation changes.
	/// </summary>
	[SupportedOSPlatform("ios16.0")]
	private static void NotifyViewControllerOfOrientationChange(UIWindowScene windowScene)
	{
		if (windowScene.Windows == null || windowScene.Windows.Length == 0)
		{
			return;
		}

		foreach (var window in windowScene.Windows)
		{
			var rootViewController = window.RootViewController;
			rootViewController?.SetNeedsUpdateOfSupportedInterfaceOrientations();
		}
	}
}
