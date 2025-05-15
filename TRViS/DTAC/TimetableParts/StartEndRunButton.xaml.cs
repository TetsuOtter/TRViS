using TRViS.Controls;

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

	public StartEndRunButton()
	{
		logger.Trace("Creating...");

		InitializeComponent();

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

		BaseBorder.Background = brush;

		logger.Trace("Created");
	}
}
