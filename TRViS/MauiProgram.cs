using Microsoft.Maui.LifecycleEvents;

using NLog;

using TRViS.Services;
using TRViS.Utils;

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
			.ConfigureMauiHandlers(static handlers =>
			{
				handlers.AddHandler<Shell, HideShellTabRenderer>();
				// iOS 12.x: CollectionViewHandler2 (default in MAUI 10) uses
				// UICollectionViewCompositionalLayoutConfiguration which requires iOS 13+.
				// Fall back to the legacy CollectionViewHandler (uses UICollectionViewFlowLayout) on iOS 12.
				if (!OperatingSystem.IsIOSVersionAtLeast(13))
				{
					handlers.AddHandler<CollectionView, Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler>();
				}
			})
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
		InstanceManager.CrashlyticsWrapper.Log(ex, "UnhandledException");
	}

	private static MauiAppBuilder ConfigureFirebase(this MauiAppBuilder builder)
	{
		builder.ConfigureLifecycleEvents(events =>
		{
#if IOS
			events.AddiOS((iOS) => iOS.FinishedLaunching((_, _) =>
			{
				ConfigureFirebase();
				return true;
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
		try
		{
			Firebase.Core.App.Configure();
			Firebase.Crashlytics.Crashlytics.SharedInstance.SetCrashlyticsCollectionEnabled(true);
			SetCrashlyticsCustomKey();
			Firebase.Crashlytics.Crashlytics.SharedInstance.SendUnsentReports();

			// Flush buffered logs after Firebase is initialized
			logger.Info("Firebase initialized, flushing buffered logs");
			InstanceManager.AnalyticsWrapper.FlushBufferedLogs();
			InstanceManager.CrashlyticsWrapper.FlushBufferedLogs();
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Firebase.Core.App.Configure() failed");
			return;
		}
#endif
#elif ANDROID
		if (Platform.CurrentActivity is Android.App.Activity currentActivity)
			ConfigureFirebase(currentActivity);
		else
			logger.Warn("Firebase: CurrentActivity is null, skipping initialization");
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
		Firebase.Analytics.FirebaseAnalytics.GetInstance(activity).SetAnalyticsCollectionEnabled(true);
		SetCrashlyticsCustomKey();
		Firebase.Crashlytics.FirebaseCrashlytics.Instance.SendUnsentReports();

		// Flush buffered logs after Firebase is initialized
		logger.Info("Firebase initialized, flushing buffered logs");
		InstanceManager.AnalyticsWrapper.FlushBufferedLogs();
		InstanceManager.CrashlyticsWrapper.FlushBufferedLogs();
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

