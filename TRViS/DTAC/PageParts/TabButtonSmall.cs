using TRViS.Services;

namespace TRViS.DTAC.PageParts;

/// <summary>
/// TabButtonの小型版
/// </summary>
public class TabButtonSmall : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();

	private static readonly Color BOTTOM_LINE_COLOR = Color.FromRgba(0, 0x88, 0, 0xFF);
	private const float SELECTED_SHADOW_OPACITY = 0.2f;
	private const float UNSELECTED_SHADOW_OPACITY = 0f;
	private const double BOTTOM_LINE_HEIGHT = 4;
	private const double BUTTON_CORNER_RADIUS = 4;
	private const double LINE_MARGIN_LR = 12;
	private const double BUTTON_MARGIN_TB = 4;

	private readonly BoxView BaseBox = new();
	private readonly Label ButtonLabel = new();
	private readonly BoxView BottomLineBox = new();
	private readonly Shadow ButtonBoxShadow = new();

	private bool _isSelected;

	public bool IsSelected
	{
		get => _isSelected;
		set
		{
			if (_isSelected == value)
				return;
			_isSelected = value;
			UpdateStyles();
		}
	}

	public string ButtonText
	{
		get => ButtonLabel?.Text ?? string.Empty;
		set => ButtonLabel?.Text = value;
	}

	public TabButtonSmall()
	{
		logger.Trace("Creating QuickSwitchTabButton...");

		InitializeComponents();
		UpdateStyles();

		logger.Trace("QuickSwitchTabButton created");
	}

	private void InitializeComponents()
	{
		// Create BoxView (background)
		BaseBox.Color = TabButton.BASE_COLOR_DISABLED;
		BaseBox.CornerRadius = BUTTON_CORNER_RADIUS;
		BaseBox.Margin = new Thickness(0, 0);

		// Create Shadow
		ButtonBoxShadow.Brush = Colors.Black;
		ButtonBoxShadow.Offset = new Point(2, 2);
		ButtonBoxShadow.Radius = 2;
		ButtonBoxShadow.Opacity = UNSELECTED_SHADOW_OPACITY;
		BaseBox.Shadow = ButtonBoxShadow;

		// Create Label (text)
		ButtonLabel.FontFamily = "Hiragino Sans";
		ButtonLabel.FontSize = 14;
		ButtonLabel.FontAttributes = FontAttributes.Bold;
		ButtonLabel.HorizontalOptions = LayoutOptions.Center;
		ButtonLabel.VerticalOptions = LayoutOptions.Center;
		ButtonLabel.Margin = new Thickness(0, BUTTON_MARGIN_TB, 0, BOTTOM_LINE_HEIGHT + BUTTON_MARGIN_TB);
		DTACElementStyles.TimetableTextColor.Apply(ButtonLabel, Label.TextColorProperty);

		// Create BoxView for bottom line (instead of Line control)
		BottomLineBox.Color = BOTTOM_LINE_COLOR;
		BottomLineBox.HeightRequest = BOTTOM_LINE_HEIGHT;
		BottomLineBox.Margin = new Thickness(LINE_MARGIN_LR, 0);
		BottomLineBox.HorizontalOptions = LayoutOptions.Fill;
		BottomLineBox.VerticalOptions = LayoutOptions.End;
		BottomLineBox.IsVisible = false;

		// Create container grid with gesture recognizer
		var tapGesture = new TapGestureRecognizer();
		tapGesture.Tapped += OnTapped;
		GestureRecognizers.Add(tapGesture);

		Add(BaseBox);
		Add(BottomLineBox);
		Add(ButtonLabel);
	}

	private void UpdateStyles()
	{
		logger.Trace("UpdateStyles: IsSelected={0}", _isSelected);

		if (_isSelected)
		{
			DTACElementStyles.DefaultBGColor.Apply(BaseBox, BoxView.ColorProperty);
			ButtonBoxShadow.Opacity = SELECTED_SHADOW_OPACITY;
			BottomLineBox.IsVisible = true;
		}
		else
		{
			DTACElementStyles.TabButtonBGColor.Apply(BaseBox, BoxView.ColorProperty);
			ButtonBoxShadow.Opacity = UNSELECTED_SHADOW_OPACITY;
			BottomLineBox.IsVisible = false;
		}
	}

	public event EventHandler? Tapped;

	private void OnTapped(object? sender, EventArgs e)
	{
		logger.Info("QuickSwitchTabButton tapped: {0}", ButtonText);
		Tapped?.Invoke(this, e);
	}
}
