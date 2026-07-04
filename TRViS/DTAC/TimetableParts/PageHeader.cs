using DependencyPropertyGenerator;

using TRViS.Services;
using TRViS.Utils;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsOpen")]
[DependencyProperty<bool>("HasHorizontalTimetable")]
public partial class PageHeader : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	const double HORIZONTAL_TIMETABLE_BUTTON_COLUMN_WIDTH = 176;
	readonly ColumnDefinition HorizontalTimetableButtonColumn = new(0);

	#region Affect Date Label

	readonly Label AffectDateLabel = DTACElementStyles.AffectDateLabelStyle<Label>();

	string _AffectDateLabelText = "";
	public string AffectDateLabelText
	{
		get => _AffectDateLabelText;
		set
		{
			if (_AffectDateLabelText == value)
			{
				logger.Trace("newValue: {0} (unchanged)", value);
				return;
			}

			_AffectDateLabelText = value;

			AffectDateLabel.Text = DTACElementStyles.AffectDateLabelTextPrefix + value;
			logger.Info("AffectDateLabelText: {0}", value);
		}
	}
	#endregion

	#region Start / End Run Button
	readonly StartEndRunButton StartEndRunButton = new();

	public Action? StartButtonTappedCallback { get; set; }

	public bool IsRunning
	{
		get => StartEndRunButton.IsChecked;
		set
		{
			StartEndRunButton.IsChecked = value;
			UpdateLocationServiceButtonStatus();
			logger.Info("IsRunning: {0}", value);
		}
	}

	private void StartEndRunButton_IsCheckedChanged(object? sender, ValueChangedEventArgs<bool> e)
	{
		logger.Trace("newValue: {0}", e.NewValue);

		UpdateLocationServiceButtonStatus();
	}
	#endregion

	#region Location Service Button
	readonly LocationServiceButton LocationServiceButton = new();
	private bool _CanUseLocationService = false;
	public bool CanUseLocationService
	{
		get => _CanUseLocationService;
		set
		{
			if (_CanUseLocationService == value)
				return;
			_CanUseLocationService = value;
			UpdateLocationServiceButtonStatus();
			if (!value)
				LocationServiceButton.IsChecked = false;
			logger.Info("CanUseLocationService: {0}", value);
		}
	}
	void UpdateLocationServiceButtonStatus() => LocationServiceButton.IsEnabled = CanUseLocationService && IsRunning;

	public Action? LocationServiceButtonTappedCallback { get; set; }

	private bool _isLocationServiceEnabled = false;
	public bool IsLocationServiceEnabled
	{
		get => _isLocationServiceEnabled;
		set
		{
			if (_isLocationServiceEnabled == value)
				return;
			_isLocationServiceEnabled = value;
			LocationServiceButton.IsChecked = value;
			logger.Info("IsLocationServiceEnabled: {0}", value);
		}
	}
	#endregion

	#region Open / Close Button
	readonly OpenCloseButton OpenCloseButton = new();

	public event EventHandler<ValueChangedEventArgs<bool>>? IsOpenChanged
	{
		add => OpenCloseButton.IsOpenChanged += value;
		remove => OpenCloseButton.IsOpenChanged -= value;
	}

	partial void OnIsOpenChanged(bool newValue)
	{
		logger.Info("OpenCloseButton.IsOpen: {0}", newValue);
		OpenCloseButton.IsOpen = newValue;
	}

	private void OpenCloseButton_IsOpenChanged(object? sender, ValueChangedEventArgs<bool> e)
	{
		logger.Trace("newValue: {0}", e.NewValue);
		IsOpen = e.NewValue;
	}
	#endregion

	#region Horizontal Timetable Button
	public const string HorizontalTimetableButtonAutomationId = "DTAC.HorizontalTimetableButton";

	readonly HorizontalTimetableButton HorizontalTimetableButtonBorder;

	partial void OnHasHorizontalTimetableChanged(bool newValue)
	{
		logger.Info("HasHorizontalTimetable: {0}", newValue);
		HorizontalTimetableButtonBorder.IsVisible = newValue;
		// Shadow is wired through MAUI's PlatformWrapperView on Android; leaving it
		// attached while hidden has been observed to allocate a backing bitmap that
		// OOMs the Glide LruBitmapPool on landscape rotation. Toggle in sync with
		// visibility so the shadow path runs only when the button is on-screen.
		HorizontalTimetableButtonBorder.SetShadowVisible(newValue);
		// 列を 0 に潰さないと、ボタン非表示時にも 110px の余白が残ってしまい
		// 行路施行日ラベルとの間に空きが生じる。表示状態に合わせて列幅を切り替える。
		HorizontalTimetableButtonColumn.Width = newValue
			? new GridLength(HORIZONTAL_TIMETABLE_BUTTON_COLUMN_WIDTH)
			: new GridLength(0);
	}

	private async void HorizontalTimetableButton_Tapped(object? sender, TappedEventArgs e)
	{
		logger.Info("HorizontalTimetableButton_Tapped");
		await Shell.Current.GoToAsync(HorizontalTimetablePage.NameOfThisClass, true);
	}
	#endregion

	#region Full Scroll Button
	public const string FullScrollButtonAutomationId = "DTAC.FullScrollButton";
	const double FULL_SCROLL_BUTTON_COLUMN_WIDTH = 60;
	readonly ColumnDefinition FullScrollButtonColumn = new(0);
	readonly Button FullScrollButton;

	private bool _IsFullScrollButtonVisible;
	/// <summary>
	/// Shows the entry button that navigates to the separated full-scroll D-TAC
	/// page (#155). Only set true by the non-full-scroll, iPhone-idiom
	/// VerticalStylePage; the full-scroll page itself hides it (no point
	/// offering "go to full scroll" while already there).
	/// </summary>
	public bool IsFullScrollButtonVisible
	{
		get => _IsFullScrollButtonVisible;
		set
		{
			if (_IsFullScrollButtonVisible == value)
				return;
			_IsFullScrollButtonVisible = value;
			FullScrollButton.IsVisible = value;
			FullScrollButtonColumn.Width = value
				? new GridLength(FULL_SCROLL_BUTTON_COLUMN_WIDTH)
				: new GridLength(0);
			logger.Info("IsFullScrollButtonVisible: {0}", value);
		}
	}

	private async void FullScrollButton_Clicked(object? sender, EventArgs e)
	{
		logger.Info("FullScrollButton_Clicked");
		await Shell.Current.GoToAsync(FullScrollVerticalTimetablePage.NameOfThisClass, true);
	}
	#endregion

	public PageHeader()
	{
		logger.Trace("Creating...");

		ColumnDefinitions = new ColumnDefinitionCollection(
			new ColumnDefinition(new GridLength(1, GridUnitType.Star)),
			FullScrollButtonColumn,
			HorizontalTimetableButtonColumn,
			new ColumnDefinition(176),
			new ColumnDefinition(134),
			new ColumnDefinition(60));

		FullScrollButton = new Button
		{
			AutomationId = FullScrollButtonAutomationId,
			Text = TRViS.Utils.MaterialIcons.Fullscreen,
			FontFamily = DTACElementStyles.MaterialIconFontFamily,
			FontSize = 32,
			FontAutoScalingEnabled = false,
			Padding = 0,
			Margin = new(2, 8),
			IsVisible = false,
		};
		DTACElementStyles.OpenCloseButtonBGColor.Apply(FullScrollButton, BackgroundColorProperty);
		DTACElementStyles.HorizontalTimetableButtonTextColor.Apply(FullScrollButton, Button.TextColorProperty);
		FullScrollButton.Clicked += FullScrollButton_Clicked;

		HorizontalTimetableButtonBorder = new HorizontalTimetableButton();
		// HasHorizontalTimetable defaults to false but OnHasHorizontalTimetableChanged
		// won't fire on the initial value, so make the hidden state explicit so the
		// Border doesn't sit in the accessibility tree as a focusable phantom.
		HorizontalTimetableButtonBorder.IsVisible = false;
		var horizontalTimetableTap = new TapGestureRecognizer();
		horizontalTimetableTap.Tapped += HorizontalTimetableButton_Tapped;
		HorizontalTimetableButtonBorder.GestureRecognizers.Add(horizontalTimetableTap);

		StartEndRunButton.Margin = new(2, 8);
		StartEndRunButton.IsCheckedChanged += StartEndRunButton_IsCheckedChanged;
		StartEndRunButton.Tapped += (_, _) =>
		{
			logger.Info("StartEndRunButton tapped");
			StartButtonTappedCallback?.Invoke();
		};

		LocationServiceButton.IsEnabled = false;
		LocationServiceButton.Margin = new(4, 8, 4, 10);
		LocationServiceButton.Tapped += (_, _) =>
		{
			logger.Info("LocationServiceButton tapped");
			LocationServiceButtonTappedCallback?.Invoke();
		};

		OpenCloseButton.AutomationId = "DTAC.OpenCloseButton";
		OpenCloseButton.TextWhenOpen = "\xe5ce";
		OpenCloseButton.TextWhenClosed = "\xe5cf";
		OpenCloseButton.IsOpenChanged += OpenCloseButton_IsOpenChanged;
		OpenCloseButton.HorizontalOptions = LayoutOptions.Center;
		OpenCloseButton.VerticalOptions = LayoutOptions.Center;

		this.Add(
			AffectDateLabel,
			column: 0
		);
		this.Add(FullScrollButton,
			column: 1
		);
		this.Add(HorizontalTimetableButtonBorder,
			column: 2
		);
		this.Add(StartEndRunButton,
			column: 3
		);
		this.Add(LocationServiceButton,
			column: 4
		);
		this.Add(
			OpenCloseButton,
			column: 5
		);

		logger.Trace("Created");
	}
}
