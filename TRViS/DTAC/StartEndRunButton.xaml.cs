using TRViS.Controls;

namespace TRViS.DTAC;

public partial class StartEndRunButton : ToggleButton
{
	static readonly Color GREEN = new(0, 0x80, 0);
	const float BUTTON_LUMINOUS_DELTA = 0.05f;

	public StartEndRunButton()
	{
		InitializeComponent();

		LinearGradientBrush brush = new()
		{
			StartPoint = new(0, 0),
			EndPoint = new(0, 1)
		};

		brush.GradientStops.Add(new(GREEN.AddLuminosity(BUTTON_LUMINOUS_DELTA), 0.1f));
		brush.GradientStops.Add(new(GREEN.AddLuminosity(-BUTTON_LUMINOUS_DELTA), 1.0f));

		BaseFrame.Background = brush;
	}
}
