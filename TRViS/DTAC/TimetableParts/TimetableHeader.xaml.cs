using DependencyPropertyGenerator;

namespace TRViS.DTAC;

[DependencyProperty<double>("FontSize_Large")]
public partial class TimetableHeader : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public TimetableHeader()
	{
		logger.Trace("Creating...");

		InitializeComponent();

		logger.Trace("Created");
	}
}
