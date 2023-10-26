using System.Text;

using CommunityToolkit.Maui;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

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

		logger = SetupLogger();
	}

	static Logger SetupLogger()
	{
		bool isNormalLogFileDirectoryExists = DirectoryPathProvider.NormalLogFileDirectory.Exists;
		if (!isNormalLogFileDirectoryExists)
		{
			DirectoryPathProvider.NormalLogFileDirectory.Create();
		}

#if DEBUG
		ConsoleTarget consoleTarget = new("console")
		{
			Layout = logFormat,
			Encoding = Encoding.UTF8,
		};
		LoggingRule consoleLoggingRule = new("*", LogLevel.Trace, consoleTarget);
#endif

		FileTarget fileTarget = new("file")
		{
			FileName = Path.Combine(DirectoryPathProvider.NormalLogFileDirectory.FullName, "logs_current.trvis.log"),
			ArchiveFileName = Path.Combine(DirectoryPathProvider.NormalLogFileDirectory.FullName, "logs.{#}.trvis.log"),
			ArchiveNumbering = ArchiveNumberingMode.DateAndSequence,
			ArchiveEvery = FileArchivePeriod.Day,
			MaxArchiveFiles = 14,

			Encoding = Encoding.UTF8,
			LineEnding = LineEndingMode.LF,
			WriteBom = false,
			CreateDirs = false,
			Layout = logFormat,
		};
		AsyncTargetWrapper fileAsyncTargetWrapper = new("async", fileTarget)
		{
			OverflowAction = AsyncTargetWrapperOverflowAction.Grow,
			QueueLimit = 5000,
			BatchSize = 100,
			TimeToSleepBetweenBatches = 100,
		};
		LoggingRule fileLoggingRule = new(
			"*",
			#if DEBUG
			LogLevel.Trace,
			#else
			LogLevel.Info,
			#endif
			fileAsyncTargetWrapper
		);

		LoggingConfiguration loggingConfiguration = new()
		{
			LoggingRules =
			{
#if DEBUG
				consoleLoggingRule,
#endif

				fileLoggingRule,
			},
		};

		LogManager
			.Setup()
			.LoadConfiguration(loggingConfiguration);

		Logger _logger = LogManager.GetCurrentClassLogger();
		_logger.Info("TRViS Starting... (isNormalLogFileDirectoryExists: {0})", isNormalLogFileDirectoryExists);
		return _logger;
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

