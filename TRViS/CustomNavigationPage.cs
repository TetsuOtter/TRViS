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
	private Grid AppBarGrid = null!;

	private Action<bool>? OnMenuButtonClicked { get; set; }

	readonly GradientStop BarBG_Top = new(Colors.White.WithAlpha(0.8f), 0);
	readonly GradientStop BarBG_Middle = new(Colors.White.WithAlpha(0.5f), 0.5f);
	readonly GradientStop BarBG_MidBottom = new(Colors.White.WithAlpha(0.1f), 0.8f);
	readonly GradientStop BarBG_Bottom = new(Colors.White.WithAlpha(0), 1);

	readonly FirebaseSettingViewModel FirebaseSettingViewModel = InstanceManager.FirebaseSettingViewModel;
	readonly EasterEggPageViewModel EasterEggPageViewModel = InstanceManager.EasterEggPageViewModel;

	public const string CHANGE_THEME_BUTTON_TEXT_TO_LIGHT = "\xe518";
	public const string CHANGE_THEME_BUTTON_TEXT_TO_DARK = "\xe51c";

	public CustomNavigationPage(ContentPage page, Action<bool>? onMenuClicked = null) : base(page)
	{
		logger.Trace("CustomNavigationPage Creating");

		OnMenuButtonClicked = onMenuClicked;

		// Create the custom AppBar
		CreateAppBar();

		// Setup navigation bar styling with gradients
		SetupNavigationBar();

		// Set time label text initially
		TimeLabel.Text = DateTime.Now.ToString("HH:mm");

		// Start time update timer
		SetupTimeUpdater();

		// Set the custom navigation bar title view
		SetTitleView(page, AppBarGrid);
		SetTitleIconImageSource(page, null);

		logger.Trace("CustomNavigationPage Created");
	}

	private void SetupTimeUpdater()
	{
		// Update time label periodically
		Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
		{
			try
			{
				TimeLabel.Text = DateTime.Now.ToString("HH:mm");
				return true; // Continue running
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Error updating time label");
				return true; // Keep timer running even on error
			}
		});
		logger.Debug("Time update timer started");
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
		};

		logger.Trace("Navigation bar setup complete");
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
			RowDefinitions = new RowDefinitionCollection { new RowDefinition(GridLength.Auto) },
			ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
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

		// Middle spacer (column 1)
		var spacer = new BoxView
		{
			HeightRequest = 0,
			BackgroundColor = Colors.Transparent,
		};
		Grid.SetColumn(spacer, 1);
		AppBarGrid.Add(spacer);

		// Right Controls
		var rightControls = new HorizontalStackLayout
		{
			Spacing = 0,
			Padding = new Thickness(0),
			Margin = new Thickness(0),
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.End,
		};

		TimeLabel = new Label
		{
			FontSize = 14,
			FontFamily = "RobotoMono",
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Start,
			Padding = new Thickness(8, 0, 4, 0),
			Text = DateTime.Now.ToString("HH:mm"),
			TextColor = Colors.Black,
			LineBreakMode = LineBreakMode.NoWrap,
		};
		rightControls.Add(TimeLabel);

		AppIconButton = new ImageButton
		{
			Source = "app_icon.png",
			Padding = new Thickness(8),
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			CornerRadius = 4,
			BorderWidth = 0,
		};
		AppIconButton.Clicked += (s, e) => OnToggleBgAppIconButtonClicked(s, e);
		rightControls.Add(AppIconButton);

		ChangeThemeButton = new Button
		{
			Text = CHANGE_THEME_BUTTON_TEXT_TO_LIGHT,
			FontFamily = "MaterialIconsRegular",
			FontSize = 28,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.End,
			Padding = new Thickness(12, 12, 16, 12),
			BackgroundColor = Colors.Transparent,
			TextColor = Colors.Black,
		};
		ChangeThemeButton.Clicked += OnChangeThemeButtonClicked;
		rightControls.Add(ChangeThemeButton);

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
