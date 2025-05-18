using DependencyPropertyGenerator;

using TRViS.Services;

namespace TRViS.DTAC;

[DependencyProperty<double>("FontSize_Large")]
public partial class TimetableHeader : Grid
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public TimetableHeader()
	{
		logger.Trace("Creating...");

		InitializeComponent();

		DTACElementStyles.SetTimetableColumnWidthCollection(this);

		logger.Trace("Created");
	}
}
