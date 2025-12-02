using Android.Content.PM;

using TRViS.Services;

namespace TRViS.Platforms.Android;

/// <summary>
/// Android-specific implementation of the orientation service.
/// </summary>
public class OrientationService : IOrientationService
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public void SetOrientation(AppDisplayOrientation orientation)
	{
		var activity = Platform.CurrentActivity;
		if (activity is null)
		{
			logger.Warn("Platform.CurrentActivity is null, cannot set orientation");
			return;
		}

		ScreenOrientation screenOrientation = orientation switch
		{
			AppDisplayOrientation.Portrait => ScreenOrientation.SensorPortrait,
			AppDisplayOrientation.Landscape => ScreenOrientation.SensorLandscape,
			AppDisplayOrientation.All => ScreenOrientation.Unspecified,
			_ => ScreenOrientation.Unspecified
		};

		logger.Debug("Setting orientation to {0} (Android: {1})", orientation, screenOrientation);
		activity.RequestedOrientation = screenOrientation;
	}
}
