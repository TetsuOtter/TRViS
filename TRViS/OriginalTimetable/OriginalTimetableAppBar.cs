using TRViS.Services;

namespace TRViS.OriginalTimetable;


// Shared in-content title bar for the Original Timetable pages (V4/V6 use it;
// V1/V2 embed an equivalent masthead of their own). Mirrors DTAC's AppBar role:
// it replaces the Shell NavBar (which is hidden on non-Android) so the design's
// own chrome — hamburger / 行路番号 / live clock — sits at the top, and it owns
// the iOS safe-area top inset (Dynamic Island clearance) the NavBar used to give.
//
// Self-managing: subscribes to AppShell.SafeAreaMarginChanged, runs its own clock
// ticker off the freezable InstanceManager.TimeProvider (so the UI_TEST clock
// freeze keeps screenshots deterministic), and toggles the flyout on menu tap.
// Pages only set DepotText / WorkText (行路番号) and IsClockVisible.
public class OriginalTimetableAppBar : Grid
{
	const double BarHeight = 56;
	const string MenuIcon = "\uE5D2"; // Material "menu"

	readonly RowDefinition _safeAreaPad;
	readonly Label _menuLabel;
	readonly Border _menuButton;
	readonly Label _depotLabel;
	readonly Label _workLabel;
	readonly VerticalStackLayout _titleStack;
	readonly Label _clockLabel;

	IDispatcherTimer? _clockTimer;

	public static readonly BindableProperty DepotTextProperty = BindableProperty.Create(
		nameof(DepotText), typeof(string), typeof(OriginalTimetableAppBar), string.Empty,
		propertyChanged: (b, _, v) => ((OriginalTimetableAppBar)b).OnTitleTextChanged((string?)v, isWork: false));

	public static readonly BindableProperty WorkTextProperty = BindableProperty.Create(
		nameof(WorkText), typeof(string), typeof(OriginalTimetableAppBar), string.Empty,
		propertyChanged: (b, _, v) => ((OriginalTimetableAppBar)b).OnTitleTextChanged((string?)v, isWork: true));

	public static readonly BindableProperty IsClockVisibleProperty = BindableProperty.Create(
		nameof(IsClockVisible), typeof(bool), typeof(OriginalTimetableAppBar), true,
		propertyChanged: (b, _, v) => ((OriginalTimetableAppBar)b)._clockLabel.IsVisible = (bool)v);

	public string DepotText
	{
		get => (string)GetValue(DepotTextProperty);
		set => SetValue(DepotTextProperty, value);
	}

	public string WorkText
	{
		get => (string)GetValue(WorkTextProperty);
		set => SetValue(WorkTextProperty, value);
	}

	public bool IsClockVisible
	{
		get => (bool)GetValue(IsClockVisibleProperty);
		set => SetValue(IsClockVisibleProperty, value);
	}

	// AutomationId pass-throughs (set once from XAML; the inner controls exist by
	// construction time so plain CLR setters are enough for E2E targeting).
	public string MenuButtonAutomationId
	{
		get => _menuButton.AutomationId;
		set => _menuButton.AutomationId = value;
	}

	public string TitleAutomationId
	{
		get => _workLabel.AutomationId;
		set => _workLabel.AutomationId = value;
	}

	public string ClockAutomationId
	{
		get => _clockLabel.AutomationId;
		set => _clockLabel.AutomationId = value;
	}

