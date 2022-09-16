using DependencyPropertyGenerator;

namespace TRViS.DTAC;

[DependencyProperty<double>("FontSize_Large")]
[DependencyProperty<bool>("IsMarkModeToggled")]
public partial class TimetableHeader : Grid
{
	public TimetableHeader()
	{
		InitializeComponent();
	}
}
