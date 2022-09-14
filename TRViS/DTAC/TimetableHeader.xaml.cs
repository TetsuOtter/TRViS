using DependencyPropertyGenerator;

namespace TRViS.DTAC;

[DependencyProperty<Style>("LabelStyle")]
[DependencyProperty<Style>("SeparatorLineStyle")]
[DependencyProperty<double>("FontSize_Large")]
[DependencyProperty<bool>("IsMarkModeToggled")]
public partial class TimetableHeader : Grid
{
	public TimetableHeader()
	{
		InitializeComponent();
	}
}
