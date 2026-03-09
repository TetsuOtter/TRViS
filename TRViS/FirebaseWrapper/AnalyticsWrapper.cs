using NLog;

using TRViS.Services;

namespace TRViS.FirebaseWrapper;

public interface IAnalyticsWrapper
{
	void SetIsEnabled(bool isEnabled);
	void Log(string eventName, Dictionary<string, string>? parameters);
	void Log(string eventName) => Log(eventName, null);
	void Log(AnalyticsEvents eventType, Dictionary<string, string>? parameters) => Log(eventType.ToString(), parameters);
	void Log(AnalyticsEvents eventType) => Log(eventType.ToString(), null);
	void FlushBufferedLogs();
}

// ref: https://learn.microsoft.com/en-au/answers/questions/2125319/firebase-analytics-not-logging-events-in-maui-net9
public class AnalyticsWrapper : IAnalyticsWrapper
{
	static readonly Logger logger = LoggerService.GetGeneralLogger();
	private readonly Queue<(string eventName, Dictionary<string, string>? parameters)> _buffer = new();

	public void SetIsEnabled(bool isEnabled)
	{
		logger.Info("Setting IsEnabled: {0}", isEnabled);
#if DISABLE_FIREBASE
		logger.Info("Firebase Disabled");
#elif IOS
		Firebase.Analytics.Analytics.SetAnalyticsCollectionEnabled(isEnabled);
#elif ANDROID
		Firebase.Analytics.FirebaseAnalytics.GetInstance(Android.App.Application.Context).SetAnalyticsCollectionEnabled(isEnabled);
#endif
	}

	public void Log(string eventName, Dictionary<string, string>? parameters)
	{
		if (!InstanceManager.FirebaseSettingViewModel.IsAnalyticsEnabled)
		{
			return;
		}
		logger.Info($"Logging event: {eventName}");
#if DISABLE_FIREBASE
		logger.Info("Firebase Disabled");
#elif IOS
		if (!IsFirebaseInitialized())
		{
			logger.Debug($"Firebase not initialized, buffering event: {eventName}");
			_buffer.Enqueue((eventName, parameters));
			return;
		}

		if (parameters is null)
		{
			Firebase.Analytics.Analytics.LogEvent(eventName, (Foundation.NSDictionary<Foundation.NSString, Foundation.NSObject>?)null);
			return;
		}

		Foundation.NSString[] keys = [.. parameters.Keys.Select(k => new Foundation.NSString(k))];
		Foundation.NSString[] values = [.. parameters.Values.Select(v => new Foundation.NSString(v))];
		Firebase.Analytics.Analytics.LogEvent(eventName, new Foundation.NSDictionary<Foundation.NSString, Foundation.NSObject>(keys, values));
#elif ANDROID
		if (!IsFirebaseInitialized())
		{
			logger.Debug($"Firebase not initialized, buffering event: {eventName}");
			_buffer.Enqueue((eventName, parameters));
			return;
		}

		var firebaseAnalytics = Firebase.Analytics.FirebaseAnalytics.GetInstance(Android.App.Application.Context);
		if (parameters is null)
		{
			firebaseAnalytics.LogEvent(eventName, (Android.OS.Bundle?)null);
			return;
		}

		var bundle = new Android.OS.Bundle();
		foreach (var (key, value) in parameters)
		{
			bundle.PutString(key, value);
		}
		firebaseAnalytics.LogEvent(eventName, bundle);
#endif
	}

	public void FlushBufferedLogs()
	{
		if (_buffer.Count == 0)
		{
			return;
		}

		logger.Info($"Flushing {_buffer.Count} buffered analytics events");
		while (_buffer.TryDequeue(out var item))
		{
			Log(item.eventName, item.parameters);
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
