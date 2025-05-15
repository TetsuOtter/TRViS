using NLog;

namespace TRViS.FirebaseWrapper;

public interface IAnalyticsWrapper
{
	void SetIsEnabled(bool isEnabled);
	void Log(string eventName, Dictionary<string, string>? parameters);
	void Log(string eventName) => Log(eventName, null);
	void Log(AnalyticsEvents eventType, Dictionary<string, string>? parameters) => Log(eventType.ToString(), parameters);
	void Log(AnalyticsEvents eventType) => Log(eventType.ToString(), null);
}

// ref: https://learn.microsoft.com/en-au/answers/questions/2125319/firebase-analytics-not-logging-events-in-maui-net9
public class AnalyticsWrapper : IAnalyticsWrapper
{
	static readonly Logger logger = LoggerService.GetGeneralLogger();

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
		if (parameters is null)
		{
			Firebase.Analytics.Analytics.LogEvent(eventName, (Foundation.NSDictionary<Foundation.NSString, Foundation.NSObject>?)null);
			return;
		}

		Foundation.NSString[] keys = [.. parameters.Keys.Select(k => new Foundation.NSString(k))];
		Foundation.NSString[] values = [.. parameters.Values.Select(v => new Foundation.NSString(v))];
		Firebase.Analytics.Analytics.LogEvent(eventName, new Foundation.NSDictionary<Foundation.NSString, Foundation.NSObject>(keys, values));
#elif ANDROID
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
}
