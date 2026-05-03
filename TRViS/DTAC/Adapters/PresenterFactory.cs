using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Presenter;

namespace TRViS.DTAC.Adapters;

/// <summary>
/// Factory for building Presenter instances with all their adapters.
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
	/// Builds a fully configured ViewHostPresenter.
	/// The ViewHostModeAdapter acts as both IViewHostModeProvider and IViewHostNavigationSink.
	/// Out parameters expose the underlying MAUI objects so that ViewHost.xaml.cs
	/// can set up pure MAUI bindings without itself referencing InstanceManager.
	/// </summary>
	public static ViewHostPresenter BuildViewHostPresenter(
		out TRViS.ViewModels.AppViewModel rawAppViewModel,
		out TRViS.ViewModels.EasterEggPageViewModel rawEasterEggViewModel,
		out TRViS.ViewModels.DTACViewHostViewModel rawViewHostViewModel)
	{
		rawAppViewModel = InstanceManager.AppViewModel;
		rawEasterEggViewModel = InstanceManager.EasterEggPageViewModel;
		rawViewHostViewModel = InstanceManager.DTACViewHostViewModel;

		var appViewModelAdapter = new AppViewModelAdapter(rawAppViewModel);
		var viewHostMode = new ViewHostModeAdapter(rawViewHostViewModel);
		var timeProvider = new TimeProviderAdapter(InstanceManager.LocationService);
		var easterEgg = new EasterEggSettingsAdapter(rawEasterEggViewModel);
		var wakeLock = new WakeLockAdapter(InstanceManager.ScreenWakeLockService);
		var orientation = new OrientationControllerAdapter(InstanceManager.OrientationService);
		var userAlerts = new UserAlertAdapter();
		var crashLogger = new CrashLoggerAdapter(InstanceManager.CrashlyticsWrapper);

		return new ViewHostPresenter(
			appViewModelAdapter,
			viewHostMode,
			timeProvider,
			easterEgg,
			wakeLock,
			orientation,
			userAlerts,
			crashLogger,
			navigationSink: viewHostMode);
	}

	/// <summary>
	/// Builds a fully configured HakoPresenter.
	/// </summary>
	public static HakoPresenter BuildHakoPresenter()
	{
		var crashLogger = new CrashLoggerAdapter(InstanceManager.CrashlyticsWrapper);
		return new HakoPresenter(crashLogger);
	}

	/// <summary>
	/// Simple clock implementation that delegates to DateTime.UtcNow.
	/// </summary>
	private class SystemClock : IClock
	{
		public DateTime UtcNow => DateTime.UtcNow;
	}
}
