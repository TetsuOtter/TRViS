using DependencyPropertyGenerator;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<double>("FontSize_Large")]
[DependencyProperty<DTACMarkerViewModel>("MarkerSettings")]
public partial class TimetableHeader : Grid
{
	public TimetableHeader()
	{
		InitializeComponent();
	}

	partial void OnMarkerSettingsChanged(DTACMarkerViewModel? newValue)
	{
		MarkerBtn.MarkerSettings = newValue;
	}
}
