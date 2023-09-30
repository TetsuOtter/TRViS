namespace TRViS.DTAC;

public partial class Hako : ContentView
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	public Hako()
	{
		logger.Trace("Creating...");

		InitializeComponent();

		logger.Trace("Created");
	}
}
