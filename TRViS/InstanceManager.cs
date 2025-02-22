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
	public static LocationService LocationService { get => _LocationService ??= new(); }

	private static CrashlyticsWrapper? _CrashlyticsWrapper = null;
	public static ICrashlyticsWrapper CrashlyticsWrapper => _CrashlyticsWrapper ??= new();
	private static AnalyticsWrapper? _AnalyticsWrapper = null;
	public static IAnalyticsWrapper AnalyticsWrapper => _AnalyticsWrapper ??= new();

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
		DisposeValue(ref _HttpClient);
	}
}
