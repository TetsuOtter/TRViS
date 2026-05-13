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
		Unloaded += (_, _) => _presenter.Dispose();

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
