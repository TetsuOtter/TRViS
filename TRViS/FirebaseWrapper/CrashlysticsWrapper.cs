using NLog;

using TRViS.Services;

namespace TRViS.FirebaseWrapper;

public interface ICrashlyticsWrapper
{
	void Log(Exception ex) => Log(ex, null);
	void Log(Exception ex, string? message);
	void FlushBufferedLogs();
}

public class CrashlyticsWrapper : ICrashlyticsWrapper
{
	static readonly Logger logger = LoggerService.GetGeneralLogger();
	private readonly Queue<(Exception ex, string? message)> _buffer = new();

	public void Log(Exception ex, string? message)
	{
		if (!InstanceManager.FirebaseSettingViewModel.IsEnabled)
		{
			logger.Info("Crashlytics logging is disabled. Exception: {0}, Message: {1}", ex, message);
			return;
		}
		logger.Warn(ex, $"Logging exception to Crashlytics: {message}");

#if DISABLE_FIREBASE
		logger.Info("Firebase Disabled");
#elif IOS
		if (!IsFirebaseInitialized())
		{
			logger.Debug($"Firebase not initialized, buffering exception: {ex.Message}");
			_buffer.Enqueue((ex, message));
			return;
		}

		var errorInfo = new Dictionary<Foundation.NSString, Foundation.NSString> {
				{ Foundation.NSError.LocalizedDescriptionKey, Foundation.NSBundle.MainBundle.GetLocalizedString(ex.Message, null) },
				{ Foundation.NSError.LocalizedFailureReasonErrorKey, Foundation.NSBundle.MainBundle.GetLocalizedString ("Managed Failure", null) },
		};
		if (message is not null)
		{
			errorInfo.Add(Foundation.NSError.LocalizedRecoverySuggestionErrorKey, Foundation.NSBundle.MainBundle.GetLocalizedString(message, null));
		}

		var error = new Foundation.NSError(
			new Foundation.NSString("NonFatalError"),
			-1001,
			Foundation.NSDictionary.FromObjectsAndKeys([.. errorInfo.Values], [.. errorInfo.Keys], errorInfo.Keys.Count));

		Firebase.Crashlytics.Crashlytics.SharedInstance.RecordError(error);
#elif ANDROID
		if (!IsFirebaseInitialized())
		{
			logger.Debug($"Firebase not initialized, buffering exception: {ex.Message}");
			_buffer.Enqueue((ex, message));
			return;
		}

		// TODO: Androidでもmessageを送信する
		Firebase.Crashlytics.FirebaseCrashlytics.Instance.RecordException(Java.Lang.Throwable.FromException(ex));
#endif
	}

	public void FlushBufferedLogs()
	{
		if (_buffer.Count == 0)
		{
			return;
		}

		logger.Info($"Flushing {_buffer.Count} buffered crashlytics exceptions");
		while (_buffer.TryDequeue(out var item))
		{
			Log(item.ex, item.message);
		}
	}

	private static bool IsFirebaseInitialized()
	{
#if IOS
		return Firebase.Core.App.DefaultInstance != null;
#elif ANDROID
		return Firebase.FirebaseApp.GetApps(Android.App.Application.Context).Count > 0;
#else
		return true;
#endif
	}
}
