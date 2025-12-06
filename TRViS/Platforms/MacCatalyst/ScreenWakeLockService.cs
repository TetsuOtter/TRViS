using TRViS.Services;

using UIKit;

namespace TRViS.Platforms.MacCatalyst;

/// <summary>
/// MacCatalyst-specific implementation of the screen wake lock service.
/// </summary>
public class ScreenWakeLockService : IScreenWakeLockService
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	private bool _isWakeLockEnabled = false;

	public bool IsWakeLockEnabled => _isWakeLockEnabled;

	public void EnableWakeLock()
	{
		if (_isWakeLockEnabled)
		{
			logger.Debug("Wake lock is already enabled");
			return;
		}

		logger.Info("Enabling screen wake lock");
		MainThread.BeginInvokeOnMainThread(() =>
		{
			UIApplication.SharedApplication.IdleTimerDisabled = true;
			_isWakeLockEnabled = true;
		});
	}

	public void DisableWakeLock()
	{
		if (!_isWakeLockEnabled)
		{
			logger.Debug("Wake lock is already disabled");
			return;
		}

		logger.Info("Disabling screen wake lock");
		MainThread.BeginInvokeOnMainThread(() =>
		{
			UIApplication.SharedApplication.IdleTimerDisabled = false;
			_isWakeLockEnabled = false;
		});
	}
}
