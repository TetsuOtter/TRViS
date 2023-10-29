using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using TRViS.ViewModels;

namespace TRViS.Services;

public static class AppCenterService
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	static public bool IsInitialized { get; private set; } = false;
	static public bool IsStarted { get; private set; } = false;

	static public bool IsCrashesStarted{ get; private set; } = false;
	static public bool IsAnalyticsStarted{ get; private set; } = false;

	static public IAppCenterSetting? CurrentSetting { get; private set; }

	static readonly object initLockObj = new();
	public static bool Initialize()
	{
		if (IsInitialized)
			return true;

		lock (initLockObj)
		{
			return _Initialize();
		}
	}

	static bool _Initialize()
	{
		if (IsInitialized)
			return true;

		try
		{
			AppCenter.Start(
#if IOS
				AppCenterSecrets.IOS
#elif ANDROID
				AppCenterSecrets.ANDROID
#elif WINDOWS
				AppCenterSecrets.WINDOWS
#elif MACCATALYST
				AppCenterSecrets.MACOS
#endif
				,
				typeof(Analytics),
				typeof(Crashes)
			);

			IsInitialized = true;
			return true;
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "AppCenter.Start Failed");
		}
		return false;
	}

	static async Task<bool> ApplyAnalyticsSettingAsync(bool statusToBe)
	{
		bool isAnalyticsEnabled = await Analytics.IsEnabledAsync();
		if (statusToBe == isAnalyticsEnabled)
		{
			logger.Debug("AppCenter Analytics already {0}", statusToBe ? "enabled" : "disabled");
			return true;
		}

		if (statusToBe == true && !IsAnalyticsStarted) {
			AppCenter.Start(typeof(Analytics));
			IsAnalyticsStarted = true;
			logger.Debug("AppCenter Analytics started");
		}

		await Analytics.SetEnabledAsync(statusToBe);
		logger.Debug("AppCenter Analytics {0}", statusToBe ? "enabled" : "disabled");
		return true;
	}

	static async Task<bool> ApplyCrashesSettingAsync(bool statusToBe)
	{
		bool isAnalyticsEnabled = await Crashes.IsEnabledAsync();
		if (statusToBe == isAnalyticsEnabled)
		{
			logger.Debug("AppCenter Crashes already {0}", statusToBe ? "enabled" : "disabled");
			return true;
		}

		if (statusToBe == true && !IsCrashesStarted) {
			Crashes.GetErrorAttachments = LoggerService.GetErrorAttachmentsCallback;

			Crashes.FailedToSendErrorReport += (sender, e) =>
			{
				ErrorReport crashReport = e.Report;
				logger.Error("Failed to send error report ...Reason: {0}", e.Exception);
				logger.Error("Failed to send error report ...Report: Id:{0}, AppStart:{1}, AppCrash:{2}, Details:{3}, StackTrace:{4}",
					crashReport.Id,
					crashReport.AppStartTime,
					crashReport.AppErrorTime,
					#if IOS || MACCATALYST
					crashReport.AppleDetails,
					#elif ANDROID
					crashReport.AndroidDetails,
					#else
					crashReport.Exception,
					#endif
					crashReport.StackTrace
				);
			};

			AppCenter.Start(typeof(Crashes));
			IsCrashesStarted = true;
			logger.Debug("AppCenter Crashes started");
			await InitCrashesSettings();
			logger.Trace("AppCenter Crashes settings initialized");
		}

		await Crashes.SetEnabledAsync(statusToBe);
		logger.Debug("AppCenter Crashes {0}", statusToBe ? "enabled" : "disabled");
		if (statusToBe == true)
		{
			Crashes.NotifyUserConfirmation(UserConfirmation.AlwaysSend);
		}
		return true;
	}

	public static async Task<bool> ApplySettingAsync(IAppCenterSetting setting)
	{
		if (!IsStarted && !setting.IsEnabled)
		{
			logger.Debug("AppCenter is not started so needless to stop");
			CurrentSetting = setting;
			return true;
		}

		if (!Initialize())
		{
			logger.Error("AppCenter.Initialize Failed");
			return false;
		}

		if (!AppCenter.Configured)
		{
			logger.Warn("AppCenter not configured");
			return false;
		}

		CurrentSetting = setting;

		// NotStarted && ToDisableは最初にチェック済みなので、ここではチェックしない
		if (!setting.IsEnabled)
		{
			await ApplyAnalyticsSettingAsync(false);
			await ApplyCrashesSettingAsync(false);
			await AppCenter.SetEnabledAsync(false);
			IsStarted = false;
			logger.Info("AppCenter stopped");
			return true;
		}

		if (!IsStarted && setting.IsEnabled)
		{
			await AppCenter.SetEnabledAsync(true);
			await ApplyCrashesSettingAsync(true);
			IsStarted = true;
		}

		await ApplyAnalyticsSettingAsync(setting.IsAnalyticsEnabled);
		logger.Info("AppCenter started");
		return true;
	}

	static async Task InitCrashesSettings()
	{
		bool hadMemoryWarning = await Crashes.HasReceivedMemoryWarningInLastSessionAsync();
		if (hadMemoryWarning)
		{
			logger.Warn("Had memory warning in last session");
		}

		bool didAppCrash = await Crashes.HasCrashedInLastSessionAsync();
		if (didAppCrash)
		{
			logger.Warn("App crashed in last session");

			ErrorReport crashReport = await Crashes.GetLastSessionCrashReportAsync();
			if (crashReport is not null)
			{
				logger.Warn("CrashReport: Id:{0}, AppStart:{1}, AppCrash:{2}, Details:{3}, StackTrace:{4}",
					crashReport.Id,
					crashReport.AppStartTime,
					crashReport.AppErrorTime,
					#if IOS || MACCATALYST
					crashReport.AppleDetails,
					#elif ANDROID
					crashReport.AndroidDetails,
					#else
					crashReport.Exception,
					#endif
					crashReport.StackTrace
				);
			}
		}
	}
}
