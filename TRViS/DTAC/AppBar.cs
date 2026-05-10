using System.ComponentModel;

using TRViS.Services;
using TRViS.Utils;
using TRViS.ViewModels;

namespace TRViS.DTAC;

/// <summary>
/// Shared title bar component. Owns the title/back/time/theme/app-icon controls;
/// callers toggle individual features via the Is*Enabled properties.
/// </summary>
public class AppBar : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	public const double TITLE_VIEW_HEIGHT = 50;
	public const string CHANGE_THEME_BUTTON_TEXT_TO_LIGHT = "\uE518";
	public const string CHANGE_THEME_BUTTON_TEXT_TO_DARK = "\uE51C";
	// 時刻表示が160px、残りはアイコンとWorkName分
	const int TIME_LABEL_VISIBLE_MIN_PARENT_WIDTH = (160 + 90) * 2;

	readonly AppViewModel _appViewModel;
	readonly EasterEggPageViewModel _eevm;

	readonly BoxView TitleBGBoxView;
	readonly BoxView TitleBGGradientBox;
	readonly Button LeftButton;
	readonly Label TitleLabel;
	readonly TapGestureRecognizer TitleTapRecognizer;
	readonly HorizontalStackLayout RightStack;
	readonly ImageButton AppIconButton;
	readonly Button ThemeButton;
	readonly Label TimeLabel;

	readonly GradientStop TitleBG_Top = new(Colors.White.WithAlpha(0.8f), 0);
	readonly GradientStop TitleBG_Middle = new(Colors.White.WithAlpha(0.5f), 0.5f);
	readonly GradientStop TitleBG_MidBottom = new(Colors.White.WithAlpha(0.1f), 0.8f);
	readonly GradientStop TitleBG_Bottom = new(Colors.White.WithAlpha(0), 1);

	double _lastWidth = 0;
	Thickness _lastSafeAreaMargin = new();

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

	public string LeftButtonAutomationId
	{
		get => LeftButton.AutomationId;
		set => LeftButton.AutomationId = value;
	}

	public string TitleLabelAutomationId
	{
		get => TitleLabel.AutomationId;
		set => TitleLabel.AutomationId = value;
	}

	public string TimeLabelAutomationId
	{
		get => TimeLabel.AutomationId;
		set => TimeLabel.AutomationId = value;
	}

	public string TimeLabelText
	{
		get => TimeLabel.Text ?? string.Empty;
		set => TimeLabel.Text = value;
	}

	bool _isTimeLabelEnabled;
	public bool IsTimeLabelEnabled
	{
		get => _isTimeLabelEnabled;
		set
		{
			if (_isTimeLabelEnabled == value)
				return;
			_isTimeLabelEnabled = value;
			UpdateTimeLabelVisibility();
		}
	}

	bool _isThemeButtonEnabled;
	public bool IsThemeButtonEnabled
	{
		get => _isThemeButtonEnabled;
		set
		{
			if (_isThemeButtonEnabled == value)
				return;
			_isThemeButtonEnabled = value;
			ThemeButton.IsVisible = value;
		}
	}

	bool _isAppIconButtonEnabled;
	public bool IsAppIconButtonEnabled
	{
		get => _isAppIconButtonEnabled;
		set
		{
			if (_isAppIconButtonEnabled == value)
				return;
			_isAppIconButtonEnabled = value;
			AppIconButton.IsVisible = value;
		}
	}

	bool _isTitleTappable;
	public bool IsTitleTappable
	{
		get => _isTitleTappable;
		set
		{
			if (_isTitleTappable == value)
				return;
			_isTitleTappable = value;
			if (value)
				TitleLabel.GestureRecognizers.Add(TitleTapRecognizer);
			else
				TitleLabel.GestureRecognizers.Remove(TitleTapRecognizer);
		}
	}

	public event EventHandler? LeftButtonClicked
	{
		add => LeftButton.Clicked += value;
		remove => LeftButton.Clicked -= value;
	}

	public event EventHandler? TitleTapped;

	public AppBar()
	{
		logger.Trace("Creating...");

		_appViewModel = InstanceManager.AppViewModel;
		_eevm = InstanceManager.EasterEggPageViewModel;

		TitlePaddingViewHeight = new RowDefinition(0);

		SafeAreaEdges = SafeAreaEdges.None;
		RowDefinitions = new RowDefinitionCollection
		{
			TitlePaddingViewHeight,
			new RowDefinition(TITLE_VIEW_HEIGHT)
		};

		TitleBGBoxView = new BoxView
		{
			Margin = new Thickness(-100, -100, -100, 0)
		};
		TitleBGBoxView.SetBinding(BoxView.ColorProperty, BindingBase.Create(static (EasterEggPageViewModel vm) => vm.ShellBackgroundColor, source: _eevm));
		Grid.SetRow((BindableObject)TitleBGBoxView, 0);
		Grid.SetRowSpan((BindableObject)TitleBGBoxView, 2);
		Children.Add(TitleBGBoxView);

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

		LeftButton = new Button
		{
			Margin = new Thickness(8, 4),
			Padding = 0,
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.Center,
			Text = DTACElementStyles.MenuIcon,
			FontFamily = DTACElementStyles.MaterialIconFontFamily,
			FontSize = 36,
			FontAutoScalingEnabled = false,
			BackgroundColor = Colors.Transparent,
			TextColor = _eevm.ShellTitleTextColor
		};
		Grid.SetRow((BindableObject)LeftButton, 1);
		Children.Add(LeftButton);

		TitleLabel = new Label
		{
			FontFamily = string.Empty,
			Margin = new Thickness(4, 8),
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.End,
			FontAutoScalingEnabled = false,
			FontAttributes = FontAttributes.Bold,
			FontSize = 20,
			TextColor = _eevm.ShellTitleTextColor
		};
		Grid.SetRow((BindableObject)TitleLabel, 1);
		Children.Add(TitleLabel);

		TitleTapRecognizer = new TapGestureRecognizer();
		TitleTapRecognizer.Tapped += (s, e) => TitleTapped?.Invoke(this, EventArgs.Empty);

		AppIconButton = new ImageButton
		{
			Aspect = Aspect.AspectFill,
			Margin = new Thickness(12, 8),
			HeightRequest = 30,
			WidthRequest = 30,
			Padding = 0,
			CornerRadius = 7,
			Source = DTACElementStyles.AppIconSource,
			IsVisible = false,
		};
		DTACElementStyles.AppIconBgColor.Apply(AppIconButton, BackgroundColorProperty);
		AppIconButton.Clicked += OnAppIconButtonClicked;

		ThemeButton = new Button
		{
			Margin = new Thickness(0, 6),
			Padding = 0,
			FontFamily = DTACElementStyles.MaterialIconFontFamily,
			FontSize = 28,
			FontAutoScalingEnabled = false,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.End,
			BackgroundColor = Colors.Transparent,
			TextColor = _eevm.ShellTitleTextColor,
			IsVisible = false,
		};
		ThemeButton.Clicked += OnThemeButtonClicked;

		TimeLabel = new Label
		{
			Text = "00:00:00",
			Margin = 0,
			Padding = 0,
			FontFamily = DTACElementStyles.TimetableNumFontFamily,
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.End,
			VerticalTextAlignment = TextAlignment.End,
			FontAutoScalingEnabled = false,
			FontAttributes = FontAttributes.Bold,
			FontSize = 40,
			TextColor = _eevm.ShellTitleTextColor,
			IsVisible = false,
		};

		RightStack = new HorizontalStackLayout
		{
			Margin = new Thickness(8, 4),
			Padding = 0,
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.End,
			Spacing = 8,
		};
		RightStack.Children.Add(AppIconButton);
		RightStack.Children.Add(ThemeButton);
		RightStack.Children.Add(TimeLabel);
		Grid.SetRow((BindableObject)RightStack, 1);
		Children.Add(RightStack);

		_appViewModel.CurrentAppThemeChanged += OnAppThemeChanged;
		SetTitleBGGradientColor(_appViewModel.CurrentAppTheme);
		UpdateThemeButtonText(_appViewModel.CurrentAppTheme);
		_eevm.PropertyChanged += Eevm_PropertyChanged;

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

	void OnAppThemeChanged(object? sender, ValueChangedEventArgs<AppTheme> e)
	{
		SetTitleBGGradientColor(e.NewValue);
		UpdateThemeButtonText(e.NewValue);
	}

	void UpdateThemeButtonText(AppTheme v)
	{
		ThemeButton.Text = v == AppTheme.Dark
			? CHANGE_THEME_BUTTON_TEXT_TO_LIGHT
			: CHANGE_THEME_BUTTON_TEXT_TO_DARK;
	}

	private void Eevm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not EasterEggPageViewModel vm)
			return;

		switch (e.PropertyName)
		{
			case nameof(EasterEggPageViewModel.ShellTitleTextColor):
				logger.Trace("ShellTitleTextColor is changed to {0}", vm.ShellTitleTextColor);
				TitleLabel.TextColor = vm.ShellTitleTextColor;
				LeftButton.TextColor = vm.ShellTitleTextColor;
				ThemeButton.TextColor = vm.ShellTitleTextColor;
				TimeLabel.TextColor = vm.ShellTitleTextColor;
				break;
		}
	}

	private void OnThemeButtonClicked(object? sender, EventArgs e)
	{
		logger.Info("ChangeThemeButton clicked");
		AppTheme newTheme = _appViewModel.CurrentAppTheme == AppTheme.Dark
			? AppTheme.Light
			: AppTheme.Dark;
		_appViewModel.CurrentAppTheme = newTheme;
		if (Application.Current is not null)
			Application.Current.UserAppTheme = newTheme;
	}

	private void OnAppIconButtonClicked(object? sender, EventArgs e)
	{
		bool newState = !_appViewModel.IsBgAppIconVisible;

		if (_appViewModel.CurrentAppTheme == AppTheme.Light && newState == false)
		{
			Util.DisplayAlertAsync(
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

		_lastSafeAreaMargin = newValue;

		TitleBGGradientBox.Margin = new(-newValue.Left, -top, -newValue.Right, TITLE_VIEW_HEIGHT * 0.5);
		TitlePaddingViewHeight.Height = new(top, GridUnitType.Absolute);
		LeftButton.Margin = new(8 + newValue.Left, 4);
		RightStack.Margin = new(8, 4, newValue.Right + 8, 4);
		UpdateTimeLabelVisibility();
		logger.Debug("SafeAreaMargin is changed -> set TitleBGGradientBox.Margin to {0}", Util.ThicknessToString(TitleBGGradientBox.Margin));
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		try
		{
			logger.Trace("width: {0}, height: {1}", width, height);
			_lastWidth = width;
			UpdateTimeLabelVisibility();
			base.OnSizeAllocated(width, height);
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			Util.ExitWithAlertAsync(ex);
		}
	}

	void UpdateTimeLabelVisibility()
	{
		if (!_isTimeLabelEnabled)
		{
			TimeLabel.IsVisible = false;
			return;
		}
		TimeLabel.IsVisible = (TIME_LABEL_VISIBLE_MIN_PARENT_WIDTH + _lastSafeAreaMargin.Right) < _lastWidth;
	}
}
