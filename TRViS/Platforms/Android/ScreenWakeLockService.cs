using Android.Views;

using Microsoft.Maui;
using Microsoft.Maui.Controls;

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

		logger.Info("Enabling screen wake lock");
		_isWakeLockEnabled = true;
		MainThread.BeginInvokeOnMainThread(() =>
		{
			var activity = Platform.CurrentActivity;
			if (activity?.Window is null)
			{
				logger.Warn("Platform.CurrentActivity or Window is null, cannot enable wake lock");
				_isWakeLockEnabled = false;
				return;
			}
			activity.Window.AddFlags(WindowManagerFlags.KeepScreenOn);
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
		_isWakeLockEnabled = false;
		MainThread.BeginInvokeOnMainThread(() =>
		{
			var activity = Platform.CurrentActivity;
			if (activity?.Window is null)
			{
				logger.Warn("Platform.CurrentActivity or Window is null, cannot disable wake lock");
				return;
			}
			activity.Window.ClearFlags(WindowManagerFlags.KeepScreenOn);
		});
	}
}
