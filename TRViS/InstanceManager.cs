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

	private static EasterEggPageViewModel? _EasterEggPageViewModel = null;
	public static EasterEggPageViewModel EasterEggPageViewModel { get => _EasterEggPageViewModel ??= new(); }
}
