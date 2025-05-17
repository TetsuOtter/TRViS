using NLog;

using TRViS.Services;

namespace TRViS.FirebaseWrapper;

public interface ICrashlyticsWrapper
{
	void Log(Exception ex) => Log(ex, null);
	void Log(Exception ex, string? message);
}

public class CrashlyticsWrapper : ICrashlyticsWrapper
{
	static readonly Logger logger = LoggerService.GetGeneralLogger();

	public void Log(Exception ex, string? message)
	{
		logger.Warn(ex, $"Logging exception to Crashlytics: {message}");

#if DISABLE_FIREBASE
		logger.Info("Firebase Disabled");
#elif IOS
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
		// TODO: Androidでもmessageを送信する
		Firebase.FirebaseCrashlytics.Instance.RecordException(Java.Lang.Throwable.FromException(ex));
#endif
	}
}
