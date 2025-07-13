using Microsoft.Maui.Controls.Shapes;

using TRViS.Controls;
using TRViS.Services;

namespace TRViS.DTAC;

public partial class StartEndRunButton : ToggleButton
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	static readonly Color GREEN = new(0, 0x80, 0);
	static readonly Color GREEN_DARK = new(0, 0x70, 0);
	const float BUTTON_LUMINOUS_DELTA = 0.05f;

	static readonly AppThemeColorBindingExtension Color_Up = new(
		GREEN.AddLuminosity(BUTTON_LUMINOUS_DELTA),
		GREEN_DARK.AddLuminosity(BUTTON_LUMINOUS_DELTA)
	);
	static readonly AppThemeColorBindingExtension Color_Down = new(
		GREEN.AddLuminosity(-BUTTON_LUMINOUS_DELTA),
		GREEN_DARK.AddLuminosity(-BUTTON_LUMINOUS_DELTA)
	);

	private readonly Border baseBorder;
	private readonly HorizontalStackLayout stackLayout;
	private readonly Label iconLabel;
	private readonly Label textLabel;

	public StartEndRunButton()
	{
		logger.Trace("Creating...");

		iconLabel = new Label
		{
			Text = "\xe039",
			FontFamily = "MaterialIconsRegular",
			FontSize = 32,
			FontAutoScalingEnabled = false,
			VerticalOptions = LayoutOptions.Center,
			Margin = new Thickness(2),
			Padding = new Thickness(0)
		};
		DTACElementStyles.Instance.StartEndRunButtonTextColor.Apply(iconLabel, Label.TextColorProperty);

		textLabel = new Label
		{
			Text = "運行開始",
			FontFamily = "Hiragino Sans",
			FontSize = 24,
			FontAutoScalingEnabled = false,
			FontAttributes = FontAttributes.Bold,
			VerticalOptions = LayoutOptions.Center,
			Margin = new Thickness(4),
			Padding = new Thickness(0)
		};
		DTACElementStyles.Instance.StartEndRunButtonTextColor.Apply(textLabel, Label.TextColorProperty);

		stackLayout = new HorizontalStackLayout
		{
			ScaleY = 0.95,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center
		};
		stackLayout.Children.Add(iconLabel);
		stackLayout.Children.Add(textLabel);

		baseBorder = new Border
		{
			Margin = new Thickness(0),
			Padding = new Thickness(4),
			Stroke = Colors.Transparent,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			StrokeShape = new RoundRectangle { CornerRadius = 8 },
			Content = stackLayout
		};

		Shadow = new Shadow
		{
			Brush = Colors.Black,
			Offset = new Point(3, 3),
			Radius = 3,
			Opacity = 0.2f
		};

		Content = baseBorder;

		PropertyChanged += (s, e) =>
		{
			if (e.PropertyName == nameof(IsChecked))
			{
				if (IsChecked)
				{
					iconLabel.Text = "\xe14b";
					textLabel.Text = "運行終了";
				}
				else
				{
					iconLabel.Text = "\xe039";
					textLabel.Text = "運行開始";
				}
			}
		};

		LinearGradientBrush brush = new()
		{
			StartPoint = new(0, 0),
			EndPoint = new(0, 1)
		};

		GradientStop gradientStop_Up = new()
		{
			Offset = 0.1f,
		};
		GradientStop gradientStop_Down = new()
		{
			Offset = 1.0f,
		};

		Color_Up.Apply(gradientStop_Up, GradientStop.ColorProperty);
		Color_Down.Apply(gradientStop_Down, GradientStop.ColorProperty);

		brush.GradientStops.Add(gradientStop_Up);
		brush.GradientStops.Add(gradientStop_Down);

		baseBorder.Background = brush;

		logger.Trace("Created");
	}
}
