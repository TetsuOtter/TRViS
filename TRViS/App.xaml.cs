using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;

namespace TRViS;

public partial class App : Application
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	public App()
	{
		logger.Trace("App Creating");

		InitializeComponent();

		MainPage = new AppShell();

		logger.Trace("App Created");
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		Window window = base.CreateWindow(activationState);

		logger.Info("Window Created");

		window.Destroying += WindowOnDestroying;

		return window;
	}

	protected override async void OnStart()
	{
		base.OnStart();

		if (AppCenter.Configured)
		{
			logger.Info("AppCenter configured. Starting...");
			await StartAppCenter();
			logger.Info("AppCenter All Services successfully Started");
		}
		else
		{
			logger.Warn("AppCenter not configured");
		}
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

	private void WindowOnDestroying(object? sender, EventArgs e)
	{
		// Destroying => この時点で、削除されようとしているWindowはまだWindowsに含まれている
		logger.Info("Window Destroying... (Count: {0})", Windows.Count);

		if (sender is Window window)
			window.Destroying -= WindowOnDestroying;

		if (Windows.Count <= 1)
		{
			NLog.LogManager.Flush();
			NLog.LogManager.Shutdown();
		}
	}
}
