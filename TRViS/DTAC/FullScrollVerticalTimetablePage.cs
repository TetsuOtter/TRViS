using TRViS.DTAC.Adapters;
using TRViS.DTAC.Logic.Presenter;
using TRViS.Services;

namespace TRViS.DTAC;

/// <summary>
/// The separated full-scroll D-TAC page (#155). iPhone-only entry point: the
/// embedded VerticalStylePage in the cached <see cref="ViewHost"/> is now
/// portrait-locked and only scrolls the timetable area, while this page hosts
/// the same timetable wrapped in a single outer ScrollView (the "full scroll"
/// experience) in landscape.
/// <para>
/// The timetable surface itself is the app-lifetime singleton
/// <see cref="InstanceManager.FullScrollVerticalStyleView"/>; this page is
/// transient (RegisterRoute'd) and only re-parents it. Rebuilding it per
/// navigation would multiply its shared-service subscriptions
/// (VerticalTimetableView -> LocationServiceAdapter -> shared LocationService
/// has no teardown), exactly the leak the singleton avoids.
/// </para>
/// </summary>
public class FullScrollVerticalTimetablePage : ContentPage
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public const string NameOfThisClass = nameof(FullScrollVerticalTimetablePage);

	public const string BackButtonAutomationId = "FullScroll.BackButton";

	readonly ViewHostPresenter _presenter;
	readonly AppBar AppBarView;
	readonly Grid _mainGrid;
	readonly WithRemarksView _content;

	public FullScrollVerticalTimetablePage()
	{
		logger.Trace("Creating...");

		// Per-navigation presenter, only for the AppBar title/clock. Mirrors
		// HorizontalTimetablePage; disposed on Unloaded. The interactive
		// timetable uses the shared VerticalStylePagePresenter via the
		// singleton view, which is NOT disposed here.
		_presenter = PresenterFactory.BuildViewHostPresenter(out _, out _, out _);
		_presenter.StateChanged += OnPresenterStateChanged;

		Shell.SetNavBarIsVisible(this, false);
		DTACElementStyles.DefaultBGColor.Apply(this, BackgroundColorProperty);

		_mainGrid = new Grid
		{
			SafeAreaEdges = SafeAreaEdges.None,
			RowDefinitions = new RowDefinitionCollection
			{
				new RowDefinition(GridLength.Auto),
				new RowDefinition(GridLength.Star),
			},
		};

		AppBarView = new AppBar
		{
			Title = _presenter.CurrentState.TitleText,
			LeftButtonText = DTACElementStyles.BackArrowIcon,
			LeftButtonAutomationId = BackButtonAutomationId,
			TimeLabelText = _presenter.CurrentState.TimeLabelText,
			IsTimeLabelEnabled = true,
			IsThemeButtonEnabled = true,
			IsAppIconButtonEnabled = false,
		};
		AppBarView.LeftButtonClicked += BackButton_Clicked;
		Grid.SetRow(AppBarView, 0);
		_mainGrid.Children.Add(AppBarView);

		_content = InstanceManager.FullScrollVerticalStyleView;
		if (_content.Parent is Layout oldParent)
			oldParent.Remove(_content);
		Grid.SetRow(_content, 1);
		_mainGrid.Children.Add(_content);

		Content = _mainGrid;

		if (Shell.Current is AppShell appShell)
		{
			appShell.SafeAreaMarginChanged += AppShell_SafeAreaMarginChanged;
			AppShell_SafeAreaMarginChanged(appShell, new(), appShell.SafeAreaMargin);
		}

		Unloaded += OnUnloaded;

		logger.Trace("Created");
	}

	private void OnUnloaded(object? sender, EventArgs e)
	{
		logger.Trace("Unloaded");
		// Leave the singleton parent-free so the next navigation can re-add it.
		_mainGrid.Remove(_content);
		_presenter.StateChanged -= OnPresenterStateChanged;
		_presenter.Dispose();
		if (Shell.Current is AppShell appShell)
			appShell.SafeAreaMarginChanged -= AppShell_SafeAreaMarginChanged;
	}

	private void AppShell_SafeAreaMarginChanged(object? sender, Thickness oldValue, Thickness newValue)
	{
		AppBarView.UpdateSafeAreaMargin(oldValue, newValue);
	}

	private async void BackButton_Clicked(object? sender, EventArgs e)
	{
		logger.Info("BackButton_Clicked -> GoBack with animation");
		await Shell.Current.GoToAsync("..", true);
	}

	private void OnPresenterStateChanged(object? sender, ViewHostStateChangedEventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			var state = _presenter.CurrentState;
			if ((e.Changed & ViewHostStateSection.TitleText) != 0)
				AppBarView.Title = state.TitleText;
			if ((e.Changed & ViewHostStateSection.TimeLabel) != 0)
				AppBarView.TimeLabelText = state.TimeLabelText;
		});
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		UpdateOrientation();
		// The singleton view is subscribed to the shared presenter forever, but
		// first appearance after a (re-)navigation still needs a kick to push
		// the current All-state into this view instance.
		if (_content.Content is VerticalStylePage vsp)
			vsp.OnViewBecameActive();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		// Unlock so back-navigation to the (now portrait-locked) ViewHost can
		// re-apply Portrait via its OnAppearing.
		if (IsIPhone)
			InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.All);
		InstanceManager.ScreenWakeLockService.DisableWakeLock();
	}

	private void UpdateOrientation()
	{
		// Landscape is the point of the full-scroll page on iPhone (it preserves
		// the prior 時刻表-tab landscape UX). This page is only reachable from
		// iPhone today; the non-iPhone arm is defensive.
		InstanceManager.OrientationService.SetOrientation(
			IsIPhone ? AppDisplayOrientation.Landscape : AppDisplayOrientation.All);
	}

	// iPhone == Phone idiom AND iOS platform (excludes iPad / Mac Catalyst),
	// matching the gating used by ViewHost / VerticalStylePage for #155.
	private static bool IsIPhone
		=> DeviceInfo.Current.Idiom == DeviceIdiom.Phone
		&& DeviceInfo.Current.Platform == DevicePlatform.iOS;
}
