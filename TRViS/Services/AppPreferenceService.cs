using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TRViS.Services;

public enum AppPreferenceKeys
{
	IsAppCenterEnabled,
	IsAppCenterAnalyticsEnabled,
	IsAppCenterLogShareEnabled,
	LastAcceptedPrivacyPolicyRevision,
	InstallId,
	ExternalResourceUrlHistory,
}

public static class AppPreferenceService
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

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

	public static T GetFromJson<T>(in AppPreferenceKeys key, T defaultValue, out bool hasKey, JsonTypeInfo<T> jsonTypeInfo)
	{
		string result = Get(in key, string.Empty, out hasKey);
		return string.IsNullOrEmpty(result) ? defaultValue : JsonSerializer.Deserialize(result, jsonTypeInfo) ?? defaultValue;
	}

	public static void Set(in AppPreferenceKeys key, bool value)
	{
		string keyStr = ToKeyString(key);
		logger.Trace("key: {0}, value:{1}", keyStr, value);
		Preferences.Set(keyStr, value);
	}

	public static void Set(in AppPreferenceKeys key, string value)
	{
		string keyStr = ToKeyString(key);
		logger.Trace("key: {0}, value:{1}", keyStr, value);
		Preferences.Set(keyStr, value);
	}

	public static void SetToJson<T>(in AppPreferenceKeys key, T value, JsonTypeInfo<T> jsonTypeInfo)
		=> Set(in key, JsonSerializer.Serialize(value, jsonTypeInfo));
}
