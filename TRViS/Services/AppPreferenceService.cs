namespace TRViS.Services;

public enum AppPreferenceKeys
{
	IsAppCenterEnabled,
	IsAppCenterLogShareEnabled,
}

public static class AppPreferenceService
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	static string ToKeyString(in AppPreferenceKeys key)
	{
		return key.ToString();
	}

	public static bool Get(in AppPreferenceKeys key, bool defaultValue, out bool hasKey)
	{
		string keyStr = ToKeyString(key);
		logger.Trace("key: {0}, default:{1}", keyStr, defaultValue);

		hasKey = Preferences.ContainsKey(keyStr);
		if (!hasKey)
		{
			logger.Trace("key not found");
			return defaultValue;
		}

		var value = Preferences.Get(keyStr, defaultValue);
		logger.Trace("value: {0}", value);
		return value;
	}

	public static string Get(in AppPreferenceKeys key, string defaultValue, out bool hasKey)
	{
		string keyStr = ToKeyString(key);
		logger.Trace("key: {0}, default:{1}", keyStr, defaultValue);

		hasKey = Preferences.ContainsKey(keyStr);
		if (!hasKey)
		{
			logger.Trace("key not found");
			return defaultValue;
		}

		var value = Preferences.Get(keyStr, defaultValue);
		logger.Trace("value: {0}", value);
		return value;
	}
}
