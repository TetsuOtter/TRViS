using System.ComponentModel;

using TR.Maui.AnchorPopover;

using TRViS.DTAC.Adapters;
using TRViS.DTAC.Logic.Abstractions;
using TRViS.DTAC.Logic.Presenter;
using TRViS.RootPages;
using TRViS.Services;
using TRViS.Utils;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class ViewHost : ContentPage
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public static readonly string NameOfThisClass = nameof(ViewHost);

	DTACViewHostViewModel ViewModel { get; }

	private readonly ViewHostPresenter _presenter;
	private readonly DTACViewHostViewModel _dtacViewModel;

	public ViewHost()
	{
		logger.Trace("Creating...");

		_presenter = PresenterFactory.BuildViewHostPresenter(
			out AppViewModel vm,
			out _,
			out DTACViewHostViewModel dtacViewModel);

		_dtacViewModel = dtacViewModel;

		_presenter.StateChanged += OnPresenterStateChanged;
		// ViewHost is a cached ShellContent (DataTemplate) — the same instance is
		// reused on every navigation. Disposing the presenter on Unloaded would
		// permanently sever its event subscriptions, so the second visit would
		// freeze the clock and stop updating the title (#240).
		// HorizontalTimetablePage uses the same factory but is RegisterRoute'd
		// (a fresh page per navigation), so its Unloaded+Dispose remains correct.

		Shell.SetNavBarIsVisible(this, false);

		InitializeComponent();

		var state = _presenter.CurrentState;
		AppBarView.Title = state.TitleText;
		Title = state.TitleText;
		AppBarView.TimeLabelText = state.TimeLabelText;
		AppBarView.LeftButtonClicked += MenuButton_Clicked;
		AppBarView.TitleTapped += TitleLabel_Tapped;

		ViewModel = dtacViewModel;
		BindingContext = ViewModel;

		Shell.Current.Navigated += (s, e) =>
		{
			bool isCurrentPage = Shell.Current.CurrentPage is ViewHost;
			_dtacViewModel.IsViewHostVisible = isCurrentPage;
			if (isCurrentPage && _dtacViewModel.IsVerticalViewMode)
				VerticalStylePageView.OnViewBecameActive();
		};

		_dtacViewModel.PropertyChanged += OnDtacViewModelPropertyChanged;

		HakoRemarksView.SetBinding(WithRemarksView.RemarksDataProperty, BindingBase.Create(static (AppViewModel vm) => vm.SelectedWork, source: vm));
		VerticalStylePageRemarksView.SetBinding(WithRemarksView.RemarksDataProperty, BindingBase.Create(static (AppViewModel vm) => vm.SelectedTrainData, source: vm));

		ApplyTabVisibility();

		if (Shell.Current is AppShell appShell)
		{
			appShell.SafeAreaMarginChanged += AppShell_SafeAreaMarginChanged;
			AppShell_SafeAreaMarginChanged(appShell, new(), appShell.SafeAreaMargin);
		}

		DTACElementStyles.DefaultBGColor.Apply(this, BackgroundColorProperty);

#if UI_TEST
		AddTestNavigateHomeSeam();
		AddTestStateSeams();
		ApplyTestStateSeams(_presenter.CurrentState);
#endif

		logger.Trace("Created");
	}

