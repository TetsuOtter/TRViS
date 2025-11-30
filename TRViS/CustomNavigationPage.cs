using TRViS.DTAC;
using TRViS.Services;
using TRViS.ViewModels;

namespace TRViS;

public class CustomNavigationPage : NavigationPage
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	// AppBar Controls
	private Button MenuButton = null!;
	private ImageButton AppIconButton = null!;
	private Button ChangeThemeButton = null!;
	private Label TimeLabel = null!;
	private Label TitleLabel = null!;
	private Grid AppBarGrid = null!;

	private Action<bool>? OnMenuButtonClicked { get; set; }
	private bool ShowAppIconButton { get; set; } = false;

	public string? AppBarTitle
	{
		get => TitleLabel.Text; set => TitleLabel?.Text = value ?? string.Empty;
	}

	readonly EasterEggPageViewModel EasterEggPageViewModel = InstanceManager.EasterEggPageViewModel;

	public const string CHANGE_THEME_BUTTON_TEXT_TO_LIGHT = "\xe518";
	public const string CHANGE_THEME_BUTTON_TEXT_TO_DARK = "\xe51c";

	public CustomNavigationPage(ContentPage page, Action<bool>? onMenuClicked = null, bool showAppIconButton = false) : base(page)
	{
		logger.Trace("CustomNavigationPage Creating");

		OnMenuButtonClicked = onMenuClicked;
		ShowAppIconButton = showAppIconButton;

		// Create the custom AppBar
		CreateAppBar();

		// Setup navigation bar styling with gradients
		SetupNavigationBar();

		// Set time label text initially
		TimeLabel.Text = "00:00:00";

		// Start time update timer
		SetupTimeUpdater();

		// Set the custom navigation bar title view
		SetTitleView(page, AppBarGrid);
		SetHasBackButton(page, false);

		logger.Trace("CustomNavigationPage Created");
	}

	private void SetupTimeUpdater()
	{
		// Update time label using LocationService.TimeChanged event
		try
		{
			var locationService = InstanceManager.LocationService;
			locationService.TimeChanged += (s, totalSeconds) =>
			{
				bool isMinus = totalSeconds < 0;
				int Hour = Math.Abs(totalSeconds / 3600);
				int Minute = Math.Abs((totalSeconds % 3600) / 60);
				int Second = Math.Abs(totalSeconds % 60);

				string text = isMinus ? "-" : string.Empty;
				text += $"{Hour:D2}:{Minute:D2}:{Second:D2}";
				TimeLabel.Text = text;
			};
			logger.Debug("Time update via LocationService.TimeChanged started");
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Error setting up LocationService.TimeChanged event");
		}
	}

	private void SetupNavigationBar()
	{
		// Setup initial gradient
		UpdateBarBackgroundGradient();

		// Listen for theme changes
		var appVM = InstanceManager.AppViewModel;
		appVM.CurrentAppThemeChanged += (s, e) => UpdateBarBackgroundGradient();

		// Listen for background color changes
		var eevm = InstanceManager.EasterEggPageViewModel;
		eevm.PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(EasterEggPageViewModel.ShellBackgroundColor))
			{
				UpdateBarBackgroundGradient();
			}
			else if (e.PropertyName == nameof(EasterEggPageViewModel.ShellTitleTextColor))
			{
				UpdateTextColors();
			}
		};

		// Set initial text colors
		UpdateTextColors();

		logger.Trace("Navigation bar setup complete");
	}

	private void UpdateTextColors()
	{
		var eevm = InstanceManager.EasterEggPageViewModel;
		var textColor = eevm.ShellTitleTextColor;

		MenuButton.TextColor = textColor;
		ChangeThemeButton.TextColor = textColor;
		TimeLabel.TextColor = textColor;
		TitleLabel.TextColor = textColor;
	}

	private void UpdateBarBackgroundGradient()
	{
		var eevm = InstanceManager.EasterEggPageViewModel;
		var endColor = eevm.ShellBackgroundColor;

		// iOS12でグラデーションをうまく表示できないため
		if (DeviceInfo.Platform == DevicePlatform.iOS && DeviceInfo.Version.Major < 13)
		{
			BarBackgroundColor = endColor;
			return;
		}

		var appVM = InstanceManager.AppViewModel;
		var startColor = appVM.CurrentAppTheme == AppTheme.Dark ? Colors.Black : Colors.White;

		logger.Debug("UpdateBarBackgroundGradient - Start: {0}, End: {1}", startColor, endColor);

		// Blend colors for smooth gradient
		var blendedMidUp = BlendColors(startColor, endColor, 0.25f);
		var blendedMidDown = BlendColors(startColor, endColor, 0.75f);

		// Create the gradient brush
		var linearGradient = new LinearGradientBrush
		{
			GradientStops =
			[
				new GradientStop(startColor, 0),
				new GradientStop(blendedMidUp, 0.1f),
				new GradientStop(blendedMidDown, 0.3f),
				new GradientStop(endColor, 0.5f)
			],
			StartPoint = new Point(0.5, 0),
			EndPoint = new Point(0.5, 1)
		};

		// Apply MAUI gradient to BarBackground
		BarBackground = linearGradient;
	}
	private Color BlendColors(Color color1, Color color2, float blend)
	{
		// Blend two colors at a given ratio (0 = color1, 1 = color2)
		var r = (byte)(color1.Red * 255 * (1 - blend) + color2.Red * 255 * blend);
		var g = (byte)(color1.Green * 255 * (1 - blend) + color2.Green * 255 * blend);
		var b = (byte)(color1.Blue * 255 * (1 - blend) + color2.Blue * 255 * blend);
		var a = (byte)(color1.Alpha * 255 * (1 - blend) + color2.Alpha * 255 * blend);

		return Color.FromRgba(r, g, b, a);
	}
	private void CreateAppBar()
	{
		// Create AppBar grid with gradient background
		AppBarGrid = new Grid
		{
			RowDefinitions = [new RowDefinition(GridLength.Auto)],
			ColumnDefinitions = [
				new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Auto)
			],
			HeightRequest = 56,
			HorizontalOptions = LayoutOptions.Fill,
			VerticalOptions = LayoutOptions.Fill,
			Padding = new Thickness(0),
			Margin = new Thickness(0),
			BackgroundColor = Colors.Transparent,
		};

		// Menu Button
		MenuButton = new Button
		{
			Text = "\xe241",
			FontFamily = "MaterialIconsRegular",
			FontSize = 28,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Start,
			Padding = new Thickness(16, 12, 12, 12),
			BackgroundColor = Colors.Transparent,
			TextColor = Colors.Black,
		};
		MenuButton.Clicked += (s, e) => OnMenuButtonClicked?.Invoke(true);
		Grid.SetColumn(MenuButton, 0);
		AppBarGrid.Add(MenuButton);

		// Title Label (center)
		TitleLabel = new Label
		{
			FontSize = 20,
			FontFamily = "",
			Margin = new Thickness(4, 8),
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.End,
			FontAttributes = FontAttributes.Bold,
			LineBreakMode = LineBreakMode.NoWrap,
		};
		Grid.SetColumnSpan(TitleLabel, 3);
		AppBarGrid.Add(TitleLabel);

		// Right Controls
		var rightControls = new HorizontalStackLayout
		{
			Margin = new Thickness(8, 4),
			Padding = new Thickness(0),
			VerticalOptions = LayoutOptions.End,
			HorizontalOptions = LayoutOptions.End,
			Spacing = 8,
		};

		AppIconButton = new ImageButton
		{
			Aspect = Aspect.AspectFill,
			Margin = new Thickness(12, 8),
			HeightRequest = 30,
			WidthRequest = 30,
			Padding = new Thickness(0),
			CornerRadius = 7,
			Source = DTACElementStyles.AppIconSource,
			IsVisible = ShowAppIconButton,
		};
		DTACElementStyles.AppIconBgColor.Apply(AppIconButton, BackgroundColorProperty);
		AppIconButton.Clicked += (s, e) => OnToggleBgAppIconButtonClicked(s, e);
		rightControls.Add(AppIconButton);

		ChangeThemeButton = new Button
		{
			Text = CHANGE_THEME_BUTTON_TEXT_TO_LIGHT,
			Margin = new Thickness(0, 6),
			Padding = new Thickness(0),
			FontFamily = "MaterialIconsRegular",
			FontSize = 28,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.End,
			BackgroundColor = Colors.Transparent,
		};
		ChangeThemeButton.Clicked += OnChangeThemeButtonClicked;
		rightControls.Add(ChangeThemeButton);

		TimeLabel = new Label
		{
			Text = "00:00:00",
			Margin = new Thickness(0),
			Padding = new Thickness(0),
			FontFamily = DTAC.DTACElementStyles.TimetableNumFontFamily,
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.End,
			FontSize = 40,
			FontAttributes = FontAttributes.Bold,
		};
		rightControls.Add(TimeLabel);

		Grid.SetColumn(rightControls, 2);
		AppBarGrid.Add(rightControls);
	}

	private void OnToggleBgAppIconButtonClicked(object? sender, EventArgs e)
	{
		bool newState = !InstanceManager.AppViewModel.IsBgAppIconVisible;
		if (InstanceManager.AppViewModel.CurrentAppTheme == AppTheme.Light
				&& newState == false)
		{
			logger.Warn("IsBgAppIconVisible cannot be toggled in Light mode");
			return;
		}
		InstanceManager.AppViewModel.IsBgAppIconVisible = newState;
		logger.Debug("IsBgAppIconVisible is changed to {0}", newState);

		// Update button background color based on new state
		if (sender is VisualElement button)
		{
			if (newState)
			{
				// Try to apply the app icon background color from DTAC styles
				try
				{
					if (Application.Current?.Resources?.TryGetValue("AppIconBgColor", out var appIconBgColorObj) == true
						&& appIconBgColorObj is Color appIconBgColor)
					{
						button.BackgroundColor = appIconBgColor;
					}
					else
					{
						// Fallback if resource not found
						button.BackgroundColor = Colors.Gray;
					}
				}
				catch (Exception ex)
				{
					logger.Error(ex, "Error applying app icon background color");
					button.BackgroundColor = Colors.Gray;
				}
			}
			else
			{
				button.BackgroundColor = Colors.Transparent;
			}
		}
	}

	private void OnChangeThemeButtonClicked(object? sender, EventArgs e)
	{
		AppViewModel vm = InstanceManager.AppViewModel;
		AppTheme newTheme = vm.CurrentAppTheme == AppTheme.Dark
				? AppTheme.Light
				: AppTheme.Dark;

		if (Application.Current is not null)
		{
			logger.Info("CurrentAppTheme is changed to {0}", newTheme);
			vm.CurrentAppTheme = newTheme;
			Application.Current.UserAppTheme = newTheme;
			ChangeThemeButton.Text = newTheme == AppTheme.Dark
					? CHANGE_THEME_BUTTON_TEXT_TO_LIGHT
					: CHANGE_THEME_BUTTON_TEXT_TO_DARK;
		}
	}
}
