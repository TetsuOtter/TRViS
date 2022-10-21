using DependencyPropertyGenerator;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsMarkModeToggled")]
public partial class MarkerButton : Frame
{
	public MarkerButton()
	{
		InitializeComponent();
	}
}
