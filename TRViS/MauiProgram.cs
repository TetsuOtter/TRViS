using CommunityToolkit.Maui;
using TRViS.IO;
using TRViS.ViewModels;

namespace TRViS;

public static class MauiProgram
{
	static readonly string CrashLogFilePath;
	public static readonly DirectoryInfo CrashLogFileDirectory;
	static readonly string CrashLogFileName;

	static MauiProgram()
	{
		CrashLogFileName = $"{DateTime.Now:yyyyMMdd_HHmmss}.crashlog.trvis.txt";

		string baseDirPath;
		if (DeviceInfo.Current.Platform == DevicePlatform.iOS)
		{
			baseDirPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
		}
		else
		{
			baseDirPath = FileSystem.Current.AppDataDirectory;
		}


		CrashLogFileDirectory = new(Path.Combine(baseDirPath, "TRViS.InternalFiles", "crashlogs"));
		CrashLogFilePath = Path.Combine(CrashLogFileDirectory.FullName, CrashLogFileName);
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

		builder.Services
			.AddSingleton(typeof(AppShell))
			.AddSingleton(typeof(SelectTrainPage))
			.AddSingleton(typeof(EasterEggPage))
			.AddSingleton(typeof(DTAC.ViewHost))
			.AddSingleton(typeof(EasterEggPageViewModel))
			.AddSingleton(typeof(AppViewModel));

		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

		return builder.Build();
	}

	private static async void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is not Exception ex)
			return;

		if (!CrashLogFileDirectory.Exists)
		{
			CrashLogFileDirectory.Create();
		}

		await File.AppendAllTextAsync(CrashLogFilePath, $"{DateTime.Now:[yyyy/MM/dd HH:mm:ss]} {ex.Message}\n{ex.StackTrace}\n---\n(InnerException: {ex.InnerException})\n\n");
	}
}

