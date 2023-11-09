using System.ComponentModel;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class ViewHost : ContentPage
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	static public readonly GridLength TitleViewHeight = new(45, GridUnitType.Absolute);

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
		TitleLabel.TextColor = MenuButton.TextColor = eevm.ShellTitleTextColor;

		TitleBGBoxView.SetBinding(BoxView.ColorProperty, new Binding()
		{
			Source = eevm,
			Path = nameof(EasterEggPageViewModel.ShellBackgroundColor)
		});

		TitleBGGradientFrame.Background = new LinearGradientBrush(new GradientStopCollection()
		{
			TitleBG_Top,
			TitleBG_Middle,
			TitleBG_MidBottom,
			TitleBG_Bottom,
		},
		new Point(0, 0),
		new Point(0, 1));

		vm.CurrentAppThemeChanged += (s, e) => SetTitleBGGradientColor(e.NewValue);
		SetTitleBGGradientColor(vm.CurrentAppTheme);
		vm.PropertyChanged += Vm_PropertyChanged;
		eevm.PropertyChanged += Eevm_PropertyChanged;

		ViewModel = new(vm);
		BindingContext = ViewModel;

		ViewModel.PropertyChanged += ViewModel_PropertyChanged;

		VerticalStylePageView.SetBinding(VerticalStylePage.SelectedTrainDataProperty, new Binding()
		{
			Source = vm,
			Path = nameof(AppViewModel.SelectedTrainData)
		});

		VerticalStylePageRemarksView.SetBinding(WithRemarksView.RemarksDataProperty, new Binding()
		{
			Source = vm,
			Path = nameof(AppViewModel.SelectedTrainData)
		});

		UpdateContent();

		if (Shell.Current is AppShell appShell)
		{
			appShell.SafeAreaMarginChanged += AppShell_SafeAreaMarginChanged;
			AppShell_SafeAreaMarginChanged(appShell, new(), appShell.SafeAreaMargin);
		}

		DTACElementStyles.DefaultBGColor.Apply(this, BackgroundColorProperty);

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

		TitleBGGradientFrame.Margin = new(-newValue.Left, -top, -newValue.Right, 30);
		logger.Debug("SafeAreaMargin is changed -> set TitleBGGradientFrame.Margin to {0}", TitleBGGradientFrame.Margin);
	}

	private void MenuButton_Clicked(object? sender, EventArgs e)
	{
		Shell.Current.FlyoutIsPresented = !Shell.Current.FlyoutIsPresented;
		logger.Debug("FlyoutIsPresented is changed to {0}", Shell.Current.FlyoutIsPresented);
	}

	private void Eevm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not EasterEggPageViewModel vm)
			return;

		switch (e.PropertyName)
		{
			case nameof(EasterEggPageViewModel.ShellTitleTextColor):
				logger.Trace("ShellTitleTextColor is changed to {0}", vm.ShellTitleTextColor);
				TitleLabel.TextColor = MenuButton.TextColor = vm.ShellTitleTextColor;
				break;
		}
	}

	private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not AppViewModel vm)
			return;

		switch (e.PropertyName)
		{
			case nameof(AppViewModel.SelectedWork):
				OnSelectedWorkChanged(vm.SelectedWork);
				break;
		}
	}

	void OnSelectedWorkChanged(IO.Models.DB.Work? newValue)
	{
		string title = newValue?.Name ?? string.Empty;
		logger.Info("SelectedWork is changed to {0}", title);
		TitleLabel.Text = title;
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
		HakoView.IsVisible = ViewModel.IsHakoMode;
		VerticalStylePageRemarksView.IsVisible = ViewModel.IsVerticalViewMode;
		WorkAffixView.IsVisible = ViewModel.IsWorkAffixMode;
	}
}