#if UI_TEST
	// UI_TEST-only seam: invisible 24×24 button placed at the bottom-left corner
	// of the page (under any DTAC content but above the page background — the
	// last child in MainGrid means highest z-order). Tapping it issues
	// Shell.Current.GoToAsync("//StartHomePage") directly so shared-session
	// fixtures can return to Home from DTAC without the Shell flyout, which
	// is unreliable on Android once the VerticalView tab has locked
	// orientation to Landscape (CI run 25727806170: the MenuButton click
	// dispatches 200 OK but the NavigationView never attaches to the
	// DrawerLayout, so WaitForFlyoutItem times out 30 s later). GoToAsync away
	// from ViewHost triggers OnDisappearing which also unlocks the orientation.
	// Added in code-behind (not XAML) so production builds carry no seam at
	// all — important here because DTAC's bottom-left corner can be reached by
	// the user (no element occupies it in the test fixtures' state, but a
	// loaded real timetable could), and a transparent no-op button would
	// silently swallow taps in a production build.
	private void AddTestNavigateHomeSeam()
	{
		var seam = new Button
		{
			AutomationId = AutomationIdValueForTestNavigateHome,
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.End,
			WidthRequest = 24,
			HeightRequest = 24,
			BackgroundColor = Colors.Transparent,
			BorderColor = Colors.Transparent,
			Padding = 0,
			Margin = 0,
		};
		seam.Clicked += TestNavigateHomeButton_Clicked;
		Grid.SetRow(seam, 2);
		MainGrid.Children.Add(seam);
	}

	// Mirrors AutomationIds.DTAC.TestNavigateHomeButton in the test project
	// (which is the consumer). Inlined here to avoid a project reference.
	private const string AutomationIdValueForTestNavigateHome = "DTAC.TestNavigateHomeButton";

	// UI_TEST-only state seams. The AppBar's TitleLabel / TimeLabel are MAUI
	// Labels; iOS only surfaces a Label in the accessibility tree when its
	// text is non-empty, and TimeLabel additionally hides itself on narrow
	// screens (width threshold in AppBar). Both make assertions over the
	// presenter's state flaky on iPhone-portrait. These seam labels mirror
	// state.TitleText and state.TimeLabelText with a sentinel prefix so they
	// are always non-empty (always findable), and are kept invisible to the
	// user via transparent text + zero size + InputTransparent. Tests strip
	// the sentinel before asserting.
	private const string AutomationIdValueForTestTitleSeam = "DTAC.TestTitleSeam";
	private const string AutomationIdValueForTestTimeSeam = "DTAC.TestTimeSeam";
	private const string TestTitleSeamPrefix = "T:";
	private const string TestTimeSeamPrefix = "C:";
	private Label _testTitleSeamLabel = null!;
	private Label _testTimeSeamLabel = null!;

	private void AddTestStateSeams()
	{
		// Stack above the existing TestNavigateHomeButton in the bottom-LEFT
		// corner. The bottom-left of row 2 is already established as the
		// reserved test-seam region (see AddTestNavigateHomeSeam) so production
		// controls (NextTrainButton at bottom-right of the timetable, the AppBar
		// AppIcon/Theme/Time stack on the right) are guaranteed not to compete
		// with these labels. TestNavigateHomeButton sits at margin 0; offset
		// these by 28/56 px so the three seams form a non-overlapping vertical
		// strip up the left edge.
		_testTimeSeamLabel = BuildSeamLabel(
			AutomationIdValueForTestTimeSeam,
			TestTimeSeamPrefix,
			bottomMarginPx: 28);
		_testTitleSeamLabel = BuildSeamLabel(
			AutomationIdValueForTestTitleSeam,
			TestTitleSeamPrefix,
			bottomMarginPx: 56);
		Grid.SetRow(_testTimeSeamLabel, 2);
		Grid.SetRow(_testTitleSeamLabel, 2);
		MainGrid.Children.Add(_testTimeSeamLabel);
		MainGrid.Children.Add(_testTitleSeamLabel);
	}

	private static Label BuildSeamLabel(string automationId, string initialText, double bottomMarginPx)
	{
		// iOS XCUITest sets accessible="true" only when isAccessibilityElement
		// is YES, which UILabel computes from frame size + text presence + alpha.
		// 1×1 with transparent text falls below the threshold and the element is
		// returned as accessible="false" (FindElement skips it). Match the
		// existing TestNavigateHomeButton 24×24 footprint with a non-zero
		// FontSize so the text drives a11y presence; TextColor=Transparent +
		// InputTransparent keep it invisible and click-through.
		return new Label
		{
			AutomationId = automationId,
			Text = initialText,
			TextColor = Colors.Transparent,
			BackgroundColor = Colors.Transparent,
			InputTransparent = true,
			FontSize = 8,
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.End,
			WidthRequest = 24,
			HeightRequest = 24,
			Margin = new Thickness(0, 0, 0, bottomMarginPx),
			Padding = 0,
		};
	}

	private void ApplyTestStateSeams(ViewHostPageState state)
	{
		_testTitleSeamLabel.Text = TestTitleSeamPrefix + (state.TitleText ?? string.Empty);
		_testTimeSeamLabel.Text = TestTimeSeamPrefix + (state.TimeLabelText ?? string.Empty);
	}
