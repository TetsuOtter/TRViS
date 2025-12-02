using TRViS.Services;

namespace TRViS.Platforms.Windows;

/// <summary>
/// Windows-specific implementation of the orientation service.
/// Windows runs on desktop where orientation locking is not applicable.
/// </summary>
public class OrientationService : IOrientationService
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public void SetOrientation(AppDisplayOrientation orientation)
	{
		// Windows runs on desktop where orientation locking is not applicable.
		logger.Debug("SetOrientation called on Windows - orientation locking is not applicable on desktop platforms");
	}
}
