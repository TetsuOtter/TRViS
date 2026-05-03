using System.ComponentModel;

using TR.Maui.AnchorPopover;

using TRViS.DTAC.Adapters;
using TRViS.DTAC.Logic.Presenter;
using TRViS.Services;
using TRViS.Utils;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class ViewHost : ContentPage
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	static public readonly double TITLE_VIEW_HEIGHT = 50;
	public const string CHANGE_THEME_BUTTON_TEXT_TO_LIGHT = "\xe518";
	public const string CHANGE_THEME_BUTTON_TEXT_TO_DARK = "\xe51c";
	// 時刻表示が160px、残りはアイコンとWorkName分
	const int TIME_LABEL_VISIBLE_MIN_PARENT_WIDTH = (160 + 90) * 2;

	public static readonly string NameOfThisClass = nameof(ViewHost);

	DTACViewHostViewModel ViewModel { get; }

	readonly GradientStop TitleBG_Top = new(Colors.White.WithAlpha(0.8f), 0);
	readonly GradientStop TitleBG_Middle = new(Colors.White.WithAlpha(0.5f), 0.5f);
	readonly GradientStop TitleBG_MidBottom = new(Colors.White.WithAlpha(0.1f), 0.8f);
	readonly GradientStop TitleBG_Bottom = new(Colors.White.WithAlpha(0), 1);

	private readonly ViewHostPresenter _presenter;
	private readonly DTACViewHostViewModel _dtacViewModel;
	private readonly AppViewModel _appViewModel;

	public ViewHost()
	{
		logger.Trace("Creating...");

		_presenter = PresenterFactory.BuildViewHostPresenter(
			out AppViewModel vm,
			out EasterEggPageViewModel eevm,
			out DTACViewHostViewModel dtacViewModel);

		_appViewModel = vm;
		_dtacViewModel = dtacViewModel;

		_presenter.StateChanged += OnPresenterStateChanged;

		Shell.SetNavBarIsVisible(this, false);

		InitializeComponent();

		var state = _presenter.CurrentState;
		TitleLabel.Text = state.TitleText;
		Title = state.TitleText;
		TimeLabel.Text = state.TimeLabelText;

		TitleLabel.TextColor
			= MenuButton.TextColor
			= ChangeThemeButton.TextColor
			= TimeLabel.TextColor
			= eevm.ShellTitleTextColor;

		TitleBGBoxView.SetBinding(BoxView.ColorProperty, BindingBase.Create(static (EasterEggPageViewModel vm) => vm.ShellBackgroundColor, source: eevm));

		TitleBGGradientBox.Color = null;
		TitleBGGradientBox.Background = new LinearGradientBrush(
		[
			TitleBG_Top,
			TitleBG_Middle,
			TitleBG_MidBottom,
			TitleBG_Bottom,
		],
		new Point(0, 0),
		new Point(0, 1));

		vm.CurrentAppThemeChanged += (s, e) => SetTitleBGGradientColor(e.NewValue);
		SetTitleBGGradientColor(vm.CurrentAppTheme);
		eevm.PropertyChanged += Eevm_PropertyChanged;

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

		VerticalStylePageView.SetBinding(VerticalStylePage.SelectedTrainDataProperty, BindingBase.Create(static (AppViewModel vm) => vm.SelectedTrainData, source: vm));
		HakoRemarksView.SetBinding(WithRemarksView.RemarksDataProperty, BindingBase.Create(static (AppViewModel vm) => vm.SelectedWork, source: vm));
		VerticalStylePageRemarksView.SetBinding(WithRemarksView.RemarksDataProperty, BindingBase.Create(static (AppViewModel vm) => vm.SelectedTrainData, source: vm));

		ApplyTabVisibility();

		if (Shell.Current is AppShell appShell)
		{
			appShell.SafeAreaMarginChanged += AppShell_SafeAreaMarginChanged;
			AppShell_SafeAreaMarginChanged(appShell, new(), appShell.SafeAreaMargin);
		}

		DTACElementStyles.DefaultBGColor.Apply(this, BackgroundColorProperty);

		ChangeChangeThemeButtonText(vm.CurrentAppTheme);
		vm.CurrentAppThemeChanged += (s, e) => ChangeChangeThemeButtonText(e.NewValue);

		logger.Trace("Created");
	}

	void SetTitleBGGradientColor(AppTheme v)
		=> SetTitleBGGradientColor(v == AppTheme.Dark ? Colors.Black : Colors.White);
	void SetTitleBGGradientColor(Color v)
	{
		logger.Debug("newValue: {0}", v);
		TitleBG_Top.Color = v.WithAlpha(0.8f);
		TitleBG_Middle.Color = v.WithAlpha(0.5f);
		TitleBG_MidBottom.Color = v.WithAlpha(0.1f);
		TitleBG_Bottom.Color = v.WithAlpha(0);
	}

	private void AppShell_SafeAreaMarginChanged(object? sender, Thickness oldValue, Thickness newValue)
	{
		double top = newValue.Top;
		if (oldValue.Top == top
			&& oldValue.Left == newValue.Left
			&& oldValue.Right == newValue.Right)
		{
			logger.Trace("SafeAreaMargin is not changed -> do nothing");
			return;
		}

		TitleBGGradientBox.Margin = new(-newValue.Left, -top, -newValue.Right, TITLE_VIEW_HEIGHT * 0.5);
		TitlePaddingViewHeight.Height = new(top, GridUnitType.Absolute);
		MenuButton.Margin = new(8 + newValue.Left, 4);
		TimeLabelStack.Margin = new(8, 4, newValue.Right + 8, 4);
		logger.Debug("SafeAreaMargin is changed -> set TitleBGGradientBox.Margin to {0}", Util.ThicknessToString(TitleBGGradientBox.Margin));
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		try
		{
			logger.Trace("width: {0}, height: {1}", width, height);
			TimeLabel.IsVisible = (TIME_LABEL_VISIBLE_MIN_PARENT_WIDTH + TimeLabel.Margin.Right) < width;

			base.OnSizeAllocated(width, height);
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			Util.ExitWithAlertAsync(ex);
		}
	}

	private void MenuButton_Clicked(object? sender, EventArgs e)
	{
		Shell.Current.FlyoutIsPresented = !Shell.Current.FlyoutIsPresented;
		logger.Debug("FlyoutIsPresented is changed to {0}", Shell.Current.FlyoutIsPresented);
	}

	private void OnToggleBgAppIconButtonClicked(object? sender, EventArgs e)
	{
		bool newState = !_appViewModel.IsBgAppIconVisible;

		if (_appViewModel.CurrentAppTheme == AppTheme.Light && newState == false)
		{
			Utils.Util.DisplayAlertAsync(
				"背景を非表示にできません",
				"現在のテーマがライトモードのため、背景アイコンは非表示にできません。",
				"OK");
			return;
		}

		_appViewModel.IsBgAppIconVisible = newState;
		logger.Debug("IsBgAppIconVisible is now {0}", newState);
		if (sender is VisualElement button)
		{
			if (newState)
				DTACElementStyles.AppIconBgColor.Apply(button, BackgroundColorProperty);
			else
				button.BackgroundColor = Colors.Transparent;
		}
	}

	private void Eevm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not EasterEggPageViewModel vm)
			return;

		switch (e.PropertyName)
		{
			case nameof(EasterEggPageViewModel.ShellTitleTextColor):
				logger.Trace("ShellTitleTextColor is changed to {0}", vm.ShellTitleTextColor);
				TitleLabel.TextColor
					= MenuButton.TextColor
					= ChangeThemeButton.TextColor
					= TimeLabel.TextColor
					= vm.ShellTitleTextColor;
				break;
		}
	}

	private void ChangeChangeThemeButtonText(AppTheme newTheme)
	{
		logger.Trace("newTheme: {0}", newTheme);
		ChangeThemeButton.Text = newTheme == AppTheme.Dark
			? CHANGE_THEME_BUTTON_TEXT_TO_LIGHT
			: CHANGE_THEME_BUTTON_TEXT_TO_DARK;
	}

	private void OnChangeThemeButtonClicked(object? sender, EventArgs e)
	{
		logger.Info("ChangeThemeButton clicked");
		AppTheme newTheme = _appViewModel.CurrentAppTheme == AppTheme.Dark
			? AppTheme.Light
			: AppTheme.Dark;
		_appViewModel.CurrentAppTheme = newTheme;
		if (Application.Current is not null)
			Application.Current.UserAppTheme = newTheme;
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
			TitleLabel.Text = state.TitleText;
			Title = state.TitleText;
		}

		if ((changed & ViewHostStateSection.TimeLabel) != 0)
		{
			TimeLabel.Text = state.TimeLabelText;
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

	async void TitleLabel_Tapped(object sender, EventArgs e)
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

			await popover.ShowAsync(popup, TitleLabel, options);
			logger.Trace("QuickSwitchPopup shown");
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			await Util.ExitWithAlertAsync(ex);
		}
	}
}
