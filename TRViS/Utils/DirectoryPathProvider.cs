namespace TRViS;

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

		CrashLogFileDirectory = new(Path.Combine(InternalFilesDirectory.FullName, "crashlogs"));
		NormalLogFileDirectory = new(Path.Combine(InternalFilesDirectory.FullName, "logs"));
  }

	public static readonly DirectoryInfo InternalFilesDirectory;
	public static readonly DirectoryInfo CrashLogFileDirectory;
	public static readonly DirectoryInfo NormalLogFileDirectory;
}
