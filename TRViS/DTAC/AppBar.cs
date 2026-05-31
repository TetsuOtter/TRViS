using System.ComponentModel;

using Microsoft.Maui.Controls.Shapes;

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

	// WebSocket \u63A5\u7D9A\u30B9\u30C6\u30FC\u30BF\u30B9\u8868\u793A (#266) \u306E\u56FA\u5B9A\u8272\u3002\u80CC\u666F\u8272\u306B\u5DE6\u53F3\u3055\u308C\u306A\u3044\u3088\u3046
	// \u7E01\u53D6\u308A (StatusDot.Stroke) \u306F ShellTitleTextColor \u3092\u4F7F\u3046\u304C\u3001\u5857\u308A (\u7DD1/\u8D64) \u306F
	// \u72B6\u614B\u304C\u4E00\u76EE\u3067\u5206\u304B\u308B\u3088\u3046\u56FA\u5B9A\u306E\u9BAE\u3084\u304B\u306A\u8272\u306B\u3059\u308B\u3002
	static readonly Color StatusConnectedColor = Color.FromRgb(0x2E, 0x7D, 0x32);
	static readonly Color StatusDisconnectedColor = Color.FromRgb(0xC6, 0x28, 0x28);
	// Tap target for the status indicator. Mirrors
	// AutomationIds.AppBar.ConnectionStatusButton (test project). Not
	// UI_TEST-gated: it identifies a real interactive control (#266).
	const string StatusIndicatorAutomationId = "AppBar.ConnectionStatusButton";
	const double STATUS_DOT_SIZE = 18;
	// 切断時はタップで再接続確認ポップオーバーを出すため、見た目の丸 (18px) より
	// 広いタップ領域を確保する (#266)。
	const double STATUS_HIT_SIZE = 28;
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

	// WebSocket 接続ステータス表示 (#266)。StatusDot は常に「縁取りの輪」を兼ね、
	// 接続済/未接続では塗りに色が入り、再接続中は塗りを透明にして StatusSpinner
	// (ぐるぐる) を輪の中に重ねる。WebSocket 以外/未ロード時はコンテナごと非表示。
	readonly Grid StatusIndicator;
	readonly Ellipse StatusDot;
	readonly ActivityIndicator StatusSpinner;
#if UI_TEST
	// iOS XCUITest は Ellipse/ActivityIndicator を確実にはツリーへ出さないため、
	// ViewHost の TestTitleSeam と同じ方式で不可視ミラー Label に状態を映す。
	// 常に非空 (prefix 付き) なので必ず検索可能。テストは prefix を剥がして判定。
	const string StatusSeamAutomationId = "AppBar.ConnectionStatus";
	const string StatusSeamPrefix = "S:";
	readonly Label StatusSeamLabel;
#endif

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

		StatusDot = new Ellipse
		{
			WidthRequest = STATUS_DOT_SIZE,
			HeightRequest = STATUS_DOT_SIZE,
			StrokeThickness = 1.5,
			Stroke = new SolidColorBrush(_eevm.ShellTitleTextColor),
			Fill = new SolidColorBrush(StatusConnectedColor),
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			InputTransparent = true,
		};
		StatusSpinner = new ActivityIndicator
		{
			WidthRequest = STATUS_DOT_SIZE - 4,
			HeightRequest = STATUS_DOT_SIZE - 4,
			Color = _eevm.ShellTitleTextColor,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			IsVisible = false,
			IsRunning = false,
			InputTransparent = true,
		};
		StatusIndicator = new Grid
		{
			WidthRequest = STATUS_HIT_SIZE,
			HeightRequest = STATUS_HIT_SIZE,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
			BackgroundColor = Colors.Transparent,
			IsVisible = false,
		};
		StatusIndicator.Children.Add(StatusDot);
		StatusIndicator.Children.Add(StatusSpinner);
		// 透明 Button をオーバーレイしてタップを受ける。Grid + TapGestureRecognizer
		// は Windows (UIA) のアクセシビリティツリーに出ず Appium から掴めない
		// (#266: CI ui-test-windows で NoSuchElement)。リポジトリの各シームと
		// 同様、可搬性の高い透明 Button をタップ対象にする。切断時のみ動作する
		// ゲートはハンドラ側 (OnStatusIndicatorTapped) で行う。
		var statusTapButton = new Button
		{
			AutomationId = StatusIndicatorAutomationId,
			BackgroundColor = Colors.Transparent,
			BorderColor = Colors.Transparent,
			BorderWidth = 0,
			Padding = 0,
			Margin = 0,
		};
		statusTapButton.Clicked += OnStatusIndicatorTapped;
		StatusIndicator.Children.Add(statusTapButton);

		RightStack = new HorizontalStackLayout
		{
			Margin = new Thickness(8, 4),
			Padding = 0,
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.End,
			Spacing = 8,
		};
		// ステータスは AppIcon の左 (先頭) に置く。狭幅で TimeLabel が消えても
		// 隠れず、視線が最初に届く位置になる。
		RightStack.Children.Add(StatusIndicator);
		RightStack.Children.Add(AppIconButton);
		RightStack.Children.Add(ThemeButton);
		RightStack.Children.Add(TimeLabel);
		Grid.SetRow((BindableObject)RightStack, 1);
		Children.Add(RightStack);

#if UI_TEST
		StatusSeamLabel = new Label
		{
			AutomationId = StatusSeamAutomationId,
			Text = StatusSeamPrefix + ServerConnectionStatus.None,
			TextColor = Colors.Transparent,
			BackgroundColor = Colors.Transparent,
			InputTransparent = true,
			FontSize = 8,
			HorizontalOptions = LayoutOptions.Start,
			VerticalOptions = LayoutOptions.Start,
			WidthRequest = 24,
			HeightRequest = 24,
			Margin = 0,
			Padding = 0,
		};
		Grid.SetRow((BindableObject)StatusSeamLabel, 1);
		Children.Add(StatusSeamLabel);
