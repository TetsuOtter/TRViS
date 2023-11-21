using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;

namespace TRViS;

public partial class App : Application
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	public App()
	{
		logger.Trace("App Creating (URL: {0})", AppLinkUri?.ToString() ?? "(null))");

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

	public static Uri? AppLinkUri { get; set; }
	protected override void OnAppLinkRequestReceived(Uri uri)
	{
		base.OnAppLinkRequestReceived(uri);

		AppLinkUri = uri;
		logger.Info("AppLinkUri: {0}", uri);
	}
}
