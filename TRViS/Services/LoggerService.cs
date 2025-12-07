using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

using TRViS.Utils;

namespace TRViS.Services;

public static class LoggerService
{
	static readonly Logger logger;
	const string LOG_FORMAT = "${longdate} [${threadid:padding=3}] [${uppercase:${level:padding=-5}}] ${callsite}() ${message} ${exception:format=tostring}";

	private static readonly LogFileManager GeneralLogFileManager = new(
		DirectoryPathProvider.GeneralLogFileDirectory,
		"logs"
	);
	private static readonly LogFileManager LocationLogFileManager = new(
		DirectoryPathProvider.LocationServiceLogFileDirectory,
		"loc-logs"
	);

	static LoggerService()
	{
		logger = SetupLogger();
	}

	public static void SetupLoggerService()
	{
		logger.Debug("LoggerService Created");
	}

	static Logger SetupLogger()
	{
		bool isGeneralLogFileDirectoryExisting = GeneralLogFileManager.CreateLogFileDirectoryIfNotExists();
		bool isLocationLogFileDirectoryExisting = LocationLogFileManager.CreateLogFileDirectoryIfNotExists();

		Exception? exceptionOnArchiveLastGeneralLogFile = GeneralLogFileManager.ArchiveLastLogFile();
		Exception? exceptionOnArchiveLastLocationLogFile = LocationLogFileManager.ArchiveLastLogFile();

#if DEBUG
		ConsoleTarget consoleTarget = new("console")
		{
			Layout = LOG_FORMAT,
			Encoding = Encoding.UTF8,
		};
		LoggingRule consoleLoggingRule = new("*", LogLevel.Trace, consoleTarget);
#endif

		LoggingConfiguration loggingConfiguration = new()
		{
			LoggingRules =
			{
#if DEBUG
				consoleLoggingRule
#endif
			},
		};
		if (isGeneralLogFileDirectoryExisting)
		{
			loggingConfiguration.AddRule(GetFileLoggingRule(GeneralLogFileManager));
		}

		if (isLocationLogFileDirectoryExisting)
		{
			loggingConfiguration.AddRule(GetFileLoggingRule(LocationLogFileManager));
		}

		LogManager
			.Setup()
			.LoadConfiguration(loggingConfiguration);

		Logger _logger = GetGeneralLogger();
		_logger.Info("TRViS Starting... (isGeneralLogFileDirectoryExisting: {0}, isLocationLogFileDirectoryExisting: {1})", isGeneralLogFileDirectoryExisting, isLocationLogFileDirectoryExisting);

		if (exceptionOnArchiveLastGeneralLogFile is not null)
			_logger.Error(exceptionOnArchiveLastGeneralLogFile, "ArchiveLastGeneralLogFile Failed");
		if (exceptionOnArchiveLastLocationLogFile is not null)
			_logger.Error(exceptionOnArchiveLastLocationLogFile, "ArchiveLastLocationLogFile Failed");

		GeneralLogFileManager.DeleteOldLogFiles(_logger);
		LocationLogFileManager.DeleteOldLogFiles(_logger);
		_logger.Info("LoggerService SetupLogger Completed");

		return _logger;
	}

	private static LoggingRule GetFileLoggingRule(
		LogFileManager logFileManager
	)
	{
		FileTarget fileTarget = new("file")
		{
			FileName = logFileManager.CurrentLogFilePath,
			ArchiveEvery = FileArchivePeriod.None,
			MaxArchiveFiles = LogFileManager.MAX_ARCHIVE_LOG_FILE_COUNT,

			Encoding = Encoding.UTF8,
			LineEnding = LineEndingMode.LF,
			WriteBom = false,
			CreateDirs = false,
			Layout = LOG_FORMAT,
		};
		AsyncTargetWrapper fileAsyncTargetWrapper = new("async", fileTarget)
		{
			OverflowAction = AsyncTargetWrapperOverflowAction.Grow,
			QueueLimit = 5000,
			BatchSize = 100,
			TimeToSleepBetweenBatches = 100,
		};
		return new(
			$"{logFileManager.LogCategory}.*",
#if DEBUG
			LogLevel.Trace,
#else
			LogLevel.Info,
#endif
			fileAsyncTargetWrapper
		);
	}

	private static string GetCallerClassName()
	{
		StackTrace stackTrace = new();
		StackFrame? stackFrame = stackTrace.GetFrame(2);
		if (stackFrame is null)
		{
			return nameof(TRViS);
		}
		return DiagnosticMethodInfo.Create(stackFrame)?.DeclaringTypeName ?? nameof(TRViS);
	}

	public static Logger GetGeneralLogger()
		=> GetGeneralLogger(GetCallerClassName());
	public static Logger GetGeneralLogger(Type type)
		=> GetGeneralLogger(type.FullName ?? nameof(TRViS));
	public static Logger GetGeneralLoggerT<T>()
		=> GetGeneralLogger(typeof(T));
	public static Logger GetGeneralLogger(string className)
		=> LogManager.GetLogger($"{GeneralLogFileManager.LogCategory}.{className}");

	public static Logger GetLocationServiceLogger()
		=> GetLocationServiceLogger(GetCallerClassName());
	public static Logger GetLocationServiceLogger(Type type)
		=> GetLocationServiceLogger(type.FullName ?? nameof(TRViS));
	public static Logger GetLocationServiceLoggerT<T>()
		=> GetLocationServiceLogger(typeof(T));
	public static Logger GetLocationServiceLogger(string className)
		=> LogManager.GetLogger($"{LocationLogFileManager.LogCategory}.{className}");
}
