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

	try
	{
		InitializeComponent();
	}
	catch (Exception ex)
	{
		logger.Error(ex, "App Initialize Failed");
		NLog.LogManager.Flush();
		NLog.LogManager.Shutdown();
		System.Environment.Exit(1);
	}

		logger.Trace("App Created");
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		Window window = new(new AppShell());

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
			InstanceManager.Dispose();
			NLog.LogManager.Flush();
			NLog.LogManager.Shutdown();
		}
	}

	static string? AppLinkUri { get; set; }

	public static void SetAppLinkUri(string uri)
	{
		logger.Info("AppLinkUri: {0}", uri);

		if (Current is not App app)
		{
			logger.Warn("App.Current is not App");
			AppLinkUri = uri;
			return;
		}

		if (app.Windows.Count == 0)
		{
			logger.Warn("app.Windows is Empty");
			AppLinkUri = uri;
			return;
		}

		HandleAppLinkUriAsync(uri);
	}

	protected override void OnAppLinkRequestReceived(Uri uri)
	{
		base.OnAppLinkRequestReceived(uri);

		logger.Info("AppLinkUri: {0}", uri);

		HandleAppLinkUriAsync(uri.ToString());
	}

	protected override void OnStart()
	{
		logger.Info("App Start");
		base.OnStart();

		if (AppLinkUri is not null)
		{
			logger.Info("AppLinkUri is not null: {0}", AppLinkUri);
			HandleAppLinkUriAsync(AppLinkUri);
		}
	}

	static Task HandleAppLinkUriAsync(string uri)
		=> HandleAppLinkUriAsync(uri, CancellationToken.None);
	static Task HandleAppLinkUriAsync(string uri, CancellationToken cancellationToken)
	{
		return InstanceManager.AppViewModel.HandleAppLinkUriAsync(uri, cancellationToken).ContinueWith(t =>
		{
			AppLinkUri = null;
			if (t.IsFaulted)
			{
				logger.Error(t.Exception, "HandleAppLinkUriAsync Failed");
				Crashes.TrackError(t.Exception);
			}
		}, cancellationToken);
	}
}
