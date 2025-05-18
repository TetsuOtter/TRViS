using NLog;

namespace TRViS.Services;

public class LogFileManager(
	DirectoryInfo logFileDirectoryInfo,
	string category
)
{
	public readonly DirectoryInfo LogFileDirectoryInfo = logFileDirectoryInfo;
	public readonly string LogCategory = category;

	public string LogFileDirectoryPath => LogFileDirectoryInfo.FullName;
	public string CurrentLogFileName => $"{LogCategory}_current.trvis.log";
	public string ArchiveLogFileNameFormat => $"{LogCategory}.{{0}}.trvis.log";
	public string ArchiveLogFileNamePattern => $"{LogCategory}.*.trvis.log";

	public string CurrentLogFilePath => Path.Combine(LogFileDirectoryPath, CurrentLogFileName);
	public const int MAX_ARCHIVE_LOG_FILE_COUNT = 14;

	public bool CreateLogFileDirectoryIfNotExists()
	{
		try
		{
			if (!LogFileDirectoryInfo.Exists)
			{
				LogFileDirectoryInfo.Create();
			}
			return true;
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[{LogCategory}] CreateLogFileDirectoryIfNotExists failed: {ex}");
			return false;
		}
	}

	public Exception? ArchiveLastLogFile()
	{
		Console.WriteLine($"[{LogCategory}] ArchiveLastLogFile");
		try
		{
			FileInfo lastLogFile = new(CurrentLogFilePath);
			if (!lastLogFile.Exists)
			{
				Console.WriteLine($"[{LogCategory}] lastLogFile not exists");
				return null;
			}

			string archiveLogFileName = string.Format(
				ArchiveLogFileNameFormat,
				lastLogFile.CreationTime.ToUniversalTime().ToString("yyyyMMdd.HHmmss")
			);
			string archiveLogFilePath = Path.Combine(LogFileDirectoryPath, archiveLogFileName);

			Console.WriteLine($"[{LogCategory}] lastLogFile: {0} -> {1}", lastLogFile.FullName, archiveLogFilePath);
			File.Move(lastLogFile.FullName, archiveLogFilePath);
			return null;
		}
		catch (Exception ex)
		{
			return ex;
		}
	}

	public void DeleteOldLogFiles(Logger logger)
	{
		logger.Trace("Executing...");
		try
		{
			IEnumerable<FileInfo> oldLogFiles = LogFileDirectoryInfo.EnumerateFiles(
				ArchiveLogFileNamePattern,
				SearchOption.TopDirectoryOnly
			)
				.OrderByDescending(static fileInfo => fileInfo.LastWriteTime)
				.Skip(MAX_ARCHIVE_LOG_FILE_COUNT);

			foreach (FileInfo oldLogFile in oldLogFiles)
			{
				logger.Info("Deleting oldLogFile: {0}", oldLogFile.FullName);
				oldLogFile.Delete();
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "DeleteOldLogFiles failed");
		}
	}
}
