using TRViS.Controls;

namespace TRViS.DTAC;

public class WorkAffix : ContentView
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	public WorkAffix()
	{
		logger.Trace("Creating...");

		BackgroundColor = Colors.White;

		DTACElementStyles.DefaultBGColor.Apply(this, BackgroundColorProperty);

		logger.Trace("Created");
	}
}
