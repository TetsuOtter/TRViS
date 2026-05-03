using TRViS.FirebaseWrapper;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS;

internal static class InstanceManager
{
	private static AppViewModel? _AppViewModel = null;
	public static AppViewModel AppViewModel { get => _AppViewModel ??= new(); }

	private static FirebaseSettingViewModel? _FirebaseSettingViewModel = null;
	public static FirebaseSettingViewModel FirebaseSettingViewModel { get => _FirebaseSettingViewModel ??= new(); }

	private static DTACMarkerViewModel? _DTACMarkerViewModel = null;
	public static DTACMarkerViewModel DTACMarkerViewModel { get => _DTACMarkerViewModel ??= new(); }

	private static DTACViewHostViewModel? _DTACViewHostViewModel = null;
	public static DTACViewHostViewModel DTACViewHostViewModel { get => _DTACViewHostViewModel ??= new(); }

	private static EasterEggPageViewModel? _EasterEggPageViewModel = null;
	public static EasterEggPageViewModel EasterEggPageViewModel { get => _EasterEggPageViewModel ??= new(); }

	private static LocationService? _LocationService = null;
	public static LocationService LocationService
	{
		get
		{
			if (_LocationService is not null)
				return _LocationService;

			var timeProvider = TimeProvider;
			var httpClient = HttpClient;
			var eevm = EasterEggPageViewModel;
			var appViewModel = AppViewModel;

			_LocationService = new LocationService(
				LoggerService.GetGeneralLoggerT<LocationService>(),
				LoggerService.GetLocationServiceLoggerT<LocationService>(),
				LoggerService.GetLocationServiceLoggerT<TRViS.Services.LonLatLocationService>(),
				httpClient,
				timeProvider
			);

			// SHIP-BLOCKING: Dispatcher must be set before any UI event fires
			_LocationService.Dispatcher = a => MainThread.BeginInvokeOnMainThread(a);

			// Interval: sync with EasterEggPageViewModel
			_LocationService.Interval = TimeSpan.FromSeconds(eevm.LocationServiceInterval_Seconds);
			eevm.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(EasterEggPageViewModel.LocationServiceInterval_Seconds))
				{
					_LocationService.Interval = TimeSpan.FromSeconds(eevm.LocationServiceInterval_Seconds);
				}
			};

			// Create adapters
			_LocationServiceGpsAdapter = new LocationServiceGpsAdapter(_LocationService);
			_LocationServiceIdSyncAdapter = new LocationServiceIdSyncAdapter(_LocationService, appViewModel);
			_LocationServiceAlertSubscriber = new LocationServiceAlertSubscriber(_LocationService);

			return _LocationService;
		}
	}

	private static LocationServiceGpsAdapter? _LocationServiceGpsAdapter = null;
	public static LocationServiceGpsAdapter LocationServiceGpsAdapter => _LocationServiceGpsAdapter ?? throw new InvalidOperationException("LocationService must be initialized first");

	private static LocationServiceIdSyncAdapter? _LocationServiceIdSyncAdapter = null;
	private static LocationServiceAlertSubscriber? _LocationServiceAlertSubscriber = null;

	private static ITimeProvider? _TimeProvider = null;
	public static ITimeProvider TimeProvider { get => _TimeProvider ??= new AppTimeProvider(); }

	private static CrashlyticsWrapper? _CrashlyticsWrapper = null;
	public static ICrashlyticsWrapper CrashlyticsWrapper => _CrashlyticsWrapper ??= new();
	private static AnalyticsWrapper? _AnalyticsWrapper = null;
	public static IAnalyticsWrapper AnalyticsWrapper => _AnalyticsWrapper ??= new();

	private static IOrientationService? _OrientationService = null;
	public static IOrientationService OrientationService
	{
		get
		{
			if (_OrientationService is not null)
				return _OrientationService;

#if ANDROID
			_OrientationService = new Platforms.Android.OrientationService();
#elif IOS
			_OrientationService = new Platforms.iOS.OrientationService();
#elif MACCATALYST
			_OrientationService = new Platforms.MacCatalyst.OrientationService();
#elif WINDOWS
			_OrientationService = new Platforms.Windows.OrientationService();
#else
			_OrientationService = new DefaultOrientationService();
#endif
			return _OrientationService;
		}
	}

	private static IScreenWakeLockService? _ScreenWakeLockService = null;
	public static IScreenWakeLockService ScreenWakeLockService
	{
		get
		{
			if (_ScreenWakeLockService is not null)
				return _ScreenWakeLockService;

#if ANDROID
			_ScreenWakeLockService = new Platforms.Android.ScreenWakeLockService();
#elif IOS
			_ScreenWakeLockService = new Platforms.iOS.ScreenWakeLockService();
#elif MACCATALYST
			_ScreenWakeLockService = new Platforms.MacCatalyst.ScreenWakeLockService();
#elif WINDOWS
			_ScreenWakeLockService = new Platforms.Windows.ScreenWakeLockService();
#else
			_ScreenWakeLockService = new DefaultScreenWakeLockService();
#endif
			return _ScreenWakeLockService;
		}
	}

	static HttpClient? _HttpClient = null;
	public static HttpClient HttpClient
	{
		get
		{
			if (_HttpClient is not null)
				return _HttpClient;

			_HttpClient = new()
			{
				Timeout = TimeSpan.FromSeconds(5),
			};

			return _HttpClient;
		}
	}

	static void DisposeValue<T>(ref T? value) where T : class, IDisposable
	{
		T? tmp = value;
		value = null;
		tmp?.Dispose();
	}
	public static void Dispose()
	{
		DisposeValue(ref _LocationServiceGpsAdapter);
		DisposeValue(ref _LocationServiceIdSyncAdapter);
		DisposeValue(ref _LocationServiceAlertSubscriber);
		DisposeValue(ref _LocationService);
		DisposeValue(ref _HttpClient);
	}

	/// <summary>
	/// Default fallback implementation of the orientation service when no platform-specific implementation is available.
	/// </summary>
	private class DefaultOrientationService : IOrientationService
	{
		public void SetOrientation(AppDisplayOrientation orientation)
		{
			// No-op for unsupported platforms
		}
	}

	/// <summary>
	/// Default fallback implementation of the screen wake lock service when no platform-specific implementation is available.
	/// </summary>
	private class DefaultScreenWakeLockService : IScreenWakeLockService
	{
		public bool IsWakeLockEnabled => false;

		public void EnableWakeLock()
		{
			// No-op for unsupported platforms
		}

		public void DisableWakeLock()
		{
			// No-op for unsupported platforms
		}
	}
}
