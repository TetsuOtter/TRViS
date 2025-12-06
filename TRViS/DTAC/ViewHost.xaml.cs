using System.ComponentModel;

using TR.Maui.AnchorPopover;

using TRViS.IO.Models;
using TRViS.Services;
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

	DTACViewHostViewModel ViewModel { get; }

	readonly GradientStop TitleBG_Top = new(Colors.White.WithAlpha(0.8f), 0);
	readonly GradientStop TitleBG_Middle = new(Colors.White.WithAlpha(0.5f), 0.5f);
	readonly GradientStop TitleBG_MidBottom = new(Colors.White.WithAlpha(0.1f), 0.8f);
	readonly GradientStop TitleBG_Bottom = new(Colors.White.WithAlpha(0), 1);

	public ViewHost()
	{
		logger.Trace("Creating...");

		AppViewModel vm = InstanceManager.AppViewModel;
		EasterEggPageViewModel eevm = InstanceManager.EasterEggPageViewModel;

		Shell.SetNavBarIsVisible(this, false);

		InitializeComponent();

		TitleLabel.Text = vm.SelectedWork?.Name;
		TitleLabel.TextColor
			= MenuButton.TextColor
			= ChangeThemeButton.TextColor
			= TimeLabel.TextColor
			= eevm.ShellTitleTextColor;

		InstanceManager.LocationService.TimeChanged += (s, totalSeconds) =>
		{
			bool isMinus = totalSeconds < 0;
			int Hour = Math.Abs(totalSeconds / 3600);
			int Minute = Math.Abs((totalSeconds % 3600) / 60);
			int Second = Math.Abs(totalSeconds % 60);

			string text = isMinus ? "-" : string.Empty;
			text += $"{Hour:D2}:{Minute:D2}:{Second:D2}";
			TimeLabel.Text = text;
		};

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
		vm.PropertyChanged += Vm_PropertyChanged;
		eevm.PropertyChanged += Eevm_PropertyChanged;

		ViewModel = InstanceManager.DTACViewHostViewModel;
		BindingContext = ViewModel;

		ViewModel.PropertyChanged += ViewModel_PropertyChanged;

		Shell.Current.Navigated += (s, e) =>
		{
			ViewModel.IsViewHostVisible = Shell.Current.CurrentPage is ViewHost;
		};

		VerticalStylePageView.SetBinding(VerticalStylePage.SelectedTrainDataProperty, BindingBase.Create(static (AppViewModel vm) => vm.SelectedTrainData, source: vm));
		HakoRemarksView.SetBinding(WithRemarksView.RemarksDataProperty, BindingBase.Create(static (AppViewModel vm) => vm.SelectedWork, source: vm));
		VerticalStylePageRemarksView.SetBinding(WithRemarksView.RemarksDataProperty, BindingBase.Create(static (AppViewModel vm) => vm.SelectedTrainData, source: vm));

		UpdateContent();

		if (Shell.Current is AppShell appShell)
		{
			appShell.SafeAreaMarginChanged += AppShell_SafeAreaMarginChanged;
			AppShell_SafeAreaMarginChanged(appShell, new(), appShell.SafeAreaMargin);
		}

		DTACElementStyles.DefaultBGColor.Apply(this, BackgroundColorProperty);

		OnSelectedWorkGroupChanged(vm.SelectedWorkGroup);
		OnSelectedWorkChanged(vm.SelectedWork);
		OnSelectedTrainChanged(vm.SelectedTrainData);

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
		TimeLabel.Margin = new(0, 0, newValue.Right, 0);
		logger.Debug("SafeAreaMargin is changed -> set TitleBGGradientBox.Margin to {0}", Utils.ThicknessToString(TitleBGGradientBox.Margin));
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
			InstanceManager.CrashlyticsWrapper.Log(ex, "ViewHost.OnSizeAllocated");
			Utils.ExitWithAlert(ex);
		}
	}

	private void MenuButton_Clicked(object? sender, EventArgs e)
	{
		Shell.Current.FlyoutIsPresented = !Shell.Current.FlyoutIsPresented;
		logger.Debug("FlyoutIsPresented is changed to {0}", Shell.Current.FlyoutIsPresented);
	}

	private void OnToggleBgAppIconButtonClicked(object? sender, EventArgs e)
	{
		bool newState = !InstanceManager.AppViewModel.IsBgAppIconVisible;
		if (InstanceManager.AppViewModel.CurrentAppTheme == AppTheme.Light
			&& newState == false)
		{
			logger.Warn("IsBgAppIconVisible is not changed to false because CurrentAppTheme is Light");
			Utils.DisplayAlert("背景を非表示にできません", "現在のテーマがライトモードのため、背景アイコンは非表示にできません。", "OK");
			return;
		}
		InstanceManager.AppViewModel.IsBgAppIconVisible = newState;
		logger.Debug("IsBgAppIconVisible is changed to {0}", newState);
		if (sender is VisualElement button)
		{
			if (newState)
			{
				DTACElementStyles.AppIconBgColor.Apply(button, BackgroundColorProperty);
			}
			else
			{
				button.BackgroundColor = Colors.Transparent;
			}
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

	private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not AppViewModel vm)
			return;

		try
		{
			switch (e.PropertyName)
			{
				case nameof(AppViewModel.SelectedWorkGroup):
					OnSelectedWorkGroupChanged(vm.SelectedWorkGroup);
					break;

				case nameof(AppViewModel.SelectedWork):
					OnSelectedWorkChanged(vm.SelectedWork);
					break;

				case nameof(AppViewModel.SelectedTrainData):
					OnSelectedTrainChanged(vm.SelectedTrainData);
					break;
			}
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			InstanceManager.CrashlyticsWrapper.Log(ex, "ViewHost.Vm_PropertyChanged");
			Utils.ExitWithAlert(ex);
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
		AppViewModel vm = InstanceManager.AppViewModel;
		AppTheme newTheme = vm.CurrentAppTheme == AppTheme.Dark
			? AppTheme.Light
			: AppTheme.Dark;

		if (Application.Current is not null)
		{
			logger.Info(
				"CurrentAppTheme is changed to {0} (User: {1}, Platform: {2}, Requested: {3})",
				newTheme,
				Application.Current.UserAppTheme,
				Application.Current.PlatformAppTheme,
				Application.Current.RequestedTheme
			);
			vm.CurrentAppTheme = newTheme;
			Application.Current.UserAppTheme = newTheme;
		}
		else
		{
			logger.Warn("Application.Current is null -> do nothing");
		}
	}

	void OnSelectedWorkGroupChanged(WorkGroup? newValue)
	{
		string title = newValue?.Name ?? string.Empty;
		logger.Info("SelectedWorkGroup is changed to {0}", title);
		HakoView.WorkSpaceName = title;
	}

	void OnSelectedWorkChanged(Work? newValue)
	{
		string title = newValue?.Name ?? string.Empty;
		logger.Info("SelectedWork is changed to {0}", title);
		TitleLabel.Text = title;
		Title = title;

		HakoView.WorkName = title;
	}

	void OnSelectedTrainChanged(TrainData? newValue)
	{
		int dayCount = newValue?.DayCount ?? 0;
		string affectDate = (
			newValue?.AffectDate
			?? DateOnly.FromDateTime(DateTime.Now).AddDays(-dayCount)
		).ToString("yyyy年M月d日");

		logger.Debug(
			"date: {0}, dayCount: {1}, AffectDate: {2}",
			newValue?.AffectDate,
			dayCount,
			affectDate
		);

		VerticalStylePageView.AffectDate = affectDate;
		HakoView.AffectDate = affectDate;
	}

	private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(DTACViewHostViewModel.TabMode))
			UpdateContent();
	}

	void UpdateContent()
	{
		logger.Debug("TabMode is changed to {0} (IsHakoMode: {1}/IsVerticalViewMode: {2}/IsWorkAffixMode: {3})",
			ViewModel.TabMode,
			ViewModel.IsHakoMode,
			ViewModel.IsVerticalViewMode,
			ViewModel.IsWorkAffixMode
		);
		HakoRemarksView.IsVisible = ViewModel.IsHakoMode;
		VerticalStylePageRemarksView.IsVisible = ViewModel.IsVerticalViewMode;
		WorkAffixView.IsVisible = ViewModel.IsWorkAffixMode;

		if (!ViewModel.IsHakoMode && HakoRemarksView.IsOpen)
		{
			HakoRemarksView.IsOpen = false;
		}
		if (!ViewModel.IsVerticalViewMode && VerticalStylePageRemarksView.IsOpen)
		{
			VerticalStylePageRemarksView.IsOpen = false;
		}

		UpdateOrientation();
	}

	void UpdateOrientation()
	{
		// Apply orientation locking only for phone devices
		if (DeviceInfo.Current.Idiom != DeviceIdiom.Phone)
		{
			logger.Debug("Device is not a phone, skipping orientation lock");
			return;
		}

		AppDisplayOrientation orientation = ViewModel.TabMode switch
		{
			DTACViewHostViewModel.Mode.Hako => AppDisplayOrientation.Portrait,
			DTACViewHostViewModel.Mode.VerticalView => AppDisplayOrientation.Landscape,
			_ => AppDisplayOrientation.All
		};

		logger.Info("Setting orientation to {0} for TabMode {1}", orientation, ViewModel.TabMode);
		InstanceManager.OrientationService.SetOrientation(orientation);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		UpdateOrientation();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();

		// Reset orientation to allow all when leaving ViewHost
		if (DeviceInfo.Current.Idiom == DeviceIdiom.Phone)
		{
			logger.Debug("Leaving ViewHost, resetting orientation to All");
			InstanceManager.OrientationService.SetOrientation(AppDisplayOrientation.All);
		}

		// Disable wake lock when leaving ViewHost
		if (InstanceManager.ScreenWakeLockService.IsWakeLockEnabled)
		{
			logger.Debug("Leaving ViewHost, disabling wake lock");
			InstanceManager.ScreenWakeLockService.DisableWakeLock();
		}
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
			InstanceManager.CrashlyticsWrapper.Log(ex, "ViewHost.TitleLabel_Tapped");
			await Utils.ExitWithAlert(ex);
		}
	}
}
