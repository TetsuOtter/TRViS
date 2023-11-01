using CommunityToolkit.Maui;

using NLog;

using TRViS.Services;

namespace TRViS;

public static class MauiProgram
{
	static readonly string CrashLogFilePath;
	static readonly string CrashLogFileName;

	static readonly Logger logger;
	const string logFormat = "${longdate} [${threadid:padding=3}] [${uppercase:${level:padding=-5}}] ${callsite}() ${message} ${exception:format=tostring}";

	static MauiProgram()
	{
		CrashLogFileName = $"{DateTime.Now:yyyyMMdd_HHmmss}.crashlog.trvis.txt";

		CrashLogFilePath = Path.Combine(DirectoryPathProvider.CrashLogFileDirectory.FullName, CrashLogFileName);

		LoggerService.SetupLoggerService();
		logger = LogManager.GetCurrentClassLogger();
	}

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIconsRegular");
			});

		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

		return builder.Build();
	}

	private static async void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is not Exception ex)
			return;

		logger.Fatal(ex, "UnhandledException");

		if (!DirectoryPathProvider.CrashLogFileDirectory.Exists)
		{
			DirectoryPathProvider.CrashLogFileDirectory.Create();
		}

		await File.AppendAllTextAsync(CrashLogFilePath, $"{DateTime.Now:[yyyy/MM/dd HH:mm:ss]} {ex.Message}\n{ex.StackTrace}\n---\n(InnerException: {ex.InnerException})\n\n");
	}
}

