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
		var markerToggle = new MarkerToggleAdapter(InstanceManager.DTACMarkerViewModel);
		var crashLogger = new CrashLoggerAdapter(InstanceManager.CrashlyticsWrapper);
		var clock = new SystemClock();
		var appViewModelProvider = new AppViewModelAdapter(InstanceManager.AppViewModel);

		return new VerticalStylePagePresenter(
			locationService,
			markerToggle,
			crashLogger,
			clock,
			appViewModelProvider);
	}

	/// <summary>
	/// Builds a fully configured ViewHostPresenter.
	/// Out parameters expose the underlying MAUI objects needed for pure UI bindings.
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
		var timeProvider = new TimeProviderAdapter(InstanceManager.LocationService);

		return new ViewHostPresenter(
			appViewModelAdapter,
			timeProvider);
	}

	/// <summary>
	/// Builds a fully configured HakoPresenter.
	/// </summary>
	public static HakoPresenter BuildHakoPresenter()
	{
		var appViewModel = new AppViewModelAdapter(InstanceManager.AppViewModel);
		var crashLogger = new CrashLoggerAdapter(InstanceManager.CrashlyticsWrapper);
		return new HakoPresenter(appViewModel, crashLogger);
	}

	/// <summary>
	/// Builds a fully configured VerticalTimetableViewPresenter.
	/// </summary>
	public static VerticalTimetableViewPresenter BuildVerticalTimetableViewPresenter(
		TRViS.DTAC.ViewModels.VerticalTimetableViewModel viewModel)
	{
		var markerToggle = new MarkerToggleAdapter(InstanceManager.DTACMarkerViewModel);
		var crashLogger = new CrashLoggerAdapter(InstanceManager.CrashlyticsWrapper);
		var dataSource = new VerticalTimetableDataSourceAdapter(viewModel);
		return new VerticalTimetableViewPresenter(markerToggle, crashLogger, dataSource);
	}

	/// <summary>
	/// Builds a fully configured NextTrainButtonPresenter.
	/// </summary>
	public static NextTrainButtonPresenter BuildNextTrainButtonPresenter()
	{
		var rawAppViewModel = InstanceManager.AppViewModel;
		var trainDataProvider = new NextTrainDataProviderAdapter(rawAppViewModel);
		var appViewModelProvider = new AppViewModelAdapter(rawAppViewModel);
		var crashLogger = new CrashLoggerAdapter(InstanceManager.CrashlyticsWrapper);
		var userAlerts = new UserAlertAdapter();

		return new NextTrainButtonPresenter(
			trainDataProvider,
			appViewModelProvider,
			crashLogger,
			userAlerts);
	}

	/// <summary>
	/// Returns the shared DTACMarkerViewModel so View-layer components can access
	/// MAUI-typed properties (Color, SelectedText) without referencing InstanceManager directly.
	/// </summary>
	public static TRViS.ViewModels.DTACMarkerViewModel GetDTACMarkerViewModel()
		=> InstanceManager.DTACMarkerViewModel;

	/// <summary>
	/// Returns the shared LocationService adapter for subscribing to ExceptionThrown
	/// from View-layer components.
	/// </summary>
	public static LocationServiceAdapter GetLocationServiceAdapter()
		=> new LocationServiceAdapter(InstanceManager.LocationService);

	/// <summary>
	/// Returns a crash logger wrapping the global CrashlyticsWrapper.
	/// Used by View-layer components that need to log exceptions to Crashlytics.
	/// </summary>
	public static IDtacCrashLogger GetCrashLogger()
		=> new CrashLoggerAdapter(InstanceManager.CrashlyticsWrapper);

	/// <summary>
	/// Returns the raw AppViewModel for View-layer components that need it as BindingContext.
	/// InstanceManager is only referenced here, not scattered across View files.
	/// </summary>
	public static TRViS.ViewModels.AppViewModel GetRawAppViewModel()
		=> InstanceManager.AppViewModel;

	/// <summary>
	/// Returns the raw DTACMarkerViewModel for View-layer components that need it as BindingContext.
	/// InstanceManager is only referenced here, not scattered across View files.
	/// </summary>
	public static TRViS.ViewModels.DTACMarkerViewModel GetRawMarkerViewModel()
		=> InstanceManager.DTACMarkerViewModel;

	/// <summary>
	/// Returns the raw DTACViewHostViewModel for View-layer components that need to write tab mode.
	/// InstanceManager is only referenced here, not scattered across View files.
	/// </summary>
	public static TRViS.ViewModels.DTACViewHostViewModel GetRawViewHostViewModel()
		=> InstanceManager.DTACViewHostViewModel;

	/// <summary>
	/// Simple clock implementation that delegates to DateTime.UtcNow.
	/// </summary>
	private class SystemClock : IClock
	{
		public DateTime UtcNow => DateTime.UtcNow;
	}
}
