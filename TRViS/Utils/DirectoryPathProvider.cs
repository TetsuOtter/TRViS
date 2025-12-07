namespace TRViS.Utils;

public static class DirectoryPathProvider
{
	static DirectoryPathProvider()
	{
		string baseDirPath;
		if (DeviceInfo.Current.Platform == DevicePlatform.iOS)
		{
			baseDirPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
		}
		else
		{
			baseDirPath = FileSystem.Current.AppDataDirectory;
		}

		InternalFilesDirectory = new(Path.Combine(baseDirPath, "TRViS.InternalFiles"));
		UserContentsDirectory = new(Path.Combine(baseDirPath, "TRViS.UserContents"));

		CrashLogFileDirectory = new(Path.Combine(InternalFilesDirectory.FullName, "crashlogs"));
		GeneralLogFileDirectory = new(Path.Combine(InternalFilesDirectory.FullName, "logs"));
		LocationServiceLogFileDirectory = new(Path.Combine(InternalFilesDirectory.FullName, "loc-logs"));

		TimetableFileDirectory = new(Path.Combine(UserContentsDirectory.FullName, "timetables"));
	}

	public static readonly DirectoryInfo InternalFilesDirectory;
	public static readonly DirectoryInfo CrashLogFileDirectory;
	public static readonly DirectoryInfo GeneralLogFileDirectory;
	public static readonly DirectoryInfo LocationServiceLogFileDirectory;
	public static readonly DirectoryInfo UserContentsDirectory;
	public static readonly DirectoryInfo TimetableFileDirectory;
}
