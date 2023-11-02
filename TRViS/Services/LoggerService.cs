using System.Text;

using Microsoft.AppCenter.Crashes;

using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace TRViS.Services;

public static class LoggerService
{
	static readonly Logger logger;
	const string logFormat = "${longdate} [${threadid:padding=3}] [${uppercase:${level:padding=-5}}] ${callsite}() ${message} ${exception:format=tostring}";

	static readonly DirectoryInfo LOG_FILE_DIRECTORY_INFO = DirectoryPathProvider.NormalLogFileDirectory;
	static readonly string LOG_FILE_DIRECTORY_PATH = LOG_FILE_DIRECTORY_INFO.FullName;
	const string CURRENT_LOG_FILE_NAME = "logs_current.trvis.log";
	const string ARCHIVE_LOG_FILE_NAME_FORMAT = "logs.{0}.trvis.log";
	const string ARCHIVE_LOG_FILE_NAME_PATTERN = "logs.*.trvis.log";

	static string CurrentLogFilePath => Path.Combine(LOG_FILE_DIRECTORY_PATH, CURRENT_LOG_FILE_NAME);
	const int MAX_ARCHIVE_LOG_FILE_COUNT = 14;

	static LoggerService()
	{
		logger = SetupLogger();
	}

	static public void SetupLoggerService()
	{
		logger.Debug("LoggerService Created");
	}

	static Exception? ArchiveLastLogFile()
	{
		Console.WriteLine("ArchiveLastLogFile");
		try
		{
			FileInfo lastLogFile = new(CurrentLogFilePath);
			if (!lastLogFile.Exists)
			{
				Console.WriteLine("lastLogFile not exists");
				return null;
			}

			string archiveLogFileName = string.Format(
				ARCHIVE_LOG_FILE_NAME_FORMAT,
				lastLogFile.CreationTime.ToUniversalTime().ToString("yyyyMMdd.HHmmss")
			);
			string archiveLogFilePath = Path.Combine(LOG_FILE_DIRECTORY_PATH, archiveLogFileName);

			Console.WriteLine("lastLogFile: {0} -> {1}", lastLogFile.FullName, archiveLogFilePath);
			File.Move(lastLogFile.FullName, archiveLogFilePath);
			return null;
		}
		catch (Exception ex)
		{
			return ex;
		}
	}

	static Exception? DeleteOldLogFiles(Logger _logger)
	{
		_logger.Trace("Executing...");
		try
		{
			IEnumerable<FileInfo> oldLogFiles = LOG_FILE_DIRECTORY_INFO.EnumerateFiles(
				ARCHIVE_LOG_FILE_NAME_PATTERN,
				SearchOption.TopDirectoryOnly
			)
				.OrderByDescending(fileInfo => fileInfo.LastWriteTime)
				.Skip(MAX_ARCHIVE_LOG_FILE_COUNT);

			foreach (FileInfo oldLogFile in oldLogFiles)
			{
				_logger.Info("Deleting oldLogFile: {0}", oldLogFile.FullName);
				oldLogFile.Delete();
			}

			return null;
		}
		catch (Exception ex)
		{
			return ex;
		}
	}

	static Logger SetupLogger()
	{
		bool isNormalLogFileDirectoryExists = LOG_FILE_DIRECTORY_INFO.Exists;
		if (!isNormalLogFileDirectoryExists)
		{
			LOG_FILE_DIRECTORY_INFO.Create();
		}

		Exception? exceptionOnArchiveLastLogFile = ArchiveLastLogFile();

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

		if (exceptionOnArchiveLastLogFile is not null)
			_logger.Error(exceptionOnArchiveLastLogFile, "ArchiveLastLogFile Failed");

		Exception? exceptionOnDeleteOldLogFiles = DeleteOldLogFiles(_logger);
		if (exceptionOnDeleteOldLogFiles is not null)
			_logger.Error(exceptionOnDeleteOldLogFiles, "DeleteOldLogFiles Failed");

		return _logger;
	}

	// AppCenter側で7MBまでの制限があるので、余裕をもってこの値を設定する
	const int MAX_LOG_LENGTH_TO_ATTACH = 6 * 1024 * 1024;
	// このCallbackは、Crash直後ではなく、Crash Logの送信直前に呼び出される。
	// つまり、次回起動時の実行になってしまう。
	public static ErrorAttachmentLog[] GetErrorAttachmentsCallback(ErrorReport report)
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

		if (!InstanceManager.AppCenterSettingViewModel.IsLogShareEnabled)
		{
			logger.Info("LogShare is disabled, so skipping...");
			return Array.Empty<ErrorAttachmentLog>();
		}

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
	}
}
