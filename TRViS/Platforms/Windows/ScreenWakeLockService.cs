using TRViS.Services;

namespace TRViS.Platforms.Windows;

/// <summary>
/// Windows-specific implementation of the screen wake lock service.
/// Windows desktop doesn't typically use wake locks, so this is a no-op implementation.
/// </summary>
public class ScreenWakeLockService : IScreenWakeLockService
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public bool IsWakeLockEnabled => false;

	public void EnableWakeLock()
	{
		logger.Debug("Wake lock not supported on Windows desktop");
	}

	public void DisableWakeLock()
	{
		logger.Debug("Wake lock not supported on Windows desktop");
	}
}