#endif

		_appViewModel.CurrentAppThemeChanged += OnAppThemeChanged;
		SetTitleBGGradientColor(_appViewModel.CurrentAppTheme);
		UpdateThemeButtonText(_appViewModel.CurrentAppTheme);
		_eevm.PropertyChanged += Eevm_PropertyChanged;
		_appViewModel.PropertyChanged += AppViewModel_PropertyChanged;
		UpdateConnectionStatus();
		Loaded += OnLoaded;

		logger.Trace("Created");
	}

	void OnLoaded(object? sender, EventArgs e)
	{
		if (Width > 0)
			_lastWidth = Width;
		UpdateTimeLabelVisibility();
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
				// ステータス表示の縁取り/ぐるぐるはタイトル文字色に追従させる
				// (ヘッダ背景に対し常に視認できる色になる #266)。
				StatusDot.Stroke = new SolidColorBrush(vm.ShellTitleTextColor);
				StatusSpinner.Color = vm.ShellTitleTextColor;
				break;
		}
	}

	private void AppViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(AppViewModel.ServerConnectionStatus))
			UpdateConnectionStatus();
	}

	void UpdateConnectionStatus()
	{
		ServerConnectionStatus status = _appViewModel.ServerConnectionStatus;
		logger.Trace("ServerConnectionStatus -> {0}", status);
#if UI_TEST
		StatusSeamLabel.Text = StatusSeamPrefix + status;
#endif
		switch (status)
		{
			case ServerConnectionStatus.None:
				StatusIndicator.IsVisible = false;
				StatusSpinner.IsRunning = false;
				StatusSpinner.IsVisible = false;
				break;

			case ServerConnectionStatus.Connecting:
				StatusIndicator.IsVisible = true;
				// 塗りを透明にして輪 (縁取り) だけ残し、その中でぐるぐる回す。
				StatusDot.Fill = new SolidColorBrush(Colors.Transparent);
				StatusSpinner.IsVisible = true;
				StatusSpinner.IsRunning = true;
				break;

			case ServerConnectionStatus.Connected:
				StatusIndicator.IsVisible = true;
				StatusSpinner.IsRunning = false;
				StatusSpinner.IsVisible = false;
				StatusDot.Fill = new SolidColorBrush(StatusConnectedColor);
				break;

			case ServerConnectionStatus.Disconnected:
				StatusIndicator.IsVisible = true;
				StatusSpinner.IsRunning = false;
				StatusSpinner.IsVisible = false;
				StatusDot.Fill = new SolidColorBrush(StatusDisconnectedColor);
				break;
		}
	}

	async void OnStatusIndicatorTapped(object? sender, EventArgs e)
	{
		try
		{
			// 切断 (赤丸) のときだけ再接続確認を出す (#266)。接続済 (緑) /
			// 再接続中 (ぐるぐる) / 非表示では何もしない。
			if (_appViewModel.ServerConnectionStatus != ServerConnectionStatus.Disconnected)
				return;

			logger.Info("Status indicator tapped while Disconnected -> ask reconnect");

			// TR.Maui.AnchorPopover は Windows MAUI でバイナリ非互換
			// (Method not found: ElementExtensions.ToPlatform) でクラッシュする。
			// アプリ全体で使われている Util.DisplayAlertAsync の確認ダイアログに
			// 統一し、全プラットフォームで確実に動くようにする (#266)。
			bool doReconnect = await Util.DisplayAlertAsync(
				"再接続",
				"再接続しますか?",
				"はい",
				"いいえ");
			logger.Info("Reconnect confirm result: {0}", doReconnect);
			if (doReconnect)
				StartReconnect();
		}
		catch (Exception ex)
		{
			logger.Fatal(ex, "Unknown Exception");
			await Util.ExitWithAlertAsync(ex);
		}
	}

	async void StartReconnect()
	{
		logger.Info("Reconnect confirmed from AppBar status indicator");
		// 手動再接続中もぐるぐる表示にする。ReconnectWebSocketAsync は内部で
		// HandleWebSocketAppLinkAsync を呼び、成功時に両フラグを false に戻して
		// 独自に成功/失敗アラートを出す。失敗時は IsServerConnectionLost が true
		// のままなので、ここでは finally で再接続中フラグだけ確実に下ろす
		// (失敗なら赤丸へ、成功なら HandleWebSocketAppLinkAsync 側で緑へ)。
		_appViewModel.IsServerReconnecting = true;
		try
		{
			await _appViewModel.ReconnectWebSocketAsync(System.Threading.CancellationToken.None);
		}
		catch (Exception ex)
		{
			logger.Error(ex, "AppBar reconnect failed");
			InstanceManager.CrashlyticsWrapper.Log(ex, "AppBar.StartReconnect");
		}
		finally
		{
			_appViewModel.IsServerReconnecting = false;
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

		// XAML sets IsTimeLabelEnabled before first layout on some platforms
		// (especially cached ShellContent). In that window _lastWidth can still
		// be 0, so re-evaluate with current Width/Parent.Width when available.
		double width = _lastWidth > 0 ? _lastWidth : Width;
		if (width <= 0 && Parent is VisualElement parent && parent.Width > 0)
			width = parent.Width;

		if (width <= 0)
		{
			TimeLabel.IsVisible = false;
			return;
		}

		TimeLabel.IsVisible = (TIME_LABEL_VISIBLE_MIN_PARENT_WIDTH + _lastSafeAreaMargin.Right) < width;
	}
}
