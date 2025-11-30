using Microsoft.Maui.Controls;
using Microsoft.Maui.LifecycleEvents;

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
		logger = LoggerService.GetGeneralLogger();
	}

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
#if IOS
			.UseMauiMaps()
#endif
			.ConfigureFonts(static fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIconsRegular");
			})
			.ConfigureFirebase();

		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

		return builder.Build();
	}

	private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is not Exception ex)
			return;

		logger.Fatal(ex, "UnhandledException");
	}

	private static MauiAppBuilder ConfigureFirebase(this MauiAppBuilder builder)
	{
		builder.ConfigureLifecycleEvents(events =>
		{
#if IOS
			events.AddiOS((iOS) => iOS.WillFinishLaunching((_, _) =>
			{
				ConfigureFirebase();
				return false;
			}));
#elif ANDROID
			events.AddAndroid((android) => android.OnCreate((activity, _) =>
			{
				ConfigureFirebase(activity);
			}));
#endif
		});

		return builder;
	}

	private static bool IsFirebaseConfigured = false;
	public static void ConfigureFirebase()
	{
		if (!InstanceManager.FirebaseSettingViewModel.IsEnabled || IsFirebaseConfigured)
		{
			return;
		}

#if DISABLE_FIREBASE
		logger.Info("Firebase Disabled");
#endif

#if IOS
#if !DISABLE_FIREBASE
		Firebase.Core.App.Configure();
		Firebase.Crashlytics.Crashlytics.SharedInstance.SetCrashlyticsCollectionEnabled(true);
		SetCrashlyticsCustomKey();
		Firebase.Crashlytics.Crashlytics.SharedInstance.SendUnsentReports();
#endif
#elif ANDROID
		ConfigureFirebase(Platform.CurrentActivity);
#else
		logger.Warn("Firebase Unsupported platform");
#endif
		IsFirebaseConfigured = true;
	}
#if ANDROID
	public static void ConfigureFirebase(Android.App.Activity activity)
	{
		if (!InstanceManager.FirebaseSettingViewModel.IsEnabled || IsFirebaseConfigured)
		{
			return;
		}

#if !DISABLE_FIREBASE
		Firebase.FirebaseApp.InitializeApp(activity);
		Firebase.FirebaseAnalytics.GetInstance(activity).SetAnalyticsCollectionEnabled(true);
		SetCrashlyticsCustomKey();
		Firebase.Crashlytics.FirebaseCrashlytics.Instance.SendUnsentReports();
#endif
		IsFirebaseConfigured = true;
	}
#endif

#if IOS || ANDROID
	private static void SetCrashlyticsCustomKey()
	{
		SetCrashlyticsCustomKey("AppVersion", AppInfo.VersionString);
		SetCrashlyticsCustomKey("AppBuild", AppInfo.BuildString);
		SetCrashlyticsCustomKey("AppBundleId", AppInfo.PackageName);
		SetCrashlyticsCustomKey("AppPlatform", DeviceInfo.Current.Platform.ToString());
		SetCrashlyticsCustomKey("DeviceModel", DeviceInfo.Current.Model);
		SetCrashlyticsCustomKey("DeviceManufacturer", DeviceInfo.Current.Manufacturer);
		SetCrashlyticsCustomKey("DeviceName", DeviceInfo.Current.Name);
		SetCrashlyticsCustomKey("DeviceVersion", DeviceInfo.Current.VersionString);
		SetCrashlyticsCustomKey("DeviceType", DeviceInfo.Current.DeviceType.ToString());
	}
#endif

#if IOS
	private static void SetCrashlyticsCustomKey(string key, string value)
	{
		logger.Debug($"SetCrashlyticsCustomKey: {key} = {value}");
#if !DISABLE_FIREBASE
		Foundation.NSObject nsValue = new Foundation.NSString(value);
		Firebase.Crashlytics.Crashlytics.SharedInstance.SetCustomValue(key: key, value: nsValue);
#endif
	}
#elif ANDROID
	private static void SetCrashlyticsCustomKey(string key, string value)
	{
		logger.Debug($"SetCrashlyticsCustomKey: {key} = {value}");
#if !DISABLE_FIREBASE
		Firebase.Crashlytics.FirebaseCrashlytics.Instance.SetCustomKey(key, value);
#endif
	}
#endif
}

