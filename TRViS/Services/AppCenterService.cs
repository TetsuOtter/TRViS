using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;

namespace TRViS.Services;

public static class AppCenterService
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	static public bool IsInitialized { get; private set; } = false;
	static public bool IsStarted { get; private set; } = false;

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

	public static async Task<bool> Start()
	{
		if (IsStarted)
			return true;

		if (!IsInitialized && !Initialize())
		{
			logger.Fatal("AppCenter.Initialize Failed");
			return false;
		}

		if (!AppCenter.Configured)
		{
			logger.Warn("AppCenter not configured");
			return false;
		}

		logger.Info("AppCenter configured. Starting...");
		await StartAppCenter();
		logger.Info("AppCenter All Services successfully Started");
		IsStarted = true;
		return true;
	}

	static async Task StartAppCenter()
	{
		AppCenter.Start(typeof(Analytics));
		logger.Debug("AppCenter Analytics started");
		AppCenter.Start(typeof(Crashes));
		logger.Debug("AppCenter Crashes started");

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

		Crashes.NotifyUserConfirmation(UserConfirmation.AlwaysSend);
	}
}
