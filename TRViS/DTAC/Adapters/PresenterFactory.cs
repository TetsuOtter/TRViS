using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Presenter;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Factory for building a VerticalStylePagePresenter with all its adapters.
/// This is the only place in the View layer that references InstanceManager.
/// </summary>
internal static class PresenterFactory
{
	/// <summary>
	/// Builds a fully configured VerticalStylePagePresenter.
	/// </summary>
	public static VerticalStylePagePresenter Build()
	{
		var locationService = new LocationServiceAdapter(InstanceManager.LocationService);
		var wakeLock = new WakeLockAdapter(InstanceManager.ScreenWakeLockService);
		var easterEgg = new EasterEggSettingsAdapter(InstanceManager.EasterEggPageViewModel);
		var viewHostMode = new ViewHostModeAdapter(InstanceManager.DTACViewHostViewModel);
		var markerToggle = new MarkerToggleAdapter(InstanceManager.DTACMarkerViewModel);
		var crashLogger = new CrashLoggerAdapter(InstanceManager.CrashlyticsWrapper);
		var clock = new SystemClock();

		return new VerticalStylePagePresenter(
			locationService,
			wakeLock,
			easterEgg,
			viewHostMode,
			markerToggle,
			crashLogger,
			clock);
	}

	/// <summary>
	/// Simple clock implementation that delegates to DateTime.UtcNow.
	/// </summary>
	private class SystemClock : IClock
	{
		public DateTime UtcNow => DateTime.UtcNow;
	}
}