#endif

	private void AppShell_SafeAreaMarginChanged(object? sender, Thickness oldValue, Thickness newValue)
	{
		AppBarView.UpdateSafeAreaMargin(oldValue, newValue);
	}

	private void MenuButton_Clicked(object? sender, EventArgs e)
	{
		Shell.Current.FlyoutIsPresented = !Shell.Current.FlyoutIsPresented;
		logger.Debug("FlyoutIsPresented is changed to {0}", Shell.Current.FlyoutIsPresented);
	}

	// ---------- DTACViewModel event handling (tab visibility, orientation) ----------

	private void OnDtacViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(DTACViewHostViewModel.IsHakoMode):
			case nameof(DTACViewHostViewModel.IsVerticalViewMode):
			case nameof(DTACViewHostViewModel.IsWorkAffixMode):
				MainThread.BeginInvokeOnMainThread(ApplyTabVisibility);
				break;
			case nameof(DTACViewHostViewModel.TabMode):
				MainThread.BeginInvokeOnMainThread(UpdateOrientation);
				if (_dtacViewModel.IsVerticalViewMode)
					VerticalStylePageView.OnViewBecameActive();
				break;
		}
	}

	private void ApplyTabVisibility()
	{
		HakoRemarksView.IsVisible = _dtacViewModel.IsHakoMode;
		VerticalStylePageRemarksView.IsVisible = _dtacViewModel.IsVerticalViewMode;
		WorkAffixView.IsVisible = _dtacViewModel.IsWorkAffixMode;

		if (!_dtacViewModel.IsHakoMode && HakoRemarksView.IsOpen)
			HakoRemarksView.IsOpen = false;
		if (!_dtacViewModel.IsVerticalViewMode && VerticalStylePageRemarksView.IsOpen)
			VerticalStylePageRemarksView.IsOpen = false;
	}

	private void UpdateOrientation()
	{
		if (DeviceInfo.Current.Idiom != DeviceIdiom.Phone)
		{
			InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.All);
			return;
		}

		AppDisplayOrientation desired = _dtacViewModel.TabMode switch
		{
			DTACViewHostViewModel.Mode.Hako => AppDisplayOrientation.Portrait,
			DTACViewHostViewModel.Mode.VerticalView => AppDisplayOrientation.Landscape,
			_ => AppDisplayOrientation.All,
		};
		InstanceManager.OrientationService.SetOrientation(desired);
	}

	// ---------- Presenter state change handling ----------

	private void OnPresenterStateChanged(object? sender, ViewHostStateChangedEventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(() => ApplyPresenterState(e.Changed));
	}

	private void ApplyPresenterState(ViewHostStateSection changed)
	{
		var state = _presenter.CurrentState;

#if UI_TEST
		ApplyTestStateSeams(state);
#endif

		if ((changed & ViewHostStateSection.TitleText) != 0)
		{
			AppBarView.Title = state.TitleText;
			Title = state.TitleText;
		}

		if ((changed & ViewHostStateSection.TimeLabel) != 0)
		{
			AppBarView.TimeLabelText = state.TimeLabelText;
		}
	}

	// ---------- MAUI lifecycle overrides ----------

	protected override void OnAppearing()
	{
		base.OnAppearing();
		UpdateOrientation();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone)
			InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.All);
		InstanceManager.ScreenWakeLockService.DisableWakeLock();
	}

#if UI_TEST
	async void TestNavigateHomeButton_Clicked(object? sender, EventArgs e)
	{
		logger.Info("TestNavigateHomeButton clicked: GoToAsync StartHomePage (bypassing flyout)");
		try
		{
			await Shell.Current.GoToAsync("//" + StartHomePage.NameOfThisClass);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "TestNavigateHomeButton failed");
		}
	}
#endif

	async void TitleLabel_Tapped(object? sender, EventArgs e)
	{
		try
		{
			logger.Info("TitleLabel tapped - showing QuickSwitchPopup");

			QuickSwitchPopup popup = new();
			var popover = AnchorPopover.Create();

			var options = new PopoverOptions
			{
				PreferredWidth = 280,
				PreferredHeight = 400,
				DismissOnTapOutside = true
			};

			await popover.ShowAsync(popup, AppBarView, options);
			logger.Trace("QuickSwitchPopup shown");
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			await Util.ExitWithAlertAsync(ex);
		}
	}
}
