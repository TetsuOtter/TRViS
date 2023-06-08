using System.ComponentModel;
using TRViS.ViewModels;

namespace TRViS.DTAC;

public partial class ViewHost : ContentPage
{
	static public readonly GridLength TitleViewHeight = new(45, GridUnitType.Absolute);

	DTACViewHostViewModel ViewModel { get; }

	readonly GradientStop TitleBG_Top = new(Colors.White.WithAlpha(0.8f), 0);
	readonly GradientStop TitleBG_Middle = new(Colors.White.WithAlpha(0.5f), 0.5f);
	readonly GradientStop TitleBG_MidBottom = new(Colors.White.WithAlpha(0.1f), 0.8f);
	readonly GradientStop TitleBG_Bottom = new(Colors.White.WithAlpha(0), 1);

	public ViewHost(AppViewModel vm, EasterEggPageViewModel eevm)
	{
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
	}

	void SetTitleBGGradientColor(AppTheme v)
		=> SetTitleBGGradientColor(v == AppTheme.Dark ? Colors.Black : Colors.White);
	void SetTitleBGGradientColor(Color v)
	{
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
			return;

		TitleBGGradientFrame.Margin = new(-newValue.Left, -top, -newValue.Right, 30);
	}

	private void MenuButton_Clicked(object? sender, EventArgs e)
	{
		Shell.Current.FlyoutIsPresented = !Shell.Current.FlyoutIsPresented;
	}

	private void Eevm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not EasterEggPageViewModel vm)
			return;

		switch (e.PropertyName)
		{
			case nameof(EasterEggPageViewModel.ShellTitleTextColor):
				TitleLabel.TextColor = MenuButton.TextColor = vm.ShellTitleTextColor;
				break;
		}
	}

	private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AppViewModel.SelectedWork))
			TitleLabel.Text = (sender as AppViewModel)?.SelectedWork?.Name;
	}

	private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(DTACViewHostViewModel.TabMode))
			UpdateContent();
	}

	void UpdateContent()
	{
		HakoView.IsVisible = ViewModel.IsHakoMode;
		VerticalStylePageRemarksView.IsVisible = ViewModel.IsVerticalViewMode;
		WorkAffixView.IsVisible = ViewModel.IsWorkAffixMode;
	}
}
