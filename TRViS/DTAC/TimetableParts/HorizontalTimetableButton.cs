using Microsoft.Maui.Controls.Shapes;

using TRViS.Services;

namespace TRViS.DTAC;

public class HorizontalTimetableButton : Border
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public HorizontalTimetableButton()
	{
		logger.Trace("Creating...");

		AutomationId = "DTAC.HorizontalTimetableButton";
		HorizontalOptions = LayoutOptions.Fill;
		VerticalOptions = LayoutOptions.Fill;
		Shadow = new Shadow
		{
			Brush = Colors.Black,
			Offset = new Point(1, 1),
			Radius = 2,
			Opacity = 0.4f
		};

		Stroke = Colors.Transparent;
		StrokeShape = new RoundRectangle
		{
			CornerRadius = 8
		};
		Margin = new(2, 8);
		Padding = 0;
		DTACElementStyles.OpenCloseButtonBGColor.Apply(this, Border.BackgroundColorProperty);
		Content = new Label
		{
			Text = "横型時刻表",
			FontSize = 28,
			FontFamily = DTACElementStyles.DefaultFontFamily,
			FontAttributes = FontAttributes.Bold,
			FontAutoScalingEnabled = false,
			VerticalOptions = LayoutOptions.Center,
			HorizontalOptions = LayoutOptions.Center,
			Margin = 4,
		};
		DTACElementStyles.DefaultTextColor.Apply(Content, Label.TextColorProperty);

		logger.Trace("Created");
	}
}
