using TRViS.Services;

namespace TRViS.Platforms.MacCatalyst;

/// <summary>
/// MacCatalyst-specific implementation of the orientation service.
/// MacCatalyst runs on desktop where orientation locking is not applicable.
/// </summary>
public class OrientationService : IOrientationService
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public void SetOrientation(AppDisplayOrientation orientation)
	{
		// MacCatalyst runs on macOS where orientation locking is not applicable.
		logger.Debug("SetOrientation called on MacCatalyst - orientation locking is not applicable on desktop platforms");
	}
}
