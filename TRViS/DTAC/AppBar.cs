using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS.DTAC;

/// <summary>
/// Shared AppBar component for the title/header area.
/// Used by ViewHost and HorizontalTimetablePage.
/// </summary>
public class AppBar : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public const double TITLE_VIEW_HEIGHT = 50;

	readonly BoxView TitleBGBoxView;
	readonly BoxView TitleBGGradientBox;
	readonly Button LeftButton;
	readonly Label TitleLabel;

	readonly GradientStop TitleBG_Top = new(Colors.White.WithAlpha(0.8f), 0);
	readonly GradientStop TitleBG_Middle = new(Colors.White.WithAlpha(0.5f), 0.5f);
	readonly GradientStop TitleBG_MidBottom = new(Colors.White.WithAlpha(0.1f), 0.8f);
	readonly GradientStop TitleBG_Bottom = new(Colors.White.WithAlpha(0), 1);

	public RowDefinition TitlePaddingViewHeight { get; }

	public string Title
	{
		get => TitleLabel.Text ?? string.Empty;
		set => TitleLabel.Text = value;
	}

	public string LeftButtonText
	{
		get => LeftButton.Text ?? string.Empty;
		set => LeftButton.Text = value;
	}

	public event EventHandler? LeftButtonClicked
	{
		add => LeftButton.Clicked += value;
		remove => LeftButton.Clicked -= value;
	}

	public AppBar()
	{
		logger.Trace("Creating...");

		AppViewModel vm = InstanceManager.AppViewModel;
		EasterEggPageViewModel eevm = InstanceManager.EasterEggPageViewModel;

		TitlePaddingViewHeight = new RowDefinition(0);

		RowDefinitions = new RowDefinitionCollection
		{
			TitlePaddingViewHeight,
			new RowDefinition(TITLE_VIEW_HEIGHT)
		};

		// Background BoxView
		TitleBGBoxView = new BoxView
		{
			Margin = new Thickness(-100, -100, -100, 0)
		};
		TitleBGBoxView.SetBinding(BoxView.ColorProperty, BindingBase.Create(static (EasterEggPageViewModel vm) => vm.ShellBackgroundColor, source: eevm));
		Grid.SetRow((BindableObject)TitleBGBoxView, 0);
		Grid.SetRowSpan((BindableObject)TitleBGBoxView, 2);
		Children.Add(TitleBGBoxView);

		// Gradient BoxView
		TitleBGGradientBox = new BoxView
		{
			CornerRadius = 0,
			Margin = new Thickness(0, 0, 0, 30),
			Color = null,
			Background = new LinearGradientBrush(new GradientStopCollection
			{
				TitleBG_Top,
				TitleBG_Middle,
				TitleBG_MidBottom,
				TitleBG_Bottom,
			},
			new Point(0, 0),
			new Point(0, 1))
		};
		Grid.SetRow((BindableObject)TitleBGGradientBox, 0);
		Grid.SetRowSpan((BindableObject)TitleBGGradientBox, 2);
		Children.Add(TitleBGGradientBox);

		// Left button (Menu/Back)
		LeftButton = new Button
		{
			Margin = new Thickness(8, 4),
			Padding = 0,
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.Center,
			Text = "\ue241", // Menu icon
			FontFamily = DTACElementStyles.MaterialIconFontFamily,
			FontSize = 36,
			BackgroundColor = Colors.Transparent,
			TextColor = eevm.ShellTitleTextColor
		};
		Grid.SetRow((BindableObject)LeftButton, 1);
		Children.Add(LeftButton);

		// Title label
		TitleLabel = new Label
		{
			FontFamily = string.Empty,
			Margin = new Thickness(4, 8),
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.End,
			FontAttributes = FontAttributes.Bold,
			FontSize = 20,
			TextColor = eevm.ShellTitleTextColor
		};
		Grid.SetRow((BindableObject)TitleLabel, 1);
		Children.Add(TitleLabel);

		vm.CurrentAppThemeChanged += (s, e) => SetTitleBGGradientColor(e.NewValue);
		SetTitleBGGradientColor(vm.CurrentAppTheme);
		eevm.PropertyChanged += Eevm_PropertyChanged;

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

	private void Eevm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (sender is not EasterEggPageViewModel vm)
			return;

		switch (e.PropertyName)
		{
			case nameof(EasterEggPageViewModel.ShellTitleTextColor):
				logger.Trace("ShellTitleTextColor is changed to {0}", vm.ShellTitleTextColor);
				TitleLabel.TextColor = vm.ShellTitleTextColor;
				LeftButton.TextColor = vm.ShellTitleTextColor;
				break;
		}
	}

	public void UpdateSafeAreaMargin(Thickness oldValue, Thickness newValue)
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
		LeftButton.Margin = new(8 + newValue.Left, 4);
		logger.Debug("SafeAreaMargin is changed -> set TitleBGGradientBox.Margin to {0}", Utils.ThicknessToString(TitleBGGradientBox.Margin));
	}

	/// <summary>
	/// Add additional content to the right side of the AppBar (e.g., time label, theme button)
	/// </summary>
	public void AddRightContent(View content)
	{
		Grid.SetRow((BindableObject)content, 1);
		Children.Add(content);
	}
}
