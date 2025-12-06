using Android.Views;

using TRViS.Services;

namespace TRViS.Platforms.Android;

/// <summary>
/// Android-specific implementation of the screen wake lock service.
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

		var activity = Platform.CurrentActivity;
		if (activity?.Window is null)
		{
			logger.Warn("Platform.CurrentActivity or Window is null, cannot enable wake lock");
			return;
		}

		logger.Info("Enabling screen wake lock");
		MainThread.BeginInvokeOnMainThread(() =>
		{
			activity.Window.AddFlags(WindowManagerFlags.KeepScreenOn);
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

		var activity = Platform.CurrentActivity;
		if (activity?.Window is null)
		{
			logger.Warn("Platform.CurrentActivity or Window is null, cannot disable wake lock");
			return;
		}

		logger.Info("Disabling screen wake lock");
		MainThread.BeginInvokeOnMainThread(() =>
		{
			activity.Window.ClearFlags(WindowManagerFlags.KeepScreenOn);
			_isWakeLockEnabled = false;
		});
	}
}
