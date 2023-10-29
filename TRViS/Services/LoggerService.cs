using System.Text;

using Microsoft.AppCenter.Crashes;

using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace TRViS.Services;

public static class LoggerService
{
	static readonly Logger logger = SetupLogger();
	const string logFormat = "${longdate} [${threadid:padding=3}] [${uppercase:${level:padding=-5}}] ${callsite}() ${message} ${exception:format=tostring}";

	static readonly DirectoryInfo LOG_FILE_DIRECTORY_INFO = DirectoryPathProvider.NormalLogFileDirectory;
	static readonly string LOG_FILE_DIRECTORY_PATH = LOG_FILE_DIRECTORY_INFO.FullName;
	const string CURRENT_LOG_FILE_NAME = "logs_current.trvis.log";
	const string ARCHIVE_LOG_FILE_NAME_FORMAT = "logs.{#}.trvis.log";
	const string ARCHIVE_LOG_FILE_NAME_PATTERN = "logs.*.trvis.log";

	static string CurrentLogFilePath => Path.Combine(LOG_FILE_DIRECTORY_PATH, CURRENT_LOG_FILE_NAME);
	static string ArchiveLogFilePathFormat => Path.Combine(LOG_FILE_DIRECTORY_PATH, ARCHIVE_LOG_FILE_NAME_FORMAT);

	static public void SetupLoggerService()
	{
		logger.Debug("LoggerService Created");
	}

	static Logger SetupLogger()
	{
		bool isNormalLogFileDirectoryExists = LOG_FILE_DIRECTORY_INFO.Exists;
		if (!isNormalLogFileDirectoryExists)
		{
			LOG_FILE_DIRECTORY_INFO.Create();
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
			FileName = CurrentLogFilePath,
			ArchiveFileName = ArchiveLogFilePathFormat,
			ArchiveNumbering = ArchiveNumberingMode.Sequence,
			ArchiveEvery = FileArchivePeriod.None,
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

	// AppCenter側で7MBまでの制限があるので、余裕をもってこの値を設定する
	const int MAX_LOG_LENGTH_TO_ATTACH = 6 * 1024 * 1024;
	// このCallbackは、Crash直後ではなく、Crash Logの送信直前に呼び出される。
	// つまり、次回起動時の実行になってしまう。
	public static readonly GetErrorAttachmentsCallback GetErrorAttachmentsCallback = (report) =>
	{
		logger.Debug("called with report: Id:{0}, AppStart:{1}, AppCrash:{2}, Details:{3}, StackTrace:{4}",
			report.Id,
			report.AppStartTime,
			report.AppErrorTime,
			#if IOS || MACCATALYST
			report.AppleDetails,
			#elif ANDROID
			report.AndroidDetails,
			#else
			report.Exception,
			#endif
			report.StackTrace
		);

		try
		{
			string? lastLogFilePath = LOG_FILE_DIRECTORY_INFO.EnumerateFiles(
				ARCHIVE_LOG_FILE_NAME_PATTERN,
				SearchOption.TopDirectoryOnly
			)
				.OrderByDescending(fileInfo => fileInfo.LastWriteTime)
				.FirstOrDefault()
				?.FullName;
			if (string.IsNullOrEmpty(lastLogFilePath))
			{
				logger.Warn("lastLogFilePath is null or empty");
				return Array.Empty<ErrorAttachmentLog>();
			}

			logger.Debug("lastLogFilePath: {0}", lastLogFilePath);

			byte[] logFileContent = File.ReadAllBytes(lastLogFilePath);
			logger.Info("logFileContent.Length: {0}", logFileContent.Length);

			if (MAX_LOG_LENGTH_TO_ATTACH < logFileContent.Length)
			{
				logFileContent = logFileContent[^MAX_LOG_LENGTH_TO_ATTACH..];
				logger.Info("logFileContent.Length: trimmed to {0}", logFileContent.Length);
			}

			var attachments = new ErrorAttachmentLog[]
			{
				ErrorAttachmentLog.AttachmentWithBinary(
					logFileContent,
					CURRENT_LOG_FILE_NAME,
					"text/plain"
				),
			};
			return attachments;
		}
		catch (Exception ex)
		{
			logger.Error(ex, "GetErrorAttachmentsCallback Failed");
			return Array.Empty<ErrorAttachmentLog>();
		}
	};
}
