using TRViS.Services;

namespace TRViS.DTAC;

public class WorkAffix : ContentView
{
	private static readonly NLog.Logger logger = LoggerService.GetGeneralLogger();
	public WorkAffix()
	{
		logger.Trace("Creating...");

		BackgroundColor = Colors.White;

		DTACElementStyles.Instance.DefaultBGColor.Apply(this, BackgroundColorProperty);

		logger.Trace("Created");
	}
}