	public OriginalTimetableAppBar()
	{
		// We manage the top inset manually via AppShell.SafeAreaMargin; don't let
		// MAUI also auto-inset this bar (would double the padding).
		SafeAreaEdges = SafeAreaEdges.None;

		_safeAreaPad = new RowDefinition(new GridLength(0));
		RowDefinitions = new RowDefinitionCollection
		{
			_safeAreaPad,
			new RowDefinition(new GridLength(BarHeight)),
		};

		this.SetAppThemeColor(BackgroundColorProperty, Res("OT_Bg_Light"), Res("OT_Bg_Dark"));

		var bar = new Grid
		{
			ColumnDefinitions = new ColumnDefinitionCollection
			{
				new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(GridLength.Star),
				new ColumnDefinition(GridLength.Auto),
			},
			ColumnSpacing = 8,
			Padding = new Thickness(8, 0, 16, 0),
		};
		SetRow((BindableObject)bar, 1);

		_menuLabel = new Label
		{
			Text = MenuIcon,
			FontFamily = "MaterialIconsRegular",
			FontSize = 24,
			FontAutoScalingEnabled = false,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
		};
		_menuLabel.SetAppThemeColor(Label.TextColorProperty, Res("OT_Fg_Light"), Res("OT_Fg_Dark"));

		_menuButton = new Border
		{
			WidthRequest = 44,
			HeightRequest = 44,
			Padding = 0,
			StrokeThickness = 0,
			BackgroundColor = Colors.Transparent,
			VerticalOptions = LayoutOptions.Center,
			Content = _menuLabel,
		};
		var menuTap = new TapGestureRecognizer();
		menuTap.Tapped += OnMenuTapped;
		_menuButton.GestureRecognizers.Add(menuTap);
		SetColumn((BindableObject)_menuButton, 0);
		bar.Children.Add(_menuButton);

		_depotLabel = new Label
		{
			FontSize = 11,
			FontAttributes = FontAttributes.Bold,
			CharacterSpacing = 1.5,
			FontAutoScalingEnabled = false,
			LineBreakMode = LineBreakMode.TailTruncation,
			IsVisible = false,
		};
		_depotLabel.SetAppThemeColor(Label.TextColorProperty, Res("OT_Muted_Light"), Res("OT_Muted_Dark"));

		_workLabel = new Label
		{
			FontSize = 17,
			FontAttributes = FontAttributes.Bold,
			FontAutoScalingEnabled = false,
			LineBreakMode = LineBreakMode.TailTruncation,
			IsVisible = false,
		};
		_workLabel.SetAppThemeColor(Label.TextColorProperty, Res("OT_Fg_Light"), Res("OT_Fg_Dark"));

		_titleStack = new VerticalStackLayout
		{
			Spacing = 0,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Start,
		};
		_titleStack.Children.Add(_depotLabel);
		_titleStack.Children.Add(_workLabel);
		SetColumn((BindableObject)_titleStack, 1);
		bar.Children.Add(_titleStack);

		_clockLabel = new Label
		{
			Text = "00:00:00",
			FontFamily = "Menlo",
			FontSize = 22,
			FontAttributes = FontAttributes.Bold,
			FontAutoScalingEnabled = false,
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Center,
		};
		_clockLabel.SetAppThemeColor(Label.TextColorProperty, Res("OT_Fg_Light"), Res("OT_Fg_Dark"));
		SetColumn((BindableObject)_clockLabel, 2);
		bar.Children.Add(_clockLabel);

		Children.Add(bar);

		// Bottom hairline rule.
		var rule = new BoxView
		{
			HeightRequest = 1,
			VerticalOptions = LayoutOptions.End,
		};
		rule.SetAppThemeColor(BoxView.ColorProperty, Res("OT_Rule_Light"), Res("OT_Rule_Dark"));
		SetRow((BindableObject)rule, 1);
		Children.Add(rule);

		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	static Color Res(string key)
		=> Application.Current?.Resources.TryGetValue(key, out var v) == true && v is Color c
			? c
			: Colors.Transparent;

	void OnTitleTextChanged(string? value, bool isWork)
	{
		var label = isWork ? _workLabel : _depotLabel;
		label.Text = value ?? string.Empty;
		label.IsVisible = !string.IsNullOrEmpty(value);
	}

	void OnMenuTapped(object? sender, TappedEventArgs e)
	{
		if (Shell.Current is not null)
			Shell.Current.FlyoutIsPresented = !Shell.Current.FlyoutIsPresented;
	}

	void OnLoaded(object? sender, EventArgs e)
	{
		if (Shell.Current is AppShell appShell)
		{
			// Idempotent: on iOS the OT pages are cached ShellContent, so Loaded can
			// fire again without a matching Unloaded — avoid double-subscription.
			appShell.SafeAreaMarginChanged -= OnSafeAreaMarginChanged;
			appShell.SafeAreaMarginChanged += OnSafeAreaMarginChanged;
			OnSafeAreaMarginChanged(appShell, new Thickness(), appShell.SafeAreaMargin);
		}
		StartClock();
	}

	void OnUnloaded(object? sender, EventArgs e)
	{
		if (Shell.Current is AppShell appShell)
			appShell.SafeAreaMarginChanged -= OnSafeAreaMarginChanged;
		StopClock();
	}

	void OnSafeAreaMarginChanged(object? sender, Thickness oldValue, Thickness newValue)
	{
		_safeAreaPad.Height = new GridLength(newValue.Top, GridUnitType.Absolute);
	}

	void StartClock()
	{
		UpdateClock();
		_clockTimer ??= Application.Current?.Dispatcher.CreateTimer();
		if (_clockTimer is null)
			return;
		_clockTimer.Interval = TimeSpan.FromSeconds(1);
		_clockTimer.Tick -= OnClockTick;
		_clockTimer.Tick += OnClockTick;
		_clockTimer.Start();
	}

	void StopClock() => _clockTimer?.Stop();

	void OnClockTick(object? sender, EventArgs e) => UpdateClock();

	void UpdateClock()
	{
		int sec = ((InstanceManager.TimeProvider.GetCurrentTimeSeconds() % 86400) + 86400) % 86400;
		string next = $"{sec / 3600:D2}:{sec % 3600 / 60:D2}:{sec % 60:D2}";
		if (next != _clockLabel.Text)
			_clockLabel.Text = next;
	}
}
