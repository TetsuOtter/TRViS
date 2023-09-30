using DependencyPropertyGenerator;
using TRViS.ViewModels;

namespace TRViS.DTAC;

[DependencyProperty<double>("FontSize_Large")]
[DependencyProperty<DTACMarkerViewModel>("MarkerSettings")]
public partial class TimetableHeader : Grid
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	public TimetableHeader()
	{
		logger.Trace("Creating...");

		InitializeComponent();

		logger.Trace("Created");
	}

	partial void OnMarkerSettingsChanged(DTACMarkerViewModel? newValue)
	{
		logger.Trace("OnMarkerSettingsChanged({0})", newValue?.GetType().Name ?? "null");

		MarkerBtn.MarkerSettings = newValue;
	}
}
