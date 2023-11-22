using TRViS.ViewModels;

namespace TRViS;

internal static class InstanceManager
{
	private static AppViewModel? _AppViewModel = null;
	public static AppViewModel AppViewModel { get => _AppViewModel ??= new(); }

	private static AppCenterSettingViewModel? _AppCenterSettingViewModel = null;
	public static AppCenterSettingViewModel AppCenterSettingViewModel { get => _AppCenterSettingViewModel ??= new(); }

	private static DTACMarkerViewModel? _DTACMarkerViewModel = null;
	public static DTACMarkerViewModel DTACMarkerViewModel { get => _DTACMarkerViewModel ??= new(); }

	private static DTACViewHostViewModel? _DTACViewHostViewModel = null;
	public static DTACViewHostViewModel DTACViewHostViewModel { get => _DTACViewHostViewModel ??= new(); }

	private static EasterEggPageViewModel? _EasterEggPageViewModel = null;
	public static EasterEggPageViewModel EasterEggPageViewModel { get => _EasterEggPageViewModel ??= new(); }

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
